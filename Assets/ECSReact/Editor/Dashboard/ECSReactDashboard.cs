using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor
{
  public class ECSReactDebugDashboard : EditorWindow
  {
    // Cache Architecture Core
    private class StateDebugInfo
    {
      public Type stateType;
      public object currentValue;
      public object previousValue;
      public int changeCount;
      public float lastChangeTime;
      public float changeFrequency; // Changes per second
      public Entity entity;
      public bool isExpanded;

      private float[] recentChangeTimes = new float[10];
      private int changeTimeIndex = 0;

      public void RecordChange(float time)
      {
        changeCount++;
        lastChangeTime = time;

        // Track recent changes for frequency calculation
        recentChangeTimes[changeTimeIndex] = time;
        changeTimeIndex = (changeTimeIndex + 1) % recentChangeTimes.Length;

        // Calculate frequency from recent changes
        float oldestTime = recentChangeTimes.Min();
        float timeSpan = time - oldestTime;
        if (timeSpan > 0) {
          int validChanges = recentChangeTimes.Count(t => t > oldestTime);
          changeFrequency = validChanges / timeSpan;
        }
      }

      public StateHeat GetHeat()
      {
        // Heat based on change frequency
        if (changeFrequency > 10f)
          return StateHeat.Hot;
        if (changeFrequency > 1f)
          return StateHeat.Warm;
        if (changeCount > 0)
          return StateHeat.Cold;
        return StateHeat.None;
      }
    }

    private enum StateHeat { None, Cold, Warm, Hot }

    private class ActionDebugInfo
    {
      public Type actionType;
      public object actionData;
      public float dispatchTime;
      public int frameNumber;
      public bool isExpanded;
    }

    private class UIEventDebugInfo
    {
      public Type eventType;
      public object eventData;
      public float processTime;
      public int frameNumber;
      public bool wasRateLimited;
      public UIEventPriority priority;
      public bool isExpanded;
    }

    // Dashboard State
    private enum DashboardTab { States, Actions, UIEvents }
    private DashboardTab currentTab = DashboardTab.States;

    // Caches
    private Dictionary<Type, StateDebugInfo> stateCache = new Dictionary<Type, StateDebugInfo>();
    private List<ActionDebugInfo> actionHistory = new List<ActionDebugInfo>();
    private List<UIEventDebugInfo> uiEventHistory = new List<UIEventDebugInfo>();
    private Dictionary<Type, EntityQuery> stateQueries = new Dictionary<Type, EntityQuery>();

    // Settings
    private const int MAX_ACTION_HISTORY = 100;
    private const int MAX_UIEVENT_HISTORY = 100;
    private bool autoRefresh = true;
    private float refreshInterval = 0.1f;
    private float lastRefreshTime;

    // UI State
    private Vector2 scrollPosition;
    private string searchFilter = "";
    private bool showOnlyChangedStates = false;

    // Reflection Cache
    private static MethodInfo dispatchMethod;
    private static FieldInfo commandBufferField;

    // Event Tracking
    private static ECSReactDebugDashboard instance;
    private bool isSubscribed = false;

    [MenuItem("ECS React/Dashboard", priority = 300)]
    public static void ShowWindow()
    {
      instance = GetWindow<ECSReactDebugDashboard>("ECSReact Dashboard");
      instance.minSize = new Vector2(400, 600);
    }

    private void OnEnable()
    {
      instance = this;
      InitializeReflectionCache();
      SubscribeToEvents();
      RefreshStateCache();

      // Ensure debug systems are active
      EnsureDebugSystemsActive();
    }

    private void EnsureDebugSystemsActive()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      if (world != null && world.IsCreated) {
        world.GetOrCreateSystem<DebugActionInterceptorSystem>();
      }
    }

    private void OnDisable()
    {
      UnsubscribeFromEvents();
    }

    private void InitializeReflectionCache()
    {
      // Cache Store.Dispatch method for potential future interception
      if (dispatchMethod == null) {
        var storeType = typeof(Store);
        dispatchMethod = storeType.GetMethod("Dispatch");
        commandBufferField = storeType.GetField("commandBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
      }
    }

    private void SubscribeToEvents()
    {
      if (isSubscribed)
        return;

      // Subscribe to UIEventQueue events
      UIEventQueue.OnUIEventProcessed += OnUIEventProcessed;

      // Subscribe to action detection
      DebugActionInterceptorSystem.OnActionDetected += OnActionDetected;

      // Hook into Store dispatch using harmony or manual polling
      EditorApplication.update += PollForChanges;

      isSubscribed = true;
    }

    private void UnsubscribeFromEvents()
    {
      if (!isSubscribed)
        return;

      UIEventQueue.OnUIEventProcessed -= OnUIEventProcessed;
      DebugActionInterceptorSystem.OnActionDetected -= OnActionDetected;
      EditorApplication.update -= PollForChanges;

      isSubscribed = false;
    }

    private void OnActionDetected(DebugActionInterceptorSystem.ActionDebugData data)
    {
      var info = new ActionDebugInfo
      {
        actionType = data.actionType,
        actionData = data.actionData,
        dispatchTime = data.timestamp,
        frameNumber = data.frame,
        isExpanded = false
      };

      actionHistory.Insert(0, info);
      if (actionHistory.Count > MAX_ACTION_HISTORY) {
        actionHistory.RemoveAt(actionHistory.Count - 1);
      }

      Repaint();
    }

    private void PollForChanges()
    {
      if (!autoRefresh)
        return;
      if (Time.realtimeSinceStartup - lastRefreshTime < refreshInterval)
        return;

      RefreshStateCache();
      lastRefreshTime = Time.realtimeSinceStartup;
    }

    private void RefreshStateCache()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null || !world.IsCreated) {
        return;
      }

      var entityManager = world.EntityManager;

      // Try to use StateRegistryService first
      var registries = StateRegistryService.AllRegistries;
      if (registries != null) {
        foreach (var registry in registries) {
          RefreshUsingRegistry(entityManager, registry);
        }
      } else {
        // Fallback to discovery from all registries
        var allStates = StateRegistryService.GetAllStatesFromAllRegistries();
        if (allStates.Count > 0) {
          RefreshUsingStateInfos(entityManager, allStates);
        } else {
          Debug.LogWarning("[ECSReactDebugDashboard] No state registry found. Please generate one using 'ECS React → Generate State Registry'");
        }
      }
    }

    private void RefreshUsingRegistry(EntityManager entityManager, IStateRegistry registry)
    {
      foreach (var kvp in registry.AllStates) {
        var stateType = kvp.Key;
        var stateInfo = kvp.Value;
        RefreshSingleState(entityManager, stateType, stateInfo);
      }
    }

    private void RefreshUsingStateInfos(EntityManager entityManager, Dictionary<Type, IStateInfo> stateInfos)
    {
      foreach (var kvp in stateInfos) {
        var stateType = kvp.Key;
        var stateInfo = kvp.Value;
        RefreshSingleState(entityManager, stateType, stateInfo);
      }
    }

    private void RefreshSingleState(EntityManager entityManager, Type stateType, IStateInfo stateInfo)
    {
      try {
        // Get or create query for this state type
        if (!stateQueries.ContainsKey(stateType)) {
          var queryDesc = new EntityQueryDesc
          {
            All = new[] { ComponentType.ReadOnly(stateType) }
          };
          stateQueries[stateType] = entityManager.CreateEntityQuery(queryDesc);
        }

        var query = stateQueries[stateType];
        if (query.CalculateEntityCount() > 0) {
          var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
          if (entities.Length > 0) {
            var entity = entities[0]; // Singleton state
            var currentValue = stateInfo.GetComponent(entityManager, entity);

            if (!stateCache.ContainsKey(stateType)) {
              stateCache[stateType] = new StateDebugInfo
              {
                stateType = stateType,
                entity = entity,
                currentValue = currentValue,
                previousValue = currentValue
              };
            } else {
              var info = stateCache[stateType];
              if (!AreValuesEqual(info.currentValue, currentValue)) {
                info.previousValue = info.currentValue;
                info.currentValue = currentValue;
                info.RecordChange(Time.realtimeSinceStartup);
              }
            }
          }
          entities.Dispose();
        }
      } catch (Exception e) {
        Debug.LogError($"Failed to query state {stateType.Name}: {e.Message}");
      }
    }

    private bool AreValuesEqual(object a, object b)
    {
      if (a == null && b == null) {
        return true;
      }
      if (a == null || b == null) {
        return false;
      }

      // Use IEquatable if available
      var equatableType = typeof(IEquatable<>).MakeGenericType(a.GetType());
      if (equatableType.IsAssignableFrom(a.GetType())) {
        var equalsMethod = equatableType.GetMethod("Equals");
        return (bool)equalsMethod.Invoke(a, new[] { b });
      }

      return a.Equals(b);
    }

    private void OnUIEventProcessed(UIEvent uiEvent)
    {
      var info = new UIEventDebugInfo
      {
        eventType = uiEvent.GetType(),
        eventData = uiEvent,
        processTime = Time.realtimeSinceStartup,
        frameNumber = Time.frameCount,
        priority = uiEvent.priority,
        wasRateLimited = false // TODO: Track this
      };

      uiEventHistory.Insert(0, info);
      if (uiEventHistory.Count > MAX_UIEVENT_HISTORY) {
        uiEventHistory.RemoveAt(uiEventHistory.Count - 1);
      }

      Repaint();
    }

    private void OnGUI()
    {
      DrawToolbar();
      DrawTabContent();
    }

    private void DrawToolbar()
    {
      EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

      // Tab buttons
      if (GUILayout.Toggle(currentTab == DashboardTab.States, $"States ({stateCache.Count})", EditorStyles.toolbarButton))
        currentTab = DashboardTab.States;
      if (GUILayout.Toggle(currentTab == DashboardTab.Actions, $"Actions ({actionHistory.Count})", EditorStyles.toolbarButton))
        currentTab = DashboardTab.Actions;
      if (GUILayout.Toggle(currentTab == DashboardTab.UIEvents, $"UI Events ({uiEventHistory.Count})", EditorStyles.toolbarButton))
        currentTab = DashboardTab.UIEvents;

      GUILayout.FlexibleSpace();

      // Settings
      autoRefresh = GUILayout.Toggle(autoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(40));
      if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) {
        RefreshStateCache();
      }

      EditorGUILayout.EndHorizontal();

      // Stats bar
      EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

      var activeStates = stateCache.Values.Count(s => s.changeCount > 0);
      var totalChanges = stateCache.Values.Sum(s => s.changeCount);

      GUILayout.Label($"Active States: {activeStates}/{stateCache.Count}", EditorStyles.miniLabel);
      GUILayout.Label($"Total Changes: {totalChanges}", EditorStyles.miniLabel);
      GUILayout.Label($"FPS: {(1f / Time.deltaTime):F0}", EditorStyles.miniLabel);

      GUILayout.FlexibleSpace();

      EditorGUILayout.EndHorizontal();

      // Search bar
      EditorGUILayout.BeginHorizontal();
      GUILayout.Label("Search:", GUILayout.Width(50));
      searchFilter = EditorGUILayout.TextField(searchFilter);
      if (currentTab == DashboardTab.States) {
        showOnlyChangedStates = GUILayout.Toggle(showOnlyChangedStates, "Changed Only", GUILayout.Width(100));
      }
      EditorGUILayout.EndHorizontal();
    }

    private void DrawTabContent()
    {
      // Check if we're in play mode      
      if (!EditorApplication.isPlaying) {
        EditorGUILayout.HelpBox(
          "ECS React Dashboard is only available in Play Mode.",
          MessageType.Info);
        return;
      }

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

      switch (currentTab) {
        case DashboardTab.States:
          DrawStatesView();
          break;
        case DashboardTab.Actions:
          DrawActionsView();
          break;
        case DashboardTab.UIEvents:
          DrawUIEventsView();
          break;
      }

      EditorGUILayout.EndScrollView();
    }

    private void DrawStatesView()
    {
      // Check if we have a registry
      if (!StateRegistryService.HasRegistry && stateCache.Count == 0) {
        EditorGUILayout.HelpBox(
            "No State Registry found!\n\n" +
            "To use the State Viewer, you need to generate a State Registry:\n" +
            "1. Go to 'ECS React → Generate State Registry'\n" +
            "2. Select the namespaces containing your IGameState types\n" +
            "3. Click 'Generate Registry'\n\n" +
            "The registry will auto-register and states will appear here.",
            MessageType.Warning
        );
        return;
      }

      var filteredStates = stateCache.Values
          .Where(s => string.IsNullOrEmpty(searchFilter) ||
                     s.stateType.Name.ToLower().Contains(searchFilter.ToLower()))
          .Where(s => !showOnlyChangedStates || s.changeCount > 0)
          .OrderByDescending(s => s.changeFrequency)
          .ToList();

      EditorGUILayout.LabelField($"Tracking {filteredStates.Count} states", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      foreach (var state in filteredStates) {
        DrawStateInfo(state);
      }
    }

    private void DrawStateInfo(StateDebugInfo info)
    {
      var heat = info.GetHeat();
      var heatColor = GetHeatColor(heat);

      EditorGUILayout.BeginVertical(EditorStyles.helpBox);
      EditorGUILayout.BeginHorizontal();

      // Heat indicator
      var heatRect = GUILayoutUtility.GetRect(20, 20);
      EditorGUI.DrawRect(heatRect, heatColor);

      // State name and expand toggle
      EditorGUILayout.Space(5);
      info.isExpanded = EditorGUILayout.Foldout(info.isExpanded, $"  {info.stateType.Name}", true);

      // Stats
      GUILayout.FlexibleSpace();
      GUILayout.Label($"Changes: {info.changeCount}", GUILayout.Width(80));
      GUILayout.Label($"Freq: {info.changeFrequency:F1}/s", GUILayout.Width(80));

      EditorGUILayout.EndHorizontal();

      if (info.isExpanded && info.currentValue != null) {
        EditorGUI.indentLevel++;
        DrawObjectFields(info.currentValue);

        if (info.previousValue != null && !AreValuesEqual(info.currentValue, info.previousValue)) {
          EditorGUILayout.LabelField("Previous Value:", EditorStyles.boldLabel);
          DrawObjectFields(info.previousValue);
        }

        // Quick test actions
        EditorGUILayout.Space();
        if (GUILayout.Button("Copy State JSON", GUILayout.Height(20))) {
          var json = JsonUtility.ToJson(info.currentValue, true);
          GUIUtility.systemCopyBuffer = json;
          Debug.Log($"Copied {info.stateType.Name} to clipboard");
        }

        EditorGUI.indentLevel--;
      }

      EditorGUILayout.EndVertical();
      EditorGUILayout.Space(2);
    }

    private void DrawObjectFields(object obj)
    {
      if (obj == null)
        return;

      var type = obj.GetType();
      var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

      foreach (var field in fields) {
        var value = field.GetValue(obj);
        var valueStr = value?.ToString() ?? "null";

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(field.Name, GUILayout.Width(150));

        if (value != null && IsComplexType(value.GetType())) {
          if (GUILayout.Button("Inspect", GUILayout.Width(60))) {
            // Could open a separate inspector window
          }
        } else {
          EditorGUILayout.SelectableLabel(valueStr, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        EditorGUILayout.EndHorizontal();
      }
    }

    private bool IsComplexType(Type type)
    {
      return !type.IsPrimitive && type != typeof(string) && !type.IsEnum;
    }

    private void DrawActionsView()
    {
      var filteredActions = actionHistory
          .Where(a => string.IsNullOrEmpty(searchFilter) ||
                     a.actionType.Name.ToLower().Contains(searchFilter.ToLower()))
          .ToList();

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField($"Action History ({filteredActions.Count}/{actionHistory.Count})", EditorStyles.boldLabel);
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Clear", GUILayout.Width(50))) {
        actionHistory.Clear();
        DebugActionInterceptorSystem.ClearHistory();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // Group by frame
      var actionsByFrame = filteredActions.GroupBy(a => a.frameNumber);

      foreach (var frameGroup in actionsByFrame.Take(20)) {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var frameTime = frameGroup.First().dispatchTime;
        var timeSinceFrame = Time.realtimeSinceStartup - frameTime;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Frame {frameGroup.Key}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"{timeSinceFrame:F2}s ago", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        foreach (var action in frameGroup) {
          DrawActionInfo(action);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
      }
    }

    private void DrawActionInfo(ActionDebugInfo info)
    {
      EditorGUILayout.BeginHorizontal();

      // Action type indicator
      var actionColor = GetActionTypeColor(info.actionType);
      var colorRect = GUILayoutUtility.GetRect(10, EditorGUIUtility.singleLineHeight);
      EditorGUI.DrawRect(colorRect, actionColor);

      // Action info
      info.isExpanded = EditorGUILayout.Foldout(info.isExpanded, info.actionType.Name, true);

      GUILayout.FlexibleSpace();

      EditorGUILayout.EndHorizontal();

      if (info.isExpanded && info.actionData != null) {
        EditorGUI.indentLevel++;
        DrawObjectFields(info.actionData);
        EditorGUI.indentLevel--;
      }
    }

    private Color GetActionTypeColor(Type actionType)
    {
      // Generate consistent colors based on type name
      var hash = actionType.Name.GetHashCode();
      var hue = (hash & 0xFF) / 255f;
      return Color.HSVToRGB(hue, 0.6f, 0.9f);
    }

    private void DrawUIEventsView()
    {
      var filteredEvents = uiEventHistory
          .Where(e => string.IsNullOrEmpty(searchFilter) ||
                     e.eventType.Name.ToLower().Contains(searchFilter.ToLower()))
          .ToList();

      EditorGUILayout.LabelField($"UI Event History ({filteredEvents.Count})", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      // Group by frame
      var eventsByFrame = filteredEvents.GroupBy(e => e.frameNumber);

      foreach (var frameGroup in eventsByFrame.Take(20)) {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Frame {frameGroup.Key}", EditorStyles.boldLabel);

        foreach (var evt in frameGroup) {
          DrawUIEventInfo(evt);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
      }
    }

    private void DrawUIEventInfo(UIEventDebugInfo info)
    {
      EditorGUILayout.BeginHorizontal();

      // Priority indicator
      var priorityColor = GetPriorityColor(info.priority);
      var priorityRect = GUILayoutUtility.GetRect(10, EditorGUIUtility.singleLineHeight);
      EditorGUI.DrawRect(priorityRect, priorityColor);

      // Event info
      info.isExpanded = EditorGUILayout.Foldout(info.isExpanded, info.eventType.Name, true);

      if (info.wasRateLimited) {
        GUILayout.Label("[Rate Limited]", EditorStyles.miniLabel);
      }

      GUILayout.FlexibleSpace();
      GUILayout.Label($"{(Time.realtimeSinceStartup - info.processTime):F2}s ago", EditorStyles.miniLabel);

      EditorGUILayout.EndHorizontal();

      if (info.isExpanded && info.eventData != null) {
        EditorGUI.indentLevel++;
        DrawObjectFields(info.eventData);
        EditorGUI.indentLevel--;
      }
    }

    private Color GetHeatColor(StateHeat heat)
    {
      switch (heat) {
        case StateHeat.Hot:
          return new Color(1f, 0.2f, 0.2f);
        case StateHeat.Warm:
          return new Color(1f, 0.7f, 0.2f);
        case StateHeat.Cold:
          return new Color(0.2f, 0.7f, 1f);
        default:
          return new Color(0.3f, 0.3f, 0.3f);
      }
    }

    private Color GetPriorityColor(UIEventPriority priority)
    {
      switch (priority) {
        case UIEventPriority.Critical:
          return Color.red;
        case UIEventPriority.High:
          return Color.yellow;
        case UIEventPriority.Normal:
          return Color.green;
        default:
          return Color.gray;
      }
    }
  }
}