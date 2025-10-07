using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 2: AI Decision Utility
  /// 
  /// Pure, stateless functions for AI decision-making.
  /// All functions are:
  /// - Burst-compatible
  /// - Deterministic (use DeterministicRandom)
  /// - Testable (pure functions with no side effects)
  /// - Thread-safe (no shared state)
  /// 
  /// These functions take context and behavior as input, and return a decision.
  /// They never modify state directly - that's the reducer's job.
  /// </summary>
  public static class AIDecisionUtility
  {
    /// <summary>
    /// Main entry point for making an AI decision.
    /// Routes to appropriate strategy based on AIBehavior.
    /// </summary>
    public static AIDecision MakeDecision(
        AIDecisionContext context,
        AIBehavior behavior,
        ref Unity.Mathematics.Random rng)
    {
      return behavior.strategy switch
      {
        AIStrategy.Random => MakeRandomDecision(context, behavior, ref rng),
        AIStrategy.Aggressive => MakeAggressiveDecision(context, behavior, ref rng),
        AIStrategy.Defensive => MakeDefensiveDecision(context, behavior, ref rng),
        AIStrategy.Balanced => MakeBalancedDecision(context, behavior, ref rng),
        AIStrategy.Tactical => MakeTacticalDecision(context, behavior, ref rng),
        _ => MakeRandomDecision(context, behavior, ref rng)
      };
    }

    /// <summary>
    /// Random strategy: Pick random action and random target.
    /// Simple but unpredictable.
    /// </summary>
    public static AIDecision MakeRandomDecision(
        AIDecisionContext context,
        AIBehavior behavior,
        ref Unity.Mathematics.Random rng)
    {
      var decision = new AIDecision();

      if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;
        decision.target = rng.NextElement(context.potentialTargets).entity;
      } else {
        // No targets available - defend
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }


    /// <summary>
    /// Aggressive strategy: Always attack, target weakest enemy.
    /// Tries to finish off low-health targets.
    /// </summary>
    public static AIDecision MakeAggressiveDecision(
        AIDecisionContext context,
        AIBehavior behavior,
        ref Unity.Mathematics.Random rng)
    {
      var decision = new AIDecision();

      if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        // Find target with lowest health percentage
        Entity weakestTarget = Entity.Null;
        float lowestHealth = float.MaxValue;

        for (int i = 0; i < context.potentialTargets.Length; i++) {
          var target = context.potentialTargets[i];
          if (target.healthPercent < lowestHealth) {
            lowestHealth = target.healthPercent;
            weakestTarget = target.entity;
          }
        }

        decision.target = weakestTarget != Entity.Null
          ? weakestTarget
          : context.potentialTargets[0].entity;
      } else {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    /// <summary>
    /// Defensive strategy: Defend when low health, otherwise attack strongest threat.
    /// Prioritizes survival.
    /// </summary>
    public static AIDecision MakeDefensiveDecision(
        AIDecisionContext context,
        AIBehavior behavior,
        ref Unity.Mathematics.Random rng)
    {
      var decision = new AIDecision();

      // Defend if health is below threshold
      if (context.ShouldConsiderDefending(behavior.defendThreshold)) {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      } else if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        // Target enemy with highest health (biggest threat)
        Entity strongestTarget = Entity.Null;
        float highestHealth = 0f;

        for (int i = 0; i < context.potentialTargets.Length; i++) {
          var target = context.potentialTargets[i];
          if (target.currentHealth > highestHealth) {
            highestHealth = target.currentHealth;
            strongestTarget = target.entity;
          }
        }

        decision.target = strongestTarget != Entity.Null
          ? strongestTarget
          : context.potentialTargets[0].entity;
      } else {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    /// <summary>
    /// Balanced strategy: Mix of offense and defense with weighted scoring.
    /// Uses behavior weights to score each target.
    /// </summary>
    public static AIDecision MakeBalancedDecision(
        AIDecisionContext context,
        AIBehavior behavior,
        ref Unity.Mathematics.Random rng)
    {
      var decision = new AIDecision();

      // Randomly consider defending if health is low
      float defendRoll = rng.NextFloat();
      if (context.healthPercent < behavior.defendThreshold && defendRoll < 0.5f) {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
        return decision;
      }

      if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        // Score each target based on behavior weights
        Entity bestTarget = Entity.Null;
        float bestScore = float.MinValue;

        for (int i = 0; i < context.potentialTargets.Length; i++) {
          var target = context.potentialTargets[i];
          float score = ScoreTarget(target, behavior, ref rng);

          if (score > bestScore) {
            bestScore = score;
            bestTarget = target.entity;
          }
        }

        decision.target = bestTarget != Entity.Null
          ? bestTarget
          : context.potentialTargets[0].entity;
      } else {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    /// <summary>
    /// Tactical strategy: Smart targeting with threat assessment.
    /// Similar to Balanced but with more sophisticated logic.
    /// </summary>
    public static AIDecision MakeTacticalDecision(
        AIDecisionContext context,
        AIBehavior behavior,
        ref Unity.Mathematics.Random rng)
    {
      var decision = new AIDecision();

      // Tactical AI defends more conservatively
      if (context.healthPercent < behavior.defendThreshold * 1.2f) {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
        return decision;
      }

      if (context.potentialTargets.Length > 0) {
        decision.action = ActionType.Attack;

        // Use enhanced scoring for tactical decisions
        Entity bestTarget = Entity.Null;
        float bestScore = float.MinValue;

        for (int i = 0; i < context.potentialTargets.Length; i++) {
          var target = context.potentialTargets[i];
          float score = ScoreTacticalTarget(target, context, behavior, ref rng);

          if (score > bestScore) {
            bestScore = score;
            bestTarget = target.entity;
          }
        }

        decision.target = bestTarget != Entity.Null
          ? bestTarget
          : context.potentialTargets[0].entity;
      } else {
        decision.action = ActionType.Defend;
        decision.target = Entity.Null;
      }

      return decision;
    }

    // ========================================================================
    // HELPER FUNCTIONS
    // ========================================================================

    /// <summary>
    /// Score a target based on behavior weights.
    /// Higher score = more desirable target.
    /// </summary>
    private static float ScoreTarget(
        AITargetInfo target,
        AIBehavior behavior,
        ref Unity.Mathematics.Random rng)
    {
      float score = 0f;

      // Low health bonus (easier to finish off)
      score += (1f - target.healthPercent) * behavior.targetLowestHealthWeight;

      // High threat bonus (using health as proxy for threat)
      float threatScore = target.currentHealth / 100f;
      score += threatScore * behavior.targetHighestThreatWeight;

      // Random factor (adds unpredictability)
      score += rng.NextFloat() * behavior.targetRandomWeight;

      // Penalty for defending targets (harder to damage)
      if (target.isDefending)
        score *= 0.5f;

      // Bonus for debuffed targets (easier to exploit)
      if (target.hasDebuffs)
        score *= 1.2f;

      return score;
    }

    /// <summary>
    /// Enhanced tactical scoring that considers battlefield state.
    /// </summary>
    private static float ScoreTacticalTarget(
        AITargetInfo target,
        AIDecisionContext context,
        AIBehavior behavior,
        ref Unity.Mathematics.Random rng)
    {
      // Start with base score
      float score = ScoreTarget(target, behavior, ref rng);

      // Tactical bonuses:

      // If outnumbered, prioritize weakest to even odds
      if (context.isOutnumbered) {
        score += (1f - target.healthPercent) * 20f;
      }

      // If last ally, be more conservative with target selection
      if (context.isLastAlly) {
        // Avoid risky targets (high health = risky)
        score -= (target.healthPercent * 10f);
      }

      // Strongly prefer debuffed targets (tactical opportunity)
      if (target.hasDebuffs) {
        score += 15f;
      }

      return score;
    }
  }

  /// <summary>
  /// AI decision result structure.
  /// Returned by decision utility functions.
  /// </summary>
  public struct AIDecision
  {
    public ActionType action;
    public Entity target;
    public int skillId;

    /// <summary>
    /// Helper to check if this is a valid decision
    /// </summary>
    public bool IsValid()
    {
      if (action == ActionType.Defend || action == ActionType.Run)
        return true; // These don't need targets

      return target != Entity.Null;
    }
  }
}