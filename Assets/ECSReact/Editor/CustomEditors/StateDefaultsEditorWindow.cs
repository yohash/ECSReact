using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
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
    private Dictionary<Type, IStateInfo> allDiscoveredStates;

    // Foldout states for complex types
    private Dictionary<string, bool> foldoutStates = new();
    private Dictionary<string, bool> listFoldouts = new();

    // Entity selection helpers
    private Dictionary<string, string> entityNameFields = new();
    private Dictionary<string, bool> entityNullStates = new();

    public static void Open(Type type, SerializedProperty config, Dictionary<Type, IStateInfo> discoveredStates = null)
    {
      var window = GetWindow<StateDefaultsEditorWindow>(true, $"Edit Defaults: {type.Name}");
      window.stateType = type;
      window.configProperty = config;
      window.allDiscoveredStates = discoveredStates;
      window.LoadCurrentValue();
      window.minSize = new Vector2(500, 400);
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
        foldoutStates.Clear();
        listFoldouts.Clear();
        entityNameFields.Clear();
        entityNullStates.Clear();
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

      // Force repaint if GUI changed
      if (GUI.changed) {
        Repaint();
      }
    }

    private void DrawFields()
    {
      var fields = stateType.GetFields(BindingFlags.Public | BindingFlags.Instance);

      foreach (var field in fields) {
        DrawField(field, currentValue, "");
      }

      if (fields.Length == 0) {
        EditorGUILayout.HelpBox("This state has no editable fields.", MessageType.Info);
      }
    }

    private void DrawField(FieldInfo field, object container, string pathPrefix)
    {
      var fieldPath = string.IsNullOrEmpty(pathPrefix) ? field.Name : $"{pathPrefix}.{field.Name}";
      var fieldType = field.FieldType;
      var fieldValue = field.GetValue(container);

      // Check if this is a complex type that needs a foldout
      if (IsComplexType(fieldType)) {
        foldoutStates[fieldPath] = EditorGUILayout.Foldout(
          foldoutStates.GetValueOrDefault(fieldPath, false),
          ObjectNames.NicifyVariableName(field.Name),
          true
        );

        if (!foldoutStates[fieldPath])
          return;

        EditorGUI.indentLevel++;

        // Handle different complex types
        if (IsFixedList(fieldType)) {
          var newValue = DrawFixedList(fieldType, fieldValue, fieldPath);
          // Always set the value back since FixedList is a value type
          field.SetValue(container, newValue);
        } else if (IsCustomStruct(fieldType)) {
          DrawCustomStruct(fieldType, fieldValue, fieldPath);
          field.SetValue(container, fieldValue);
        }

        EditorGUI.indentLevel--;
      } else {
        // Simple field
        EditorGUILayout.BeginHorizontal();

        var fieldContent = new GUIContent(
          ObjectNames.NicifyVariableName(field.Name),
          $"Type: {GetFriendlyTypeName(fieldType)}"
        );
        EditorGUILayout.LabelField(fieldContent, GUILayout.Width(150));

        var newValue = DrawFieldValue(fieldType, fieldValue, fieldPath);
        if (!Equals(fieldValue, newValue)) {
          field.SetValue(container, newValue);
        }

        EditorGUILayout.EndHorizontal();
      }
    }

    private bool IsComplexType(Type type)
    {
      return IsFixedList(type) || IsCustomStruct(type);
    }

    private bool IsFixedList(Type type)
    {
      return type.IsValueType && type.Name.StartsWith("FixedList");
    }

    private bool IsCustomStruct(Type type)
    {
      return type.IsValueType &&
             !type.IsPrimitive &&
             !type.IsEnum &&
             type.Namespace != "Unity.Mathematics" &&
             type != typeof(Entity) &&
             !type.Name.StartsWith("FixedString");
    }

    private object DrawFieldValue(Type fieldType, object value, string fieldPath)
    {
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

      // Unity types
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

      // Entity reference
      else if (fieldType == typeof(Entity)) {
        return DrawEntityField(value, fieldPath);
      }

      // Default case
      else {
        EditorGUILayout.LabelField($"[{GetFriendlyTypeName(fieldType)}] - Unsupported");
      }

      return value;
    }

    private object DrawEntityField(object value, string fieldPath)
    {
      var entity = (Entity)value;
      var isNull = entity == Entity.Null;

      EditorGUILayout.BeginHorizontal();

      // Null checkbox
      var nullKey = $"{fieldPath}_null";
      entityNullStates[nullKey] = EditorGUILayout.Toggle("Null", isNull, GUILayout.Width(50));

      if (entityNullStates[nullKey]) {
        EditorGUILayout.LabelField("Entity.Null", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        return Entity.Null;
      }

      // Entity name field for runtime lookup
      var nameKey = $"{fieldPath}_name";
      if (!entityNameFields.ContainsKey(nameKey)) {
        entityNameFields[nameKey] = isNull ? "" : $"Entity_{entity.Index}";
      }

      EditorGUILayout.LabelField("Name Tag:", GUILayout.Width(70));
      entityNameFields[nameKey] = EditorGUILayout.TextField(entityNameFields[nameKey], GUILayout.Width(120));

      EditorGUILayout.LabelField($"(Index: {entity.Index})", EditorStyles.miniLabel);

      EditorGUILayout.EndHorizontal();

      // For now, we can't create valid entities in editor, so return current value
      // In runtime, you'd use the name tag to find the actual entity
      return entity;
    }

    private object DrawMathematicsType(Type type, object value)
    {
      switch (type.Name) {
        case "float2":
          var v2 = (float2)value;
          var r2 = EditorGUILayout.Vector2Field("", new Vector2(v2.x, v2.y));
          return new float2(r2.x, r2.y);

        case "float3":
          var v3 = (float3)value;
          var r3 = EditorGUILayout.Vector3Field("", new Vector3(v3.x, v3.y, v3.z));
          return new float3(r3.x, r3.y, r3.z);

        case "float4":
          var v4 = (float4)value;
          var r4 = EditorGUILayout.Vector4Field("", new Vector4(v4.x, v4.y, v4.z, v4.w));
          return new float4(r4.x, r4.y, r4.z, r4.w);

        case "int2":
          var i2 = (int2)value;
          var ri2 = EditorGUILayout.Vector2IntField("", new Vector2Int(i2.x, i2.y));
          return new int2(ri2.x, ri2.y);

        case "int3":
          var i3 = (int3)value;
          var ri3 = EditorGUILayout.Vector3IntField("", new Vector3Int(i3.x, i3.y, i3.z));
          return new int3(ri3.x, ri3.y, ri3.z);

        case "quaternion":
          var q = (quaternion)value;
          var euler = math.degrees(math.Euler(q));
          var newEuler = EditorGUILayout.Vector3Field("", euler);
          return quaternion.Euler(math.radians(newEuler));

        default:
          EditorGUILayout.LabelField($"[{type.Name}]");
          return value;
      }
    }

    private object DrawFixedString(Type type, object value)
    {
      var currentString = value?.ToString() ?? "";
      var capacity = GetFixedStringCapacity(type);

      EditorGUILayout.BeginHorizontal();
      var newString = EditorGUILayout.TextField(currentString);
      EditorGUILayout.LabelField($"({newString.Length}/{capacity})", EditorStyles.miniLabel, GUILayout.Width(60));
      EditorGUILayout.EndHorizontal();

      if (newString.Length > capacity) {
        newString = newString.Substring(0, capacity);
        EditorGUILayout.HelpBox($"String truncated to {capacity} characters", MessageType.Warning);
      }

      if (newString != currentString) {
        try {
          // Create new FixedString instance
          var ctor = type.GetConstructor(new[] { typeof(string) });
          if (ctor != null) {
            return ctor.Invoke(new object[] { newString });
          }

          // Try using implicit conversion
          var implicitOp = type.GetMethod("op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null);

          if (implicitOp != null) {
            return implicitOp.Invoke(null, new object[] { newString });
          }
        } catch (Exception e) {
          Debug.LogWarning($"Failed to update FixedString: {e.Message}");
        }
      }

      return value;
    }

    private int GetFixedStringCapacity(Type type)
    {
      // Extract capacity from type name (e.g., "FixedString32Bytes" -> 32)
      var match = System.Text.RegularExpressions.Regex.Match(type.Name, @"FixedString(\d+)Bytes");
      if (match.Success && int.TryParse(match.Groups[1].Value, out int capacity)) {
        return capacity - 2; // Account for length storage
      }
      return 30; // Default fallback
    }

    private object DrawFixedList(Type listType, object listValue, string fieldPath)
    {
      // NOTE: FixedList is a value type (struct), so we must return the modified value
      // and the caller must set it back to the parent object

      var elementType = GetFixedListElementType(listType);
      if (elementType == null) {
        EditorGUILayout.LabelField("Unable to determine element type");
        return listValue;
      }

      // Get list properties via reflection
      var lengthProp = listType.GetProperty("Length");
      var capacityProp = listType.GetProperty("Capacity");
      var indexer = listType.GetProperty("Item");

      if (lengthProp == null || capacityProp == null) {
        EditorGUILayout.LabelField("Invalid FixedList type");
        return listValue;
      }

      int length = (int)lengthProp.GetValue(listValue);
      int capacity = (int)capacityProp.GetValue(listValue);

      // List header
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField($"Count: {length}/{capacity}", EditorStyles.boldLabel);

      if (length < capacity && GUILayout.Button("+", GUILayout.Width(25))) {
        // Since FixedList is a value type, we need to return the modified value
        listValue = AddFixedListElement(listType, listValue, elementType);
        length = (int)lengthProp.GetValue(listValue);
        GUI.changed = true; // Force UI refresh
      }

      if (length > 0 && GUILayout.Button("-", GUILayout.Width(25))) {
        listValue = RemoveFixedListElement(listType, listValue);
        length = (int)lengthProp.GetValue(listValue);
        GUI.changed = true; // Force UI refresh
      }

      if (GUILayout.Button("Clear", GUILayout.Width(50))) {
        listValue = ClearFixedList(listType, listValue);
        length = 0;
        GUI.changed = true; // Force UI refresh
      }

      EditorGUILayout.EndHorizontal();

      // Draw elements
      if (indexer != null && length > 0) {
        EditorGUI.indentLevel++;

        for (int i = 0; i < length; i++) {
          // For complex element types, use a foldout
          if (IsCustomStruct(elementType)) {
            var elementFoldoutKey = $"{fieldPath}[{i}]_foldout";
            listFoldouts[elementFoldoutKey] = EditorGUILayout.Foldout(
              listFoldouts.GetValueOrDefault(elementFoldoutKey, false),
              $"[{i}] {elementType.Name}",
              true
            );

            if (listFoldouts[elementFoldoutKey]) {
              EditorGUI.indentLevel++;
              var elementValue = indexer.GetValue(listValue, new object[] { i });
              DrawCustomStruct(elementType, elementValue, $"{fieldPath}[{i}]");
              indexer.SetValue(listValue, elementValue, new object[] { i });
              EditorGUI.indentLevel--;
            }
          } else {
            // Simple types - inline editing
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(40));

            var elementValue = indexer.GetValue(listValue, new object[] { i });
            var newElementValue = DrawFieldValue(elementType, elementValue, $"{fieldPath}[{i}]");

            if (!Equals(elementValue, newElementValue)) {
              indexer.SetValue(listValue, newElementValue, new object[] { i });
            }

            EditorGUILayout.EndHorizontal();
          }
        }

        EditorGUI.indentLevel--;
      }

      return listValue;
    }

    private Type GetFixedListElementType(Type listType)
    {
      // FixedList types have a generic parameter that tells us the element type
      // For FixedList128Bytes<Entity>, we need to extract Entity

      // Check if it implements IEnumerable<T>
      var enumerable = listType.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

      if (enumerable != null) {
        return enumerable.GetGenericArguments()[0];
      }

      // Fallback: try to infer from indexer
      var indexer = listType.GetProperty("Item");
      if (indexer != null) {
        return indexer.PropertyType;
      }

      return null;
    }

    private object AddFixedListElement(Type listType, object listValue, Type elementType)
    {
      var addMethod = listType.GetMethod("Add");
      if (addMethod != null) {
        // Create a default element with sensible defaults
        var defaultElement = CreateDefaultElement(elementType);

        try {
          addMethod.Invoke(listValue, new[] { defaultElement });
        } catch (Exception e) {
          Debug.LogError($"Failed to add element to FixedList: {e.Message}");
        }
      }
      return listValue;
    }

    private object CreateDefaultElement(Type elementType)
    {
      if (!elementType.IsValueType)
        return null;

      var instance = Activator.CreateInstance(elementType);

      // Set reasonable defaults for known field names/types
      var fields = elementType.GetFields();
      foreach (var field in fields) {
        // String fields
        if (field.FieldType.Name.StartsWith("FixedString")) {
          var defaultText = GetDefaultStringForField(field.Name);
          if (!string.IsNullOrEmpty(defaultText)) {
            SetFixedStringValue(field, instance, defaultText);
          }
        }
        // Numeric fields with common names
        else if (field.FieldType == typeof(int)) {
          var defaultValue = GetDefaultIntForField(field.Name);
          if (defaultValue > 0) {
            field.SetValue(instance, defaultValue);
          }
        }
        // Boolean fields
        else if (field.FieldType == typeof(bool)) {
          var defaultValue = GetDefaultBoolForField(field.Name);
          field.SetValue(instance, defaultValue);
        }
        // Entity fields
        else if (field.FieldType == typeof(Entity)) {
          field.SetValue(instance, Entity.Null);
        }
      }

      return instance;
    }

    private string GetDefaultStringForField(string fieldName)
    {
      var lowerName = fieldName.ToLower();
      if (lowerName.Contains("name"))
        return "New Character";
      if (lowerName.Contains("title"))
        return "Untitled";
      if (lowerName.Contains("description"))
        return "Description";
      return "";
    }

    private int GetDefaultIntForField(string fieldName)
    {
      var lowerName = fieldName.ToLower();
      if (lowerName.Contains("health") && lowerName.Contains("max"))
        return 100;
      if (lowerName.Contains("health") && lowerName.Contains("current"))
        return 100;
      if (lowerName.Contains("mana") && lowerName.Contains("max"))
        return 50;
      if (lowerName.Contains("mana") && lowerName.Contains("current"))
        return 50;
      if (lowerName.Contains("level"))
        return 1;
      if (lowerName.Contains("damage"))
        return 10;
      if (lowerName.Contains("defense"))
        return 5;
      return 0;
    }

    private bool GetDefaultBoolForField(string fieldName)
    {
      var lowerName = fieldName.ToLower();
      if (lowerName.Contains("alive"))
        return true;
      if (lowerName.Contains("enabled"))
        return true;
      if (lowerName.Contains("active"))
        return true;
      return false;
    }

    private void SetFixedStringValue(FieldInfo field, object instance, string value)
    {
      try {
        // Try implicit conversion first
        var implicitOp = field.FieldType.GetMethod("op_Implicit",
          BindingFlags.Public | BindingFlags.Static,
          null,
          new[] { typeof(string) },
          null);

        if (implicitOp != null) {
          var fixedString = implicitOp.Invoke(null, new object[] { value });
          field.SetValue(instance, fixedString);
        } else {
          // Fallback to constructor
          var ctor = field.FieldType.GetConstructor(new[] { typeof(string) });
          if (ctor != null) {
            field.SetValue(instance, ctor.Invoke(new object[] { value }));
          }
        }
      } catch { }
    }

    private object RemoveFixedListElement(Type listType, object listValue)
    {
      var lengthProp = listType.GetProperty("Length");
      var removeAtMethod = listType.GetMethod("RemoveAt");

      if (lengthProp != null && removeAtMethod != null) {
        int length = (int)lengthProp.GetValue(listValue);
        if (length > 0) {
          try {
            removeAtMethod.Invoke(listValue, new object[] { length - 1 });
          } catch (Exception e) {
            Debug.LogError($"Failed to remove element from FixedList: {e.Message}");
          }
        }
      }
      return listValue;
    }

    private object ClearFixedList(Type listType, object listValue)
    {
      var clearMethod = listType.GetMethod("Clear");
      if (clearMethod != null) {
        try {
          clearMethod.Invoke(listValue, null);
        } catch (Exception e) {
          Debug.LogError($"Failed to clear FixedList: {e.Message}");
        }
      }
      return listValue;
    }

    private void DrawCustomStruct(Type structType, object structValue, string pathPrefix)
    {
      var fields = structType.GetFields(BindingFlags.Public | BindingFlags.Instance);

      foreach (var field in fields) {
        DrawField(field, structValue, pathPrefix);
      }

      if (fields.Length == 0) {
        EditorGUILayout.LabelField("No fields", EditorStyles.miniLabel);
      }

      // Note: Since structs are value types, changes are made directly to structValue
      // The caller is responsible for setting the modified struct back to its container
    }

    private string GetFriendlyTypeName(Type type)
    {
      if (type.Name.StartsWith("FixedString"))
        return $"FixedString[{GetFixedStringCapacity(type)}]";

      if (type.Name.StartsWith("FixedList")) {
        var elementType = GetFixedListElementType(type);
        return elementType != null ? $"FixedList<{elementType.Name}>" : type.Name;
      }

      return type.Name;
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