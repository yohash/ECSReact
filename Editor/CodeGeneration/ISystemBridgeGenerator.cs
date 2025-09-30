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
    private List<ReducerInfo> discoveredReducers = new();
    private List<MiddlewareInfo> discoveredMiddleware = new();
    private Vector2 scrollPosition;
    private bool generateXmlDocs = true;
    private bool verboseLogging = false;
    private string outputPath = Constants.DEFAULT_OUTPUT_PATH;

    [MenuItem("ECS React/ISystem Bridge Generator", priority = 204)]
    public static void ShowWindow()
    {
      var window = GetWindow<ISystemBridgeGenerator>("ECSReact ISystem Generator");
      window.minSize = new Vector2(600, 400);
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
      EditorGUILayout.Space();

      EditorGUILayout.HelpBox(
        "This tool generates ISystem implementations for your Reducer and Middleware structs.\n" +
        "• IReducer: Sequential processing with SystemState access\n" +
        "• IParallelReducer: Parallel processing with PrepareData pattern\n" +
        "• IMiddleware: Sequential with filtering capability\n" +
        "• IParallelMiddleware: Parallel transform-only processing",
        MessageType.Info);
      EditorGUILayout.Space();
    }

    private void DrawSettings()
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);
      EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

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

      generateXmlDocs = EditorGUILayout.Toggle("Generate XML Docs", generateXmlDocs);
      verboseLogging = EditorGUILayout.Toggle("Verbose Logging", verboseLogging);

      EditorGUILayout.EndVertical();
      EditorGUILayout.Space();
    }

    private void DrawDiscoveredSystems()
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);
      EditorGUILayout.LabelField($"Discovered Systems ({discoveredReducers.Count} Reducers, {discoveredMiddleware.Count} Middleware)",
        EditorStyles.boldLabel);

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(300));

      if (discoveredReducers.Count > 0) {
        EditorGUILayout.LabelField("Reducers:", EditorStyles.miniBoldLabel);
        foreach (var reducer in discoveredReducers) {
          EditorGUILayout.BeginHorizontal();
          reducer.shouldGenerate = EditorGUILayout.Toggle(reducer.shouldGenerate, GUILayout.Width(20));

          string typeLabel = reducer.isParallel ? "[Parallel]" : "[Sequential]";
          string burstLabel = reducer.disableBurst ? "[No Burst]" : "[Burst]";
          EditorGUILayout.LabelField(
            $"{typeLabel} {reducer.structName} → {reducer.stateType} + {reducer.actionType} {burstLabel}");

          if (reducer.isParallel && !string.IsNullOrEmpty(reducer.dataType)) {
            EditorGUILayout.LabelField($"(Data: {reducer.dataType})", GUILayout.Width(150));
          }
          EditorGUILayout.EndHorizontal();
        }
      }

      if (discoveredMiddleware.Count > 0) {
        EditorGUILayout.LabelField("Middleware:", EditorStyles.miniBoldLabel);
        foreach (var middleware in discoveredMiddleware) {
          EditorGUILayout.BeginHorizontal();
          middleware.shouldGenerate = EditorGUILayout.Toggle(middleware.shouldGenerate, GUILayout.Width(20));

          string typeLabel = middleware.isParallel ? "[Parallel]" : "[Sequential]";
          string burstLabel = middleware.disableBurst ? "[No Burst]" : "[Burst]";
          string filterLabel = !middleware.isParallel ? "[Can Filter]" : "[Transform Only]";
          EditorGUILayout.LabelField(
            $"{typeLabel} {middleware.structName} → {middleware.actionType} {filterLabel} {burstLabel}");

          if (middleware.isParallel && !string.IsNullOrEmpty(middleware.dataType)) {
            EditorGUILayout.LabelField($"(Data: {middleware.dataType})", GUILayout.Width(150));
          }
          EditorGUILayout.EndHorizontal();
        }
      }

      EditorGUILayout.EndScrollView();
      EditorGUILayout.EndVertical();
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
      discoveredReducers.Clear();
      discoveredMiddleware.Clear();

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

                  if (genDef == typeof(IReducer<,>)) {
                    discoveredReducers.Add(new ReducerInfo
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
                    });
                    break;
                  } else if (genDef == typeof(IParallelReducer<,,>)) {
                    // FIXED: Properly extract all three type parameters
                    discoveredReducers.Add(new ReducerInfo
                    {
                      structType = type,
                      structName = type.Name,
                      namespaceName = type.Namespace ?? "Global",
                      stateType = genericArgs[0].Name,
                      actionType = genericArgs[1].Name,
                      dataType = genericArgs[2].Name,  // Extract TData type!
                      disableBurst = reducerAttr.DisableBurst,
                      order = reducerAttr.Order,
                      systemName = reducerAttr.SystemName ?? $"{type.Name}_System",
                      isParallel = true,
                      shouldGenerate = true
                    });
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

                  if (genDef == typeof(IMiddleware<>)) {
                    discoveredMiddleware.Add(new MiddlewareInfo
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
                    });
                    break;
                  } else if (genDef == typeof(IParallelMiddleware<,>)) {
                    // FIXED: Extract TData for parallel middleware
                    discoveredMiddleware.Add(new MiddlewareInfo
                    {
                      structType = type,
                      structName = type.Name,
                      namespaceName = type.Namespace ?? "Global",
                      actionType = genericArgs[0].Name,
                      dataType = genericArgs[1].Name,  // Extract TData type!
                      disableBurst = middlewareAttr.DisableBurst,
                      order = middlewareAttr.Order,
                      systemName = middlewareAttr.SystemName ?? $"{type.Name}_System",
                      isParallel = true,
                      shouldGenerate = true
                    });
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

      if (verboseLogging)
        Debug.Log($"Found {discoveredReducers.Count} reducers and {discoveredMiddleware.Count} middleware");
    }

    private bool HasSystemsToGenerate()
    {
      return discoveredReducers.Any(r => r.shouldGenerate) ||
             discoveredMiddleware.Any(m => m.shouldGenerate);
    }

    private void GenerateSystems()
    {
      int generatedCount = 0;

      foreach (var reducer in discoveredReducers.Where(r => r.shouldGenerate)) {
        GenerateReducerSystem(reducer);
        generatedCount++;
      }

      foreach (var middleware in discoveredMiddleware.Where(m => m.shouldGenerate)) {
        GenerateMiddlewareSystem(middleware);
        generatedCount++;
      }

      AssetDatabase.Refresh();
      Debug.Log($"[ECSReact] Generated {generatedCount} ISystem implementations");

      EditorUtility.DisplayDialog("Generation Complete",
        $"Successfully generated {generatedCount} ISystem implementations.", "OK");
    }

    private void GenerateReducerSystem(ReducerInfo reducer)
    {
      var sb = new StringBuilder();

      // Header
      GenerateFileHeader(sb, reducer.structName, reducer.isParallel ? "Parallel Reducer" : "Sequential Reducer");

      // Using statements
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Burst;");
      sb.AppendLine("using Unity.Collections;");
      sb.AppendLine("using Unity.Jobs;");
      sb.AppendLine("using ECSReact.Core;");
      if (reducer.namespaceName != "ECSReact.Core")
        sb.AppendLine($"using {reducer.namespaceName};");
      sb.AppendLine();

      // Namespace
      sb.AppendLine($"namespace {reducer.namespaceName}.Generated");
      sb.AppendLine("{");

      // XML Documentation
      if (generateXmlDocs) {
        sb.AppendLine("  /// <summary>");
        sb.AppendLine($"  /// Auto-generated ISystem for {reducer.structName} reducer.");
        sb.AppendLine($"  /// Processes {reducer.actionType} actions to modify {reducer.stateType}.");
        if (reducer.isParallel)
          sb.AppendLine($"  /// Uses parallel processing with PrepareData pattern (Data type: {reducer.dataType}).");
        else
          sb.AppendLine("  /// Sequential processing with full SystemAPI access.");
        if (!reducer.disableBurst)
          sb.AppendLine("  /// Burst-compiled for maximum performance.");
        sb.AppendLine("  /// </summary>");
      }

      // System attributes
      sb.AppendLine("  [UpdateInGroup(typeof(ReducerSystemGroup))]");
      if (!reducer.disableBurst)
        sb.AppendLine("  [BurstCompile]");
      if (reducer.order != 0)
        sb.AppendLine($"  [UpdateOrder({reducer.order})]");

      // System declaration
      sb.AppendLine($"  public partial struct {reducer.systemName} : ISystem");
      sb.AppendLine("  {");

      // Fields
      sb.AppendLine($"    private {reducer.structName} logic;");
      sb.AppendLine($"    private EntityQuery actionQuery;");
      if (reducer.isParallel && !string.IsNullOrEmpty(reducer.dataType)) {
        sb.AppendLine($"    private {reducer.structName}.{reducer.dataType} preparedData;");
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
      sb.AppendLine($"      state.RequireForUpdate<{reducer.stateType}>();");
      sb.AppendLine($"      state.RequireForUpdate(actionQuery);");
      sb.AppendLine("    }");
      sb.AppendLine();

      // OnUpdate
      if (!reducer.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnUpdate(ref SystemState state)");
      sb.AppendLine("    {");
      sb.AppendLine($"      var gameState = SystemAPI.GetSingletonRW<{reducer.stateType}>();");
      sb.AppendLine();

      if (reducer.isParallel) {
        // FIXED: Parallel processing with PrepareData
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
      } else {
        // Sequential processing
        sb.AppendLine("      // Process all actions sequentially with SystemAPI access");
        sb.AppendLine($"      foreach (var (action, tag) in SystemAPI.Query<RefRO<{reducer.actionType}>, RefRO<ActionTag>>())");
        sb.AppendLine("      {");
        sb.AppendLine("        logic.Execute(ref gameState.ValueRW, in action.ValueRO, ref state);");
        sb.AppendLine("      }");
      }

      sb.AppendLine("    }");
      sb.AppendLine();

      // OnDestroy
      if (!reducer.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnDestroy(ref SystemState state) { }");

      // Parallel job if needed
      if (reducer.isParallel) {
        sb.AppendLine();
        sb.AppendLine("    // Parallel processing job");
        if (!reducer.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine("    private partial struct ProcessActionsJob : IJobEntity");
        sb.AppendLine("    {");
        sb.AppendLine($"      public RefRW<{reducer.stateType}> State;");
        sb.AppendLine($"      [ReadOnly] public {reducer.structName} Logic;");
        if (!string.IsNullOrEmpty(reducer.dataType)) {
          sb.AppendLine($"      [ReadOnly] public {reducer.structName}.{reducer.dataType} Data;");
        }
        sb.AppendLine();
        sb.AppendLine($"      public void Execute(in {reducer.actionType} action, in ActionTag tag)");
        sb.AppendLine("      {");
        // FIXED: Pass the Data parameter to Execute
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
      GenerateFileHeader(sb, middleware.structName, middleware.isParallel ? "Parallel Middleware" : "Sequential Middleware");

      // Using statements
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Burst;");
      sb.AppendLine("using Unity.Collections;");
      sb.AppendLine("using Unity.Jobs;");
      sb.AppendLine("using ECSReact.Core;");
      if (middleware.namespaceName != "ECSReact.Core")
        sb.AppendLine($"using {middleware.namespaceName};");
      sb.AppendLine();

      // Namespace
      sb.AppendLine($"namespace {middleware.namespaceName}.Generated");
      sb.AppendLine("{");

      // XML Documentation
      if (generateXmlDocs) {
        sb.AppendLine("  /// <summary>");
        sb.AppendLine($"  /// Auto-generated ISystem for {middleware.structName} middleware.");
        sb.AppendLine($"  /// Processes {middleware.actionType} actions before they reach reducers.");
        if (middleware.isParallel) {
          sb.AppendLine($"  /// Uses parallel processing with PrepareData pattern (Data type: {middleware.dataType}).");
          sb.AppendLine("  /// Transform-only: cannot filter actions.");
        } else {
          sb.AppendLine("  /// Sequential processing with full SystemAPI access.");
          sb.AppendLine("  /// Can filter actions by returning false from Process().");
          if (!middleware.disableBurst) {
            sb.AppendLine("  /// ");
            sb.AppendLine("  /// NOTE: Burst-compiled. Cannot use managed calls like ECSActionDispatcher.Dispatch().");
            sb.AppendLine("  /// If you need to dispatch actions or use managed operations, add [Middleware(DisableBurst = true)]");
          } else {
            sb.AppendLine("  /// ");
            sb.AppendLine("  /// NOTE: Burst disabled for managed operations (dispatching, logging, etc.)");
            sb.AppendLine("  /// For parallel dispatch from jobs, use ECB.DispatchAction() extension instead.");
          }
        }
        if (!middleware.disableBurst && middleware.isParallel)
          sb.AppendLine("  /// Burst-compiled for maximum performance.");
        sb.AppendLine("  /// </summary>");
      }

      // System attributes
      sb.AppendLine("  [UpdateInGroup(typeof(MiddlewareSystemGroup))]");

      // Sequential middleware: Default to NO Burst (for managed operations like dispatching)
      // Parallel middleware: Default to Burst (for performance)
      bool shouldUseBurst = middleware.isParallel ? !middleware.disableBurst : false;

      // Allow explicit opt-in for sequential middleware via DisableBurst = false
      if (!middleware.isParallel && !middleware.disableBurst) {
        // User explicitly set DisableBurst = false, they want Burst
        shouldUseBurst = true;
      }

      if (shouldUseBurst)
        sb.AppendLine("  [BurstCompile]");

      if (middleware.order != 0)
        sb.AppendLine($"  [UpdateOrder({middleware.order})]");

      // System declaration
      sb.AppendLine($"  public partial struct {middleware.systemName} : ISystem");
      sb.AppendLine("  {");

      // Fields
      sb.AppendLine($"    private {middleware.structName} logic;");
      sb.AppendLine($"    private EntityQuery actionQuery;");
      if (middleware.isParallel && !string.IsNullOrEmpty(middleware.dataType)) {
        sb.AppendLine($"    private {middleware.structName}.{middleware.dataType} preparedData;");
      }
      sb.AppendLine();

      // OnCreate
      // Always allow Burst for OnCreate (it's just setup)
      if (!middleware.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnCreate(ref SystemState state)");
      sb.AppendLine("    {");
      sb.AppendLine($"      logic = new {middleware.structName}();");
      sb.AppendLine();
      sb.AppendLine("      // Use EntityQueryBuilder for Burst compatibility");
      sb.AppendLine("      var queryBuilder = new EntityQueryBuilder(Allocator.Temp)");
      sb.AppendLine($"        .WithAllRW<{middleware.actionType}>()");
      sb.AppendLine($"        .WithAll<ActionTag>();");
      sb.AppendLine("      actionQuery = state.GetEntityQuery(queryBuilder);");
      sb.AppendLine("      queryBuilder.Dispose();");
      sb.AppendLine();
      sb.AppendLine($"      state.RequireForUpdate(actionQuery);");
      sb.AppendLine("    }");
      sb.AppendLine();

      // OnUpdate
      if (!middleware.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnUpdate(ref SystemState state)");
      sb.AppendLine("    {");

      if (middleware.isParallel) {
        // FIXED: Parallel processing with PrepareData
        sb.AppendLine("      // Prepare data from SystemAPI on main thread");
        sb.AppendLine("      preparedData = logic.PrepareData(ref state);");
        sb.AppendLine();
        sb.AppendLine("      // Schedule parallel transformation job");
        sb.AppendLine("      state.Dependency = new ProcessActionsJob");
        sb.AppendLine("      {");
        sb.AppendLine("        Logic = logic,");
        sb.AppendLine("        Data = preparedData");
        sb.AppendLine("      }.ScheduleParallel(actionQuery, state.Dependency);");
      } else {
        // FIXED: Sequential processing with filtering
        sb.AppendLine("      // Process actions sequentially with filtering capability");
        if (!middleware.disableBurst) {
          sb.AppendLine("      // Note: Cannot use ECSActionDispatcher.Dispatch() in Burst-compiled code.");
          sb.AppendLine("      // To dispatch side-effect actions, either:");
          sb.AppendLine("      //   1. Add [Middleware(DisableBurst = true)] to your middleware, OR");
          sb.AppendLine("      //   2. Use ECB to create action entities directly");
        }
        sb.AppendLine("      var ecb = new EntityCommandBuffer(Allocator.TempJob);");
        sb.AppendLine();
        sb.AppendLine($"      foreach (var (action, entity) in SystemAPI.Query<RefRW<{middleware.actionType}>>()");
        sb.AppendLine("        .WithAll<ActionTag>().WithEntityAccess())");
        sb.AppendLine("      {");
        sb.AppendLine("        // Process returns false to filter out the action");
        sb.AppendLine("        if (!logic.Process(ref action.ValueRW, ref state))");
        sb.AppendLine("        {");
        sb.AppendLine("          ecb.DestroyEntity(entity);");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      ecb.Playback(state.EntityManager);");
        sb.AppendLine("      ecb.Dispose();");
      }

      sb.AppendLine("    }");
      sb.AppendLine();

      // OnDestroy
      // Always allow Burst for cleanup
      if (!middleware.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnDestroy(ref SystemState state) { }");

      // Parallel job if needed
      if (middleware.isParallel) {
        sb.AppendLine();
        sb.AppendLine("    // Parallel transformation job (no filtering)");
        if (!middleware.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine("    private partial struct ProcessActionsJob : IJobEntity");
        sb.AppendLine("    {");
        sb.AppendLine($"      [ReadOnly] public {middleware.structName} Logic;");
        if (!string.IsNullOrEmpty(middleware.dataType)) {
          sb.AppendLine($"      [ReadOnly] public {middleware.structName}.{middleware.dataType} Data;");
        }
        sb.AppendLine();
        sb.AppendLine($"      public void Execute(ref {middleware.actionType} action, in ActionTag tag)");
        sb.AppendLine("      {");
        // FIXED: Pass the Data parameter to Process
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

    // Info classes with complete type information
    private class ReducerInfo
    {
      public Type structType;
      public string structName;
      public string namespaceName;
      public string stateType;
      public string actionType;
      public string dataType;  // For IParallelReducer<,,> third type param
      public bool disableBurst;
      public int order;
      public string systemName;
      public bool isParallel;
      public bool shouldGenerate;
    }

    private class MiddlewareInfo
    {
      public Type structType;
      public string structName;
      public string namespaceName;
      public string actionType;
      public string dataType;  // For IParallelMiddleware<,> second type param
      public bool disableBurst;
      public int order;
      public string systemName;
      public bool isParallel;
      public bool shouldGenerate;
    }
  }
}