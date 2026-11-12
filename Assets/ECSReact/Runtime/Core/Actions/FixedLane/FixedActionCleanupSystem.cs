using Unity.Burst;
using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Destroys every fixed-lane action entity (FixedActionTag) at the end of the fixed
  /// action phase, after all fixed-lane middleware and reducers have seen them.
  /// Frame-lane actions (ActionTag) are left alone; ActionCleanupSystem handles those.
  /// </summary>
  [BurstCompile]
  [UpdateInGroup(typeof(FixedActionSystemGroup), OrderLast = true)]
  public partial struct FixedActionCleanupSystem : ISystem
  {
    private EntityQuery fixedActionQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
      fixedActionQuery = state.GetEntityQuery(ComponentType.ReadOnly<FixedActionTag>());
      state.RequireForUpdate(fixedActionQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
      state.EntityManager.DestroyEntity(fixedActionQuery);
    }
  }
}
