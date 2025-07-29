using System;
using System.Collections.Generic;

namespace ECSReact.Core
{
  /// <summary>
  /// Static service locator for state registries. Generated code will register
  /// its implementation here at startup.
  /// </summary>
  public static class StateRegistryService
  {
    private static IStateRegistry _activeRegistry;
    private static readonly List<IStateRegistry> _registeredRegistries = new List<IStateRegistry>();

    /// <summary>
    /// The currently active state registry. Will be null if no registry has been registered.
    /// </summary>
    public static IStateRegistry ActiveRegistry => _activeRegistry;

    /// <summary>
    /// All registered state registries (in case multiple assemblies register registries).
    /// </summary>
    public static IReadOnlyList<IStateRegistry> AllRegistries => _registeredRegistries;

    /// <summary>
    /// Register a state registry implementation. Called by generated code.
    /// </summary>
    public static void RegisterRegistry(IStateRegistry registry)
    {
      if (registry == null)
        return;

      if (!_registeredRegistries.Contains(registry)) {
        _registeredRegistries.Add(registry);

        // If this is the first registry, make it active
        if (_activeRegistry == null) {
          _activeRegistry = registry;
        }

        UnityEngine.Debug.Log($"[StateRegistryService] Registered state registry with {registry.AllStates.Count} states");
      }
    }

    /// <summary>
    /// Set which registry should be active (if multiple are registered).
    /// </summary>
    public static void SetActiveRegistry(IStateRegistry registry)
    {
      if (_registeredRegistries.Contains(registry)) {
        _activeRegistry = registry;
      } else {
        UnityEngine.Debug.LogWarning("[StateRegistryService] Attempted to set unregistered registry as active");
      }
    }

    /// <summary>
    /// Clear all registered registries. Useful for testing.
    /// </summary>
    public static void ClearRegistries()
    {
      _registeredRegistries.Clear();
      _activeRegistry = null;
    }

    /// <summary>
    /// Check if any registry has been registered.
    /// </summary>
    public static bool HasRegistry => _activeRegistry != null;

    /// <summary>
    /// Utility method to get all states from all registered registries.
    /// </summary>
    public static Dictionary<Type, IStateInfo> GetAllStatesFromAllRegistries()
    {
      var allStates = new Dictionary<Type, IStateInfo>();

      foreach (var registry in _registeredRegistries) {
        foreach (var kvp in registry.AllStates) {
          // Later registries override earlier ones for the same type
          allStates[kvp.Key] = kvp.Value;
        }
      }

      return allStates;
    }
  }
}