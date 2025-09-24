using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Entities;

namespace ECSReact.Editor.CodeGeneration
{
  public class StateSubscriptionHelperGenerator : EditorWindow
  {
    private Vector2 scrollPosition;
    private Dictionary<string, NamespaceGroup> namespaceGroups = new();
    private bool autoRefreshDiscovery = false;
    private bool generateDebugLogs = false;
    private string outputPath = Constants.DEFAULT_OUTPUT_PATH;

    [MenuItem("ECS React/Generate StateSubscriptionHelper", priority = 202)]
    public static void ShowWindow()
    {
      GetWindow<StateSubscriptionHelperGenerator>("StateSubscriptionHelper Generator");
    }

    private void OnEnable()
    {
      discoverStateTypes();
    }

    private void OnGUI()
    {
      GUILayout.Label("StateSubscriptionHelper Code Generator", EditorStyles.boldLabel);

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

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(180));

        foreach (var kvp in namespaceGroups.OrderBy(n => n.Key)) {
          drawNamespaceGroup(kvp.Key, kvp.Value);
          EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        // Warning about IEquatable
        var statesWithoutEquatable = namespaceGroups.Values
            .SelectMany(g => g.states)
            .Where(s => s.includeInGeneration && !s.implementsIEquatable)
            .ToList();

        if (statesWithoutEquatable.Any()) {
          EditorGUILayout.HelpBox(
              $"Warning: {statesWithoutEquatable.Count} selected state(s) don't implement IEquatable<T>. " +
              "State change detection requires IEquatable for efficient comparison. " +
              "Consider implementing IEquatable<T> on: " +
              string.Join(", ", statesWithoutEquatable.Select(s => s.typeName)),
              MessageType.Warning);
        }
      } else {
        EditorGUILayout.HelpBox("No IGameState types discovered. Make sure you have defined state types in your project.", MessageType.Info);
      }

      EditorGUILayout.Space();

      // Generation controls
      EditorGUILayout.BeginHorizontal();

      bool hasSelectedStates = namespaceGroups.Values.Any(g => g.includeInGeneration && g.states.Any(s => s.includeInGeneration));
      GUI.enabled = hasSelectedStates;
      if (GUILayout.Button("Generate StateSubscriptionHelper Extensions", GUILayout.Height(30))) {
        generateStateSubscriptionHelperExtensions();
      }
      GUI.enabled = true;

      if (GUILayout.Button("Preview Generated Code", GUILayout.Height(30))) {
        previewGeneratedCode();
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // Status/Help
      EditorGUILayout.HelpBox(
          "States are grouped by namespace for organized code generation.\n\n" +
          "This generator creates the StateSubscriptionHelper extensions that connect UI components to state change events.\n\n" +
          "Generated code registers handlers that:\n" +
          "• Connect IStateSubscriber<T> to UIStateNotifier events\n" +
          "• Enable type-safe Subscribe<T>() and Unsubscribe<T>() methods\n" +
          "• Complete the UI state subscription system\n\n" +
          "Output Structure:\n" +
          "• Each namespace generates its own initialization method\n" +
          "• Files organized in namespace-specific folders\n\n" +
          "Requirements:\n" +
          "• UIStateNotifier extensions must be generated first\n" +
          "• State types should implement IEquatable<T> for change detection",
          MessageType.Info);

      // Auto-refresh
      if (autoRefreshDiscovery && Event.current.type == EventType.Layout) {
        discoverStateTypes();
      }
    }

    private void drawNamespaceGroup(string namespaceName, NamespaceGroup group)
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      // Namespace header with checkbox and expand/collapse
      EditorGUILayout.BeginHorizontal();

      // Namespace-level checkbox
      bool newIncludeNamespace = EditorGUILayout.Toggle(group.includeInGeneration, GUILayout.Width(20));
      if (newIncludeNamespace != group.includeInGeneration) {
        group.includeInGeneration = newIncludeNamespace;
        // Update all states in this namespace
        foreach (var state in group.states) {
          state.includeInGeneration = newIncludeNamespace;
        }
      }

      EditorGUILayout.BeginHorizontal();
      group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, $"  {namespaceName} ({group.states.Count} states)", true);
      EditorGUILayout.EndHorizontal();

      // Show namespace-level IEquatable summary
      int equatableCount = group.states.Count(s => s.implementsIEquatable);
      string equatableSummary = $"({equatableCount}/{group.states.Count} with IEquatable)";
      EditorGUILayout.LabelField(equatableSummary, EditorStyles.miniLabel, GUILayout.Width(150));

      EditorGUILayout.EndHorizontal();

      // Show states in this namespace if expanded
      if (group.isExpanded) {
        EditorGUI.indentLevel++;

        foreach (var state in group.states) {
          EditorGUILayout.BeginHorizontal();

          state.includeInGeneration = EditorGUILayout.Toggle(state.includeInGeneration, GUILayout.Width(40));
          EditorGUILayout.LabelField(state.typeName, GUILayout.Width(180));
          EditorGUILayout.LabelField(state.assemblyName, EditorStyles.miniLabel, GUILayout.Width(80), GUILayout.ExpandWidth(true));

          // Show if it implements IEquatable (required for state change detection)
          bool hasEquatable = state.implementsIEquatable;
          GUI.enabled = false;
          EditorGUILayout.Toggle("IEquatable", hasEquatable, GUILayout.Width(180));
          GUI.enabled = true;

          if (!hasEquatable) {
            EditorGUILayout.LabelField("⚠️", EditorStyles.boldLabel, GUILayout.Width(36));
          }

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

      // Get all assemblies in the project
      var assemblies = AppDomain.CurrentDomain.GetAssemblies();

      foreach (var assembly in assemblies) {
        try {
          var types = assembly.GetTypes()
            .Where(t => t.IsValueType && !t.IsEnum && !t.IsGenericType)
            .Where(t => typeof(IComponentData).IsAssignableFrom(t))
            .Where(t => t.GetInterfaces().Any(i => i.Name == "IGameState"))
            .ToList();

          foreach (var type in types) {
            string namespaceName = type.Namespace ?? "Global";

            // Check if it implements IEquatable<T>
            var equatableInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                              i.GetGenericTypeDefinition() == typeof(IEquatable<>) &&
                              i.GetGenericArguments()[0] == type);

            var stateInfo = new StateTypeInfo
            {
              typeName = type.Name,
              fullTypeName = type.FullName,
              namespaceName = namespaceName,
              assemblyName = assembly.GetName().Name,
              includeInGeneration = namespaceName == "ECSReact.Core" ? false : true,
              implementsIEquatable = equatableInterface != null
            };

            // Get or create namespace group
            if (!namespaceGroups.ContainsKey(namespaceName)) {
              namespaceGroups[namespaceName] = new NamespaceGroup
              {
                namespaceName = namespaceName,
                includeInGeneration = namespaceName == "ECSReact.Core" ? false : true,
                isExpanded = namespaceName == "ECSReact.Core" ? false : true,
                states = new List<StateTypeInfo>()
              };
            }

            // Preserve previous settings if they exist
            if (previousGroups.TryGetValue(namespaceName, out var previousGroup)) {
              var previousState = previousGroup.states.FirstOrDefault(s => s.typeName == type.Name);
              if (previousState != null) {
                stateInfo.includeInGeneration = previousState.includeInGeneration;
              }

              namespaceGroups[namespaceName].includeInGeneration = previousGroup.includeInGeneration;
              namespaceGroups[namespaceName].isExpanded = previousGroup.isExpanded;
            }

            namespaceGroups[namespaceName].states.Add(stateInfo);
          }
        } catch (ReflectionTypeLoadException ex) {
          Debug.LogWarning($"Could not load types from assembly {assembly.GetName().Name}: {ex.Message}");
        } catch (Exception ex) {
          Debug.LogWarning($"Error processing assembly {assembly.GetName().Name}: {ex.Message}");
        }
      }

      // Sort states within each namespace
      foreach (var group in namespaceGroups.Values) {
        group.states = group.states.OrderBy(s => s.typeName).ToList();
      }

      int totalStates = namespaceGroups.Values.Sum(g => g.states.Count);
      Debug.Log($"StateSubscriptionHelper Generator: Discovered {totalStates} state types across {namespaceGroups.Count} namespaces");
    }

    private void generateStateSubscriptionHelperExtensions()
    {
      var selectedNamespaces = namespaceGroups.Values
          .Where(g => g.includeInGeneration && g.states.Any(s => s.includeInGeneration))
          .ToList();

      if (selectedNamespaces.Count == 0) {
        EditorUtility.DisplayDialog("No States Selected", "Please select at least one state type to generate.", "OK");
        return;
      }
      List<string> generatedFiles = new List<string>();
      int totalStatesGenerated = 0;

      foreach (var namespaceGroup in selectedNamespaces) {
        var selectedStates = namespaceGroup.states.Where(s => s.includeInGeneration).ToList();
        if (selectedStates.Count == 0) {
          continue;
        }

        GenerateStateSubscriptionHelperCodeForNamespace(namespaceGroup, ref generatedFiles);
        totalStatesGenerated += selectedStates.Count;
      }

      // Refresh Unity to recognize the new files
      AssetDatabase.Refresh();

      string fileList = string.Join("\n• ", generatedFiles.Select(f => f.Replace(Application.dataPath, "Assets")));

      // Generate list of initialization methods to call
      string initMethods = string.Join("\n• ", selectedNamespaces.Select(ns =>
        $"StateSubscriptionHelper.Initialize{ns.namespaceName.Replace(".", "").Replace(" ", "")}Subscriptions()"));

      EditorUtility.DisplayDialog("Generation Complete",
        $"Generated StateSubscriptionHelper extensions for {totalStatesGenerated} states across {selectedNamespaces.Count} namespaces.\n\n" +
        $"Files created:\n• {fileList}\n\n" +
        $"Remember to call these initialization methods in your startup code:\n• {initMethods}\n\n" +
        "Note: Make sure UIStateNotifier extensions are also generated for complete functionality.", "OK");
    }

    public void GenerateStateSubscriptionHelperCodeForNamespace(NamespaceGroup namespaceGroup, ref List<string> generatedFiles)
    {
      if (namespaceGroup.states.Count == 0) {
        Debug.LogWarning($"No states found for namespace {namespaceGroup.namespaceName}");
        return;
      }
      var selectedStates = namespaceGroup.states.Where(s => s.includeInGeneration).ToList();
      if (selectedStates.Count == 0) {
        Debug.LogWarning($"No states selected for generation in namespace {namespaceGroup.namespaceName}");
        return;
      }

      // Create namespace-specific output directory
      string namespaceOutputPath = createNamespaceOutputPath(namespaceGroup.namespaceName);

      // Generate StateSubscriptionHelper partial class for this namespace
      string helperCode = generateStateSubscriptionHelperCode(selectedStates, namespaceGroup.namespaceName);
      string helperPath = Path.Combine(namespaceOutputPath, "StateSubscriptionHelper.Generated.cs");
      File.WriteAllText(helperPath, helperCode);
      generatedFiles.Add(helperPath);

      // Generate StateChangeNotificationSystems for this namespace
      string notificationSystemsCode = generateStateChangeNotificationSystems(selectedStates, namespaceGroup.namespaceName);
      string notificationSystemsPath = Path.Combine(namespaceOutputPath, "StateChangeNotificationSystems.Generated.cs");
      File.WriteAllText(notificationSystemsPath, notificationSystemsCode);
      generatedFiles.Add(notificationSystemsPath);
    }

    private string createNamespaceOutputPath(string namespaceName)
    {
      // Convert namespace to folder path: ECSReact.Core → ECSReact/Core
      string namespacePath = namespaceName.Replace('.', Path.DirectorySeparatorChar);
      string fullOutputPath = Path.Combine(outputPath, namespacePath);

      if (!Directory.Exists(fullOutputPath)) {
        Directory.CreateDirectory(fullOutputPath);
      }

      return fullOutputPath;
    }

    private string generateStateSubscriptionHelperCode(List<StateTypeInfo> states, string namespaceName)
    {
      var sb = new StringBuilder();

      // File header
      sb.AppendLine("// <auto-generated>");
      sb.AppendLine("// This file was automatically generated by StateSubscriptionHelper Generator");
      sb.AppendLine($"// Namespace: {namespaceName}");
      sb.AppendLine("// Do not modify this file directly - it will be overwritten");
      sb.AppendLine("// </auto-generated>");
      sb.AppendLine();
      sb.AppendLine("using System;");
      sb.AppendLine("using ECSReact.Core;");

      // Add namespace-specific using if different from ECSReact.Core
      if (namespaceName != "ECSReact.Core") {
        sb.AppendLine($"using {namespaceName};");
      }

      if (generateDebugLogs) {
        sb.AppendLine("using UnityEngine;");
      }
      sb.AppendLine();
      sb.AppendLine($"namespace {namespaceName}");
      sb.AppendLine("{");
      sb.AppendLine("  /// <summary>");
      sb.AppendLine($"  /// Generated helpers for StateSubscriptionHelper that register subscription handlers for {namespaceName} state types.");
      sb.AppendLine("  /// </summary>");
      sb.AppendLine("  public static class StateSubscriptionRegistration");
      sb.AppendLine("  {");

      // Generate initialization method for this namespace
      string initMethodName = $"InitializeSubscriptions";
      sb.AppendLine("    /// <summary>");
      sb.AppendLine($"    /// Initialize subscription handlers for {namespaceName} state types.");
      sb.AppendLine($"    /// Call this method during application startup or when setting up the UI system.");
      sb.AppendLine("    /// </summary>");
      sb.AppendLine($"    public static void {initMethodName}()");
      sb.AppendLine("    {");

      if (generateDebugLogs) {
        sb.AppendLine($"      Debug.Log(\"StateSubscriptionHelper: Initializing subscriptions for {namespaceName} state types\");");
        sb.AppendLine();
      }

      foreach (var state in states) {
        sb.AppendLine($"      // Register handlers for {state.typeName}");
        sb.AppendLine($"      StateSubscriptionHelper.RegisterStateSubscriptionHandlers<{state.typeName}>(");
        if (generateDebugLogs) {
          sb.AppendLine($"        subscriber => {{");
          sb.AppendLine($"          Debug.Log($\"Subscribing {{subscriber.GetType().Name}} to {state.typeName} changes\");");
          sb.AppendLine($"          StateNotificationEvents.On{state.typeName}Changed += subscriber.OnStateChanged;");
          sb.AppendLine($"        }},");
          sb.AppendLine($"        subscriber => {{");
          sb.AppendLine($"          Debug.Log($\"Unsubscribing {{subscriber.GetType().Name}} from {state.typeName} changes\");");
          sb.AppendLine($"          StateNotificationEvents.On{state.typeName}Changed -= subscriber.OnStateChanged;");
          sb.AppendLine($"        }}");
        } else {
          sb.AppendLine($"        subscriber => StateNotificationEvents.On{state.typeName}Changed += subscriber.OnStateChanged,");
          sb.AppendLine($"        subscriber => StateNotificationEvents.On{state.typeName}Changed -= subscriber.OnStateChanged");
        }
        sb.AppendLine($"      );");
        sb.AppendLine();
      }

      if (generateDebugLogs) {
        sb.AppendLine($"      Debug.Log(\"StateSubscriptionHelper: Initialized handlers for {states.Count} state types in {namespaceName}\");");
      }

      sb.AppendLine("    }");
      sb.AppendLine("  }");
      sb.AppendLine("}");

      return sb.ToString();
    }

    private string generateStateChangeNotificationSystems(List<StateTypeInfo> states, string namespaceName)
    {
      var sb = new StringBuilder();

      // File header
      sb.AppendLine("// <auto-generated>");
      sb.AppendLine("// This file was automatically generated by StateSubscriptionHelper Generator");
      sb.AppendLine($"// Namespace: {namespaceName}");
      sb.AppendLine("// Do not modify this file directly - it will be overwritten");
      sb.AppendLine("// </auto-generated>");
      sb.AppendLine();
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Burst;");
      sb.AppendLine("using ECSReact.Core;");

      // Add namespace-specific using if different from ECSReact.Core
      if (namespaceName != "ECSReact.Core") {
        sb.AppendLine($"using {namespaceName};");
      }

      sb.AppendLine();
      sb.AppendLine($"namespace {namespaceName}");
      sb.AppendLine("{");

      // Generate a notification system for each state type
      foreach (var state in states) {
        sb.AppendLine($"  /// <summary>");
        sb.AppendLine($"  /// Generated system that detects {state.typeName} changes and queues UI events.");
        sb.AppendLine($"  /// Generated for namespace: {namespaceName}");
        sb.AppendLine($"  /// </summary>");

        // Add burst compile only if state implements IEquatable
        if (state.implementsIEquatable) {
          sb.AppendLine("  [BurstCompile]");
        }

        sb.AppendLine("  [UINotificationSystem]");
        sb.AppendLine($"  public partial class {state.typeName}ChangeNotificationSystem : StateChangeNotificationSystem<{state.typeName}>");
        sb.AppendLine("  {");
        sb.AppendLine($"    protected override UIEvent CreateStateChangeEvent({state.typeName} newState, {state.typeName} oldState, bool hasOldState)");
        sb.AppendLine("    {");
        sb.AppendLine($"      return new {state.typeName}ChangedEvent(newState, oldState, hasOldState);");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
      }

      sb.AppendLine("}");

      return sb.ToString();
    }

    private void previewGeneratedCode()
    {
      var selectedNamespaces = namespaceGroups.Values
          .Where(g => g.includeInGeneration && g.states.Any(s => s.includeInGeneration))
          .ToList();

      if (selectedNamespaces.Count == 0) {
        EditorUtility.DisplayDialog("No States Selected", "Please select at least one state type to preview.", "OK");
        return;
      }

      // For preview, just show the first namespace's generated code
      var firstNamespace = selectedNamespaces.First();
      var selectedStates = firstNamespace.states.Where(s => s.includeInGeneration).ToList();

      string helperCode = generateStateSubscriptionHelperCode(selectedStates, firstNamespace.namespaceName);
      string notificationSystemsCode = generateStateChangeNotificationSystems(selectedStates, firstNamespace.namespaceName);

      // Create a preview window
      var previewWindow = GetWindow<StateSubscriptionCodePreviewWindow>("Generated Code Preview");

      string title = selectedNamespaces.Count > 1 ?
          $"Preview for {firstNamespace.namespaceName} (+{selectedNamespaces.Count - 1} more namespaces)" :
          $"Preview for {firstNamespace.namespaceName}";

      previewWindow.SetPreviewContent(
          "StateSubscriptionHelper.Generated.cs", helperCode,
          "StateChangeNotificationSystems.Generated.cs", notificationSystemsCode,
          title);
    }
  }

  public class StateSubscriptionCodePreviewWindow : EditorWindow
  {
    private Vector2 scrollPosition;
    private string content1Title;
    private string content1;
    private string content2Title;
    private string content2;
    private string windowTitle;
    private int selectedTab = 0;

    public void SetPreviewContent(string title1, string code1, string title2, string code2, string windowTitle = "Generated Code Preview")
    {
      content1Title = title1;
      content1 = code1;
      content2Title = title2;
      content2 = code2;
      this.windowTitle = windowTitle;
      titleContent = new GUIContent(windowTitle);
    }

    private void OnGUI()
    {
      // Tab selection
      selectedTab = GUILayout.Toolbar(selectedTab, new string[] { content1Title, content2Title });

      EditorGUILayout.Space();

      // Display selected content
      string currentContent = selectedTab == 0 ? content1 : content2;

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
      EditorGUILayout.TextArea(currentContent, GUILayout.ExpandHeight(true));
      EditorGUILayout.EndScrollView();

      // Copy to clipboard button
      if (GUILayout.Button("Copy to Clipboard")) {
        EditorGUIUtility.systemCopyBuffer = currentContent;
        ShowNotification(new GUIContent("Copied to clipboard!"));
      }
    }
  }
}