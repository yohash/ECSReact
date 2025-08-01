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
    private static readonly List<IStateRegistry> _registeredRegistries = new List<IStateRegistry>();

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

        UnityEngine.Debug.Log($"[StateRegistryService] Registered state registry with" +
          $" {registry.AllStates.Count} states");
      }
    }

    /// <summary>
    /// Clear all registered registries. Useful for testing.
    /// </summary>
    public static void ClearRegistries()
    {
      _registeredRegistries.Clear();
    }

    /// <summary>
    /// Check if any registry has been registered.
    /// </summary>
    public static bool HasRegistry => _registeredRegistries != null && _registeredRegistries.Count > 0;

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