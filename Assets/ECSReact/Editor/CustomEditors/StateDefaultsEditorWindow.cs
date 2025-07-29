using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor
{
  public class StateDefaultsEditorWindow : EditorWindow
  {
    private Type stateType;
    private SerializedProperty configProperty;
    private object currentValue;
    private Vector2 scrollPosition;
    private bool showJson = false;
    private Dictionary<Type, IStateInfo> allDiscoveredStates; // For accessing state info

    public static void Open(Type type, SerializedProperty config, Dictionary<Type, IStateInfo> discoveredStates = null)
    {
      var window = GetWindow<StateDefaultsEditorWindow>(true, $"Edit Defaults: {type.Name}");
      window.stateType = type;
      window.configProperty = config;
      window.allDiscoveredStates = discoveredStates;
      window.LoadCurrentValue();
      window.minSize = new Vector2(450, 350);
      window.Show();
    }

    private void LoadCurrentValue()
    {
      var serializedDefaults = configProperty.FindPropertyRelative("serializedDefaults").stringValue;

      // Try to find the state info from discovered states
      IStateInfo stateInfo = null;

      // First try our passed discovered states
      if (allDiscoveredStates != null) {
        allDiscoveredStates.TryGetValue(stateType, out stateInfo);
      }

      // Fallback to discovering registries ourselves
      if (stateInfo == null) {
        var registries = DiscoverAllRegistriesStatic();
        foreach (var registry in registries) {
          stateInfo = registry.GetStateInfo(stateType);
          if (stateInfo != null)
            break;
        }
      }

      if (stateInfo != null && !string.IsNullOrEmpty(serializedDefaults)) {
        currentValue = stateInfo.DeserializeJson(serializedDefaults);
      } else {
        // Fallback to JsonUtility or Activator
        if (!string.IsNullOrEmpty(serializedDefaults)) {
          try {
            currentValue = JsonUtility.FromJson(serializedDefaults, stateType);
          } catch {
            currentValue = Activator.CreateInstance(stateType);
          }
        } else {
          currentValue = Activator.CreateInstance(stateType);
        }
      }
    }

    private static List<IStateRegistry> DiscoverAllRegistriesStatic()
    {
      var registries = new List<IStateRegistry>();
      var registryInterface = typeof(IStateRegistry);

      var registryTypes = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(a =>
          {
            try { return a.GetTypes(); } catch { return new Type[0]; }
          })
          .Where(t => t.IsClass && !t.IsAbstract && registryInterface.IsAssignableFrom(t))
          .ToList();

      foreach (var registryType in registryTypes) {
        try {
          var instanceProperty = registryType.GetProperty("Instance",
              BindingFlags.Public | BindingFlags.Static);

          if (instanceProperty != null) {
            var instance = instanceProperty.GetValue(null) as IStateRegistry;
            if (instance != null)
              registries.Add(instance);
          }
        } catch { }
      }

      return registries;
    }

    private void OnGUI()
    {
      if (stateType == null || configProperty == null) {
        Close();
        return;
      }

      // Header info
      EditorGUILayout.BeginVertical("box");
      EditorGUILayout.LabelField($"State Type: {stateType.Name}", EditorStyles.boldLabel);
      EditorGUILayout.LabelField($"Namespace: {stateType.Namespace ?? "Global"}");
      EditorGUILayout.LabelField($"Full Type: {stateType.FullName}", EditorStyles.miniLabel);
      EditorGUILayout.EndVertical();

      EditorGUILayout.Space(10);

      // Toggle between field editor and JSON view
      showJson = EditorGUILayout.Toggle("Show JSON", showJson);

      EditorGUILayout.Space(5);

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

      if (showJson) {
        // JSON view
        var json = JsonUtility.ToJson(currentValue, true);
        var newJson = EditorGUILayout.TextArea(json, GUILayout.MinHeight(200));

        if (newJson != json) {
          try {
            currentValue = JsonUtility.FromJson(newJson, stateType);
          } catch (Exception e) {
            EditorGUILayout.HelpBox($"Invalid JSON: {e.Message}", MessageType.Error);
          }
        }
      } else {
        // Field-by-field editor
        DrawFields();
      }

      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space(10);

      // Buttons
      EditorGUILayout.BeginHorizontal();

      if (GUILayout.Button("Reset to Default")) {
        currentValue = Activator.CreateInstance(stateType);
        GUI.FocusControl(null);
      }

      if (GUILayout.Button("Copy JSON")) {
        var json = JsonUtility.ToJson(currentValue, true);
        EditorGUIUtility.systemCopyBuffer = json;
        EditorUtility.DisplayDialog("Success", "JSON copied to clipboard", "OK");
      }

      GUI.backgroundColor = Color.green;
      if (GUILayout.Button("Save", GUILayout.Width(100))) {
        SaveChanges();
      }
      GUI.backgroundColor = Color.white;

      if (GUILayout.Button("Close", GUILayout.Width(60))) {
        Close();
      }

      EditorGUILayout.EndHorizontal();
    }

    private void DrawFields()
    {
      var fields = stateType.GetFields(BindingFlags.Public | BindingFlags.Instance);
      bool changed = false;

      foreach (var field in fields) {
        EditorGUILayout.BeginHorizontal();

        // Field name with tooltip showing type
        var fieldContent = new GUIContent(ObjectNames.NicifyVariableName(field.Name),
            $"Type: {field.FieldType.Name}");
        EditorGUILayout.LabelField(fieldContent, GUILayout.Width(150));

        var oldValue = field.GetValue(currentValue);
        var newValue = DrawFieldValue(field, oldValue);

        if (!Equals(oldValue, newValue)) {
          field.SetValue(currentValue, newValue);
          changed = true;
        }

        EditorGUILayout.EndHorizontal();
      }

      if (fields.Length == 0) {
        EditorGUILayout.HelpBox("This state has no editable fields.", MessageType.Info);
      }
    }

    private object DrawFieldValue(FieldInfo field, object value)
    {
      var fieldType = field.FieldType;

      // Primitive types
      if (fieldType == typeof(int))
        return EditorGUILayout.IntField((int)value);
      else if (fieldType == typeof(uint))
        return (uint)EditorGUILayout.LongField((uint)value);
      else if (fieldType == typeof(float))
        return EditorGUILayout.FloatField((float)value);
      else if (fieldType == typeof(double))
        return EditorGUILayout.DoubleField((double)value);
      else if (fieldType == typeof(bool))
        return EditorGUILayout.Toggle((bool)value);
      else if (fieldType == typeof(string))
        return EditorGUILayout.TextField((string)value ?? "");
      else if (fieldType == typeof(Vector3))
        return EditorGUILayout.Vector3Field("", (Vector3)value);
      else if (fieldType == typeof(Vector2))
        return EditorGUILayout.Vector2Field("", (Vector2)value);
      else if (fieldType == typeof(Color))
        return EditorGUILayout.ColorField((Color)value);

      // Enums
      else if (fieldType.IsEnum) {
        return EditorGUILayout.EnumPopup((Enum)value);
      }

      // Unity.Mathematics types
      else if (fieldType.Namespace == "Unity.Mathematics") {
        return DrawMathematicsType(fieldType, value);
      }

      // FixedString types
      else if (fieldType.Name.StartsWith("FixedString")) {
        return DrawFixedString(fieldType, value);
      }

      // FixedList types
      else if (fieldType.Name.StartsWith("FixedList")) {
        return DrawFixedListPreview(fieldType, value);
      }

      // Entity reference
      else if (fieldType == typeof(Entity)) {
        var entity = (Entity)value;
        EditorGUILayout.LabelField($"Entity (Index: {entity.Index}, Version: {entity.Version})");
        return entity;
      }

      // Default case
      else {
        EditorGUILayout.LabelField($"[{fieldType.Name}] - Complex type");
      }

      return value;
    }

    private object DrawMathematicsType(Type type, object value)
    {
      if (type.Name == "float2") {
        var v = (float2)value;
        var result = EditorGUILayout.Vector2Field("", new Vector2(v.x, v.y));
        return new float2(result.x, result.y);
      } else if (type.Name == "float3") {
        var v = (float3)value;
        var result = EditorGUILayout.Vector3Field("", new Vector3(v.x, v.y, v.z));
        return new float3(result.x, result.y, result.z);
      } else if (type.Name == "float4") {
        var v = (float4)value;
        var result = EditorGUILayout.Vector4Field("", new Vector4(v.x, v.y, v.z, v.w));
        return new float4(result.x, result.y, result.z, result.w);
      } else if (type.Name == "int2") {
        var v = (int2)value;
        var result = EditorGUILayout.Vector2IntField("", new Vector2Int(v.x, v.y));
        return new int2(result.x, result.y);
      } else if (type.Name == "int3") {
        var v = (int3)value;
        var result = EditorGUILayout.Vector3IntField("", new Vector3Int(v.x, v.y, v.z));
        return new int3(result.x, result.y, result.z);
      } else if (type.Name == "quaternion") {
        var q = (quaternion)value;
        var euler = math.degrees(math.Euler(q));
        var newEuler = EditorGUILayout.Vector3Field("", euler);
        return quaternion.Euler(math.radians(newEuler));
      }

      EditorGUILayout.LabelField($"[{type.Name}]");
      return value;
    }

    private object DrawFixedString(Type type, object value)
    {
      var currentString = value?.ToString() ?? "";
      var newString = EditorGUILayout.TextField(currentString);

      if (newString != currentString) {
        try {
          // Create new FixedString instance
          var ctor = type.GetConstructor(new[] { typeof(string) });
          if (ctor != null) {
            return ctor.Invoke(new object[] { newString });
          }

          // Try property setter
          var instance = Activator.CreateInstance(type);
          var valueProp = type.GetProperty("Value");
          if (valueProp != null) {
            valueProp.SetValue(instance, newString);
            return instance;
          }
        } catch (Exception e) {
          Debug.LogWarning($"Failed to update FixedString: {e.Message}");
        }
      }

      return value;
    }

    private object DrawFixedListPreview(Type type, object value)
    {
      try {
        var lengthProp = type.GetProperty("Length");
        var capacityProp = type.GetProperty("Capacity");

        if (lengthProp != null && capacityProp != null) {
          var length = (int)lengthProp.GetValue(value);
          var capacity = (int)capacityProp.GetValue(value);
          EditorGUILayout.LabelField($"FixedList (Length: {length}/{capacity})");
        } else {
          EditorGUILayout.LabelField($"[{type.Name}]");
        }
      } catch {
        EditorGUILayout.LabelField($"[{type.Name}]");
      }

      return value;
    }

    private void SaveChanges()
    {
      try {
        var json = JsonUtility.ToJson(currentValue);
        configProperty.FindPropertyRelative("serializedDefaults").stringValue = json;
        configProperty.serializedObject.ApplyModifiedProperties();

        EditorUtility.DisplayDialog("Success", "Defaults saved successfully!", "OK");
      } catch (Exception e) {
        EditorUtility.DisplayDialog("Error", $"Failed to save: {e.Message}", "OK");
      }
    }
  }
}