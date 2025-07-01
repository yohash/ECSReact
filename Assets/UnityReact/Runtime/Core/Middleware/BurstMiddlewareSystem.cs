using Unity.Entities;
using Unity.Burst;

namespace ECSReact.Core
{
  /// <summary>
  /// Abstract base class for Burst-compatible middleware systems.
  /// Use this when your middleware doesn't need managed objects or Unity APIs.
  /// Provides better performance for high-frequency action processing.
  /// </summary>
  [BurstCompile]
  [UpdateInGroup(typeof(MiddlewareSystemGroup))]
  [UpdateBefore(typeof(SimulationSystemGroup))]
  public abstract partial class BurstMiddlewareSystem<T> : SystemBase
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
      var actionEntities = actionQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

      foreach (var entity in actionEntities) {
        var action = EntityManager.GetComponentData<T>(entity);
        ProcessAction(action, entity);
      }

      actionEntities.Dispose();
    }

    /// <summary>
    /// Override this method to implement your Burst-compatible middleware logic.
    /// Cannot use managed objects, Unity APIs, or async operations.
    /// </summary>
    protected abstract void ProcessAction(T action, Entity actionEntity);

    /// <summary>
    /// Burst-compatible helper for dispatching additional actions.
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