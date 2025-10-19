using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Core interface for state registries. User-generated code will implement this interface.
  /// This allows ECSReact.Core to remain independent of user namespaces.
  /// </summary>
  public interface IStateRegistry
  {
    IReadOnlyDictionary<Type, IStateInfo> AllStates { get; }
    IStateInfo GetStateInfo(Type type);
    Entity CreateStateSingleton(EntityManager entityManager, Type stateType, FixedString64Bytes name);
    List<Type> GetStatesByNamespace(string namespaceName);
    List<string> GetAllNamespaces();
  }
}