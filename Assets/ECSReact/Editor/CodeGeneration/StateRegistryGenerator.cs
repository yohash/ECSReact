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
  /// Generates a strongly-typed registry for all IGameState types in the project.
  /// This allows us to use Unity's CreateSingleton method without reflection.
  /// </summary>
  public class StateRegistryGenerator : EditorWindow
  {
    private Vector2 scrollPosition;
    private Dictionary<string, NamespaceGroup> namespaceGroups = new();
    private string outputPath = Constants.DEFAULT_OUTPUT_PATH;
    private bool autoRefreshDiscovery = false;
    private bool generateDebugLogs = false;

    [MenuItem("ECS React/Generate State Registry", priority = 200)]
    public static void ShowWindow()
    {
      GetWindow<StateRegistryGenerator>("State Registry Generator");
    }

    private void OnEnable()
    {
      discoverStateTypes();
    }

    private void OnGUI()
    {
      GUILayout.Label("State Registry Generator", EditorStyles.boldLabel);

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

      EditorGUILayout.Space();

      // Options
      autoRefreshDiscovery = EditorGUILayout.Toggle("Auto-refresh Discovery", autoRefreshDiscovery);
      generateDebugLogs = EditorGUILayout.Toggle("Generate Debug Logs", generateDebugLogs);

      // Discovery controls
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Discover State Types")) {
        discoverStateTypes();
      }
      if (GUILayout.Button("Clear Discovery")) {
        namespaceGroups.Clear();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // Display discovered states grouped by namespace
      int totalStates = namespaceGroups.Values.Sum(g => g.states.Count);

      if (totalStates > 0) {
        EditorGUILayout.LabelField($"Discovered {totalStates} IGameState types in {namespaceGroups.Count} namespaces:", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var kvp in namespaceGroups.OrderBy(n => n.Key)) {
          drawNamespaceGroup(kvp.Key, kvp.Value);
          EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();
      } else {
        EditorGUILayout.HelpBox("No IGameState types discovered. Make sure you have defined state types in your project.", MessageType.Info);
      }

      EditorGUILayout.Space();

      // Generation controls
      EditorGUILayout.BeginHorizontal();

      bool hasSelectedStates = namespaceGroups.Values.Any(g => g.includeInGeneration && g.states.Any(s => s.includeInGeneration));
      GUI.enabled = hasSelectedStates;

      if (GUILayout.Button("Generate State Registry", GUILayout.Height(30))) {
        generateStateRegistry();
      }

      GUI.enabled = true;

      if (GUILayout.Button("Preview Generated Code", GUILayout.Height(30))) {
        previewGeneratedCode();
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // Status/Help
      EditorGUILayout.HelpBox(
          "This generator creates a registry that enables strongly-typed state singleton creation.\n\n" +
          "The generated registry:\n" +
          "• Implements IStateRegistry interface from ECSReact.Core\n" +
          "• Auto-registers with StateRegistryService on startup\n" +
          "• Provides CreateSingleton delegates for each state type\n" +
          "• Enables SceneStateManager to create state entities\n\n" +
          "Requirements:\n" +
          "• States must implement both IComponentData and IGameState\n" +
          "• States must be unmanaged types (no reference fields)",
          MessageType.Info);
    }

    private void drawNamespaceGroup(string namespaceName, NamespaceGroup group)
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      // Namespace header
      EditorGUILayout.BeginHorizontal();

      // Toggle for including namespace
      bool oldInclude = group.includeInGeneration;
      group.includeInGeneration = EditorGUILayout.Toggle(group.includeInGeneration, GUILayout.Width(20));

      if (oldInclude != group.includeInGeneration) {
        // Apply to all states in namespace
        foreach (var state in group.states) {
          state.includeInGeneration = group.includeInGeneration;
        }
      }

      // Foldout
      group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, $"  {namespaceName} ({group.states.Count} states)", true);

      EditorGUILayout.EndHorizontal();

      // Show states if expanded
      if (group.isExpanded) {
        EditorGUI.indentLevel++;

        foreach (var state in group.states) {
          EditorGUILayout.BeginHorizontal();

          GUI.enabled = false;
          state.includeInGeneration = EditorGUILayout.Toggle(state.includeInGeneration, GUILayout.Width(40));

          EditorGUILayout.LabelField(state.typeName);
          GUI.enabled = true;

          EditorGUILayout.EndHorizontal();
        }

        EditorGUI.indentLevel--;
      }

      EditorGUILayout.EndVertical();
    }

    private void discoverStateTypes()
    {
      var previousGroups = namespaceGroups.ToDictionary(g => g.Key, g => g.Value);
      namespaceGroups.Clear();

      var assemblies = AppDomain.CurrentDomain.GetAssemblies();

      foreach (var assembly in assemblies) {
        try {
          var types = assembly.GetTypes()
              .Where(t => t.IsValueType && !t.IsEnum && !t.IsGenericType)
              .Where(t => typeof(Unity.Entities.IComponentData).IsAssignableFrom(t))
              .Where(t => t.GetInterfaces().Any(i => i.Name == "IGameState"))
              .ToList();

          foreach (var type in types) {
            string ns = type.Namespace ?? "Global";

            var stateInfo = new StateTypeInfo
            {
              stateType = type,
              typeName = type.Name,
              fullTypeName = type.FullName,
              namespaceName = ns,
              assemblyName = assembly.GetName().Name,
              includeInGeneration = true
            };

            if (!namespaceGroups.ContainsKey(ns)) {
              namespaceGroups[ns] = new NamespaceGroup
              {
                namespaceName = ns,
                states = new List<StateTypeInfo>(),
                isExpanded = true,
                includeInGeneration = true
              };
            }

            // Preserve previous checkbox state on re-discovery
            if (previousGroups.TryGetValue(ns, out var previousGroup)) {
              var previousState = previousGroup.states.FirstOrDefault(s => s.typeName == type.Name);
              if (previousState != null) {
                stateInfo.includeInGeneration = previousState.includeInGeneration;
              }
              namespaceGroups[ns].includeInGeneration = previousGroup.includeInGeneration;
              namespaceGroups[ns].isExpanded = previousGroup.isExpanded;
            }

            namespaceGroups[ns].states.Add(stateInfo);
          }
        } catch (System.Reflection.ReflectionTypeLoadException ex) {
          Debug.LogWarning($"[StateRegistryGenerator] Could not load types from assembly {assembly.GetName().Name}: {ex.Message}");
        } catch (Exception ex) {
          Debug.LogWarning($"[StateRegistryGenerator] Error processing assembly {assembly.GetName().Name}: {ex.Message}");
        }
      }

      foreach (var group in namespaceGroups.Values) {
        group.states = group.states.OrderBy(s => s.typeName).ToList();
      }

      Debug.Log($"[StateRegistryGenerator] Discovered {namespaceGroups.Values.Sum(g => g.states.Count)} state types across {namespaceGroups.Count} namespaces");
    }

    private void generateStateRegistry()
    {
      var selectedNamespaces = new List<NamespaceGroup>();
      var generatedFiles = new List<string>();

      foreach (var group in namespaceGroups.Values) {
        if (group.includeInGeneration && group.states.Any(s => s.includeInGeneration)) {
          selectedNamespaces.Add(group);
        }
      }

      if (selectedNamespaces.Count == 0) {
        EditorUtility.DisplayDialog("No States Selected",
            "Please select at least one state type to generate the registry.", "OK");
        return;
      }

      // Generate a registry file for each namespace
      foreach (var namespaceGroup in selectedNamespaces) {
        GenerateRegistryForNamespace(namespaceGroup, ref generatedFiles);
      }

      AssetDatabase.Refresh();

      // Show success dialog
      int totalStates = selectedNamespaces.Sum(ns => ns.states.Count(s => s.includeInGeneration));
      string fileList = string.Join("\n• ", generatedFiles.Select(f => f.Replace(Application.dataPath, "Assets")));

      EditorUtility.DisplayDialog("Generation Complete",
          $"Generated state registries for {totalStates} states across {selectedNamespaces.Count} namespaces.\n\n" +
          $"Files created:\n• {fileList}\n\n" +
          "The registries will auto-register on startup and be available to SceneStateManager.",
          "OK");
    }

    public void GenerateRegistryForNamespace(NamespaceGroup namespaceGroup, ref List<string> generatedFiles)
    {
      var selectedStates = namespaceGroup.states
          .Where(s => s.includeInGeneration)
          .Select(s => s.stateType)
          .ToList();

      if (selectedStates.Count == 0)
        return;

      // Create namespace-specific output directory
      string namespaceOutputPath = createNamespaceOutputPath(namespaceGroup.namespaceName);

      // Generate registry code for this namespace
      string registryCode = generateNamespaceRegistryCode(selectedStates, namespaceGroup.namespaceName);
      string registryPath = Path.Combine(namespaceOutputPath, "StateRegistry.Generated.cs");

      File.WriteAllText(registryPath, registryCode);
      generatedFiles.Add(registryPath);
    }

    private string createNamespaceOutputPath(string namespaceName)
    {
      // Convert namespace to folder path: ECSReact.Core → ECSReact/Core
      string namespacePath = namespaceName.Replace('.', '/');
      string fullPath = Path.Combine(outputPath, namespacePath);

      if (!Directory.Exists(fullPath)) {
        Directory.CreateDirectory(fullPath);
      }

      return fullPath;
    }

    private string generateNamespaceRegistryCode(List<Type> stateTypes, string namespaceName)
    {
      var sb = new StringBuilder();

      // File header
      sb.AppendLine("// <auto-generated>");
      sb.AppendLine("// This file was automatically generated by StateRegistryGenerator");
      sb.AppendLine($"// Namespace: {namespaceName}");
      sb.AppendLine("// Do not modify this file manually - your changes will be overwritten");
      sb.AppendLine("// </auto-generated>");
      sb.AppendLine();

      // Usings
      sb.AppendLine("using System;");
      sb.AppendLine("using System.Collections.Generic;");
      sb.AppendLine("using System.Linq;");
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Collections;");
      sb.AppendLine("using UnityEngine;");
      sb.AppendLine("using ECSReact.Core;");

      // Add namespace-specific using if different from current namespace
      if (namespaceName != "ECSReact.Core") {
        sb.AppendLine($"using {namespaceName};");
      }

      sb.AppendLine();

      // Begin namespace
      sb.AppendLine($"namespace {namespaceName}");
      sb.AppendLine("{");

      // Generate unique class name based on namespace
      string className = generateRegistryClassName(namespaceName);

      sb.AppendLine("  /// <summary>");
      sb.AppendLine($"  /// Auto-generated state registry for {namespaceName} namespace.");
      sb.AppendLine("  /// Provides strongly-typed access to all game states in this namespace.");
      sb.AppendLine("  /// </summary>");
      sb.AppendLine($"  public class {className} : IStateRegistry");
      sb.AppendLine("  {");

      // Static instance and initialization
      sb.AppendLine($"    private static {className} _instance;");
      sb.AppendLine("    private static readonly object _lock = new object();");
      sb.AppendLine();
      sb.AppendLine("    /// <summary>");
      sb.AppendLine("    /// Singleton instance of the registry.");
      sb.AppendLine("    /// </summary>");
      sb.AppendLine($"    public static {className} Instance {{");
      sb.AppendLine("      get {");
      sb.AppendLine("        if (_instance == null) {");
      sb.AppendLine("          lock (_lock) {");
      sb.AppendLine("            if (_instance == null) {");
      sb.AppendLine($"              _instance = new {className}();");
      sb.AppendLine("              _instance.Initialize();");
      sb.AppendLine("            }");
      sb.AppendLine("          }");
      sb.AppendLine("        }");
      sb.AppendLine("        return _instance;");
      sb.AppendLine("      }");
      sb.AppendLine("    }");
      sb.AppendLine();

      // Generate registry dictionary
      sb.AppendLine("    private readonly Dictionary<Type, IStateInfo> stateInfos = new Dictionary<Type, IStateInfo>();");
      sb.AppendLine();

      sb.AppendLine("    private void Initialize()");
      sb.AppendLine("    {");

      foreach (var stateType in stateTypes) {
        var typeName = stateType.Name; // Just use simple name since we have using directives
        sb.AppendLine($"      stateInfos.Add(typeof({typeName}), new StateInfoBase");
        sb.AppendLine("      {");
        sb.AppendLine($"        Type = typeof({typeName}),");
        sb.AppendLine($"        Name = \"{stateType.Name}\",");
        sb.AppendLine($"        Namespace = \"{stateType.Namespace ?? "Global"}\",");
        sb.AppendLine($"        CreateSingletonFunc = (em, name) => em.CreateSingleton(default({typeName}), name),");
        sb.AppendLine($"        GetComponentFunc = (em, entity) => em.GetComponentData<{typeName}>(entity),");
        sb.AppendLine($"        SetComponentAction = (em, entity, data) => em.SetComponentData(entity, ({typeName})data),");
        sb.AppendLine($"        DeserializeJsonFunc = (json) => string.IsNullOrEmpty(json) ? default({typeName}) : JsonUtility.FromJson<{typeName}>(json)");
        sb.AppendLine("      });");
      }

      if (generateDebugLogs) {
        sb.AppendLine();
        sb.AppendLine($"      Debug.Log(\"[{className}] Initialized with \" + stateInfos.Count + \" state types from {namespaceName}\");");
      }

      sb.AppendLine("    }");
      sb.AppendLine();

      // Implement IStateRegistry interface
      sb.AppendLine("    public IReadOnlyDictionary<Type, IStateInfo> AllStates => stateInfos;");
      sb.AppendLine();

      sb.AppendLine("    public IStateInfo GetStateInfo(Type type)");
      sb.AppendLine("    {");
      sb.AppendLine("      return stateInfos.TryGetValue(type, out var info) ? info : null;");
      sb.AppendLine("    }");
      sb.AppendLine();

      sb.AppendLine("    public Entity CreateStateSingleton(EntityManager entityManager, Type stateType, FixedString64Bytes name)");
      sb.AppendLine("    {");
      sb.AppendLine("      var info = GetStateInfo(stateType);");
      sb.AppendLine("      return info?.CreateSingleton(entityManager, name) ?? Entity.Null;");
      sb.AppendLine("    }");
      sb.AppendLine();

      sb.AppendLine("    public List<Type> GetStatesByNamespace(string namespaceName)");
      sb.AppendLine("    {");
      sb.AppendLine("      return stateInfos.Values");
      sb.AppendLine("        .Where(info => info.Namespace == namespaceName)");
      sb.AppendLine("        .Select(info => info.Type)");
      sb.AppendLine("        .ToList();");
      sb.AppendLine("    }");
      sb.AppendLine();

      sb.AppendLine("    public List<string> GetAllNamespaces()");
      sb.AppendLine("    {");
      sb.AppendLine("      return stateInfos.Values");
      sb.AppendLine("        .Select(info => info.Namespace)");
      sb.AppendLine("        .Distinct()");
      sb.AppendLine("        .OrderBy(ns => ns)");
      sb.AppendLine("        .ToList();");
      sb.AppendLine("    }");
      sb.AppendLine();

      // Auto-registration with ECSReact.Core
      sb.AppendLine("    /// <summary>");
      sb.AppendLine("    /// Ensures the registry is created and registered with StateRegistryService.");
      sb.AppendLine("    /// Called automatically by Unity's RuntimeInitializeOnLoadMethod.");
      sb.AppendLine("    /// </summary>");
      sb.AppendLine("    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]");
      sb.AppendLine("    public static void EnsureRegistered()");
      sb.AppendLine("    {");
      sb.AppendLine("      var registry = Instance;");
      sb.AppendLine("      StateRegistryService.RegisterRegistry(registry);");

      if (generateDebugLogs) {
        sb.AppendLine($"      Debug.Log(\"[{className}] Auto-registered with StateRegistryService (\" + registry.AllStates.Count + \" states)\");");
      }

      sb.AppendLine("    }");

      // End class and namespace
      sb.AppendLine("  }");
      sb.AppendLine("}");

      return sb.ToString();
    }

    private string generateRegistryClassName(string namespaceName)
    {
      // Convert namespace to a valid class name
      // e.g., "MyGame.Combat" → "MyGameCombatStateRegistry"
      var parts = namespaceName.Split('.');
      var className = string.Join("", parts) + "StateRegistry";

      // Handle edge cases
      if (namespaceName == "Global" || string.IsNullOrEmpty(namespaceName)) {
        className = "GlobalStateRegistry";
      }

      return className;
    }

    private void previewGeneratedCode()
    {
      var selectedNamespaces = new List<NamespaceGroup>();

      foreach (var group in namespaceGroups.Values) {
        if (group.includeInGeneration && group.states.Any(s => s.includeInGeneration)) {
          selectedNamespaces.Add(group);
        }
      }

      if (selectedNamespaces.Count == 0) {
        EditorUtility.DisplayDialog("No States Selected",
            "Please select at least one state type to preview the generated code.", "OK");
        return;
      }

      // For preview, just show the first namespace's generated code
      var firstNamespace = selectedNamespaces.First();
      var selectedStates = firstNamespace.states
          .Where(s => s.includeInGeneration)
          .Select(s => s.stateType)
          .ToList();

      var code = generateNamespaceRegistryCode(selectedStates, firstNamespace.namespaceName);

      // Create a preview window
      var previewWindow = GetWindow<CodePreviewWindow>($"Preview: {firstNamespace.namespaceName}");
      previewWindow.SetCode(code);

      if (selectedNamespaces.Count > 1) {
        EditorUtility.DisplayDialog("Preview Note",
            $"Showing preview for {firstNamespace.namespaceName} namespace.\n\n" +
            $"Note: {selectedNamespaces.Count} namespace files will be generated, one for each selected namespace.",
            "OK");
      }
    }
  }

}