using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Marker interface for action components that describe state changes.
  /// Actions are created as temporary entities and consumed by reducer systems.
  /// </summary>
  public interface IGameAction : IComponentData
  {
    // Marker interface - no methods required
  }
}
