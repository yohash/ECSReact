using Unity.Entities;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor
{
  /// <summary>
  /// Debug utility that logs system execution order and timing.
  /// Add this component to a GameObject to monitor system update group performance.
  /// </summary>
  public class SystemUpdateGroupDebugger : MonoBehaviour
  {
    [SerializeField] private bool logExecutionOrder = false;
    [SerializeField] private bool logTimings = false;
    [SerializeField] private float loggingInterval = 5.0f;

    private float lastLogTime = 0;

    void Update()
    {
      if ((logExecutionOrder || logTimings) &&
          Time.time - lastLogTime > loggingInterval) {
        LogSystemGroupInfo();
        lastLogTime = Time.time;
      }
    }

    private void LogSystemGroupInfo()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      if (world == null) {
        return;
      }

      if (logExecutionOrder) {
        Debug.Log("=== ECS-React System Update Order ===");
        LogSystemGroupSystems(world.GetExistingSystemManaged<MiddlewareSystemGroup>(), "Middleware");
        LogSystemGroupSystems(world.GetExistingSystemManaged<SimulationSystemGroup>(), "Simulation");
        LogSystemGroupSystems(world.GetExistingSystemManaged<ActionCleanupSystemGroup>(), "Action Cleanup");
        LogSystemGroupSystems(world.GetExistingSystemManaged<UINotificationSystemGroup>(), "UI Notification");
      }

      if (logTimings) {
        // Note: Detailed timing would require more complex profiling
        Debug.Log("System timing logging enabled - check Unity Profiler for detailed metrics");
      }
    }

    private void LogSystemGroupSystems(ComponentSystemGroup group, string groupName)
    {
      if (group == null) {
        return;
      }

      Debug.Log($"--- {groupName} Systems ---");

      // Use reflection to access the systems list since it's not directly exposed
      var systemsField = typeof(ComponentSystemGroup).GetField("m_systemsToUpdate",
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

      if (systemsField != null) {
        var systems = systemsField.GetValue(group) as System.Collections.Generic.List<ComponentSystemBase>;
        if (systems != null) {
          foreach (var system in systems) {
            Debug.Log($"  {system.GetType().Name}");
          }
        } else {
          Debug.Log($"  Could not access systems list for {groupName}");
        }
      } else {
        Debug.Log($"  System enumeration not available for {groupName}");
      }
    }
  }
}
