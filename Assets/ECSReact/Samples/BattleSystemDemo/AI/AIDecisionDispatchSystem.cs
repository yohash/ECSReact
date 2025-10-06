using Unity.Entities;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 2 CORRECTED: AI Decision Dispatch System
  /// 
  /// This system handles side effects (dispatching actions) that the reducer cannot do.
  /// 
  /// Architecture:
  /// - Reducer (AIDecisionReducer): Pure, only mutates AIThinkingState
  /// - This system: Reads state, performs side effects (dispatches actions)
  /// 
  /// Flow:
  /// 1. Reducer makes decision, stores in AIThinkingState
  /// 2. This system detects hasPendingDecision = true
  /// 3. Reads decision from state
  /// 4. Dispatches AIDecisionMadeAction
  /// 5. Clears pending decision flag
  /// 
  /// This is the proper way to handle side effects in a reducer-based architecture.
  /// Reducer stays pure, side effects are isolated in dedicated systems.
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  public partial class AIDecisionDispatchSystem : SystemBase
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      RequireForUpdate<AIThinkingState>();
    }

    protected override void OnUpdate()
    {
      // Get thinking state singleton
      if (!SystemAPI.TryGetSingleton<AIThinkingState>(out var thinkingState))
        return;

      // Check if there's a pending decision to dispatch
      if (!thinkingState.hasPendingDecision)
        return;

      // Read the decision from state
      Entity decidingEnemy = thinkingState.decidingEnemy;
      ActionType chosenAction = thinkingState.chosenAction;
      Entity chosenTarget = thinkingState.chosenTarget;
      int chosenSkillId = thinkingState.chosenSkillId;

      // ====================================================================
      // SIDE EFFECT: Dispatch action based on state
      // ====================================================================

      ECSActionDispatcher.Dispatch(new AIDecisionMadeAction
      {
        enemyEntity = decidingEnemy,
        chosenAction = chosenAction,
        targetEntity = chosenTarget,
        skillId = chosenSkillId
      });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
      Debug.Log($"AIDecisionDispatchSystem: Dispatched AIDecisionMadeAction for " +
                $"entity {decidingEnemy.Index}: {chosenAction} targeting {chosenTarget.Index}");
#endif

      // Clear the pending decision
      var thinkingStateEntity = SystemAPI.GetSingletonEntity<AIThinkingState>();
      var updatedState = EntityManager.GetComponentData<AIThinkingState>(thinkingStateEntity);
      updatedState.ClearPendingDecision();
      EntityManager.SetComponentData(thinkingStateEntity, updatedState);
    }
  }
}