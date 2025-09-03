using Unity.Collections;
using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
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