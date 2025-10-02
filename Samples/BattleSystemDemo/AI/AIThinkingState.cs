using Unity.Entities;
using ECSReact.Core;
using System;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Singleton state tracking which enemy is currently thinking.
  /// Only one enemy can think at a time in turn-based combat.
  /// 
  /// This replaces the old AIState component on individual enemies.
  /// Benefits:
  /// - Single source of truth for "who's thinking"
  /// - UI components can subscribe to thinking state changes
  /// - Modified through reducers like any other game state
  /// - No per-entity state management needed during thinking
  /// </summary>
  public struct AIThinkingState : IGameState, IEquatable<AIThinkingState>
  {
    /// <summary>
    /// The entity currently thinking (Entity.Null if no one is thinking)
    /// </summary>
    public Entity thinkingEnemy;

    /// <summary>
    /// When the enemy started thinking (ElapsedTime from World.Time)
    /// </summary>
    public double thinkingStartTime;

    /// <summary>
    /// How long this enemy should think (configured from AIBehavior)
    /// </summary>
    public float thinkDuration;

    /// <summary>
    /// Whether an enemy is currently in the thinking state
    /// </summary>
    public bool isThinking;

    /// <summary>
    /// Optional: Track for debugging/analytics
    /// </summary>
    public int decisionsMadeThisBattle;

    /// <summary>
    /// Helper to check if thinking timer has completed
    /// </summary>
    public bool IsThinkingComplete(double currentTime)
    {
      return isThinking && (currentTime >= thinkingStartTime + thinkDuration);
    }

    /// <summary>
    /// Helper to get thinking progress (0-1)
    /// </summary>
    public float GetThinkingProgress(double currentTime)
    {
      if (!isThinking || thinkDuration <= 0)
        return 0f;

      float elapsed = (float)(currentTime - thinkingStartTime);
      return Unity.Mathematics.math.clamp(elapsed / thinkDuration, 0f, 1f);
    }

    /// <summary>
    /// Start thinking for a new enemy
    /// </summary>
    public void StartThinking(Entity enemy, float duration, double startTime)
    {
      thinkingEnemy = enemy;
      thinkingStartTime = startTime;
      thinkDuration = duration;
      isThinking = true;
    }

    /// <summary>
    /// Clear thinking state after decision is made
    /// </summary>
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
             decisionsMadeThisBattle == other.decisionsMadeThisBattle;
    }
  }
}