using ECSReact.Core;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Tools
{
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
  //public partial class SpendMatterActionTracker : TypedActionTrackingSystem<SpendMatterAction>
  //{
  //  protected override void OnActionTracked(SpendMatterAction action, Entity actionEntity)
  //  {
  //    Debug.Log($"Tracked SpendMatterAction: amount={action.amount}, itemId={action.itemId}");
  //  }
  //}
}