namespace ECSReact.Core
{
  /// <summary>
  /// Base class for UI components that need exactly 2 state subscriptions.
  /// Uses unique method names to avoid interface conflicts.
  /// Zero reflection - completely compile-time safe and fast.
  /// </summary>
  public abstract class ReactiveUIComponent<T1, T2> : ReactiveUIComponent
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
      private readonly ReactiveUIComponent<T1, T2> parent;
      public StateSubscriber1(ReactiveUIComponent<T1, T2> parent) => this.parent = parent;
      public void OnStateChanged(T1 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber2 : IStateSubscriber<T2>
    {
      private readonly ReactiveUIComponent<T1, T2> parent;
      public StateSubscriber2(ReactiveUIComponent<T1, T2> parent) => this.parent = parent;
      public void OnStateChanged(T2 newState) => parent.OnStateChanged(newState);
    }
  }

  /// <summary>
  /// Base class for UI components that need exactly 3 state subscriptions.
  /// </summary>
  public abstract class ReactiveUIComponent<T1, T2, T3> : ReactiveUIComponent
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
      private readonly ReactiveUIComponent<T1, T2, T3> parent;
      public StateSubscriber1(ReactiveUIComponent<T1, T2, T3> parent) => this.parent = parent;
      public void OnStateChanged(T1 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber2 : IStateSubscriber<T2>
    {
      private readonly ReactiveUIComponent<T1, T2, T3> parent;
      public StateSubscriber2(ReactiveUIComponent<T1, T2, T3> parent) => this.parent = parent;
      public void OnStateChanged(T2 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber3 : IStateSubscriber<T3>
    {
      private readonly ReactiveUIComponent<T1, T2, T3> parent;
      public StateSubscriber3(ReactiveUIComponent<T1, T2, T3> parent) => this.parent = parent;
      public void OnStateChanged(T3 newState) => parent.OnStateChanged(newState);
    }
  }

  /// <summary>
  /// Base class for UI components that need exactly 4 state subscriptions.
  /// </summary>
  public abstract class ReactiveUIComponent<T1, T2, T3, T4> : ReactiveUIComponent
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
      private readonly ReactiveUIComponent<T1, T2, T3, T4> parent;
      public StateSubscriber1(ReactiveUIComponent<T1, T2, T3, T4> parent) => this.parent = parent;
      public void OnStateChanged(T1 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber2 : IStateSubscriber<T2>
    {
      private readonly ReactiveUIComponent<T1, T2, T3, T4> parent;
      public StateSubscriber2(ReactiveUIComponent<T1, T2, T3, T4> parent) => this.parent = parent;
      public void OnStateChanged(T2 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber3 : IStateSubscriber<T3>
    {
      private readonly ReactiveUIComponent<T1, T2, T3, T4> parent;
      public StateSubscriber3(ReactiveUIComponent<T1, T2, T3, T4> parent) => this.parent = parent;
      public void OnStateChanged(T3 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber4 : IStateSubscriber<T4>
    {
      private readonly ReactiveUIComponent<T1, T2, T3, T4> parent;
      public StateSubscriber4(ReactiveUIComponent<T1, T2, T3, T4> parent) => this.parent = parent;
      public void OnStateChanged(T4 newState) => parent.OnStateChanged(newState);
    }
  }

  /// <summary>
  /// Base class for UI components that need exactly 5 state subscriptions.
  /// For components that need more than 5 states, consider if they're doing too much!
  /// </summary>
  public abstract class ReactiveUIComponent<T1, T2, T3, T4, T5> : ReactiveUIComponent
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
      private readonly ReactiveUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber1(ReactiveUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T1 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber2 : IStateSubscriber<T2>
    {
      private readonly ReactiveUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber2(ReactiveUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T2 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber3 : IStateSubscriber<T3>
    {
      private readonly ReactiveUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber3(ReactiveUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T3 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber4 : IStateSubscriber<T4>
    {
      private readonly ReactiveUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber4(ReactiveUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T4 newState) => parent.OnStateChanged(newState);
    }

    private class StateSubscriber5 : IStateSubscriber<T5>
    {
      private readonly ReactiveUIComponent<T1, T2, T3, T4, T5> parent;
      public StateSubscriber5(ReactiveUIComponent<T1, T2, T3, T4, T5> parent) => this.parent = parent;
      public void OnStateChanged(T5 newState) => parent.OnStateChanged(newState);
    }
  }
}