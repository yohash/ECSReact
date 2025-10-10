using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor.CodeGeneration
{
  /// <summary>
  /// Corrected version of ISystemBridgeGenerator that properly implements all four interface types:
  /// - IReducer: Sequential with SystemState access
  /// - IParallelReducer: Two-phase with PrepareData and TData
  /// - IMiddleware: Sequential with filtering capability
  /// - IParallelMiddleware: Two-phase with PrepareData, transform-only
  /// </summary>
  public class ISystemBridgeGenerator : EditorWindow
  {
    private Dictionary<string, NamespaceGroup> namespaceGroups = new();
    private Vector2 scrollPosition;
    private bool generateXmlDocs = true;
    private bool verboseLogging = false;
    private string outputPath = Constants.DEFAULT_OUTPUT_PATH;

    [MenuItem("ECS React/ISystem Bridge Generator", priority = 204)]
    public static void ShowWindow()
    {
      var window = GetWindow<ISystemBridgeGenerator>("ISystem Code Generator");
    }

    private void OnEnable()
    {
      RefreshSystemList();
    }

    private void OnGUI()
    {
      DrawHeader();
      DrawSettings();
      DrawDiscoveredSystems();
      DrawGenerateButton();
    }

    private void DrawHeader()
    {
      EditorGUILayout.LabelField("ECSReact ISystem Generator", EditorStyles.boldLabel);
    }

    private void DrawSettings()
    {
      EditorGUILayout.BeginVertical();

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

      generateXmlDocs = EditorGUILayout.Toggle("Generate XML Documentation", generateXmlDocs);
      verboseLogging = EditorGUILayout.Toggle("Verbose Logging", verboseLogging);

      EditorGUILayout.EndVertical();
      EditorGUILayout.Space();
    }

    private void DrawDiscoveredSystems()
    {
      EditorGUILayout.LabelField("Discovered Systems", EditorStyles.boldLabel);

      if (namespaceGroups.Count == 0) {
        EditorGUILayout.HelpBox("No systems discovered. Click 'Refresh List' to scan for [Reducer] and [Middleware] types.", MessageType.Info);
        return;
      }

      int totalReducers = namespaceGroups.Values.Sum(g => g.reducers.Count);
      int totalMiddleware = namespaceGroups.Values.Sum(g => g.middleware.Count);
      EditorGUILayout.LabelField($"Found {totalReducers} reducers and {totalMiddleware} middleware across {namespaceGroups.Count} namespaces", EditorStyles.miniLabel);
      EditorGUILayout.Space();

      EditorGUILayout.BeginVertical(EditorStyles.helpBox);
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

      foreach (var kvp in namespaceGroups.OrderBy(n => n.Key)) {
        DrawNamespaceGroup(kvp.Key, kvp.Value);
        EditorGUILayout.Space(5);
      }

      EditorGUILayout.EndScrollView();
      EditorGUILayout.EndVertical();
    }

    private void DrawNamespaceGroup(string namespaceName, NamespaceGroup group)
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      // Namespace header with checkbox and expand/collapse
      EditorGUILayout.BeginHorizontal();

      // Namespace-level checkbox
      bool newIncludeNamespace = EditorGUILayout.Toggle(group.includeInGeneration, GUILayout.Width(20));
      if (newIncludeNamespace != group.includeInGeneration) {
        group.includeInGeneration = newIncludeNamespace;
        // Update all systems in this namespace
        foreach (var reducer in group.reducers) {
          reducer.shouldGenerate = newIncludeNamespace;
        }
        foreach (var middleware in group.middleware) {
          middleware.shouldGenerate = newIncludeNamespace;
        }
      }

      group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, $"  {namespaceName}", true);

      // Summary
      int totalSystems = group.reducers.Count + group.middleware.Count;
      string summary = $"({totalSystems} systems: {group.reducers.Count} reducers, {group.middleware.Count} middleware)";
      EditorGUILayout.LabelField(summary, EditorStyles.miniLabel, GUILayout.Width(350));

      EditorGUILayout.EndHorizontal();

      // Show systems in this namespace if expanded
      if (group.isExpanded) {
        EditorGUI.indentLevel++;

        string label = "";
        string state = "State";
        string action = "Action";
        string typeLabel = "Thread";
        string burstLabel = "Burst";
        string filterLabel = "Filters";

        // Draw reducers
        if (group.reducers.Count > 0) {
          EditorGUILayout.BeginHorizontal();
          label = "Reducers:";
          EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
          EditorGUILayout.LabelField("", GUILayout.Width(40)); // fill the checkbox column
          EditorGUILayout.LabelField(state, EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
          EditorGUILayout.LabelField(action, EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
          EditorGUILayout.LabelField(filterLabel, EditorStyles.miniBoldLabel, GUILayout.Width(100));
          EditorGUILayout.LabelField(typeLabel, EditorStyles.miniBoldLabel, GUILayout.Width(100));
          EditorGUILayout.LabelField(burstLabel, EditorStyles.miniBoldLabel, GUILayout.Width(100));
          EditorGUILayout.EndHorizontal();

          foreach (var reducer in group.reducers) {
            DrawReducerInfo(reducer);
          }
          EditorGUILayout.Space(5);
        }

        // Draw middleware
        if (group.middleware.Count > 0) {
          EditorGUILayout.BeginHorizontal();
          label = "Middleware:";
          EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
          EditorGUILayout.LabelField("", GUILayout.Width(40)); // fill the checkbox column
          EditorGUILayout.LabelField(state, EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
          EditorGUILayout.LabelField(action, EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
          EditorGUILayout.LabelField(filterLabel, EditorStyles.miniBoldLabel, GUILayout.Width(100));
          EditorGUILayout.LabelField(typeLabel, EditorStyles.miniBoldLabel, GUILayout.Width(100));
          EditorGUILayout.LabelField(burstLabel, EditorStyles.miniBoldLabel, GUILayout.Width(100));
          EditorGUILayout.EndHorizontal();
          foreach (var middleware in group.middleware) {
            DrawMiddlewareInfo(middleware);
          }
        }

        EditorGUI.indentLevel--;
      }

      EditorGUILayout.EndVertical();
    }

    private void DrawReducerInfo(ReducerInfo reducer)
    {
      EditorGUILayout.BeginHorizontal();
      reducer.shouldGenerate = EditorGUILayout.Toggle(reducer.shouldGenerate, GUILayout.Width(40));

      string label = $"{reducer.structName}";
      string state = $"→ {reducer.stateType} ";
      string action = $"→ {reducer.actionType}";
      string typeLabel = reducer.isParallel ? "[Parallel]" : "[Sequential]";
      string burstLabel = reducer.disableBurst ? "[No Burst]" : "[Burst]";
      string filterLabel = "";

      if (reducer.isParallel && !string.IsNullOrEmpty(reducer.dataType)) {
        label += $" + {reducer.dataType}";
      }

      EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));
      EditorGUILayout.LabelField(state, GUILayout.ExpandWidth(true));
      EditorGUILayout.LabelField(action, GUILayout.ExpandWidth(true));
      EditorGUILayout.LabelField(filterLabel, GUILayout.Width(100));
      EditorGUILayout.LabelField(typeLabel, GUILayout.Width(100));
      EditorGUILayout.LabelField(burstLabel, GUILayout.Width(100));

      EditorGUILayout.EndHorizontal();
    }

    private void DrawMiddlewareInfo(MiddlewareInfo middleware)
    {
      EditorGUILayout.BeginHorizontal();
      middleware.shouldGenerate = EditorGUILayout.Toggle(middleware.shouldGenerate, GUILayout.Width(40));

      string label = $"{middleware.structName}";
      string state = "";
      string action = $"→ {middleware.actionType}";
      string typeLabel = middleware.isParallel ? "[Parallel]" : "[Sequential]";
      string burstLabel = middleware.disableBurst ? "[No Burst]" : "[Burst]";
      string filterLabel = !middleware.isParallel ? "[Can Filter]" : "[Transform Only]";

      if (middleware.isParallel && !string.IsNullOrEmpty(middleware.dataType)) {
        label += $" + {middleware.dataType})";
      }

      EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));
      EditorGUILayout.LabelField(state, GUILayout.ExpandWidth(true));
      EditorGUILayout.LabelField(action, GUILayout.ExpandWidth(true));
      EditorGUILayout.LabelField(filterLabel, GUILayout.Width(100));
      EditorGUILayout.LabelField(typeLabel, GUILayout.Width(100));
      EditorGUILayout.LabelField(burstLabel, GUILayout.Width(100));

      EditorGUILayout.EndHorizontal();
    }

    private void DrawGenerateButton()
    {
      EditorGUILayout.Space();

      EditorGUILayout.BeginHorizontal();

      if (GUILayout.Button("Refresh List", GUILayout.Height(30))) {
        RefreshSystemList();
      }

      GUI.enabled = HasSystemsToGenerate();
      if (GUILayout.Button("Generate Selected Systems", GUILayout.Height(30))) {
        GenerateSystems();
      }
      GUI.enabled = true;

      EditorGUILayout.EndHorizontal();

      if (!HasSystemsToGenerate()) {
        EditorGUILayout.HelpBox("No systems selected for generation.", MessageType.Info);
      }
    }

    private void RefreshSystemList()
    {
      // Preserve previous selections
      var previousGroups = new Dictionary<string, NamespaceGroup>(namespaceGroups);
      namespaceGroups.Clear();

      var assemblies = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var assembly in assemblies) {
        try {
          var types = assembly.GetTypes();
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
                    // Get or create namespace group
                    if (!namespaceGroups.ContainsKey(reducerInfo.namespaceName)) {
                      namespaceGroups[reducerInfo.namespaceName] = new NamespaceGroup
                      {
                        namespaceName = reducerInfo.namespaceName,
                        includeInGeneration = reducerInfo.namespaceName == "ECSReact.Core" ? false : true,
                        isExpanded = reducerInfo.namespaceName == "ECSReact.Core" ? false : true,
                        reducers = new List<ReducerInfo>(),
                        middleware = new List<MiddlewareInfo>()
                      };
                    }

                    // Preserve previous settings if they exist
                    if (previousGroups.TryGetValue(reducerInfo.namespaceName, out var previousGroup)) {
                      var previousReducer = previousGroup.reducers.FirstOrDefault(r => r.structName == type.Name);
                      if (previousReducer != null) {
                        reducerInfo.shouldGenerate = previousReducer.shouldGenerate;
                      }

                      namespaceGroups[reducerInfo.namespaceName].includeInGeneration = previousGroup.includeInGeneration;
                      namespaceGroups[reducerInfo.namespaceName].isExpanded = previousGroup.isExpanded;
                    }

                    namespaceGroups[reducerInfo.namespaceName].reducers.Add(reducerInfo);
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
                    // Get or create namespace group
                    if (!namespaceGroups.ContainsKey(middlewareInfo.namespaceName)) {
                      namespaceGroups[middlewareInfo.namespaceName] = new NamespaceGroup
                      {
                        namespaceName = middlewareInfo.namespaceName,
                        includeInGeneration = middlewareInfo.namespaceName == "ECSReact.Core" ? false : true,
                        isExpanded = middlewareInfo.namespaceName == "ECSReact.Core" ? false : true,
                        reducers = new List<ReducerInfo>(),
                        middleware = new List<MiddlewareInfo>()
                      };
                    }

                    // Preserve previous settings if they exist
                    if (previousGroups.TryGetValue(middlewareInfo.namespaceName, out var previousGroup)) {
                      var previousMiddleware = previousGroup.middleware.FirstOrDefault(m => m.structName == type.Name);
                      if (previousMiddleware != null) {
                        middlewareInfo.shouldGenerate = previousMiddleware.shouldGenerate;
                      }

                      namespaceGroups[middlewareInfo.namespaceName].includeInGeneration = previousGroup.includeInGeneration;
                      namespaceGroups[middlewareInfo.namespaceName].isExpanded = previousGroup.isExpanded;
                    }

                    namespaceGroups[middlewareInfo.namespaceName].middleware.Add(middlewareInfo);
                    break;
                  }
                }
              }
            }
          }
        } catch (Exception ex) {
          if (verboseLogging)
            Debug.LogWarning($"Could not process assembly {assembly.FullName}: {ex.Message}");
        }
      }

      // Sort systems within each namespace
      foreach (var group in namespaceGroups.Values) {
        group.reducers = group.reducers.OrderBy(r => r.structName).ToList();
        group.middleware = group.middleware.OrderBy(m => m.structName).ToList();
      }

      int totalReducers = namespaceGroups.Values.Sum(g => g.reducers.Count);
      int totalMiddleware = namespaceGroups.Values.Sum(g => g.middleware.Count);

      if (verboseLogging)
        Debug.Log($"Found {totalReducers} reducers and {totalMiddleware} middleware across {namespaceGroups.Count} namespaces");
    }

    private bool HasSystemsToGenerate()
    {
      return namespaceGroups.Values.Any(g =>
        g.includeInGeneration &&
        (g.reducers.Any(r => r.shouldGenerate) || g.middleware.Any(m => m.shouldGenerate)));
    }

    private void GenerateSystems()
    {
      var selectedNamespaces = namespaceGroups.Values
        .Where(g => g.includeInGeneration &&
          (g.reducers.Any(r => r.shouldGenerate) || g.middleware.Any(m => m.shouldGenerate)))
        .ToList();

      if (selectedNamespaces.Count == 0) {
        EditorUtility.DisplayDialog("No Systems Selected", "Please select at least one system to generate.", "OK");
        return;
      }

      List<string> generatedFiles = new List<string>();
      int generatedCount = 0;

      foreach (var namespaceGroup in selectedNamespaces) {
        GenerateISystemBridgeCodeForNamespace(namespaceGroup, ref generatedFiles);
        generatedCount += namespaceGroup.reducers.Count(r => r.shouldGenerate);
        generatedCount += namespaceGroup.middleware.Count(m => m.shouldGenerate);
      }

      AssetDatabase.Refresh();

      string fileList = string.Join("\n• ", generatedFiles.Select(f => f.Replace(Application.dataPath, "Assets")));

      Debug.Log($"[ECSReact] Generated {generatedCount} ISystem implementations");

      EditorUtility.DisplayDialog("Generation Complete",
        $"Successfully generated {generatedCount} ISystem implementations across {selectedNamespaces.Count} namespaces.\n\n" +
        $"Files created:\n• {fileList}", "OK");
    }

    /// <summary>
    /// Public method for AutoGenerateAll to generate ISystem bridges for a specific namespace.
    /// Follows the same pattern as other generators.
    /// </summary>
    public void GenerateISystemBridgeCodeForNamespace(NamespaceGroup namespaceGroup, ref List<string> generatedFiles)
    {
      var selectedReducers = namespaceGroup.reducers.Where(r => r.shouldGenerate).ToList();
      var selectedMiddleware = namespaceGroup.middleware.Where(m => m.shouldGenerate).ToList();

      if (selectedReducers.Count == 0 && selectedMiddleware.Count == 0) {
        Debug.LogWarning($"No systems selected for generation in namespace {namespaceGroup.namespaceName}");
        return;
      }

      foreach (var reducer in selectedReducers) {
        GenerateReducerSystem(reducer);

        // Track generated file
        string namespacePath = namespaceGroup.namespaceName.Replace('.', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(outputPath, namespacePath, "Generated");
        string filePath = Path.Combine(fullPath, $"{reducer.systemName}.cs");
        generatedFiles.Add(filePath);
      }

      foreach (var middleware in selectedMiddleware) {
        GenerateMiddlewareSystem(middleware);

        // Track generated file
        string namespacePath = namespaceGroup.namespaceName.Replace('.', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(outputPath, namespacePath, "Generated");
        string filePath = Path.Combine(fullPath, $"{middleware.systemName}.cs");
        generatedFiles.Add(filePath);
      }
    }

    private void GenerateReducerSystem(ReducerInfo reducer)
    {
      var sb = new StringBuilder();

      // Header
      GenerateFileHeader(sb, reducer.structName, reducer.isParallel ? "IParallelReducer" : "IReducer");

      // Usings
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Burst;");
      sb.AppendLine("using Unity.Collections;");
      sb.AppendLine("using ECSReact.Core;");
      if (reducer.namespaceName != "ECSReact.Core")
        sb.AppendLine($"using {reducer.namespaceName};");
      sb.AppendLine();

      // Namespace
      sb.AppendLine($"namespace {reducer.namespaceName}");
      sb.AppendLine("{");

      if (generateXmlDocs) {
        sb.AppendLine("  /// <summary>");
        sb.AppendLine($"  /// Generated ISystem implementation for {reducer.structName}.");
        sb.AppendLine($"  /// Type: {(reducer.isParallel ? "IParallelReducer" : "IReducer")}");
        sb.AppendLine($"  /// State: {reducer.stateType}, Action: {reducer.actionType}");
        if (reducer.isParallel)
          sb.AppendLine($"  /// Data: {reducer.structName}.{reducer.dataType}");
        sb.AppendLine("  /// </summary>");
      }

      // System declaration
      sb.AppendLine("  [UpdateInGroup(typeof(ReducerSystemGroup))]");
      if (!reducer.disableBurst)
        sb.AppendLine("  [BurstCompile]");
      sb.AppendLine($"  public partial struct {reducer.systemName} : ISystem");
      sb.AppendLine("  {");
      sb.AppendLine($"    private {reducer.structName} logic;");
      sb.AppendLine("    private EntityQuery actionQuery;");

      if (reducer.isParallel) {
        sb.AppendLine($"    private {reducer.structName}.{reducer.dataType} preparedData;");
      } else {
        // Sequential reducers need ComponentLookup field
        sb.AppendLine($"    private ComponentLookup<{reducer.actionType}> actionLookup;");
      }
      sb.AppendLine();

      // OnCreate
      if (!reducer.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnCreate(ref SystemState state)");
      sb.AppendLine("    {");
      sb.AppendLine($"      logic = new {reducer.structName}();");
      sb.AppendLine();
      sb.AppendLine("      // Use EntityQueryBuilder for Burst compatibility");
      sb.AppendLine("      var queryBuilder = new EntityQueryBuilder(Allocator.Temp)");
      sb.AppendLine($"        .WithAll<{reducer.actionType}, ActionTag>();");
      sb.AppendLine("      actionQuery = state.GetEntityQuery(queryBuilder);");
      sb.AppendLine("      queryBuilder.Dispose();");
      sb.AppendLine();

      if (!reducer.isParallel) {
        // Sequential reducers: create ComponentLookup once in OnCreate
        sb.AppendLine("      // Create ComponentLookup once for reuse");
        sb.AppendLine($"      actionLookup = state.GetComponentLookup<{reducer.actionType}>(isReadOnly: true);");
        sb.AppendLine();
      }

      sb.AppendLine($"      state.RequireForUpdate<{reducer.stateType}>();");
      sb.AppendLine($"      state.RequireForUpdate(actionQuery);");
      sb.AppendLine("    }");
      sb.AppendLine();

      // OnUpdate
      sb.AppendLine("    public void OnUpdate(ref SystemState state)");
      sb.AppendLine("    {");
      sb.AppendLine($"      var gameState = SystemAPI.GetSingletonRW<{reducer.stateType}>();");
      sb.AppendLine();

      if (reducer.isParallel) {
        // Parallel processing with PrepareData
        sb.AppendLine("      // Prepare data from SystemAPI on main thread");
        sb.AppendLine("      preparedData = logic.PrepareData(ref state);");
        sb.AppendLine();
        sb.AppendLine("      // Schedule parallel job with prepared data");
        sb.AppendLine("      state.Dependency = new ProcessActionsJob");
        sb.AppendLine("      {");
        sb.AppendLine("        State = gameState,");
        sb.AppendLine("        Logic = logic,");
        sb.AppendLine("        Data = preparedData");
        sb.AppendLine("      }.ScheduleParallel(actionQuery, state.Dependency);");
        sb.AppendLine("    }");
      } else {
        // Sequential processing with ComponentLookup
        sb.AppendLine("      // Update ComponentLookup to latest data");
        sb.AppendLine("      actionLookup.Update(ref state);");
        sb.AppendLine("      ");
        sb.AppendLine("      // Call Burst-compiled processing");
        sb.AppendLine("      ProcessActions(ref state, gameState, actionLookup);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Add Burst-compiled helper method
        if (!reducer.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine("    private void ProcessActions(");
        sb.AppendLine("      ref SystemState state,");
        sb.AppendLine($"      RefRW<{reducer.stateType}> gameState,");
        sb.AppendLine($"      ComponentLookup<{reducer.actionType}> actionLookup)");
        sb.AppendLine("    {");
        sb.AppendLine("      // Process all actions sequentially - uses cached query");
        sb.AppendLine("      var entities = actionQuery.ToEntityArray(Allocator.Temp);");
        sb.AppendLine();
        sb.AppendLine("      // ✅ Check once per frame if component is zero-sized");
        sb.AppendLine($"      var type = new ComponentType(typeof({reducer.actionType}));");
        sb.AppendLine("      bool isZeroSized = type.IsZeroSized;");
        sb.AppendLine();
        sb.AppendLine("      foreach (var entity in entities)");
        sb.AppendLine("      {");
        sb.AppendLine("        if (isZeroSized) {");
        sb.AppendLine("          // ✅ Zero-sized: use default value");
        sb.AppendLine($"          var action = default({reducer.actionType});");
        sb.AppendLine("          logic.Execute(ref gameState.ValueRW, in action, ref state);");
        sb.AppendLine("        } else {");
        sb.AppendLine("          var action =  actionLookup.GetRefRO(entity);");
        sb.AppendLine("          logic.Execute(ref gameState.ValueRW, in action.ValueRO, ref state);");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("      ");
        sb.AppendLine("      entities.Dispose();");
        sb.AppendLine("    }");
      }

      sb.AppendLine();

      // Job struct for parallel processing
      if (reducer.isParallel) {
        if (!reducer.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine($"    private partial struct ProcessActionsJob : IJobEntity");
        sb.AppendLine("    {");
        sb.AppendLine($"      public RefRW<{reducer.stateType}> State;");
        sb.AppendLine($"      [ReadOnly] public {reducer.structName} Logic;");
        sb.AppendLine($"      [ReadOnly] public {reducer.structName}.{reducer.dataType} Data;");
        sb.AppendLine();
        sb.AppendLine($"      public void Execute(in {reducer.actionType} action, in ActionTag tag)");
        sb.AppendLine("      {");
        sb.AppendLine("        // Parallel execution with prepared data");
        sb.AppendLine("        Logic.Execute(ref State.ValueRW, in action, in Data);");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
      }

      sb.AppendLine("  }");
      sb.AppendLine("}");

      WriteGeneratedFile(reducer.namespaceName, reducer.systemName, sb.ToString());
    }

    private void GenerateMiddlewareSystem(MiddlewareInfo middleware)
    {
      var sb = new StringBuilder();

      // Header
      GenerateFileHeader(sb, middleware.structName, middleware.isParallel ? "IParallelMiddleware" : "IMiddleware");

      // Usings
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Burst;");
      sb.AppendLine("using Unity.Collections;");
      sb.AppendLine("using ECSReact.Core;");
      if (middleware.namespaceName != "ECSReact.Core")
        sb.AppendLine($"using {middleware.namespaceName};");
      sb.AppendLine();

      // Namespace
      sb.AppendLine($"namespace {middleware.namespaceName}");
      sb.AppendLine("{");

      if (generateXmlDocs) {
        sb.AppendLine("  /// <summary>");
        sb.AppendLine($"  /// Generated ISystem implementation for {middleware.structName}.");
        sb.AppendLine($"  /// Type: {(middleware.isParallel ? "IParallelMiddleware" : "IMiddleware")}");
        sb.AppendLine($"  /// Action: {middleware.actionType}");
        if (middleware.isParallel)
          sb.AppendLine($"  /// Data: {middleware.structName}.{middleware.dataType}");
        sb.AppendLine($"  /// {(middleware.isParallel ? "Transform-only (cannot filter)" : "Can filter actions")}");
        sb.AppendLine("  /// </summary>");
      }

      // System declaration
      sb.AppendLine("  [UpdateInGroup(typeof(MiddlewareSystemGroup))]");
      if (!middleware.disableBurst)
        sb.AppendLine("  [BurstCompile]");  // System-level Burst (for OnCreate)
      sb.AppendLine($"  public partial struct {middleware.systemName} : ISystem");
      sb.AppendLine("  {");
      sb.AppendLine($"    private {middleware.structName} logic;");
      sb.AppendLine("    private EntityQuery actionQuery;");

      if (middleware.isParallel) {
        // Parallel middleware - existing implementation (unchanged)
        sb.AppendLine($"    private {middleware.structName}.{middleware.dataType} preparedData;");
        sb.AppendLine();

        // OnCreate
        if (!middleware.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine("    public void OnCreate(ref SystemState state)");
        sb.AppendLine("    {");
        sb.AppendLine($"      logic = new {middleware.structName}();");
        sb.AppendLine();
        sb.AppendLine("      // Use EntityQueryBuilder for Burst compatibility");
        sb.AppendLine("      var queryBuilder = new EntityQueryBuilder(Allocator.Temp)");
        sb.AppendLine($"        .WithAll<{middleware.actionType}, ActionTag>();");
        sb.AppendLine("      actionQuery = state.GetEntityQuery(queryBuilder);");
        sb.AppendLine("      queryBuilder.Dispose();");
        sb.AppendLine();
        sb.AppendLine("      // Create ComponentLookup once for reuse");
        sb.AppendLine($"      actionLookup = state.GetComponentLookup<{middleware.actionType}>(isReadOnly: false);");
        sb.AppendLine();
        sb.AppendLine($"      state.RequireForUpdate(actionQuery);");
        sb.AppendLine($"      state.RequireForUpdate<{middleware.actionType}>();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // OnUpdate for parallel
        sb.AppendLine("    public void OnUpdate(ref SystemState state)");
        sb.AppendLine("    {");
        sb.AppendLine("      // Prepare data from SystemAPI on main thread");
        sb.AppendLine("      preparedData = logic.PrepareData(ref state);");
        sb.AppendLine();
        sb.AppendLine("      // Schedule parallel job - transform only, cannot filter");
        sb.AppendLine("      state.Dependency = new ProcessActionsJob");
        sb.AppendLine("      {");
        sb.AppendLine("        Logic = logic,");
        sb.AppendLine("        Data = preparedData");
        sb.AppendLine("      }.ScheduleParallel(actionQuery, state.Dependency);");
        sb.AppendLine("    }");
      } else {
        // Sequential middleware with filtering capability
        sb.AppendLine();
        sb.AppendLine("    // ECB writer for Burst-compatible action dispatching");
        sb.AppendLine("    private EntityCommandBuffer.ParallelWriter ecbWriter;");
        sb.AppendLine($"    private ComponentLookup<{middleware.actionType}> actionLookup;");
        sb.AppendLine();

        // OnCreate
        if (!middleware.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine("    public void OnCreate(ref SystemState state)");
        sb.AppendLine("    {");
        sb.AppendLine($"      logic = new {middleware.structName}();");
        sb.AppendLine();
        sb.AppendLine("      // Use EntityQueryBuilder for Burst compatibility");
        sb.AppendLine("      var queryBuilder = new EntityQueryBuilder(Allocator.Temp)");
        sb.AppendLine($"        .WithAll<{middleware.actionType}, ActionTag>();");
        sb.AppendLine("      actionQuery = state.GetEntityQuery(queryBuilder);");
        sb.AppendLine("      queryBuilder.Dispose();");
        sb.AppendLine();
        sb.AppendLine($"      actionLookup = state.GetComponentLookup<{middleware.actionType}>(isReadOnly: false);");
        sb.AppendLine($"      state.RequireForUpdate(actionQuery);");
        sb.AppendLine($"      state.RequireForUpdate<{middleware.actionType}>();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // OnUpdate - Create lookup on main thread, call Burst helper
        sb.AppendLine("    public void OnUpdate(ref SystemState state)");
        sb.AppendLine("    {");
        sb.AppendLine("      // Pre-fetch ECB writer on main thread");
        sb.AppendLine("      ecbWriter = ECSActionDispatcher.GetJobCommandBuffer(state.World);");
        sb.AppendLine();
        sb.AppendLine("      // Update ComponentLookup to latest data");
        sb.AppendLine("      actionLookup.Update(ref state);");
        sb.AppendLine();
        sb.AppendLine("      // Call Burst-compiled middleware processing");
        sb.AppendLine("      ProcessMiddleware(ref state, actionLookup);");
        sb.AppendLine();
        sb.AppendLine("      // Register job handle for proper synchronization");
        sb.AppendLine("      ECSActionDispatcher.RegisterJobHandle(state.Dependency, state.World);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ProcessMiddleware - Burst compiled helper
        if (!middleware.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine("    private void ProcessMiddleware(");
        sb.AppendLine("      ref SystemState state,");
        sb.AppendLine($"      ComponentLookup<{middleware.actionType}> actionLookup)");
        sb.AppendLine("    {");
        sb.AppendLine("      // ECB for filtering actions (destroy entity if filtered)");
        sb.AppendLine("      var ecb = new EntityCommandBuffer(Allocator.TempJob);");
        sb.AppendLine();
        sb.AppendLine("      // Process all actions sequentially - uses cached query");
        sb.AppendLine("      var entities = actionQuery.ToEntityArray(Allocator.Temp);");
        sb.AppendLine("      int sortKey = 0;");
        sb.AppendLine("      ");
        sb.AppendLine("      // Check if action is zero-sized (no fields)");
        sb.AppendLine($"      var type = new ComponentType(typeof({middleware.actionType}));");
        sb.AppendLine($"      bool isZeroSized = type.IsZeroSized;");
        sb.AppendLine("      ");
        sb.AppendLine("      foreach (var entity in entities)");
        sb.AppendLine("      {");
        sb.AppendLine("        bool shouldContinue;");
        sb.AppendLine("        ");
        sb.AppendLine("        if (isZeroSized)");
        sb.AppendLine("        {");
        sb.AppendLine("          // Zero-sized components: use default value");
        sb.AppendLine($"          var action = default({middleware.actionType});");
        sb.AppendLine("          shouldContinue = logic.Process(");
        sb.AppendLine("            ref action,");
        sb.AppendLine("            ref state,");
        sb.AppendLine("            ecbWriter,");
        sb.AppendLine("            sortKey");
        sb.AppendLine("          );");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("          // Normal components: use ComponentLookup");
        sb.AppendLine("          var action =  actionLookup.GetRefRW(entity);");
        sb.AppendLine("          shouldContinue = logic.Process(");
        sb.AppendLine("            ref action.ValueRW,");
        sb.AppendLine("            ref state,");
        sb.AppendLine("            ecbWriter,");
        sb.AppendLine("            sortKey");
        sb.AppendLine("          );");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!shouldContinue)");
        sb.AppendLine("        {");
        sb.AppendLine("          // Middleware filtered this action - destroy it");
        sb.AppendLine("          ecb.DestroyEntity(entity);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        sortKey++;");
        sb.AppendLine("      }");
        sb.AppendLine("      ");
        sb.AppendLine("      entities.Dispose();");
        sb.AppendLine("      ecb.Playback(state.EntityManager);");
        sb.AppendLine("      ecb.Dispose();");
        sb.AppendLine("    }");
      }

      sb.AppendLine();

      // OnDestroy
      sb.AppendLine("    public void OnDestroy(ref SystemState state) { }");
      sb.AppendLine();

      // Job struct for parallel processing (if parallel middleware)
      if (middleware.isParallel) {
        if (!middleware.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine($"    private partial struct ProcessActionsJob : IJobEntity");
        sb.AppendLine("    {");
        sb.AppendLine($"      [ReadOnly] public {middleware.structName} Logic;");
        sb.AppendLine($"      [ReadOnly] public {middleware.structName}.{middleware.dataType} Data;");
        sb.AppendLine();
        sb.AppendLine($"      public void Execute(ref {middleware.actionType} action, in ActionTag tag)");
        sb.AppendLine("      {");
        sb.AppendLine("        // Transform only - parallel middleware cannot filter");
        sb.AppendLine("        Logic.Process(ref action, in Data);");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
      }

      sb.AppendLine("  }");
      sb.AppendLine("}");

      WriteGeneratedFile(middleware.namespaceName, middleware.systemName, sb.ToString());
    }

    private void GenerateFileHeader(StringBuilder sb, string sourceName, string systemType)
    {
      sb.AppendLine("// <auto-generated>");
      sb.AppendLine($"// Generated by ECSReact ISystem Bridge Generator");
      sb.AppendLine($"// Source: {sourceName}");
      sb.AppendLine($"// Type: {systemType}");
      sb.AppendLine($"// Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
      sb.AppendLine("// Do not modify this file directly - changes will be lost on regeneration");
      sb.AppendLine("// </auto-generated>");
      sb.AppendLine();
    }

    private void WriteGeneratedFile(string namespaceName, string systemName, string content)
    {
      string namespacePath = namespaceName.Replace('.', Path.DirectorySeparatorChar);
      string fullPath = Path.Combine(outputPath, namespacePath, "Generated");

      if (!Directory.Exists(fullPath)) {
        Directory.CreateDirectory(fullPath);
      }

      string filePath = Path.Combine(fullPath, $"{systemName}.cs");
      File.WriteAllText(filePath, content);

      if (verboseLogging)
        Debug.Log($"Generated: {filePath}");
    }
  }
}