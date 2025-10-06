using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 2 CORRECTED: Pure AI Decision Reducer
  /// 
  /// This reducer is NOW PURE - following best practices:
  /// 1. NO state fetching - all context comes from enriched action
  /// 2. NO side effects - only mutates AIThinkingState
  /// 3. NO dispatching - stores decision in state for follow-up system
  /// 
  /// The reducer:
  /// - Receives fully enriched AIReadyToDecideAction (with all context)
  /// - Uses DeterministicRandom for decision
  /// - Calls pure AIDecisionUtility functions
  /// - ONLY mutates AIThinkingState (stores decision)
  /// - Follow-up system will read decision and dispatch actions
  /// 
  /// Benefits:
  /// - Truly pure function (testable, deterministic)
  /// - No EntityManager or state lookups
  /// - Follows React/Redux best practices
  /// - Clear separation of concerns
  /// </summary>
  [Reducer]
  public struct AIDecisionReducer : IReducer<AIThinkingState, AIReadyToDecideAction>
  {
    public void Execute(
        ref AIThinkingState state,
        in AIReadyToDecideAction action,
        ref SystemState systemState)
    {
      // Verify this action is for the currently thinking enemy
      if (!state.isThinking || state.thinkingEnemy != action.enemyEntity) {
        return;
      }

      // Build decision context from enriched action
      // NO state fetching - everything comes from the action!
      var context = BuildContextFromAction(action);

      // Create deterministic RNG for this decision
      var rng = DeterministicRandom.CreateForDecision(action.enemyEntity, action.turnCount);

      // Make decision using pure strategy functions
      var decision = AIDecisionUtility.MakeDecision(context, action.behavior, ref rng);

      // Validate decision
      if (!decision.IsValid()) {
        // Clear thinking state on error
        state.ClearThinking();
        return;
      }

      // ====================================================================
      // PURE REDUCER: ONLY MUTATE STATE
      // ====================================================================

      // Store the decision in state (no dispatching!)
      state.StoreDecision(
        action.enemyEntity,
        decision.action,
        decision.target,
        decision.skillId
      );

      // That's it! No dispatching, no side effects.
      // A follow-up system will read hasPendingDecision and dispatch actions.
    }

    /// <summary>
    /// Build decision context from enriched action.
    /// Pure function - no state fetching, all data from action.
    /// </summary>
    private AIDecisionContext BuildContextFromAction(AIReadyToDecideAction action)
    {
      var context = new AIDecisionContext
      {
        selfEntity = action.enemyEntity,
        currentHealth = action.currentHealth,
        maxHealth = action.maxHealth,
        healthPercent = action.maxHealth > 0
          ? (float)action.currentHealth / action.maxHealth
          : 0f,
        currentMana = 0, // TODO: Add mana system
        statusEffects = action.statusEffects,
        aliveAllies = action.aliveAllies,
        aliveEnemies = action.aliveEnemies,
        isOutnumbered = action.aliveEnemies > action.aliveAllies,
        isLastAlly = action.aliveAllies == 1,
        potentialTargets = action.potentialTargets,
        lastAction = ActionType.None, // TODO: Track decision history
        lastTarget = Entity.Null,
        turnsDefending = 0
      };

      return context;
    }
  }
}
