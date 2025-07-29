using ECSReact.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Editor
{
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
}