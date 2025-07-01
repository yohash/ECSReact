using Unity.Entities;
using System;

namespace ECSReact.Core
{
  /// <summary>
  /// ECS system that detects state changes and queues UI events.
  /// This system compares current state with previous frame state and dispatches events.
  /// </summary>
  [UINotificationSystem]
  public abstract partial class StateChangeNotificationSystem<T> : SystemBase
      where T : unmanaged, IGameState, IEquatable<T>
  {
    private T lastState;
    private bool hasLastState = false;

    protected override void OnUpdate()
    {
      if (!SystemAPI.HasSingleton<T>()) {
        return;
      }

      var currentState = SystemAPI.GetSingleton<T>();

      // Compare with last frame's state
      if (!hasLastState || !currentState.Equals(lastState)) {
        var stateEvent = CreateStateChangeEvent(currentState, lastState, hasLastState);
        UIEventQueue.QueueEvent(stateEvent);

        lastState = currentState;
        hasLastState = true;
      }
    }

    /// <summary>
    /// Override this method to create the appropriate UI event for your state type.
    /// </summary>
    protected abstract UIEvent CreateStateChangeEvent(T newState, T oldState, bool hasOldState);
  }
}
