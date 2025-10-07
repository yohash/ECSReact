using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// AI Thinking Timer System with Action Enrichment
  /// 
  /// This system follows the "Action Enrichment" pattern:
  /// - Gathers ALL context needed for decision-making
  /// - Enriches AIReadyToDecideAction with complete context
  /// - Reducer can be pure (no state fetching needed)
  /// 
  /// The system has access to all states and can build complete context
  /// once, then pass it to the reducer via the enriched action.
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  public partial class AIThinkingTimerSystem : SystemBase
  {
    protected override void OnCreate()
    {
      RequireForUpdate<AIThinkingState>();
      RequireForUpdate<BattleState>();
      RequireForUpdate<PartyState>();
    }

    protected override void OnUpdate()
    {
      // Get thinking state singleton
      if (!SystemAPI.TryGetSingleton<AIThinkingState>(out var thinkingState))
        return;

      // Only process if an enemy is currently thinking
      if (!thinkingState.isThinking)
        return;

      // Check if thinking timer has completed
      double currentTime = SystemAPI.Time.ElapsedTime;

      if (!thinkingState.IsThinkingComplete(currentTime))
        return; // Still thinking

      // Thinking complete! Enrich and dispatch action
      DispatchEnrichedAction(thinkingState);
    }

    private void DispatchEnrichedAction(AIThinkingState thinkingState)
    {
      Entity enemyEntity = thinkingState.thinkingEnemy;

      // ====================================================================
      // ACTION ENRICHMENT: Gather ALL context here
      // ====================================================================

      // Get battle state
      if (!SystemAPI.TryGetSingleton<BattleState>(out var battleState)) {
        Debug.LogError("BattleState not found when enriching AIReadyToDecideAction");
        return;
      }

      // Get party state
      if (!SystemAPI.TryGetSingleton<PartyState>(out var partyState)) {
        Debug.LogError("PartyState not found when enriching AIReadyToDecideAction");
        return;
      }

      // Get AI behavior for this enemy
      if (!EntityManager.HasComponent<AIBehavior>(enemyEntity)) {
        Debug.LogError($"Enemy {enemyEntity.Index} has no AIBehavior component");
        return;
      }
      var behavior = EntityManager.GetComponentData<AIBehavior>(enemyEntity);

      // Build complete decision context
      var context = BuildDecisionContext(enemyEntity, battleState, partyState);

      // Create fully enriched action
      var enrichedAction = new AIReadyToDecideAction
      {
        // Basic info
        enemyEntity = enemyEntity,
        thinkingDuration = thinkingState.thinkDuration,
        thinkingStartTime = thinkingState.thinkingStartTime,

        // Enriched context (everything reducer needs)
        behavior = behavior,
        turnCount = battleState.turnCount,
        currentHealth = context.currentHealth,
        maxHealth = context.maxHealth,
        statusEffects = context.statusEffects,
        potentialTargets = context.potentialTargets,
        aliveAllies = context.aliveAllies,
        aliveEnemies = context.aliveEnemies,
      };

      // Dispatch the enriched action
      ECSActionDispatcher.Dispatch(enrichedAction);
    }

    /// <summary>
    /// Build decision context from current battle state.
    /// This gathers all the information the AI needs to make a decision.
    /// </summary>
    private AIDecisionContext BuildDecisionContext(
        Entity enemy,
        BattleState battleState,
        PartyState partyState)
    {
      var context = new AIDecisionContext
      {
        selfEntity = enemy,
        potentialTargets = new FixedList128Bytes<AITargetInfo>()
      };

      // Find self in party state
      CharacterData? selfData = null;
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == enemy) {
          selfData = partyState.characters[i];
          break;
        }
      }

      if (!selfData.HasValue) {
        Debug.LogWarning($"Enemy {enemy.Index} not found in PartyState");
        return context;
      }

      // Fill self assessment
      context.currentHealth = selfData.Value.currentHealth;
      context.maxHealth = selfData.Value.maxHealth;
      context.healthPercent = selfData.Value.maxHealth > 0
        ? (float)selfData.Value.currentHealth / selfData.Value.maxHealth
        : 0f;
      context.statusEffects = selfData.Value.status;

      // Count allies and build target list
      int aliveAllies = 0;
      int aliveEnemies = 0;

      for (int i = 0; i < partyState.characters.Length; i++) {
        var character = partyState.characters[i];
        if (!character.isAlive)
          continue;

        // Count allies (same team as self)
        if (character.isEnemy == selfData.Value.isEnemy) {
          aliveAllies++;
        } else {
          // This is a potential target (opposite team)
          aliveEnemies++;

          // Add to target list if there's room
          if (context.potentialTargets.Length < context.potentialTargets.Capacity) {
            var targetInfo = new AITargetInfo
            {
              entity = character.entity,
              currentHealth = character.currentHealth,
              healthPercent = character.maxHealth > 0
                ? (float)character.currentHealth / character.maxHealth
                : 0f,
              isDefending = character.status.HasFlag(CharacterStatus.Defending),
              hasDebuffs = character.status.HasFlag(CharacterStatus.Weakened) ||
                          character.status.HasFlag(CharacterStatus.Poisoned),
              threatLevel = 50,
              distance = 1.0f
            };

            context.potentialTargets.Add(targetInfo);
          }
        }
      }

      context.aliveAllies = aliveAllies;
      context.aliveEnemies = aliveEnemies;
      context.isOutnumbered = aliveEnemies > aliveAllies;
      context.isLastAlly = aliveAllies == 1;

      return context;
    }
  }
}

