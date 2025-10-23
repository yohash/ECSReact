using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ECSReact.Core;
using Unity.Entities;
using System.Collections;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// UI component that displays enemy thinking state and action preview - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState subscription
  /// - Added CharacterIdentityState subscription
  /// - Replaced O(n) loops with O(1) HashMap lookups for names
  /// - Uses CharacterIdentityState to verify enemy status
  /// </summary>
  public class EnemyTurnIndicator : ReactiveUIComponent<BattleState, CharacterIdentityState>
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
    private CharacterIdentityState identityState;
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

    public override void OnStateChanged(CharacterIdentityState newState)
    {
      identityState = newState;
      UpdateDisplay();
    }

    protected override void Start()
    {
      base.Start();

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

      // Get active entity from battle state
      if (battleState.activeCharacterIndex >= battleState.turnOrder.Length) {
        HideIndicator();
        return;
      }

      Entity activeEntity = battleState.turnOrder[battleState.activeCharacterIndex];

      // NEW: Verify this is an enemy using CharacterIdentityState (O(1))
      if (!IsEnemy(activeEntity)) {
        HideIndicator();
        return;
      }

      // Show indicator for this enemy
      ShowIndicator(activeEntity);
    }

    /// <summary>
    /// NEW: Check if entity is an enemy using O(1) HashMap lookup.
    /// OLD: O(n) loop through PartyState.characters
    /// </summary>
    private bool IsEnemy(Entity entity)
    {
      if (!identityState.isEnemy.IsCreated)
        return false;

      if (identityState.isEnemy.TryGetValue(entity, out bool isEnemy)) {
        return isEnemy;
      }

      return false;
    }

    /// <summary>
    /// NEW: Get enemy name using O(1) HashMap lookup.
    /// OLD: O(n) loop through PartyState.characters
    /// </summary>
    private string GetEnemyName(Entity enemyEntity)
    {
      if (!identityState.names.IsCreated)
        return "Enemy";

      if (identityState.names.TryGetValue(enemyEntity, out var name)) {
        return name.ToString();
      }

      return "Enemy";
    }

    private void ShowIndicator(Entity enemy)
    {
      if (indicatorPanel == null)
        return;

      // Update enemy info using O(1) lookup
      if (enemyNameText != null) {
        string name = GetEnemyName(enemy);
        enemyNameText.text = $"{name}'s Turn";
      }

      if (statusText != null)
        statusText.text = "Thinking...";

      // Reset and show
      isThinking = true;
      thinkingTimer = 0f;
      currentEnemyTurn = enemy;

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

    /// <summary>
    /// Called when AI makes a decision (would be event-driven in full implementation).
    /// Could subscribe to AIThinkingState changes to detect this.
    /// </summary>
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

    /// <summary>
    /// NEW: Get target name using O(1) HashMap lookup.
    /// OLD: O(n) loop through PartyState.characters
    /// </summary>
    private string GetTargetName(Entity target)
    {
      if (target == Entity.Null)
        return "";

      if (!identityState.names.IsCreated)
        return "Unknown";

      if (identityState.names.TryGetValue(target, out var name)) {
        return name.ToString();
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