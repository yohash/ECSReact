using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using ECSReact.Core;
using System;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 3: AIThinkingState with Combat Execution Storage
  /// 
  /// Extended to support the state-driven execution pattern:
  /// 1. AIExecutionReducer calculates combat details (deterministic!)
  /// 2. Stores in state: readyToExecuteCombat = true
  /// 3. EnemyAISystem reads state and dispatches action
  /// 4. CombatExecutionCleanupReducer clears flag
  /// 
  /// This maintains pure reducers while enabling reactive dispatch.
  /// </summary>
  public struct AIThinkingState : IGameState, IEquatable<AIThinkingState>
  {
    // ========================================================================
    // THINKING STATE (from Phase 0/1)
    // ========================================================================

    public Entity thinkingEnemy;
    public double thinkingStartTime;
    public float thinkDuration;
    public bool isThinking;
    public int decisionsMadeThisBattle;

    // ========================================================================
    // DECISION STORAGE (from Phase 2)
    // ========================================================================

    public bool hasPendingDecision;
    public Entity decidingEnemy;
    public ActionType chosenAction;
    public Entity chosenTarget;
    public int chosenSkillId;

    // ========================================================================
    // COMBAT EXECUTION STORAGE (Phase 3 NEW!)
    // ========================================================================

    /// <summary>
    /// Flag indicating combat action is ready to be dispatched.
    /// EnemyAISystem watches this flag and dispatches when true.
    /// </summary>
    public bool readyToExecuteCombat;

    /// <summary>Entity executing the combat action</summary>
    public Entity combatExecutor;

    /// <summary>Type of combat action to execute</summary>
    public ActionType combatAction;

    /// <summary>Target entity for combat action</summary>
    public Entity combatTarget;

    /// <summary>
    /// Pre-calculated damage (deterministic!).
    /// Calculated by AIExecutionReducer using DeterministicRandom.
    /// </summary>
    public int combatDamage;

    /// <summary>
    /// Pre-calculated critical hit flag (deterministic!).
    /// Calculated by AIExecutionReducer using DeterministicRandom.
    /// </summary>
    public bool combatIsCritical;

    /// <summary>Skill ID if using skill action</summary>
    public int combatSkillId;

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    public bool IsThinkingComplete(double currentTime)
    {
      return isThinking && (currentTime >= thinkingStartTime + thinkDuration);
    }

    public float GetThinkingProgress(double currentTime)
    {
      if (!isThinking || thinkDuration <= 0)
        return 0f;

      float elapsed = (float)(currentTime - thinkingStartTime);
      return math.clamp(elapsed / thinkDuration, 0f, 1f);
    }

    public void StartThinking(Entity enemy, float duration, double startTime)
    {
      thinkingEnemy = enemy;
      thinkingStartTime = startTime;
      thinkDuration = duration;
      isThinking = true;
    }

    public void StoreDecision(Entity enemy, ActionType action, Entity target, int skillId)
    {
      hasPendingDecision = true;
      decidingEnemy = enemy;
      chosenAction = action;
      chosenTarget = target;
      chosenSkillId = skillId;

      isThinking = false;
      thinkingEnemy = Entity.Null;
      decisionsMadeThisBattle++;
    }

    public void ClearPendingDecision()
    {
      hasPendingDecision = false;
      decidingEnemy = Entity.Null;
      chosenAction = ActionType.None;
      chosenTarget = Entity.Null;
      chosenSkillId = 0;
    }

    /// <summary>
    /// PHASE 3: Store pre-calculated combat execution details.
    /// Called by AIExecutionReducer after deterministic calculation.
    /// </summary>
    public void StoreCombatExecution(
        Entity executor,
        ActionType action,
        Entity target,
        int damage,
        bool isCritical,
        int skillId = 0)
    {
      readyToExecuteCombat = true;
      combatExecutor = executor;
      combatAction = action;
      combatTarget = target;
      combatDamage = damage;
      combatIsCritical = isCritical;
      combatSkillId = skillId;
    }

    /// <summary>
    /// PHASE 3: Clear combat execution flag.
    /// Called by CombatExecutionCleanupReducer after action is dispatched.
    /// </summary>
    public void ClearCombatExecution()
    {
      readyToExecuteCombat = false;
      combatExecutor = Entity.Null;
      combatAction = ActionType.None;
      combatTarget = Entity.Null;
      combatDamage = 0;
      combatIsCritical = false;
      combatSkillId = 0;
    }

    public void ClearThinking()
    {
      thinkingEnemy = Entity.Null;
      thinkingStartTime = 0;
      thinkDuration = 0;
      isThinking = false;
      decisionsMadeThisBattle++;
    }

    public bool Equals(AIThinkingState other)
    {
      return thinkingEnemy == other.thinkingEnemy &&
             thinkingStartTime == other.thinkingStartTime &&
             thinkDuration == other.thinkDuration &&
             isThinking == other.isThinking &&
             decisionsMadeThisBattle == other.decisionsMadeThisBattle &&
             hasPendingDecision == other.hasPendingDecision &&
             decidingEnemy == other.decidingEnemy &&
             chosenAction == other.chosenAction &&
             chosenTarget == other.chosenTarget &&
             chosenSkillId == other.chosenSkillId &&
             readyToExecuteCombat == other.readyToExecuteCombat &&
             combatExecutor == other.combatExecutor &&
             combatAction == other.combatAction &&
             combatTarget == other.combatTarget &&
             combatDamage == other.combatDamage &&
             combatIsCritical == other.combatIsCritical &&
             combatSkillId == other.combatSkillId;
    }
  }
}
