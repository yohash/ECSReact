using ECSReact.Core;
using ECSReact.Samples.SampleCodeGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Tools
{
  /// <summary>
  /// Tracks action dispatching and processing for the State Viewer using pure ECS systems.
  /// No modifications to Store or UIEventQueue required - tracks through ECS queries.
  /// </summary>
  public static class ActionHistoryTracker
  {
    public static event System.Action<string, string> OnActionDispatched;
    public static event System.Action<string, string> OnUIEventGenerated;

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

  /// <summary>
  /// ECS system that tracks all action entities by querying for ActionTag components.
  /// This approach requires no modifications to Store - it just observes the ECS world.
  /// </summary>
  [UpdateInGroup(typeof(MiddlewareSystemGroup))]
  [UpdateBefore(typeof(SimulationSystemGroup))]
  public partial class ActionTrackingSystem : SystemBase
  {
    private EntityQuery actionQuery;
    private Dictionary<Entity, bool> trackedActions = new Dictionary<Entity, bool>();
    private bool isTrackingEnabled = false;

    protected override void OnCreate()
    {
      base.OnCreate();

      // Query for all entities with ActionTag (all actions)
      actionQuery = GetEntityQuery(ComponentType.ReadOnly<ActionTag>());

      // System is created but not enabled by default
      Enabled = false;
    }

    public void StartTracking()
    {
      isTrackingEnabled = true;
      Enabled = true;
      trackedActions.Clear();
    }

    public void StopTracking()
    {
      isTrackingEnabled = false;
      Enabled = false;
      trackedActions.Clear();
    }

    protected override void OnUpdate()
    {
      if (!isTrackingEnabled)
        return;

      // Get all action entities
      var actionEntities = actionQuery.ToEntityArray(Allocator.Temp);

      foreach (var entity in actionEntities) {
        // Only process each action entity once
        if (trackedActions.ContainsKey(entity))
          continue;

        trackedActions[entity] = true;

        // Try to extract action data using reflection
        ProcessActionEntity(entity);
      }

      actionEntities.Dispose();

      // Clean up tracked actions that no longer exist
      CleanupTrackedActions();
    }

    private void ProcessActionEntity(Entity entity)
    {
      try {
        // Get all components on this action entity
        var componentTypes = EntityManager.GetComponentTypes(entity, Allocator.Temp);

        foreach (var componentType in componentTypes) {
          var type = componentType.GetManagedType();

          // Skip ActionTag and built-in components
          if (type == typeof(ActionTag) || type == null || !IsGameAction(type))
            continue;

          // Get the component data
          var componentData = EntityManager.GetComponentObject<IComponentData>(entity, componentType);
          if (componentData != null) {
            string actionType = type.Name;
            string parameters = ActionHistoryTracker.FormatActionParameters(componentData, type);

            ActionHistoryTracker.RecordActionFromSystem(actionType, parameters);
            break; // Only process the first IGameAction component found
          }
        }

        componentTypes.Dispose();
      } catch (Exception ex) {
        Debug.LogError($"ActionTrackingSystem: Error processing action entity {entity}: {ex.Message}");
      }
    }

    private bool IsGameAction(Type type)
    {
      if (type == null || !type.IsValueType)
        return false;
      return type.GetInterfaces().Any(i => i.Name == "IGameAction");
    }

    private void CleanupTrackedActions()
    {
      // Remove entities that no longer exist from our tracking dictionary
      var keysToRemove = new List<Entity>();

      foreach (var kvp in trackedActions) {
        if (!EntityManager.Exists(kvp.Key)) {
          keysToRemove.Add(kvp.Key);
        }
      }

      foreach (var key in keysToRemove) {
        trackedActions.Remove(key);
      }
    }
  }

  /// <summary>
  /// Generic ECS system that can track specific action types.
  /// Alternative approach for more targeted tracking.
  /// </summary>
  [UpdateInGroup(typeof(MiddlewareSystemGroup))]
  public abstract partial class TypedActionTrackingSystem<T> : SystemBase where T : unmanaged, IGameAction
  {
    private EntityQuery actionQuery;
    private Dictionary<Entity, bool> trackedActions = new Dictionary<Entity, bool>();

    protected override void OnCreate()
    {
      base.OnCreate();

      actionQuery = GetEntityQuery(
          ComponentType.ReadOnly<T>(),
          ComponentType.ReadOnly<ActionTag>()
      );
    }

    protected override void OnUpdate()
    {
      var actionEntities = actionQuery.ToEntityArray(Allocator.Temp);

      foreach (var entity in actionEntities) {
        if (trackedActions.ContainsKey(entity))
          continue;

        trackedActions[entity] = true;

        try {
          var action = EntityManager.GetComponentData<T>(entity);
          OnActionTracked(action, entity);
        } catch (Exception ex) {
          Debug.LogError($"TypedActionTrackingSystem<{typeof(T).Name}>: Error tracking action: {ex.Message}");
        }
      }

      actionEntities.Dispose();

      // Cleanup
      var keysToRemove = new List<Entity>();
      foreach (var kvp in trackedActions) {
        if (!EntityManager.Exists(kvp.Key))
          keysToRemove.Add(kvp.Key);
      }
      foreach (var key in keysToRemove)
        trackedActions.Remove(key);
    }

    protected abstract void OnActionTracked(T action, Entity actionEntity);
  }

  /// <summary>
  /// Example of a typed tracking system for specific actions.
  /// You can create these for actions you want to track in detail.
  /// </summary>
  public partial class SpendMatterActionTracker : TypedActionTrackingSystem<SpendMatterAction>
  {
    protected override void OnActionTracked(SpendMatterAction action, Entity actionEntity)
    {
      Debug.Log($"Tracked SpendMatterAction: amount={action.amount}, itemId={action.itemId}");
    }
  }
}