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
  ///     public bool Process(ref DamageAction action, ref SystemState systemState)
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
    /// Processes an action before it reaches reducers with full SystemAPI access.
    /// Can modify the action or prevent it from propagating.
    /// 
    /// Use 'ref' for the action parameter to modify it before it reaches reducers.
    /// Use 'ref' for the systemState to access SystemAPI features.
    /// Return false to prevent the action from reaching reducers (it will be destroyed).
    /// </summary>
    /// <param name="action">The action to process (can be modified)</param>
    /// <param name="systemState">The system state providing access to SystemAPI</param>
    /// <returns>True to continue processing, false to prevent the action from reaching reducers</returns>
    bool Process(ref TAction action, ref SystemState systemState);
  }
}