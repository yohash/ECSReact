using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Manages UI state transitions for action selection and targeting.
  /// Handles the flow from action selection → target selection → execution.
  /// </summary>
  [ReducerSystem]
  public partial class UIActionSelectionReducer : ReducerSystem<UIBattleState, SelectActionTypeAction>
  {
    public override void ReduceState(ref UIBattleState state, SelectActionTypeAction action)
    {
      // Store the selected action type
      state.selectedAction = action.actionType;

      // Clear any previous target selection
      state.selectedTarget = Entity.Null;
      state.selectedSkillId = 0;
      state.selectedItemId = 0;

      // Determine UI panel to show based on action type
      switch (action.actionType) {
        case ActionType.Attack:
          // Attack goes straight to target selection
          state.activePanel = MenuPanel.TargetSelection;
          state.showTargetingMode = true;
          state.showActionMenu = false;
          state.lastMessage = "Select target for attack";
          break;

        case ActionType.Skill:
          // Skills show skill selection panel first
          state.activePanel = MenuPanel.SkillList;
          state.showTargetingMode = false;
          state.showActionMenu = false;
          state.lastMessage = "Choose a skill";
          break;

        case ActionType.Item:
          // Items show item selection panel first
          state.activePanel = MenuPanel.ItemList;
          state.showTargetingMode = false;
          state.showActionMenu = false;
          state.lastMessage = "Choose an item";
          break;

        case ActionType.Defend:
          // Defend executes immediately - no UI change needed
          state.activePanel = MenuPanel.None;
          state.showTargetingMode = false;
          state.showActionMenu = true;
          state.lastMessage = "Defending...";
          break;

        case ActionType.Run:
          // Run attempt executes immediately
          state.activePanel = MenuPanel.None;
          state.showTargetingMode = false;
          state.showActionMenu = false;
          state.lastMessage = "Attempting to escape...";
          break;

        case ActionType.None:
          // Cancel/back to main menu
          state.activePanel = MenuPanel.MainActions;
          state.showTargetingMode = false;
          state.showActionMenu = true;
          state.lastMessage = "";
          break;

        default:
          state.activePanel = MenuPanel.MainActions;
          state.showActionMenu = true;
          break;
      }
    }
  }

  /// <summary>
  /// Handles target selection for attacks and skills.
  /// </summary>
  [ReducerSystem]
  public partial class UITargetSelectionReducer : ReducerSystem<UIBattleState, SelectTargetAction>
  {
    public override void ReduceState(ref UIBattleState state, SelectTargetAction action)
    {
      if (action.confirmSelection) {
        // Target confirmed - store it and prepare for execution
        state.selectedTarget = action.targetEntity;
        state.showTargetingMode = false;
        state.activePanel = MenuPanel.None;
        state.lastMessage = "Executing action...";

        // The UI or middleware will now dispatch the actual attack/skill action
      } else {
        // Just hovering/highlighting a target
        state.selectedTarget = action.targetEntity;

        // Update message based on selected action type
        if (state.selectedAction == ActionType.Attack) {
          state.lastMessage = action.targetEntity != Entity.Null
            ? "Press confirm to attack"
            : "Select target for attack";
        } else if (state.selectedAction == ActionType.Skill) {
          state.lastMessage = action.targetEntity != Entity.Null
            ? "Press confirm to use skill"
            : "Select target for skill";
        }
      }
    }
  }

  /// <summary>
  /// Clears UI state when actions are executed.
  /// </summary>
  [ReducerSystem]
  public partial class UIActionExecutionReducer : ReducerSystem<UIBattleState, AttackAction>
  {
    public override void ReduceState(ref UIBattleState state, AttackAction action)
    {
      // Clear all UI state when attack executes
      state.selectedAction = ActionType.None;
      state.selectedTarget = Entity.Null;
      state.activePanel = MenuPanel.None;
      state.showTargetingMode = false;
      state.showActionMenu = false;
      state.lastMessage = "Attack executed!";
    }
  }

  /// <summary>
  /// Handles skill selection from the skill panel.
  /// </summary>
  [ReducerSystem]
  public partial class UISkillSelectionReducer : ReducerSystem<UIBattleState, SelectSkillAction>
  {
    public override void ReduceState(ref UIBattleState state, SelectSkillAction action)
    {
      state.selectedSkillId = action.skillId;

      if (action.targetRequired) {
        // Move to target selection for this skill
        state.activePanel = MenuPanel.TargetSelection;
        state.showTargetingMode = true;
        state.lastMessage = "Select target for skill";
      } else {
        // Self-targeting or no target needed
        state.activePanel = MenuPanel.None;
        state.showTargetingMode = false;
        state.lastMessage = "Using skill...";
      }
    }
  }

  /// <summary>
  /// Resets UI state when turn changes.
  /// </summary>
  [ReducerSystem]
  public partial class UITurnChangeReducer : ReducerSystem<UIBattleState, NextTurnAction>
  {
    public override void ReduceState(ref UIBattleState state, NextTurnAction action)
    {
      // Reset UI for next turn
      state.selectedAction = ActionType.None;
      state.selectedTarget = Entity.Null;
      state.selectedSkillId = 0;
      state.selectedItemId = 0;
      state.activePanel = action.isPlayerTurn ? MenuPanel.MainActions : MenuPanel.None;
      state.showTargetingMode = false;
      state.showActionMenu = action.isPlayerTurn;
      state.lastMessage = action.isPlayerTurn ? "Choose your action" : "Enemy turn...";
    }
  }

  /// <summary>
  /// Handles cancel/back navigation in menus.
  /// </summary>
  public struct CancelActionAction : IGameAction
  {
    public Entity actingCharacter;
  }

  [ReducerSystem]
  public partial class UICancelActionReducer : ReducerSystem<UIBattleState, CancelActionAction>
  {
    public override void ReduceState(ref UIBattleState state, CancelActionAction action)
    {
      // Go back one step in the menu hierarchy
      switch (state.activePanel) {
        case MenuPanel.TargetSelection:
          // If we were selecting a target for attack, go back to main menu
          if (state.selectedAction == ActionType.Attack) {
            state.activePanel = MenuPanel.MainActions;
            state.showActionMenu = true;
          }
          // If for skill, go back to skill list
          else if (state.selectedAction == ActionType.Skill) {
            state.activePanel = MenuPanel.SkillList;
          }
          state.showTargetingMode = false;
          state.selectedTarget = Entity.Null;
          state.lastMessage = "";
          break;

        case MenuPanel.SkillList:
        case MenuPanel.ItemList:
          // Go back to main action menu
          state.activePanel = MenuPanel.MainActions;
          state.showActionMenu = true;
          state.selectedSkillId = 0;
          state.selectedItemId = 0;
          state.lastMessage = "Choose your action";
          break;

        default:
          // Already at main menu, do nothing
          break;
      }

      // Clear selection if canceling
      if (state.activePanel == MenuPanel.MainActions) {
        state.selectedAction = ActionType.None;
      }
    }
  }
}