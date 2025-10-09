using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using System.Linq;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Displays battle log entries with auto-scroll and filtering options.
  /// Demonstrates handling a stream of events from middleware.
  /// </summary>
  public class BattleLogDisplay : ReactiveUIComponent<BattleLogState>
  {
    [Header("UI References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform logEntryContainer;
    [SerializeField] private Toggle autoScrollToggle;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button collapseButton;

    [Header("Display Settings")]
    [SerializeField] private int maxLogEntries = 50;
    [SerializeField] private float entrySpacing = 5f;
    [SerializeField] private bool autoScroll = true;
    [SerializeField] private float fadeInDuration = 0.3f;

    [Header("Filter Options")]
    [SerializeField] private Toggle showActionsToggle;
    [SerializeField] private Toggle showDamageToggle;
    [SerializeField] private Toggle showSystemToggle;

    [Header("Visual States")]
    [SerializeField] private float collapsedHeight = 100f;
    [SerializeField] private float expandedHeight = 300f;

    private BattleLogState logState;
    private bool isCollapsed = false;
    private LogTypeFilter activeFilters = LogTypeFilter.All;

    public override void OnStateChanged(BattleLogState newState)
    {
      logState = newState;
      UpdateElements();
    }

    protected override void Start()
    {
      base.Start();

      // Hook up UI controls
      if (autoScrollToggle) {
        autoScrollToggle.isOn = autoScroll;
        autoScrollToggle.onValueChanged.AddListener(OnAutoScrollChanged);
      }

      if (clearButton)
        clearButton.onClick.AddListener(OnClearClicked);
      if (collapseButton)
        collapseButton.onClick.AddListener(OnCollapseClicked);

      // Filter toggles
      if (showActionsToggle)
        showActionsToggle.onValueChanged.AddListener((on) => UpdateFilter(LogTypeFilter.Actions, on));
      if (showDamageToggle)
        showDamageToggle.onValueChanged.AddListener((on) => UpdateFilter(LogTypeFilter.Damage, on));
      if (showSystemToggle)
        showSystemToggle.onValueChanged.AddListener((on) => UpdateFilter(LogTypeFilter.System, on));
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      // Filter entries based on active filters
      var filteredEntries = GetFilteredEntries();

      // Limit to max entries (keep most recent)
      var entriesToShow = filteredEntries
          .Skip(math.max(0, filteredEntries.Count - maxLogEntries))
          .ToList();

      int index = 0;
      foreach (var entry in entriesToShow) {
        yield return Mount.Element.FromResources(
            key: $"log_entry_{entry.timestamp}_{entry.message.GetHashCode()}",
            prefabPath: "UI/BattleLogEntry",
            props: new LogEntryProps
            {
              Entry = entry,
              IsNewEntry = IsRecentEntry(entry),
              EntryColor = GetLogTypeColor(entry.logType)
            },
            index: index++,
            parentTransform: logEntryContainer
        );
      }

      // Auto-scroll to bottom -- if this section runs, it's due to an update
      Canvas.ForceUpdateCanvases();
      scrollRect.verticalNormalizedPosition = 0f;
    }

    private List<BattleLogEntry> GetFilteredEntries()
    {
      var entries = new List<BattleLogEntry>();

      for (int i = 0; i < logState.entries.Length; i++) {
        var entry = logState.entries[i];
        if (ShouldShowEntry(entry)) {
          entries.Add(entry);
        }
      }

      return entries;
    }

    private bool ShouldShowEntry(BattleLogEntry entry)
    {
      return entry.logType switch
      {
        LogType.Action => activeFilters.HasFlag(LogTypeFilter.Actions),
        LogType.Damage => activeFilters.HasFlag(LogTypeFilter.Damage),
        LogType.Healing => activeFilters.HasFlag(LogTypeFilter.Damage),
        LogType.TurnChange => activeFilters.HasFlag(LogTypeFilter.Actions),
        LogType.System => activeFilters.HasFlag(LogTypeFilter.System),
        _ => true
      };
    }

    private bool IsRecentEntry(BattleLogEntry entry)
    {
      // Entry is "new" if it was added in the last second
      return Time.realtimeSinceStartup - entry.timestamp < 1f;
    }

    private Color GetLogTypeColor(LogType logType)
    {
      return logType switch
      {
        LogType.Damage => new Color(1f, 0.3f, 0.3f), // Red
        LogType.Healing => new Color(0.3f, 1f, 0.3f), // Green
        LogType.StatusEffect => new Color(0.8f, 0.8f, 0.3f), // Yellow
        LogType.TurnChange => new Color(0.5f, 0.8f, 1f), // Light Blue
        LogType.Victory => new Color(1f, 0.8f, 0.2f), // Gold
        LogType.Defeat => new Color(0.5f, 0.5f, 0.5f), // Gray
        LogType.System => new Color(0.7f, 0.7f, 0.7f), // Light Gray
        _ => Color.white
      };
    }

    private void OnAutoScrollChanged(bool enabled)
    {
      autoScroll = enabled;
    }

    private void OnClearClicked()
    {
      // Dispatch action to clear the log
      DispatchAction(new ClearBattleLogAction());
    }

    private void OnCollapseClicked()
    {
      isCollapsed = !isCollapsed;

      // Animate panel height
      var rectTransform = GetComponent<RectTransform>();
      if (rectTransform) {
        float targetHeight = isCollapsed ? collapsedHeight : expandedHeight;
        // In real implementation, would animate this
        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
      }
    }

    private void UpdateFilter(LogTypeFilter filter, bool enabled)
    {
      if (enabled)
        activeFilters |= filter;
      else
        activeFilters &= ~filter;

      UpdateElements(); // Refresh display
    }

    [System.Flags]
    private enum LogTypeFilter
    {
      Actions = 1 << 0,
      Damage = 1 << 1,
      System = 1 << 2,
      All = Actions | Damage | System
    }
  }
}