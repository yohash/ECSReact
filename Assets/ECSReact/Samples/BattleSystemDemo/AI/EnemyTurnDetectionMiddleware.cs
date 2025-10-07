using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Enemy Turn Detection Middleware
  /// 
  /// This middleware observes NextTurnAction events and detects when it's an enemy's turn.
  /// When an enemy turn begins, it enriches and dispatches EnemyTurnStartedAction with
  /// complete context following the action enrichment pattern.
  /// 
  /// Architecture:
  /// - Middleware can observe actions without consuming them
  /// - Performs side effects (action dispatching) outside of reducers
  /// - Gathers context once and enriches the action
  /// - Maintains separation of concerns
  /// 
  /// Flow:
  /// 1. NextTurnAction dispatched (isPlayerTurn = false)
  /// 2. This middleware detects it's an enemy turn
  /// 3. Enriches EnemyTurnStartedAction with enemy entity and context
  /// 4. Dispatches enriched action
  /// 5. Optionally dispatches AIThinkingAction for UI feedback
  /// 6. AIThinkingStartReducer responds and starts thinking
  /// 
  /// This replaces the temporary AIThinkingTriggerSystem which polled for phase changes.
  /// Now we have a fully event-driven system!
  /// </summary>
  [Middleware]
  public struct EnemyTurnDetectionMiddleware : IMiddleware<NextTurnAction>
  {
    public bool Process(
      ref NextTurnAction action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter dispatcher,
      int sortKey
    )
    {
      // Only process if this is an enemy turn
      if (action.isPlayerTurn)
        return true; // Continue processing, but nothing to do

      // Get battle state to find active enemy
      if (!systemState.TryGetSingleton<BattleState>(out var battleState)) {
        return true;
      }

      // Get party state to validate enemy
      if (!systemState.TryGetSingleton<PartyState>(out var partyState)) {
        return true;
      }

      // Find the active enemy entity
      Entity activeEnemy = GetActiveEnemy(battleState, partyState, ref systemState);
      if (activeEnemy == Entity.Null) {
        return true;
      }

      // Get AI behavior for validation and UI feedback
      var behaviorLookup = systemState.GetComponentLookup<AIBehavior>(true);
      if (!behaviorLookup.HasComponent(activeEnemy)) {
        return true;
      }

      var behavior = behaviorLookup[activeEnemy];

      // Get enemy name for enrichment (optional but useful)
      FixedString64Bytes enemyName = GetEnemyName(activeEnemy, partyState);

      // ====================================================================
      // SIDE EFFECT: Dispatch enriched EnemyTurnStartedAction
      // ====================================================================

      dispatcher.DispatchAction(sortKey, new EnemyTurnStartedAction
      {
        enemyEntity = activeEnemy,
        turnIndex = battleState.activeCharacterIndex,
        turnCount = battleState.turnCount,
        enemyName = enemyName
      });

      // ====================================================================
      // SIDE EFFECT: Dispatch UI feedback action
      // ====================================================================

      dispatcher.DispatchAction(sortKey + 1, new AIThinkingAction
      {
        enemyEntity = activeEnemy,
        thinkDuration = behavior.thinkingDuration
      });

      // Continue processing the original NextTurnAction
      return true;
    }

    /// <summary>
    /// Get the active enemy entity from battle state.
    /// Validates that the active character is actually an enemy.
    /// </summary>
    private Entity GetActiveEnemy(
        BattleState battleState,
        PartyState partyState,
        ref SystemState systemState)
    {
      int nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      Entity activeEntity = battleState.turnOrder[nextIndex];

      // Verify this is actually an enemy
      for (int i = 0; i < partyState.characters.Length; i++) {
        var character = partyState.characters[i];
        if (character.entity == activeEntity &&
            character.isEnemy &&
            character.isAlive) {
          return activeEntity;
        }
      }
      return Entity.Null;
    }

    /// <summary>
    /// Get enemy name from party state for logging/debugging.
    /// Returns empty string if not found.
    /// </summary>
    private FixedString64Bytes GetEnemyName(Entity enemyEntity, PartyState partyState)
    {
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == enemyEntity) {
          return partyState.characters[i].name;
        }
      }

      return new FixedString64Bytes();
    }
  }
}