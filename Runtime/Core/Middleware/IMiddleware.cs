using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Interface for middleware logic with full SystemAPI access.
  /// Use this when you need to query entities, access singletons, or create complex side effects.
  /// Processes actions sequentially on the main thread.
  /// 
  /// All middleware are Burst-compiled by default for maximum performance.
  /// Use the [Middleware(DisableBurst = true)] attribute if your logic requires
  /// managed code, Unity API calls, or other non-Burstable operations.
  /// 
  /// Example:
  /// <code>
  /// [Middleware]
  /// public struct DamageValidationMiddleware : IMiddleware&lt;DamageAction&gt;
  /// {
  ///     public bool Process(
  ///         ref NextTurnAction action, 
  ///         ref SystemState systemState, 
  ///         EntityCommandBuffer.ParallelWriter dispatcher, 
  ///         int sortKey
  ///     )
  ///     {
  ///         var config = SystemAPI.GetSingleton&lt;GameConfig&gt;();
  ///         action.damage = math.clamp(action.damage, 0, config.maxDamage);
  ///         
  ///         if (action.damage > 100)
  ///         {
  ///             var ecb = SystemAPI.GetSingleton&lt;EndSimulationEntityCommandBufferSystem.Singleton&gt;()
  ///                 .CreateCommandBuffer(systemState.WorldUnmanaged);
  ///             var entity = ecb.CreateEntity();
  ///             ecb.AddComponent(entity, new CriticalHitEvent { damage = action.damage });
  ///         }
  ///         
  ///         return action.damage > 0; // false prevents action from reaching reducers
  ///     }
  /// }
  /// </code>
  /// </summary>
  /// <typeparam name="TAction">The action type this middleware processes (must implement IGameAction)</typeparam>
  public interface IMiddleware<TAction>
      where TAction : unmanaged, IGameAction
  {
    /// <summary>
    /// Processes an action before it reaches reducers.
    /// NEW: Now receives EntityCommandBuffer.ParallelWriter for Burst-compatible dispatching.
    /// 
    /// NOTE: Actions dispatched via 'dispatcher' are deferred and will appear in the next frame.
    /// For immediate action dispatch, use [Middleware(DisableBurst = true)] and ECSActionDispatcher.Dispatch().
    /// </summary>
    /// <param name="action">The action to process (can be modified)</param>
    /// <param name="systemState">The system state providing access to SystemAPI</param>
    /// <param name="dispatcher">ECB ParallelWriter for Burst-compatible action dispatching</param>
    /// <param name="sortKey">Unique index for deterministic command buffer ordering</param>
    bool Process(
      ref TAction action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter dispatcher,
      int sortKey);
  }
}