namespace ECSReact.Core
{
  /// <summary>
  /// Base class for UI components that need exactly 2 state subscriptions.
  /// Uses unique method names to avoid interface conflicts.
  /// Zero reflection - completely compile-time safe and fast.
  /// </summary>
  public abstract class MultiStateUIComponent<T1, T2> : ReactiveUIComponent
      where T1 : unmanaged, IGameState
      where T2 : unmanaged, IGameState
  {
    // Internal state subscribers to avoid interface conflicts
    private StateSubscriber1 subscriber1;
    private StateSubscriber2 subscriber2;

    protected override void SubscribeToStateChanges()
    {
      subscriber1 = new StateSubscriber1(this);
      subscriber2 = new StateSubscriber2(this);

      StateSubscriptionHelper.Subscribe<T1>(subscriber1);
      StateSubscriptionHelper.Subscribe<T2>(subscriber2);
    }

    protected override void UnsubscribeFromStateChanges()
    {
      if (subscriber1 != null) {
        StateSubscriptionHelper.Unsubscribe<T1>(subscriber1);
      }
      if (subscriber2 != null) {
        StateSubscriptionHelper.Unsubscribe<T2>(subscriber2);
      }
    }

    // Clean, unique method names for each state type
    public abstract void OnStateChanged(T1 newState);
    public abstract void OnStateChanged(T2 newState);

    // Internal wrapper classes to handle the subscriptions
    private class StateSubscriber1 : IStateSubscriber<T1>
    {
      private readonly MultiStateUIComponent<T1, T2> parent;
      public StateSubscriber1(MultiStateUIComponent<T1, T2> parent) => this.parent = parent;
      public void OnStateChanged(T1 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber2 : IStateSubscriber<T2>
    {
      private readonly MultiStateUIComponent<T1, T2> parent;
      public StateSubscriber2(MultiStateUIComponent<T1, T2> parent) => this.parent = parent;
      public void OnStateChanged(T2 newState) => parent.OnStateChanged(newState);
    }
  }

  /// <summary>
  /// Base class for UI components that need exactly 3 state subscriptions.
  /// </summary>
  public abstract class MultiStateUIComponent<T1, T2, T3> : ReactiveUIComponent
      where T1 : unmanaged, IGameState
      where T2 : unmanaged, IGameState
      where T3 : unmanaged, IGameState
  {
    private StateSubscriber1 subscriber1;
    private StateSubscriber2 subscriber2;
    private StateSubscriber3 subscriber3;

    protected override void SubscribeToStateChanges()
    {
      subscriber1 = new StateSubscriber1(this);
      subscriber2 = new StateSubscriber2(this);
      subscriber3 = new StateSubscriber3(this);

      StateSubscriptionHelper.Subscribe<T1>(subscriber1);
      StateSubscriptionHelper.Subscribe<T2>(subscriber2);
      StateSubscriptionHelper.Subscribe<T3>(subscriber3);
    }

    protected override void UnsubscribeFromStateChanges()
    {
      if (subscriber1 != null) {
        StateSubscriptionHelper.Unsubscribe<T1>(subscriber1);
      }
      if (subscriber2 != null) {
        StateSubscriptionHelper.Unsubscribe<T2>(subscriber2);
      }
      if (subscriber3 != null) {
        StateSubscriptionHelper.Unsubscribe<T3>(subscriber3);
      }
    }

    public abstract void OnStateChanged(T1 newState);
    public abstract void OnStateChanged(T2 newState);
    public abstract void OnStateChanged(T3 newState);

    private class StateSubscriber1 : IStateSubscriber<T1>
    {
      private readonly MultiStateUIComponent<T1, T2, T3> parent;
      public StateSubscriber1(MultiStateUIComponent<T1, T2, T3> parent) => this.parent = parent;
      public void OnStateChanged(T1 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber2 : IStateSubscriber<T2>
    {
      private readonly MultiStateUIComponent<T1, T2, T3> parent;
      public StateSubscriber2(MultiStateUIComponent<T1, T2, T3> parent) => this.parent = parent;
      public void OnStateChanged(T2 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber3 : IStateSubscriber<T3>
    {
      private readonly MultiStateUIComponent<T1, T2, T3> parent;
      public StateSubscriber3(MultiStateUIComponent<T1, T2, T3> parent) => this.parent = parent;
      public void OnStateChanged(T3 newState) => parent.OnStateChanged(newState);
    }
  }

  /// <summary>
  /// Base class for UI components that need exactly 4 state subscriptions.
  /// </summary>
  public abstract class MultiStateUIComponent<T1, T2, T3, T4> : ReactiveUIComponent
      where T1 : unmanaged, IGameState
      where T2 : unmanaged, IGameState
      where T3 : unmanaged, IGameState
      where T4 : unmanaged, IGameState
  {
    private StateSubscriber1 subscriber1;
    private StateSubscriber2 subscriber2;
    private StateSubscriber3 subscriber3;
    private StateSubscriber4 subscriber4;

    protected override void SubscribeToStateChanges()
    {
      subscriber1 = new StateSubscriber1(this);
      subscriber2 = new StateSubscriber2(this);
      subscriber3 = new StateSubscriber3(this);
      subscriber4 = new StateSubscriber4(this);

      StateSubscriptionHelper.Subscribe<T1>(subscriber1);
      StateSubscriptionHelper.Subscribe<T2>(subscriber2);
      StateSubscriptionHelper.Subscribe<T3>(subscriber3);
      StateSubscriptionHelper.Subscribe<T4>(subscriber4);
    }

    protected override void UnsubscribeFromStateChanges()
    {
      if (subscriber1 != null) {
        StateSubscriptionHelper.Unsubscribe<T1>(subscriber1);
      }
      if (subscriber2 != null) {
        StateSubscriptionHelper.Unsubscribe<T2>(subscriber2);
      }
      if (subscriber3 != null) {
        StateSubscriptionHelper.Unsubscribe<T3>(subscriber3);
      }
      if (subscriber4 != null) {
        StateSubscriptionHelper.Unsubscribe<T4>(subscriber4);
      }
    }

    public abstract void OnStateChanged(T1 newState);
    public abstract void OnStateChanged(T2 newState);
    public abstract void OnStateChanged(T3 newState);
    public abstract void OnStateChanged(T4 newState);

    private class StateSubscriber1 : IStateSubscriber<T1>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4> parent;
      public StateSubscriber1(MultiStateUIComponent<T1, T2, T3, T4> parent) => this.parent = parent;
      public void OnStateChanged(T1 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber2 : IStateSubscriber<T2>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4> parent;
      public StateSubscriber2(MultiStateUIComponent<T1, T2, T3, T4> parent) => this.parent = parent;
      public void OnStateChanged(T2 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber3 : IStateSubscriber<T3>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4> parent;
      public StateSubscriber3(MultiStateUIComponent<T1, T2, T3, T4> parent) => this.parent = parent;
      public void OnStateChanged(T3 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber4 : IStateSubscriber<T4>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4> parent;
      public StateSubscriber4(MultiStateUIComponent<T1, T2, T3, T4> parent) => this.parent = parent;
      public void OnStateChanged(T4 newState) => parent.OnStateChanged(newState);
    }
  }

  /// <summary>
  /// Base class for UI components that need exactly 5 state subscriptions.
  /// For components that need more than 5 states, consider if they're doing too much!
  /// </summary>
  public abstract class MultiStateUIComponent<T1, T2, T3, T4, T5> : ReactiveUIComponent
      where T1 : unmanaged, IGameState
      where T2 : unmanaged, IGameState
      where T3 : unmanaged, IGameState
      where T4 : unmanaged, IGameState
      where T5 : unmanaged, IGameState
  {
    private StateSubscriber1 subscriber1;
    private StateSubscriber2 subscriber2;
    private StateSubscriber3 subscriber3;
    private StateSubscriber4 subscriber4;
    private StateSubscriber5 subscriber5;

    protected override void SubscribeToStateChanges()
    {
      subscriber1 = new StateSubscriber1(this);
      subscriber2 = new StateSubscriber2(this);
      subscriber3 = new StateSubscriber3(this);
      subscriber4 = new StateSubscriber4(this);
      subscriber5 = new StateSubscriber5(this);

      StateSubscriptionHelper.Subscribe<T1>(subscriber1);
      StateSubscriptionHelper.Subscribe<T2>(subscriber2);
      StateSubscriptionHelper.Subscribe<T3>(subscriber3);
      StateSubscriptionHelper.Subscribe<T4>(subscriber4);
      StateSubscriptionHelper.Subscribe<T5>(subscriber5);
    }

    protected override void UnsubscribeFromStateChanges()
    {
      if (subscriber1 != null) {
        StateSubscriptionHelper.Unsubscribe<T1>(subscriber1);
      }
      if (subscriber2 != null) {
        StateSubscriptionHelper.Unsubscribe<T2>(subscriber2);
      }
      if (subscriber3 != null) {
        StateSubscriptionHelper.Unsubscribe<T3>(subscriber3);
      }
      if (subscriber4 != null) {
        StateSubscriptionHelper.Unsubscribe<T4>(subscriber4);
      }
      if (subscriber5 != null) {
        StateSubscriptionHelper.Unsubscribe<T5>(subscriber5);
      }
    }

    public abstract void OnStateChanged(T1 newState);
    public abstract void OnStateChanged(T2 newState);
    public abstract void OnStateChanged(T3 newState);
    public abstract void OnStateChanged(T4 newState);
    public abstract void OnStateChanged(T5 newState);

    private class StateSubscriber1 : IStateSubscriber<T1>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber1(MultiStateUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T1 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber2 : IStateSubscriber<T2>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber2(MultiStateUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T2 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber3 : IStateSubscriber<T3>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber3(MultiStateUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T3 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber4 : IStateSubscriber<T4>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber4(MultiStateUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T4 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber5 : IStateSubscriber<T5>
    {
      private readonly MultiStateUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber5(MultiStateUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T5 newState) => parent.OnStateChanged(newState);
    }
  }

  /// <summary>
  /// Example: A game HUD that monitors health/score and player level/XP.
  /// Extend the 2-state base class
  /// </summary>
  /*
  public class GameHUDExample : MultiStateUIComponent<GameState, PlayerState>
  {
      [SerializeField] private UnityEngine.UI.Text healthText;
      [SerializeField] private UnityEngine.UI.Text scoreText;
      [SerializeField] private UnityEngine.UI.Text playerLevelText;

      // Automatically subscribed - just implement the required methods!
      public override void OnStateChanged(GameState newGameState)
      {
          healthText.text = $"Health: {newGameState.health}";
          scoreText.text = $"Score: {newGameState.score}";
      }

      public override void OnStateChanged(PlayerState newPlayerState)
      {
          playerLevelText.text = $"Level: {newPlayerState.level}";
      }

      // That's it! Zero boilerplate, zero reflection, maximum performance! 🚀
  }
  */

  /// <summary>
  /// Example: A complex dashboard that needs 4 different state types.
  /// Extends the 4-state base class
  /// </summary>
  /*
  public class ComplexDashboardExample : MultiStateUIComponent<GameState, PlayerState, InventoryState, UIState>
  {
      public override void OnStateChanged(GameState newGameState) 
      { 
          UpdateHealthBar(newGameState.health);
          UpdateScoreDisplay(newGameState.score);
      }

      public override void OnStateChanged(PlayerState newPlayerState) 
      { 
          UpdateLevelDisplay(newPlayerState.level);
          UpdateExperienceBar(newPlayerState.experience);
      }

      public override void OnStateChanged(InventoryState newInventoryState) 
      { 
          UpdateInventoryCount(newInventoryState.itemCount);
      }

      public override void OnStateChanged(UIState newUIState) 
      { 
          if (newUIState.showNotifications)
              ShowNotificationPanel();
      }

      // Clean, fast, type-safe! Perfect for game performance! ✨
  }
  */
}
