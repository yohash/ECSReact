using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.CodeGen
{
  public class UIStateNotifierGenerator : EditorWindow
  {
    private Vector2 scrollPosition;
    private Dictionary<string, NamespaceGroup> namespaceGroups = new Dictionary<string, NamespaceGroup>();
    private string outputPath = "Assets/Generated/";
    private bool autoRefreshDiscovery = false;

    [MenuItem("ECS React/Generate UIStateNotifier", priority = 201)]
    public static void ShowWindow()
    {
      GetWindow<UIStateNotifierGenerator>("UIStateNotifier Generator");
    }

    private void OnEnable()
    {
      discoverStateTypes();
    }

    private void OnGUI()
    {
      GUILayout.Label("UIStateNotifier Code Generator", EditorStyles.boldLabel);

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

      // Auto-refresh toggle
      autoRefreshDiscovery = EditorGUILayout.Toggle("Auto-refresh Discovery", autoRefreshDiscovery);

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
      if (GUILayout.Button("Generate UIStateNotifier Extensions", GUILayout.Height(30))) {
        generateUIStateNotifierExtensions();
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
          "Features:\n" +
          "• Check/uncheck entire namespaces or individual states\n" +
          "• Generated code will be placed in namespace-specific folders\n" +
          "• Each namespace generates its own initialization method\n\n" +
          "Output Structure:\n" +
          "• ECSReact.Core states → Assets/Generated/ECSReact/Core/\n" +
          "• MyGame.Logic states → Assets/Generated/MyGame/Logic/\n\n" +
          "Note: You'll need to call the generated Initialize*Events() methods in your application startup code.",
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

      group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, $"  {namespaceName} ({group.states.Count} states)", true);

      EditorGUILayout.EndHorizontal();

      // Show states in this namespace if expanded
      if (group.isExpanded) {
        EditorGUI.indentLevel++;

        foreach (var state in group.states) {
          EditorGUILayout.BeginHorizontal();

          state.includeInGeneration = EditorGUILayout.Toggle(state.includeInGeneration, GUILayout.Width(40));
          EditorGUILayout.LabelField(state.typeName, GUILayout.Width(200));
          EditorGUILayout.LabelField(state.assemblyName, EditorStyles.miniLabel, GUILayout.Width(120));

          // Priority selection
          state.eventPriority = (UIEventPriority)EditorGUILayout.EnumPopup(state.eventPriority, GUILayout.Width(80));

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

            var stateInfo = new StateTypeInfo
            {
              typeName = type.Name,
              fullTypeName = type.FullName,
              namespaceName = namespaceName,
              assemblyName = assembly.GetName().Name,
              includeInGeneration = namespaceName == "ECSReact.Core" ? false : true,
              eventPriority = UIEventPriority.High
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
                stateInfo.eventPriority = previousState.eventPriority;
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
      Debug.Log($"UIStateNotifier Generator: Discovered {totalStates} state types across {namespaceGroups.Count} namespaces");
    }

    private void generateUIStateNotifierExtensions()
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

        GenerateUIStateNotifierExtensionsForNamespace(namespaceGroup, ref generatedFiles);
        totalStatesGenerated += selectedStates.Count;
      }

      // Refresh Unity to recognize the new files
      AssetDatabase.Refresh();

      string fileList = string.Join("\n• ", generatedFiles.Select(f => f.Replace(Application.dataPath, "Assets")));

      // Generate list of initialization methods to call
      string initMethods = string.Join("\n• ", selectedNamespaces.Select(ns =>
          $"UIStateNotifier.Initialize{ns.namespaceName.Replace(".", "").Replace(" ", "")}Events()"));

      EditorUtility.DisplayDialog("Generation Complete",
          $"Generated UIStateNotifier extensions for {totalStatesGenerated} states across {selectedNamespaces.Count} namespaces.\n\n" +
          $"Files created:\n• {fileList}\n\n" +
          $"Remember to call these initialization methods in your startup code:\n• {initMethods}", "OK");
    }

    public void GenerateUIStateNotifierExtensionsForNamespace(NamespaceGroup namespaceGroup, ref List<string> generatedFiles)
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

      // Generate UIStateNotifier partial class for this namespace
      string notifierCode = generateUIStateNotifierCode(selectedStates, namespaceGroup.namespaceName);
      string notifierPath = Path.Combine(namespaceOutputPath, "UIStateNotifier.Generated.cs");
      File.WriteAllText(notifierPath, notifierCode);
      generatedFiles.Add(notifierPath);

      // Generate UI Event classes for this namespace
      string eventsCode = generateUIEventClasses(selectedStates, namespaceGroup.namespaceName);
      string eventsPath = Path.Combine(namespaceOutputPath, "UIEvents.Generated.cs");
      File.WriteAllText(eventsPath, eventsCode);
      generatedFiles.Add(eventsPath);
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

    private string generateUIStateNotifierCode(List<StateTypeInfo> states, string namespaceName)
    {
      var sb = new StringBuilder();

      // File header
      sb.AppendLine("// <auto-generated>");
      sb.AppendLine("// This file was automatically generated by UIStateNotifier Generator");
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

      sb.AppendLine();
      sb.AppendLine($"namespace {namespaceName}");
      sb.AppendLine("{");
      sb.AppendLine("  /// <summary>");
      sb.AppendLine($"  /// Generated extensions for UIStateNotifier that provide typed events for {namespaceName} state types.");
      sb.AppendLine("  /// </summary>");
      sb.AppendLine("  public static class StateNotificationEvents");
      sb.AppendLine("  {");

      // Generate static events for each state
      foreach (var state in states) {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Event fired when {state.typeName} changes.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static System.Action<{state.typeName}> On{state.typeName}Changed;");
        sb.AppendLine();
      }

      // Generate static initialization method for this namespace
      string initMethodName = $"InitializeEvents";
      sb.AppendLine("    /// <summary>");
      sb.AppendLine($"    /// Initialize event processors for {namespaceName} state types.");
      sb.AppendLine($"    /// Call this method during application startup or when setting up the UI system.");
      sb.AppendLine("    /// </summary>");
      sb.AppendLine($"    public static void {initMethodName}()");
      sb.AppendLine("    {");

      foreach (var state in states) {
        sb.AppendLine($"      ECSReact.Core.UIStateNotifier");
        sb.AppendLine($"        .RegisterEventProcessor<{namespaceName}.{state.typeName}ChangedEvent>(evt => ");
        sb.AppendLine($"          On{state.typeName}Changed?.Invoke(evt.newState));");
        if (states.IndexOf(state) < states.Count - 1) {
          sb.AppendLine();
        }
      }

      sb.AppendLine("    }");
      sb.AppendLine("  }");
      sb.AppendLine("}");

      return sb.ToString();
    }

    private string generateUIEventClasses(List<StateTypeInfo> states, string namespaceName)
    {
      var sb = new StringBuilder();

      // File header
      sb.AppendLine("// <auto-generated>");
      sb.AppendLine("// This file was automatically generated by UIStateNotifier Generator");
      sb.AppendLine($"// Namespace: {namespaceName}");
      sb.AppendLine("// Do not modify this file directly - it will be overwritten");
      sb.AppendLine("// </auto-generated>");
      sb.AppendLine();
      sb.AppendLine("using ECSReact.Core;");

      // Add namespace-specific using if different from ECSReact.Core
      if (namespaceName != "ECSReact.Core") {
        sb.AppendLine($"using {namespaceName};");
      }

      sb.AppendLine();
      sb.AppendLine($"namespace {namespaceName}");
      sb.AppendLine("{");

      // Generate event classes for each state
      foreach (var state in states) {
        sb.AppendLine($"  /// <summary>");
        sb.AppendLine($"  /// UI event fired when {state.typeName} from {namespaceName} changes.");
        sb.AppendLine($"  /// </summary>");
        sb.AppendLine($"  public class {state.typeName}ChangedEvent : UIEvent");
        sb.AppendLine("  {");
        sb.AppendLine($"    public {state.typeName} newState;");
        sb.AppendLine($"    public {state.typeName} oldState;");
        sb.AppendLine("    public bool hasOldState;");
        sb.AppendLine();
        sb.AppendLine($"    public {state.typeName}ChangedEvent({state.typeName} newState, {state.typeName} oldState, bool hasOldState)");
        sb.AppendLine("    {");
        sb.AppendLine("      this.newState = newState;");
        sb.AppendLine("      this.oldState = oldState;");
        sb.AppendLine("      this.hasOldState = hasOldState;");
        sb.AppendLine($"      this.priority = UIEventPriority.{state.eventPriority};");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        if (states.IndexOf(state) < states.Count - 1) {
          sb.AppendLine();
        }
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

      string notifierCode = generateUIStateNotifierCode(selectedStates, firstNamespace.namespaceName);
      string eventsCode = generateUIEventClasses(selectedStates, firstNamespace.namespaceName);

      // Create a preview window
      var previewWindow = GetWindow<CodePreviewWindow>("Generated Code Preview");

      string title = selectedNamespaces.Count > 1 ?
          $"Preview for {firstNamespace.namespaceName} (+{selectedNamespaces.Count - 1} more namespaces)" :
          $"Preview for {firstNamespace.namespaceName}";

      previewWindow.SetPreviewContent(
          "UIStateNotifier.Generated.cs", notifierCode,
          "UIEvents.Generated.cs", eventsCode,
          title);
    }
  }

  public class CodePreviewWindow : EditorWindow
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