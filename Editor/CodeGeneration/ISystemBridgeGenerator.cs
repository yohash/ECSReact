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
  /// Generates ISystem implementations from Reducer and Middleware structs.
  /// This eliminates boilerplate and ensures consistent, optimized system generation.
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

      // Options
      EditorGUILayout.BeginHorizontal();
      generateXmlDocs = EditorGUILayout.Toggle("", generateXmlDocs, GUILayout.Width(30));
      EditorGUILayout.LabelField("Generate XML Documentation");
      EditorGUILayout.EndHorizontal();


      EditorGUILayout.BeginHorizontal();
      verboseLogging = EditorGUILayout.Toggle("", verboseLogging, GUILayout.Width(30));
      EditorGUILayout.LabelField("Verbose Logging");
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.EndVertical();

      // Discovery controls
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Discover Action Types")) {
        RefreshSystemList();
      }
      if (GUILayout.Button("Clear Discovery")) {
        discoveredReducers.Clear();
        discoveredMiddleware.Clear();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();
    }

    private void DrawDiscoveredSystems()
    {
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

      if (discoveredReducers.Count > 0) {
        EditorGUILayout.LabelField($"Reducers ({discoveredReducers.Count})", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Draw Reducers
        foreach (var reducer in discoveredReducers) {
          EditorGUILayout.BeginHorizontal();
          reducer.shouldGenerate = EditorGUILayout.Toggle(reducer.shouldGenerate, GUILayout.Width(20));

          var burstStatus = reducer.disableBurst ? " [No Burst]" : " [Burst]";
          var parallelStatus = reducer.isParallel ? " [Parallel]" : " [Sequential]";
          EditorGUILayout.LabelField($"{reducer.structName}{burstStatus}{parallelStatus}", EditorStyles.miniLabel);
          EditorGUILayout.LabelField($"→ {reducer.stateType} × {reducer.actionType}", EditorStyles.miniLabel);
          EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
      }

      EditorGUILayout.Space();

      // Draw Middleware
      if (discoveredMiddleware.Count > 0) {
        EditorGUILayout.LabelField($"Middleware ({discoveredMiddleware.Count})", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        foreach (var middleware in discoveredMiddleware) {
          EditorGUILayout.BeginHorizontal();
          middleware.shouldGenerate = EditorGUILayout.Toggle(middleware.shouldGenerate, GUILayout.Width(20));

          var burstStatus = middleware.disableBurst ? " [No Burst]" : " [Burst]";
          var parallelStatus = middleware.isParallel ? " [Parallel]" : " [Sequential]";
          EditorGUILayout.LabelField($"{middleware.structName}{burstStatus}{parallelStatus}", EditorStyles.miniLabel);
          EditorGUILayout.LabelField($"→ {middleware.actionType}", EditorStyles.miniLabel);
          EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
      }

      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space();
    }

    private void DrawGenerateButton()
    {
      EditorGUI.BeginDisabledGroup(!HasSystemsToGenerate());
      if (GUILayout.Button("Generate ISystems", GUILayout.Height(30))) {
        GenerateSystems();
      }
      EditorGUI.EndDisabledGroup();

      if (!HasSystemsToGenerate()) {
        EditorGUILayout.HelpBox("No systems found to generate. Make sure you have structs with [Reducer] or [Middleware] attributes.", MessageType.Info);
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
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IReducer<,>)) {
                  var genericArgs = iface.GetGenericArguments();
                  discoveredReducers.Add(new ReducerInfo
                  {
                    structType = type,
                    structName = type.Name,
                    namespaceName = type.Namespace,
                    stateType = genericArgs[0].Name,
                    actionType = genericArgs[1].Name,
                    disableBurst = reducerAttr.DisableBurst,
                    order = reducerAttr.Order,
                    systemName = reducerAttr.SystemName ?? $"{type.Name}_System",
                    isParallel = false,
                    shouldGenerate = true
                  });
                  break;
                } else if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IParallelReducer<,,>)) {
                  var genericArgs = iface.GetGenericArguments();
                  discoveredReducers.Add(new ReducerInfo
                  {
                    structType = type,
                    structName = type.Name,
                    namespaceName = type.Namespace,
                    stateType = genericArgs[0].Name,
                    actionType = genericArgs[1].Name,
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

            // Find Middleware
            var middlewareAttr = type.GetCustomAttribute<MiddlewareAttribute>();
            if (middlewareAttr != null && type.IsValueType) {
              var interfaces = type.GetInterfaces();
              foreach (var iface in interfaces) {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IMiddleware<>)) {
                  var genericArgs = iface.GetGenericArguments();
                  discoveredMiddleware.Add(new MiddlewareInfo
                  {
                    structType = type,
                    structName = type.Name,
                    namespaceName = type.Namespace,
                    actionType = genericArgs[0].Name,
                    disableBurst = middlewareAttr.DisableBurst,
                    order = middlewareAttr.Order,
                    systemName = middlewareAttr.SystemName ?? $"{type.Name}_System",
                    isParallel = false,
                    shouldGenerate = true
                  });
                  break;
                } else if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IParallelMiddleware<,>)) {
                  var genericArgs = iface.GetGenericArguments();
                  discoveredMiddleware.Add(new MiddlewareInfo
                  {
                    structType = type,
                    structName = type.Name,
                    namespaceName = type.Namespace,
                    actionType = genericArgs[0].Name,
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

      // Generate Reducer Systems
      foreach (var reducer in discoveredReducers.Where(r => r.shouldGenerate)) {
        GenerateReducerSystem(reducer);
        generatedCount++;
      }

      // Generate Middleware Systems
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
      GenerateFileHeader(sb, reducer.structName, "Reducer");

      // Using statements
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Burst;");
      sb.AppendLine("using Unity.Collections;");
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
        if (!reducer.disableBurst)
          sb.AppendLine("  /// Burst-compiled for maximum performance.");
        sb.AppendLine("  /// </summary>");
      }

      // System attributes
      sb.AppendLine("  [ReducerUpdateGroup]");
      if (!reducer.disableBurst)
        sb.AppendLine("  [BurstCompile]");
      if (reducer.order != 0)
        sb.AppendLine($"  [UpdateOrder({reducer.order})]");

      // System declaration
      sb.AppendLine($"  public partial struct {reducer.systemName} : ISystem");
      sb.AppendLine("  {");

      // Fields
      sb.AppendLine($"    private {reducer.structName} logic;");
      if (reducer.isParallel && reducer.dataType != null) {
        sb.AppendLine($"    private {reducer.structName}.{reducer.dataType} preparedData;");
      }
      sb.AppendLine();

      // OnCreate
      if (!reducer.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnCreate(ref SystemState state)");
      sb.AppendLine("    {");
      sb.AppendLine($"      state.RequireForUpdate<{reducer.stateType}>();");
      sb.AppendLine($"      state.RequireForUpdate<{reducer.actionType}>();");
      sb.AppendLine($"      logic = new {reducer.structName}();");
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
        // Parallel processing with IJobEntity
        sb.AppendLine("      // Parallel processing for better performance with many actions");
        sb.AppendLine("      // Note: Parallel jobs cannot pass SystemState directly");
        sb.AppendLine("      // Consider using sequential processing if SystemAPI access is critical");
        sb.AppendLine($"      new ProcessActionsJob");
        sb.AppendLine("      {");
        sb.AppendLine("        State = gameState,");
        sb.AppendLine("        Logic = logic");
        sb.AppendLine("      }.Schedule();");
      } else {
        // Sequential processing
        sb.AppendLine("      // Process all actions sequentially");
        sb.AppendLine($"      foreach (var action in SystemAPI.Query<RefRO<{reducer.actionType}>>()");
        sb.AppendLine("        .WithAll<ActionTag>())");
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
        sb.AppendLine();
        sb.AppendLine($"      public void Execute(in {reducer.actionType} action, in ActionTag tag)");
        sb.AppendLine("      {");
        sb.AppendLine("        Logic.Execute(ref State.ValueRW, in action);");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
      }

      sb.AppendLine("  }");
      sb.AppendLine("}");

      // Write file
      WriteGeneratedFile(reducer.namespaceName, reducer.systemName, sb.ToString());
    }

    private void GenerateMiddlewareSystem(MiddlewareInfo middleware)
    {
      var sb = new StringBuilder();

      // Header
      GenerateFileHeader(sb, middleware.structName, "Middleware");

      // Using statements
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Burst;");
      sb.AppendLine("using Unity.Collections;");
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
        if (middleware.isParallel)
          sb.AppendLine("  /// Uses parallel processing for maximum performance (transform only, no filtering).");
        else
          sb.AppendLine("  /// Sequential processing with full SystemAPI access and filtering capability.");
        if (!middleware.disableBurst)
          sb.AppendLine("  /// Burst-compiled for maximum performance.");
        sb.AppendLine("  /// </summary>");
      }

      // System attributes
      sb.AppendLine("  [MiddlewareUpdateGroup]");
      if (!middleware.disableBurst)
        sb.AppendLine("  [BurstCompile]");
      if (middleware.order != 0)
        sb.AppendLine($"  [UpdateOrder({middleware.order})]");

      // System declaration
      sb.AppendLine($"  public partial struct {middleware.systemName} : ISystem");
      sb.AppendLine("  {");

      // Fields
      sb.AppendLine($"    private {middleware.structName} logic;");
      if (middleware.isParallel && middleware.dataType != null) {
        sb.AppendLine($"    private {middleware.structName}.{middleware.dataType} preparedData;");
      }
      sb.AppendLine();

      // OnCreate
      if (!middleware.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnCreate(ref SystemState state)");
      sb.AppendLine("    {");
      sb.AppendLine($"      state.RequireForUpdate<{middleware.actionType}>();");
      sb.AppendLine($"      logic = new {middleware.structName}();");
      sb.AppendLine("    }");
      sb.AppendLine();

      // OnUpdate
      if (!middleware.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnUpdate(ref SystemState state)");
      sb.AppendLine("    {");

      if (middleware.isParallel) {
        // Parallel processing - transform only, no filtering
        sb.AppendLine("      // Prepare data from SystemAPI on main thread");
        sb.AppendLine("      preparedData = logic.PrepareData(ref state);");
        sb.AppendLine();
        sb.AppendLine("      // Parallel transformation (no filtering)");
        sb.AppendLine($"      new ProcessActionsJob");
        sb.AppendLine("      {");
        sb.AppendLine("        Logic = logic,");
        sb.AppendLine("        Data = preparedData");
        sb.AppendLine("      }.ScheduleParallel();");
      } else {
        // Sequential processing with filtering via EntityManager
        sb.AppendLine("      var entitiesToDestroy = new NativeList<Entity>(Allocator.Temp);");
        sb.AppendLine();
        sb.AppendLine("      // Process all actions through middleware");
        sb.AppendLine($"      foreach (var (action, entity) in SystemAPI.Query<RefRW<{middleware.actionType}>>()");
        sb.AppendLine("        .WithAll<ActionTag>()");
        sb.AppendLine("        .WithEntityAccess())");
        sb.AppendLine("      {");
        sb.AppendLine("        // Process returns false if action should be filtered");
        sb.AppendLine("        bool shouldContinue = logic.Process(ref action.ValueRW, ref state);");
        sb.AppendLine();
        sb.AppendLine("        if (!shouldContinue)");
        sb.AppendLine("        {");
        sb.AppendLine("          entitiesToDestroy.Add(entity);");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      // Immediately destroy filtered actions using EntityManager");
        sb.AppendLine("      foreach (var entity in entitiesToDestroy)");
        sb.AppendLine("      {");
        sb.AppendLine("        state.EntityManager.DestroyEntity(entity);");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      entitiesToDestroy.Dispose();");
      }

      sb.AppendLine("    }");
      sb.AppendLine();

      // OnDestroy
      if (!middleware.disableBurst)
        sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    public void OnDestroy(ref SystemState state) { }");

      // Parallel job if needed
      if (middleware.isParallel) {
        sb.AppendLine();
        sb.AppendLine("    // Parallel processing job (transform only, no filtering)");
        if (!middleware.disableBurst)
          sb.AppendLine("    [BurstCompile]");
        sb.AppendLine("    private partial struct ProcessActionsJob : IJobEntity");
        sb.AppendLine("    {");
        sb.AppendLine($"      [ReadOnly] public {middleware.structName} Logic;");
        sb.AppendLine($"      [ReadOnly] public {middleware.structName}.{middleware.dataType} Data;");
        sb.AppendLine();
        sb.AppendLine("      [BurstCompile]");
        sb.AppendLine($"      public void Execute(ref {middleware.actionType} action, in ActionTag tag)");
        sb.AppendLine("      {");
        sb.AppendLine("        // Transform only - parallel middleware cannot filter");
        sb.AppendLine("        Logic.Process(ref action, in Data);");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
      }

      sb.AppendLine("  }");
      sb.AppendLine("}");

      // Write file
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
      // Create directory structure
      string namespacePath = namespaceName.Replace('.', '/');
      string fullPath = Path.Combine(outputPath, namespacePath, "Bridges");

      if (!Directory.Exists(fullPath)) {
        Directory.CreateDirectory(fullPath);
      }

      // Write file
      string filePath = Path.Combine(fullPath, $"{systemName}.Generated.cs");
      File.WriteAllText(filePath, content);

      if (verboseLogging)
        Debug.Log($"Generated: {filePath}");
    }

    // Info classes
    private class ReducerInfo
    {
      public Type structType;
      public string structName;
      public string namespaceName;
      public string stateType;
      public string actionType;
      public string dataType; // For parallel reducers
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
      public string dataType; // For parallel middleware
      public bool disableBurst;
      public int order;
      public string systemName;
      public bool isParallel;
      public bool shouldGenerate;
    }
  }
}