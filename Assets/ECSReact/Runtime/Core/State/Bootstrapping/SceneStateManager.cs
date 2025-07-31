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
  /// Scene State Manager that uses all registered state registries for strongly-typed state creation.
  /// This component is part of ECSReact.Core and has no direct dependencies on user code.
  /// All state discovery is handled by the editor at design time.
  /// </summary>
  public class SceneStateManager : MonoBehaviour
  {
    [SerializeField] private List<StateConfiguration> stateConfigurations = new();

    private EntityManager entityManager;
    private Dictionary<Type, Entity> stateEntities = new();
    private Dictionary<Type, IStateInfo> mergedStateInfos = new();
    private HashSet<string> duplicateStateWarnings = new();

    private void Awake()
    {
      // Check if any registries have been registered
      if (StateRegistryService.AllRegistries.Count == 0) {
        Debug.LogWarning("[SceneStateManager] No state registries found. " +
            "Make sure to generate the state registry (ECS React > Generate State Registry)");
      }
    }

    private void Start()
    {
      initializeEntityManager();
      mergeAllRegistries();
      createStateEntities();

      // Display any duplicate warnings
      foreach (var warning in duplicateStateWarnings) {
        Debug.LogWarning($"[SceneStateManager] {warning}");
      }
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

    private void mergeAllRegistries()
    {
      mergedStateInfos.Clear();
      duplicateStateWarnings.Clear();

      var allRegistries = StateRegistryService.AllRegistries;

      if (allRegistries.Count == 0) {
        Debug.LogError("[SceneStateManager] No state registries available. " +
            "State creation will be skipped. Generate and register a state registry to use this feature.");
        return;
      }

      Debug.Log($"[SceneStateManager] Merging {allRegistries.Count} state registries");

      // Track which types we've seen and from which namespaces
      var typeToNamespaces = new Dictionary<Type, List<string>>();

      // Merge all registries, collecting state info from all namespaces
      foreach (var registry in allRegistries) {
        foreach (var kvp in registry.AllStates) {
          var stateType = kvp.Key;
          var stateInfo = kvp.Value;

          // Track which namespaces contain this type
          if (!typeToNamespaces.ContainsKey(stateType)) {
            typeToNamespaces[stateType] = new List<string>();
          }
          typeToNamespaces[stateType].Add(stateInfo.Namespace);

          // Store the state info (last one wins if duplicates)
          mergedStateInfos[stateType] = stateInfo;
        }
      }

      // Check for duplicates and generate warnings
      foreach (var kvp in typeToNamespaces) {
        if (kvp.Value.Count > 1) {
          var typeName = kvp.Key.Name;
          var namespaces = string.Join(", ", kvp.Value);
          duplicateStateWarnings.Add(
            $"State type '{typeName}' found in multiple namespaces: {namespaces}. " +
            "Only one instance will be created. Consider using unique type names across namespaces.");
        }
      }

      Debug.Log($"[SceneStateManager] Merged registry contains {mergedStateInfos.Count} unique state types");
    }

    private void createStateEntities()
    {
      if (mergedStateInfos.Count == 0) {
        Debug.LogWarning("[SceneStateManager] No states found in merged registries");
        return;
      }

      // Check for enabled states that would create duplicates
      var enabledTypeNames = new HashSet<string>();
      var duplicatesInConfig = new List<string>();

      foreach (var config in stateConfigurations.Where(c => c.enabled)) {
        var baseTypeName = config.typeName.Split('.').Last();
        if (!enabledTypeNames.Add(baseTypeName)) {
          duplicatesInConfig.Add($"{config.displayName} ({config.namespaceName})");
        }
      }

      if (duplicatesInConfig.Count > 0) {
        Debug.LogError($"[SceneStateManager] Duplicate state types enabled: {string.Join(", ", duplicatesInConfig)}. " +
            "States are singletons - only enable one instance of each state type!");

        // You could choose to stop here or continue with first-one-wins
        // For now, let's continue but skip duplicates
      }

      var createdTypes = new HashSet<Type>();

      foreach (var config in stateConfigurations.Where(c => c.enabled)) {
        // Try to find the state info in our merged registry
        var stateInfo = mergedStateInfos.Values
            .FirstOrDefault(info => info.Type.FullName == config.typeName);

        if (stateInfo == null) {
          Debug.LogWarning($"State type not found in any registry: {config.typeName} ({config.displayName})");
          continue;
        }

        // Skip if we already created this type (handle duplicates)
        if (!createdTypes.Add(stateInfo.Type)) {
          Debug.LogWarning($"Skipping duplicate state creation for type: {stateInfo.Type.Name}");
          continue;
        }

        createStateEntity(config, stateInfo);
      }

      Debug.Log($"[SceneStateManager] Created {stateEntities.Count} state singleton entities");
    }

    private void createStateEntity(StateConfiguration config, IStateInfo stateInfo)
    {
      try {
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
        Debug.Log($"Created state singleton: {entityName} (from {stateInfo.Namespace})");
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

    // Runtime type accessors using the merged registry
    public Entity GetStateEntity(Type stateType)
    {
      return stateEntities.TryGetValue(stateType, out var entity) ? entity : Entity.Null;
    }

    public object GetState(Type stateType)
    {
      var entity = GetStateEntity(stateType);
      if (entity == Entity.Null || !mergedStateInfos.ContainsKey(stateType))
        return null;

      var stateInfo = mergedStateInfos[stateType];
      return stateInfo?.GetComponent(entityManager, entity);
    }

    public void SetState(Type stateType, object state)
    {
      var entity = GetStateEntity(stateType);
      if (entity == Entity.Null || !mergedStateInfos.ContainsKey(stateType))
        return;

      var stateInfo = mergedStateInfos[stateType];
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

    // Get duplicate warnings for UI display
    public IReadOnlyCollection<string> GetDuplicateWarnings()
    {
      return duplicateStateWarnings;
    }
  }
}