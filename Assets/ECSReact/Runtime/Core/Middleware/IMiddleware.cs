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
  ///     private EntityQuery configQuery;
  ///
  ///     public void OnCreate(ref SystemState state)
  ///     {
  ///         // Cache queries here to avoid GetEntityQuery warnings during OnUpdate
  ///         configQuery = state.GetEntityQuery(ComponentType.ReadOnly&lt;GameConfig&gt;());
  ///     }
  ///
  ///     public bool Process(
  ///         in DamageAction action,
  ///         ref SystemState systemState,
  ///         ref ActionWriter dispatch
  ///     )
  ///     {
  ///         var config = configQuery.GetSingleton&lt;GameConfig&gt;();
  ///         // Read-only: inspect action data, cannot mutate
  ///         if (action.damage > 100)
  ///         {
  ///             // Routed to the lane of CriticalHitEvent (frame or [FixedStepAction])
  ///             dispatch.Dispatch(new CriticalHitEvent { damage = action.damage });
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
    /// Called once when the generated system is created.
    /// Use this to cache EntityQueries, ComponentLookups, or any other data
    /// that should be prepared once rather than per-frame.
    /// 
    /// This runs inside the generated system's OnCreate, so state.GetEntityQuery()
    /// and state.GetComponentLookup() will not produce warnings here.
    /// </summary>
    /// <param name="state">The system state for query/lookup creation</param>
    void OnCreate(ref SystemState state);

    /// <summary>
    /// Processes an action before it reaches reducers.
    /// Receives an <see cref="ActionWriter"/> for Burst-compatible dispatching.
    ///
    /// Dispatches are deferred and routed by the dispatched action's lane:
    /// - frame-lane actions are processed next frame;
    /// - [FixedStepAction] actions are processed in the next fixed step.
    /// For raw command buffer work (e.g. creating non-action entities), use dispatch.Ecb with
    /// dispatch.NextSortKey(); that buffer belongs to this middleware's own lane.
    ///
    /// Actions are read-only. Middleware cannot mutate action data — it can only
    /// inspect, filter (return false), or dispatch new actions.
    /// </summary>
    /// <param name="action">The action to inspect (read-only)</param>
    /// <param name="systemState">The system state providing access to SystemAPI</param>
    /// <param name="dispatch">Lane-routing writer for dispatching actions</param>
    bool Process(
      in TAction action,
      ref SystemState systemState,
      ref ActionWriter dispatch);
  }
}
