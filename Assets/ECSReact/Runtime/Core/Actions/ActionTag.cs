using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Tag component added to all action entities for efficient cleanup.
  /// The ActionCleanupSystem destroys all entities with this tag at the end of each frame.
  /// </summary>
  public struct ActionTag : IComponentData
  {
    // Empty tag component
  }
}
