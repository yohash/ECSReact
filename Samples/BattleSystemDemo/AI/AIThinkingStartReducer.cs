using Unity.Entities;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 4: AI Thinking Start Reducer
  /// 
  /// Pure reducer that responds to EnemyTurnStartedAction and initiates the AI thinking process.
  /// This replaces the temporary AIThinkingTriggerSystem from Phase 1.
  /// 
  /// Architecture:
  /// - Pure reducer (only mutates AIThinkingState)
  /// - No side effects (no action dispatching)
  /// - Minimal component lookup (only AIBehavior)
  /// - Event-driven (responds to action, no polling)
  /// 
  /// Flow:
  /// 1. Receives EnemyTurnStartedAction (enriched by middleware)
  /// 2. Gets AIBehavior for the enemy
  /// 3. Mutates AIThinkingState to start thinking
  /// 4. That's it! Timer system takes over from here
  /// 
  /// Benefits:
  /// - Fully event-driven (no polling!)
  /// - Proper reducer pattern
  /// - Clear separation of concerns
  /// - Easy to test
  /// </summary>
  [Reducer]
  public struct AIThinkingStartReducer : IReducer<AIThinkingState, EnemyTurnStartedAction>
  {
    public void Execute(
        ref AIThinkingState state,
        in EnemyTurnStartedAction action,
        ref SystemState systemState)
    {
      // Validate the action
      if (action.enemyEntity == Entity.Null) {
        return;
      }

      // Get AI behavior for this enemy
      var behaviorLookup = systemState.GetComponentLookup<AIBehavior>(true);
      if (!behaviorLookup.HasComponent(action.enemyEntity)) {
        return;
      }

      var behavior = behaviorLookup[action.enemyEntity];

      // Get current time for thinking start
      double currentTime = systemState.WorldUnmanaged.Time.ElapsedTime;

      // Mutate state to start thinking
      state.StartThinking(action.enemyEntity, behavior.thinkingDuration, currentTime);
    }
  }
}