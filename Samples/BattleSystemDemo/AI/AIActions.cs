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
  /// Dispatched when the AI thinking timer completes.
  /// This triggers the actual decision-making logic.
  /// 
  /// Replaces the timer polling in the old system - the thinking timer
  /// system will dispatch this after the dramatic pause completes.
  /// </summary>
  public struct AIReadyToDecideAction : IGameAction
  {
    public Entity enemyEntity;
    public float thinkingDuration;
    public double thinkingStartTime;
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