using Unity.Entities;
using Unity.Burst;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 1: AI Thinking Timer System
  /// 
  /// Dedicated system that manages the AI thinking timer and dispatches
  /// AIReadyToDecideAction when the dramatic pause completes.
  /// 
  /// This system:
  /// - Queries AIThinkingState singleton
  /// - Updates timer based on elapsed time
  /// - Dispatches AIReadyToDecideAction when thinking completes
  /// - Replaces the timer logic that was in EnemyAISystem
  /// 
  /// Benefits:
  /// - Separation of concerns (timing vs decision-making)
  /// - Event-driven (dispatches action instead of setting flags)
  /// - Testable in isolation
  /// - No sync points (read-only query)
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  [BurstCompile]
  public partial struct AIThinkingTimerSystem : ISystem
  {
    // Store the ECB as a field
    private EntityCommandBuffer.ParallelWriter ecbWriter;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
      // Only run when AIThinkingState singleton exists
      state.RequireForUpdate<AIThinkingState>();
    }

    public void OnUpdate(ref SystemState state)
    {
      // CRITICAL: Get the ECB BEFORE entering Burst context
      // This happens on main thread before Burst compilation kicks in
      ecbWriter = ECSActionDispatcher.GetJobCommandBuffer(state.World);

      // Now we can use it in Burst-compiled code
      ExecuteBurstLogic(ref state);

      // Register a dummy handle if needed for synchronization
      ECSActionDispatcher.RegisterJobHandle(state.Dependency, state.World);
    }

    [BurstCompile]
    private void ExecuteBurstLogic(ref SystemState state)
    {
      // Get the thinking state singleton
      if (!SystemAPI.TryGetSingleton<AIThinkingState>(out var thinkingState))
        return;

      // Only process if an enemy is currently thinking
      if (!thinkingState.isThinking)
        return;

      // Check if thinking timer has completed
      double currentTime = SystemAPI.Time.ElapsedTime;

      if (thinkingState.IsThinkingComplete(currentTime)) {
        // Thinking complete! Dispatch action to trigger decision-making
        ecbWriter.DispatchAction(1, new AIReadyToDecideAction
        {
          enemyEntity = thinkingState.thinkingEnemy,
          thinkingDuration = thinkingState.thinkDuration,
          thinkingStartTime = thinkingState.thinkingStartTime
        });
      }
    }
  }

  /// <summary>
  /// PHASE 1: Temporary Trigger System
  /// 
  /// This system detects when the battle phase changes to EnemyTurn and
  /// starts the AI thinking process by setting the AIThinkingState singleton.
  /// 
  /// NOTE: This is a TEMPORARY implementation for Phase 1!
  /// In Phase 4, this will be replaced by a proper reducer that responds
  /// to EnemyTurnStartedAction. For now, we're still working with the
  /// existing battle flow that polls for phase changes.
  /// 
  /// This system:
  /// - Detects phase change to EnemyTurn
  /// - Gets active enemy and their AIBehavior
  /// - Sets AIThinkingState singleton to start thinking
  /// - Dispatches AIThinkingAction for UI feedback
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  [UpdateBefore(typeof(AIThinkingTimerSystem))]
  public partial class AIThinkingTriggerSystem : SystemBase
  {
    private BattlePhase lastPhase = BattlePhase.Initializing;

    protected override void OnCreate()
    {
      base.OnCreate();
      RequireForUpdate<BattleState>();
      RequireForUpdate<PartyState>();
      RequireForUpdate<AIThinkingState>();
    }

    protected override void OnUpdate()
    {
      // Get current battle state
      if (!SystemAPI.TryGetSingleton<BattleState>(out var battleState))
        return;

      // Detect phase change to EnemyTurn
      if (battleState.currentPhase != BattlePhase.EnemyTurn || lastPhase == BattlePhase.EnemyTurn) {
        lastPhase = battleState.currentPhase;
        return;
      }

      lastPhase = battleState.currentPhase;

      // Get party state to find the active enemy
      if (!SystemAPI.TryGetSingleton<PartyState>(out var partyState))
        return;

      // Get active enemy entity
      Entity activeEnemy = GetActiveEnemy(battleState, partyState);
      if (activeEnemy == Entity.Null)
        return;

      // Get AI behavior for this enemy
      if (!EntityManager.HasComponent<AIBehavior>(activeEnemy)) {
        Debug.LogWarning($"Active enemy {activeEnemy.Index} has no AIBehavior component!");
        return;
      }

      var aiBehavior = EntityManager.GetComponentData<AIBehavior>(activeEnemy);

      // Get and update the thinking state singleton
      var thinkingStateEntity = SystemAPI.GetSingletonEntity<AIThinkingState>();
      var thinkingState = EntityManager.GetComponentData<AIThinkingState>(thinkingStateEntity);

      // Start thinking for this enemy
      double currentTime = SystemAPI.Time.ElapsedTime;
      thinkingState.StartThinking(activeEnemy, aiBehavior.thinkingDuration, currentTime);

      EntityManager.SetComponentData(thinkingStateEntity, thinkingState);

      // Dispatch UI feedback action
      ECSActionDispatcher.Dispatch(new AIThinkingAction
      {
        enemyEntity = activeEnemy,
        thinkDuration = aiBehavior.thinkingDuration
      });

      Debug.Log($"Enemy {activeEnemy.Index} started thinking (duration: {aiBehavior.thinkingDuration}s)");
    }

    private Entity GetActiveEnemy(BattleState battleState, PartyState partyState)
    {
      if (battleState.activeCharacterIndex >= battleState.turnOrder.Length)
        return Entity.Null;

      var activeEntity = battleState.turnOrder[battleState.activeCharacterIndex];

      // Verify this is actually an enemy
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == activeEntity &&
            partyState.characters[i].isEnemy &&
            partyState.characters[i].isAlive) {
          return activeEntity;
        }
      }

      return Entity.Null;
    }
  }
}