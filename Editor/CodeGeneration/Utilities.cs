using System;
using System.Linq;
using System.Collections.Generic;
using ECSReact.Core;

namespace ECSReact.Editor.CodeGeneration
{
  public static class Constants
  {
    public const string DEFAULT_OUTPUT_PATH = "Assets/_Generated/";
  }

  [Serializable]
  public class NamespaceGroup
  {
    public string namespaceName;
    public string assemblyName;
    public bool includeInGeneration;
    public bool isExpanded;
    public List<ActionTypeInfo> actions;
    public List<StateTypeInfo> states;
    public int actionCount => actions?.Count ?? 0;
    public int stateCount => states?.Count ?? 0;
  }

  [Serializable]
  public struct GenerationResult
  {
    public bool success;
    public string summary;
  }

  [Serializable]
  public class ActionTypeInfo
  {
    public string typeName;
    public string fullTypeName;
    public string namespaceName;
    public string assemblyName;
    public bool includeInGeneration;
    public List<FieldInfo> fields = new List<FieldInfo>();
  }

  [Serializable]
  public class StateTypeInfo
  {
    public string typeName;
    public Type stateType;
    public string fullTypeName;
    public string namespaceName;
    public string assemblyName;
    public bool includeInGeneration;
    public bool implementsIEquatable;
    public UIEventPriority eventPriority;
  }

  [Serializable]
  public class FieldInfo
  {
    public string fieldName;
    public string fieldType;
    public bool isOptional;
  }

  public static class CodeGenUtils
  {
    public static string GetFriendlyTypeName(Type type)
    {
      // Handle nullable types
      if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
        return GetFriendlyTypeName(Nullable.GetUnderlyingType(type)) + "?";
      }

      // Handle simple types
      if (type == typeof(int))
        return "int";
      if (type == typeof(float))
        return "float";
      if (type == typeof(bool))
        return "bool";
      if (type == typeof(string))
        return "string";
      if (type == typeof(uint))
        return "uint";
      if (type == typeof(long))
        return "long";
      if (type == typeof(ulong))
        return "ulong";
      if (type == typeof(short))
        return "short";
      if (type == typeof(ushort))
        return "ushort";
      if (type == typeof(byte))
        return "byte";
      if (type == typeof(sbyte))
        return "sbyte";
      if (type == typeof(double))
        return "double";
      if (type == typeof(decimal))
        return "decimal";

      // Handle Unity/ECS types
      if (type.Name == "Entity")
        return "Entity";
      if (type.Name == "float3")
        return "float3";
      if (type.Name == "float2")
        return "float2";
      if (type.Name == "float4")
        return "float4";
      if (type.Name == "int3")
        return "int3";
      if (type.Name == "int2")
        return "int2";
      if (type.Name == "int4")
        return "int4";
      if (type.Name == "quaternion")
        return "quaternion";

      // Handle FixedString types (non-generic)
      if (type.Name.StartsWith("FixedString") && !type.IsGenericType)
        return type.Name;

      // Handle Unity Collections types
      if (type.IsGenericType) {
        string typeName = type.Name;

        // Check if it's a Unity Collections type that we want to handle specially
        bool isUnityCollection = typeName.StartsWith("FixedList") ||
                                 typeName.StartsWith("NativeArray") ||
                                 typeName.StartsWith("NativeList") ||
                                 typeName.StartsWith("NativeHashMap") ||
                                 typeName.StartsWith("NativeHashSet") ||
                                 typeName.StartsWith("NativeMultiHashMap");

        if (isUnityCollection || type.Namespace?.StartsWith("Unity.Collections") == true) {
          // Remove the generic arity backtick notation
          int backtickIndex = typeName.IndexOf('`');
          if (backtickIndex > 0) {
            typeName = typeName.Substring(0, backtickIndex);
          }

          // Get generic arguments
          Type[] genericArgs = type.GetGenericArguments();
          string genericArgNames = string.Join(", ", genericArgs.Select(arg => GetFriendlyTypeName(arg)));

          return $"{typeName}<{genericArgNames}>";
        }
      }

      // Handle other generic types
      if (type.IsGenericType) {
        string typeName = type.Name;

        // Remove the generic arity backtick notation (e.g., `1, `2)
        int backtickIndex = typeName.IndexOf('`');
        if (backtickIndex > 0) {
          typeName = typeName.Substring(0, backtickIndex);
        }

        // Get generic arguments
        Type[] genericArgs = type.GetGenericArguments();

        // Build the full generic type name
        string genericArgNames = string.Join(", ", genericArgs.Select(arg => GetFriendlyTypeName(arg)));

        return $"{typeName}<{genericArgNames}>";
      }

      // Handle arrays
      if (type.IsArray) {
        return GetFriendlyTypeName(type.GetElementType()) + "[]";
      }

      // For everything else, use the full name if in a namespace, otherwise just the name
      if (!string.IsNullOrEmpty(type.Namespace)) {
        // For types in known Unity/ECS namespaces, use just the type name
        if (type.Namespace.StartsWith("Unity.") ||
            type.Namespace.StartsWith("UnityEngine.") ||
            type.Namespace.StartsWith("System.")) {
          return type.Name;
        }

        // For custom types, you might want to include the namespace
        // or just return the simple name based on your preference
        return type.Name;
      }

      return type.Name;
    }

    public static bool IsOptionalField(System.Reflection.FieldInfo field)
    {
      // Check if field has a default value or is nullable
      // This is a simplified check - you could expand this with attributes
      return field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>);
    }
  }
}