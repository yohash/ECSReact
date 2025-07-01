using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace ECSReact.Tools
{
  public class StateViewerWindow : EditorWindow
  {
    [MenuItem("ECS React/State Viewer")]
    public static void ShowWindow()
    {
      var window = GetWindow<StateViewerWindow>("ECS-React State Viewer");
      window.minSize = new Vector2(400, 600);
    }

    // === CORE DATA ===
    private List<StateTypeInfo> discoveredStates = new List<StateTypeInfo>();
    private List<ActionTypeInfo> discoveredActions = new List<ActionTypeInfo>();
    private List<ActionHistoryEntry> actionHistory = new List<ActionHistoryEntry>();
    private List<UIEventHistoryEntry> uiEventHistory = new List<UIEventHistoryEntry>();
    private Dictionary<string, bool> stateExpandedStates = new Dictionary<string, bool>();
    private Dictionary<string, Dictionary<string, bool>> fieldExpandedStates = new Dictionary<string, Dictionary<string, bool>>();

    // === UI STATE ===
    private Vector2 mainScrollPosition;
    private Vector2 actionHistoryScrollPosition;
    private Vector2 uiEventScrollPosition;
    private bool autoRefresh = true;
    private bool showActionHistory = true;
    private bool showUIEventHistory = true;
    private int maxHistoryEntries = 50;
    private int selectedTab = 0;
    private string[] tabs = { "Live States", "Action History", "UI Events", "Test Actions" };

    // === TIMING ===
    private float lastRefreshTime;
    private float refreshInterval = 0.1f; // 10 FPS refresh rate
    private bool isPlaying => Application.isPlaying;

    private void OnEnable()
    {
      DiscoverStateTypes();
      DiscoverActionTypes();
      EditorApplication.playModeStateChanged += OnPlayModeChanged;

      // Subscribe to action and UI event tracking
      ActionHistoryTracker.OnActionDispatched += RecordActionHistory;
      ActionHistoryTracker.OnUIEventGenerated += RecordUIEventHistory;
      ActionHistoryTracker.StartTracking();
    }

    private void OnDisable()
    {
      EditorApplication.playModeStateChanged -= OnPlayModeChanged;

      // Unsubscribe from action and UI event tracking
      ActionHistoryTracker.OnActionDispatched -= RecordActionHistory;
      ActionHistoryTracker.OnUIEventGenerated -= RecordUIEventHistory;
      ActionHistoryTracker.StopTracking();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
      if (state == PlayModeStateChange.EnteredPlayMode) {
        // Clear history when entering play mode
        actionHistory.Clear();
        uiEventHistory.Clear();
      }
    }

    private void Update()
    {
      if (autoRefresh && isPlaying && Time.realtimeSinceStartup - lastRefreshTime > refreshInterval) {
        RefreshLiveStates();
        lastRefreshTime = Time.realtimeSinceStartup;
        Repaint();
      }
    }

    private void OnGUI()
    {
      DrawHeader();
      EditorGUILayout.Space();

      if (!isPlaying) {
        EditorGUILayout.HelpBox("State Viewer requires Play Mode to show live data.", MessageType.Info);
        return;
      }

      // Tab selection
      selectedTab = GUILayout.Toolbar(selectedTab, tabs);
      EditorGUILayout.Space();

      switch (selectedTab) {
        case 0:
          DrawLiveStatesTab();
          break;
        case 1:
          DrawActionHistoryTab();
          break;
        case 2:
          DrawUIEventHistoryTab();
          break;
        case 3:
          DrawTestActionsTab();
          break;
      }
    }

    private void DrawHeader()
    {
      EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

      // Auto-refresh toggle
      autoRefresh = GUILayout.Toggle(autoRefresh, "🔄 Auto-Refresh", EditorStyles.toolbarButton, GUILayout.Width(100));

      // Refresh interval
      GUILayout.Label("Interval:", GUILayout.Width(50));
      refreshInterval = EditorGUILayout.Slider(refreshInterval, 0.05f, 1.0f, GUILayout.Width(100));

      GUILayout.FlexibleSpace();

      // Snapshot button
      if (GUILayout.Button("📸 Snapshot", EditorStyles.toolbarButton, GUILayout.Width(80))) {
        TakeStateSnapshot();
      }

      // Subscription health button
      if (GUILayout.Button("🔗 Subscriptions", EditorStyles.toolbarButton, GUILayout.Width(100))) {
        ShowSubscriptionHealth();
      }

      // Discover button
      if (GUILayout.Button("🔍 Discover", EditorStyles.toolbarButton, GUILayout.Width(80))) {
        DiscoverStateTypes();
      }

      EditorGUILayout.EndHorizontal();
    }

    private void DrawLiveStatesTab()
    {
      if (discoveredStates.Count == 0) {
        EditorGUILayout.HelpBox("No IGameState types discovered. Make sure you have state types in your project.", MessageType.Info);
        return;
      }

      mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

      EditorGUILayout.LabelField("LIVE STATES", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      foreach (var stateInfo in discoveredStates.OrderBy(s => s.typeName)) {
        DrawStateEntry(stateInfo);
        EditorGUILayout.Space(2);
      }

      EditorGUILayout.EndScrollView();
    }

    private void DrawStateEntry(StateTypeInfo stateInfo)
    {
      // State header with expand/collapse and jump to code
      EditorGUILayout.BeginHorizontal();

      string expandKey = stateInfo.typeName;
      bool isExpanded = stateExpandedStates.GetValueOrDefault(expandKey, false);

      // Expand/collapse arrow and state name
      string arrow = isExpanded ? "▼" : "▶";
      string lastChangedText = stateInfo.lastChanged > 0 ?
          $"(Changed {Time.realtimeSinceStartup - stateInfo.lastChanged:F1}s ago)" :
          "(Never changed)";

      if (GUILayout.Button($"{arrow} {stateInfo.typeName} {lastChangedText}", EditorStyles.foldout)) {
        stateExpandedStates[expandKey] = !isExpanded;
      }

      GUILayout.FlexibleSpace();

      // Jump to code button
      if (GUILayout.Button("→ Code", EditorStyles.miniButton, GUILayout.Width(60))) {
        JumpToStateCode(stateInfo);
      }

      EditorGUILayout.EndHorizontal();

      if (isExpanded) {
        EditorGUI.indentLevel++;

        if (stateInfo.currentValue != null) {
          DrawStateFields(stateInfo);
        } else {
          EditorGUILayout.LabelField("State not available (no singleton found)", EditorStyles.miniLabel);
        }

        EditorGUI.indentLevel--;
      }
    }

    private void DrawStateFields(StateTypeInfo stateInfo)
    {
      var stateValue = stateInfo.currentValue;
      var stateType = stateInfo.stateType;

      var fields = stateType.GetFields(BindingFlags.Public | BindingFlags.Instance);

      foreach (var field in fields) {
        EditorGUILayout.BeginHorizontal();

        try {
          var fieldValue = field.GetValue(stateValue);
          string valueText = FormatFieldValue(fieldValue, field.FieldType);

          EditorGUILayout.LabelField(field.Name, GUILayout.Width(120));
          EditorGUILayout.LabelField($"{GetFriendlyTypeName(field.FieldType)}", EditorStyles.miniLabel, GUILayout.Width(80));
          EditorGUILayout.LabelField($"= {valueText}", GUILayout.MinWidth(100));

          // Edit button for simple types
          if (CanEditFieldType(field.FieldType)) {
            if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(40))) {
              ShowFieldEditor(stateInfo, field, fieldValue);
            }
          }
        } catch (Exception ex) {
          EditorGUILayout.LabelField(field.Name, GUILayout.Width(120));
          EditorGUILayout.LabelField("Error", EditorStyles.miniLabel, GUILayout.Width(80));
          EditorGUILayout.LabelField($"= {ex.Message}", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();
      }
    }

    private void DrawActionHistoryTab()
    {
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField("ACTION HISTORY", EditorStyles.boldLabel);
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50))) {
        actionHistory.Clear();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      if (actionHistory.Count == 0) {
        EditorGUILayout.HelpBox("No actions recorded yet. Actions will appear here when dispatched.", MessageType.Info);
        return;
      }

      actionHistoryScrollPosition = EditorGUILayout.BeginScrollView(actionHistoryScrollPosition);

      foreach (var entry in actionHistory.OrderByDescending(e => e.timestamp)) {
        DrawActionHistoryEntry(entry);
      }

      EditorGUILayout.EndScrollView();
    }

    private void DrawActionHistoryEntry(ActionHistoryEntry entry)
    {
      EditorGUILayout.BeginHorizontal();

      // Timestamp
      string timeText = $"{entry.timestamp:mm:ss.fff}";
      EditorGUILayout.LabelField(timeText, GUILayout.Width(80));

      // Action name and parameters
      EditorGUILayout.LabelField($"{entry.actionType}({entry.parameters})", GUILayout.MinWidth(200));

      GUILayout.FlexibleSpace();

      // Test button to replay this action
      if (GUILayout.Button("Test", EditorStyles.miniButton, GUILayout.Width(40))) {
        // TODO: Implement action replay
        Debug.Log($"Would replay: {entry.actionType}({entry.parameters})");
      }

      EditorGUILayout.EndHorizontal();
    }

    private void DrawUIEventHistoryTab()
    {
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField("UI EVENT HISTORY", EditorStyles.boldLabel);
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50))) {
        uiEventHistory.Clear();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      if (uiEventHistory.Count == 0) {
        EditorGUILayout.HelpBox("No UI events recorded yet. Events will appear here when state changes occur.", MessageType.Info);
        return;
      }

      uiEventScrollPosition = EditorGUILayout.BeginScrollView(uiEventScrollPosition);

      foreach (var entry in uiEventHistory.OrderByDescending(e => e.timestamp)) {
        DrawUIEventHistoryEntry(entry);
      }

      EditorGUILayout.EndScrollView();
    }

    private void DrawUIEventHistoryEntry(UIEventHistoryEntry entry)
    {
      EditorGUILayout.BeginHorizontal();

      // Timestamp
      string timeText = $"{entry.timestamp:mm:ss.fff}";
      EditorGUILayout.LabelField(timeText, GUILayout.Width(80));

      // Priority icon
      string priorityIcon = entry.priority switch
      {
        "Critical" => "🔴",
        "High" => "⚡",
        "Normal" => "📋",
        _ => "📋"
      };

      EditorGUILayout.LabelField(priorityIcon, GUILayout.Width(20));

      // Event name and priority
      EditorGUILayout.LabelField($"{entry.eventType} ({entry.priority})", GUILayout.MinWidth(200));

      EditorGUILayout.EndHorizontal();
    }

    private void DrawTestActionsTab()
    {
      EditorGUILayout.LabelField("TEST ACTIONS", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      if (discoveredActions.Count == 0) {
        EditorGUILayout.HelpBox("No IGameAction types discovered. Make sure you have action types in your project.", MessageType.Info);
        return;
      }

      EditorGUILayout.HelpBox("Select an action type to dispatch a test instance:", MessageType.Info);
      EditorGUILayout.Space();

      foreach (var actionInfo in discoveredActions.OrderBy(a => a.typeName)) {
        DrawTestActionEntry(actionInfo);
        EditorGUILayout.Space(2);
      }
    }

    private void DrawTestActionEntry(ActionTypeInfo actionInfo)
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(actionInfo.typeName, EditorStyles.boldLabel);

      GUILayout.FlexibleSpace();

      if (GUILayout.Button("Dispatch Test", EditorStyles.miniButton, GUILayout.Width(100))) {
        DispatchTestAction(actionInfo);
      }

      EditorGUILayout.EndHorizontal();

      // Show fields for editing
      if (actionInfo.testValues == null) {
        InitializeTestValues(actionInfo);
      }

      EditorGUI.indentLevel++;
      foreach (var field in actionInfo.fields) {
        DrawTestActionField(actionInfo, field);
      }
      EditorGUI.indentLevel--;

      EditorGUILayout.EndVertical();
    }

    private void DrawTestActionField(ActionTypeInfo actionInfo, ActionFieldInfo field)
    {
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(field.fieldName, GUILayout.Width(120));

      var currentValue = actionInfo.testValues.GetValueOrDefault(field.fieldName, GetDefaultValue(field.fieldType));
      var newValue = DrawValueEditor(currentValue, field.fieldType);
      actionInfo.testValues[field.fieldName] = newValue;

      EditorGUILayout.EndHorizontal();
    }

    private object DrawValueEditor(object value, Type type)
    {
      if (type == typeof(int)) {
        return EditorGUILayout.IntField((int)value);
      } else if (type == typeof(float)) {
        return EditorGUILayout.FloatField((float)value);
      } else if (type == typeof(bool)) {
        return EditorGUILayout.Toggle((bool)value);
      } else if (type.Name.Contains("FixedString")) {
        string stringValue = value?.ToString() ?? "";
        return EditorGUILayout.TextField(stringValue);
      } else if (type.Name == "Entity") {
        // For entities, we'll show a simplified editor
        EditorGUILayout.LabelField(value?.ToString() ?? "Entity.Null", EditorStyles.miniLabel);
        return value; // Can't easily edit entities in inspector
      } else if (type.Name == "float3") {
        // Handle Unity.Mathematics.float3 if available
        EditorGUILayout.LabelField(value?.ToString() ?? "(0, 0, 0)", EditorStyles.miniLabel);
        return value; // Simplified for now
      } else {
        EditorGUILayout.LabelField($"[{type.Name}] {value}", EditorStyles.miniLabel);
        return value;
      }
    }

    private object GetDefaultValue(Type type)
    {
      if (type == typeof(int))
        return 0;
      if (type == typeof(float))
        return 0f;
      if (type == typeof(bool))
        return false;
      if (type.Name.Contains("FixedString"))
        return "";
      return Activator.CreateInstance(type);
    }

    private void InitializeTestValues(ActionTypeInfo actionInfo)
    {
      actionInfo.testValues = new Dictionary<string, object>();
      foreach (var field in actionInfo.fields) {
        actionInfo.testValues[field.fieldName] = GetDefaultValue(field.fieldType);
      }
    }

    private void DispatchTestAction(ActionTypeInfo actionInfo)
    {
      if (!isPlaying) {
        EditorUtility.DisplayDialog("Not Playing", "Actions can only be dispatched during Play Mode.", "OK");
        return;
      }

      try {
        // Create action instance using reflection
        var actionInstance = Activator.CreateInstance(actionInfo.actionType);

        // Set field values
        foreach (var field in actionInfo.fields) {
          var fieldInfo = actionInfo.actionType.GetField(field.fieldName);
          var value = actionInfo.testValues[field.fieldName];

          // Convert value if necessary
          if (fieldInfo != null) {
            var convertedValue = ConvertValueForField(value, fieldInfo.FieldType);
            fieldInfo.SetValue(actionInstance, convertedValue);
          }
        }

        // Dispatch through Store
        if (ECSReact.Core.Store.Instance != null) {
          var dispatchMethod = typeof(ECSReact.Core.Store).GetMethod("Dispatch")
              ?.MakeGenericMethod(actionInfo.actionType);

          if (dispatchMethod != null) {
            dispatchMethod.Invoke(ECSReact.Core.Store.Instance, new object[] { actionInstance });
            Debug.Log($"Test action dispatched: {actionInfo.typeName}");
          }
        } else {
          Debug.LogError("Store.Instance not found! Make sure Store is in the scene.");
        }
      } catch (Exception ex) {
        Debug.LogError($"Failed to dispatch test action {actionInfo.typeName}: {ex.Message}");
      }
    }

    private object ConvertValueForField(object value, Type targetType)
    {
      if (targetType.Name.Contains("FixedString") && value is string stringValue) {
        // Convert string to FixedString type
        return Activator.CreateInstance(targetType, stringValue);
      }

      return Convert.ChangeType(value, targetType);
    }

    // === CORE FUNCTIONALITY ===

    private void DiscoverStateTypes()
    {
      discoveredStates.Clear();

      var assemblies = AppDomain.CurrentDomain.GetAssemblies();

      foreach (var assembly in assemblies) {
        try {
          var types = assembly.GetTypes()
              .Where(t => t.IsValueType && !t.IsEnum && !t.IsGenericType)
              .Where(t => typeof(IComponentData).IsAssignableFrom(t))
              .Where(t => t.GetInterfaces().Any(i => i.Name == "IGameState"))
              .ToList();

          foreach (var type in types) {
            discoveredStates.Add(new StateTypeInfo
            {
              typeName = type.Name,
              stateType = type,
              assemblyName = assembly.GetName().Name
            });
          }
        } catch (Exception ex) {
          Debug.LogWarning($"Error discovering state types in assembly {assembly.GetName().Name}: {ex.Message}");
        }
      }

      Debug.Log($"State Viewer: Discovered {discoveredStates.Count} state types");
    }

    private void DiscoverActionTypes()
    {
      discoveredActions.Clear();

      var assemblies = AppDomain.CurrentDomain.GetAssemblies();

      foreach (var assembly in assemblies) {
        try {
          var types = assembly.GetTypes()
              .Where(t => t.IsValueType && !t.IsEnum && !t.IsGenericType)
              .Where(t => typeof(IComponentData).IsAssignableFrom(t))
              .Where(t => t.GetInterfaces().Any(i => i.Name == "IGameAction"))
              .ToList();

          foreach (var type in types) {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsStatic)
                .Select(f => new ActionFieldInfo
                {
                  fieldName = f.Name,
                  fieldType = f.FieldType
                })
                .ToList();

            discoveredActions.Add(new ActionTypeInfo
            {
              typeName = type.Name,
              actionType = type,
              assemblyName = assembly.GetName().Name,
              fields = fields
            });
          }
        } catch (Exception ex) {
          Debug.LogWarning($"Error discovering action types in assembly {assembly.GetName().Name}: {ex.Message}");
        }
      }

      Debug.Log($"State Viewer: Discovered {discoveredActions.Count} action types");
    }

    private void RecordActionHistory(string actionType, string parameters)
    {
      var entry = new ActionHistoryEntry
      {
        timestamp = DateTime.Now,
        actionType = actionType,
        parameters = parameters
      };

      actionHistory.Insert(0, entry); // Insert at beginning for newest first

      // Limit history size
      if (actionHistory.Count > maxHistoryEntries) {
        actionHistory.RemoveAt(actionHistory.Count - 1);
      }
    }

    private void RecordUIEventHistory(string eventType, string priority)
    {
      var entry = new UIEventHistoryEntry
      {
        timestamp = DateTime.Now,
        eventType = eventType,
        priority = priority
      };

      uiEventHistory.Insert(0, entry); // Insert at beginning for newest first

      // Limit history size
      if (uiEventHistory.Count > maxHistoryEntries) {
        uiEventHistory.RemoveAt(uiEventHistory.Count - 1);
      }
    }

    private void RefreshLiveStates()
    {
      if (!isPlaying)
        return;

      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null)
        return;

      foreach (var stateInfo in discoveredStates) {
        try {
          // Use reflection to call SystemAPI.GetSingleton<T>()
          var getSingletonMethod = typeof(SystemAPI).GetMethod("GetSingleton")
              ?.MakeGenericMethod(stateInfo.stateType);

          if (getSingletonMethod != null) {
            var newValue = getSingletonMethod.Invoke(null, null);

            // Check if state changed
            if (!Equals(newValue, stateInfo.currentValue)) {
              stateInfo.previousValue = stateInfo.currentValue;
              stateInfo.currentValue = newValue;
              stateInfo.lastChanged = Time.realtimeSinceStartup;
            }
          }
        } catch (Exception) {
          // State not available (no singleton)
          stateInfo.currentValue = null;
        }
      }
    }

    // === UTILITY METHODS ===

    private string FormatFieldValue(object value, Type fieldType)
    {
      if (value == null)
        return "null";

      if (fieldType == typeof(float)) {
        return ((float)value).ToString("F2");
      } else if (fieldType.Name.Contains("float3")) {
        // Handle Unity.Mathematics.float3
        return value.ToString();
      } else if (fieldType.Name.Contains("Entity")) {
        return value.ToString();
      } else if (fieldType.Name.Contains("FixedString")) {
        return $"\"{value}\"";
      }

      return value.ToString();
    }

    private string GetFriendlyTypeName(Type type)
    {
      if (type == typeof(int))
        return "int";
      if (type == typeof(float))
        return "float";
      if (type == typeof(bool))
        return "bool";
      if (type == typeof(string))
        return "string";
      return type.Name;
    }

    private bool CanEditFieldType(Type type)
    {
      return type == typeof(int) || type == typeof(float) || type == typeof(bool);
    }

    private void ShowFieldEditor(StateTypeInfo stateInfo, FieldInfo field, object currentValue)
    {
      // TODO: Implement field editing popup
      Debug.Log($"Would edit {stateInfo.typeName}.{field.Name} = {currentValue}");
    }

    private void JumpToStateCode(StateTypeInfo stateInfo)
    {
      // TODO: Implement code navigation
      Debug.Log($"Would jump to code for {stateInfo.typeName}");
    }

    private void TakeStateSnapshot()
    {
      // TODO: Implement state snapshot
      Debug.Log("Would take state snapshot");
    }

    private void ShowSubscriptionHealth()
    {
      SubscriptionHealthWindow.ShowWindow();
    }
  }

  // === DATA CLASSES ===

  [Serializable]
  public class StateTypeInfo
  {
    public string typeName;
    public Type stateType;
    public string assemblyName;
    public object currentValue;
    public object previousValue;
    public float lastChanged;
  }

  [Serializable]
  public class ActionTypeInfo
  {
    public string typeName;
    public Type actionType;
    public string assemblyName;
    public List<ActionFieldInfo> fields = new List<ActionFieldInfo>();
    public Dictionary<string, object> testValues = new Dictionary<string, object>();
  }

  [Serializable]
  public class ActionFieldInfo
  {
    public string fieldName;
    public Type fieldType;
  }

  [Serializable]
  public class ActionHistoryEntry
  {
    public DateTime timestamp;
    public string actionType;
    public string parameters;
  }

  [Serializable]
  public class UIEventHistoryEntry
  {
    public DateTime timestamp;
    public string eventType;
    public string priority;
  }

  // === SUBSCRIPTION HEALTH WINDOW ===

  public class SubscriptionHealthWindow : EditorWindow
  {
    public static void ShowWindow()
    {
      var window = GetWindow<SubscriptionHealthWindow>("Subscription Health");
      window.minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
      GUILayout.Label("Subscription Health", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      EditorGUILayout.HelpBox("Subscription health monitoring coming soon!\n\nThis will show:\n• Which UI components subscribe to which states\n• Orphaned states with no subscribers\n• Subscription performance metrics", MessageType.Info);
    }
  }
}