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
    /// Context for a specific world containing ECB management
    /// </summary>
    private class DispatchContext : IDisposable
    {
      public World World { get; }
      public EntityCommandBufferSystem ECBSystem { get; }

      // Frame-based ECB caching
      public EntityCommandBuffer? CurrentFrameECB { get; set; }
      public double LastTimeUpdated { get; set; } = -1;

      // Job dispatch support - persistent ECB for jobs action dispatching
      public EntityCommandBuffer.ParallelWriter JobCommandBuffer { get; private set; }
      public EntityCommandBufferSystem JobECBSystem { get; private set; }

      // Track job dependencies for safe buffer refresh
      public JobHandle LastJobHandle { get; set; }

      // Track created buffers for disposal
      private readonly List<EntityCommandBuffer> createdBuffers = new();
      private readonly object bufferLock = new object();

      public DispatchContext(World world)
      {
        World = world;

        // Use BeginInitialization to ensure actions process next frame
        ECBSystem = world.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();

        // Job dispatches collected by a dedicated system that extends EntityCommandBufferSystem
        // so that the EntityCommandBuffer.ParallelWriter JobCommandBuffer has automatic
        // playback in the OnUpdate method
        JobECBSystem = world.GetOrCreateSystemManaged<JobActionCollectorSystem>();
      }

      public void RefreshJobBuffer()
      {
        // Ensure previous jobs complete before creating new buffer
        LastJobHandle.Complete();

        lock (bufferLock) {
          // Create a fresh parallel writer for jobs to use this frame
          var newECB = JobECBSystem.CreateCommandBuffer();
          JobCommandBuffer = newECB.AsParallelWriter();
          createdBuffers.Add(newECB);
        }
      }

      public EntityCommandBuffer GetOrCreateFrameECB()
      {
        double currentTime = World.Time.ElapsedTime;

        lock (bufferLock) {
          // Check if we need a new ECB for this frame
          if (!CurrentFrameECB.HasValue || currentTime > LastTimeUpdated) {
            // Create new ECB for this frame
            var newECB = ECBSystem.CreateCommandBuffer();
            CurrentFrameECB = newECB;
            LastTimeUpdated = currentTime;
            createdBuffers.Add(newECB);
          }

          return CurrentFrameECB.Value;
        }
      }

      public void RegisterJobHandle(JobHandle handle)
      {
        lock (bufferLock) {
          LastJobHandle = JobHandle.CombineDependencies(LastJobHandle, handle);
        }
      }

      public void Dispose()
      {
        lock (bufferLock) {
          // Complete any remaining jobs
          LastJobHandle.Complete();

          // Dispose all created buffers
          foreach (var buffer in createdBuffers) {
            if (buffer.IsCreated) {
              try {
                buffer.Dispose();
              } catch (ObjectDisposedException) {
                // Buffer was already disposed by the system, which is fine
              }
            }
          }
          createdBuffers.Clear();
        }
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

      var context = contexts.GetOrAdd(world, w =>
      {
        var newContext = new DispatchContext(w);
        newContext.RefreshJobBuffer();
        return newContext;
      });

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
    /// Automatically batches multiple dispatches in the same frame.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dispatch<T>(T action) where T : unmanaged, IGameAction
    {
      var context = GetOrCreateContext();
      var ecb = context.GetOrCreateFrameECB();

      // Create action entity
      var entity = ecb.CreateEntity();
      ecb.AddComponent(entity, action);
      ecb.AddComponent(entity, new ActionTag());
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
    /// Register a job handle that uses the job command buffer.
    /// This ensures the buffer won't be refreshed until the job completes.
    /// Call this after scheduling any job that uses GetJobCommandBuffer().
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
    /// Refresh job buffers for a new frame.
    /// Called automatically by JobActionCollectorSystem.
    /// Waits for all registered job handles to complete before refreshing.
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
    private static DispatchContext GetOrCreateContext()
    {
      var world = currentWorld ?? World.DefaultGameObjectInjectionWorld;

      return contexts.GetOrAdd(world, w =>
      {
        var newContext = new DispatchContext(w);
        newContext.RefreshJobBuffer();
        return newContext;
      });
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
