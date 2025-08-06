using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor
{
  /// <summary>
  /// Enhanced ECS React Debug Dashboard for runtime state monitoring and testing.
  /// Now supports complex types including FixedString, FixedList, structs, and Entity references.
  /// </summary>
  public class ECSReactDebugDashboard : EditorWindow
  {
    // State tracking
    private class StateDebugInfo
    {
      public Type stateType;
      public Entity entity;
      public object currentValue;
      public object previousValue;
      public float lastChangeTime;
      public int changeCount;
      public float changeFrequency;
      public bool isExpanded;
      public Dictionary<string, bool> fieldFoldouts = new Dictionary<string, bool>();

      public void RecordChange(float time)
      {
        changeCount++;
        lastChangeTime = time;

        var timeSinceStart = Time.realtimeSinceStartup;
        if (timeSinceStart > 0)
          changeFrequency = changeCount / timeSinceStart;
      }

      public StateHeat GetHeat()
      {
        var timeSinceChange = Time.realtimeSinceStartup - lastChangeTime;
        if (timeSinceChange < 0.5f)
          return StateHeat.Hot;
        if (timeSinceChange < 2f)
          return StateHeat.Warm;
        return StateHeat.Cold;
      }
    }

    private enum StateHeat { Cold, Warm, Hot }

    private class ActionDebugInfo
    {
      public Type actionType;
      public object actionData;
      public float dispatchTime;
      public int frameNumber;
      public bool isExpanded;
      public Dictionary<string, bool> fieldFoldouts = new Dictionary<string, bool>();
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
      public Dictionary<string, bool> fieldFoldouts = new Dictionary<string, bool>();
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
    private bool showDataTypes = true;

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
      var window = GetWindow<ECSReactDebugDashboard>("ECS React Dashboard");
      window.minSize = new Vector2(600, 400);
      window.Show();
    }

    private void OnEnable()
    {
      instance = this;
      titleContent = new GUIContent("ECS React Dashboard");

      if (!isSubscribed && EditorApplication.isPlaying) {
        SubscribeToEvents();
      }
    }

    private void OnDisable()
    {
      if (isSubscribed) {
        UnsubscribeFromEvents();
      }
    }

    private void SubscribeToEvents()
    {
      DebugActionInterceptorSystem.OnActionDetected += OnActionDetected;

      // Subscribe to UI events if available
      try {
        var uiEventQueueType = typeof(UIEventQueue);
        var eventField = uiEventQueueType.GetField("OnEventProcessed", BindingFlags.Public | BindingFlags.Static);
        if (eventField != null) {
          var eventDelegate = eventField.GetValue(null) as Action<UIEvent>;
          if (eventDelegate != null) {
            eventDelegate += OnUIEventProcessed;
          }
        }
      } catch { }

      isSubscribed = true;
    }

    private void UnsubscribeFromEvents()
    {
      DebugActionInterceptorSystem.OnActionDetected -= OnActionDetected;

      isSubscribed = false;
    }

    private void OnActionDetected(DebugActionInterceptorSystem.ActionDebugData data)
    {
      var info = new ActionDebugInfo
      {
        actionType = data.actionType,
        actionData = data.actionData,
        dispatchTime = data.timestamp,
        frameNumber = data.frame
      };

      actionHistory.Insert(0, info);
      if (actionHistory.Count > MAX_ACTION_HISTORY) {
        actionHistory.RemoveAt(actionHistory.Count - 1);
      }

      Repaint();
    }

    private void Update()
    {
      if (!EditorApplication.isPlaying) {
        return;
      }

      if (!isSubscribed) {
        SubscribeToEvents();
      }

      if (autoRefresh && Time.realtimeSinceStartup - lastRefreshTime > refreshInterval) {
        RefreshStateCache();
        lastRefreshTime = Time.realtimeSinceStartup;
        Repaint();
      }
    }

    private void RefreshStateCache()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null || !world.IsCreated) {
        return;
      }

      var entityManager = world.EntityManager;

      // Try to use StateRegistryService first
      if (StateRegistryService.HasRegistry) {
        foreach (var registry in StateRegistryService.AllRegistries) {
          RefreshUsingRegistry(entityManager, registry);
        }
      }
      // Fallback to SceneStateManager
      else if (SceneStateManager.Instance != null) {
        var allStates = SceneStateManager.Instance.GetAllStateEntities();
        var allStateInfos = StateRegistryService.GetAllStatesFromAllRegistries();
        if (allStateInfos != null && allStateInfos.Count > 0) {
          RefreshUsingStateInfos(entityManager, allStateInfos);
        } else {
          Debug.LogWarning("No State Registry found! Please generate one using 'ECS React → Generate State Registry'");
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
      showDataTypes = GUILayout.Toggle(showDataTypes, "Types", EditorStyles.toolbarButton, GUILayout.Width(50));
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
        DrawComplexObjectFields(info.currentValue, info.fieldFoldouts, "");

        if (info.previousValue != null && !AreValuesEqual(info.currentValue, info.previousValue)) {
          EditorGUILayout.LabelField("Previous Value:", EditorStyles.boldLabel);
          DrawComplexObjectFields(info.previousValue, new Dictionary<string, bool>(), "");
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

    private void DrawComplexObjectFields(object obj, Dictionary<string, bool> foldouts, string path)
    {
      if (obj == null) {
        return;
      }

      var type = obj.GetType();
      var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

      // Table header if showing types
      if (showDataTypes && string.IsNullOrEmpty(path)) {
        float windowWidth = position.width;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Field", EditorStyles.boldLabel, GUILayout.Width(windowWidth / 3.5f));
        EditorGUILayout.LabelField("Type", EditorStyles.miniLabel, GUILayout.Width(windowWidth / 3.5f));
        EditorGUILayout.LabelField("Value", EditorStyles.boldLabel, GUILayout.Width(windowWidth / 3.5f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
      }

      foreach (var field in fields) {
        DrawFieldWithType(field, obj, foldouts, path);
      }
    }

    private void DrawFieldWithType(FieldInfo field, object container, Dictionary<string, bool> foldouts, string pathPrefix)
    {
      var fieldPath = string.IsNullOrEmpty(pathPrefix) ? field.Name : $"{pathPrefix}.{field.Name}";
      var fieldType = field.FieldType;
      var fieldValue = field.GetValue(container);

      EditorGUILayout.BeginHorizontal();

      float windowWidth = position.width;

      // Field name
      var displayName = ObjectNames.NicifyVariableName(field.Name);

      if (IsExpandableType(fieldType)) {
        if (!foldouts.ContainsKey(fieldPath)) {
          foldouts[fieldPath] = false;
        }

        EditorGUILayout.BeginHorizontal();
        foldouts[fieldPath] = EditorGUILayout.Foldout(foldouts[fieldPath], $"  {displayName}", true);
        EditorGUILayout.EndHorizontal();
      } else {
        EditorGUILayout.LabelField(displayName);
      }

      // Type name (if enabled)
      if (showDataTypes) {
        var typeName = GetFriendlyTypeName(fieldType);
        EditorGUILayout.LabelField(typeName, EditorStyles.miniLabel, GUILayout.Width(windowWidth / 3.5f));
      }

      // Value
      if (!IsExpandableType(fieldType)) {
        DrawInlineValue(fieldType, fieldValue);
      } else {
        var preview = GetComplexTypePreview(fieldType, fieldValue);
        EditorGUILayout.LabelField(preview, EditorStyles.miniLabel, GUILayout.Width(windowWidth / 3.5f));
      }

      EditorGUILayout.EndHorizontal();

      // Draw expanded content
      if (IsExpandableType(fieldType) && foldouts.ContainsKey(fieldPath) && foldouts[fieldPath]) {
        EditorGUI.indentLevel++;
        DrawExpandedComplexType(fieldType, fieldValue, foldouts, fieldPath);
        EditorGUI.indentLevel--;
      }
    }

    private bool IsExpandableType(Type type)
    {
      return IsFixedList(type) ||
             IsCustomStruct(type) ||
             (type.IsValueType && !type.IsPrimitive && !type.IsEnum &&
              type != typeof(Entity) && !type.Name.StartsWith("FixedString"));
    }

    private bool IsFixedList(Type type)
    {
      return type.IsValueType && type.Name.StartsWith("FixedList");
    }

    private bool IsCustomStruct(Type type)
    {
      return type.IsValueType &&
             !type.IsPrimitive &&
             !type.IsEnum &&
             type.Namespace != "Unity.Mathematics" &&
             type != typeof(Entity) &&
             !type.Name.StartsWith("FixedString") &&
             !type.Name.StartsWith("FixedList");
    }

    private void DrawInlineValue(Type fieldType, object value)
    {
      var valueStr = "";

      // Handle specific types
      if (value == null) {
        valueStr = "null";
      } else if (fieldType == typeof(Entity)) {
        var entity = (Entity)value;
        valueStr = entity == Entity.Null ? "Entity.Null" : $"Entity({entity.Index}:{entity.Version})";
      } else if (fieldType.Name.StartsWith("FixedString")) {
        valueStr = $"\"{value}\"";
      } else if (fieldType.IsEnum) {
        valueStr = value.ToString();
      } else if (fieldType == typeof(bool)) {
        valueStr = value.ToString().ToLower();
      } else if (fieldType.Namespace == "Unity.Mathematics") {
        valueStr = FormatMathematicsType(fieldType, value);
      } else {
        valueStr = value.ToString();
      }

      float windowWidth = position.width;
      EditorGUILayout.SelectableLabel(
        valueStr,
        GUILayout.Height(EditorGUIUtility.singleLineHeight),
        GUILayout.Width(windowWidth / 3.5f)
      );
    }

    private string FormatMathematicsType(Type type, object value)
    {
      switch (type.Name) {
        case "float2":
          var f2 = (float2)value;
          return $"({f2.x:F2}, {f2.y:F2})";
        case "float3":
          var f3 = (float3)value;
          return $"({f3.x:F2}, {f3.y:F2}, {f3.z:F2})";
        case "float4":
          var f4 = (float4)value;
          return $"({f4.x:F2}, {f4.y:F2}, {f4.z:F2}, {f4.w:F2})";
        case "int2":
          var i2 = (int2)value;
          return $"({i2.x}, {i2.y})";
        case "int3":
          var i3 = (int3)value;
          return $"({i3.x}, {i3.y}, {i3.z})";
        case "quaternion":
          var q = (quaternion)value;
          var euler = math.degrees(math.Euler(q));
          return $"Euler({euler.x:F1}, {euler.y:F1}, {euler.z:F1})";
        default:
          return value.ToString();
      }
    }

    private string GetComplexTypePreview(Type type, object value)
    {
      if (value == null) {
        return "null";
      }

      if (IsFixedList(type)) {
        var lengthProp = type.GetProperty("Length");
        var capacityProp = type.GetProperty("Capacity");
        if (lengthProp != null && capacityProp != null) {
          var length = (int)lengthProp.GetValue(value);
          var capacity = (int)capacityProp.GetValue(value);
          return $"[{length}/{capacity} items]";
        }
      }

      return "[Complex]";
    }

    private void DrawExpandedComplexType(Type type, object value, Dictionary<string, bool> foldouts, string path)
    {
      if (value == null) {
        return;
      }

      if (IsFixedList(type)) {
        DrawFixedListContents(type, value, foldouts, path);
      } else if (IsCustomStruct(type)) {
        DrawComplexObjectFields(value, foldouts, path);
      }
    }

    private void DrawFixedListContents(Type listType, object listValue, Dictionary<string, bool> foldouts, string path)
    {
      var elementType = GetFixedListElementType(listType);
      if (elementType == null) {
        return;
      }

      var lengthProp = listType.GetProperty("Length");
      var indexer = listType.GetProperty("Item");

      if (lengthProp == null || indexer == null) {
        return;
      }

      float windowWidth = position.width;

      int length = (int)lengthProp.GetValue(listValue);

      for (int i = 0; i < length; i++) {
        var elementValue = indexer.GetValue(listValue, new object[] { i });
        var elementPath = $"{path}[{i}]";

        EditorGUILayout.BeginHorizontal();

        var typeName = GetFriendlyTypeName(elementType);

        if (IsExpandableType(elementType)) {
          if (!foldouts.ContainsKey(elementPath)) {
            foldouts[elementPath] = false;
          }

          EditorGUILayout.BeginHorizontal();
          foldouts[elementPath] = EditorGUILayout.Foldout(
            foldouts[elementPath],
            $"  [{i}]  {typeName}",
            true
          );
          EditorGUILayout.EndHorizontal();
        } else {
          EditorGUILayout.LabelField($"  [{i}]  {typeName}", GUILayout.Width(windowWidth / 3.5f));
        }

        if (IsExpandableType(elementType)) {
          EditorGUILayout.LabelField(GetComplexTypePreview(elementType, elementValue), GUILayout.Width(windowWidth / 3.5f));
        } else {
          DrawInlineValue(elementType, elementValue);
        }

        EditorGUILayout.EndHorizontal();

        if (IsExpandableType(elementType) && foldouts.ContainsKey(elementPath) && foldouts[elementPath]) {
          EditorGUI.indentLevel++;
          EditorGUILayout.BeginVertical(EditorStyles.helpBox);
          DrawExpandedComplexType(elementType, elementValue, foldouts, elementPath);
          EditorGUILayout.EndVertical();
          EditorGUI.indentLevel--;
        }
      }
    }

    private Type GetFixedListElementType(Type listType)
    {
      var enumerable = listType.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

      if (enumerable != null) {
        return enumerable.GetGenericArguments()[0];
      }

      var indexer = listType.GetProperty("Item");
      return indexer?.PropertyType;
    }

    private string GetFriendlyTypeName(Type type)
    {
      if (type.Name.StartsWith("FixedString")) {
        var match = System.Text.RegularExpressions.Regex.Match(type.Name, @"FixedString(\d+)Bytes");
        if (match.Success) {
          return $"FixedString[{match.Groups[1].Value}]";
        }
      }

      if (type.Name.StartsWith("FixedList")) {
        var elementType = GetFixedListElementType(type);
        if (elementType != null) {
          return $"FixedList<{elementType.Name}>";
        }
      }

      if (type.Namespace == "Unity.Mathematics") {
        return type.Name;
      }

      if (type == typeof(Entity)) {
        return "Entity";
      }

      if (type.IsEnum) {
        return $"enum {type.Name}";
      }

      return type.Name;
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
        DrawComplexObjectFields(info.actionData, info.fieldFoldouts, "");
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
        DrawComplexObjectFields(info.eventData, info.fieldFoldouts, "");
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