using ECSReact.Core;
using System;
using System.Reflection;
using System.Text;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Editor
{
  /// <summary>
  /// Tracks action dispatching and processing for the State Viewer using pure ECS systems.
  /// No modifications to Store or UIEventQueue required - tracks through ECS queries.
  /// </summary>
  public static class ActionHistoryTracker
  {
    public static event Action<string, string> OnActionDispatched;
    public static event Action<string, string> OnUIEventGenerated;

    private static bool isTracking = false;
    private static ActionTrackingSystem trackingSystem;

    /// <summary>
    /// Enable action tracking. Called automatically when State Viewer is opened.
    /// </summary>
    public static void StartTracking()
    {
      if (isTracking)
        return;

      isTracking = true;

      // Subscribe to UI event tracking
      UIEventQueue.OnUIEventProcessed += RecordUIEvent;

      // Find or create the tracking system
      var world = World.DefaultGameObjectInjectionWorld;
      if (world != null) {
        trackingSystem = world.GetExistingSystemManaged<ActionTrackingSystem>();
        if (trackingSystem == null) {
          trackingSystem = world.CreateSystemManaged<ActionTrackingSystem>();
          world.GetExistingSystemManaged<MiddlewareSystemGroup>()?.AddSystemToUpdateList(trackingSystem);
        }
        trackingSystem.StartTracking();
      }

      Debug.Log("Action History Tracker: Started ECS-based tracking");
    }

    /// <summary>
    /// Disable action tracking to reduce performance overhead.
    /// </summary>
    public static void StopTracking()
    {
      if (!isTracking)
        return;

      isTracking = false;

      // Unsubscribe from UI event tracking
      UIEventQueue.OnUIEventProcessed -= RecordUIEvent;

      // Stop the tracking system
      if (trackingSystem != null) {
        trackingSystem.StopTracking();
      }

      Debug.Log("Action History Tracker: Stopped tracking");
    }

    /// <summary>
    /// Record a UI event. Called by UIEventQueue callback.
    /// </summary>
    private static void RecordUIEvent(UIEvent uiEvent)
    {
      if (!isTracking)
        return;

      try {
        string eventType = uiEvent.GetType().Name;
        string priority = uiEvent.priority.ToString();

        OnUIEventGenerated?.Invoke(eventType, priority);
      } catch (Exception ex) {
        Debug.LogError($"Action History Tracker: Error recording UI event: {ex.Message}");
      }
    }

    /// <summary>
    /// Internal method called by ActionTrackingSystem to record actions.
    /// </summary>
    internal static void RecordActionFromSystem(string actionType, string parameters)
    {
      if (!isTracking)
        return;

      try {
        OnActionDispatched?.Invoke(actionType, parameters);
      } catch (Exception ex) {
        Debug.LogError($"Action History Tracker: Error recording action {actionType}: {ex.Message}");
      }
    }

    /// <summary>
    /// Format action parameters for display in the history.
    /// </summary>
    internal static string FormatActionParameters(object action, Type actionType)
    {
      var sb = new StringBuilder();
      var fields = actionType.GetFields(BindingFlags.Public | BindingFlags.Instance);

      for (int i = 0; i < fields.Length; i++) {
        if (i > 0)
          sb.Append(", ");

        var field = fields[i];
        var value = field.GetValue(action);

        // Format value based on type
        string formattedValue = FormatValue(value, field.FieldType);
        sb.Append($"{field.Name}: {formattedValue}");
      }

      return sb.ToString();
    }

    /// <summary>
    /// Format a value for display based on its type.
    /// </summary>
    private static string FormatValue(object value, Type type)
    {
      if (value == null)
        return "null";

      if (type == typeof(float)) {
        return ((float)value).ToString("F2");
      } else if (type == typeof(bool)) {
        return value.ToString().ToLower();
      } else if (type.Name.Contains("FixedString")) {
        return $"\"{value}\"";
      } else if (type.Name.Contains("float3")) {
        return value.ToString();
      } else if (type.Name == "Entity") {
        return value.ToString();
      } else if (type == typeof(string)) {
        return $"\"{value}\"";
      }

      return value.ToString();
    }
  }
}
