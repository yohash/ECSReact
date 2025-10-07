using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ECSReact.Core;
using Unity.Entities;
using Unity.Collections;

namespace ECSReact.Samples.BattleSystem
{
  public class TargetSelectionProps : UIProps
  {
    public Entity ActiveCharacter { get; set; }
    public ActionType ActionType { get; set; }
    public int SelectedSkillId { get; set; }
  }

  /// <summary>
  /// Target selection panel - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState subscription
  /// - Added CharacterIdentityState, CharacterHealthState subscriptions
  /// - GetCharacterData() loop → O(1) HashMap lookups from multiple states
  /// - GetCharacterName() loop → O(1) lookup from CharacterIdentityState
  /// </summary>
  public class TargetSelectionPanel : ReactiveUIComponent<CharacterIdentityState, CharacterHealthState, BattleState, UIBattleState>, IElementChild
  {
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI targetNameText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private GameObject confirmPanel;

    [Header("Visual Elements")]
    [SerializeField] private Image backgroundOverlay;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private CharacterIdentityState identityState;
    private CharacterHealthState healthState;
    private BattleState battleState;
    private UIBattleState uiState;
    private TargetSelectionProps currentProps;

    private Entity selectedTarget = Entity.Null;
    private float fadeTimer = 0f;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as TargetSelectionProps;
      UpdateInstructions();
      StartFadeIn();
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as TargetSelectionProps;
      UpdateInstructions();
    }

    public override void OnStateChanged(CharacterIdentityState newState)
    {
      identityState = newState;
      UpdateTargetInfo();
    }

    public override void OnStateChanged(CharacterHealthState newState)
    {
      healthState = newState;
      UpdateTargetInfo();
    }

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateTargetInfo();
    }

    public override void OnStateChanged(UIBattleState newState)
    {
      uiState = newState;
      selectedTarget = newState.selectedTarget;

      UpdateInstructions();
      UpdateTargetInfo();
      UpdateConfirmButton();
    }

    protected override void Start()
    {
      base.Start();

      if (confirmButton)
        confirmButton.onClick.AddListener(OnConfirmClicked);

      if (cancelButton)
        cancelButton.onClick.AddListener(OnCancelClicked);
    }

    void Update()
    {
      // Fade in animation
      if (fadeTimer < fadeInDuration) {
        fadeTimer += Time.deltaTime;
        float t = fadeTimer / fadeInDuration;
        float alpha = fadeInCurve.Evaluate(t);

        if (canvasGroup != null) {
          canvasGroup.alpha = alpha;
        }
      }
    }

    private void StartFadeIn()
    {
      fadeTimer = 0f;
      if (canvasGroup != null) {
        canvasGroup.alpha = 0f;
      }
    }

    // ========================================================================
    // NORMALIZED LOOKUPS - O(1) instead of O(n) loops
    // ========================================================================

    private void UpdateInstructions()
    {
      if (instructionText == null || currentProps == null)
        return;

      string actionName = FormatActionName(currentProps.ActionType);
      string targetType = DetermineTargetType();

      if (selectedTarget != Entity.Null) {
        // Target is selected, show confirm prompt
        var targetName = GetCharacterName(selectedTarget);
        instructionText.text = $"Confirm {actionName} on <color=yellow>{targetName}</color>?";
      } else {
        // No target selected yet
        instructionText.text = $"Select {targetType} for {actionName}";
      }
    }

    private void UpdateTargetInfo()
    {
      if (targetNameText == null)
        return;

      if (selectedTarget != Entity.Null) {
        // NEW: Lookup character data from normalized states
        if (GetCharacterHealth(selectedTarget, out var health) &&
            GetCharacterIdentity(selectedTarget, out var name, out var isEnemy)) {
          targetNameText.text = name.ToString();

          // Color based on enemy/ally
          if (isEnemy) {
            targetNameText.color = new Color(1f, 0.3f, 0.3f); // Red for enemies
          } else {
            targetNameText.color = new Color(0.3f, 1f, 0.3f); // Green for allies
          }

          // Show health status
          float healthPercent = health.max > 0
            ? (float)health.current / health.max
            : 0f;

          targetNameText.text += $"\n<size=18>HP: {health.current}/{health.max} ({Mathf.RoundToInt(healthPercent * 100)}%)</size>";
        }
      } else {
        targetNameText.text = "<color=#888>No target selected</color>";
      }
    }

    /// <summary>
    /// NEW: O(1) lookup from CharacterIdentityState
    /// OLD: O(n) loop through PartyState.characters
    /// </summary>
    private FixedString32Bytes GetCharacterName(Entity entity)
    {
      if (identityState.names.IsCreated &&
          identityState.names.TryGetValue(entity, out var name)) {
        return name;
      }
      return "Unknown";
    }

    /// <summary>
    /// NEW: O(1) lookups from normalized states
    /// OLD: O(n) loop to build CharacterData
    /// </summary>
    private bool GetCharacterHealth(Entity entity, out HealthData health)
    {
      if (healthState.health.IsCreated &&
          healthState.health.TryGetValue(entity, out health)) {
        return true;
      }
      health = default;
      return false;
    }

    /// <summary>
    /// NEW: O(1) lookups from CharacterIdentityState
    /// </summary>
    private bool GetCharacterIdentity(Entity entity, out FixedString32Bytes name, out bool isEnemy)
    {
      name = default;
      isEnemy = false;

      if (!identityState.names.IsCreated)
        return false;

      if (!identityState.names.TryGetValue(entity, out name))
        return false;

      if (identityState.isEnemy.IsCreated) {
        identityState.isEnemy.TryGetValue(entity, out isEnemy);
      }

      return true;
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    private void UpdateConfirmButton()
    {
      // Show/hide confirm panel based on selection
      if (confirmPanel != null) {
        bool hasTarget = selectedTarget != Entity.Null;
        confirmPanel.SetActive(hasTarget);
      }

      // Update confirm button interactability
      if (confirmButton != null) {
        confirmButton.interactable = selectedTarget != Entity.Null;
      }
    }

    private void OnConfirmClicked()
    {
      if (selectedTarget == Entity.Null || currentProps == null)
        return;

      // Mark selection as confirmed
      DispatchAction(new SelectTargetAction
      {
        targetEntity = selectedTarget,
        confirmSelection = true
      });

      // Dispatch the actual combat action based on type
      switch (currentProps.ActionType) {
        case ActionType.Attack:
          int damage = CalculateBaseDamage(currentProps.ActiveCharacter);
          bool isCrit = Random.value < GetCriticalChance(currentProps.ActiveCharacter);

          DispatchAction(new AttackAction
          {
            attackerEntity = currentProps.ActiveCharacter,
            targetEntity = selectedTarget,
            baseDamage = damage,
            isCritical = isCrit
          });
          break;

        case ActionType.Skill:
          // Would dispatch skill action with currentProps.SelectedSkillId
          break;

        case ActionType.Item:
          // Would dispatch item action
          break;
      }
    }

    private void OnCancelClicked()
    {
      if (currentProps == null)
        return;

      DispatchAction(new CancelActionAction
      {
        actingCharacter = currentProps.ActiveCharacter
      });
    }

    private string FormatActionName(ActionType actionType)
    {
      return actionType switch
      {
        ActionType.Attack => "Attack",
        ActionType.Skill => "Skill",
        ActionType.Item => "Item",
        _ => actionType.ToString()
      };
    }

    private string DetermineTargetType()
    {
      if (currentProps == null)
        return "a target";

      // Determine based on action type and skill
      bool targetingAllies = false;

      if (currentProps.ActionType == ActionType.Skill) {
        // Healing skills target allies
        targetingAllies = (currentProps.SelectedSkillId == 2 || currentProps.SelectedSkillId == 5);
      } else if (currentProps.ActionType == ActionType.Item) {
        // Most items target allies
        targetingAllies = true;
      }

      return targetingAllies ? "an ally" : "an enemy";
    }

    private int CalculateBaseDamage(Entity attacker)
    {
      // Simple damage formula for demo
      // In real game, would consider stats from CharacterStatsState
      int baseDamage = Random.Range(15, 25);

      // Could check CharacterStatusState for buffs/debuffs here

      return baseDamage;
    }

    private float GetCriticalChance(Entity attacker)
    {
      // Base 10% crit chance
      // In real game, would query stats from CharacterStatsState
      return 0.1f;
    }
  }
}