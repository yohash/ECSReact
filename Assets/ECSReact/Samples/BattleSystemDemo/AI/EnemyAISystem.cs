using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Core system that manages enemy AI decision making during battle.
  /// Monitors battle state and triggers AI decisions when it's an enemy's turn.
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  public partial class EnemyAISystem : SystemBase
  {
    protected override void OnCreate()
    {
      base.OnCreate();

      // Only run when we have the required state singletons
      RequireForUpdate<BattleState>();
      RequireForUpdate<PartyState>();
    }

    protected override void OnUpdate()
    {
      // Get current states using SystemAPI
      if (!SystemAPI.TryGetSingleton<BattleState>(out var battleState))
        return;
      if (!SystemAPI.TryGetSingleton<PartyState>(out var partyState))
        return;

      // Only process during enemy turn phase
      if (battleState.currentPhase != BattlePhase.EnemyTurn)
        return;

      // Get the active enemy
      Entity activeEnemy = GetActiveEnemy(battleState, partyState);
      if (activeEnemy == Entity.Null)
        return;

      // Check if this enemy has AI behavior
      if (!EntityManager.HasComponent<AIBehavior>(activeEnemy)) {
        // Add default AI behavior if missing
        EntityManager.AddComponentData(activeEnemy, AIBehavior.CreateRandom());
      }

      // Get or create AI state for this enemy
      if (!EntityManager.HasComponent<AIState>(activeEnemy)) {
        EntityManager.AddComponentData(activeEnemy, new AIState
        {
          isThinking = false,
          hasDecided = false,
          thinkingTimer = 0f
        });
      }

      var aiState = EntityManager.GetComponentData<AIState>(activeEnemy);
      var aiBehavior = EntityManager.GetComponentData<AIBehavior>(activeEnemy);

      // Process AI state machine
      if (!aiState.isThinking && !aiState.hasDecided) {
        // Start thinking
        StartAIThinking(activeEnemy, aiBehavior);
      } else if (aiState.isThinking) {
        // Update thinking timer
        aiState.thinkingTimer += SystemAPI.Time.DeltaTime;

        if (aiState.thinkingTimer >= aiBehavior.thinkingDuration) {
          // Time to make a decision
          MakeAIDecision(activeEnemy, battleState, partyState, aiBehavior);
        }

        EntityManager.SetComponentData(activeEnemy, aiState);
      } else if (aiState.hasDecided) {
        // Execute the decision
        ExecuteAIDecision(activeEnemy, aiState);

        // Reset AI state for next turn
        aiState.hasDecided = false;
        aiState.isThinking = false;
        aiState.thinkingTimer = 0f;
        EntityManager.SetComponentData(activeEnemy, aiState);
      }
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

    private void StartAIThinking(Entity enemy, AIBehavior behavior)
    {
      Debug.Log($"Enemy AI starting to think (duration: {behavior.thinkingDuration}s)");

      // Update AI state
      var aiState = EntityManager.GetComponentData<AIState>(enemy);
      aiState.isThinking = true;
      aiState.thinkingTimer = 0f;
      EntityManager.SetComponentData(enemy, aiState);

      // Dispatch thinking action for UI feedback
      ECSActionDispatcher.Dispatch(new AIThinkingAction
      {
        enemyEntity = enemy,
        thinkDuration = behavior.thinkingDuration
      });
    }

    private void MakeAIDecision(Entity enemy, BattleState battleState, PartyState partyState, AIBehavior behavior)
    {
      Debug.Log("Enemy AI making decision...");

      // Build decision context
      var context = BuildDecisionContext(enemy, battleState, partyState);

      // Choose action based on AI behavior
      var decision = behavior.strategy switch
      {
        AIStrategy.Random => MakeRandomDecision(context, behavior),
        AIStrategy.Aggressive => MakeAggressiveDecision(context, behavior),
        AIStrategy.Defensive => MakeDefensiveDecision(context, behavior),
        AIStrategy.Balanced => MakeBalancedDecision(context, behavior),
        _ => MakeRandomDecision(context, behavior)
      };

      // Store the decision
      var aiState = EntityManager.GetComponentData<AIState>(enemy);
      aiState.isThinking = false;
      aiState.hasDecided = true;
      aiState.chosenAction = decision.action;
      aiState.chosenTarget = decision.target;
      aiState.chosenSkillId = decision.skillId;
      EntityManager.SetComponentData(enemy, aiState);

      Debug.Log($"Enemy decided: {decision.action} targeting {decision.target}");
    }

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
      context.currentMana = selfData.Value.currentMana;
      context.statusEffects = selfData.Value.status;

      // Count allies and enemies, build target list
      for (int i = 0; i < partyState.characters.Length; i++) {
        var character = partyState.characters[i];

        if (!character.isAlive)
          continue;

        if (character.isEnemy) {
          context.aliveAllies++;
        } else {
          context.aliveEnemies++;

          // Add as potential target
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
              threatLevel = 50, // Default threat (would be calculated from damage history)
              distance = 1.0f   // Default distance (for future spatial AI)
            };

            context.potentialTargets.Add(targetInfo);
          }
        }
      }

      context.isOutnumbered = context.aliveEnemies > context.aliveAllies;
      context.isLastAlly = context.aliveAllies == 1;

      return context;
    }

    private AIDecision MakeRandomDecision(AIDecisionContext context, AIBehavior behavior)
    {
      var decision = new AIDecision();

      // Simple random: Always attack a random target
      if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;
        int randomIndex = UnityEngine.Random.Range(0, context.potentialTargets.Length);
        decision.target = context.potentialTargets[randomIndex].entity;
      } else {
        // No valid targets, defend
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    private AIDecision MakeAggressiveDecision(AIDecisionContext context, AIBehavior behavior)
    {
      var decision = new AIDecision();

      // Aggressive: Target lowest health enemy
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

      // Defensive: Defend if low health, otherwise attack highest threat
      if (context.ShouldConsiderDefending(behavior.defendThreshold)) {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      } else if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        // Target highest threat (for now, highest health as proxy)
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

      // Balanced: Mix of aggressive and defensive based on situation
      float randomRoll = UnityEngine.Random.Range(0f, 1f);

      // Consider defending if health is low
      if (context.healthPercent < behavior.defendThreshold && randomRoll < 0.5f) {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      } else if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        // Score each target based on weights
        Entity bestTarget = Entity.Null;
        float bestScore = float.MinValue;

        for (int i = 0; i < context.potentialTargets.Length; i++) {
          var target = context.potentialTargets[i];

          float score = 0f;

          // Low health bonus
          score += (1f - target.healthPercent) * behavior.targetLowestHealthWeight;

          // High threat bonus (using health as proxy for now)
          score += (target.currentHealth / 100f) * behavior.targetHighestThreatWeight;

          // Random factor
          score += UnityEngine.Random.Range(0f, 1f) * behavior.targetRandomWeight;

          // Penalty for defending targets
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

    private void ExecuteAIDecision(Entity enemy, AIState aiState)
    {
      Debug.Log($"Executing AI decision: {aiState.chosenAction}");

      // Dispatch the chosen action
      switch (aiState.chosenAction) {
        case ActionType.Attack:
          if (aiState.chosenTarget != Entity.Null) {
            // Calculate damage
            int baseDamage = UnityEngine.Random.Range(10, 20);
            bool isCritical = UnityEngine.Random.Range(0f, 1f) < 0.05f; // 5% crit for enemies

            Store.Instance?.Dispatch(new AttackAction
            {
              attackerEntity = enemy,
              targetEntity = aiState.chosenTarget,
              baseDamage = baseDamage,
              isCritical = isCritical
            });
          }
          break;

        case ActionType.Defend:
          Store.Instance?.Dispatch(new SelectActionTypeAction
          {
            actionType = ActionType.Defend,
            actingCharacter = enemy
          });
          break;

        case ActionType.Skill:
          // TODO: Implement skill usage
          Debug.Log("Skill usage not yet implemented, falling back to attack");
          break;
      }

      // Always advance turn after action
      AdvanceToNextTurn();
    }

    private void AdvanceToNextTurn()
    {
      // Get fresh state using SystemAPI
      if (!SystemAPI.TryGetSingleton<BattleState>(out var battleState))
        return;
      if (!SystemAPI.TryGetSingleton<PartyState>(out var partyState))
        return;

      // Calculate next turn
      var nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      if (nextIndex >= battleState.turnOrder.Length)
        return;

      var nextEntity = battleState.turnOrder[nextIndex];

      // Determine if next turn is player or enemy
      bool isPlayerTurn = false;
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == nextEntity) {
          isPlayerTurn = !partyState.characters[i].isEnemy;
          break;
        }
      }

      // Dispatch turn advance
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