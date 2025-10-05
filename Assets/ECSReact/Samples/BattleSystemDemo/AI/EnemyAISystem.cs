// ============================================================================
// PHASE 1: ENEMYAISYSTEM MODIFICATIONS
// ============================================================================
// Instructions for modifying EnemyAISystem to work with the new event-driven flow
//
// WHAT WE'RE DOING:
// 1. Remove the thinking timer update logic (now in AIThinkingTimerSystem)
// 2. Remove the phase polling and "start thinking" logic (now in AIThinkingTriggerSystem)
// 3. Make the system listen for AIReadyToDecideAction instead
// 4. Keep decision-making and execution logic (will be extracted in later phases)
//
// NOTE: This is a transitional state. We're keeping the EnemyAISystem for now
// but removing the polling and timer logic. In later phases, we'll extract
// the decision and execution logic into reducers and delete this system entirely.
// ============================================================================

using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 1 MODIFIED: Enemy AI Decision and Execution System
  /// 
  /// This system now ONLY handles:
  /// - Responding to AIReadyToDecideAction (decision trigger)
  /// - Making AI decisions based on behavior
  /// - Executing the chosen action
  /// 
  /// REMOVED in Phase 1:
  /// - Phase polling (moved to AIThinkingTriggerSystem)
  /// - Thinking timer updates (moved to AIThinkingTimerSystem)
  /// - Component checks and additions (handled at initialization)
  /// 
  /// TODO in future phases:
  /// - Phase 2: Extract decision logic to AIDecisionReducer
  /// - Phase 3: Extract execution logic to AIExecutionReducer
  /// - Phase 5: Delete this system entirely
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  [UpdateAfter(typeof(AIThinkingTimerSystem))]
  public partial class EnemyAISystem : SystemBase
  {
    // Query for AIReadyToDecideAction
    private EntityQuery readyToDecideQuery;

    protected override void OnCreate()
    {
      base.OnCreate();

      // Create query for AIReadyToDecideAction
      readyToDecideQuery = GetEntityQuery(
        ComponentType.ReadOnly<AIReadyToDecideAction>(),
        ComponentType.ReadOnly<ActionTag>()
      );

      // Only run when we have the required state singletons
      RequireForUpdate<BattleState>();
      RequireForUpdate<PartyState>();
      RequireForUpdate<AIThinkingState>();
    }

    protected override void OnUpdate()
    {
      // Check if there's a ready-to-decide action
      if (readyToDecideQuery.IsEmpty)
        return;

      // Get current states
      if (!SystemAPI.TryGetSingleton<BattleState>(out var battleState))
        return;
      if (!SystemAPI.TryGetSingleton<PartyState>(out var partyState))
        return;

      // Process the AIReadyToDecideAction
      var actions = readyToDecideQuery.ToComponentDataArray<AIReadyToDecideAction>(Allocator.Temp);

      foreach (var action in actions) {
        // Make decision for this enemy
        MakeAndExecuteDecision(action.enemyEntity, battleState, partyState);

        // Clear the thinking state
        ClearThinkingState();
      }

      actions.Dispose();

      // Clean up the action entities
      EntityManager.DestroyEntity(readyToDecideQuery);
    }

    private void MakeAndExecuteDecision(Entity enemy, BattleState battleState, PartyState partyState)
    {
      // Get AI behavior
      if (!EntityManager.HasComponent<AIBehavior>(enemy)) {
        Debug.LogWarning($"Enemy {enemy.Index} has no AIBehavior - using default");
        EntityManager.AddComponentData(enemy, AIBehavior.CreateRandom());
      }

      var aiBehavior = EntityManager.GetComponentData<AIBehavior>(enemy);

      // Build decision context
      var context = BuildDecisionContext(enemy, battleState, partyState);

      // Make decision based on strategy
      var decision = aiBehavior.strategy switch
      {
        AIStrategy.Random => MakeRandomDecision(context, aiBehavior),
        AIStrategy.Aggressive => MakeAggressiveDecision(context, aiBehavior),
        AIStrategy.Defensive => MakeDefensiveDecision(context, aiBehavior),
        AIStrategy.Balanced => MakeBalancedDecision(context, aiBehavior),
        _ => MakeRandomDecision(context, aiBehavior)
      };

      Debug.Log($"Enemy {enemy.Index} decided: {decision.action} targeting {decision.target.Index}");

      // Execute the decision immediately
      ExecuteAIDecision(enemy, decision);
    }

    private void ClearThinkingState()
    {
      // Clear the thinking state singleton
      var thinkingStateEntity = SystemAPI.GetSingletonEntity<AIThinkingState>();
      var thinkingState = EntityManager.GetComponentData<AIThinkingState>(thinkingStateEntity);
      thinkingState.ClearThinking();
      EntityManager.SetComponentData(thinkingStateEntity, thinkingState);
    }

    // ========================================================================
    // DECISION LOGIC (unchanged from original - will be extracted in Phase 2)
    // ========================================================================

    private AIDecisionContext BuildDecisionContext(Entity enemy, BattleState battleState, PartyState partyState)
    {
      var context = new AIDecisionContext
      {
        selfEntity = enemy,
        potentialTargets = new FixedList64Bytes<AITargetInfo>()
      };

      // Find self in party state
      CharacterData? selfData = null;
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == enemy) {
          selfData = partyState.characters[i];
          break;
        }
      }

      if (!selfData.HasValue)
        return context;

      // Fill self assessment
      context.currentHealth = selfData.Value.currentHealth;
      context.maxHealth = selfData.Value.maxHealth;
      context.healthPercent = selfData.Value.maxHealth > 0
        ? (float)selfData.Value.currentHealth / selfData.Value.maxHealth
        : 0f;

      // Count allies and enemies
      int aliveAllies = 0;
      int aliveEnemies = 0;

      for (int i = 0; i < partyState.characters.Length; i++) {
        var character = partyState.characters[i];
        if (!character.isAlive)
          continue;

        if (character.isEnemy == selfData.Value.isEnemy) {
          aliveAllies++;
        } else {
          aliveEnemies++;

          // Add as potential target
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

      context.aliveAllies = aliveAllies;
      context.aliveEnemies = aliveEnemies;
      context.isOutnumbered = aliveEnemies > aliveAllies;
      context.isLastAlly = aliveAllies == 1;

      return context;
    }

    private AIDecision MakeRandomDecision(AIDecisionContext context, AIBehavior behavior)
    {
      var decision = new AIDecision();

      if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;
        int randomIndex = UnityEngine.Random.Range(0, context.potentialTargets.Length);
        decision.target = context.potentialTargets[randomIndex].entity;
      } else {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    private AIDecision MakeAggressiveDecision(AIDecisionContext context, AIBehavior behavior)
    {
      var decision = new AIDecision();

      if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        Entity weakestTarget = Entity.Null;
        float lowestHealth = float.MaxValue;

        for (int i = 0; i < context.potentialTargets.Length; i++) {
          var target = context.potentialTargets[i];
          if (target.healthPercent < lowestHealth) {
            lowestHealth = target.healthPercent;
            weakestTarget = target.entity;
          }
        }

        decision.target = weakestTarget != Entity.Null ? weakestTarget : context.potentialTargets[0].entity;
      } else {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    private AIDecision MakeDefensiveDecision(AIDecisionContext context, AIBehavior behavior)
    {
      var decision = new AIDecision();

      if (context.ShouldConsiderDefending(behavior.defendThreshold)) {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      } else if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        Entity strongestTarget = Entity.Null;
        float highestHealth = 0f;

        for (int i = 0; i < context.potentialTargets.Length; i++) {
          var target = context.potentialTargets[i];
          if (target.currentHealth > highestHealth) {
            highestHealth = target.currentHealth;
            strongestTarget = target.entity;
          }
        }

        decision.target = strongestTarget != Entity.Null ? strongestTarget : context.potentialTargets[0].entity;
      } else {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    private AIDecision MakeBalancedDecision(AIDecisionContext context, AIBehavior behavior)
    {
      var decision = new AIDecision();

      float randomRoll = UnityEngine.Random.Range(0f, 1f);

      if (context.healthPercent < behavior.defendThreshold && randomRoll < 0.5f) {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      } else if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        Entity bestTarget = Entity.Null;
        float bestScore = float.MinValue;

        for (int i = 0; i < context.potentialTargets.Length; i++) {
          var target = context.potentialTargets[i];

          float score = 0f;
          score += (1f - target.healthPercent) * behavior.targetLowestHealthWeight;
          score += (target.currentHealth / 100f) * behavior.targetHighestThreatWeight;
          score += UnityEngine.Random.Range(0f, 1f) * behavior.targetRandomWeight;

          if (target.isDefending)
            score *= 0.5f;

          if (score > bestScore) {
            bestScore = score;
            bestTarget = target.entity;
          }
        }

        decision.target = bestTarget != Entity.Null ? bestTarget : context.potentialTargets[0].entity;
      } else {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    // ========================================================================
    // EXECUTION LOGIC (unchanged from original - will be extracted in Phase 3)
    // ========================================================================

    private void ExecuteAIDecision(Entity enemy, AIDecision decision)
    {
      Debug.Log($"Executing AI decision: {decision.action}");

      switch (decision.action) {
        case ActionType.Attack:
          if (decision.target != Entity.Null) {
            int baseDamage = UnityEngine.Random.Range(10, 20);
            bool isCritical = UnityEngine.Random.Range(0f, 1f) < 0.05f;

            ECSActionDispatcher.Dispatch(new AttackAction
            {
              attackerEntity = enemy,
              targetEntity = decision.target,
              baseDamage = baseDamage,
              isCritical = isCritical
            });
          }
          break;

        case ActionType.Defend:
          ECSActionDispatcher.Dispatch(new SelectActionTypeAction
          {
            actionType = ActionType.Defend,
            actingCharacter = enemy
          });
          break;

        case ActionType.Skill:
          Debug.Log("Skill usage not yet implemented, falling back to attack");
          break;
      }

      // Always advance turn after action
      AdvanceToNextTurn();
    }

    private void AdvanceToNextTurn()
    {
      if (!SystemAPI.TryGetSingleton<BattleState>(out var battleState))
        return;
      if (!SystemAPI.TryGetSingleton<PartyState>(out var partyState))
        return;

      var nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      if (nextIndex >= battleState.turnOrder.Length)
        return;

      var nextEntity = battleState.turnOrder[nextIndex];

      bool isPlayerTurn = false;
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == nextEntity) {
          isPlayerTurn = !partyState.characters[i].isEnemy;
          break;
        }
      }

      ECSActionDispatcher.Dispatch(new NextTurnAction
      {
        skipAnimation = false,
        isPlayerTurn = isPlayerTurn
      });
    }

    // Helper struct for AI decisions
    private struct AIDecision
    {
      public ActionType action;
      public Entity target;
      public int skillId;
    }
  }
}