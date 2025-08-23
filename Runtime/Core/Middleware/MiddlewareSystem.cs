using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Abstract base class for middleware systems that process actions before reducers.
  /// Middleware systems can handle cross-cutting concerns like validation, logging,
  /// analytics, and async operations without consuming the actions.
  /// </summary>
  [UpdateInGroup(typeof(MiddlewareSystemGroup))]
  [UpdateBefore(typeof(SimulationSystemGroup))]
  public abstract partial class MiddlewareSystem<T> : SystemBase
      where T : unmanaged, IGameAction
  {
    private EntityQuery actionQuery;

    protected override void OnCreate()
    {
      base.OnCreate();

      // Create query for the specific action type
      actionQuery = GetEntityQuery(
        ComponentType.ReadOnly<T>(),
        ComponentType.ReadOnly<ActionTag>()
      );
    }

    protected override void OnUpdate()
    {
      // Process actions but don't consume them (reducers will do that)
      var actionEntities = actionQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

      foreach (var entity in actionEntities) {
        var action = EntityManager.GetComponentData<T>(entity);
        ProcessAction(action, entity);
      }

      actionEntities.Dispose();
    }

    /// <summary>
    /// Override this method to implement your middleware logic.
    /// This is called for each action of the specified type.
    /// Do NOT destroy the action entity - that's handled by ActionCleanupSystem.
    /// </summary>
    /// <param name="action">The action data to process</param>
    /// <param name="actionEntity">The entity containing the action (for additional components)</param>
    protected abstract void ProcessAction(T action, Entity actionEntity);

    /// <summary>
    /// Helper method to dispatch additional actions from middleware.
    /// Useful for triggering side effects or validation failures.
    /// </summary>
    protected void DispatchAction<TNewAction>(TNewAction newAction)
        where TNewAction : unmanaged, IGameAction
    {
      var entity = EntityManager.CreateEntity();
      EntityManager.AddComponentData(entity, newAction);
      EntityManager.AddComponentData(entity, new ActionTag());
    }
  }
}
