using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace ECSReact.Core
{
  /// <summary>
  /// System that cleans up all processed action entities at the end of each frame.
  /// Runs after all other systems to ensure actions can be processed by multiple reducers.
  /// </summary>
  [BurstCompile]
  [UpdateInGroup(typeof(ActionCleanupSystemGroup))]
  public partial class ActionCleanupSystem : SystemBase
  {
    protected override void OnUpdate()
    {
      var actionEntities = SystemAPI.QueryBuilder()
        .WithAll<ActionTag>()
        .Build()
        .ToEntityArray(Allocator.Temp);

      EntityManager.DestroyEntity(actionEntities);
      actionEntities.Dispose();
    }
  }
}
