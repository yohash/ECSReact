using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Frame-budgeted UI event queue that prevents main thread blocking.
  /// Processes events in priority order with configurable time limits per frame.
  /// </summary>
  public class UIEventQueue : MonoBehaviour
  {
    public static UIEventQueue Instance { get; private set; }

    [SerializeField] private float maxProcessingTimePerFrame = 0.5f; // 0.5ms budget per frame
    [SerializeField] private bool enableDebugLogging = false;

    private static readonly Queue<UIEvent> eventQueue = new();
    private static readonly Queue<UIEvent> highPriorityQueue = new();
    private static readonly Queue<UIEvent> criticalPriorityQueue = new();

    private int eventsProcessedThisFrame = 0;
    private int totalEventsQueued = 0;

    public static event Action<UIEvent> OnUIEventProcessed;

    void Awake()
    {
      if (Instance == null) {
        Instance = this;
        DontDestroyOnLoad(gameObject);
      } else {
        Destroy(gameObject);
      }
    }

    /// <summary>
    /// Queue a UI event for processing. Events are processed in priority order.
    /// </summary>
    public static void QueueEvent(UIEvent uiEvent)
    {
      if (Instance == null) {
        Debug.LogError("UIEventQueue instance not found! Make sure UIEventQueue is in the scene.");
        return;
      }

      Instance.totalEventsQueued++;

      switch (uiEvent.priority) {
        case UIEventPriority.Critical:
          criticalPriorityQueue.Enqueue(uiEvent);
          break;
        case UIEventPriority.High:
          highPriorityQueue.Enqueue(uiEvent);
          break;
        default: // Normal priority
          eventQueue.Enqueue(uiEvent);
          break;
      }

      if (Instance.enableDebugLogging) {
        Debug.Log($"UI Event Queued: {uiEvent.GetType().Name} (Priority: {uiEvent.priority})");
      }
    }

    void Update()
    {
      float startTime = Time.realtimeSinceStartup;
      eventsProcessedThisFrame = 0;

      // Process critical events first (no time limit)
      while (criticalPriorityQueue.Count > 0) {
        var uiEvent = criticalPriorityQueue.Dequeue();
        ProcessEvent(uiEvent);
        eventsProcessedThisFrame++;
      }

      // Process high priority events with time budget
      while (highPriorityQueue.Count > 0 &&
             (Time.realtimeSinceStartup - startTime) < maxProcessingTimePerFrame) {
        var uiEvent = highPriorityQueue.Dequeue();
        ProcessEvent(uiEvent);
        eventsProcessedThisFrame++;
      }

      // Process normal priority events with remaining time budget
      while (eventQueue.Count > 0 &&
             (Time.realtimeSinceStartup - startTime) < maxProcessingTimePerFrame) {
        var uiEvent = eventQueue.Dequeue();
        ProcessEvent(uiEvent);
        eventsProcessedThisFrame++;
      }

      if (enableDebugLogging && eventsProcessedThisFrame > 0) {
        Debug.Log($"UI Events processed this frame: {eventsProcessedThisFrame}");
      }
    }

    private void ProcessEvent(UIEvent uiEvent)
    {
      try {
        // Notify tracking systems (optional callback for debugging tools)
        OnUIEventProcessed?.Invoke(uiEvent);

        // Process the event normally
        UIStateNotifier.ProcessEvent(uiEvent);
      } catch (Exception ex) {
        Debug.LogError($"Error processing UI event {uiEvent.GetType().Name}: {ex.Message}");
      }
    }

    /// <summary>
    /// Get current queue statistics for debugging.
    /// </summary>
    public (int normal, int high, int critical, int totalQueued) GetQueueStats()
    {
      return (eventQueue.Count, highPriorityQueue.Count, criticalPriorityQueue.Count, totalEventsQueued);
    }
  }

}
