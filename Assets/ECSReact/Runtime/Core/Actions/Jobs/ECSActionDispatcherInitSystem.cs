using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// System to initialize ECS dispatcher at world creation.
  /// </summary>
  [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
  public partial class ECSActionDispatcherInitSystem : SystemBase
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      ECSActionDispatcher.Initialize(World);
    }

    protected override void OnDestroy()
    {
      ECSActionDispatcher.Cleanup(World);
      base.OnDestroy();
    }

    protected override void OnUpdate()
    {
      // No update method, exists solely for lifecycle init/cleanup
    }
  }
}