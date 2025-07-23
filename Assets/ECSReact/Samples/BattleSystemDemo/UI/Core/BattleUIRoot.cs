using System.Collections.Generic;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Root battle UI component that demonstrates conditional element rendering
  /// based on battle phase. This is the main orchestrator for all battle UI.
  /// </summary>
  public class BattleUIRoot : ReactiveUIComponent<BattleState, UIBattleState>
  {
    [Header("UI Configuration")]
    [SerializeField] private bool showDebugPanels = false;
    [SerializeField] private bool enableTutorialMode = false;

    [SerializeField] private RectTransform leftColumn;

    private BattleState battleState;
    private UIBattleState uiState;

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateElements(); // Trigger element reconciliation
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

      // Always show save system panel (compact, top-right corner)
      yield return Mount.Element.FromResources(
          key: "save_system",
          prefabPath: "UI/SaveSystemPanel",
          index: 5,
          parentTransform: leftColumn
      );

      // Conditional rendering based on battle phase
      switch (battleState.currentPhase) {
        case BattlePhase.Initializing:
          //yield return UIElement.FromComponent<BattleLoadingScreen>(
          //    key: "loading",
          //    index: 2
          //);
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
            //yield return UIElement.FromComponent<ItemSelectionPanel>(
            //    key: "item_panel",
            //    index: 3
            //);
          }
          break;

        case BattlePhase.PlayerSelectTarget:
          // Targeting overlay
          //yield return UIElement.FromComponent<TargetingOverlay>(
          //    key: "targeting",
          //    props: new TargetingProps
          //    {
          //      ActionType = uiState.selectedAction,
          //      ValidTargets = GetValidTargets(uiState.selectedAction)
          //    },
          //    index: 2
          //);

          //// Target confirmation panel
          //yield return UIElement.FromComponent<TargetConfirmPanel>(
          //    key: "target_confirm",
          //    index: 3
          //);
          break;

        case BattlePhase.ExecutingAction:
          // Action animation display
          //yield return UIElement.FromComponent<ActionExecutionDisplay>(
          //    key: "action_execution",
          //    props: new ActionExecutionProps
          //    {
          //      ActionType = uiState.selectedAction,
          //      Source = GetActiveCharacterEntity(),
          //      Target = uiState.selectedTarget
          //    },
          //    index: 2
          //);
          break;

        case BattlePhase.EnemyTurn:
          // Enemy thinking indicator
          //yield return UIElement.FromComponent<EnemyTurnIndicator>(
          //    key: "enemy_turn",
          //    props: new EnemyTurnProps
          //    {
          //      EnemyEntity = GetActiveCharacterEntity(),
          //      ThinkingDuration = 1.5f
          //    },
          //    index: 2
          //);
          break;

        case BattlePhase.Victory:
          //yield return UIElement.FromComponent<VictoryScreen>(
          //    key: "victory",
          //    props: new VictoryProps
          //    {
          //      TurnCount = battleState.turnCount,
          //      PartyState = GetPartyStateSummary()
          //    },
          //    index: 2
          //);
          break;

        case BattlePhase.Defeat:
          //yield return UIElement.FromComponent<DefeatScreen>(
          //    key: "defeat",
          //    index: 2
          //);
          break;
      }

      // Battle log (always visible but can be collapsed)
      yield return Mount.Element.FromResources(
        key: "battle_log",
        prefabPath: "UI/BattleLogDisplay",
        index: 10,
        parentTransform: leftColumn
      );

      //// Optional debug panels
      //if (showDebugPanels) {
      //  yield return UIElement.FromComponent<StateDebugPanel>(
      //      key: "debug_states",
      //      index: 20
      //  );

      //  yield return UIElement.FromComponent<ActionHistoryPanel>(
      //      key: "debug_actions",
      //      index: 21
      //  );
      //}

      // Tutorial overlay if enabled
      if (enableTutorialMode && battleState.currentPhase == BattlePhase.PlayerSelectAction) {
        //yield return UIElement.FromComponent<TutorialOverlay>(
        //    key: "tutorial",
        //    props: new TutorialProps
        //    {
        //      CurrentPhase = battleState.currentPhase,
        //      HighlightElement = GetTutorialHighlight()
        //    },
        //    index: 100 // Always on top
        //);
      }
    }

    // Helper methods for props data
    private Unity.Entities.Entity GetActiveCharacterEntity()
    {
      if (battleState.activeCharacterIndex >= 0 &&
          battleState.activeCharacterIndex < battleState.turnOrder.Length) {
        return battleState.turnOrder[battleState.activeCharacterIndex];
      }
      return Unity.Entities.Entity.Null;
    }

    private bool HasManaForSkills()
    {
      // In real implementation, check PartyState for active character's mana
      return true;
    }

    private bool HasItemsAvailable()
    {
      // In real implementation, check InventoryState
      return true;
    }

    private Unity.Collections.FixedList64Bytes<Unity.Entities.Entity> GetValidTargets(ActionType actionType)
    {
      var targets = new Unity.Collections.FixedList64Bytes<Unity.Entities.Entity>();
      // In real implementation, determine valid targets based on action type
      // For now, return empty list
      return targets;
    }

    private string GetPartyStateSummary()
    {
      // In real implementation, generate summary from PartyState
      return "All party members survived!";
    }

    private string GetTutorialHighlight()
    {
      // Determine what to highlight based on current state
      if (uiState.selectedAction == ActionType.None)
        return "action_panel";

      return "";
    }
  }

  public class SkillPanelProps : UIProps
  {
    public Unity.Entities.Entity CharacterEntity { get; set; }
  }

  public class TargetingProps : UIProps
  {
    public ActionType ActionType { get; set; }
    public Unity.Collections.FixedList64Bytes<Unity.Entities.Entity> ValidTargets { get; set; }
  }

  public class ActionExecutionProps : UIProps
  {
    public ActionType ActionType { get; set; }
    public Unity.Entities.Entity Source { get; set; }
    public Unity.Entities.Entity Target { get; set; }
  }

  public class EnemyTurnProps : UIProps
  {
    public Unity.Entities.Entity EnemyEntity { get; set; }
    public float ThinkingDuration { get; set; }
  }

  public class VictoryProps : UIProps
  {
    public int TurnCount { get; set; }
    public string PartyState { get; set; }
  }

  public class TutorialProps : UIProps
  {
    public BattlePhase CurrentPhase { get; set; }
    public string HighlightElement { get; set; }
  }
}