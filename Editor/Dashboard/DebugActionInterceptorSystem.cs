using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using ECSReact.Core;
using System.Linq;

namespace ECSReact.Editor
{
  /// <summary>
  /// ECS System that monitors action entities to feed data to the debug dashboard.
  /// This runs in the editor and captures action data before cleanup.
  /// </summary>
  [UpdateInGroup(typeof(InitializationSystemGroup))]
  [UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
  public partial class DebugActionInterceptorSystem : SystemBase
  {
    public struct ActionDebugData
    {
      public Type actionType;
      public object actionData;
      public float timestamp;
      public int frame;
    }

    private static Queue<ActionDebugData> actionQueue = new Queue<ActionDebugData>();
    private static Dictionary<Type, EntityQuery> actionQueries = new Dictionary<Type, EntityQuery>();
    private static List<Type> cachedActionTypes;
    public static event Action<ActionDebugData> OnActionDetected;

    protected override void OnCreate()
    {
      base.OnCreate();

      // Only run in editor
      Enabled = Application.isEditor;

      if (Enabled) {
        DiscoverActionTypes();
        CreateActionQueries();
      }
    }

    private void DiscoverActionTypes()
    {
      if (cachedActionTypes == null) {
        cachedActionTypes = new List<Type>();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies) {
          try {
            var types = assembly.GetTypes();
            foreach (var type in types) {
              if (type.IsValueType && !type.IsAbstract &&
                  typeof(IGameAction).IsAssignableFrom(type) &&
                  typeof(IComponentData).IsAssignableFrom(type)) {
                cachedActionTypes.Add(type);
              }
            }
          } catch (Exception e) {
            // Skip assemblies we can't reflect
            Debug.LogWarning($"Failed to reflect assembly {assembly.FullName}: {e.Message}");
          }
        }

        Debug.Log($"[DebugActionInterceptor] Discovered {cachedActionTypes.Count} action types");
      }
    }

    private void CreateActionQueries()
    {
      foreach (var actionType in cachedActionTypes) {
        try {
          var queryDesc = new EntityQueryDesc
          {
            All = new[]
            {
              ComponentType.ReadOnly(actionType),
              ComponentType.ReadOnly<ActionTag>()
            }
          };

          actionQueries[actionType] = GetEntityQuery(queryDesc);
        } catch (Exception e) {
          Debug.LogError($"Failed to create query for action type {actionType.Name}: {e.Message}");
        }
      }
    }

    protected override void OnUpdate()
    {
      var nonEmpty = actionQueries.Where(q => !q.Value.IsEmpty);

      // Check all action queries
      foreach (var kvp in nonEmpty) {
        var actionType = kvp.Key;
        var query = kvp.Value;

        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var entity in entities) {
          try {
            // Get action data using GetComponentObject for runtime type handling
            var actionData = ComponentDataGetter.GetComponentData(EntityManager, entity, actionType);

            var debugData = new ActionDebugData
            {
              actionType = actionType,
              actionData = actionData,
              timestamp = (float)World.Time.ElapsedTime,
              frame = (int)(World.Time.ElapsedTime / World.Time.fixedDeltaTime)
            };

            actionQueue.Enqueue(debugData);
            OnActionDetected?.Invoke(debugData);

            // Keep queue size manageable
            while (actionQueue.Count > 1000) {
              actionQueue.Dequeue();
            }
          } catch (Exception e) {
            Debug.LogError($"Failed to capture action data for {actionType.Name}: {e.Message}\n" +
              $"{e.StackTrace}");
          }
        }

        entities.Dispose();
      }
    }

    public static List<ActionDebugData> GetRecentActions(int maxCount = 100)
    {
      var result = new List<ActionDebugData>();
      var tempArray = actionQueue.ToArray();

      int startIndex = Math.Max(0, tempArray.Length - maxCount);
      for (int i = startIndex; i < tempArray.Length; i++) {
        result.Add(tempArray[i]);
      }

      return result;
    }

    public static void ClearHistory()
    {
      actionQueue.Clear();
    }
  }

  /// <summary>
  /// MonoBehaviour helper to ensure the debug system is active in editor
  /// </summary>
  [ExecuteInEditMode]
  public class DebugSystemBootstrapper : MonoBehaviour
  {
    private static DebugSystemBootstrapper instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
      if (Application.isEditor && instance == null) {
        var go = new GameObject("[ECS Debug Systems]", typeof(DebugSystemBootstrapper));
        go.hideFlags = HideFlags.HideAndDontSave;
        instance = go.GetComponent<DebugSystemBootstrapper>();
      }
    }

    private void OnEnable()
    {
      if (Application.isEditor) {
        // Ensure our debug system is registered with the world
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated) {
          var system = world.GetOrCreateSystem<DebugActionInterceptorSystem>();
          Debug.Log("[DebugSystemBootstrapper] Debug action interceptor system activated");
        }
      }
    }
  }
}