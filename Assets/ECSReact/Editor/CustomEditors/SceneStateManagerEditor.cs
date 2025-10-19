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
  public class SceneStateManagerEditor : UnityEditor.Editor
  {
    private Dictionary<string, bool> namespaceFoldouts = new();
    private Dictionary<string, bool> namespaceIncludeAll = new();
    private string searchFilter = "";
    private bool showOnlyEnabled = false;
    private List<IStateRegistry> cachedRegistries;
    private Dictionary<Type, IStateInfo> allDiscoveredStates = new();
    private Dictionary<string, List<StateConfiguration>> duplicateStates = new();

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

      // Clear and rebuild configurations
      configurationsProperty.ClearArray();

      foreach (var kvp in allDiscoveredStates) {
        var stateInfo = kvp.Value;

        var index = configurationsProperty.arraySize;
        configurationsProperty.InsertArrayElementAtIndex(index);
        var configProp = configurationsProperty.GetArrayElementAtIndex(index);

        configProp.FindPropertyRelative("typeName").stringValue = stateInfo.Type.FullName;
        configProp.FindPropertyRelative("namespaceName").stringValue = stateInfo.Namespace ?? "Global";
        configProp.FindPropertyRelative("displayName").stringValue = stateInfo.Name;

        // Restore existing settings if available
        if (existingConfigs.TryGetValue(stateInfo.Type.FullName, out var existing)) {
          configProp.FindPropertyRelative("enabled").boolValue = existing.enabled;
          configProp.FindPropertyRelative("serializedDefaults").stringValue = existing.defaults;
        } else {
          configProp.FindPropertyRelative("enabled").boolValue = false;
          configProp.FindPropertyRelative("serializedDefaults").stringValue = "";
        }
      }

      serializedObject.ApplyModifiedProperties();
    }

    private void checkForDuplicates()
    {
      duplicateStates.Clear();
      var configurationsProperty = serializedObject.FindProperty("stateConfigurations");

      // Group by base type name (without namespace)
      var statesByBaseName = new Dictionary<string, List<StateConfiguration>>();

      for (int i = 0; i < configurationsProperty.arraySize; i++) {
        var config = configurationsProperty.GetArrayElementAtIndex(i);
        var typeName = config.FindPropertyRelative("typeName").stringValue;
        var namespaceName = config.FindPropertyRelative("namespaceName").stringValue;
        var displayName = config.FindPropertyRelative("displayName").stringValue;
        var enabled = config.FindPropertyRelative("enabled").boolValue;

        var baseTypeName = typeName.Split('.').Last();

        if (!statesByBaseName.ContainsKey(baseTypeName)) {
          statesByBaseName[baseTypeName] = new List<StateConfiguration>();
        }

        statesByBaseName[baseTypeName].Add(new StateConfiguration
        {
          typeName = typeName,
          namespaceName = namespaceName,
          displayName = displayName,
          enabled = enabled
        });
      }

      // Find duplicates
      foreach (var kvp in statesByBaseName) {
        if (kvp.Value.Count > 1) {
          duplicateStates[kvp.Key] = kvp.Value;
        }
      }
    }

    private void refreshRegistries()
    {
      cachedRegistries = discoverAllRegistries();
      allDiscoveredStates = new Dictionary<Type, IStateInfo>();

      // Collect all states from all registries
      foreach (var registry in cachedRegistries) {
        foreach (var kvp in registry.AllStates) {
          // Store all instances, even duplicates
          allDiscoveredStates[kvp.Key] = kvp.Value;
        }
      }

      Debug.Log($"[StateManagerEditor] Discovered {cachedRegistries.Count} registries with {allDiscoveredStates.Count} total states");
    }

    private List<IStateRegistry> discoverAllRegistries()
    {
      var registries = new List<IStateRegistry>();

      // Use StateRegistryService.AllRegistries if available
      if (StateRegistryService.AllRegistries != null && StateRegistryService.AllRegistries.Count > 0) {
        registries.AddRange(StateRegistryService.AllRegistries);
        return registries;
      }

      // Fallback to discovery
      var registryInterface = typeof(IStateRegistry);

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
        EditorUtility.SetDirty(manager);
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.EndVertical();

      EditorGUILayout.Space(10);

      // Check for duplicates
      checkForDuplicates();

      // Show duplicate warnings
      if (duplicateStates.Count > 0) {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox(
            "⚠️ Duplicate state types detected across namespaces!\n" +
            "States are singletons - only one instance of each type can exist.\n" +
            "Consider using unique type names or only enable one per type.",
            MessageType.Warning);

        foreach (var kvp in duplicateStates) {
          var enabled = kvp.Value.Count(s => s.enabled);
          var icon = enabled > 1 ? "❌" : "⚠️";
          var color = enabled > 1 ? Color.red : Color.yellow;

          using (new EditorGUILayout.HorizontalScope()) {
            var oldColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField($"{icon} {kvp.Key}:", EditorStyles.boldLabel, GUILayout.Width(200));
            GUI.color = oldColor;

            var namespaces = kvp.Value.Select(s => $"{s.namespaceName} [{(s.enabled ? "ON" : "OFF")}]");
            EditorGUILayout.LabelField(string.Join(", ", namespaces));
          }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
      }

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

      EditorGUILayout.HelpBox($"Total States: {configurationsProperty.arraySize} | Enabled: {enabledCount} | Registries: {cachedRegistries.Count}", MessageType.Info);

      EditorGUILayout.Space(5);

      // Filters and controls
      EditorGUILayout.BeginVertical();

      EditorGUILayout.BeginHorizontal();
      var width = 164;
      EditorGUILayout.LabelField("Search:", GUILayout.Width(width));
      var oldWidth = EditorGUIUtility.labelWidth;
      EditorGUIUtility.labelWidth = width;
      searchFilter = EditorGUILayout.TextField(searchFilter);
      EditorGUILayout.EndHorizontal();

      showOnlyEnabled = EditorGUILayout.Toggle("Show only enabled", showOnlyEnabled, GUILayout.Width(120));
      EditorGUIUtility.labelWidth = oldWidth;

      EditorGUILayout.Space(3);

      // Select/Deselect all buttons
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Enable All Visible")) {
        enableAllVisible(true);
      }
      if (GUILayout.Button("Disable All Visible")) {
        enableAllVisible(false);
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.EndVertical();

      EditorGUILayout.Space(10);

      // Group states by namespace
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

      // Draw namespace groups
      foreach (var kvp in statesByNamespace.OrderBy(k => k.Key)) {
        drawNamespaceGroup(kvp.Key, kvp.Value);
      }

      serializedObject.ApplyModifiedProperties();
    }

    private void drawNamespaceGroup(string namespaceName, List<SerializedProperty> configs)
    {
      var isDuplicate = configs.Any(c =>
      {
        var typeName = c.FindPropertyRelative("typeName").stringValue;
        var baseTypeName = typeName.Split('.').Last();
        return duplicateStates.ContainsKey(baseTypeName);
      });

      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      // Namespace header
      EditorGUILayout.BeginHorizontal();

      // Toggle all in namespace
      var allEnabled = configs.All(c => c.FindPropertyRelative("enabled").boolValue);
      var newAllEnabled = EditorGUILayout.Toggle(allEnabled, GUILayout.Width(36));
      if (newAllEnabled != allEnabled) {
        foreach (var config in configs) {
          config.FindPropertyRelative("enabled").boolValue = newAllEnabled;
        }
      }

      if (isDuplicate) {
        var oldColor = GUI.color;
        GUI.color = Color.yellow;
        namespaceFoldouts[namespaceName] = EditorGUILayout.Foldout(
          namespaceFoldouts.GetValueOrDefault(namespaceName, true),
          $"  ⚠️ {namespaceName} ({configs.Count} states)",
          true
        );
        GUI.color = oldColor;
      } else {
        namespaceFoldouts[namespaceName] = EditorGUILayout.Foldout(
          namespaceFoldouts.GetValueOrDefault(namespaceName, true),
          $"  {namespaceName} ({configs.Count} states)",
          true
        );
      }

      EditorGUILayout.EndHorizontal();

      // Show states in namespace
      if (namespaceFoldouts[namespaceName]) {
        EditorGUI.indentLevel++;

        foreach (var config in configs) {
          var enabled = config.FindPropertyRelative("enabled").boolValue;
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

    private void drawStateConfiguration(SerializedProperty config)
    {
      var typeName = config.FindPropertyRelative("typeName").stringValue;
      var displayName = config.FindPropertyRelative("displayName").stringValue;
      var enabled = config.FindPropertyRelative("enabled");
      var baseTypeName = typeName.Split('.').Last();
      var isDuplicate = duplicateStates.ContainsKey(baseTypeName);

      EditorGUILayout.BeginHorizontal();

      // Checkbox with warning color if duplicate
      var oldColor = GUI.color;
      if (isDuplicate && enabled.boolValue) {
        var dupeStates = duplicateStates[baseTypeName];
        var enabledDupes = dupeStates.Count(s => s.enabled);
        if (enabledDupes > 1) {
          GUI.color = Color.red;
        }
      }

      enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(40));
      GUI.color = oldColor;

      // Type name with tooltip showing full type name
      var content = isDuplicate
        ? new GUIContent($"⚠️ {displayName}", typeName + "\n⚠️ Duplicate type name detected!")
        : new GUIContent(displayName, typeName);
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

    private void enableAllVisible(bool enable)
    {
      var configurationsProperty = serializedObject.FindProperty("stateConfigurations");

      for (int i = 0; i < configurationsProperty.arraySize; i++) {
        var config = configurationsProperty.GetArrayElementAtIndex(i);
        var displayName = config.FindPropertyRelative("displayName").stringValue;

        // Check search filter
        if (!string.IsNullOrEmpty(searchFilter) &&
            !displayName.ToLower().Contains(searchFilter.ToLower())) {
          continue;
        }

        config.FindPropertyRelative("enabled").boolValue = enable;
      }
    }

    private void clearRegistries()
    {
      var configurationsProperty = serializedObject.FindProperty("stateConfigurations");
      configurationsProperty.ClearArray();
      serializedObject.ApplyModifiedProperties();
    }
  }
}