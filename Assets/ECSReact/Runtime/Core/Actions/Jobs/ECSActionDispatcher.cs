using Unity.Entities;
using Unity.Jobs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ECSReact.Core
{
  /// <summary>
  /// Optimized ECSActionDispatcher that manages its own command buffers
  /// and handles high-volume action dispatching efficiently.
  /// Supports both main-thread and job-based dispatching with proper synchronization.
  /// </summary>
  public static class ECSActionDispatcher
  {
    // Thread-safe ECB management per world
    private static readonly ConcurrentDictionary<World, DispatchContext> contexts = new();

    // Thread-local current world for context-aware dispatching
    [ThreadStatic]
    private static World currentWorld;

    /// <summary>
    /// A command buffer cached until its EntityCommandBufferSystem next updates.
    ///
    /// An ECB system plays back (and disposes) its pending buffers when it updates, and its
    /// LastSystemVersion changes on every update. So a cached buffer is valid exactly while
    /// that version hasn't moved. This replaces the old time-based cache, which reused an
    /// already played-back buffer whenever ElapsedTime didn't advance (Time.timeScale = 0).
    /// </summary>
    private struct CachedBuffer
    {
      private EntityCommandBuffer buffer;
      private uint systemVersion;
      private bool hasBuffer;

      public EntityCommandBuffer GetOrCreate(EntityCommandBufferSystem system)
      {
        uint version = system.LastSystemVersion;
        if (!hasBuffer || version != systemVersion) {
          buffer = system.CreateCommandBuffer();
          systemVersion = version;
          hasBuffer = true;
        }
        return buffer;
      }

      public void Clear() => hasBuffer = false;
    }

    /// <summary>
    /// Context for a specific world containing ECB management
    /// </summary>
    private class DispatchContext : IDisposable
    {
      public World World { get; }

      // Frame lane: main-thread dispatches, played back next frame
      public EntityCommandBufferSystem ECBSystem { get; }
      private CachedBuffer frameECB;

      // Fixed lane: played back at the start of the next fixed action phase
      public EntityCommandBufferSystem FixedECBSystem { get; }
      private CachedBuffer fixedECB;
      private CachedBuffer fixedJobECB;

      // Job dispatch support: frame-lane job writes, played back by JobActionCollectorSystem
      public EntityCommandBufferSystem JobECBSystem { get; private set; }
      private CachedBuffer frameJobECB;

      // Middleware (ActionWriter) writes, one buffer per lane. Kept separate from the job
      // buffers so main-thread middleware writes never touch a buffer an unfinished user job
      // is writing to.
      private CachedBuffer writerFrameECB;
      private CachedBuffer writerFixedECB;

      // Track job dependencies for safe buffer refresh
      public JobHandle LastJobHandle { get; set; }

      public DispatchContext(World world)
      {
        World = world;

        // Use BeginInitialization to ensure actions process next frame
        ECBSystem = world.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();

        // Job dispatches collected by a dedicated system that extends EntityCommandBufferSystem
        JobECBSystem = world.GetOrCreateSystemManaged<JobActionCollectorSystem>();

        // Fixed-lane dispatches, played back at the end of each fixed step
        FixedECBSystem = world.GetOrCreateSystemManaged<FixedActionCommandBufferSystem>();

        // Lane lookup must be ready before any dispatch, including from Burst jobs
        ActionLaneRegistry.EnsureInitialized();
      }

      /// <summary>
      /// Called by JobActionCollectorSystem after each playback. Completes the registered jobs
      /// and resets the combined handle so it doesn't keep growing every frame. The job buffer
      /// itself rotates on its own: it is created lazily and replaced after each playback.
      /// </summary>
      public void RefreshJobBuffer()
      {
        LastJobHandle.Complete();
        LastJobHandle = default;
      }

      // All buffers are created lazily, at first use after each playback, never while the
      // context is being constructed. A world may create its systems in any order, so the ECB
      // systems' OnCreate may not have run yet when ECSActionDispatcherInitSystem.OnCreate does.
      public EntityCommandBuffer.ParallelWriter JobCommandBuffer =>
        frameJobECB.GetOrCreate(JobECBSystem).AsParallelWriter();

      public EntityCommandBuffer GetOrCreateFrameECB() => frameECB.GetOrCreate(ECBSystem);

      public EntityCommandBuffer GetOrCreateFixedECB() => fixedECB.GetOrCreate(FixedECBSystem);

      public EntityCommandBuffer.ParallelWriter GetFixedJobCommandBuffer() =>
        fixedJobECB.GetOrCreate(FixedECBSystem).AsParallelWriter();

      public ActionWriter CreateActionWriter(bool isFixedLane) => new ActionWriter(
        writerFrameECB.GetOrCreate(JobECBSystem).AsParallelWriter(),
        writerFixedECB.GetOrCreate(FixedECBSystem).AsParallelWriter(),
        isFixedLane);

      public void RegisterJobHandle(JobHandle handle)
      {
        LastJobHandle = JobHandle.CombineDependencies(LastJobHandle, handle);

        // An EntityCommandBufferSystem only waits for jobs registered as producers before
        // it plays back. Without this, playback could start while a job is still writing
        // to the buffer: a safety exception in the Editor, a data race in builds.
        // The handle is registered with both lanes because a job may write to either writer.
        JobECBSystem.AddJobHandleForProducer(handle);
        FixedECBSystem.AddJobHandleForProducer(handle);
      }

      public void Dispose()
      {
        // The EntityCommandBufferSystem will handle ECB disposal in its OnDestroy
        LastJobHandle.Complete();

        // Clear references
        frameECB.Clear();
        fixedECB.Clear();
        fixedJobECB.Clear();
        frameJobECB.Clear();
        writerFrameECB.Clear();
        writerFixedECB.Clear();
      }
    }

    /// <summary>
    /// Initialize the dispatcher for a specific world.
    /// Call this once during world initialization.
    /// Thread-safe - can be called from multiple threads.
    /// </summary>
    public static void Initialize(World world = null)
    {
      world ??= World.DefaultGameObjectInjectionWorld;

      var context = contexts.GetOrAdd(world, w => new DispatchContext(w));

      // Set as current if no world is current
      currentWorld ??= world;
    }

    /// <summary>
    /// Set the current world context for dispatching.
    /// Useful when working with multiple worlds.
    /// </summary>
    public static void SetWorld(World world)
    {
      currentWorld = world;
      if (!contexts.ContainsKey(world)) {
        Initialize(world);
      }
    }

    /// <summary>
    /// Primary dispatch method - automatically manages command buffers.
    /// Thread-safe for main thread only (maintains determinism).
    /// Automatically batches multiple dispatches until the lane's next playback.
    ///
    /// Routes by the action type's lane:
    /// - frame lane: processed next frame (BeginInitializationEntityCommandBufferSystem);
    /// - [FixedStepAction]: processed in the next fixed action phase. From a fixed-step
    ///   system that runs before the phase, that is this step.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dispatch<T>(T action) where T : unmanaged, IGameAction
    {
      Dispatch(action, null);
    }

    /// <summary>
    /// Dispatch into a specific world. Uses the current world when <paramref name="world"/> is null.
    /// </summary>
    public static void Dispatch<T>(T action, World world) where T : unmanaged, IGameAction
    {
      var context = GetOrCreateContext(world);
      var ecb = ActionLane<T>.IsFixed
        ? context.GetOrCreateFixedECB()
        : context.GetOrCreateFrameECB();

      // Creates the action entity, tagged ActionTag or FixedActionTag by lane
      ecb.DispatchAction(action);
    }

    /// <summary>
    /// Get the parallel writer for job dispatching.
    /// Call this before scheduling jobs that need to dispatch actions.
    /// The returned writer can be passed to Burst-compiled jobs safely.
    /// 
    /// Use the ECB.DispatchAction() extension method for cleaner code in your jobs.
    /// 
    /// Example usage:
    /// <code>
    /// // In your system:
    /// var ecb = ECSActionDispatcher.GetJobCommandBuffer();
    /// var jobHandle = new MyActionDispatchJob 
    /// { 
    ///     ECB = ecb,
    ///     // ... other job data
    /// }.ScheduleParallel(Dependency);
    /// 
    /// // Register the handle so buffer refresh waits for completion
    /// ECSActionDispatcher.RegisterJobHandle(jobHandle);
    /// 
    /// // In your job:
    /// [BurstCompile]
    /// public struct MyActionDispatchJob : IJobEntity
    /// {
    ///     public EntityCommandBuffer.ParallelWriter ECB;
    ///     
    ///     public void Execute([EntityIndexInQuery] int index, in SomeData data)
    ///     {
    ///         // Use the extension method for clean dispatch
    ///         ECB.DispatchAction(index, new MyAction { value = data.value });
    ///     }
    /// }
    /// </code>
    /// </summary>
    public static EntityCommandBuffer.ParallelWriter GetJobCommandBuffer(World world = null)
    {
      world ??= currentWorld ?? World.DefaultGameObjectInjectionWorld;

      if (!contexts.TryGetValue(world, out var context)) {
        Initialize(world);
        context = contexts[world];
      }

      return context.JobCommandBuffer;
    }

    /// <summary>
    /// Get the parallel writer for jobs that dispatch fixed-lane ([FixedStepAction]) actions.
    /// Writes are played back at the start of the next fixed action phase, so a job scheduled
    /// in fixed step N (before the phase) has its actions processed in step N.
    /// Call RegisterJobHandle after scheduling, as with GetJobCommandBuffer.
    /// The writer is valid until the next fixed-lane playback; fetch it again each update.
    /// </summary>
    public static EntityCommandBuffer.ParallelWriter GetFixedJobCommandBuffer(World world = null)
    {
      return GetOrCreateContext(world).GetFixedJobCommandBuffer();
    }

    /// <summary>
    /// Get a lane-routing <see cref="ActionWriter"/> for middleware dispatches.
    /// Main thread only; call once per system update and pass the writer by ref.
    /// Generated middleware systems call this for you.
    /// The writer has its own buffers, separate from GetJobCommandBuffer() /
    /// GetFixedJobCommandBuffer(), so it never writes into a buffer a user job may still be
    /// writing to. Playback timing per lane is the same.
    /// </summary>
    /// <param name="world">The world (uses current if null)</param>
    /// <param name="isFixedLane">True when the calling middleware runs in the fixed-step lane</param>
    public static ActionWriter GetActionWriter(World world, bool isFixedLane)
    {
      return GetOrCreateContext(world).CreateActionWriter(isFixedLane);
    }

    /// <summary>
    /// Register a job handle that uses the job command buffer.
    /// This ensures the buffer won't be played back or refreshed until the job completes.
    /// Call this after scheduling any job that uses GetJobCommandBuffer() or
    /// GetFixedJobCommandBuffer().
    /// </summary>
    /// <param name="handle">The JobHandle returned from scheduling</param>
    /// <param name="world">The world (uses current if null)</param>
    public static void RegisterJobHandle(JobHandle handle, World world = null)
    {
      world ??= currentWorld ?? World.DefaultGameObjectInjectionWorld;

      if (contexts.TryGetValue(world, out var context)) {
        context.RegisterJobHandle(handle);
      }
    }

    /// <summary>
    /// Called automatically by JobActionCollectorSystem after each playback.
    /// Completes and resets the registered job handles. Job buffers rotate lazily.
    /// </summary>
    internal static void RefreshJobBuffers(World world)
    {
      if (contexts.TryGetValue(world, out var context)) {
        context.RefreshJobBuffer();
      }
    }

    /// <summary>
    /// Get or create context for current world.
    /// Thread-safe through ConcurrentDictionary.
    /// </summary>
    private static DispatchContext GetOrCreateContext(World world = null)
    {
      world ??= currentWorld ?? World.DefaultGameObjectInjectionWorld;

      return contexts.GetOrAdd(world, w => new DispatchContext(w));
    }

    /// <summary>
    /// Cleanup when world is destroyed.
    /// Properly disposes all command buffers and removes the context.
    /// </summary>
    public static void Cleanup(World world)
    {
      if (contexts.TryRemove(world, out var context)) {
        context.Dispose();
      }

      if (currentWorld == world) {
        currentWorld = null;
      }
    }

    /// <summary>
    /// Cleanup all contexts.
    /// Call this during application shutdown.
    /// </summary>
    public static void CleanupAll()
    {
      foreach (var kvp in contexts) {
        kvp.Value.Dispose();
      }
      contexts.Clear();
      currentWorld = null;
    }
  }

}
