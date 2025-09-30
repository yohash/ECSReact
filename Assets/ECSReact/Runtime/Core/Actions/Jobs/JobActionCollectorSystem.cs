using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Custom ECB system for collecting job-dispatched actions.
  /// During OnUpdate, this system will playback any dispatches received
  /// by the ECSActionDispatcher.DispatchFromJob, which will be processed
  /// by reducers 
  /// </summary>
  [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
  [UpdateBefore(typeof(BeginSimulationEntityCommandBufferSystem))]
  public partial class JobActionCollectorSystem : EntityCommandBufferSystem
  {
    protected override void OnUpdate()
    {
      base.OnUpdate();

      // Job playback occurs automatically in base.OnUpdate(), due to the 
      // JobCommandBuffer that is generated from this system.
      // After playing back job actions, create new buffer for next frame's jobs
      ECSActionDispatcher.RefreshJobBuffers(World);
    }
  }
}