using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace ECSReact.CodeGen
{
  /// <summary>
  /// Two-step code generation for all ECS-React generators with namespace selection.
  /// Step 1: Discover and select namespaces
  /// Step 2: Generate all code for selected namespaces
  /// </summary>
  public class AutoGenerateAllWindow : EditorWindow
  {
    private const string DEFAULT_OUTPUT_PATH = "Assets/Generated/";

    private Vector2 scrollPosition;
    private Dictionary<string, NamespaceGroup> discoveredNamespaces = new Dictionary<string, NamespaceGroup>();
    private bool hasDiscovered = false;

    [MenuItem("ECS React/Auto Generate All")]
    public static void ShowWindow()
    {
      var window = GetWindow<AutoGenerateAllWindow>("Auto Generate All");
      window.minSize = new Vector2(400, 500);
      window.discoverNamespaces();
    }

    private void OnGUI()
    {
      GUILayout.Label("Auto Generate All - Namespace Selection", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      if (!hasDiscovered) {
        EditorGUILayout.HelpBox("Discovering namespaces...", MessageType.Info);
        return;
      }

      if (discoveredNamespaces.Count == 0) {
        EditorGUILayout.HelpBox("No IGameState or IGameAction types found in your project. Make sure you have defined state and action types.", MessageType.Warning);

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Discovery")) {
          discoverNamespaces();
        }
        return;
      }

      // Step 1: Namespace Selection
      EditorGUILayout.LabelField("Step 1: Select Namespaces to Generate", EditorStyles.boldLabel);
      EditorGUILayout.HelpBox("Choose which namespaces you want to generate code for. This will run all three generators (UIStateNotifier, StateSubscriptionHelper, and Store Extensions) for the selected namespaces.", MessageType.Info);

      EditorGUILayout.Space();

      // Select/Deselect All buttons
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Select All", GUILayout.Width(80))) {
        foreach (var ns in discoveredNamespaces.Values) {
          ns.includeInGeneration = true;
        }
      }
      if (GUILayout.Button("Select None", GUILayout.Width(80))) {
        foreach (var ns in discoveredNamespaces.Values) {
          ns.includeInGeneration = false;
        }
      }
      GUILayout.FlexibleSpace();
      if (GUILayout.Button("Refresh Discovery", GUILayout.Width(120))) {
        discoverNamespaces();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // Namespace list with checkboxes
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

      foreach (var kvp in discoveredNamespaces.OrderBy(ns => ns.Key)) {
        var namespaceName = kvp.Key;
        var namespaceInfo = kvp.Value;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        // Checkbox for namespace
        namespaceInfo.includeInGeneration = EditorGUILayout.Toggle(namespaceInfo.includeInGeneration, GUILayout.Width(24));

        // Namespace name
        EditorGUILayout.LabelField(namespaceName, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

        EditorGUILayout.LabelField(namespaceInfo.assemblyName, EditorStyles.miniLabel, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // Summary of what's in this namespace
        string summary = $"{namespaceInfo.stateCount} states, {namespaceInfo.actionCount} actions";
        EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);


        EditorGUILayout.EndVertical();
      }

      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space();

      // Step 2: Generation
      EditorGUILayout.LabelField("Step 2: Generate Code", EditorStyles.boldLabel);

      var selectedNamespaces = discoveredNamespaces.Values.Where(ns => ns.includeInGeneration).ToList();

      if (selectedNamespaces.Count == 0) {
        EditorGUILayout.HelpBox("Select at least one namespace to enable code generation.", MessageType.Info);
      } else {
        EditorGUILayout.HelpBox($"Ready to generate code for {selectedNamespaces.Count} namespace(s):\n• " +
            string.Join("\n• ", selectedNamespaces.Select(ns => $"{ns.namespaceName} ({ns.stateCount} states, {ns.actionCount} actions)")),
            MessageType.Info);
      }

      EditorGUILayout.Space();

      // Generate All button
      GUI.enabled = selectedNamespaces.Count > 0;
      if (GUILayout.Button("🚀 Generate All Selected Namespaces", GUILayout.Height(40))) {
        generateAllForSelectedNamespaces(selectedNamespaces);
      }
      GUI.enabled = true;

      EditorGUILayout.Space();

      // Additional options
      EditorGUILayout.LabelField("Additional Options", EditorStyles.boldLabel);

      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Open Generated Folder")) {
        openGeneratedFolder();
      }
      if (GUILayout.Button("Clean Generated Code")) {
        cleanGeneratedCode();
      }
      EditorGUILayout.EndHorizontal();
    }

    private void discoverNamespaces()
    {
      hasDiscovered = false;
      discoveredNamespaces.Clear();

      // Preserve previous selections
      var previousSelections = discoveredNamespaces.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.includeInGeneration);

      try {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies) {
          try {
            var types = assembly.GetTypes()
                .Where(t => t.IsValueType && !t.IsEnum && !t.IsGenericType)
                .Where(t => typeof(IComponentData).IsAssignableFrom(t))
                .Where(t => t.GetInterfaces().Any(i => i.Name == "IGameState" || i.Name == "IGameAction"))
                .ToList();

            foreach (var type in types) {
              string namespaceName = type.Namespace ?? "Global";
              bool isState = type.GetInterfaces().Any(i => i.Name == "IGameState");
              bool isAction = type.GetInterfaces().Any(i => i.Name == "IGameAction");

              if (!discoveredNamespaces.ContainsKey(namespaceName)) {
                discoveredNamespaces[namespaceName] = new NamespaceGroup
                {
                  namespaceName = namespaceName,
                  includeInGeneration = previousSelections.GetValueOrDefault(
                    namespaceName,
                    // Default to selected UNLESS it's the core namespace
                    namespaceName == "ECSReact.Core" ? false : true),
                  assemblyName = assembly.GetName().Name,
                  states = new List<StateTypeInfo>(),
                  actions = new List<ActionTypeInfo>(),
                };
              }

              var namespaceInfo = discoveredNamespaces[namespaceName];

              if (isState) {
                var stateInfo = new StateTypeInfo
                {
                  typeName = type.Name,
                  fullTypeName = type.FullName,
                  namespaceName = namespaceName,
                  assemblyName = assembly.GetName().Name,
                  includeInGeneration = namespaceName == "ECSReact.Core" ? false : true,
                };
                namespaceInfo.states.Add(stateInfo);
              }

              if (isAction) {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                  .Where(f => !f.IsStatic)
                  .Select(f => new FieldInfo
                  {
                    fieldName = f.Name,
                    fieldType = CodeGenUtils.GetFriendlyTypeName(f.FieldType),
                    isOptional = CodeGenUtils.IsOptionalField(f)
                  })
                  .ToList();
                var actionInfo = new ActionTypeInfo
                {
                  typeName = type.Name,
                  fullTypeName = type.FullName,
                  namespaceName = namespaceName,
                  assemblyName = assembly.GetName().Name,
                  includeInGeneration = namespaceName == "ECSReact.Core" ? false : true,
                  fields = fields
                };
                namespaceInfo.actions.Add(actionInfo);
              }
            }
          } catch (Exception ex) {
            Debug.LogWarning($"Error discovering types in assembly {assembly.GetName().Name}: {ex.Message}");
          }
        }

        hasDiscovered = true;
        int totalStates = discoveredNamespaces.Values.Sum(ns => ns.stateCount);
        int totalActions = discoveredNamespaces.Values.Sum(ns => ns.actionCount);

        Debug.Log($"Auto Generate All: Discovered {totalStates} states and {totalActions} actions across {discoveredNamespaces.Count} namespaces");
      } catch (Exception ex) {
        Debug.LogError($"Auto Generate All: Discovery failed - {ex.Message}");
        hasDiscovered = true; // Allow UI to show error state
      }
    }

    private void generateAllForSelectedNamespaces(List<NamespaceGroup> selectedNamespaces)
    {
      if (!EditorUtility.DisplayDialog(
          "Generate All Selected",
          $"This will automatically generate code for {selectedNamespaces.Count} namespace(s):\n\n" +
          "• UIStateNotifier extensions\n" +
          "• StateSubscriptionHelper extensions\n" +
          "• Store action dispatch extensions\n\n" +
          "Selected namespaces:\n• " + string.Join("\n• ", selectedNamespaces.Select(ns => ns.namespaceName)) + "\n\n" +
          "Continue?",
          "Generate All",
          "Cancel")) {
        return;
      }

      bool success = true;
      var results = new List<string>();

      try {
        EditorUtility.DisplayProgressBar("Auto Generate All", "Preparing output directory...", 0.1f);

        // Ensure output directory exists
        if (!Directory.Exists(DEFAULT_OUTPUT_PATH)) {
          Directory.CreateDirectory(DEFAULT_OUTPUT_PATH);
          Debug.Log($"Created output directory: {DEFAULT_OUTPUT_PATH}");
        }

        // Step 1: Generate UIStateNotifier extensions
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating UIStateNotifier extensions...", 0.25f);
        var uiStateResult = generateUIStateNotifierForNamespaces(selectedNamespaces);
        results.Add(uiStateResult.summary);
        success &= uiStateResult.success;

        // Step 2: Generate StateSubscriptionHelper extensions  
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating StateSubscriptionHelper extensions...", 0.5f);
        var subscriptionResult = generateStateSubscriptionForNamespaces(selectedNamespaces);
        results.Add(subscriptionResult.summary);
        success &= subscriptionResult.success;

        // Step 3: Generate Store extensions
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating Store extensions...", 0.75f);
        var storeResult = generateStoreExtensionsForNamespaces(selectedNamespaces);
        results.Add(storeResult.summary);
        success &= storeResult.success;

        // Step 4: Refresh Unity
        EditorUtility.DisplayProgressBar("Auto Generate All", "Refreshing Unity assets...", 0.9f);
        AssetDatabase.Refresh();

        EditorUtility.ClearProgressBar();

        // Show results
        string title = success ? "Generation Complete!" : "Generation Completed with Issues";
        string message = success ?
            $"All code generation completed successfully for {selectedNamespaces.Count} namespaces!\n\n" + string.Join("\n", results) :
            $"Code generation completed but some generators had issues:\n\n" + string.Join("\n", results);

        EditorUtility.DisplayDialog(title, message, "OK");

        if (success) {
          Debug.Log($"🎉 Auto Generate All: Successfully generated all ECS-React code for {selectedNamespaces.Count} namespaces!");
        }
      } catch (Exception ex) {
        EditorUtility.ClearProgressBar();

        string errorMessage = $"Auto generation failed with error:\n\n{ex.Message}\n\nCheck the console for details.";
        EditorUtility.DisplayDialog("Generation Failed", errorMessage, "OK");

        Debug.LogError($"Auto Generate All failed: {ex}");
      }
    }

    private GenerationResult generateUIStateNotifierForNamespaces(List<NamespaceGroup> namespaces)
    {
      try {
        // Simplified implementation - in practice, you'd integrate with the actual generator
        var namespacesWithStates = namespaces.Where(ns => ns.stateCount > 0).ToList();

        if (namespacesWithStates.Count == 0) {
          return new GenerationResult
          {
            success = false,
            summary = "❌ UIStateNotifier: No states found in selected namespaces"
          };
        }

        foreach (var ns in namespacesWithStates) {
          var gen = new UIStateNotifierGenerator();
          var files = new List<string>();
          gen.GenerateUIStateNotifierExtensionsForNamespace(ns, ref files);
        }
        int totalStates = namespacesWithStates.Sum(ns => ns.stateCount);

        return new GenerationResult
        {
          success = true,
          summary = $"✅ UIStateNotifier: Generated for {totalStates} states across {namespacesWithStates.Count} namespaces"
        };
      } catch (Exception ex) {
        Debug.LogError($"Failed to generate UIStateNotifier extensions: {ex.Message}");
        return new GenerationResult
        {
          success = false,
          summary = $"❌ UIStateNotifier: Generation failed - {ex.Message}"
        };
      }
    }

    private GenerationResult generateStateSubscriptionForNamespaces(List<NamespaceGroup> namespaces)
    {
      try {
        var namespacesWithStates = namespaces.Where(ns => ns.stateCount > 0).ToList();

        if (namespacesWithStates.Count == 0) {
          return new GenerationResult
          {
            success = false,
            summary = "❌ StateSubscriptionHelper: No states found in selected namespaces"
          };
        }

        foreach (var ns in namespacesWithStates) {
          var gen = new StateSubscriptionHelperGenerator();
          var files = new List<string>();
          gen.GenerateStateSubscriptionHelperCodeForNamespace(ns, ref files);
        }
        int totalStates = namespacesWithStates.Sum(ns => ns.stateCount);

        return new GenerationResult
        {
          success = true,
          summary = $"✅ StateSubscriptionHelper: Generated for {totalStates} states across {namespacesWithStates.Count} namespaces"
        };
      } catch (Exception ex) {
        Debug.LogError($"Failed to generate StateSubscriptionHelper extensions: {ex.Message}");
        return new GenerationResult
        {
          success = false,
          summary = $"❌ StateSubscriptionHelper: Generation failed - {ex.Message}"
        };
      }
    }

    private GenerationResult generateStoreExtensionsForNamespaces(List<NamespaceGroup> namespaces)
    {
      try {
        var namespacesWithActions = namespaces.Where(ns => ns.actionCount > 0).ToList();

        if (namespacesWithActions.Count == 0) {
          return new GenerationResult
          {
            success = false,
            summary = "❌ Store Extensions: No actions found in selected namespaces"
          };
        }

        foreach (var ns in namespacesWithActions) {
          var gen = new StoreExtensionsGenerator();
          var files = new List<string>();
          gen.GenerateStoreExtensionsForNamespace(ns, ref files);
        }
        int totalActions = namespacesWithActions.Sum(ns => ns.actionCount);

        return new GenerationResult
        {
          success = true,
          summary = $"✅ Store Extensions: Generated for {totalActions} actions across {namespacesWithActions.Count} namespaces"
        };
      } catch (Exception ex) {
        Debug.LogError($"Failed to generate Store extensions: {ex.Message}");
        return new GenerationResult
        {
          success = false,
          summary = $"❌ Store Extensions: Generation failed - {ex.Message}"
        };
      }
    }

    /// <summary>
    /// Quick menu item to open the generated code folder.
    /// </summary>
    private static void openGeneratedFolder()
    {
      if (Directory.Exists(DEFAULT_OUTPUT_PATH)) {
        EditorUtility.RevealInFinder(DEFAULT_OUTPUT_PATH);
      } else {
        EditorUtility.DisplayDialog("Folder Not Found",
            $"Generated code folder not found at:\n{DEFAULT_OUTPUT_PATH}\n\nRun 'Auto Generate All' first to create generated code.",
            "OK");
      }
    }

    /// <summary>
    /// Clean up all generated files.
    /// </summary>
    private static void cleanGeneratedCode()
    {
      if (!EditorUtility.DisplayDialog(
          "Clean Generated Code",
          "This will delete all generated code files in the Generated folder and all its subfolders.\n\n" +
          "Are you sure?",
          "Delete All",
          "Cancel")) {
        return;
      }

      try {
        if (Directory.Exists(DEFAULT_OUTPUT_PATH)) {
          Directory.Delete(DEFAULT_OUTPUT_PATH, true);
          AssetDatabase.Refresh();

          EditorUtility.DisplayDialog("Clean Complete",
              "All generated code has been deleted.",
              "OK");

          Debug.Log("Auto Generate All: Cleaned all generated code");
        } else {
          EditorUtility.DisplayDialog("Nothing to Clean",
              "No generated code folder found.",
              "OK");
        }
      } catch (Exception ex) {
        EditorUtility.DisplayDialog("Clean Failed",
            $"Failed to clean generated code:\n{ex.Message}",
            "OK");
        Debug.LogError($"Failed to clean generated code: {ex}");
      }
    }
  }
}
