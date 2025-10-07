using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using System.Collections;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Individual turn slot that displays a character portrait and animates position changes.
  /// </summary>
  public class TurnOrderSlot : ReactiveUIComponent<BattleState>, IElementChild
  {
    [Header("UI References")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private Image healthBar;
    [SerializeField] private GameObject currentTurnIndicator;
    [SerializeField] private GameObject speedIndicator;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Visual Configuration")]
    [SerializeField] private Color allyFrameColor = new Color(0.2f, 0.5f, 1f);
    [SerializeField] private Color enemyFrameColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color currentTurnColor = Color.yellow;
    [SerializeField] private Sprite[] characterPortraits; // Temp portraits for demo

    private TurnOrderSlotProps currentProps;
    private Vector3 currentPosition;
    private Vector2 currentScale;
    private Coroutine animationCoroutine;

    private BattleState battleState;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as TurnOrderSlotProps;
      if (currentProps != null) {
        // Set initial position immediately
        transform.localPosition = currentProps.TargetPosition;
        transform.localScale = currentProps.TargetScale;
        currentPosition = currentProps.TargetPosition;
        currentScale = currentProps.TargetScale;

        UpdateDisplay();
      }
    }

    public void UpdateProps(UIProps props)
    {
      var newProps = props as TurnOrderSlotProps;
      if (newProps != null) {
        // Animate to new position if it changed
        if (currentProps == null ||
            currentProps.TargetPosition != newProps.TargetPosition ||
            currentProps.TargetScale != newProps.TargetScale) {
          AnimateToPosition(newProps.TargetPosition, newProps.TargetScale);
        }

        currentProps = newProps;
        UpdateDisplay();
      }
    }

    public override void OnStateChanged(BattleState newState)
    {
      // Turn slots primarily update through props

      // Detect turn changes for animation
      bool turnChanged = battleState.activeCharacterIndex != newState.activeCharacterIndex;

      if (turnChanged) {
        StartSlideAnimation();
      }

      battleState = newState;
    }

    private void UpdateDisplay()
    {
      if (currentProps == null)
        return;

      var character = currentProps.Character;

      // Update portrait (using temp sprites for demo)
      if (portraitImage && characterPortraits.Length > 0) {
        int portraitIndex = character.isEnemy ? 0 : 1;
        if (portraitIndex < characterPortraits.Length)
          portraitImage.sprite = characterPortraits[portraitIndex];
      }

      // Update frame color
      if (frameImage) {
        if (currentProps.IsCurrent)
          frameImage.color = currentTurnColor;
        else if (character.isEnemy)
          frameImage.color = enemyFrameColor;
        else
          frameImage.color = allyFrameColor;
      }

      // Update name
      if (characterNameText) {
        characterNameText.text = character.name.ToString();
        characterNameText.gameObject.SetActive(currentProps.IsCurrent);
      }

      // Update health bar
      if (healthBar) {
        float healthPercent = character.maxHealth > 0
            ? (float)character.currentHealth / character.maxHealth
            : 0f;
        healthBar.fillAmount = healthPercent;
      }

      // Update current turn indicator
      if (currentTurnIndicator)
        currentTurnIndicator.SetActive(currentProps.IsCurrent);

      // Update speed indicator (shows turn order)
      if (speedIndicator) {
        speedIndicator.SetActive(!currentProps.IsCurrent);
        if (speedText)
          speedText.text = (currentProps.SlotIndex + 1).ToString();
      }

      // Apply grayout if dead
      if (!character.isAlive) {
        var canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup)
          canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.5f;
      }
    }

    private void AnimateToPosition(Vector3 targetPos, Vector2 targetScale)
    {
      if (animationCoroutine != null)
        StopCoroutine(animationCoroutine);

      animationCoroutine = StartCoroutine(AnimateTransform(targetPos, targetScale));
    }

    private IEnumerator AnimateTransform(Vector3 targetPos, Vector2 targetScale)
    {
      Vector3 startPos = transform.localPosition;
      Vector2 startScale = transform.localScale;
      float elapsed = 0f;
      float duration = 0.3f;

      while (elapsed < duration) {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        // Use easing curve
        t = Mathf.SmoothStep(0, 1, t);

        transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
        transform.localScale = Vector2.Lerp(startScale, targetScale, t);

        yield return null;
      }

      transform.localPosition = targetPos;
      transform.localScale = targetScale;
      currentPosition = targetPos;
      currentScale = targetScale;
    }

    private void StartSlideAnimation()
    {
      // Called by parent when turn changes
      // Could add special effects here
      if (currentProps != null && currentProps.SlotIndex == 0) {
        // Animate out the current turn
        StartCoroutine(PulseAnimation());
      }
    }

    private IEnumerator PulseAnimation()
    {
      Vector2 originalScale = transform.localScale;
      Vector2 pulseScale = originalScale * 1.3f;

      // Quick pulse
      float duration = 0.2f;
      float elapsed = 0f;

      while (elapsed < duration) {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t < 0.5f) {
          transform.localScale = Vector2.Lerp(originalScale, pulseScale, t * 2);
        } else {
          transform.localScale = Vector2.Lerp(pulseScale, originalScale, (t - 0.5f) * 2);
        }

        yield return null;
      }

      transform.localScale = originalScale;
    }
  }
}