using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Scene component that manages ECS state singleton entities.
  /// Provides an interface for discovering and adding/removing state singletons per scene.
  /// </summary>
  public class SceneStateManager : MonoBehaviour
  {
    [SerializeField] private List<StateConfiguration> stateConfigurations = new List<StateConfiguration>();
    [SerializeField] private bool autoDiscoverOnAwake = true;
    [SerializeField] private bool createSingletonsOnStart = true;

    private Dictionary<string, List<StateTypeInfo>> statesByNamespace = new Dictionary<string, List<StateTypeInfo>>();
    private bool hasDiscovered = false;

    // Static registry for state creation functions - populated during discovery
    private static readonly Dictionary<Type, System.Func<object, Entity>> stateCreators
      = new Dictionary<Type, System.Func<object, Entity>>();

    [System.Serializable]
    public class StateConfiguration
    {
      public string typeName;
      public string namespaceName;
      public bool isEnabled;
      public bool hasDefaultValues;
      public string serializedDefaultValues; // JSON for default state values
    }

    private void Awake()
    {
      if (autoDiscoverOnAwake && !hasDiscovered) {
        DiscoverStates();
      }
    }

    private void Start()
    {
      if (createSingletonsOnStart && Application.isPlaying) {
        CreateEnabledSingletons();
      }
    }

    /// <summary>
    /// Discover all IGameState types and organize by namespace.
    /// Also automatically registers them for runtime creation (no external setup needed).
    /// </summary>
    public void DiscoverStates()
    {
      statesByNamespace.Clear();
      var discoveredStates = new List<StateTypeInfo>();

      var assemblies = AppDomain.CurrentDomain.GetAssemblies();

      foreach (var assembly in assemblies) {
        try {
          var types = assembly.GetTypes()
            .Where(t => t.IsValueType && !t.IsEnum && !t.IsGenericType)
            .Where(t => typeof(IComponentData).IsAssignableFrom(t))
            .Where(t => t.GetInterfaces().Any(i => i.Name == "IGameState"))
            .ToList();

          foreach (var type in types) {
            var stateInfo = new StateTypeInfo
            {
              typeName = type.Name,
              fullTypeName = type.FullName,
              stateType = type,
              namespaceName = type.Namespace ?? "Global",
              assemblyName = assembly.GetName().Name,
              hasEquatable = HasIEquatable(type)
            };

            discoveredStates.Add(stateInfo);

            // Auto-register this state type for runtime creation (self-contained!)
            RegisterStateTypeInternal(type);
          }
        } catch (Exception ex) {
          Debug.LogWarning($"Error discovering states in assembly {assembly.GetName().Name}: {ex.Message}");
        }
      }

      // Group by namespace
      foreach (var state in discoveredStates) {
        if (!statesByNamespace.ContainsKey(state.namespaceName)) {
          statesByNamespace[state.namespaceName] = new List<StateTypeInfo>();
        }
        statesByNamespace[state.namespaceName].Add(state);
      }

      // Update configurations
      SyncStateConfigurations(discoveredStates);
      hasDiscovered = true;

      Debug.Log($"SceneStateManager: Discovered and auto-registered {discoveredStates.Count} state types across {statesByNamespace.Count} namespaces");
    }

    /// <summary>
    /// Internal method to register a state type during discovery.
    /// Uses reflection once per type discovery, not at runtime.
    /// </summary>
    private void RegisterStateTypeInternal(Type stateType)
    {
      if (stateCreators.ContainsKey(stateType))
        return; // Already registered

      try {
        // Create the registration function using reflection (one-time during discovery)
        var registerMethod = typeof(SceneStateManager)
            .GetMethod("CreateStateCreatorFunction", BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(stateType);

        var creatorFunction = (System.Func<object, Entity>)registerMethod.Invoke(null, null);
        stateCreators[stateType] = creatorFunction;
      } catch (Exception ex) {
        Debug.LogError($"Failed to register state type {stateType.Name}: {ex.Message}");
      }
    }

    /// <summary>
    /// Creates a typed creator function for a specific state type.
    /// Called via reflection during discovery, not at runtime.
    /// </summary>
    private static System.Func<object, Entity> CreateStateCreatorFunction<T>() where T : unmanaged, IGameState
    {
      return (defaultJson) =>
      {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
          return Entity.Null;

        var entityManager = world.EntityManager;

        // Check if singleton already exists
        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
        if (query.CalculateEntityCount() > 0) {
          var existing = query.GetSingletonEntity();
          query.Dispose();
          return existing; // Return existing singleton
        }
        query.Dispose();

        // Create new singleton
        var entity = entityManager.CreateEntity();

        // Create state instance with defaults
        T stateInstance = default(T);
        if (!string.IsNullOrEmpty((string)defaultJson)) {
          try {
            JsonUtility.FromJsonOverwrite((string)defaultJson, stateInstance);
          } catch {
            // Use default if deserialization fails
            stateInstance = default(T);
          }
        }

        // Direct generic call - no runtime reflection!
        entityManager.AddComponentData(entity, stateInstance);
        return entity;
      };
    }


    /// <summary>
    /// Verify that enabled singletons actually exist in the ECS world.
    /// Useful for debugging and validation.
    /// </summary>
    public void VerifySingletonStates()
    {
      if (!Application.isPlaying) {
        Debug.LogWarning("Singleton verification only works during Play Mode.");
        return;
      }

      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null) {
        Debug.LogError("No ECS world found!");
        return;
      }

      var entityManager = world.EntityManager;
      var foundStates = new List<string>();
      var missingStates = new List<string>();

      foreach (var config in stateConfigurations.Where(c => c.isEnabled)) {
        var stateType = GetStateType(config.typeName);
        if (stateType == null)
          continue;

        try {
          var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly(stateType));
          var entityCount = query.CalculateEntityCount();

          if (entityCount > 0) {
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var entity = entities[0];
            entities.Dispose();

            // Try to get the entity name for better display
            string entityDisplay = $"Entity {entity.Index}:{entity.Version}";

            foundStates.Add($"{config.typeName} → {entityDisplay}");

            if (entityCount > 1) {
              Debug.LogWarning($"⚠️ Multiple singletons found for {config.typeName} ({entityCount} entities). Should be exactly 1!");
            }
          } else {
            missingStates.Add(config.typeName);
          }

          query.Dispose();
        } catch (Exception ex) {
          missingStates.Add($"{config.typeName} (error: {ex.Message})");
        }
      }

      // Report results
      if (foundStates.Count > 0) {
        Debug.Log($"✅ <color=green><b>Verified ECS Singletons Found:</b></color>\n" +
                 string.Join("\n", foundStates.Select(s => $"  • {s}")));
      }

      if (missingStates.Count > 0) {
        Debug.LogError($"❌ <color=red><b>Missing ECS Singletons:</b></color>\n" +
                      string.Join("\n", missingStates.Select(s => $"  • {s}")));
      }

      Debug.Log($"🔍 <b>Verification Complete:</b> {foundStates.Count} found, {missingStates.Count} missing");
    }


    /// <summary>
    /// Sync discovered states with serialized configurations.
    /// </summary>
    private void SyncStateConfigurations(List<StateTypeInfo> discoveredStates)
    {
      var existingConfigs = stateConfigurations.ToDictionary(c => c.typeName + c.namespaceName, c => c);
      stateConfigurations.Clear();

      foreach (var state in discoveredStates) {
        var key = state.typeName + state.namespaceName;
        var config = new StateConfiguration
        {
          typeName = state.typeName,
          namespaceName = state.namespaceName,
          isEnabled = existingConfigs.ContainsKey(key)
            ? existingConfigs[key].isEnabled : false,
          hasDefaultValues = existingConfigs.ContainsKey(key)
            ? existingConfigs[key].hasDefaultValues : false,
          serializedDefaultValues = existingConfigs.ContainsKey(key)
            ? existingConfigs[key].serializedDefaultValues : ""
        };

        stateConfigurations.Add(config);
      }
    }

    /// <summary>
    /// Create singleton entities for all enabled states.
    /// Uses pre-registered creation functions - zero reflection at runtime!
    /// </summary>
    public void CreateEnabledSingletons()
    {
      if (!Application.isPlaying)
        return;

      var createdStates = new List<string>();
      var failedStates = new List<string>();

      foreach (var config in stateConfigurations.Where(c => c.isEnabled)) {
        try {
          var stateType = GetStateType(config.typeName);
          if (stateType == null) {
            failedStates.Add($"{config.typeName} (type not found)");
            continue;
          }

          // Look up the pre-registered creation function
          if (stateCreators.TryGetValue(stateType, out var createFunction)) {
            // No reflection - direct delegate call!
            var entity = createFunction(config.serializedDefaultValues);

            if (entity != Entity.Null) {
              createdStates.Add($"{config.typeName} (Entity: {entity.Index}:{entity.Version})");
            } else {
              failedStates.Add($"{config.typeName} (entity creation failed)");
            }
          } else {
            failedStates.Add($"{config.typeName} (not registered - click Discover States first)");
          }
        } catch (Exception ex) {
          failedStates.Add($"{config.typeName} (exception: {ex.Message})");
        }
      }

      // Comprehensive logging
      if (createdStates.Count > 0) {
        Debug.Log($"✅ <color=green><b>ECS Singletons Created Successfully:</b></color>\n" +
                 string.Join("\n", createdStates.Select(s => $"  • {s}")));
      }

      if (failedStates.Count > 0) {
        Debug.LogWarning($"⚠️ <color=orange><b>ECS Singleton Creation Failed:</b></color>\n" +
                        string.Join("\n", failedStates.Select(s => $"  • {s}")));
      }

      if (createdStates.Count == 0 && failedStates.Count == 0) {
        Debug.Log("ℹ️ No states enabled for singleton creation. Check your state configurations.");
      }

      // Summary
      Debug.Log($"📊 <b>ECS Singleton Summary:</b> {createdStates.Count} created, {failedStates.Count} failed");
    }

    /// <summary>
    /// Remove singleton entities for disabled states.
    /// </summary>
    public void RemoveDisabledSingletons()
    {
      if (!Application.isPlaying)
        return;

      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null)
        return;

      var entityManager = world.EntityManager;

      foreach (var config in stateConfigurations.Where(c => !c.isEnabled)) {
        try {
          var stateType = GetStateType(config.typeName);
          if (stateType == null)
            continue;

          var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly(stateType));
          var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

          if (entities.Length > 0) {
            entityManager.DestroyEntity(entities);
            Debug.Log($"Removed singleton for {config.typeName}");
          }

          entities.Dispose();
          query.Dispose();
        } catch (Exception ex) {
          Debug.LogError($"Failed to remove singleton for {config.typeName}: {ex.Message}");
        }
      }
    }

    private Type GetStateType(string typeName)
    {
      return AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(a => a.GetTypes())
          .FirstOrDefault(t => t.Name == typeName && t.GetInterfaces().Any(i => i.Name == "IGameState"));
    }

    private bool HasIEquatable(Type type)
    {
      return type.GetInterfaces()
          .Any(i => i.IsGenericType &&
                   i.GetGenericTypeDefinition() == typeof(IEquatable<>) &&
                   i.GetGenericArguments()[0] == type);
    }

    /// <summary>
    /// Get states organized by namespace for UI display.
    /// </summary>
    public Dictionary<string, List<StateTypeInfo>> GetStatesByNamespace()
    {
      if (!hasDiscovered)
        DiscoverStates();
      return statesByNamespace;
    }

    /// <summary>
    /// Get current state configurations (read-only access for editor).
    /// </summary>
    public IReadOnlyList<StateConfiguration> GetStateConfigurations()
    {
      return stateConfigurations.AsReadOnly();
    }

    public class StateTypeInfo
    {
      public string typeName;
      public string fullTypeName;
      public Type stateType;
      public string namespaceName;
      public string assemblyName;
      public bool hasEquatable;
    }
  }
}
