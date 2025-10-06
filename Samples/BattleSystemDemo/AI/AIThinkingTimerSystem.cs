using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{

  /// <summary>
  /// PHASE 2 CORRECTED: AI Thinking Timer System with Action Enrichment
  /// 
  /// This system now follows the "Action Enrichment" pattern:
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
      Debug.Log($"AIThinkingTimerSystem: Dispatched enriched AIReadyToDecideAction for " +
                $"entity {enemyEntity.Index} with {context.potentialTargets.Length} targets");
#endif
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


  /// <summary>
  /// PHASE 1: Temporary Trigger System
  /// 
  /// This system detects when the battle phase changes to EnemyTurn and
  /// starts the AI thinking process by setting the AIThinkingState singleton.
  /// 
  /// NOTE: This is a TEMPORARY implementation for Phase 1!
  /// In Phase 4, this will be replaced by a proper reducer that responds
  /// to EnemyTurnStartedAction. For now, we're still working with the
  /// existing battle flow that polls for phase changes.
  /// 
  /// This system:
  /// - Detects phase change to EnemyTurn
  /// - Gets active enemy and their AIBehavior
  /// - Sets AIThinkingState singleton to start thinking
  /// - Dispatches AIThinkingAction for UI feedback
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  [UpdateBefore(typeof(AIThinkingTimerSystem))]
  public partial class AIThinkingTriggerSystem : SystemBase
  {
    private BattlePhase lastPhase = BattlePhase.Initializing;

    protected override void OnCreate()
    {
      base.OnCreate();
      RequireForUpdate<BattleState>();
      RequireForUpdate<PartyState>();
      RequireForUpdate<AIThinkingState>();
    }

    protected override void OnUpdate()
    {
      // Get current battle state
      if (!SystemAPI.TryGetSingleton<BattleState>(out var battleState))
        return;

      // Detect phase change to EnemyTurn
      if (battleState.currentPhase != BattlePhase.EnemyTurn || lastPhase == BattlePhase.EnemyTurn) {
        lastPhase = battleState.currentPhase;
        return;
      }

      lastPhase = battleState.currentPhase;

      // Get party state to find the active enemy
      if (!SystemAPI.TryGetSingleton<PartyState>(out var partyState))
        return;

      // Get active enemy entity
      Entity activeEnemy = GetActiveEnemy(battleState, partyState);
      if (activeEnemy == Entity.Null)
        return;

      // Get AI behavior for this enemy
      if (!EntityManager.HasComponent<AIBehavior>(activeEnemy)) {
        Debug.LogWarning($"Active enemy {activeEnemy.Index} has no AIBehavior component!");
        return;
      }

      var aiBehavior = EntityManager.GetComponentData<AIBehavior>(activeEnemy);

      // Get and update the thinking state singleton
      var thinkingStateEntity = SystemAPI.GetSingletonEntity<AIThinkingState>();
      var thinkingState = EntityManager.GetComponentData<AIThinkingState>(thinkingStateEntity);

      // Start thinking for this enemy
      double currentTime = SystemAPI.Time.ElapsedTime;
      thinkingState.StartThinking(activeEnemy, aiBehavior.thinkingDuration, currentTime);

      EntityManager.SetComponentData(thinkingStateEntity, thinkingState);

      // Dispatch UI feedback action
      ECSActionDispatcher.Dispatch(new AIThinkingAction
      {
        enemyEntity = activeEnemy,
        thinkDuration = aiBehavior.thinkingDuration
      });

      Debug.Log($"Enemy {activeEnemy.Index} started thinking (duration: {aiBehavior.thinkingDuration}s)");
    }

    private Entity GetActiveEnemy(BattleState battleState, PartyState partyState)
    {
      if (battleState.activeCharacterIndex >= battleState.turnOrder.Length)
        return Entity.Null;

      var activeEntity = battleState.turnOrder[battleState.activeCharacterIndex];

      // Verify this is actually an enemy
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == activeEntity &&
            partyState.characters[i].isEnemy &&
            partyState.characters[i].isAlive) {
          return activeEntity;
        }
      }

      return Entity.Null;
    }
  }
}