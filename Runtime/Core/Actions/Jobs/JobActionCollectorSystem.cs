using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Custom ECB system for collecting job-dispatched actions.
  /// During OnUpdate, this system will playback any dispatches received
  /// by the ECSActionDispatcher.DispatchFromJob, which will be processed
  /// by reducers 
  /// </summary>
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  [UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
  public partial class JobActionCollectorSystem : EntityCommandBufferSystem
  {
    protected override void OnUpdate()
    {
      base.OnUpdate();

      // Job playback occurs automatically in base.OnUpdate(), due to the
      // JobCommandBuffer that is generated from this system. It waits for jobs registered
      // via ECSActionDispatcher.RegisterJobHandle. The next job buffer is created lazily.
      // Afterwards, reset the dispatcher's combined job handle.
      ECSActionDispatcher.RefreshJobBuffers(World);
    }

    protected override void OnDestroy()
    {
      ECSActionDispatcher.Cleanup(World);
      base.OnDestroy();
    }
  }
}