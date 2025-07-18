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

/// <summary>
/// Example: A simple component that displays health state in a UI slider.
/// </summary>
/*
public struct HealthState : IGameState, IEquatable<HealthState>
{
  public int currentHealth;
  public int maxHealth;
  public bool Equals(HealthState other)
  {
    return currentHealth == other.currentHealth && maxHealth == other.maxHealth;
  }
}

/// <summary>
/// Simple concrete example of a single-state UI component.
/// Shows the minimal implementation needed for a reactive UI component.
/// </summary>
public class ExampleHealthBarUI : SingleStateUIComponent<HealthState>
{
  [SerializeField] private UnityEngine.UI.Slider healthSlider;

  public override void OnStateChanged(HealthState newState)
  {
    // Example implementation - in practice you'd cast to your specific state type
    Debug.Log($"ExampleHealthBarUI received state change: {newState}");

    // Example: Update health bar
    if (healthSlider != null)
      healthSlider.value = (float)newState.currentHealth / newState.maxHealth;
  }

  public void OnTakeDamageButtonClicked()
  {
    // Example: Dispatch an action when UI button is clicked
    // DispatchAction(new TakeDamageAction { damage = 10 });
    Debug.Log("Take damage button clicked - would dispatch TakeDamageAction");
  }
}
*/
