using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// High-performance system that cleans up all processed action entities at the end of each frame.
  /// Uses zero-allocation patterns for optimal performance in high-volume action scenarios.
  /// Runs after all other systems to ensure actions can be processed by multiple reducers.
  /// </summary>
  [BurstCompile]
  [UpdateInGroup(typeof(ActionCleanupSystemGroup))]
  public partial struct ActionCleanupSystem : ISystem
  {
    private EntityQuery actionQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
      // Cache the query for all entities with ActionTag
      actionQuery = state.GetEntityQuery(
        ComponentType.ReadOnly<ActionTag>()
      );
      state.RequireForUpdate(actionQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
      // Early exit if no actions to clean up
      if (actionQuery.IsEmpty)
        return;

      // Zero-allocation destruction using direct iteration
      var ecb = new EntityCommandBuffer(Allocator.TempJob);
      foreach (var (tag, entity) in SystemAPI.Query<RefRO<ActionTag>>().WithEntityAccess()) {
        ecb.DestroyEntity(entity);
      }

      ecb.Playback(state.EntityManager);
      ecb.Dispose();
    }
  }
}
