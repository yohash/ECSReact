using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor.CodeGeneration
{
  /// <summary>
  /// Two-step code generation for all ECS-React generators with namespace selection.
  /// Step 1: Discover and select namespaces
  /// Step 2: Generate all code for selected namespaces
  /// </summary>
  public class AutoGenerateAllWindow : EditorWindow
  {
    private Vector2 scrollPosition;
    private Dictionary<string, NamespaceGroup> discoveredNamespaces = new Dictionary<string, NamespaceGroup>();
    private string outputPath = Constants.DEFAULT_OUTPUT_PATH;
    private bool hasDiscovered = false;

    [MenuItem("ECS React/Auto Generate All Code", priority = 210)]
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

      // Output path selection
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField("Output Path:", GUILayout.Width(80));
      outputPath = EditorGUILayout.TextField(outputPath);
      if (GUILayout.Button("Browse", GUILayout.Width(60))) {
        string selectedPath = EditorUtility.OpenFolderPanel("Select Output Folder", outputPath, "");
        if (!string.IsNullOrEmpty(selectedPath)) {
          outputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length) + "/";
        }
      }
      EditorGUILayout.EndHorizontal();

      if (!hasDiscovered) {
        EditorGUILayout.HelpBox("Discovering namespaces...", MessageType.Info);
        return;
      }

      if (discoveredNamespaces.Count == 0) {
        EditorGUILayout.HelpBox("No IGameState, IGameAction, or Reducer/Middleware systems found in your project.", MessageType.Warning);

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Discovery")) {
          discoverNamespaces();
        }
        return;
      }

      // Step 1: Namespace Selection
      EditorGUILayout.LabelField("Select Namespaces to Generate", EditorStyles.boldLabel);
      EditorGUILayout.HelpBox(
        "Choose which namespaces you want to generate code for. This will run all generators:\n" +
        "• Bridge Systems (for reducers/middleware)\n" +
        "• State Registry\n" +
        "• UIStateNotifier extensions\n" +
        "• StateSubscriptionHelper extensions\n" +
        "• Store action dispatch extensions",
        MessageType.Info);

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

        EditorGUILayout.LabelField(namespaceInfo.assemblyName, EditorStyles.miniLabel, GUILayout.Width(240));
        EditorGUILayout.EndHorizontal();

        // Summary of what's in this namespace
        int totalStates = discoveredNamespaces.Values.Sum(ns => ns.StateCount);
        int totalActions = discoveredNamespaces.Values.Sum(ns => ns.ActionCount);
        int totalReducers = discoveredNamespaces.Values.Sum(ns => ns.ReducerCount);
        int totalBurstReducers = discoveredNamespaces.Values.Sum(ns => ns.BurstReducerCount);
        int totalMiddleware = discoveredNamespaces.Values.Sum(ns => ns.MiddlewareCount);
        int totalBurstMiddleware = discoveredNamespaces.Values.Sum(ns => ns.BurstMiddlewareCount);

        var summaryParts = new List<string>();

        if (totalStates > 0) { summaryParts.Add($"{totalStates} states"); }
        if (totalActions > 0) { summaryParts.Add($"{totalActions} actions"); }
        if (totalReducers > 0) { summaryParts.Add($"{totalReducers} reducers"); }
        if (totalBurstReducers > 0) { summaryParts.Add($"{totalBurstReducers} burst reducers"); }
        if (totalMiddleware > 0) { summaryParts.Add($"{totalMiddleware} middleware"); }
        if (totalBurstMiddleware > 0) { summaryParts.Add($"{totalBurstMiddleware} burst middleware"); }
        var summary = string.Join(", ", summaryParts);

        EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
      }

      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space();

      // Step 2: Generation
      EditorGUILayout.LabelField("Generate Code", EditorStyles.boldLabel);

      var selectedNamespaces = discoveredNamespaces.Values.Where(ns => ns.includeInGeneration).ToList();

      if (selectedNamespaces.Count == 0) {
        EditorGUILayout.HelpBox("Select at least one namespace to enable code generation.", MessageType.Info);
      } else {
        int totalSystems = selectedNamespaces.Sum(ns => ns.SystemCount);
        string systemInfo = totalSystems > 0 ? $" + {totalSystems} bridge systems" : "";

        EditorGUILayout.HelpBox($"Ready to generate code for {selectedNamespaces.Count} namespace(s):\n• " +
            string.Join("\n• ", selectedNamespaces.Select(ns =>
                $"{ns.namespaceName} ({ns.StateCount} states, {ns.ActionCount} actions" +
                (ns.SystemCount > 0 ? $", {ns.SystemCount} systems)" : ")"))),
            MessageType.Info);
      }

      EditorGUILayout.Space();

      // Generate All button
      GUI.enabled = selectedNamespaces.Count > 0;
      if (GUILayout.Button("Generate All Selected Namespaces", GUILayout.Height(40))) {
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
            var types = assembly.GetTypes();

            // First, discover states and actions
            var componentTypes = types
                .Where(t => t.IsValueType && !t.IsEnum && !t.IsGenericType)
                .Where(t => typeof(IComponentData).IsAssignableFrom(t))
                .Where(t => t.GetInterfaces().Any(i => i.Name == "IGameState" || i.Name == "IGameAction"))
                .ToList();

            foreach (var type in componentTypes) {
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
                  assemblyName = assembly.GetName().Name
                };
              }

              var namespaceInfo = discoveredNamespaces[namespaceName];

              if (isState) {
                var stateInfo = new StateTypeInfo
                {
                  stateType = type,
                  typeName = type.Name,
                  fullTypeName = type.FullName,
                  namespaceName = namespaceName,
                  assemblyName = assembly.GetName().Name,
                  includeInGeneration = true
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
                  includeInGeneration = type.Name == "Payload" ? false : true,
                  fields = fields
                };
                namespaceInfo.actions.Add(actionInfo);
              }
            }

            // Then, discover reducer and middleware systems
            var systemTypes = types
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType && isRedOrMed(t))
                .ToList();

            bool isRedOrMed(Type type)
            {
              // Check if it inherits from any of our system base classes
              var baseType = type.BaseType;

              while (baseType != null) {
                if (baseType.IsGenericType) {
                  var genericDef = baseType.GetGenericTypeDefinition();
                  var genericDefName = genericDef.Name;

                  // Check for our four system types
                  if (genericDefName == "ReducerSystem`2" ||
                      genericDefName == "BurstReducerSystem`3" ||
                      genericDefName == "MiddlewareSystem`1" ||
                      genericDefName == "BurstMiddlewareSystem`2") {
                    return true;
                  }
                }
                baseType = baseType.BaseType;
              }

              return false;
            }

            foreach (var type in systemTypes) {
              string namespaceName = type.Namespace ?? "Global";

              if (!discoveredNamespaces.ContainsKey(namespaceName)) {
                discoveredNamespaces[namespaceName] = new NamespaceGroup
                {
                  namespaceName = namespaceName,
                  includeInGeneration = previousSelections.GetValueOrDefault(
                    namespaceName,
                    namespaceName == "ECSReact.Core" ? false : true),
                  assemblyName = assembly.GetName().Name
                };
              }

              var namespaceInfo = discoveredNamespaces[namespaceName];

              // Add system info
              var systemInfo = BridgeSystemGenerator.AnalyzeSystemType(type);
              namespaceInfo.systems.Add(systemInfo);
            }

          } catch (Exception ex) {
            Debug.LogWarning($"Error discovering types in assembly {assembly.GetName().Name}: {ex.Message}");
          }
        }

        hasDiscovered = true;
        int totalStates = discoveredNamespaces.Values.Sum(ns => ns.StateCount);
        int totalActions = discoveredNamespaces.Values.Sum(ns => ns.ActionCount);
        int totalReducers = discoveredNamespaces.Values.Sum(ns => ns.ReducerCount);
        int totalBurstReducers = discoveredNamespaces.Values.Sum(ns => ns.BurstReducerCount);
        int totalMiddleware = discoveredNamespaces.Values.Sum(ns => ns.MiddlewareCount);
        int totalBurstMiddleware = discoveredNamespaces.Values.Sum(ns => ns.BurstMiddlewareCount);

        Debug.Log($"Auto Generate All: " +
          $"Discovered {totalStates} states, " +
          $"{totalActions} actions, " +
          $"{totalReducers} reducers" +
          (totalBurstReducers > 0 ? $" ({totalBurstReducers} burst), " : ", ") +
          $"and {totalMiddleware} middleware" +
          (totalBurstMiddleware > 0 ? $" ({totalBurstMiddleware} burst) " : " ") +
          $"across {discoveredNamespaces.Count} namespaces");
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
          "• Bridge Systems (for reducers/middleware)\n" +
          "• State Registry\n" +
          "• UIStateNotifier extensions\n" +
          "• StateSubscriptionHelper extensions\n" +
          "• Store action dispatch extensions\n\n" +
          "Selected namespaces:\n• " + string.Join("\n• ", selectedNamespaces.Select(ns => ns.namespaceName)) + "\n\n" +
          "Continue?",
          "Generate",
          "Cancel")) {
        return;
      }

      try {
        List<GenerationResult> results = new List<GenerationResult>();

        EditorUtility.DisplayProgressBar("Auto Generate All", "Starting generation...", 0);

        // Generate Bridge Systems
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating Bridge Systems...", 0.1f);
        results.Add(generateBridgeSystemsForNamespaces(selectedNamespaces));

        // Generate StateRegistry
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating State Registry...", 0.3f);
        results.Add(generateStateRegistryForNamespaces(selectedNamespaces));

        // Generate UIStateNotifier
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating UIStateNotifier extensions...", 0.5f);
        results.Add(generateUIStateNotifierForNamespaces(selectedNamespaces));

        // Generate StateSubscriptionHelper
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating StateSubscriptionHelper extensions...", 0.7f);
        results.Add(generateStateSubscriptionForNamespaces(selectedNamespaces));

        // Generate Store Extensions
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating Store extensions...", 0.9f);
        results.Add(generateStoreExtensionsForNamespaces(selectedNamespaces));

        EditorUtility.ClearProgressBar();

        // Refresh the asset database to recognize the new files
        AssetDatabase.Refresh();

        // Show results
        string summaryMessage = "Code generation complete!\n\n";
        foreach (var result in results) {
          summaryMessage += result.summary + "\n";
        }

        EditorUtility.DisplayDialog("Generation Complete", summaryMessage, "OK");

        Debug.Log($"Auto Generate All: Successfully generated code for {selectedNamespaces.Count} namespaces\n" +
                  string.Join("\n", results.Select(r => r.summary)));

      } catch (Exception ex) {
        EditorUtility.ClearProgressBar();

        string errorMessage = $"Auto generation failed with error:\n\n{ex.Message}\n\nCheck the console for details.";
        EditorUtility.DisplayDialog("Generation Failed", errorMessage, "OK");

        Debug.LogError($"Auto Generate All failed: {ex}");
      }
    }

    private GenerationResult generateBridgeSystemsForNamespaces(List<NamespaceGroup> namespaces)
    {
      try {
        var namespacesWithSystems = namespaces.Where(ns => ns.SystemCount > 0).ToList();

        if (namespacesWithSystems.Count == 0) {
          return new GenerationResult
          {
            success = true, // Not a failure, just nothing to do
            summary = "✓ Bridge Systems: No reducer/middleware systems found to generate bridges for"
          };
        }

        // Call the BridgeSystemGenerator method
        var gen = new BridgeSystemGenerator();
        var generatedFiles = new List<string>();
        int totalBridges = 0;

        foreach (var ns in namespacesWithSystems) {
          int bridgesInNamespace = gen.GenerateBridgesForNamespace(ns, outputPath, ref generatedFiles);
          totalBridges += bridgesInNamespace;
        }

        return new GenerationResult
        {
          success = true,
          summary = $"✅ Bridge Systems: Generated {totalBridges} bridges across {namespacesWithSystems.Count} namespaces"
        };
      } catch (Exception ex) {
        Debug.LogError($"Failed to generate Bridge Systems: {ex.Message}");
        return new GenerationResult
        {
          success = false,
          summary = $"❌ Bridge Systems: Generation failed - {ex.Message}"
        };
      }
    }

    private GenerationResult generateStateRegistryForNamespaces(List<NamespaceGroup> namespaces)
    {
      try {
        var namespacesWithStates = namespaces.Where(ns => ns.StateCount > 0).ToList();

        if (namespacesWithStates.Count == 0) {
          return new GenerationResult
          {
            success = false,
            summary = "❌ StateRegistryGenerator: No states found in selected namespaces"
          };
        }

        foreach (var ns in namespacesWithStates) {
          var gen = new StateRegistryGenerator();
          var files = new List<string>();
          gen.GenerateRegistryForNamespace(ns, ref files);
        }
        int totalStates = namespacesWithStates.Sum(ns => ns.StateCount);

        return new GenerationResult
        {
          success = true,
          summary = $"✅ StateRegistryGenerator: Generated for {totalStates} states across {namespacesWithStates.Count} namespaces"
        };
      } catch (Exception ex) {
        Debug.LogError($"Failed to generate StateRegistryGenerator extensions: {ex.Message}");
        return new GenerationResult
        {
          success = false,
          summary = $"❌ StateRegistryGenerator: Generation failed - {ex.Message}"
        };
      }
    }

    private GenerationResult generateUIStateNotifierForNamespaces(List<NamespaceGroup> namespaces)
    {
      try {
        var namespacesWithStates = namespaces.Where(ns => ns.StateCount > 0).ToList();

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
        int totalStates = namespacesWithStates.Sum(ns => ns.StateCount);

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
        var namespacesWithStates = namespaces.Where(ns => ns.StateCount > 0).ToList();

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
        int totalStates = namespacesWithStates.Sum(ns => ns.StateCount);

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
        var namespacesWithActions = namespaces.Where(ns => ns.ActionCount > 0).ToList();

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
        int totalActions = namespacesWithActions.Sum(ns => ns.ActionCount);

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
    private void openGeneratedFolder()
    {
      if (Directory.Exists(outputPath)) {
        EditorUtility.RevealInFinder(outputPath);
      } else {
        EditorUtility.DisplayDialog("Folder Not Found",
            $"Generated code folder not found at:\n{outputPath}\n\nRun 'Generate All' first to create generated code.",
            "OK");
      }
    }

    /// <summary>
    /// Clean up all generated files.
    /// </summary>
    private void cleanGeneratedCode()
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
        if (Directory.Exists(outputPath)) {
          Directory.Delete(outputPath, true);
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