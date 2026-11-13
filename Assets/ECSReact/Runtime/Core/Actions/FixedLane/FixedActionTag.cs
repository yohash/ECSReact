using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Tag component added to fixed-lane action entities (action types marked with
  /// <see cref="FixedStepActionAttribute"/>), in place of <see cref="ActionTag"/>.
  ///
  /// Keeps the lanes isolated: frame-lane systems query ActionTag and never match these,
  /// and the frame ActionCleanupSystem never destroys a fixed-lane action that is still
  /// waiting for the next fixed step. FixedActionCleanupSystem destroys them at the end
  /// of the fixed action phase.
  /// </summary>
  public struct FixedActionTag : IComponentData
  {
    // Empty tag component
  }
}
