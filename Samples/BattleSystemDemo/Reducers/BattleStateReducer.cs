using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Handles turn progression and battle phase transitions
  /// </summary>
  [ReducerSystem]
  public partial class TurnOrderReducer : ReducerSystem<BattleState, InitializeTurnOrderAction>
  {
    protected override void ReduceState(ref BattleState state, InitializeTurnOrderAction action)
    {
      state.battleActive = true;
      state.turnCount = 1;
      state.turnTimer = 0f;
      state.activeCharacterIndex = 0;
      state.currentPhase = BattlePhase.PlayerSelectAction;
      state.turnOrder = new FixedList128Bytes<Entity>();

      foreach (var entity in action.turnOrder) {
        state.turnOrder.Add(entity);
      }
    }
  }

  /// <summary>
  /// Handles turn progression and battle phase transitions
  /// </summary>
  [ReducerSystem]
  public partial class BattleStateReducer : ReducerSystem<BattleState, NextTurnAction>
  {
    protected override void ReduceState(ref BattleState state, NextTurnAction action)
    {
      if (!state.battleActive)
        return;

      // Advance to next character in turn order
      state.activeCharacterIndex = (state.activeCharacterIndex + 1) % state.turnOrder.Length;
      state.turnCount++;
      state.turnTimer = 0f;

      // Determine next phase based on whose turn it is
      state.currentPhase = action.isPlayerTurn
          ? BattlePhase.PlayerSelectAction
          : BattlePhase.EnemyTurn;
    }
  }

  /// <summary>
  /// Handles attack execution and damage application
  /// </summary>
  [ReducerSystem]
  public partial class AttackExecutionReducer : ReducerSystem<BattleState, AttackAction>
  {
    protected override void ReduceState(ref BattleState state, AttackAction action)
    {
      // Set phase to executing
      state.currentPhase = BattlePhase.ExecutingAction;

      // In a real implementation, we'd trigger animation here
      // For demo, we'll auto-advance after "execution"
    }
  }
}