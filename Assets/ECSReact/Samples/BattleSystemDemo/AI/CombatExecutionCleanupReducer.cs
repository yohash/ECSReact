using Unity.Entities;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 3: Combat Execution Cleanup Reducer
  /// 
  /// Closes the execution loop by clearing the readyToExecuteCombat flag
  /// after combat actions are dispatched.
  /// 
  /// Flow:
  /// 1. AIExecutionReducer sets: readyToExecuteCombat = true
  /// 2. EnemyAISystem dispatches: AttackAction/DefendAction/etc
  /// 3. This reducer responds to combat actions
  /// 4. Clears: readyToExecuteCombat = false
  /// 
  /// This prevents infinite loops and completes the state cycle cleanly.
  /// 
  /// The reducer responds to multiple action types (Attack, Defend, etc.)
  /// to ensure the flag is cleared regardless of which action was dispatched.
  /// </summary>
  [Reducer]
  public struct AttackExecutionCleanupReducer : IReducer<AIThinkingState, AttackAction>
  {
    public void Execute(
        ref AIThinkingState state,
        in AttackAction action,
        ref SystemState systemState)
    {
      // If this attack came from AI execution, clear the flag
      if (state.readyToExecuteCombat && state.combatExecutor == action.attackerEntity) {
        state.ClearCombatExecution();
      }
    }
  }

  /// <summary>
  /// Cleanup reducer for defend actions.
  /// </summary>
  [Reducer]
  public struct DefendExecutionCleanupReducer : IReducer<AIThinkingState, SelectActionTypeAction>
  {
    public void Execute(
        ref AIThinkingState state,
        in SelectActionTypeAction action,
        ref SystemState systemState)
    {
      // Only clear if this is a defend action from AI execution
      if (action.actionType == ActionType.Defend &&
          state.readyToExecuteCombat &&
          state.combatExecutor == action.actingCharacter) {
        state.ClearCombatExecution();
      }
    }
  }
  // Note: Additional cleanup reducers (SkillExecutionCleanupReducer, etc.) 
  // can be added as new action types are implemented.
}