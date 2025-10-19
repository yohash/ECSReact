using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor
{
  /// <summary>
  /// Debug helper component that can be added to GameObjects to monitor UI event flow.
  /// Useful for debugging and performance profiling.
  /// </summary>
  public class UIEventDebugger : MonoBehaviour
  {
    [SerializeField] private bool logAllEvents = false;
    [SerializeField] private bool showQueueStats = true;
    [SerializeField] private float statsUpdateInterval = 1.0f;

    private float lastStatsUpdate = 0;

    void Update()
    {
      if (showQueueStats && Time.time - lastStatsUpdate > statsUpdateInterval) {
        if (UIEventQueue.Instance != null) {
          var stats = UIEventQueue.Instance.GetQueueStats();
          Debug.Log($"UI Event Queue Stats - Normal: {stats.normal}, High: {stats.high}, Critical: {stats.critical}, Total Processed: {stats.totalQueued}");
        }
        lastStatsUpdate = Time.time;
      }
    }

    void OnEnable()
    {
      if (logAllEvents) {
        // Could subscribe to a global event logging system here
        Debug.Log("UI Event Debugger enabled - logging all events");
      }
    }
  }
}
