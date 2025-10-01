using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using System.Runtime.CompilerServices;

namespace ECSReact.Core
{
  /// <summary>
  /// Example of how to properly use the dispatcher from a Burst-compiled job:
  /// </summary>
  /// <example>
  /// <code>
  /// [BurstCompile]
  /// public struct ActionDispatchJob : IJobEntity
  /// {
  ///     public EntityCommandBuffer.ParallelWriter ECB;
  ///     
  ///     public void Execute([EntityIndexInQuery] int index, in SomeComponent data)
  ///     {
  ///         if (data.shouldDispatch)
  ///         {
  ///             // Clean one-liner using extension method!
  ///             ECB.DispatchAction(index, new MyAction { value = data.value });
  ///         }
  ///     }
  /// }
  /// 
  /// // In your system:
  /// protected override void OnUpdate()
  /// {
  ///     var ecb = ECSActionDispatcher.GetJobCommandBuffer(World);
  ///     var handle = new ActionDispatchJob { ECB = ecb }.ScheduleParallel(Dependency);
  ///     
  ///     // Register the handle so buffer refresh waits for completion
  ///     ECSActionDispatcher.RegisterJobHandle(handle, World);
  ///     Dependency = handle;
  /// }
  /// </code>
  /// </example>

  /// <summary>
  /// Extension methods for EntityCommandBuffer to reduce boilerplate when dispatching actions.
  /// These methods are Burst-compatible and can be used in jobs.
  /// </summary>
  [BurstCompile]
  public static class ECBActionExtensions
  {
    /// <summary>
    /// Dispatches an action through a parallel command buffer with proper tagging.
    /// Reduces boilerplate from 3 lines to 1.
    /// </summary>
    /// <param name="ecb">The parallel command buffer writer</param>
    /// <param name="sortKey">Unique sort key for deterministic ordering (e.g., entityInQueryIndex)</param>
    /// <param name="action">The action to dispatch</param>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DispatchAction<T>(
        this EntityCommandBuffer.ParallelWriter ecb,
        int sortKey,
        T action) where T : unmanaged, IGameAction
    {
      var entity = ecb.CreateEntity(sortKey);
      ecb.AddComponent(sortKey, entity, action);
      ecb.AddComponent(sortKey, entity, new ActionTag());
    }

    /// <summary>
    /// Dispatches an action through a regular command buffer with proper tagging.
    /// Use this overload when not in a parallel job.
    /// </summary>
    /// <param name="ecb">The command buffer</param>
    /// <param name="action">The action to dispatch</param>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DispatchAction<T>(
        this EntityCommandBuffer ecb,
        T action) where T : unmanaged, IGameAction
    {
      var entity = ecb.CreateEntity();
      ecb.AddComponent(entity, action);
      ecb.AddComponent(entity, new ActionTag());
    }

    /// <summary>
    /// Dispatches multiple actions through a parallel command buffer.
    /// Useful when a job needs to create multiple actions at once.
    /// </summary>
    /// <param name="ecb">The parallel command buffer writer</param>
    /// <param name="sortKey">Base sort key (will increment for each action)</param>
    /// <param name="actions">Native array of actions to dispatch</param>
    [BurstCompile]
    public static void DispatchActions<T>(
        this EntityCommandBuffer.ParallelWriter ecb,
        int sortKey,
        NativeArray<T> actions) where T : unmanaged, IGameAction
    {
      for (int i = 0; i < actions.Length; i++) {
        ecb.DispatchAction(sortKey + i, actions[i]);
      }
    }
  }
}