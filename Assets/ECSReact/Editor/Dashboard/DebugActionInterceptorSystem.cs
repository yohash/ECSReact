using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using ECSReact.Core;
using System.Linq;

namespace ECSReact.Editor
{
  /// <summary>
  /// ECS System that monitors frame-lane action entities (ActionTag) to feed data to the
  /// debug dashboard. This runs in the editor and captures action data before cleanup.
  /// Fixed-lane actions are captured by FixedLaneDebugActionInterceptorSystem, which feeds
  /// the same history and event.
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
      public bool isFixedLane; // true when captured in the fixed-step action phase
    }

    private static Queue<ActionDebugData> actionQueue = new Queue<ActionDebugData>();
    private readonly Dictionary<Type, EntityQuery> actionQueries = new Dictionary<Type, EntityQuery>();
    private static List<Type> cachedActionTypes;
    public static event Action<ActionDebugData> OnActionDetected;

    protected override void OnCreate()
    {
      base.OnCreate();

      // Only run in editor
      Enabled = Application.isEditor;

      if (Enabled) {
        foreach (var actionType in GetActionTypes()) {
          var query = CreateLaneQuery(actionType, ComponentType.ReadOnly<ActionTag>(), GetEntityQuery);
          if (query.HasValue)
            actionQueries[actionType] = query.Value;
        }
      }
    }

    /// <summary>All IGameAction component types, discovered once and shared by both lanes' interceptors.</summary>
    internal static List<Type> GetActionTypes()
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
      return cachedActionTypes;
    }

    /// <summary>Builds the query for one action type in one lane, via the calling system's GetEntityQuery.</summary>
    internal static EntityQuery? CreateLaneQuery(
      Type actionType, ComponentType laneTag, Func<EntityQueryDesc[], EntityQuery> getEntityQuery)
    {
      try {
        var queryDesc = new EntityQueryDesc
        {
          All = new[]
          {
            ComponentType.ReadOnly(actionType),
            laneTag
          }
        };
        return getEntityQuery(new[] { queryDesc });
      } catch (Exception e) {
        Debug.LogError($"Failed to create query for action type {actionType.Name}: {e.Message}");
        return null;
      }
    }

    protected override void OnUpdate()
    {
      Capture(EntityManager, World, actionQueries, isFixedLane: false);
    }

    /// <summary>Records every action matched by the given queries into the shared history.</summary>
    internal static void Capture(
      EntityManager entityManager, World world, Dictionary<Type, EntityQuery> actionQueries, bool isFixedLane)
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
            var actionData = ComponentDataGetter.GetComponentData(entityManager, entity, actionType);

            var debugData = new ActionDebugData
            {
              actionType = actionType,
              actionData = actionData,
              timestamp = (float)world.Time.ElapsedTime,
              frame = (int)(world.Time.ElapsedTime / world.Time.fixedDeltaTime),
              isFixedLane = isFixedLane
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
  /// Captures fixed-lane ([FixedStepAction]) actions for the debug dashboard.
  /// Fixed-lane action entities only exist inside FixedActionSystemGroup, so this runs there,
  /// right after FixedActionCommandBufferSystem plays them back and before any fixed-lane
  /// middleware can filter them. Feeds the same history and event as DebugActionInterceptorSystem.
  /// </summary>
  [UpdateInGroup(typeof(FixedActionSystemGroup))]
  [UpdateAfter(typeof(FixedActionCommandBufferSystem))]
  [UpdateBefore(typeof(FixedMiddlewareSystemGroup))]
  public partial class FixedLaneDebugActionInterceptorSystem : SystemBase
  {
    private readonly Dictionary<Type, EntityQuery> actionQueries = new Dictionary<Type, EntityQuery>();

    protected override void OnCreate()
    {
      base.OnCreate();

      // Only run in editor
      Enabled = Application.isEditor;

      if (Enabled) {
        foreach (var actionType in DebugActionInterceptorSystem.GetActionTypes()) {
          var query = DebugActionInterceptorSystem.CreateLaneQuery(
            actionType, ComponentType.ReadOnly<FixedActionTag>(), GetEntityQuery);
          if (query.HasValue)
            actionQueries[actionType] = query.Value;
        }
      }
    }

    protected override void OnUpdate()
    {
      DebugActionInterceptorSystem.Capture(EntityManager, World, actionQueries, isFixedLane: true);
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
          world.GetOrCreateSystem<DebugActionInterceptorSystem>();
          world.GetOrCreateSystem<FixedLaneDebugActionInterceptorSystem>();
          Debug.Log("[DebugSystemBootstrapper] Debug action interceptor systems activated");
        }
      }
    }
  }
}