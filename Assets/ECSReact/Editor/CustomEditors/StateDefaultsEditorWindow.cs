using System;
using System.Reflection;
using UnityEngine;
using ECSReact.Core;
using UnityEditor;

namespace ECSReact.Tools
{
  /// <summary>
  /// Editor window for configuring default values for state types.
  /// </summary>
  public class StateDefaultsEditorWindow : EditorWindow
  {
    private SceneStateManager manager;
    private int configIndex;
    private SceneStateManager.StateTypeInfo stateInfo;
    private object stateInstance;
    private Vector2 scrollPosition;
    private SerializedObject serializedManager;

    public void Initialize(SceneStateManager manager, int configIndex, SceneStateManager.StateTypeInfo stateInfo)
    {
      this.manager = manager;
      this.configIndex = configIndex;
      this.stateInfo = stateInfo;
      this.serializedManager = new SerializedObject(manager);

      // Create instance and deserialize existing values
      stateInstance = Activator.CreateInstance(stateInfo.stateType);

      // Get current config values using SerializedProperty
      var stateConfigurationsProperty = serializedManager.FindProperty("stateConfigurations");
      if (configIndex >= 0 && configIndex < stateConfigurationsProperty.arraySize) {
        var configProperty = stateConfigurationsProperty.GetArrayElementAtIndex(configIndex);
        var hasDefaultValuesProperty = configProperty.FindPropertyRelative("hasDefaultValues");
        var serializedDefaultValuesProperty = configProperty.FindPropertyRelative("serializedDefaultValues");

        if (hasDefaultValuesProperty.boolValue && !string.IsNullOrEmpty(serializedDefaultValuesProperty.stringValue)) {
          try {
            JsonUtility.FromJsonOverwrite(serializedDefaultValuesProperty.stringValue, stateInstance);
          } catch {
            // Use default instance if deserialization fails
          }
        }
      }

      titleContent = new GUIContent($"Defaults: {stateInfo.typeName}");
      minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
      if (stateInfo == null || serializedManager == null)
        return;

      serializedManager.Update();

      EditorGUILayout.LabelField($"Default Values for {stateInfo.typeName}", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

      // Draw fields
      var fields = stateInfo.stateType.GetFields(BindingFlags.Public | BindingFlags.Instance);
      foreach (var field in fields) {
        DrawFieldEditor(field);
      }

      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space();

      EditorGUILayout.BeginHorizontal();

      if (GUILayout.Button("Save Defaults")) {
        SaveDefaults();
        Close();
      }

      if (GUILayout.Button("Clear Defaults")) {
        ClearDefaults();
        Close();
      }

      if (GUILayout.Button("Cancel")) {
        Close();
      }

      EditorGUILayout.EndHorizontal();
    }

    private void SaveDefaults()
    {
      var stateConfigurationsProperty = serializedManager.FindProperty("stateConfigurations");
      if (configIndex >= 0 && configIndex < stateConfigurationsProperty.arraySize) {
        var configProperty = stateConfigurationsProperty.GetArrayElementAtIndex(configIndex);
        var hasDefaultValuesProperty = configProperty.FindPropertyRelative("hasDefaultValues");
        var serializedDefaultValuesProperty = configProperty.FindPropertyRelative("serializedDefaultValues");

        serializedDefaultValuesProperty.stringValue = JsonUtility.ToJson(stateInstance);
        hasDefaultValuesProperty.boolValue = true;

        serializedManager.ApplyModifiedProperties();
      }
    }

    private void ClearDefaults()
    {
      var stateConfigurationsProperty = serializedManager.FindProperty("stateConfigurations");
      if (configIndex >= 0 && configIndex < stateConfigurationsProperty.arraySize) {
        var configProperty = stateConfigurationsProperty.GetArrayElementAtIndex(configIndex);
        var hasDefaultValuesProperty = configProperty.FindPropertyRelative("hasDefaultValues");
        var serializedDefaultValuesProperty = configProperty.FindPropertyRelative("serializedDefaultValues");

        serializedDefaultValuesProperty.stringValue = "";
        hasDefaultValuesProperty.boolValue = false;

        serializedManager.ApplyModifiedProperties();
      }
    }

    private void DrawFieldEditor(FieldInfo field)
    {
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(field.Name, GUILayout.Width(120));

      var currentValue = field.GetValue(stateInstance);
      var newValue = DrawValueEditor(currentValue, field.FieldType);

      if (!Equals(currentValue, newValue)) {
        field.SetValue(stateInstance, newValue);
      }

      EditorGUILayout.EndHorizontal();
    }

    private object DrawValueEditor(object value, Type type)
    {
      if (type == typeof(int)) {
        return EditorGUILayout.IntField((int)value);
      } else if (type == typeof(float)) {
        return EditorGUILayout.FloatField((float)value);
      } else if (type == typeof(bool)) {
        return EditorGUILayout.Toggle((bool)value);
      } else if (type == typeof(string)) {
        return EditorGUILayout.TextField((string)value ?? "");
      } else {
        EditorGUILayout.LabelField($"[{type.Name}] {value}", EditorStyles.miniLabel);
        return value;
      }
    }
  }
}
