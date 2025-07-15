using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Abstract base class for UI components that react to ECS state changes.
  /// Handles subscription/unsubscription lifecycle automatically.
  /// </summary>
  public abstract class ReactiveUIComponent : MonoBehaviour
  {
    protected virtual void Start()
    {
      SubscribeToStateChanges();
    }

    protected virtual void OnDestroy()
    {
      UnsubscribeFromStateChanges();
    }

    /// <summary>
    /// Override this method to subscribe to state changes you care about.
    /// Use StateSubscriptionHelper for type-safe subscriptions.
    /// </summary>
    protected abstract void SubscribeToStateChanges();

    /// <summary>
    /// Override this method to unsubscribe from state changes.
    /// Must match exactly with SubscribeToStateChanges().
    /// </summary>
    protected abstract void UnsubscribeFromStateChanges();

    /// <summary>
    /// Helper method to dispatch actions to the ECS world.
    /// Provides convenient access to Store.Instance.Dispatch().
    /// </summary>
    protected void DispatchAction<T>(T action) where T : unmanaged, IGameAction
    {
      if (Store.Instance != null) {
        Store.Instance.Dispatch(action);
      } else {
        Debug.LogError("Store instance not found! Make sure Store is in the scene.");
      }
    }
  }
}
