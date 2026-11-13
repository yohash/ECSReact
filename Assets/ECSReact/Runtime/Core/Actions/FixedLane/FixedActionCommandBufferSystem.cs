using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Command buffer system for the fixed-step action lane.
  ///
  /// Every dispatch of a [FixedStepAction] action (from ECSActionDispatcher, Store, or a
  /// fixed-lane job writer) is recorded into a buffer from this system. It runs first in
  /// FixedActionSystemGroup, so actions dispatched earlier in fixed step N are played back
  /// and processed in step N. Dispatches recorded after playback (for example by fixed-lane
  /// middleware) wait for the next step's playback.
  ///
  /// When a frame runs zero fixed steps this system doesn't update, so pending buffers
  /// persist until the next step. Nothing is dropped.
  /// </summary>
  [UpdateInGroup(typeof(FixedActionSystemGroup), OrderFirst = true)]
  public partial class FixedActionCommandBufferSystem : EntityCommandBufferSystem
  {
  }
}
