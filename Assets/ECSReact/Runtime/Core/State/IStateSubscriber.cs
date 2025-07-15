namespace ECSReact.Core
{
  /// <summary>
  /// Interface for UI components that want to subscribe to specific state changes.
  /// Implement this interface for type-safe state subscriptions.
  /// </summary>
  public interface IStateSubscriber<T> where T : unmanaged, IGameState
  {
    void OnStateChanged(T newState);
  }
}
