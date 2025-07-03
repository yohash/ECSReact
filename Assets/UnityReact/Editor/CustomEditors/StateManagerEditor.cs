using ECSReact.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ECSReact.Tools
{
  /// <summary>
  /// Custom editor for SceneStateManager with namespace-organized checkboxes.
  /// </summary>
  [CustomEditor(typeof(SceneStateManager))]
  public class SceneStateManagerEditor : Editor
  {
    private Dictionary<string, bool> namespaceFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> namespaceCheckAll = new Dictionary<string, bool>();

    private Vector2 scrollPosition;

    private SerializedProperty stateConfigurationsProperty;
    private SerializedProperty autoDiscoverProperty;
    private SerializedProperty createSingletonsProperty;

    private SerializedProperty namespaceFoldoutsProperty;
    private SerializedProperty namespaceCheckAllProperty;

    private void OnEnable()
    {
      stateConfigurationsProperty = serializedObject.FindProperty("stateConfigurations");
      autoDiscoverProperty = serializedObject.FindProperty("autoDiscoverOnAwake");
      createSingletonsProperty = serializedObject.FindProperty("createSingletonsOnStart");

      namespaceFoldoutsProperty = serializedObject.FindProperty("namespaceFoldouts");
      namespaceCheckAllProperty = serializedObject.FindProperty("namespaceCheckAll");
    }

    public override void OnInspectorGUI()
    {
      var manager = (SceneStateManager)target;
      serializedObject.Update();

      EditorGUILayout.LabelField("Scene State Manager", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      // Options
      EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
      EditorGUILayout.PropertyField(autoDiscoverProperty, new GUIContent("Auto Discover On Awake"));
      EditorGUILayout.PropertyField(createSingletonsProperty, new GUIContent("Create Singletons On Start"));

      EditorGUILayout.Space();

      // Discovery controls
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("🔍 Discover States")) {
        manager.DiscoverStates();
        serializedObject.Update(); // Refresh serialized properties
      }
      if (GUILayout.Button("✅ Create Enabled")) {
        manager.CreateEnabledSingletons();
      }
      if (GUILayout.Button("❌ Remove Disabled")) {
        manager.RemoveDisabledSingletons();
      }
      if (GUILayout.Button("🔍 Verify Singletons")) {
        manager.VerifySingletonStates();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // State configurations by namespace
      var currentNamespaceGroups = manager.GetStatesByNamespace().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
      if (currentNamespaceGroups.Count == 0) {
        EditorGUILayout.HelpBox("No states discovered. Click 'Discover States' to scan for IGameState types.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
        return;
      }

      EditorGUILayout.LabelField($"Discovered States ({currentNamespaceGroups.Values.Sum(list => list.Count)} total)", EditorStyles.boldLabel);

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

      foreach (var group in currentNamespaceGroups.OrderBy(ng => ng.Key)) {

        string namespaceName = group.Key;
        var states = group.Value;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        // Namespace-level checkbox
        bool checkAll = namespaceCheckAll.GetValueOrDefault(namespaceName, false);
        bool newCheckAll = EditorGUILayout.Toggle(checkAll, GUILayout.Width(30));

        if (newCheckAll != checkAll) {
          setNamespaceStates(namespaceName, newCheckAll);
          namespaceCheckAll[namespaceName] = newCheckAll;
        }

        // Namespace foldout
        bool foldout = namespaceFoldouts.GetValueOrDefault(namespaceName, true);
        foldout = EditorGUILayout.Foldout(foldout, $"  {namespaceName} ({states.Count} states)", true);
        namespaceFoldouts[namespaceName] = foldout;

        EditorGUILayout.EndHorizontal();

        if (foldout) {
          EditorGUI.indentLevel++;
          EditorGUILayout.Space(3);

          // Individual state checkboxes
          foreach (var state in states.OrderBy(s => s.typeName)) {
            drawStateConfigurationRow(manager, state, currentNamespaceGroups);
          }

          EditorGUI.indentLevel--;
          EditorGUILayout.Space();
        }

        EditorGUILayout.EndVertical();
      }

      EditorGUILayout.EndScrollView();

      // Summary
      int enabledCount = getEnabledStateCount();
      int totalCount = stateConfigurationsProperty.arraySize;

      EditorGUILayout.LabelField($"Summary: {enabledCount}/{totalCount} states enabled", EditorStyles.miniLabel);

      bool duplicatesFound = currentNamespaceGroups.Values
        .SelectMany(g => g)
        .GroupBy(info => info.typeName)
        .Any(g => g.Count() > 1 && g.All(info => getStateConfigurationEnabledState(info.typeName, info.namespaceName)));

      if (duplicatesFound) {
        EditorGUILayout.HelpBox("❗ Duplicate state types found across different namespaces. Ensure unique type names or disable duplicates.", MessageType.Error);
      }

      serializedObject.ApplyModifiedProperties();
    }

    private void drawStateConfigurationRow(
      SceneStateManager manager,
      SceneStateManager.StateTypeInfo state,
      Dictionary<string, List<SceneStateManager.StateTypeInfo>> currentGroups
    )
    {
      var configIndex = findStateConfigurationIndex(state.typeName, state.namespaceName);
      if (configIndex == -1) {
        return;
      }

      var configProperty = stateConfigurationsProperty.GetArrayElementAtIndex(configIndex);
      var isEnabledProperty = configProperty.FindPropertyRelative("isEnabled");
      var hasDefaultValuesProperty = configProperty.FindPropertyRelative("hasDefaultValues");

      EditorGUILayout.BeginHorizontal();

      // Checkbox
      EditorGUILayout.PropertyField(isEnabledProperty, GUIContent.none, GUILayout.Width(30));

      // State name
      EditorGUILayout.LabelField(state.typeName, GUILayout.Width(150));

      // IEquatable indicator
      if (!state.hasEquatable) {
        EditorGUILayout.LabelField("⚠️", EditorStyles.boldLabel, GUILayout.Width(40));
      } else if (currentGroups.Values
          .SelectMany(g => g)
          .Any(info =>
            state.typeName == info.typeName
            && state.namespaceName != info.namespaceName
            && getStateConfigurationEnabledState(state.typeName, state.namespaceName)
            && getStateConfigurationEnabledState(info.typeName, info.namespaceName))
      ) {
        EditorGUILayout.LabelField("❗", EditorStyles.boldLabel, GUILayout.Width(40));
      } else {
        EditorGUILayout.LabelField("✓", EditorStyles.boldLabel, GUILayout.Width(40));
      }

      // Assembly info
      EditorGUILayout.LabelField(state.assemblyName, EditorStyles.miniLabel, GUILayout.MinWidth(120));

      // Default values button
      if (GUILayout.Button("Defaults", EditorStyles.miniButton)) {
        showDefaultValuesEditor(manager, configIndex, state);
      }

      EditorGUILayout.EndHorizontal();
    }

    private void setNamespaceStates(string namespaceName, bool enabled)
    {
      for (int i = 0; i < stateConfigurationsProperty.arraySize; i++) {
        var configProperty = stateConfigurationsProperty.GetArrayElementAtIndex(i);
        var namespaceProperty = configProperty.FindPropertyRelative("namespaceName");

        if (namespaceProperty.stringValue == namespaceName) {
          var isEnabledProperty = configProperty.FindPropertyRelative("isEnabled");
          isEnabledProperty.boolValue = enabled;
        }
      }
    }

    private bool getStateConfigurationEnabledState(string typeName, string namespaceName)
    {
      int index = findStateConfigurationIndex(typeName, namespaceName);
      if (index == -1) {
        return false;
      }
      var configProperty = stateConfigurationsProperty.GetArrayElementAtIndex(index);
      var isEnabledProperty = configProperty.FindPropertyRelative("isEnabled");
      return isEnabledProperty.boolValue;
    }

    private int findStateConfigurationIndex(string typeName, string namespaceName)
    {
      for (int i = 0; i < stateConfigurationsProperty.arraySize; i++) {
        var configProperty = stateConfigurationsProperty.GetArrayElementAtIndex(i);
        var typeNameProperty = configProperty.FindPropertyRelative("typeName");
        var namespaceProperty = configProperty.FindPropertyRelative("namespaceName");

        if (typeNameProperty.stringValue == typeName && namespaceProperty.stringValue == namespaceName) {
          return i;
        }
      }
      return -1;
    }

    private int getEnabledStateCount()
    {
      int count = 0;
      for (int i = 0; i < stateConfigurationsProperty.arraySize; i++) {
        var configProperty = stateConfigurationsProperty.GetArrayElementAtIndex(i);
        var isEnabledProperty = configProperty.FindPropertyRelative("isEnabled");

        if (isEnabledProperty.boolValue) {
          count++;
        }
      }
      return count;
    }

    private void showDefaultValuesEditor(SceneStateManager manager, int configIndex, ECSReact.Core.SceneStateManager.StateTypeInfo state)
    {
      var window = EditorWindow.GetWindow<StateDefaultsEditorWindow>("State Defaults");
      window.Initialize(manager, configIndex, state);
    }
  }
}
