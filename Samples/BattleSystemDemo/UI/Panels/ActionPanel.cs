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
  public class ActionPanel : ReactiveUIComponent<BattleState, UIBattleState, CharacterIdentityState, CharacterHealthState>, IElementChild
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
    private ActionPanelProps currentProps;

    // Cached character data
    private FixedString32Bytes characterName;
    private bool characterIsAlive;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as ActionPanelProps;
      UpdateActiveCharacter();
      UpdateButtonStates();
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as ActionPanelProps;
      UpdateActiveCharacter();
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
      UpdateButtonStates();
    }

    public override void OnStateChanged(CharacterIdentityState newState)
    {
      identityState = newState;
      UpdateActiveCharacter();
    }

    public override void OnStateChanged(CharacterHealthState newState)
    {
      healthState = newState;
      UpdateActiveCharacter();
      UpdateButtonStates();
    }

    protected override void Start()
    {
      base.Start();

      // Wire up button click handlers
      if (attackButton)
        attackButton.onClick.AddListener(() => OnActionSelected(ActionType.Attack));
      if (skillsButton)
        skillsButton.onClick.AddListener(() => OnActionSelected(ActionType.Skill));
      if (itemsButton)
        itemsButton.onClick.AddListener(() => OnActionSelected(ActionType.Item));
      if (defendButton)
        defendButton.onClick.AddListener(() => OnActionSelected(ActionType.Defend));
      if (runButton)
        runButton.onClick.AddListener(() => OnActionSelected(ActionType.Run));
    }

    /// <summary>
    /// NEW: Fetch character info using O(1) lookups
    /// OLD: O(n) loop through PartyState.characters
    /// </summary>
    private void UpdateActiveCharacter()
    {
      if (currentProps == null || currentProps.ActiveCharacterEntity == Entity.Null)
        return;

      Entity entity = currentProps.ActiveCharacterEntity;

      // Lookup name (O(1))
      if (identityState.names.IsCreated &&
          identityState.names.TryGetValue(entity, out var name)) {
        characterName = name;

        if (characterNameText)
          characterNameText.text = $"{characterName}'s Turn";
      }

      // Lookup alive status (O(1))
      if (healthState.health.IsCreated &&
          healthState.health.TryGetValue(entity, out var healthData)) {
        characterIsAlive = healthData.isAlive;
      }
    }

    private void UpdateButtonStates()
    {
      bool isPlayerTurn = battleState.currentPhase == BattlePhase.PlayerSelectAction;
      bool characterCanAct = characterIsAlive && isPlayerTurn;

      // Enable/disable buttons based on state
      if (attackButton)
        attackButton.interactable = characterCanAct;

      if (skillsButton)
        skillsButton.interactable = characterCanAct && (currentProps?.CanUseSkills ?? false);

      if (itemsButton)
        itemsButton.interactable = characterCanAct && (currentProps?.CanUseItems ?? false);

      if (defendButton)
        defendButton.interactable = characterCanAct;

      if (runButton)
        runButton.interactable = characterCanAct;
    }

    private void OnActionSelected(ActionType actionType)
    {
      if (currentProps == null)
        return;

      DispatchAction(new SelectActionTypeAction
      {
        actionType = actionType,
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