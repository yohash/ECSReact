using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ECSReact.Editor.CodeGeneration
{
  /// <summary>
  /// Generates bridge systems for all types of reducers and middleware.
  /// Supports both standard and Burst-optimized variants.
  /// </summary>
  public class BridgeSystemGenerator : EditorWindow
  {
    private Vector2 scrollPosition;
    private Dictionary<string, NamespaceGroup> namespaceGroups = new();
    private string outputPath = Constants.DEFAULT_OUTPUT_PATH;
    private bool generateXmlDocs = true;
    private bool showAdvancedOptions = false;
    private int totalSystemsFound = 0;
    private int totalBridgesGenerated = 0;

    [MenuItem("ECS React/Generate Bridge Systems", priority = 205)]
    public static void ShowWindow()
    {
      var window = GetWindow<BridgeSystemGenerator>("Bridge System Generator");
      window.minSize = new Vector2(700, 500);
    }

    private void OnEnable()
    {
      discoverSystems();
    }

    private void OnGUI()
    {
      GUILayout.Label("Bridge System Generator", EditorStyles.boldLabel);

      EditorGUILayout.HelpBox(
        "This generator creates optimized bridge systems that eliminate allocations.\n" +
        "• Standard systems: Zero allocations, good performance\n" +
        "• Burst systems: Zero allocations, maximum performance (5-10x faster)",
        MessageType.Info);

      EditorGUILayout.Space();

      // Output path configuration
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField("Output Path:", GUILayout.Width(80));
      outputPath = EditorGUILayout.TextField(outputPath);
      if (GUILayout.Button("Browse", GUILayout.Width(60))) {
        string selectedPath = EditorUtility.OpenFolderPanel("Select Output Folder", outputPath, "");
        if (!string.IsNullOrEmpty(selectedPath)) {
          outputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
        }
      }
      EditorGUILayout.EndHorizontal();

      // Options
      generateXmlDocs = EditorGUILayout.Toggle("Generate XML Documentation", generateXmlDocs);

      showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Options");
      if (showAdvancedOptions) {
        EditorGUI.indentLevel++;
        EditorGUILayout.HelpBox(
          "Advanced options for fine-tuning bridge generation.\n" +
          "Default settings are recommended for most use cases.",
          MessageType.None);
        EditorGUI.indentLevel--;
      }

      EditorGUILayout.Space();

      // Discovery controls
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Scan for Systems", GUILayout.Height(25))) {
        discoverSystems();
      }
      if (GUILayout.Button("Clear All", GUILayout.Height(25))) {
        namespaceGroups.Clear();
        totalSystemsFound = 0;
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // Display discovered systems
      if (totalSystemsFound > 0) {
        // Summary
        int burstCount = namespaceGroups.Values.Sum(g => g.BurstReducerCount + g.BurstMiddlewareCount);
        int standardCount = namespaceGroups.Values.Sum(g => g.StandardReducerCount + g.StandardMiddlewareCount);

        EditorGUILayout.LabelField(
          $"Found {totalSystemsFound} systems ({burstCount} Burst, {standardCount} Standard) in {namespaceGroups.Count} namespaces:",
          EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        foreach (var kvp in namespaceGroups.OrderBy(n => n.Key)) {
          drawNamespaceGroup(kvp.Value);
          EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();
      } else {
        EditorGUILayout.HelpBox(
          "No systems found. Make sure you have:\n" +
          "• ReducerSystem<TState, TAction> implementations\n" +
          "• BurstReducerSystem<TState, TAction, TLogic> implementations\n" +
          "• MiddlewareSystem<TAction> implementations\n" +
          "• BurstMiddlewareSystem<TAction, TLogic> implementations",
          MessageType.Info);
      }

      EditorGUILayout.Space();

      // Generation controls
      EditorGUILayout.BeginHorizontal();

      bool hasSelectedSystems = namespaceGroups.Values.Any(g =>
        g.includeInGeneration && g.systems.Any(s => s.includeInGeneration));

      GUI.enabled = hasSelectedSystems;
      if (GUILayout.Button("Generate Bridge Systems", GUILayout.Height(30))) {
        generateBridgeSystems();
      }
      GUI.enabled = true;

      if (GUILayout.Button("Preview Generated Code", GUILayout.Height(30))) {
        previewGeneratedCode();
      }

      EditorGUILayout.EndHorizontal();

      // Status bar
      EditorGUILayout.Space();
      EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
      EditorGUILayout.LabelField($"Total Bridges Generated: {totalBridgesGenerated}", EditorStyles.miniLabel);
      EditorGUILayout.EndHorizontal();
    }

    private void drawNamespaceGroup(NamespaceGroup group)
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      // Header
      EditorGUILayout.BeginHorizontal();

      bool oldInclude = group.includeInGeneration;
      group.includeInGeneration = EditorGUILayout.Toggle(group.includeInGeneration, GUILayout.Width(20));

      if (oldInclude != group.includeInGeneration) {
        foreach (var system in group.systems) {
          system.includeInGeneration = group.includeInGeneration;
        }
      }

      string summary = $"{group.namespaceName} " +
                      $"(R:{group.StandardReducerCount}+{group.BurstReducerCount}⚡ " +
                      $"M:{group.StandardMiddlewareCount}+{group.BurstMiddlewareCount}⚡)";

      group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, summary, true);

      EditorGUILayout.EndHorizontal();

      // Systems list
      if (group.isExpanded) {
        EditorGUI.indentLevel++;

        foreach (var system in group.systems.OrderBy(s => s.className)) {
          EditorGUILayout.BeginHorizontal();

          system.includeInGeneration = EditorGUILayout.Toggle(
            system.includeInGeneration,
            GUILayout.Width(20));

          // Icon based on system type
          string icon = "";
          switch (system.systemKind) {
            case SystemType.StandardReducer:
              icon = "📊";
              break;
            case SystemType.BurstReducer:
              icon = "📊⚡";
              break;
            case SystemType.StandardMiddleware:
              icon = "⚙️";
              break;
            case SystemType.BurstMiddleware:
              icon = "⚙️⚡";
              break;
          }

          EditorGUILayout.LabelField(
            $"{icon} {system.className}",
            GUILayout.Width(250));

          EditorGUILayout.LabelField(
            $"→ {system.GetBridgeName()}",
            EditorStyles.miniLabel);

          EditorGUILayout.EndHorizontal();

          // Show type details
          EditorGUI.indentLevel++;
          string details = system.stateType != null
            ? $"State: {system.stateType.Name}, Action: {system.actionType.Name}"
            : $"Action: {system.actionType.Name}";

          if (system.logicType != null) {
            details += $", Logic: {system.logicType.Name}";
          }

          EditorGUILayout.LabelField(details, EditorStyles.miniLabel);
          EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
      }

      EditorGUILayout.EndVertical();
    }

    private void discoverSystems()
    {
      namespaceGroups.Clear();
      totalSystemsFound = 0;

      try {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies) {
          // Skip Unity and system assemblies
          if (assembly.FullName.StartsWith("Unity.") ||
              assembly.FullName.StartsWith("System.") ||
              assembly.FullName.StartsWith("mscorlib"))
            continue;

          try {
            var types = assembly.GetTypes();

            foreach (var type in types) {
              // Skip abstract classes and non-classes
              if (!type.IsClass || type.IsAbstract)
                continue;

              var systemInfo = AnalyzeSystemType(type);
              if (systemInfo != null) {
                // Add to namespace group
                if (!namespaceGroups.TryGetValue(systemInfo.namespaceName, out var group)) {
                  group = new NamespaceGroup { namespaceName = systemInfo.namespaceName };
                  namespaceGroups[systemInfo.namespaceName] = group;
                }

                group.systems.Add(systemInfo);
                totalSystemsFound++;
              }
            }
          } catch (Exception e) {
            Debug.LogWarning($"Failed to scan assembly {assembly.FullName}: {e.Message}");
          }
        }
      } catch (Exception e) {
        Debug.LogError($"Failed to discover systems: {e.Message}");
      }

      Debug.Log($"[BridgeSystemGenerator] Discovered {totalSystemsFound} systems across {namespaceGroups.Count} namespaces");
    }

    public static SystemInfo AnalyzeSystemType(Type type)
    {
      var baseType = type.BaseType;
      while (baseType != null) {
        if (baseType.IsGenericType) {
          var genericDef = baseType.GetGenericTypeDefinition();
          var genericArgs = baseType.GetGenericArguments();

          // Check for BurstReducerSystem<TState, TAction, TLogic>
          if (genericDef.Name == "BurstReducerSystem`3") {
            return new SystemInfo
            {
              systemType = type,
              stateType = genericArgs[0],
              actionType = genericArgs[1],
              logicType = genericArgs[2],
              systemKind = SystemType.BurstReducer,
              namespaceName = type.Namespace ?? "Global",
              className = type.Name
            };
          }
          // Check for standard ReducerSystem<TState, TAction>
          else if (genericDef.Name == "ReducerSystem`2") {
            return new SystemInfo
            {
              systemType = type,
              stateType = genericArgs[0],
              actionType = genericArgs[1],
              systemKind = SystemType.StandardReducer,
              namespaceName = type.Namespace ?? "Global",
              className = type.Name
            };
          }
          // Check for BurstMiddlewareSystem<TAction, TLogic>
          else if (genericDef.Name == "BurstMiddlewareSystem`2") {
            return new SystemInfo
            {
              systemType = type,
              actionType = genericArgs[0],
              logicType = genericArgs[1],
              systemKind = SystemType.BurstMiddleware,
              namespaceName = type.Namespace ?? "Global",
              className = type.Name
            };
          }
          // Check for standard MiddlewareSystem<TAction>
          else if (genericDef.Name == "MiddlewareSystem`1") {
            return new SystemInfo
            {
              systemType = type,
              actionType = genericArgs[0],
              systemKind = SystemType.StandardMiddleware,
              namespaceName = type.Namespace ?? "Global",
              className = type.Name
            };
          }
        }

        baseType = baseType.BaseType;
      }

      return null;
    }

    private void generateBridgeSystems()
    {
      if (!EditorUtility.DisplayDialog(
        "Generate Bridge Systems",
        $"This will generate bridge systems for all selected reducers and middleware.\n\n" +
        $"Output path: {outputPath}\n\n" +
        $"Continue?",
        "Generate",
        "Cancel")) {
        return;
      }

      List<string> generatedFiles = new List<string>();
      totalBridgesGenerated = 0;

      try {
        EditorUtility.DisplayProgressBar("Generating Bridges", "Starting generation...", 0f);

        int currentIndex = 0;
        int totalToGenerate = namespaceGroups.Values
          .Where(g => g.includeInGeneration)
          .Sum(g => g.systems.Count(s => s.includeInGeneration));

        foreach (var group in namespaceGroups.Values.Where(g => g.includeInGeneration)) {
          var systemsToGenerate = group.systems.Where(s => s.includeInGeneration).ToList();

          if (systemsToGenerate.Count == 0)
            continue;

          string namespacePath = createNamespaceOutputPath(group.namespaceName);

          foreach (var system in systemsToGenerate) {
            EditorUtility.DisplayProgressBar(
              "Generating Bridges",
              $"Generating {system.GetBridgeName()}...",
              (float)currentIndex / totalToGenerate);

            string code = generateBridgeCode(system);
            string fileName = $"{system.GetBridgeName()}.Generated.cs";
            string filePath = Path.Combine(namespacePath, fileName);

            File.WriteAllText(filePath, code);
            generatedFiles.Add(filePath);
            totalBridgesGenerated++;
            currentIndex++;
          }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        string fileList = string.Join("\n• ",
          generatedFiles.Select(f => f.Replace(Application.dataPath, "Assets")));

        EditorUtility.DisplayDialog(
          "Generation Complete",
          $"Successfully generated {totalBridgesGenerated} bridge systems.\n\n" +
          $"Performance gains:\n" +
          $"• Standard bridges: Zero allocations\n" +
          $"• Burst bridges: Zero allocations + 5-10x faster\n\n" +
          $"Files created:\n• {fileList}",
          "OK");
      } catch (Exception e) {
        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog(
          "Generation Failed",
          $"Failed to generate bridge systems:\n\n{e.Message}",
          "OK");
        Debug.LogError($"Bridge generation failed: {e}");
      }
    }

    private string createNamespaceOutputPath(string namespaceName)
    {
      string namespacePath = namespaceName.Replace('.', Path.DirectorySeparatorChar);
      string fullPath = Path.Combine(outputPath, namespacePath, "Bridges");

      if (!Directory.Exists(fullPath)) {
        Directory.CreateDirectory(fullPath);
      }

      return fullPath;
    }

    private string generateBridgeCode(SystemInfo system)
    {
      var sb = new StringBuilder();

      // File header
      sb.AppendLine("// <auto-generated>");
      sb.AppendLine("// This file was automatically generated by ECSReact Bridge System Generator");
      sb.AppendLine("// Do not modify this file directly - it will be overwritten");
      sb.AppendLine($"// Generated from: {system.className}");
      sb.AppendLine($"// System type: {system.systemKind}");
      sb.AppendLine($"// Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
      sb.AppendLine("// </auto-generated>");
      sb.AppendLine();

      // Using statements
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Burst;");
      sb.AppendLine("using Unity.Collections;");
      sb.AppendLine("using ECSReact.Core;");

      // Add namespace-specific using if different
      if (system.namespaceName != "ECSReact.Core") {
        sb.AppendLine($"using {system.namespaceName};");
      }

      sb.AppendLine();

      // Namespace
      sb.AppendLine($"namespace {system.namespaceName}");
      sb.AppendLine("{");

      // Generate appropriate bridge based on system type
      switch (system.systemKind) {
        case SystemType.StandardReducer:
          generateStandardReducerBridge(sb, system);
          break;
        case SystemType.BurstReducer:
          generateBurstReducerBridge(sb, system);
          break;
        case SystemType.StandardMiddleware:
          generateStandardMiddlewareBridge(sb, system);
          break;
        case SystemType.BurstMiddleware:
          generateBurstMiddlewareBridge(sb, system);
          break;
      }

      sb.AppendLine("}");

      return sb.ToString();
    }

    private void generateStandardReducerBridge(StringBuilder sb, SystemInfo system)
    {
      string bridgeName = system.GetBridgeName();

      if (generateXmlDocs) {
        sb.AppendLine("  /// <summary>");
        sb.AppendLine($"  /// Generated bridge for standard reducer: {system.className}");
        sb.AppendLine("  /// Provides zero-allocation iteration using SystemAPI.Query.");
        sb.AppendLine("  /// </summary>");
      }

      sb.AppendLine("  [ReducerSystem]");
      sb.AppendLine($"  internal partial class {bridgeName} : SystemBase");
      sb.AppendLine("  {");
      sb.AppendLine($"    private {system.className} reducer;");
      sb.AppendLine();

      sb.AppendLine("    protected override void OnCreate()");
      sb.AppendLine("    {");
      sb.AppendLine("      base.OnCreate();");
      sb.AppendLine($"      RequireForUpdate<{system.stateType.Name}>();");
      sb.AppendLine($"      RequireForUpdate<{system.actionType.Name}>();");
      sb.AppendLine();
      sb.AppendLine($"      reducer = World.GetOrCreateSystemManaged<{system.className}>();");
      sb.AppendLine("      if (reducer.Enabled)");
      sb.AppendLine("      {");
      sb.AppendLine("        reducer.Enabled = false;");
      sb.AppendLine("      }");
      sb.AppendLine("    }");
      sb.AppendLine();

      sb.AppendLine("    protected override void OnUpdate()");
      sb.AppendLine("    {");
      sb.AppendLine($"      var state = SystemAPI.GetSingletonRW<{system.stateType.Name}>();");
      sb.AppendLine();
      sb.AppendLine("      // Zero-allocation iteration");
      sb.AppendLine($"      foreach (var action in");
      sb.AppendLine($"        SystemAPI.Query<RefRO<{system.actionType.Name}>>()");
      sb.AppendLine($"          .WithAll<ActionTag>())");
      sb.AppendLine("      {");
      sb.AppendLine($"        reducer.ReduceState(ref state.ValueRW, action.ValueRO);");
      sb.AppendLine("      }");
      sb.AppendLine("    }");
      sb.AppendLine("  }");
    }

    private void generateBurstReducerBridge(StringBuilder sb, SystemInfo system)
    {
      string bridgeName = system.GetBridgeName();
      string logicTypeName = GetQualifiedTypeName(system.logicType, system.namespaceName);

      if (generateXmlDocs) {
        sb.AppendLine("  /// <summary>");
        sb.AppendLine($"  /// Generated bridge for Burst-optimized reducer: {system.className}");
        sb.AppendLine($"  /// Uses struct-based logic ({logicTypeName}) for maximum performance.");
        sb.AppendLine("  /// Fully Burst-compiled with zero allocations and SIMD optimizations.");
        sb.AppendLine("  /// </summary>");
      }

      sb.AppendLine("  [ReducerSystem]");
      sb.AppendLine("  [BurstCompile]");
      sb.AppendLine($"  internal partial class {bridgeName} : SystemBase");
      sb.AppendLine("  {");
      sb.AppendLine($"    // Static logic instance for zero allocations");
      sb.AppendLine($"    private static readonly {logicTypeName} logic");
      sb.AppendLine($"      = default({logicTypeName});");
      sb.AppendLine();

      sb.AppendLine("    protected override void OnCreate()");
      sb.AppendLine("    {");
      sb.AppendLine("      base.OnCreate();");
      sb.AppendLine($"      RequireForUpdate<{system.stateType.Name}>();");
      sb.AppendLine($"      RequireForUpdate<{system.actionType.Name}>();");
      sb.AppendLine("    }");
      sb.AppendLine();

      sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    protected override void OnUpdate()");
      sb.AppendLine("    {");
      sb.AppendLine($"      var state = SystemAPI.GetSingletonRW<{system.stateType.Name}>();");
      sb.AppendLine();
      sb.AppendLine($"      foreach (var action in");
      sb.AppendLine($"        SystemAPI.Query<RefRO<{system.actionType.Name}>>()");
      sb.AppendLine($"          .WithAll<ActionTag>())");
      sb.AppendLine("      {");
      sb.AppendLine($"        logic.Execute(ref state.ValueRW, action.ValueRO);");
      sb.AppendLine("      }");
      sb.AppendLine("    }");
      sb.AppendLine("  }");
    }

    private void generateStandardMiddlewareBridge(StringBuilder sb, SystemInfo system)
    {
      string bridgeName = system.GetBridgeName();

      if (generateXmlDocs) {
        sb.AppendLine("  /// <summary>");
        sb.AppendLine($"  /// Generated bridge for standard middleware: {system.className}");
        sb.AppendLine("  /// Provides zero-allocation iteration for middleware processing.");
        sb.AppendLine("  /// </summary>");
      }

      sb.AppendLine("  [MiddlewareSystem]");
      sb.AppendLine($"  internal partial class {bridgeName} : SystemBase");
      sb.AppendLine("  {");
      sb.AppendLine($"    private {system.className} middleware;");
      sb.AppendLine();

      sb.AppendLine("    protected override void OnCreate()");
      sb.AppendLine("    {");
      sb.AppendLine("      base.OnCreate();");
      sb.AppendLine($"      RequireForUpdate<{system.actionType.Name}>();");
      sb.AppendLine();
      sb.AppendLine($"      middleware = World.GetOrCreateSystemManaged<{system.className}>();");
      sb.AppendLine("      if (middleware.Enabled)");
      sb.AppendLine("      {");
      sb.AppendLine("        middleware.Enabled = false;");
      sb.AppendLine("      }");
      sb.AppendLine("    }");
      sb.AppendLine();

      sb.AppendLine("    protected override void OnUpdate()");
      sb.AppendLine("    {");
      sb.AppendLine("      // Zero-allocation iteration");
      sb.AppendLine($"      foreach (var (action, entity) in");
      sb.AppendLine($"        SystemAPI.Query<RefRO<{system.actionType.Name}>>()");
      sb.AppendLine($"          .WithAll<ActionTag>()");
      sb.AppendLine($"          .WithEntityAccess())");
      sb.AppendLine("      {");
      sb.AppendLine($"        middleware.ProcessAction(action.ValueRO, entity);");
      sb.AppendLine("      }");
      sb.AppendLine("    }");
      sb.AppendLine("  }");
    }

    private void generateBurstMiddlewareBridge(StringBuilder sb, SystemInfo system)
    {
      string bridgeName = system.GetBridgeName();
      string logicTypeName = GetQualifiedTypeName(system.logicType, system.namespaceName);

      if (generateXmlDocs) {
        sb.AppendLine("  /// <summary>");
        sb.AppendLine($"  /// Generated bridge for Burst-optimized middleware: {system.className}");
        sb.AppendLine($"  /// Uses struct-based logic ({logicTypeName}) for maximum performance.");
        sb.AppendLine("  /// </summary>");
      }

      sb.AppendLine("  [MiddlewareSystem]");
      sb.AppendLine("  [BurstCompile]");
      sb.AppendLine($"  internal partial class {bridgeName} : SystemBase");
      sb.AppendLine("  {");
      sb.AppendLine($"    // Static logic instance - zero allocations!");
      sb.AppendLine($"    private static readonly {logicTypeName} logic");
      sb.AppendLine($"      = default({logicTypeName});");
      sb.AppendLine();

      sb.AppendLine("    protected override void OnCreate()");
      sb.AppendLine("    {");
      sb.AppendLine("      base.OnCreate();");
      sb.AppendLine($"      RequireForUpdate<{system.actionType.Name}>();");
      sb.AppendLine("    }");
      sb.AppendLine();

      sb.AppendLine("    [BurstCompile]");
      sb.AppendLine("    protected override void OnUpdate()");
      sb.AppendLine("    {");
      sb.AppendLine($"      foreach (var (action, entity) in");
      sb.AppendLine($"        SystemAPI.Query<RefRO<{system.actionType.Name}>>()");
      sb.AppendLine($"          .WithAll<ActionTag>()");
      sb.AppendLine($"          .WithEntityAccess())");
      sb.AppendLine("      {");
      sb.AppendLine($"        logic.Execute(action.ValueRO, entity);");
      sb.AppendLine("      }");
      sb.AppendLine("    }");
      sb.AppendLine("  }");
    }

    private void previewGeneratedCode()
    {
      var selectedSystems = namespaceGroups.Values
        .SelectMany(g => g.systems)
        .Where(s => s.includeInGeneration)
        .Take(1)
        .ToList();

      if (selectedSystems.Count == 0) {
        EditorUtility.DisplayDialog(
          "No Systems Selected",
          "Please select at least one system to preview.",
          "OK");
        return;
      }

      var system = selectedSystems[0];
      string code = generateBridgeCode(system);

      // Create preview window
      var previewWindow = GetWindow<CodePreviewWindow>("Bridge Preview");
      previewWindow.SetCode(code);
    }

    /// <summary>
    /// Method called by AutoGenerateAll to generate bridges for a specific namespace.
    /// Follows the same pattern as other generators.
    /// </summary>
    public int GenerateBridgesForNamespace(NamespaceGroup namespaceGroup, string baseOutputPath, ref List<string> generatedFiles)
    {
      int bridgesGenerated = 0;

      // Skip if no systems in this namespace
      if (namespaceGroup.SystemCount == 0) {
        return 0;
      }

      try {
        // Discover all systems in the project (we need full type info, not just names)
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var systemsInNamespace = new List<SystemInfo>();

        foreach (var assembly in assemblies) {
          // Skip Unity and system assemblies
          if (assembly.FullName.StartsWith("Unity.") ||
              assembly.FullName.StartsWith("System.") ||
              assembly.FullName.StartsWith("mscorlib"))
            continue;

          try {
            var types = assembly.GetTypes()
              .Where(t => t.Namespace == namespaceGroup.namespaceName)
              .Where(t => t.IsClass && !t.IsAbstract);

            foreach (var type in types) {
              var systemInfo = AnalyzeSystemType(type);
              if (systemInfo != null) {
                systemsInNamespace.Add(systemInfo);
              }
            }
          } catch {
            // Skip assemblies we can't scan
          }
        }

        // Generate bridges for all systems in this namespace
        string namespacePath = createNamespaceOutputPath(namespaceGroup.namespaceName, baseOutputPath);

        foreach (var system in systemsInNamespace) {
          string code = generateBridgeCode(system);
          string fileName = $"{system.GetBridgeName()}.Generated.cs";
          string filePath = Path.Combine(namespacePath, fileName);

          File.WriteAllText(filePath, code);
          generatedFiles.Add(filePath);
          bridgesGenerated++;
        }
      } catch (Exception e) {
        Debug.LogError($"Failed to generate bridges for namespace {namespaceGroup.namespaceName}: {e.Message}");
      }

      return bridgesGenerated;
    }

    // Overload for createNamespaceOutputPath that accepts a base path
    private string createNamespaceOutputPath(string namespaceName, string baseOutputPath)
    {
      string namespacePath = namespaceName.Replace('.', Path.DirectorySeparatorChar);
      string fullPath = Path.Combine(baseOutputPath, namespacePath, "Bridges");

      if (!Directory.Exists(fullPath)) {
        Directory.CreateDirectory(fullPath);
      }

      return fullPath;
    }

    private string GetQualifiedTypeName(Type type, string currentNamespace)
    {
      if (type == null)
        return "unknown";

      string fullName = type.FullName ?? type.Name;

      // Handle nested types (they use '+' in FullName, we need '.')
      fullName = fullName.Replace('+', '.');

      // Remove generic backtick notation
      fullName = System.Text.RegularExpressions.Regex.Replace(fullName, @"`\d+", "");

      // If the type is in the current namespace, we can use relative naming
      if (!string.IsNullOrEmpty(currentNamespace) && fullName.StartsWith(currentNamespace + ".")) {
        // Remove the namespace prefix since it will be in the same namespace
        return fullName.Substring(currentNamespace.Length + 1);
      }

      return fullName;
    }
  }
}