using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Core
{
  [Serializable]
  public class StateConfiguration
  {
    public string typeName;
    public string namespaceName;
    public string displayName;
    public bool enabled = true;
    public string serializedDefaults;
  }

  /// <summary>
  /// Scene State Manager that uses the registered state registry for strongly-typed state creation.
  /// This component is part of ECSReact.Core and has no direct dependencies on user code.
  /// All state discovery is handled by the editor at design time.
  /// </summary>
  public class SceneStateManager : MonoBehaviour
  {
    [SerializeField] private List<StateConfiguration> stateConfigurations = new();

    private EntityManager entityManager;
    private Dictionary<Type, Entity> stateEntities = new();
    private IStateRegistry stateRegistry;

    private void Awake()
    {
      // Check if a registry has been registered
      if (!StateRegistryService.HasRegistry) {
        Debug.LogWarning("[SceneStateManager] No state registry found. " +
            "Make sure to generate the state registry (ECS React > Generate State Registry)");
      }
    }

    private void Start()
    {
      initializeEntityManager();
      initializeStateRegistry();
      createStateEntities();
    }

    private void initializeEntityManager()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null) {
        Debug.LogError("No ECS World found. Make sure ECS is properly initialized.");
        return;
      }

      entityManager = world.EntityManager;
    }

    private void initializeStateRegistry()
    {
      stateRegistry = StateRegistryService.ActiveRegistry;

      if (stateRegistry == null) {
        Debug.LogError("[SceneStateManager] No state registry available. " +
            "State creation will be skipped. Generate and register a state registry to use this feature.");
        return;
      }

      Debug.Log($"[SceneStateManager] Using state registry with {stateRegistry.AllStates.Count} registered states");
    }

    private void createStateEntities()
    {
      if (stateRegistry == null) {
        Debug.LogWarning("[SceneStateManager] Cannot create states without a registered state registry");
        return;
      }

      foreach (var config in stateConfigurations.Where(c => c.enabled)) {
        createStateEntity(config);
      }

      Debug.Log($"[SceneStateManager] Created {stateEntities.Count} state singleton entities");
    }

    private void createStateEntity(StateConfiguration config)
    {
      try {
        // Try to find the state info in the registry
        var stateInfo = stateRegistry.AllStates.Values
            .FirstOrDefault(info => info.Type.FullName == config.typeName);

        if (stateInfo == null) {
          Debug.LogWarning($"State type not found in registry: {config.typeName} ({config.displayName})");
          return;
        }

        // Create the singleton entity with a proper name
        var entityName = new FixedString64Bytes($"{config.displayName}");
        var entity = stateInfo.CreateSingleton(entityManager, entityName);

        // Apply serialized defaults if available
        if (!string.IsNullOrEmpty(config.serializedDefaults)) {
          try {
            var componentData = stateInfo.DeserializeJson(config.serializedDefaults);
            stateInfo.SetComponent(entityManager, entity, componentData);
          } catch (Exception e) {
            Debug.LogWarning($"Failed to apply defaults for {config.displayName}: {e.Message}");
          }
        }

        stateEntities[stateInfo.Type] = entity;
        Debug.Log($"Created state singleton: {entityName}");
      } catch (Exception e) {
        Debug.LogError($"Failed to create state entity for {config.displayName}: {e.Message}");
      }
    }

    // Generic accessors for compile-time known types (fastest path)
    public Entity GetStateEntity<T>() where T : unmanaged, IComponentData, IGameState
    {
      return stateEntities.TryGetValue(typeof(T), out var entity) ? entity : Entity.Null;
    }

    public T GetState<T>() where T : unmanaged, IComponentData, IGameState
    {
      var entity = GetStateEntity<T>();
      return entity != Entity.Null ? entityManager.GetComponentData<T>(entity) : default;
    }

    public void SetState<T>(T state) where T : unmanaged, IComponentData, IGameState
    {
      var entity = GetStateEntity<T>();
      if (entity != Entity.Null) {
        entityManager.SetComponentData(entity, state);
      }
    }

    // Runtime type accessors using the registry (no direct dependency on generated code)
    public Entity GetStateEntity(Type stateType)
    {
      return stateEntities.TryGetValue(stateType, out var entity) ? entity : Entity.Null;
    }

    public object GetState(Type stateType)
    {
      var entity = GetStateEntity(stateType);
      if (entity == Entity.Null || stateRegistry == null)
        return null;

      var stateInfo = stateRegistry.GetStateInfo(stateType);
      return stateInfo?.GetComponent(entityManager, entity);
    }

    public void SetState(Type stateType, object state)
    {
      var entity = GetStateEntity(stateType);
      if (entity == Entity.Null || stateRegistry == null)
        return;

      var stateInfo = stateRegistry.GetStateInfo(stateType);
      stateInfo?.SetComponent(entityManager, entity, state);
    }

    // Query methods
    public bool HasState<T>() where T : unmanaged, IComponentData, IGameState
    {
      return stateEntities.ContainsKey(typeof(T));
    }

    public bool HasState(Type stateType)
    {
      return stateEntities.ContainsKey(stateType);
    }

    public IReadOnlyDictionary<Type, Entity> GetAllStateEntities()
    {
      return stateEntities;
    }
  }
}