using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Base class for all UI events that represent state changes.
  /// UI events are queued and processed with frame budgeting to maintain 60fps.
  /// </summary>
  public abstract class UIEvent
  {
    public UIEventPriority priority = UIEventPriority.Normal;
    public float timestamp;

    protected UIEvent()
    {
      timestamp = Time.realtimeSinceStartup;
    }
  }

  /// <summary>
  /// Priority levels for UI event processing.
  /// High priority events are processed first each frame.
  /// </summary>
  public enum UIEventPriority
  {
    Normal,
    High,
    Critical
  }
}
