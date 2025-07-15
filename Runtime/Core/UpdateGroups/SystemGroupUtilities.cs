using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Utility class for runtime system group management.
  /// Provides helpers for enabling/disabling system groups and debugging.
  /// </summary>
  public static class SystemGroupUtilities
  {
    /// <summary>
    /// Enable or disable a specific system group at runtime.
    /// Useful for debugging or performance testing.
    /// </summary>
    public static void SetSystemGroupEnabled<T>(bool enabled) where T : ComponentSystemGroup
    {
      var world = World.DefaultGameObjectInjectionWorld;
      var systemGroup = world?.GetExistingSystemManaged<T>();

      if (systemGroup != null) {
        systemGroup.Enabled = enabled;
        UnityEngine.Debug.Log($"System group {typeof(T).Name} set to {(enabled ? "enabled" : "disabled")}");
      } else {
        UnityEngine.Debug.LogWarning($"System group {typeof(T).Name} not found in world");
      }
    }

    /// <summary>
    /// Get execution statistics for ECS-React system groups.
    /// Returns system counts and enabled status.
    /// </summary>
    public static (int middlewareCount, int cleanupCount, int uiNotificationCount, bool allEnabled) GetSystemGroupStats()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null)
        return (0, 0, 0, false);

      var middleware = world.GetExistingSystemManaged<MiddlewareSystemGroup>();
      var cleanup = world.GetExistingSystemManaged<ActionCleanupSystemGroup>();
      var uiNotification = world.GetExistingSystemManaged<UINotificationSystemGroup>();

      // Use reflection to get system counts since Systems property is not exposed
      int middlewareCount = getSystemCount(middleware);
      int cleanupCount = getSystemCount(cleanup);
      int uiNotificationCount = getSystemCount(uiNotification);

      bool allEnabled = (middleware?.Enabled ?? false) &&
                       (cleanup?.Enabled ?? false) &&
                       (uiNotification?.Enabled ?? false);

      return (middlewareCount, cleanupCount, uiNotificationCount, allEnabled);
    }

    private static int getSystemCount(ComponentSystemGroup group)
    {
      if (group == null)
        return 0;

      var systemsField = typeof(ComponentSystemGroup).GetField("m_systemsToUpdate",
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

      if (systemsField?.GetValue(group) is System.Collections.Generic.List<ComponentSystemBase> systems) {
        return systems.Count;
      }

      return 0;
    }

    /// <summary>
    /// Validate that the ECS-React architecture is properly set up.
    /// Checks for required system groups and core systems.
    /// </summary>
    public static bool ValidateArchitectureSetup()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null) {
        UnityEngine.Debug.LogError("No default ECS world found!");
        return false;
      }

      bool isValid = true;

      // Check for required system groups
      if (!CheckSystemGroup<MiddlewareSystemGroup>("MiddlewareSystemGroup"))
        isValid = false;
      if (!CheckSystemGroup<ActionCleanupSystemGroup>("ActionCleanupSystemGroup"))
        isValid = false;
      if (!CheckSystemGroup<UINotificationSystemGroup>("UINotificationSystemGroup"))
        isValid = false;

      // Check for core systems
      var actionCleanup = world.GetExistingSystemManaged<ActionCleanupSystem>();
      if (actionCleanup == null) {
        UnityEngine.Debug.LogError("ActionCleanupSystem not found! Actions will not be cleaned up.");
        isValid = false;
      }

      if (isValid) {
        UnityEngine.Debug.Log("ECS-React architecture validation passed!");
      }

      return isValid;

      bool CheckSystemGroup<T>(string name) where T : ComponentSystemGroup
      {
        var group = world.GetExistingSystemManaged<T>();
        if (group == null) {
          UnityEngine.Debug.LogError($"{name} not found! System update order may be incorrect.");
          return false;
        }
        return true;
      }
    }
  }
}