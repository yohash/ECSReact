using UnityEngine;
using System.Collections.Generic;
using System;

namespace ECSReact.Core
{
  /// <summary>
  /// Static class that bridges UI events to C# events for UI component subscriptions.
  /// This is where the ECS world connects to the UI callback system.
  /// </summary>
  public static class UIStateNotifier
  {
    private static readonly Dictionary<Type, Action<UIEvent>> eventProcessors = new();

    /// <summary>
    /// Register a processor for a specific UI event type.
    /// This allows code generation to register handlers without modifying core files.
    /// </summary>
    public static void RegisterEventProcessor<T>(Action<T> processor) where T : UIEvent
    {
      eventProcessors[typeof(T)] = evt => processor((T)evt);
    }

    /// <summary>
    /// Central processing method that dispatches UI events to appropriate handlers.
    /// Uses registered processors to handle specific event types.
    /// </summary>
    public static void ProcessEvent(UIEvent uiEvent)
    {
      if (eventProcessors.TryGetValue(uiEvent.GetType(), out var processor)) {
        processor(uiEvent);
      } else {
        Debug.LogWarning($"Unhandled UI event type: {uiEvent.GetType().Name}");
      }
    }
  }
}
