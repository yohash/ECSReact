using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Enemy Turn Detection Middleware - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState dependency
  /// - Uses CharacterIdentityState for enemy validation and names (O(1))
  /// - Uses CharacterHealthState for alive check (O(1))
  /// - Replaced O(n) loops with HashMap lookups
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

      // NEW: Get normalized states for validation
      if (!systemState.TryGetSingleton<CharacterIdentityState>(out var identityState)) {
        return true;
      }

      if (!systemState.TryGetSingleton<CharacterHealthState>(out var healthState)) {
        return true;
      }

      // Find the active enemy entity
      Entity activeEnemy = GetActiveEnemy(battleState, identityState, healthState, ref systemState);
      if (activeEnemy == Entity.Null) {
        return true;
      }

      // Get AI behavior for validation and UI feedback
      var behaviorLookup = systemState.GetComponentLookup<AIBehavior>(true);
      if (!behaviorLookup.HasComponent(activeEnemy)) {
        return true;
      }

      var behavior = behaviorLookup[activeEnemy];

      // NEW: Get enemy name using O(1) lookup
      FixedString64Bytes enemyName = GetEnemyName(activeEnemy, identityState);

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
    /// Validates that the active character is actually an enemy and alive.
    /// 
    /// NEW: Uses O(1) HashMap lookups in CharacterIdentityState and CharacterHealthState.
    /// OLD: O(n) loop through PartyState.characters array.
    /// </summary>
    private Entity GetActiveEnemy(
        BattleState battleState,
        CharacterIdentityState identityState,
        CharacterHealthState healthState,
        ref SystemState systemState)
    {
      // Calculate next index (wraps around)
      int nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      Entity activeEntity = battleState.turnOrder[nextIndex];

      if (activeEntity == Entity.Null)
        return Entity.Null;

      // NEW: Verify this is an enemy using O(1) HashMap lookup
      if (!identityState.isEnemy.IsCreated ||
          !identityState.isEnemy.TryGetValue(activeEntity, out bool isEnemy)) {
        return Entity.Null;
      }

      if (!isEnemy)
        return Entity.Null;

      // NEW: Verify alive status using O(1) HashMap lookup
      if (!healthState.health.IsCreated ||
          !healthState.health.TryGetValue(activeEntity, out var health)) {
        return Entity.Null;
      }

      if (!health.isAlive)
        return Entity.Null;

      return activeEntity;
    }

    /// <summary>
    /// Get enemy name from CharacterIdentityState for logging/debugging.
    /// Returns empty string if not found.
    /// 
    /// NEW: O(1) HashMap lookup.
    /// OLD: O(n) loop through PartyState.characters array.
    /// </summary>
    private FixedString64Bytes GetEnemyName(
        Entity enemyEntity,
        CharacterIdentityState identityState)
    {
      if (!identityState.names.IsCreated)
        return new FixedString64Bytes();

      if (identityState.names.TryGetValue(enemyEntity, out var name)) {
        // Convert FixedString32Bytes to FixedString64Bytes
        FixedString64Bytes result = default;
        result.Append(name);
        return result;
      }

      return new FixedString64Bytes();
    }
  }
}