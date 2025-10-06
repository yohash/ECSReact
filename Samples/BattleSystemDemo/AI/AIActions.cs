using Unity.Collections;
using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Dispatched when an enemy's turn begins in the battle.
  /// This replaces the polling architecture - the battle system will dispatch
  /// this action when it transitions to EnemyTurn phase.
  /// 
  /// Contains all context needed for AI to begin thinking, following the
  /// "Action Enrichment" pattern - no entity lookups needed in reducers.
  /// </summary>
  public struct EnemyTurnStartedAction : IGameAction
  {
    public Entity enemyEntity;
    public int turnIndex;
    public int turnCount;

    /// <summary>
    /// Optional: Enemy name for logging/debugging
    /// Can be empty if not needed for AI logic
    /// </summary>
    public FixedString64Bytes enemyName;
  }

  /// <summary>
  /// PHASE 2 CORRECTED: Enriched AIReadyToDecideAction
  /// 
  /// Following action enrichment pattern from best practices:
  /// "Actions describe what happened with full context"
  /// 
  /// This action now carries ALL information needed for the reducer to make
  /// a decision without fetching any additional state.
  /// 
  /// The dispatching system (AIThinkingTimerSystem) enriches this action
  /// with battle context before sending it.
  /// </summary>
  public struct AIReadyToDecideAction : IGameAction
  {
    // ========================================================================
    // BASIC INFO (from Phase 1)
    // ========================================================================

    /// <summary>The enemy entity that's ready to make a decision</summary>
    public Entity enemyEntity;

    /// <summary>How long the enemy thought (for analytics/tuning)</summary>
    public float thinkingDuration;

    /// <summary>Timestamp when thinking started (for validation)</summary>
    public double thinkingStartTime;

    // ========================================================================
    // ENRICHED CONTEXT (Phase 2 addition)
    // ========================================================================

    /// <summary>AI behavior configuration for this enemy</summary>
    public AIBehavior behavior;

    /// <summary>Current turn count (for deterministic random seed)</summary>
    public int turnCount;

    /// <summary>Enemy's current health</summary>
    public int currentHealth;

    /// <summary>Enemy's maximum health</summary>
    public int maxHealth;

    /// <summary>Enemy's status effects</summary>
    public CharacterStatus statusEffects;

    /// <summary>
    /// List of potential targets for this enemy.
    /// Pre-populated with all alive enemies on the opposite team.
    /// </summary>
    public FixedList64Bytes<AITargetInfo> potentialTargets;

    /// <summary>Number of allies still alive (same team as this enemy)</summary>
    public int aliveAllies;

    /// <summary>Number of enemies still alive (opposite team)</summary>
    public int aliveEnemies;
  }

  /// <summary>
  /// Dispatched when an enemy starts thinking about their action.
  /// Used to trigger UI feedback like thinking animations.
  /// </summary>
  public struct AIThinkingAction : IGameAction
  {
    public Entity enemyEntity;
    public float thinkDuration;
  }

  /// <summary>
  /// Dispatched when an enemy has made their decision.
  /// Can be used for UI preview of the action about to be taken.
  /// </summary>
  public struct AIDecisionMadeAction : IGameAction
  {
    public Entity enemyEntity;
    public ActionType chosenAction;
    public Entity targetEntity;
    public int skillId;
  }

  /// <summary>
  /// Dispatched to initialize AI for all enemies at battle start.
  /// </summary>
  public struct InitializeAIAction : IGameAction
  {
    public bool useAdvancedAI;
    public float globalDifficultyModifier;
  }

  /// <summary>
  /// Dispatched to modify AI behavior mid-battle.
  /// Useful for boss phase changes or difficulty adjustments.
  /// </summary>
  public struct ModifyAIBehaviorAction : IGameAction
  {
    public Entity targetEntity;
    public AIStrategy newStrategy;
    public float aggressionModifier;
  }

  /// <summary>
  /// Dispatched when AI makes an invalid decision and needs to retry.
  /// Helps with debugging and fallback behavior.
  /// </summary>
  public struct AIDecisionFailedAction : IGameAction
  {
    public Entity enemyEntity;
    public FixedString32Bytes failureReason;
  }
}