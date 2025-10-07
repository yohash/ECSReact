using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using ECSReact.Core;
using System.Collections;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Individual status effect icon that displays in CharacterStatusCard.
  /// Demonstrates IElementChild pattern for receiving props from parent.
  /// Includes tooltip functionality on hover.
  /// </summary>
  public class StatusEffectIcon : ReactiveUIComponent, IElementChild, IPointerEnterHandler, IPointerExitHandler
  {
    [Header("Icon Display")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;

    [Header("Duration Display")]
    [SerializeField] private GameObject durationContainer;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private Image durationFillBar;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipTitle;
    [SerializeField] private TextMeshProUGUI tooltipDescription;
    [SerializeField] private float tooltipDelay = 0.5f;

    [Header("Visual Configuration")]
    [SerializeField] private Color buffColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color debuffColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color neutralColor = new Color(0.5f, 0.5f, 0.8f, 1f);

    [Header("Animation")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.1f;
    [SerializeField] private bool animateOnApply = true;

    // Icon sprites - these would be loaded from Resources in a real implementation
    [Header("Status Icons (Assign in Prefab)")]
    [SerializeField] private Sprite poisonedIcon;
    [SerializeField] private Sprite stunnedIcon;
    [SerializeField] private Sprite defendingIcon;
    [SerializeField] private Sprite buffedIcon;
    [SerializeField] private Sprite weakenedIcon;

    private StatusEffectProps currentProps;
    private Coroutine tooltipCoroutine;
    private Coroutine pulseCoroutine;
    private bool isNewEffect = true;

    // Status effect metadata
    private readonly System.Collections.Generic.Dictionary<CharacterStatus, StatusEffectInfo> statusInfo =
      new System.Collections.Generic.Dictionary<CharacterStatus, StatusEffectInfo>
      {
        { CharacterStatus.Poisoned, new StatusEffectInfo
          {
            Name = "Poisoned",
            Description = "Takes damage at the end of each turn",
            IsDebuff = true
          }
        },
        { CharacterStatus.Stunned, new StatusEffectInfo
          {
            Name = "Stunned",
            Description = "Cannot take actions this turn",
            IsDebuff = true
          }
        },
        { CharacterStatus.Defending, new StatusEffectInfo
          {
            Name = "Defending",
            Description = "Takes 50% less damage until next turn",
            IsDebuff = false
          }
        },
        { CharacterStatus.Buffed, new StatusEffectInfo
          {
            Name = "Buffed",
            Description = "Attack power increased by 25%",
            IsDebuff = false
          }
        },
        { CharacterStatus.Weakened, new StatusEffectInfo
          {
            Name = "Weakened",
            Description = "Attack power decreased by 25%",
            IsDebuff = true
          }
        }
      };

    private struct StatusEffectInfo
    {
      public string Name;
      public string Description;
      public bool IsDebuff;
    }

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as StatusEffectProps;
      if (currentProps != null) {
        isNewEffect = true;
        UpdateDisplay();

        if (animateOnApply) {
          PlayApplyAnimation();
        }
      }
    }

    public void UpdateProps(UIProps props)
    {
      var newProps = props as StatusEffectProps;
      if (newProps != null) {
        // Check if this is a newly applied effect
        if (currentProps == null || currentProps.StatusType != newProps.StatusType) {
          isNewEffect = true;
          if (animateOnApply) {
            PlayApplyAnimation();
          }
        } else {
          isNewEffect = false;
        }

        currentProps = newProps;
        UpdateDisplay();
      }
    }

    private void UpdateDisplay()
    {
      if (currentProps == null)
        return;

      // Set icon sprite
      if (iconImage != null) {
        iconImage.sprite = GetStatusIcon(currentProps.StatusType);
      }

      // Set colors based on buff/debuff
      if (statusInfo.TryGetValue(currentProps.StatusType, out var info)) {
        Color effectColor = info.IsDebuff ? debuffColor : buffColor;

        if (backgroundImage != null)
          backgroundImage.color = effectColor * 0.3f;

        if (borderImage != null)
          borderImage.color = effectColor;
      }

      // Hide tooltip initially
      if (tooltipPanel != null) {
        tooltipPanel.SetActive(false);
      }

      // Start pulse animation for certain effects
      if (ShouldPulse(currentProps.StatusType)) {
        if (pulseCoroutine != null)
          StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseAnimation());
      }
    }

    private Sprite GetStatusIcon(CharacterStatus status)
    {
      // In a real implementation, these would be loaded from Resources
      // For now, return assigned sprites from inspector
      return status switch
      {
        CharacterStatus.Poisoned => poisonedIcon,
        CharacterStatus.Stunned => stunnedIcon,
        CharacterStatus.Defending => defendingIcon,
        CharacterStatus.Buffed => buffedIcon,
        CharacterStatus.Weakened => weakenedIcon,
        _ => null
      };
    }

    private bool ShouldPulse(CharacterStatus status)
    {
      // Pulse debuffs and critical effects
      return status == CharacterStatus.Poisoned ||
             status == CharacterStatus.Stunned;
    }

    private void PlayApplyAnimation()
    {
      // Scale pop animation when effect is first applied
      if (iconImage != null) {
        StartCoroutine(ApplyAnimationCoroutine());
      }
    }

    private IEnumerator ApplyAnimationCoroutine()
    {
      if (iconImage == null)
        yield break;

      Vector3 originalScale = iconImage.transform.localScale;
      iconImage.transform.localScale = Vector3.zero;

      float elapsed = 0f;
      float duration = 0.3f;

      while (elapsed < duration) {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        // Elastic ease out
        float scale = 1f + Mathf.Sin(-13f * (t + 1f) * Mathf.PI * 0.5f) * Mathf.Pow(2f, -10f * t);
        iconImage.transform.localScale = originalScale * scale;

        yield return null;
      }

      iconImage.transform.localScale = originalScale;
    }

    private IEnumerator PulseAnimation()
    {
      float time = 0f;
      Color originalColor = borderImage != null ? borderImage.color : Color.white;

      while (true) {
        time += Time.deltaTime * pulseSpeed;
        float intensity = (Mathf.Sin(time) + 1f) * 0.5f * pulseIntensity;

        if (borderImage != null) {
          Color pulseColor = originalColor;
          pulseColor.a = originalColor.a + intensity;
          borderImage.color = pulseColor;
        }

        yield return null;
      }
    }

    // Tooltip handling
    public void OnPointerEnter(PointerEventData eventData)
    {
      if (tooltipCoroutine != null)
        StopCoroutine(tooltipCoroutine);
      tooltipCoroutine = StartCoroutine(ShowTooltipDelayed());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
      if (tooltipCoroutine != null) {
        StopCoroutine(tooltipCoroutine);
        tooltipCoroutine = null;
      }

      if (tooltipPanel != null) {
        tooltipPanel.SetActive(false);
      }
    }

    private IEnumerator ShowTooltipDelayed()
    {
      yield return new WaitForSeconds(tooltipDelay);

      if (tooltipPanel != null && currentProps != null) {
        if (statusInfo.TryGetValue(currentProps.StatusType, out var info)) {
          if (tooltipTitle != null)
            tooltipTitle.text = info.Name;

          if (tooltipDescription != null) {
            string desc = info.Description;
            tooltipDescription.text = desc;
          }

          tooltipPanel.SetActive(true);

          // Position tooltip above the icon
          if (tooltipPanel.transform is RectTransform tooltipRect) {
            Vector3 pos = transform.position;
            pos.y += 50f; // Offset above icon
            tooltipPanel.transform.position = pos;
          }
        }
      }
    }

    protected override void OnDestroy()
    {
      if (tooltipCoroutine != null) {
        StopCoroutine(tooltipCoroutine);
      }

      if (pulseCoroutine != null) {
        StopCoroutine(pulseCoroutine);
      }

      base.OnDestroy();
    }

    protected override void SubscribeToStateChanges() { }
    protected override void UnsubscribeFromStateChanges() { }
  }
}