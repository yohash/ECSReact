using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ECSReact.Core;
using Unity.Entities;
using System.Collections;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// UI component that displays enemy thinking state and action preview.
  /// Shows visual feedback when enemies are making decisions.
  /// </summary>
  public class EnemyTurnIndicator : ReactiveUIComponent<BattleState, PartyState>
  {
    [Header("UI References")]
    [SerializeField] private GameObject indicatorPanel;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image thinkingSpinner;
    [SerializeField] private GameObject actionPreview;
    [SerializeField] private TextMeshProUGUI actionText;
    [SerializeField] private Image targetHighlight;

    [Header("Animation")]
    [SerializeField] private float spinnerRotationSpeed = 180f;
    [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float fadeInDuration = 0.3f;

    private BattleState battleState;
    private PartyState partyState;
    private Entity currentEnemyTurn = Entity.Null;
    private bool isThinking = false;
    private float thinkingTimer = 0f;
    private float totalThinkDuration = 1f;
    private Coroutine fadeCoroutine;

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateDisplay();
    }

    public override void OnStateChanged(PartyState newState)
    {
      partyState = newState;
      UpdateDisplay();
    }

    protected override void Start()
    {
      base.Start();

      // Subscribe to AI thinking events
      if (Store.Instance != null) {
        // In a full implementation, we'd have a proper event system
        // For now, we'll check state changes
      }

      if (indicatorPanel != null)
        indicatorPanel.SetActive(false);
    }

    protected override void OnDestroy()
    {
      if (fadeCoroutine != null)
        StopCoroutine(fadeCoroutine);

      base.OnDestroy();
    }

    protected void Update()
    {
      // Rotate thinking spinner
      if (isThinking && thinkingSpinner != null) {
        thinkingSpinner.transform.Rotate(Vector3.forward, -spinnerRotationSpeed * Time.deltaTime);

        // Update progress
        thinkingTimer += Time.deltaTime;
        if (statusText != null) {
          float progress = Mathf.Clamp01(thinkingTimer / totalThinkDuration);
          statusText.text = $"Thinking... {Mathf.RoundToInt(progress * 100)}%";
        }
      }
    }

    private void UpdateDisplay()
    {
      // Check if it's enemy turn
      bool isEnemyTurn = battleState.currentPhase == BattlePhase.EnemyTurn;

      if (!isEnemyTurn) {
        HideIndicator();
        return;
      }

      // Get active enemy
      if (battleState.activeCharacterIndex >= battleState.turnOrder.Length) {
        HideIndicator();
        return;
      }

      Entity activeEntity = battleState.turnOrder[battleState.activeCharacterIndex];

      // Find enemy in party state
      CharacterData? enemyData = null;
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == activeEntity &&
            partyState.characters[i].isEnemy) {
          enemyData = partyState.characters[i];
          break;
        }
      }

      if (!enemyData.HasValue) {
        HideIndicator();
        return;
      }

      // Show indicator for this enemy
      ShowIndicator(enemyData.Value);
    }

    private void ShowIndicator(CharacterData enemy)
    {
      if (indicatorPanel == null)
        return;

      // Update enemy info
      if (enemyNameText != null)
        enemyNameText.text = $"{enemy.name}'s Turn";

      if (statusText != null)
        statusText.text = "Thinking...";

      // Reset and show
      isThinking = true;
      thinkingTimer = 0f;

      if (!indicatorPanel.activeSelf) {
        indicatorPanel.SetActive(true);
        if (fadeCoroutine != null)
          StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeIn());
      }

      // Hide action preview initially
      if (actionPreview != null)
        actionPreview.SetActive(false);
    }

    private void HideIndicator()
    {
      if (indicatorPanel != null && indicatorPanel.activeSelf) {
        if (fadeCoroutine != null)
          StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOut());
      }

      isThinking = false;
      currentEnemyTurn = Entity.Null;
    }

    // Called when AI makes a decision (would be event-driven)
    public void OnAIDecisionMade(ActionType action, Entity target)
    {
      isThinking = false;

      if (actionPreview != null) {
        actionPreview.SetActive(true);

        if (actionText != null) {
          string targetName = GetTargetName(target);
          actionText.text = action switch
          {
            ActionType.Attack => $"Attacking {targetName}!",
            ActionType.Defend => "Defending!",
            ActionType.Skill => $"Using skill on {targetName}!",
            _ => "Acting..."
          };
        }
      }

      // Highlight target if applicable
      HighlightTarget(target);
    }

    private void HighlightTarget(Entity target)
    {
      if (target == Entity.Null || targetHighlight == null)
        return;

      // In a full implementation, we'd position this over the target
      // For now, just show/hide
      targetHighlight.gameObject.SetActive(true);
    }

    private string GetTargetName(Entity target)
    {
      if (target == Entity.Null)
        return "";

      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == target) {
          return partyState.characters[i].name.ToString();
        }
      }

      return "Unknown";
    }

    private IEnumerator FadeIn()
    {
      if (indicatorPanel == null)
        yield break;

      CanvasGroup canvasGroup = indicatorPanel.GetComponent<CanvasGroup>();
      if (canvasGroup == null)
        canvasGroup = indicatorPanel.AddComponent<CanvasGroup>();

      float elapsed = 0f;
      while (elapsed < fadeInDuration) {
        elapsed += Time.deltaTime;
        float t = elapsed / fadeInDuration;
        canvasGroup.alpha = fadeInCurve.Evaluate(t);
        yield return null;
      }

      canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
      if (indicatorPanel == null)
        yield break;

      CanvasGroup canvasGroup = indicatorPanel.GetComponent<CanvasGroup>();
      if (canvasGroup == null)
        yield break;

      float elapsed = 0f;
      while (elapsed < fadeInDuration) {
        elapsed += Time.deltaTime;
        float t = 1f - (elapsed / fadeInDuration);
        canvasGroup.alpha = fadeInCurve.Evaluate(t);
        yield return null;
      }

      canvasGroup.alpha = 0f;
      indicatorPanel.SetActive(false);
    }
  }
}