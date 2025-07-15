using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Marker interface for singleton state components that represent game state.
  /// These components should be added to singleton entities and contain the authoritative game data.
  /// </summary>
  public interface IGameState : IComponentData
  {
    // Marker interface - no methods required
  }
}
