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
  public class TurnOrderSlot : ReactiveUIComponent<BattleState, CharacterIdentityState>, IElementChild
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
    [SerializeField] private float animateDuration = 0.3f;

    private TurnOrderSlotProps currentProps;
    private Vector3 currentPosition;
    private Vector2 currentScale;
    private Coroutine animationCoroutine;
    private Coroutine pulseCoroutine;

    private BattleState battleState;
    private CharacterIdentityState identityState;

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

    public override void OnStateChanged(CharacterIdentityState newState)
    {
      identityState = newState;
      UpdateDisplay();
    }

    private void UpdateDisplay()
    {
      if (currentProps == null)
        return;

      var character = currentProps.Character;

      // Update portrait (using temp sprites for demo)
      if (portraitImage) {
        int portraitIndex = character.isEnemy ? 0 : 1;
        var entity = currentProps.Character.entity;
        if (identityState.names.IsCreated && identityState.names.TryGetValue(entity, out var name)) {
          SetPortrait(name.ToString());
        }
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
    private void SetPortrait(string characterName)
    {
      switch (characterName.ToLower()) {
        case "hero":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/hero");
          break;
        case "mage":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/wizard");
          break;
        case "warrior":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/warrior");
          break;
        case "goblin":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/goblin");
          break;
        case "orc":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/orc");
          break;

        default:
          break;
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
      float elapsed = 0f;
      float duration = animateDuration;

      while (elapsed < duration) {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        // Use easing curve
        t = Mathf.SmoothStep(0, 1, t);

        transform.localPosition = Vector3.Lerp(startPos, targetPos, t);

        yield return null;
      }

      transform.localPosition = targetPos;
      currentPosition = targetPos;
    }

    private void StartSlideAnimation()
    {
      // Called by parent when turn changes
      // Could add special effects here
      if (currentProps != null && currentProps.SlotIndex == 0) {
        if (pulseCoroutine != null)
          StopCoroutine(pulseCoroutine);

        // Animate out the current turn
        pulseCoroutine = StartCoroutine(PulseAnimation());
      }
    }

    private IEnumerator PulseAnimation()
    {
      Vector2 originalScale = Vector3.one;
      Vector2 pulseScale = originalScale * 1.3f;

      transform.localScale = originalScale;

      // Quick pulse
      float duration = animateDuration;
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