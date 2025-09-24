using Unity.Entities;
using Unity.Burst;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ECSReact.Core
{
  /// <summary>
  /// Optimized ECSActionDispatcher that manages its own command buffers
  /// and handles high-volume action dispatching efficiently.
  /// Supports both main-thread and job-based dispatching.
  /// </summary>
  public static class ECSActionDispatcher
  {
    // ECB management per world (support for multiple worlds)
    private static readonly Dictionary<World, DispatchContext> contexts = new();

    // Thread-local current world for context-aware dispatching
    [ThreadStatic]
    private static World currentWorld;

    /// <summary>
    /// Context for a specific world containing ECB management
    /// </summary>
    private class DispatchContext
    {
      public World World { get; }
      public EntityCommandBufferSystem ECBSystem { get; }

      // Frame-based ECB caching
      public EntityCommandBuffer? CurrentFrameECB { get; set; }
      public double LastTimeUpdated { get; set; } = -1;

      // Job dispatch support - persistent ECB for jobs action dispatching
      public EntityCommandBuffer.ParallelWriter JobCommandBuffer { get; private set; }
      public EntityCommandBufferSystem JobECBSystem { get; private set; }

      public DispatchContext(World world)
      {
        World = world;

        // Use BeginInitialization to match Store and ensure same-frame processing
        ECBSystem = world.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();

        // Job dispatches collected by a dedicated system that extends EntityCommandBufferSystem
        // so that the EntityCommandBuffer.ParallelWriter JobCommandBuffer has automatic
        // playback in the OnUpdate method
        JobECBSystem = world.GetOrCreateSystemManaged<JobActionCollectorSystem>();
      }

      public void RefreshJobBuffer()
      {
        // Create a fresh parallel writer for jobs to use this frame
        JobCommandBuffer = JobECBSystem.CreateCommandBuffer().AsParallelWriter();
      }
    }

    /// <summary>
    /// Initialize the dispatcher for a specific world.
    /// Call this once during world initialization.
    /// </summary>
    public static void Initialize(World world = null)
    {
      world ??= World.DefaultGameObjectInjectionWorld;

      if (!contexts.ContainsKey(world)) {
        var context = new DispatchContext(world);
        context.RefreshJobBuffer(); // Initialize job buffer
        contexts[world] = context;
      }

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
      var ecb = GetOrCreateFrameECB(context);

      // Create action entity
      var entity = ecb.CreateEntity();
      ecb.AddComponent(entity, action);
      ecb.AddComponent(entity, new ActionTag());
    }

    /// <summary>
    /// Dispatch from within a job - thread-safe for parallel execution.
    /// Actions are queued and processed on the next main thread update.
    /// </summary>
    /// <param name="action">The action to dispatch</param>
    /// <param name="sortKey">Unique sort key for deterministic ordering (e.g., entityInQueryIndex)</param>
    [BurstCompile]
    public static void DispatchFromJob<T>(T action, int sortKey) where T : unmanaged, IGameAction
    {
      var context = GetOrCreateContext();

      // Use the persistent parallel writer for this world
      var entity = context.JobCommandBuffer.CreateEntity(sortKey);
      context.JobCommandBuffer.AddComponent(sortKey, entity, action);
      context.JobCommandBuffer.AddComponent(sortKey, entity, new ActionTag());
    }

    /// <summary>
    /// Get the parallel writer for job dispatching.
    /// Call this before scheduling jobs that need to dispatch actions.
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
    /// Refresh job buffers for a new frame.
    /// Called automatically by JobActionCollectorSystem.
    /// </summary>
    internal static void RefreshJobBuffers(World world)
    {
      if (contexts.TryGetValue(world, out var context)) {
        context.RefreshJobBuffer();
      }
    }

    /// <summary>
    /// Get or create the ECB for the current frame.
    /// Reuses the same ECB for all dispatches in a frame.
    /// </summary>
    private static EntityCommandBuffer GetOrCreateFrameECB(DispatchContext context)
    {
      double currentTime = context.World.Time.ElapsedTime;

      // Check if we need a new ECB for this frame
      if (!context.CurrentFrameECB.HasValue || currentTime > context.LastTimeUpdated) {
        // Create new ECB for this frame
        context.CurrentFrameECB = context.ECBSystem.CreateCommandBuffer();
        context.LastTimeUpdated = currentTime;
      }

      return context.CurrentFrameECB.Value;
    }

    /// <summary>
    /// Get or create context for current world
    /// </summary>
    private static DispatchContext GetOrCreateContext()
    {
      var world = currentWorld ?? World.DefaultGameObjectInjectionWorld;

      if (!contexts.TryGetValue(world, out var context)) {
        Initialize(world);
        context = contexts[world];
      }

      return context;
    }

    /// <summary>
    /// Cleanup when world is destroyed
    /// </summary>
    public static void Cleanup(World world)
    {
      contexts.Remove(world);
      if (currentWorld == world) {
        currentWorld = null;
      }
    }
  }
}