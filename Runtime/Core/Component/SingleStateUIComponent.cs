namespace ECSReact.Core
{
  /// <summary>
  /// Base class for UI components that subscribe to a single state type.
  /// Simplifies implementation for components that only care about one state.
  /// This serves as an example and can be used directly.
  /// </summary>
  public abstract class ReactiveUIComponent<T> : ReactiveUIComponent, IStateSubscriber<T>
      where T : unmanaged, IGameState
  {
    protected override void SubscribeToStateChanges()
    {
      StateSubscriptionHelper.Subscribe<T>(this);
    }

    protected override void UnsubscribeFromStateChanges()
    {
      StateSubscriptionHelper.Unsubscribe<T>(this);
    }

    /// <summary>
    /// Override this method to handle state changes for your specific state type.
    /// </summary>
    public abstract void OnStateChanged(T newState);
  }
}
