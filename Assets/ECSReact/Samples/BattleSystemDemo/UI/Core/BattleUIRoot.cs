using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Root battle UI component - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState subscription
  /// - Added CharacterManaState, CharacterRosterState subscriptions
  /// - Replaced HasManaForSkills() O(n) loop with O(1) HashMap lookup
  /// </summary>
  public class BattleUIRoot : ReactiveUIComponent<BattleState, CharacterManaState, CharacterRosterState, UIBattleState>
  {
    [Header("UI Configuration")]
    [SerializeField] private bool showDebugPanels = false;
    [SerializeField] private bool enableTutorialMode = false;

    [SerializeField] private RectTransform leftColumn;

    private BattleState battleState;
    private CharacterManaState manaState;
    private CharacterRosterState rosterState;
    private UIBattleState uiState;

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateElements();
    }

    public override void OnStateChanged(CharacterManaState newState)
    {
      manaState = newState;
      UpdateElements();
    }

    public override void OnStateChanged(CharacterRosterState newState)
    {
      rosterState = newState;
      UpdateElements();
    }

    public override void OnStateChanged(UIBattleState newState)
    {
      uiState = newState;
      UpdateElements();
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      // Always show turn order at the top
      yield return Mount.Element.FromResources(
          key: "turn_order",
          prefabPath: "UI/TurnOrderDisplay",
          index: 0,
          parentTransform: leftColumn
      );

      // Always show party status
      yield return Mount.Element.FromResources(
          key: "party_status",
          prefabPath: "UI/PartyStatusBar",
          index: 1,
          parentTransform: leftColumn
      );

      // Conditional rendering based on battle phase
      switch (battleState.currentPhase) {
        case BattlePhase.Initializing:
          // Loading screen could go here
          break;

        case BattlePhase.PlayerSelectAction:
          // Main action panel
          yield return Mount.Element.FromResources(
            key: "action_panel",
            prefabPath: "UI/ActionPanel",
            props: new ActionPanelProps
            {
              ActiveCharacterEntity = GetActiveCharacterEntity(),
              CanUseSkills = HasManaForSkills(),
              CanUseItems = HasItemsAvailable()
            },
            index: 2,
            parentTransform: leftColumn
          );

          // Show target selection if in targeting mode (UI state driven)
          if (uiState.showTargetingMode || uiState.activePanel == MenuPanel.TargetSelection) {
            yield return Mount.Element.FromResources(
              key: "target_selection",
              prefabPath: "UI/TargetSelectionPanel",
              props: new TargetSelectionProps
              {
                ActiveCharacter = GetActiveCharacterEntity(),
                ActionType = uiState.selectedAction,
                SelectedSkillId = uiState.selectedSkillId
              },
              index: 4,
              parentTransform: leftColumn
            );
          }

          // Conditional sub-panels based on UI state
          if (uiState.activePanel == MenuPanel.SkillList) {
            yield return Mount.Element.FromResources(
              key: "skill_panel",
              prefabPath: "UI/SkillSelectionPanel",
              props: new SkillPanelProps
              {
                CharacterEntity = GetActiveCharacterEntity()
              },
              index: 3,
              parentTransform: leftColumn
            );
          } else if (uiState.activePanel == MenuPanel.ItemList) {
            // Item panel could go here
          }
          break;

        case BattlePhase.PlayerSelectTarget:
          // Target selection overlay panel
          yield return Mount.Element.FromResources(
            key: "target_selection",
            prefabPath: "UI/TargetSelectionPanel",
            props: new TargetSelectionProps
            {
              ActiveCharacter = GetActiveCharacterEntity(),
              ActionType = uiState.selectedAction,
              SelectedSkillId = uiState.selectedSkillId
            },
            index: 4,
            parentTransform: leftColumn
          );

          // Keep the action panel visible but disabled during targeting
          yield return Mount.Element.FromResources(
            key: "action_panel_disabled",
            prefabPath: "UI/ActionPanel",
            props: new ActionPanelProps
            {
              ActiveCharacterEntity = GetActiveCharacterEntity(),
              CanUseSkills = false,
              CanUseItems = false
            },
            index: 2,
            parentTransform: leftColumn
          );
          break;

        case BattlePhase.ExecutingAction:
          // Action animation display could go here
          break;

        case BattlePhase.EnemyTurn:
          // Enemy thinking indicator could go here
          break;

        case BattlePhase.Victory:
          // Victory screen could go here
          break;

        case BattlePhase.Defeat:
          // Defeat screen could go here
          break;
      }

      // Battle log (always visible but can be collapsed)
      yield return Mount.Element.FromResources(
        key: "battle_log",
        prefabPath: "UI/BattleLogDisplay",
        index: 10,
        parentTransform: leftColumn
      );

      // Always show save system panel (compact, top-right corner)
      yield return Mount.Element.FromResources(
          key: "save_system",
          prefabPath: "UI/SaveSystemPanel",
          index: 11,
          parentTransform: leftColumn
      );

      // Tutorial overlay if enabled
      if (enableTutorialMode && battleState.currentPhase == BattlePhase.PlayerSelectAction) {
        // Tutorial overlay could go here
      }
    }

    // ========================================================================
    // HELPER METHODS - NORMALIZED VERSION
    // ========================================================================

    private Entity GetActiveCharacterEntity()
    {
      if (battleState.activeCharacterIndex >= 0 &&
          battleState.activeCharacterIndex < battleState.turnOrder.Length) {
        return battleState.turnOrder[battleState.activeCharacterIndex];
      }
      return Entity.Null;
    }

    /// <summary>
    /// Check if active character has mana for skills.
    /// OLD: O(n) loop through PartyState.characters array
    /// NEW: O(1) HashMap lookup in CharacterManaState
    /// </summary>
    private bool HasManaForSkills()
    {
      var activeEntity = GetActiveCharacterEntity();
      if (activeEntity == Entity.Null)
        return false;

      // O(1) lookup in normalized state
      if (manaState.mana.IsCreated &&
          manaState.mana.TryGetValue(activeEntity, out var manaData)) {
        return manaData.current > 0;
      }

      return false;
    }

    private bool HasItemsAvailable()
    {
      // In real implementation, check InventoryState
      return true;
    }
  }
}