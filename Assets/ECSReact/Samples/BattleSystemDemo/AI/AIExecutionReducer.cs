using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 3: AI Execution Reducer - Deterministic Combat Calculation
  /// 
  /// Pure reducer that responds to AIDecisionMadeAction and calculates
  /// combat details (damage, crit) deterministically using DeterministicRandom.
  /// 
  /// Flow:
  /// 1. Receives AIDecisionMadeAction
  /// 2. Uses DeterministicRandom to calculate damage/crit
  /// 3. Stores results in AIThinkingState
  /// 4. Sets readyToExecuteCombat = true
  /// 5. EnemyAISystem (separate) reads state and dispatches
  /// 
  /// This is PURE:
  /// - No dispatching (only state mutation)
  /// - No UnityEngine.Random (uses DeterministicRandom)
  /// - Fully deterministic (testable!)
  /// </summary>
  [Reducer(DisableBurst = true)]
  public struct AIExecutionReducer : IReducer<AIThinkingState, AIDecisionMadeAction>
  {
    // Combat balance constants
    private const int MIN_BASE_DAMAGE = 10;
    private const int MAX_BASE_DAMAGE = 20;
    private const float BASE_CRIT_CHANCE = 0.05f; // 5%

    public void Execute(
        ref AIThinkingState state,
        in AIDecisionMadeAction action,
        ref SystemState systemState)
    {
      // Validate the action
      if (action.enemyEntity == Entity.Null) {
        Debug.LogWarning("AIExecutionReducer: Received action with null enemy entity");
        return;
      }

      // Create deterministic RNG for combat calculations
      // Use entity index + a different context to get different rolls than decision
      var rng = DeterministicRandom.CreateForDecisionWithContext(
        action.enemyEntity,
        GetTurnCount(ref systemState),
        contextId: 1 // Context 0 = decision, Context 1 = execution
      );

      // Calculate combat details based on action type
      switch (action.chosenAction) {
        case ActionType.Attack:
          CalculateAttackExecution(ref state, action, ref rng);
          break;

        case ActionType.Defend:
          CalculateDefendExecution(ref state, action);
          break;

        case ActionType.Skill:
          CalculateSkillExecution(ref state, action, ref rng);
          break;

        case ActionType.Run:
          // Run doesn't need pre-calculation
          state.StoreCombatExecution(
            action.enemyEntity,
            ActionType.Run,
            Entity.Null,
            damage: 0,
            isCritical: false
          );
          break;

        default:
          Debug.LogWarning($"Unknown action type: {action.chosenAction}");
          return;
      }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
      Debug.Log($"AIExecutionReducer: Calculated combat for entity {action.enemyEntity.Index} - " +
                $"Action: {action.chosenAction}, Target: {action.targetEntity.Index}, " +
                $"Damage: {state.combatDamage}, Crit: {state.combatIsCritical}");
#endif
    }

    /// <summary>
    /// Calculate attack execution details deterministically.
    /// </summary>
    private void CalculateAttackExecution(
        ref AIThinkingState state,
        AIDecisionMadeAction action,
        ref Unity.Mathematics.Random rng)
    {
      if (action.targetEntity == Entity.Null) {
        Debug.LogWarning($"Attack action has no target for entity {action.enemyEntity.Index}");
        return;
      }

      // Deterministic damage calculation
      int damage = rng.NextInt(MIN_BASE_DAMAGE, MAX_BASE_DAMAGE + 1);

      // Deterministic crit calculation
      bool isCritical = rng.NextBool(BASE_CRIT_CHANCE);

      // Store in state for dispatch system
      state.StoreCombatExecution(
        action.enemyEntity,
        ActionType.Attack,
        action.targetEntity,
        damage,
        isCritical
      );
    }

    /// <summary>
    /// Calculate defend execution (no RNG needed).
    /// </summary>
    private void CalculateDefendExecution(
        ref AIThinkingState state,
        AIDecisionMadeAction action)
    {
      // Defend doesn't need random calculations
      state.StoreCombatExecution(
        action.enemyEntity,
        ActionType.Defend,
        Entity.Null, // Defend has no target
        damage: 0,
        isCritical: false
      );
    }

    /// <summary>
    /// Calculate skill execution details deterministically.
    /// TODO: Implement proper skill system with damage formulas.
    /// </summary>
    private void CalculateSkillExecution(
        ref AIThinkingState state,
        AIDecisionMadeAction action,
        ref Unity.Mathematics.Random rng)
    {
      // For now, skills use similar calculation to attacks
      // TODO: Look up skill data and use proper formulas

      int damage = rng.NextInt(MIN_BASE_DAMAGE * 2, MAX_BASE_DAMAGE * 2 + 1); // Skills do more damage
      bool isCritical = rng.NextBool(BASE_CRIT_CHANCE * 1.5f); // Higher crit chance

      state.StoreCombatExecution(
        action.enemyEntity,
        ActionType.Skill,
        action.targetEntity,
        damage,
        isCritical,
        action.skillId
      );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
      Debug.Log($"Skill execution calculated (skill ID: {action.skillId}) - not yet fully implemented");
#endif
    }

    /// <summary>
    /// Helper to get current turn count for RNG seed.
    /// If BattleState is not available, use a default.
    /// </summary>
    private int GetTurnCount(ref SystemState systemState)
    {
      if (systemState.TryGetSingleton<BattleState>(out var battleState)) {
        return battleState.turnCount;
      }

      // Fallback if BattleState not available
      return 1;
    }
  }
}
