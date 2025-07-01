using Unity.Entities;
using Unity.Burst;

namespace ECSReact.Core
{
  /// <summary>
  /// Abstract base class for reducer systems that process actions and update state.
  /// Uses dynamic queries to work around Unity ECS generic type constraints.
  /// </summary>
  [BurstCompile]
  public abstract partial class StateReducerSystem<TState, TAction> : SystemBase
      where TState : unmanaged, IGameState
      where TAction : unmanaged, IGameAction
  {
    private EntityQuery actionQuery;

    protected override void OnCreate()
    {
      base.OnCreate();

      // Create query for the specific action type
      actionQuery = GetEntityQuery(
          ComponentType.ReadOnly<TAction>(),
          ComponentType.ReadOnly<ActionTag>()
      );
    }

    protected override void OnUpdate()
    {
      var state = SystemAPI.GetSingletonRW<TState>();
      var actionEntities = actionQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

      foreach (var entity in actionEntities) {
        var action = EntityManager.GetComponentData<TAction>(entity);
        ReduceState(ref state.ValueRW, action);
      }

      actionEntities.Dispose();
    }

    /// <summary>
    /// Override this method to implement your state reduction logic.
    /// This is called once for each action of the specified type.
    /// </summary>
    protected abstract void ReduceState(ref TState state, TAction action);
  }
}
