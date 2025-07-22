using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Main action selection panel with Attack, Skills, Items, Defend buttons.
  /// Demonstrates multi-state subscription and conditional button states.
  /// </summary>
  public class ActionPanel : ReactiveUIComponent<BattleState, UIBattleState, PartyState>, IElementChild
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
    private PartyState partyState;
    private ActionPanelProps currentProps;
    private CharacterData activeCharacter;

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
      UpdateActiveCharacter();
      UpdateButtonStates();
    }

    public override void OnStateChanged(UIBattleState newState)
    {
      uiState = newState;
      UpdateSelectionHighlight();
    }

    public override void OnStateChanged(PartyState newState)
    {
      partyState = newState;
      UpdateActiveCharacter();
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

    private void UpdateActiveCharacter()
    {
      if (currentProps == null)
        return;

      // Find the active character's data
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == currentProps.ActiveCharacterEntity) {
          activeCharacter = partyState.characters[i];

          if (characterNameText)
            characterNameText.text = $"{activeCharacter.name}'s Turn";
          break;
        }
      }
    }

    private void UpdateButtonStates()
    {
      if (currentProps == null)
        return;

      // Enable/disable buttons based on character state and props
      bool isPlayerTurn = battleState.currentPhase == BattlePhase.PlayerSelectAction;

      if (attackButton) {
        attackButton.interactable = isPlayerTurn && activeCharacter.isAlive;
        UpdateButtonVisual(attackButton, attackButton.interactable);
      }

      if (skillsButton) {
        bool hasSkills = currentProps.CanUseSkills && activeCharacter.currentMana > 0;
        skillsButton.interactable = isPlayerTurn && hasSkills && activeCharacter.isAlive;
        UpdateButtonVisual(skillsButton, skillsButton.interactable);
      }

      if (itemsButton) {
        itemsButton.interactable = isPlayerTurn && currentProps.CanUseItems && activeCharacter.isAlive;
        UpdateButtonVisual(itemsButton, itemsButton.interactable);
      }

      if (defendButton) {
        defendButton.interactable = isPlayerTurn && activeCharacter.isAlive;
        UpdateButtonVisual(defendButton, defendButton.interactable);
      }

      if (runButton) {
        // Running might be disabled in boss battles
        runButton.interactable = isPlayerTurn && !IsBossBattle();
        UpdateButtonVisual(runButton, runButton.interactable);
      }
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

      // Also dispatch next turn action
      DispatchAction(new NextTurnAction { skipAnimation = false });
    }

    private void OnRunClicked()
    {
      DispatchAction(new SelectActionTypeAction
      {
        actionType = ActionType.Run,
        actingCharacter = currentProps.ActiveCharacterEntity
      });
    }

    private bool IsBossBattle()
    {
      // Check if any enemy is a boss type
      // For demo, we'll just return false
      return false;
    }
  }
}