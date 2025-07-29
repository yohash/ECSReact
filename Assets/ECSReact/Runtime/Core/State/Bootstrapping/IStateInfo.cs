using System;
using Unity.Collections;
using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Information about a single state type. Provides methods to manipulate states
  /// without needing compile-time type information.
  /// </summary>
  public interface IStateInfo
  {
    Type Type { get; }
    string Name { get; }
    string Namespace { get; }
    Entity CreateSingleton(EntityManager em, FixedString64Bytes name);
    object GetComponent(EntityManager em, Entity entity);
    void SetComponent(EntityManager em, Entity entity, object data);
    object DeserializeJson(string json);
  }

  /// <summary>
  /// Base implementation of IStateInfo that generated code can use or extend.
  /// </summary>
  public class StateInfoBase : IStateInfo
  {
    public Type Type { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }
    public Func<EntityManager, FixedString64Bytes, Entity> CreateSingletonFunc { get; set; }
    public Func<EntityManager, Entity, object> GetComponentFunc { get; set; }
    public Action<EntityManager, Entity, object> SetComponentAction { get; set; }
    public Func<string, object> DeserializeJsonFunc { get; set; }

    public Entity CreateSingleton(EntityManager em, FixedString64Bytes name)
    {
      return CreateSingletonFunc?.Invoke(em, name) ?? Entity.Null;
    }

    public object GetComponent(EntityManager em, Entity entity)
    {
      return GetComponentFunc?.Invoke(em, entity);
    }

    public void SetComponent(EntityManager em, Entity entity, object data)
    {
      SetComponentAction?.Invoke(em, entity, data);
    }

    public object DeserializeJson(string json)
    {
      return DeserializeJsonFunc?.Invoke(json);
    }
  }
}
