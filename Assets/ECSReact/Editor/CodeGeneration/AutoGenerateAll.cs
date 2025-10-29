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
        "• ISystem Bridges (for new IReducer/IParallelReducer/IMiddleware/IParallelMiddleware)\n" +
        "• State Registry\n" +
        "• UIStateNotifier extensions\n" +
        "• StateSubscriptionHelper extensions",
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
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

      foreach (var kvp in discoveredNamespaces.OrderBy(ns => ns.Key)) {
        drawNamespace(kvp.Key, kvp.Value);
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
        int totalISystems = selectedNamespaces.Sum(ns => ns.ISystemBridgeCount);
        string systemInfo = totalSystems > 0 ? $" + {totalSystems} old bridge systems" : "";
        string iSystemInfo = totalISystems > 0 ? $" + {totalISystems} ISystem bridges" : "";

        EditorGUILayout.HelpBox($"Ready to generate code for {selectedNamespaces.Count} namespace(s):\n• " +
            string.Join("\n• ", selectedNamespaces.Select(ns =>
                $"{ns.namespaceName} ({ns.StateCount} states, {ns.ActionCount} actions{(ns.SystemCount > 0 ? $", {ns.SystemCount} old systems" : "")}{(ns.ISystemBridgeCount > 0 ? $", {ns.ISystemBridgeCount} ISystem bridges" : "")})")),
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

      EditorGUILayout.Space(10);
    }

    private void drawNamespace(string namespaceName, NamespaceGroup namespaceInfo)
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      EditorGUILayout.BeginHorizontal();

      // Checkbox for namespace
      namespaceInfo.includeInGeneration = EditorGUILayout.Toggle(namespaceInfo.includeInGeneration, GUILayout.Width(24));

      // Namespace name
      EditorGUILayout.LabelField(namespaceName, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

      EditorGUILayout.LabelField(namespaceInfo.assemblyName, EditorStyles.miniLabel, GUILayout.Width(240));
      EditorGUILayout.EndHorizontal();

      // Summary of what's in this namespace
      int totalStates = namespaceInfo.StateCount;
      int totalActions = namespaceInfo.ActionCount;
      int totalIReducers = namespaceInfo.IReducerCount;
      int totalIMiddleware = namespaceInfo.IMiddlewareCount;

      var summaryParts = new List<string>();

      if (totalStates > 0) { summaryParts.Add($"{totalStates} states"); }
      if (totalActions > 0) { summaryParts.Add($"{totalActions} actions"); }
      if (totalIReducers > 0) { summaryParts.Add($"{totalIReducers} reducers"); }
      if (totalIMiddleware > 0) { summaryParts.Add($"{totalIMiddleware} middleware"); }
      var summary = string.Join(", ", summaryParts);

      EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

      EditorGUILayout.EndVertical();
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
                var equatableInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>));

                namespaceInfo.states.Add(new StateTypeInfo
                {
                  typeName = type.Name,
                  stateType = type,
                  fullTypeName = type.FullName,
                  namespaceName = namespaceName,
                  assemblyName = assembly.GetName().Name,
                  includeInGeneration = true,
                  implementsIEquatable = equatableInterface != null,
                  eventPriority = UIEventPriority.Normal
                });
              }

              if (isAction) {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => !f.IsStatic)
                    .Select(f => new FieldInfo
                    {
                      fieldName = f.Name,
                      fieldType = CodeGenUtils.GetFriendlyTypeName(f.FieldType),
                      isOptional = false
                    })
                    .ToList();

                namespaceInfo.actions.Add(new ActionTypeInfo
                {
                  typeName = type.Name,
                  fullTypeName = type.FullName,
                  namespaceName = namespaceName,
                  assemblyName = assembly.GetName().Name,
                  includeInGeneration = true,
                  hasFields = fields.Count > 0,
                  fields = fields
                });
              }
            }

            // Discover IReducer/IParallelReducer/IMiddleware/IParallelMiddleware types
            foreach (var type in types) {
              // Find Reducers
              var reducerAttr = type.GetCustomAttribute<ReducerAttribute>();
              if (reducerAttr != null && type.IsValueType) {
                var interfaces = type.GetInterfaces();
                foreach (var iface in interfaces) {
                  if (iface.IsGenericType) {
                    var genDef = iface.GetGenericTypeDefinition();
                    var genericArgs = iface.GetGenericArguments();

                    ReducerInfo reducerInfo = null;

                    if (genDef == typeof(IReducer<,>)) {
                      reducerInfo = new ReducerInfo
                      {
                        structType = type,
                        structName = type.Name,
                        namespaceName = type.Namespace ?? "Global",
                        stateType = genericArgs[0].Name,
                        actionType = genericArgs[1].Name,
                        dataType = null,
                        disableBurst = reducerAttr.DisableBurst,
                        order = reducerAttr.Order,
                        systemName = reducerAttr.SystemName ?? $"{type.Name}_System",
                        isParallel = false,
                        shouldGenerate = true
                      };
                    } else if (genDef == typeof(IParallelReducer<,,>)) {
                      reducerInfo = new ReducerInfo
                      {
                        structType = type,
                        structName = type.Name,
                        namespaceName = type.Namespace ?? "Global",
                        stateType = genericArgs[0].Name,
                        actionType = genericArgs[1].Name,
                        dataType = genericArgs[2].Name,
                        disableBurst = reducerAttr.DisableBurst,
                        order = reducerAttr.Order,
                        systemName = reducerAttr.SystemName ?? $"{type.Name}_System",
                        isParallel = true,
                        shouldGenerate = true
                      };
                    }

                    if (reducerInfo != null) {
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

                      discoveredNamespaces[namespaceName].reducers.Add(reducerInfo);
                      break;
                    }
                  }
                }
              }

              // Find Middleware
              var middlewareAttr = type.GetCustomAttribute<MiddlewareAttribute>();
              if (middlewareAttr != null && type.IsValueType) {
                var interfaces = type.GetInterfaces();
                foreach (var iface in interfaces) {
                  if (iface.IsGenericType) {
                    var genDef = iface.GetGenericTypeDefinition();
                    var genericArgs = iface.GetGenericArguments();

                    MiddlewareInfo middlewareInfo = null;

                    if (genDef == typeof(IMiddleware<>)) {
                      middlewareInfo = new MiddlewareInfo
                      {
                        structType = type,
                        structName = type.Name,
                        namespaceName = type.Namespace ?? "Global",
                        actionType = genericArgs[0].Name,
                        dataType = null,
                        disableBurst = middlewareAttr.DisableBurst,
                        order = middlewareAttr.Order,
                        systemName = middlewareAttr.SystemName ?? $"{type.Name}_System",
                        isParallel = false,
                        shouldGenerate = true
                      };
                    } else if (genDef == typeof(IParallelMiddleware<,>)) {
                      middlewareInfo = new MiddlewareInfo
                      {
                        structType = type,
                        structName = type.Name,
                        namespaceName = type.Namespace ?? "Global",
                        actionType = genericArgs[0].Name,
                        dataType = genericArgs[1].Name,
                        disableBurst = middlewareAttr.DisableBurst,
                        order = middlewareAttr.Order,
                        systemName = middlewareAttr.SystemName ?? $"{type.Name}_System",
                        isParallel = true,
                        shouldGenerate = true
                      };
                    }

                    if (middlewareInfo != null) {
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

                      discoveredNamespaces[namespaceName].middleware.Add(middlewareInfo);
                      break;
                    }
                  }
                }
              }
            }

          } catch (Exception ex) {
            Debug.LogWarning($"Error discovering types in assembly {assembly.GetName().Name}: {ex.Message}");
          }
        }

        hasDiscovered = true;
        int totalStates = discoveredNamespaces.Values.Sum(ns => ns.StateCount);
        int totalActions = discoveredNamespaces.Values.Sum(ns => ns.ActionCount);
        int totalIReducers = discoveredNamespaces.Values.Sum(ns => ns.IReducerCount);
        int totalIMiddleware = discoveredNamespaces.Values.Sum(ns => ns.IMiddlewareCount);

        Debug.Log($"Auto Generate All: " +
          $"Discovered {totalStates} states, " +
          $"{totalActions} actions, " +
          $"{totalIReducers} reducers, " +
          $"{totalIMiddleware} middleware " +
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
          "• ISystem Bridges (for new IReducer/IParallelReducer/IMiddleware/IParallelMiddleware)\n" +
          "• State Registry\n" +
          "• UIStateNotifier extensions\n" +
          "• StateSubscriptionHelper extensions\n" +
          "Selected namespaces:\n• " + string.Join("\n• ", selectedNamespaces.Select(ns => ns.namespaceName)) + "\n\n" +
          "Continue?",
          "Generate",
          "Cancel")) {
        return;
      }

      try {
        List<GenerationResult> results = new List<GenerationResult>();

        EditorUtility.DisplayProgressBar("Auto Generate All", "Starting generation...", 0);

        // NEW: Generate ISystem Bridges
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating ISystem Bridges...", 0.15f);
        results.Add(generateISystemBridgesForNamespaces(selectedNamespaces));

        // Generate StateRegistry
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating State Registry...", 0.3f);
        results.Add(generateStateRegistryForNamespaces(selectedNamespaces));

        // Generate UIStateNotifier
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating UIStateNotifier extensions...", 0.5f);
        results.Add(generateUIStateNotifierForNamespaces(selectedNamespaces));

        // Generate StateSubscriptionHelper
        EditorUtility.DisplayProgressBar("Auto Generate All", "Generating StateSubscriptionHelper extensions...", 0.7f);
        results.Add(generateStateSubscriptionForNamespaces(selectedNamespaces));

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

    // Generate ISystem Bridges
    private GenerationResult generateISystemBridgesForNamespaces(List<NamespaceGroup> namespaces)
    {
      try {
        var namespacesWithISystems = namespaces.Where(ns => ns.ISystemBridgeCount > 0).ToList();

        if (namespacesWithISystems.Count == 0) {
          return new GenerationResult
          {
            success = true,
            summary = "✓ ISystem Bridges: No IReducer/IMiddleware systems found"
          };
        }

        var gen = new ISystemBridgeGenerator();
        var generatedFiles = new List<string>();

        foreach (var ns in namespacesWithISystems) {
          gen.GenerateISystemBridgeCodeForNamespace(ns, ref generatedFiles);
        }

        int totalIReducers = namespacesWithISystems.Sum(ns => ns.IReducerCount);
        int totalIMiddleware = namespacesWithISystems.Sum(ns => ns.IMiddlewareCount);
        int totalISystems = totalIReducers + totalIMiddleware;

        return new GenerationResult
        {
          success = true,
          summary = $"✅ ISystem Bridges: Generated {totalISystems} ISystem implementations ({totalIReducers} reducers, {totalIMiddleware} middleware) across {namespacesWithISystems.Count} namespaces"
        };
      } catch (Exception ex) {
        Debug.LogError($"Failed to generate ISystem Bridges: {ex.Message}");
        return new GenerationResult
        {
          success = false,
          summary = $"❌ ISystem Bridges: Generation failed - {ex.Message}"
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