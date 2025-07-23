using ECSReact.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace ECSReact.CodeGen
{
  public class StoreExtensionsGenerator : EditorWindow
  {
    private Vector2 scrollPosition;
    private Dictionary<string, NamespaceGroup> namespaceGroups = new();
    private string outputPath = Constants.DEFAULT_OUTPUT_PATH;
    private bool autoRefreshDiscovery = false;
    private bool generateXmlDocs = true;
    private bool useFluentNaming = true; // SpendMatter vs SpendMatterAction

    [MenuItem("ECS React/Generate Store Extensions", priority = 203)]
    public static void ShowWindow()
    {
      GetWindow<StoreExtensionsGenerator>("Store Extensions Generator");
    }

    private void OnEnable()
    {
      discoverActionTypes();
    }

    private void OnGUI()
    {
      GUILayout.Label("Store Action Dispatch Extensions Generator", EditorStyles.boldLabel);

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
      generateXmlDocs = EditorGUILayout.Toggle("Generate XML Documentation", generateXmlDocs);
      useFluentNaming = EditorGUILayout.Toggle("Use Fluent Naming (remove 'Action' suffix)", useFluentNaming);

      // Discovery controls
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Discover Action Types")) {
        discoverActionTypes();
      }
      if (GUILayout.Button("Clear Discovery")) {
        namespaceGroups.Clear();
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // Display discovered actions grouped by namespace
      int totalActions = namespaceGroups.Values.Sum(g => g.actions.Count);

      if (totalActions > 0) {
        EditorGUILayout.LabelField($"Discovered {totalActions} IGameAction types in {namespaceGroups.Count} namespaces:", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        foreach (var kvp in namespaceGroups.OrderBy(n => n.Key)) {
          drawNamespaceGroup(kvp.Key, kvp.Value);
          EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();
      } else {
        EditorGUILayout.HelpBox("No IGameAction types discovered. Make sure you have defined action types in your project.", MessageType.Info);
      }

      EditorGUILayout.Space();

      // Generation controls
      EditorGUILayout.BeginHorizontal();

      bool hasSelectedActions = namespaceGroups.Values.Any(g => g.includeInGeneration && g.actions.Any(a => a.includeInGeneration));
      GUI.enabled = hasSelectedActions;
      if (GUILayout.Button("Generate Store Extensions", GUILayout.Height(30))) {
        generateStoreExtensions();
      }
      GUI.enabled = true;

      if (GUILayout.Button("Preview Generated Code", GUILayout.Height(30))) {
        previewGeneratedCode();
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.Space();

      // Status/Help
      EditorGUILayout.HelpBox(
          "Actions are grouped by namespace for organized code generation.\n\n" +
          "This generator creates convenience extension methods for Store.Instance.Dispatch().\n\n" +
          "Benefits:\n" +
          "• Cleaner API: Store.SpendMatter(100) vs Store.Dispatch(new SpendMatterAction { amount = 100 })\n" +
          "• IntelliSense support with parameter names and documentation\n" +
          "• Compile-time type safety for action parameters\n" +
          "• Reduces boilerplate in UI event handlers\n\n" +
          "Output Structure:\n" +
          "• Each namespace generates its own extension methods\n" +
          "• Files organized in namespace-specific folders\n" +
          "• Generated methods are static extensions on the Store class",
          MessageType.Info);

      // Auto-refresh
      if (autoRefreshDiscovery && Event.current.type == EventType.Layout) {
        discoverActionTypes();
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
        // Update all actions in this namespace
        foreach (var action in group.actions) {
          action.includeInGeneration = newIncludeNamespace;
        }
      }

      group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, $"  {namespaceName} ({group.actions.Count} actions)", true);

      // Show namespace-level field summary
      int totalFields = group.actions.Sum(a => a.fields.Count);
      string fieldSummary = $"({totalFields} total fields)";
      EditorGUILayout.LabelField(fieldSummary, EditorStyles.miniLabel, GUILayout.Width(120));

      EditorGUILayout.EndHorizontal();

      // Show actions in this namespace if expanded
      if (group.isExpanded) {
        EditorGUI.indentLevel++;

        // widths
        var includeStyle = GUILayout.Width(60);
        var actionTypeStyle = GUILayout.MinWidth(120);
        var generatedMethodStyle = GUILayout.Width(120);
        var fieldsStyle = GUILayout.Width(60);
        var assemblyStyle = GUILayout.Width(120);

        var parametersStyle = EditorStyles.textArea;

        // Header for detailed view
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Include", EditorStyles.miniLabel, includeStyle);
        EditorGUILayout.LabelField("Action Type", EditorStyles.miniLabel, actionTypeStyle, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Generated Method", EditorStyles.miniLabel, generatedMethodStyle);
        EditorGUILayout.LabelField("Fields", EditorStyles.miniLabel, fieldsStyle);
        EditorGUILayout.LabelField("Assembly", EditorStyles.miniLabel, assemblyStyle);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        foreach (var action in group.actions) {
          EditorGUILayout.BeginHorizontal();

          action.includeInGeneration = EditorGUILayout.Toggle(action.includeInGeneration, includeStyle);
          EditorGUILayout.LabelField(action.typeName, actionTypeStyle);

          // Show generated method name
          string methodName = getMethodName(action.typeName);
          EditorGUILayout.LabelField(methodName, EditorStyles.miniLabel, generatedMethodStyle);

          // Show field count
          EditorGUILayout.LabelField($"{action.fields.Count}", EditorStyles.miniLabel, fieldsStyle);
          EditorGUILayout.LabelField(action.assemblyName, EditorStyles.miniLabel, assemblyStyle);

          EditorGUILayout.EndHorizontal();

          // Show fields in a more detailed view
          if (action.fields.Count > 0) {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(50)); // Align with checkbox

            string fieldsText = string.Join(", ", action.fields.Select(f => $"{f.fieldType} {f.fieldName}"));
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            EditorGUILayout.LabelField($"Parameters: {fieldsText}", labelStyle);
            EditorGUILayout.LabelField("", EditorStyles.miniLabel, GUILayout.Width(280));
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
          }

          EditorGUILayout.Space(1);
        }

        EditorGUI.indentLevel--;
      }

      EditorGUILayout.EndVertical();
    }

    private string getMethodName(string actionTypeName)
    {
      if (useFluentNaming && actionTypeName.EndsWith("Action")) {
        return actionTypeName.Substring(0, actionTypeName.Length - 6); // Remove "Action"
      }
      return actionTypeName;
    }

    private void discoverActionTypes()
    {
      var previousGroups = namespaceGroups.ToDictionary(a => a.Key, a => a.Value);
      namespaceGroups.Clear();

      // Get all assemblies in the project
      var assemblies = AppDomain.CurrentDomain.GetAssemblies();

      foreach (var assembly in assemblies) {
        try {
          var types = assembly.GetTypes()
              .Where(t => t.IsValueType && !t.IsEnum && !t.IsGenericType)
              .Where(t => typeof(IComponentData).IsAssignableFrom(t))
              .Where(t => t.GetInterfaces().Any(i => i.Name == "IGameAction"))
              .ToList();

          foreach (var type in types) {
            string namespaceName = type.Namespace ?? "Global";

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

            // Get or create namespace group
            if (!namespaceGroups.ContainsKey(namespaceName)) {
              namespaceGroups[namespaceName] = new NamespaceGroup
              {
                namespaceName = namespaceName,
                includeInGeneration = namespaceName == "ECSReact.Core" ? false : true,
                isExpanded = namespaceName == "ECSReact.Core" ? false : true,
                actions = new List<ActionTypeInfo>()
              };
            }

            // Preserve previous settings if they exist
            if (previousGroups.TryGetValue(namespaceName, out var previousGroup)) {
              var previousAction = previousGroup.actions.FirstOrDefault(s => s.typeName == type.Name);
              if (previousAction != null) {
                actionInfo.includeInGeneration = previousAction.includeInGeneration;
              }

              namespaceGroups[namespaceName].includeInGeneration = previousGroup.includeInGeneration;
              namespaceGroups[namespaceName].isExpanded = previousGroup.isExpanded;
            }

            namespaceGroups[namespaceName].actions.Add(actionInfo);
          }
        } catch (ReflectionTypeLoadException ex) {
          Debug.LogWarning($"Could not load types from assembly {assembly.GetName().Name}: {ex.Message}");
        } catch (Exception ex) {
          Debug.LogWarning($"Error processing assembly {assembly.GetName().Name}: {ex.Message}");
        }
      }

      //discoveredActions = discoveredActions.OrderBy(a => a.typeName).ToList();

      // Sort states within each namespace
      foreach (var group in namespaceGroups.Values) {
        group.actions = group.actions.OrderBy(s => s.typeName).ToList();
      }

      int totalStates = namespaceGroups.Values.Sum(g => g.actions.Count);
      Debug.Log($"UIStateNotifier Generator: Discovered {totalStates} state types across {namespaceGroups.Count} namespaces");
    }

    private void generateStoreExtensions()
    {
      var selectedNamespaces = namespaceGroups.Values
          .Where(g => g.includeInGeneration && g.actions.Any(s => s.includeInGeneration))
          .ToList();

      if (selectedNamespaces.Count == 0) {
        EditorUtility.DisplayDialog("No Actions Selected", "Please select at least one action type to generate.", "OK");
        return;
      }

      List<string> generatedFiles = new List<string>();
      int totalStatesGenerated = 0;

      foreach (var namespaceGroup in selectedNamespaces) {
        var selectedStates = namespaceGroup.actions.Where(s => s.includeInGeneration).ToList();
        if (selectedStates.Count == 0) {
          continue;
        }

        GenerateStoreExtensionsForNamespace(namespaceGroup, ref generatedFiles);
        totalStatesGenerated += selectedStates.Count;
      }

      // Refresh Unity to recognize the new files
      AssetDatabase.Refresh();

      string fileList = string.Join("\n• ", generatedFiles.Select(f => f.Replace(Application.dataPath, "Assets")));

      EditorUtility.DisplayDialog("Generation Complete",
          $"Generated Store extensions for {selectedNamespaces.Count} actions.\n\n" +
          $"Files created:\n• {fileList}\n\n" +
          "You can now use typed dispatch methods like Store.Instance.SpendMatter(100) in your UI code!", "OK");
    }

    public void GenerateStoreExtensionsForNamespace(NamespaceGroup namespaceGroup, ref List<string> generatedFiles)
    {
      if (namespaceGroup.states.Count == 0) {
        Debug.LogWarning($"No states found for namespace {namespaceGroup.namespaceName}");
        return;
      }
      var selectedStates = namespaceGroup.actions.Where(s => s.includeInGeneration).ToList();
      if (selectedStates.Count == 0) {
        Debug.LogWarning($"No states selected for generation in namespace {namespaceGroup.namespaceName}");
        return;
      }

      // Create namespace-specific output directory
      string namespaceOutputPath = createNamespaceOutputPath(namespaceGroup.namespaceName);

      // Generate UIStateNotifier partial class for this namespace
      string code = generateStoreExtensionsCode(selectedStates, namespaceGroup.namespaceName);
      string path = Path.Combine(namespaceOutputPath, "StoreExtensions.Generated.cs");
      File.WriteAllText(path, code);
      generatedFiles.Add(path);
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

    private string generateStoreExtensionsCode(List<ActionTypeInfo> actions, string namespaceName)
    {
      var sb = new StringBuilder();

      // File header
      sb.AppendLine("// <auto-generated>");
      sb.AppendLine("// This file was automatically generated by Store Extensions Generator");
      sb.AppendLine("// Do not modify this file directly - it will be overwritten");
      sb.AppendLine("// </auto-generated>");
      sb.AppendLine();
      sb.AppendLine("using System;");
      sb.AppendLine("using Unity.Entities;");
      sb.AppendLine("using Unity.Mathematics;");
      sb.AppendLine("using Unity.Collections;");
      sb.AppendLine("using ECSReact.Core;");
      sb.AppendLine();
      sb.AppendLine($"namespace {namespaceName}");
      sb.AppendLine("{");
      sb.AppendLine("  /// <summary>");
      sb.AppendLine("  /// Generated extension methods for Store that provide typed action dispatch methods.");
      sb.AppendLine("  /// These methods eliminate boilerplate and provide IntelliSense support for action parameters.");
      sb.AppendLine("  /// </summary>");
      sb.AppendLine("  public static class StoreExtensions");
      sb.AppendLine("  {");

      foreach (var action in actions) {
        generateActionExtensionMethod(sb, action);
        sb.AppendLine();
      }

      sb.AppendLine("  }");
      sb.AppendLine("}");

      return sb.ToString();
    }

    private void generateActionExtensionMethod(StringBuilder sb, ActionTypeInfo action)
    {
      string methodName = getMethodName(action.typeName);

      // Generate XML documentation
      if (generateXmlDocs) {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Dispatch a {action.typeName} to the ECS world.");
        sb.AppendLine("    /// </summary>");

        foreach (var field in action.fields) {
          sb.AppendLine($"    /// <param name=\"{field.fieldName}\">The {field.fieldName} value for the action.</param>");
        }

        sb.AppendLine("    /// <returns>True if the action was dispatched successfully, false if Store instance is not available.</returns>");
      }

      // Generate method signature
      sb.Append($"    public static bool {methodName}(this Store store");

      foreach (var field in action.fields) {
        sb.Append($", {field.fieldType} {field.fieldName}");
      }

      sb.AppendLine(")");
      sb.AppendLine("    {");
      sb.AppendLine("      if (store == null)");
      sb.AppendLine("      {");
      sb.AppendLine($"        UnityEngine.Debug.LogError(\"Store instance is null when dispatching {action.typeName}\");");
      sb.AppendLine("        return false;");
      sb.AppendLine("      }");
      sb.AppendLine();

      // Create and dispatch the action
      sb.AppendLine($"      var action = new {action.typeName}");
      sb.AppendLine("      {");

      foreach (var field in action.fields) {
        sb.AppendLine($"        {field.fieldName} = {field.fieldName},");
      }

      sb.AppendLine("      };");
      sb.AppendLine();
      sb.AppendLine("      store.Dispatch(action);");
      sb.AppendLine("      return true;");
      sb.AppendLine("    }");

      // Generate overload for static access
      if (generateXmlDocs) {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Dispatch a {action.typeName} using the Store singleton instance.");
        sb.AppendLine("    /// </summary>");

        foreach (var field in action.fields) {
          sb.AppendLine($"    /// <param name=\"{field.fieldName}\">The {field.fieldName} value for the action.</param>");
        }

        sb.AppendLine("    /// <returns>True if the action was dispatched successfully, false if Store instance is not available.</returns>");
      }

      sb.Append($"    public static bool {methodName}(");

      for (int i = 0; i < action.fields.Count; i++) {
        var field = action.fields[i];
        if (i > 0)
          sb.Append(", ");
        sb.Append($"{field.fieldType} {field.fieldName}");
      }

      sb.AppendLine(")");
      sb.AppendLine("    {");
      sb.AppendLine("      if (Store.Instance == null)");
      sb.AppendLine("      {");
      sb.AppendLine($"        UnityEngine.Debug.LogError(\"Store.Instance is null when dispatching {action.typeName}\");");
      sb.AppendLine("        return false;");
      sb.AppendLine("      }");
      sb.AppendLine();
      sb.Append($"      return Store.Instance.{methodName}(");

      for (int i = 0; i < action.fields.Count; i++) {
        var field = action.fields[i];
        if (i > 0)
          sb.Append(", ");
        sb.Append(field.fieldName);
      }

      sb.AppendLine(");");
      sb.AppendLine("    }");
    }

    private void previewGeneratedCode()
    {
      var selectedActions = namespaceGroups.SelectMany(g => g.Value.actions).Where(a => a.includeInGeneration).ToList();

      if (selectedActions.Count == 0) {
        EditorUtility.DisplayDialog("No Actions Selected", "Please select at least one action type to preview.", "OK");
        return;
      }

      string extensionsCode = generateStoreExtensionsCode(selectedActions, "ECSReact.Core");

      // Create a preview window
      var previewWindow = GetWindow<StoreExtensionsCodePreviewWindow>("Generated Code Preview");
      previewWindow.SetPreviewContent("StoreExtensions.Generated.cs", extensionsCode);
    }
  }


  public class StoreExtensionsCodePreviewWindow : EditorWindow
  {
    private Vector2 scrollPosition;
    private string content;
    private string contentTitle;
    private string windowTitle;

    public void SetPreviewContent(string title, string code, string windowTitle = "Generated Code Preview")
    {
      contentTitle = title;
      content = code;
      this.windowTitle = windowTitle;
      titleContent = new GUIContent(windowTitle);
    }

    private void OnGUI()
    {
      GUILayout.Label(contentTitle, EditorStyles.boldLabel);
      EditorGUILayout.Space();

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
      EditorGUILayout.TextArea(content, GUILayout.ExpandHeight(true));
      EditorGUILayout.EndScrollView();

      // Copy to clipboard button
      if (GUILayout.Button("Copy to Clipboard")) {
        EditorGUIUtility.systemCopyBuffer = content;
        ShowNotification(new GUIContent("Copied to clipboard!"));
      }
    }
  }
}