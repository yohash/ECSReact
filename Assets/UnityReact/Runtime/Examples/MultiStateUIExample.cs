using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Example implementation showing how to create a multi-state UI component.
  /// This demonstrates the pattern for components that need multiple state subscriptions.
  /// </summary>
  public abstract class MultiStateUIExample : ReactiveUIComponent
    //IStateSubscriber<GameState>,
    //IStateSubscriber<PlayerState>,
    //IStateSubscriber<UIState>
  {
    // Example: A UI component that cares about multiple states
    // In practice, you'd implement IStateSubscriber<T> for each state you care about

    protected override void SubscribeToStateChanges()
    {
      // Example subscriptions - would be implemented with real state types
      // StateSubscriptionHelper.Subscribe<GameState>(this);
      // StateSubscriptionHelper.Subscribe<PlayerState>(this);
      // StateSubscriptionHelper.Subscribe<UIState>(this);

      Debug.Log($"MultiStateUIExample: {gameObject.name} subscribed to multiple states");
    }

    protected override void UnsubscribeFromStateChanges()
    {
      // Must match the subscriptions above
      // StateSubscriptionHelper.Unsubscribe<GameState>(this);
      // StateSubscriptionHelper.Unsubscribe<PlayerState>(this);
      // StateSubscriptionHelper.Unsubscribe<UIState>(this);

      Debug.Log($"MultiStateUIExample: {gameObject.name} unsubscribed from multiple states");
    }

    // Example state change handlers
    // public void OnStateChanged(GameState newState) { /* Handle game state */ }
    // public void OnStateChanged(PlayerState newState) { /* Handle player state */ }
    // public void OnStateChanged(UIState newState) { /* Handle UI state */ }
  }
}