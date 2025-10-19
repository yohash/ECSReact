using System.Collections.Generic;
using UnityEngine;
using System;

namespace ECSReact.Core
{
  /// <summary>
  /// Helper class for type-safe state subscriptions.
  /// Uses a registration system that can be extended via code generation without modifying core files.
  /// </summary>
  public static partial class StateSubscriptionHelper
  {
    private static readonly Dictionary<Type, Action<object>> subscribers = new();
    private static readonly Dictionary<Type, Action<object>> unsubscribers = new();

    /// <summary>
    /// Register subscription and unsubscription handlers for a specific state type.
    /// This allows code generation to register handlers without modifying core files.
    /// </summary>
    public static void RegisterStateSubscriptionHandlers<T>(
      Action<IStateSubscriber<T>> subscribeHandler,
      Action<IStateSubscriber<T>> unsubscribeHandler)
        where T : unmanaged, IGameState
    {
      subscribers[typeof(T)] = subscriber => subscribeHandler((IStateSubscriber<T>)subscriber);
      unsubscribers[typeof(T)] = subscriber => unsubscribeHandler((IStateSubscriber<T>)subscriber);
    }

    /// <summary>
    /// Subscribe to state changes for a specific state type.
    /// Uses registered handlers to connect to the appropriate UI event.
    /// </summary>
    public static void Subscribe<T>(IStateSubscriber<T> subscriber)
      where T : unmanaged, IGameState
    {
      if (subscribers.TryGetValue(typeof(T), out var handler)) {
        handler(subscriber);
      } else {
        Debug.LogWarning($"No subscription handler registered for state type {typeof(T).Name}. " +
          "Make sure code generation has run or manually register handlers.");
      }

      // Get current state and invoke immediately to initialize
      if (SceneStateManager.Instance.GetState(out T currentState)) {
        subscriber.OnStateChanged(currentState);
      }
    }

    /// <summary>
    /// Unsubscribe from state changes for a specific state type.
    /// Uses registered handlers to disconnect from the appropriate UI event.
    /// </summary>
    public static void Unsubscribe<T>(IStateSubscriber<T> subscriber)
      where T : unmanaged, IGameState
    {
      if (unsubscribers.TryGetValue(typeof(T), out var handler)) {
        handler(subscriber);
      } else {
        Debug.LogWarning($"No unsubscription handler registered for state type {typeof(T).Name}. " +
          "Make sure code generation has run or manually register handlers.");
      }
    }
  }
}
