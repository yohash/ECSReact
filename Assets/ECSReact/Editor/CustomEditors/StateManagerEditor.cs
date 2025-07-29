using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor
{
  [CustomEditor(typeof(SceneStateManager))]
  public class StateManagerEditor : UnityEditor.Editor
  {
    private Dictionary<string, bool> namespaceFoldouts = new();
    private Dictionary<string, bool> namespaceIncludeAll = new();
    private string searchFilter = "";
    private bool showOnlyEnabled = false;
    private List<IStateRegistry> cachedRegistries;
    private Dictionary<Type, IStateInfo> allDiscoveredStates = new();

    private void OnEnable()
    {
      refreshRegistries();

      // Update configurations if we have discovered states
      if (allDiscoveredStates != null && allDiscoveredStates.Count > 0) {
        var manager = (SceneStateManager)target;
        updateStateConfigurations(manager);
      }
    }

    private void updateStateConfigurations(SceneStateManager manager)
    {
      var configurationsProperty = serializedObject.FindProperty("stateConfigurations");

      // Build a map of existing configurations to preserve user settings
      var existingConfigs = new Dictionary<string, (bool enabled, string defaults)>();
      for (int i = 0; i < configurationsProperty.arraySize; i++) {
        var config = configurationsProperty.GetArrayElementAtIndex(i);
        var typeName = config.FindPropertyRelative("typeName").stringValue;
        var enabled = config.FindPropertyRelative("enabled").boolValue;
        var defaults = config.FindPropertyRelative("serializedDefaults").stringValue;
        existingConfigs[typeName] = (enabled, defaults);
      }

      // Clear and rebuild the configurations list
      configurationsProperty.ClearArray();

      // Sort states by namespace and name
      var sortedStates = allDiscoveredStates.Values
        .OrderBy(info => info.Namespace)
        .ThenBy(info => info.Name)
        .ToList();

      // Add configurations for each discovered state
      int index = 0;
      foreach (var stateInfo in sortedStates) {
        configurationsProperty.InsertArrayElementAtIndex(index);
        var element = configurationsProperty.GetArrayElementAtIndex(index);

        element.FindPropertyRelative("typeName").stringValue = stateInfo.Type.FullName;
        element.FindPropertyRelative("namespaceName").stringValue = stateInfo.Namespace;
        element.FindPropertyRelative("displayName").stringValue = stateInfo.Name;

        // Preserve existing settings if available
        if (existingConfigs.TryGetValue(stateInfo.Type.FullName, out var existing)) {
          element.FindPropertyRelative("enabled").boolValue = existing.enabled;
          element.FindPropertyRelative("serializedDefaults").stringValue = existing.defaults;
        } else {
          element.FindPropertyRelative("enabled").boolValue = true;
          element.FindPropertyRelative("serializedDefaults").stringValue = "";
        }

        index++;
      }

      serializedObject.ApplyModifiedProperties();
    }

    private void clearRegistries()
    {
      cachedRegistries?.Clear();
      allDiscoveredStates?.Clear();
      cachedRegistries = new List<IStateRegistry>();
      allDiscoveredStates = new Dictionary<Type, IStateInfo>();
    }

    private void refreshRegistries()
    {
      cachedRegistries = discoverAllRegistries();
      allDiscoveredStates = new Dictionary<Type, IStateInfo>();

      // Collect all states from all registries
      foreach (var registry in cachedRegistries) {
        foreach (var kvp in registry.AllStates) {
          // Later registries override earlier ones for the same type
          allDiscoveredStates[kvp.Key] = kvp.Value;
        }
      }

      Debug.Log($"[StateManagerEditor] Discovered {cachedRegistries.Count} registries with {allDiscoveredStates.Count} total states");
    }

    private List<IStateRegistry> discoverAllRegistries()
    {
      var registries = new List<IStateRegistry>();
      var registryInterface = typeof(IStateRegistry);

      // Find all types that implement IStateRegistry
      var registryTypes = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(a =>
          {
            try { return a.GetTypes(); } catch { return new Type[0]; }
          })
          .Where(t => t.IsClass &&
                     !t.IsAbstract &&
                     registryInterface.IsAssignableFrom(t))
          .ToList();

      foreach (var registryType in registryTypes) {
        try {
          // Try to get singleton instance
          var instanceProperty = registryType.GetProperty("Instance",
              BindingFlags.Public | BindingFlags.Static);

          if (instanceProperty != null) {
            var instance = instanceProperty.GetValue(null) as IStateRegistry;
            if (instance != null) {
              registries.Add(instance);
              Debug.Log($"[StateManagerEditor] Found registry: {registryType.Name}");
            }
          } else {
            // Try to create instance directly
            var instance = Activator.CreateInstance(registryType) as IStateRegistry;
            if (instance != null) {
              registries.Add(instance);
              Debug.Log($"[StateManagerEditor] Created registry instance: {registryType.Name}");
            }
          }
        } catch (Exception e) {
          Debug.LogWarning($"[StateManagerEditor] Failed to instantiate registry {registryType.Name}: {e.Message}");
        }
      }

      return registries;
    }

    public override void OnInspectorGUI()
    {
      var manager = (SceneStateManager)target;

      // Header
      EditorGUILayout.BeginVertical("box");
      EditorGUILayout.LabelField("ECS State Manager", EditorStyles.boldLabel);

      // Control buttons
      EditorGUILayout.BeginHorizontal();

      if (GUILayout.Button("Refresh States")) {
        refreshRegistries();
        updateStateConfigurations(manager);
        EditorUtility.SetDirty(manager);
      }

      if (GUILayout.Button("Clear States")) {
        clearRegistries();
        updateStateConfigurations(manager);
        EditorUtility.SetDirty(manager);
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.EndVertical();

      EditorGUILayout.Space(10);

      // Check if we have any discovered states
      if (allDiscoveredStates == null || allDiscoveredStates.Count == 0) {
        EditorGUILayout.HelpBox(
            "No state registries found.\n\n" +
            "To use SceneStateManager:\n" +
            "1. Create state types implementing IGameState\n" +
            "2. Generate a state registry using 'ECS React > Generate State Registry'\n" +
            "3. Click 'Refresh States' to discover available states",
            MessageType.Warning);
        return;
      }

      // State count info
      var enabledCount = 0;
      var configurationsProperty = serializedObject.FindProperty("stateConfigurations");

      for (int i = 0; i < configurationsProperty.arraySize; i++) {
        if (configurationsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("enabled").boolValue)
          enabledCount++;
      }

      EditorGUILayout.HelpBox($"Total States: {configurationsProperty.arraySize} | Enabled: {enabledCount}", MessageType.Info);

      EditorGUILayout.Space(5);

      // Group states by namespace using the configurations
      var statesByNamespace = new Dictionary<string, List<SerializedProperty>>();

      for (int i = 0; i < configurationsProperty.arraySize; i++) {
        var config = configurationsProperty.GetArrayElementAtIndex(i);
        var namespaceName = config.FindPropertyRelative("namespaceName").stringValue;

        if (string.IsNullOrEmpty(namespaceName)) {
          namespaceName = "Global";
        }

        var displayName = config.FindPropertyRelative("displayName").stringValue;

        // Apply search filter
        if (!string.IsNullOrEmpty(searchFilter) &&
            !displayName.ToLower().Contains(searchFilter.ToLower())) {
          continue;
        }

        if (!statesByNamespace.ContainsKey(namespaceName)) {
          statesByNamespace[namespaceName] = new List<SerializedProperty>();
        }

        statesByNamespace[namespaceName].Add(config);
      }

      // Filters
      EditorGUILayout.Space(5);
      EditorGUILayout.BeginVertical();
      var oldLabel = EditorGUIUtility.labelWidth;
      EditorGUIUtility.labelWidth = 120;
      searchFilter = EditorGUILayout.TextField("Search:", searchFilter);
      showOnlyEnabled = EditorGUILayout.Toggle("Only Enabled", showOnlyEnabled);
      EditorGUIUtility.labelWidth = oldLabel;
      EditorGUILayout.Space(5);
      EditorGUILayout.EndVertical();

      // Display grouped states
      foreach (var kvp in statesByNamespace.OrderBy(k => k.Key)) {
        var ns = kvp.Key;
        var configs = kvp.Value;

        if (!namespaceFoldouts.ContainsKey(ns)) {
          namespaceFoldouts[ns] = true;
        }
        if (!namespaceIncludeAll.ContainsKey(ns)) {
          namespaceIncludeAll[ns] = true;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Namespace header with batch operations
        EditorGUILayout.BeginHorizontal();

        bool newIncludeNamespace = EditorGUILayout.Toggle(namespaceIncludeAll[ns], GUILayout.Width(35));
        if (newIncludeNamespace != namespaceIncludeAll[ns]) {
          namespaceIncludeAll[ns] = newIncludeNamespace;
          foreach (var config in configs) {
            config.FindPropertyRelative("enabled").boolValue = newIncludeNamespace;
          }
        }

        namespaceFoldouts[ns] = EditorGUILayout.Foldout(namespaceFoldouts[ns], $"  {ns} ({configs.Count} states)", true);

        EditorGUILayout.EndHorizontal();

        if (namespaceFoldouts[ns]) {
          EditorGUI.indentLevel++;

          foreach (var config in configs) {
            var enabled = config.FindPropertyRelative("enabled").boolValue;

            // Apply show only enabled filter
            if (showOnlyEnabled && !enabled) {
              continue;
            }
            drawStateConfiguration(config);
          }

          EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
      }

      serializedObject.ApplyModifiedProperties();
    }

    private void drawStateConfiguration(SerializedProperty config)
    {
      var typeName = config.FindPropertyRelative("typeName").stringValue;
      var displayName = config.FindPropertyRelative("displayName").stringValue;
      var enabled = config.FindPropertyRelative("enabled");

      EditorGUILayout.BeginHorizontal();

      // Checkbox
      enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(40));

      // Type name with tooltip showing full type name
      var content = new GUIContent(displayName, typeName);
      EditorGUILayout.LabelField(content, GUILayout.MinWidth(150));

      // Show if defaults are set
      var hasDefaults = !string.IsNullOrEmpty(config.FindPropertyRelative("serializedDefaults").stringValue);
      if (hasDefaults) {
        EditorGUILayout.LabelField("✓", GUILayout.Width(40));
      }

      // Edit defaults button
      if (GUILayout.Button("Edit Defaults", GUILayout.Width(100))) {
        // Get the type from our discovered states for the defaults editor
        var type = allDiscoveredStates.Values
            .FirstOrDefault(info => info.Type.FullName == typeName)?.Type;

        if (type != null) {
          StateDefaultsEditorWindow.Open(type, config, allDiscoveredStates);
        } else {
          EditorUtility.DisplayDialog("Error",
              "Could not find type information. Try refreshing states.", "OK");
        }
      }

      EditorGUILayout.EndHorizontal();
    }
  }
}