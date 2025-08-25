using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ECSReact.Core;
using Unity.Entities;

namespace ECSReact.Samples.BattleSystem
{
  public class TargetSelectionProps : UIProps
  {
    public Entity ActiveCharacter { get; set; }
    public ActionType ActionType { get; set; }
    public int SelectedSkillId { get; set; }
  }

  /// <summary>
  /// Simplified target selection panel that displays instructions and handles confirmation.
  /// Character cards handle their own hover/click detection and dispatch actions.
  /// </summary>
  public class TargetSelectionPanel : ReactiveUIComponent<PartyState, BattleState, UIBattleState>, IElementChild
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

    private PartyState partyState;
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

    public override void OnStateChanged(PartyState newState)
    {
      partyState = newState;
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
        var character = GetCharacterData(selectedTarget);
        if (character.HasValue) {
          targetNameText.text = character.Value.name.ToString();

          // Color based on enemy/ally
          if (character.Value.isEnemy) {
            targetNameText.color = new Color(1f, 0.3f, 0.3f); // Red for enemies
          } else {
            targetNameText.color = new Color(0.3f, 1f, 0.3f); // Green for allies
          }

          // Show health status
          float healthPercent = character.Value.maxHealth > 0
            ? (float)character.Value.currentHealth / character.Value.maxHealth
            : 0f;

          targetNameText.text += $"\n<size=18>HP: {character.Value.currentHealth}/{character.Value.maxHealth} ({Mathf.RoundToInt(healthPercent * 100)}%)</size>";
        }
      } else {
        targetNameText.text = "<color=#888>No target selected</color>";
      }
    }

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
          DispatchAttackAction();
          break;

        case ActionType.Skill:
          DispatchSkillAction();
          break;

        case ActionType.Item:
          DispatchItemAction();
          break;
      }
    }

    private void DispatchAttackAction()
    {
      if (currentProps == null || selectedTarget == Entity.Null)
        return;

      // Calculate damage based on attacker stats
      int baseDamage = CalculateBaseDamage(currentProps.ActiveCharacter);
      bool isCritical = Random.Range(0f, 1f) < GetCriticalChance(currentProps.ActiveCharacter);

      // Dispatch the attack
      DispatchAction(new AttackAction
      {
        attackerEntity = currentProps.ActiveCharacter,
        targetEntity = selectedTarget,
        baseDamage = baseDamage,
        isCritical = isCritical
      });

      // Auto-advance turn after attack
      DispatchTurnAdvance();
    }

    private void DispatchSkillAction()
    {
      // Skill execution would go here
      Debug.Log($"Using skill {currentProps.SelectedSkillId} on target");

      // For now, just advance turn
      DispatchTurnAdvance();
    }

    private void DispatchItemAction()
    {
      // Item usage would go here
      Debug.Log("Using item on target");

      // For now, just advance turn
      DispatchTurnAdvance();
    }

    private void DispatchTurnAdvance()
    {
      // Calculate next turn info
      var nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      var nextEntity = battleState.turnOrder[nextIndex];

      bool isPlayerTurn = false;
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == nextEntity) {
          isPlayerTurn = !partyState.characters[i].isEnemy;
          break;
        }
      }

      // Dispatch next turn with a small delay for animation
      DispatchAction(new NextTurnAction
      {
        skipAnimation = false,
        isPlayerTurn = isPlayerTurn
      });
    }

    private void OnCancelClicked()
    {
      // Go back to action selection
      DispatchAction(new CancelActionAction
      {
        actingCharacter = currentProps?.ActiveCharacter ?? Entity.Null
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

    private CharacterData? GetCharacterData(Entity entity)
    {
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == entity) {
          return partyState.characters[i];
        }
      }
      return null;
    }

    private Unity.Collections.FixedString32Bytes GetCharacterName(Entity entity)
    {
      var character = GetCharacterData(entity);
      return character?.name ?? "Unknown";
    }

    private int CalculateBaseDamage(Entity attacker)
    {
      // Get attacker's stats
      var attackerData = GetCharacterData(attacker);
      if (!attackerData.HasValue)
        return 10;

      // Simple damage formula for demo
      // In real game, would consider STR, weapon, buffs, etc.
      int baseDamage = Random.Range(15, 25);

      // Buff increases damage
      if (attackerData.Value.status.HasFlag(CharacterStatus.Buffed))
        baseDamage = Mathf.RoundToInt(baseDamage * 1.25f);

      // Weakened decreases damage
      if (attackerData.Value.status.HasFlag(CharacterStatus.Weakened))
        baseDamage = Mathf.RoundToInt(baseDamage * 0.75f);

      return baseDamage;
    }

    private float GetCriticalChance(Entity attacker)
    {
      // Base 10% crit chance
      // In real game, would consider stats, equipment, skills
      return 0.1f;
    }
  }
}