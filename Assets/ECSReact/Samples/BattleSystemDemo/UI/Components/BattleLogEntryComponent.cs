using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using System.Collections;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  // Props for log entries
  public class LogEntryProps : UIProps
  {
    public BattleLogEntry Entry { get; set; }
    public bool IsNewEntry { get; set; }
    public Color EntryColor { get; set; }
  }

  /// <summary>
  /// Individual log entry component with fade-in animation
  /// </summary>
  public class BattleLogEntryComponent : ReactiveUIComponent<BattleLogState>, IElementChild
  {
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI timestampText;
    [SerializeField] private Image typeIcon;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Icons")]
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite damageIcon;
    [SerializeField] private Sprite healIcon;
    [SerializeField] private Sprite turnIcon;
    [SerializeField] private Sprite systemIcon;

    private LogEntryProps currentProps;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as LogEntryProps;
      UpdateDisplay();

      // Fade in if new entry
      if (currentProps?.IsNewEntry == true && canvasGroup) {
        StartCoroutine(FadeIn());
      }
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as LogEntryProps;
      UpdateDisplay();
    }

    public override void OnStateChanged(BattleLogState newState)
    {
      // Log entries are static once created
    }

    private void UpdateDisplay()
    {
      if (currentProps == null)
        return;

      var entry = currentProps.Entry;

      // Set message text
      if (messageText) {
        messageText.text = entry.message.ToString();
        messageText.color = currentProps.EntryColor;
      }

      // Set timestamp
      if (timestampText) {
        timestampText.text = FormatTimestamp(entry.timestamp);
      }

      // Set icon based on type
      if (typeIcon) {
        typeIcon.sprite = GetIconForType(entry.logType);
        typeIcon.color = currentProps.EntryColor;
      }
    }

    private string FormatTimestamp(float timestamp)
    {
      int minutes = (int)(timestamp / 60);
      int seconds = (int)(timestamp % 60);
      return $"[{minutes:00}:{seconds:00}]";
    }

    private Sprite GetIconForType(LogType logType)
    {
      return logType switch
      {
        LogType.Action => attackIcon,
        LogType.Damage => damageIcon,
        LogType.Healing => healIcon,
        LogType.TurnChange => turnIcon,
        LogType.System => systemIcon,
        _ => null
      };
    }

    private IEnumerator FadeIn()
    {
      canvasGroup.alpha = 0f;
      float duration = 0.3f;
      float elapsed = 0f;

      while (elapsed < duration) {
        elapsed += Time.deltaTime;
        canvasGroup.alpha = elapsed / duration;
        yield return null;
      }

      canvasGroup.alpha = 1f;
    }
  }
}