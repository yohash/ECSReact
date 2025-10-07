using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// AI Thinking Timer System with Action Enrichment - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState dependency
  /// - Uses CharacterHealthState, CharacterStatusState, CharacterIdentityState, CharacterRosterState
  /// - BuildDecisionContext now uses O(1) HashMap lookups
  /// - Iterates roster lists instead of filtering all characters
  /// 
  /// This system follows the "Action Enrichment" pattern:
  /// - Gathers ALL context needed for decision-making
  /// - Enriches AIReadyToDecideAction with complete context
  /// - Reducer can be pure (no state fetching needed)
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  public partial class AIThinkingTimerSystem : SystemBase
  {
    protected override void OnCreate()
    {
      RequireForUpdate<AIThinkingState>();
      RequireForUpdate<BattleState>();
      RequireForUpdate<CharacterHealthState>();
      RequireForUpdate<CharacterStatusState>();
      RequireForUpdate<CharacterIdentityState>();
      RequireForUpdate<CharacterRosterState>();
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

      // NEW: Get normalized states
      if (!SystemAPI.TryGetSingleton<CharacterHealthState>(out var healthState)) {
        Debug.LogError("CharacterHealthState not found when enriching AIReadyToDecideAction");
        return;
      }

      if (!SystemAPI.TryGetSingleton<CharacterStatusState>(out var statusState)) {
        Debug.LogError("CharacterStatusState not found when enriching AIReadyToDecideAction");
        return;
      }

      if (!SystemAPI.TryGetSingleton<CharacterIdentityState>(out var identityState)) {
        Debug.LogError("CharacterIdentityState not found when enriching AIReadyToDecideAction");
        return;
      }

      if (!SystemAPI.TryGetSingleton<CharacterRosterState>(out var rosterState)) {
        Debug.LogError("CharacterRosterState not found when enriching AIReadyToDecideAction");
        return;
      }

      // Get AI behavior for this enemy
      if (!EntityManager.HasComponent<AIBehavior>(enemyEntity)) {
        Debug.LogError($"Enemy {enemyEntity.Index} has no AIBehavior component");
        return;
      }
      var behavior = EntityManager.GetComponentData<AIBehavior>(enemyEntity);

      // Build complete decision context using normalized states
      var context = BuildDecisionContext(
          enemyEntity,
          battleState,
          healthState,
          statusState,
          identityState,
          rosterState);

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
    /// Build decision context from normalized states - NEW VERSION
    /// 
    /// OLD: O(n) loop through PartyState.characters array
    /// NEW: O(1) HashMap lookups + iterate pre-filtered roster lists
    /// 
    /// This gathers all the information the AI needs to make a decision.
    /// </summary>
    private AIDecisionContext BuildDecisionContext(
        Entity enemy,
        BattleState battleState,
        CharacterHealthState healthState,
        CharacterStatusState statusState,
        CharacterIdentityState identityState,
        CharacterRosterState rosterState)
    {
      var context = new AIDecisionContext
      {
        selfEntity = enemy,
        potentialTargets = new FixedList128Bytes<AITargetInfo>()
      };

      // ====================================================================
      // STEP 1: Get self data using O(1) lookups
      // ====================================================================

      // Get self health (O(1))
      if (!healthState.health.TryGetValue(enemy, out var selfHealth)) {
        Debug.LogWarning($"Enemy {enemy.Index} not found in CharacterHealthState");
        return context;
      }

      context.currentHealth = selfHealth.current;
      context.maxHealth = selfHealth.max;
      context.healthPercent = selfHealth.max > 0
        ? (float)selfHealth.current / selfHealth.max
        : 0f;

      // Get self status (O(1))
      if (statusState.statuses.TryGetValue(enemy, out var selfStatus)) {
        context.statusEffects = selfStatus;
      }

      // Get self team affiliation (O(1))
      bool selfIsEnemy = false;
      if (identityState.isEnemy.TryGetValue(enemy, out selfIsEnemy)) {
        // We know our team
      }

      // ====================================================================
      // STEP 2: Determine which roster list has targets (opposite team)
      // ====================================================================

      FixedList32Bytes<Entity> targetRoster;
      FixedList32Bytes<Entity> allyRoster;

      if (selfIsEnemy) {
        // We're an enemy, target players
        targetRoster = rosterState.players;
        allyRoster = rosterState.enemies;
      } else {
        // We're a player, target enemies
        targetRoster = rosterState.enemies;
        allyRoster = rosterState.players;
      }

      // ====================================================================
      // STEP 3: Build target list by iterating target roster
      // OLD: Loop ALL characters, filter by team
      // NEW: Iterate pre-filtered target roster
      // ====================================================================

      int aliveTargets = 0;

      for (int i = 0; i < targetRoster.Length; i++) {
        Entity targetEntity = targetRoster[i];

        if (targetEntity == Entity.Null)
          continue;

        // Get target health
        if (!healthState.health.TryGetValue(targetEntity, out var targetHealth))
          continue;

        // Skip dead targets
        if (!targetHealth.isAlive)
          continue;

        aliveTargets++;

        // Add to potential targets list if there's room
        if (context.potentialTargets.Length < context.potentialTargets.Capacity) {
          // Get target status
          CharacterStatus targetStatus = CharacterStatus.None;
          statusState.statuses.TryGetValue(targetEntity, out targetStatus);

          var targetInfo = new AITargetInfo
          {
            entity = targetEntity,
            currentHealth = targetHealth.current,
            healthPercent = targetHealth.max > 0
              ? (float)targetHealth.current / targetHealth.max
              : 0f,
            isDefending = (targetStatus & CharacterStatus.Defending) != 0,
            hasDebuffs = (targetStatus & CharacterStatus.Weakened) != 0 ||
                        (targetStatus & CharacterStatus.Poisoned) != 0,
            threatLevel = 50, // Could be calculated based on stats
            distance = 1.0f   // Could be calculated from positions
          };

          context.potentialTargets.Add(targetInfo);
        }
      }

      // ====================================================================
      // STEP 4: Count alive allies by iterating ally roster
      // OLD: Loop ALL characters, filter by team and alive status
      // NEW: Iterate pre-filtered ally roster
      // ====================================================================

      int aliveAllies = 0;

      for (int i = 0; i < allyRoster.Length; i++) {
        Entity allyEntity = allyRoster[i];

        if (allyEntity == Entity.Null)
          continue;

        // Get ally health
        if (!healthState.health.TryGetValue(allyEntity, out var allyHealth))
          continue;

        // Count if alive
        if (allyHealth.isAlive)
          aliveAllies++;
      }

      // ====================================================================
      // STEP 5: Fill context with counts
      // ====================================================================

      context.aliveAllies = aliveAllies;
      context.aliveEnemies = aliveTargets;
      context.isOutnumbered = aliveTargets > aliveAllies;
      context.isLastAlly = aliveAllies == 1;

      return context;
    }
  }
}