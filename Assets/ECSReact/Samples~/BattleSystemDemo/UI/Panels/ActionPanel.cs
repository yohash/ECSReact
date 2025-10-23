using ECSReact.Core;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ECSReact.Samples.BattleSystem
{
  public class ActionPanelProps : UIProps
  {
    public Entity ActiveCharacterEntity { get; set; }
    public bool CanUseSkills { get; set; }
    public bool CanUseItems { get; set; }
  }


  // ============================================================================
  // ACTION PANEL - NORMALIZED VERSION
  // ============================================================================

  /// <summary>
  /// Main action selection panel - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState subscription
  /// - Added CharacterIdentityState, CharacterHealthState subscriptions
  /// - Replaced loop to find active character with O(1) lookups
  /// </summary>
  public class ActionPanel : ReactiveUIComponent<BattleState, UIBattleState, CharacterIdentityState, CharacterHealthState, CharacterManaState>, IElementChild
  {
    [Header("UI References")]
    [SerializeField] private Button attackButton;
    [SerializeField] private Button skillsButton;
    [SerializeField] private Button itemsButton;
    [SerializeField] private Button defendButton;
    [SerializeField] private Button runButton;

    [Header("Visual Feedback")]
    [SerializeField] private Image panelBackground;
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private GameObject selectionHighlight;
    [SerializeField] private Color disabledButtonColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private BattleState battleState;
    private UIBattleState uiState;
    private CharacterIdentityState identityState;
    private CharacterHealthState healthState;
    private CharacterManaState manaState;
    private ActionPanelProps currentProps;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as ActionPanelProps;
      UpdateButtonStates();
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as ActionPanelProps;
      UpdateButtonStates();
    }

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateButtonStates();
    }

    public override void OnStateChanged(UIBattleState newState)
    {
      uiState = newState;
      UpdateButtonStates();
    }

    public override void OnStateChanged(CharacterIdentityState newState)
    {
      identityState = newState;
      UpdateButtonStates();
    }

    public override void OnStateChanged(CharacterHealthState newState)
    {
      healthState = newState;
      UpdateButtonStates();
    }

    public override void OnStateChanged(CharacterManaState newState)
    {
      manaState = newState;
      UpdateButtonStates();
    }

    protected override void Start()
    {
      base.Start();

      // Hook up button click handlers
      if (attackButton)
        attackButton.onClick.AddListener(OnAttackClicked);
      if (skillsButton)
        skillsButton.onClick.AddListener(OnSkillsClicked);
      if (itemsButton)
        itemsButton.onClick.AddListener(OnItemsClicked);
      if (defendButton)
        defendButton.onClick.AddListener(OnDefendClicked);
      if (runButton)
        runButton.onClick.AddListener(OnRunClicked);
    }

    private void UpdateButtonStates()
    {
      FixedString32Bytes name = "";
      var hasName = identityState.names.IsCreated
        && identityState.names.TryGetValue(currentProps.ActiveCharacterEntity, out name);

      characterNameText.text = hasName ? name.ToString() : "Unknown";

      bool isPlayerTurn = battleState.currentPhase == BattlePhase.PlayerSelectAction;

      bool isAlive = healthState.health.IsCreated
        && healthState.health.TryGetValue(currentProps.ActiveCharacterEntity, out var healthData)
        && healthData.isAlive;

      bool characterCanAct = isAlive && isPlayerTurn;
      int activeMana = manaState.mana.IsCreated
        && manaState.mana.TryGetValue(currentProps.ActiveCharacterEntity, out var manaData)
          ? manaData.current
          : 0;

      if (attackButton) {
        attackButton.interactable = characterCanAct;
        UpdateButtonVisual(attackButton, attackButton.interactable);
      }

      if (skillsButton) {
        bool hasSkills = currentProps.CanUseSkills && activeMana > 0;
        skillsButton.interactable = characterCanAct && hasSkills;
        UpdateButtonVisual(skillsButton, skillsButton.interactable);
      }

      if (itemsButton) {
        itemsButton.interactable = characterCanAct && currentProps.CanUseItems;
        UpdateButtonVisual(itemsButton, itemsButton.interactable);
      }

      if (defendButton) {
        defendButton.interactable = characterCanAct;
        UpdateButtonVisual(defendButton, defendButton.interactable);
      }

      if (runButton) {
        // Running might be disabled in boss battles
        runButton.interactable = isPlayerTurn;
        UpdateButtonVisual(runButton, runButton.interactable);
      }

      UpdateSelectionHighlight();
    }

    private void UpdateButtonVisual(Button button, bool enabled)
    {
      var colors = button.colors;
      colors.normalColor = enabled ? Color.white : disabledButtonColor;
      button.colors = colors;
    }

    private void UpdateSelectionHighlight()
    {
      if (selectionHighlight == null)
        return;

      // Move highlight to selected action type
      switch (uiState.selectedAction) {
        case ActionType.Attack:
          MoveHighlightToButton(attackButton);
          break;
        case ActionType.Skill:
          MoveHighlightToButton(skillsButton);
          break;
        case ActionType.Item:
          MoveHighlightToButton(itemsButton);
          break;
        case ActionType.Defend:
          MoveHighlightToButton(defendButton);
          break;
        default:
          selectionHighlight.SetActive(false);
          break;
      }
    }


    private void MoveHighlightToButton(Button button)
    {
      if (button == null || selectionHighlight == null)
        return;

      selectionHighlight.SetActive(true);
      selectionHighlight.transform.position = button.transform.position;
    }
    private void OnAttackClicked()
    {
      DispatchAction(new SelectActionTypeAction
      {
        actionType = ActionType.Attack,
        actingCharacter = currentProps.ActiveCharacterEntity
      });
    }

    private void OnSkillsClicked()
    {
      DispatchAction(new SelectActionTypeAction
      {
        actionType = ActionType.Skill,
        actingCharacter = currentProps.ActiveCharacterEntity
      });
    }

    private void OnItemsClicked()
    {
      DispatchAction(new SelectActionTypeAction
      {
        actionType = ActionType.Item,
        actingCharacter = currentProps.ActiveCharacterEntity
      });
    }

    private void OnDefendClicked()
    {
      // Defend is immediate - no target selection needed
      DispatchAction(new SelectActionTypeAction
      {
        actionType = ActionType.Defend,
        actingCharacter = currentProps.ActiveCharacterEntity
      });

      DispatchAction(new ReadyForNextTurn());
    }

    private void OnRunClicked()
    {
      DispatchAction(new SelectActionTypeAction
      {
        actionType = ActionType.Run,
        actingCharacter = currentProps.ActiveCharacterEntity
      });
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      // No child elements for this simple panel
      yield break;
    }
  }
}