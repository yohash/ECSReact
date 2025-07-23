using System;
using System.Collections.Generic;
using ECSReact.Core;

namespace ECSReact.CodeGen
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
      if (type == typeof(int))
        return "int";
      if (type == typeof(float))
        return "float";
      if (type == typeof(bool))
        return "bool";
      if (type == typeof(string))
        return "string";
      if (type.Name.StartsWith("FixedString"))
        return type.Name;
      if (type.Name == "Entity")
        return "Entity";
      if (type.Name == "float3")
        return "float3";
      if (type.Name == "float2")
        return "float2";
      if (type.Name == "quaternion")
        return "quaternion";

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