using Unity.Entities;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// PHASE 3: Repurposed Enemy AI System - Simple Reactive Dispatcher
  /// 
  /// This system is now dramatically simplified:
  /// - Watches AIThinkingState.readyToExecuteCombat flag
  /// - Dispatches pre-calculated combat actions
  /// - Advances turn
  /// 
  /// All the heavy lifting (decision-making, combat calculation) happens
  /// in pure reducers. This system is just a thin reactive layer that
  /// reads state and performs side effects (dispatching actions).
  /// 
  /// Flow:
  /// 1. Detect: readyToExecuteCombat = true
  /// 2. Read: Pre-calculated combat details from state
  /// 3. Dispatch: Appropriate combat action with those details
  /// 4. Advance: Turn progression
  /// 
  /// Simple, focused, and easy to understand!
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  public partial class EnemyAISystem : SystemBase
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      RequireForUpdate<AIThinkingState>();
      RequireForUpdate<BattleState>();
      RequireForUpdate<PartyState>();
    }

    protected override void OnUpdate()
    {
      // Get thinking state singleton
      if (!SystemAPI.TryGetSingleton<AIThinkingState>(out var thinkingState))
        return;

      // Check if there's a combat action ready to execute
      if (!thinkingState.readyToExecuteCombat)
        return;

      // Read pre-calculated combat details from state
      Entity executor = thinkingState.combatExecutor;
      ActionType action = thinkingState.combatAction;
      Entity target = thinkingState.combatTarget;
      int damage = thinkingState.combatDamage;
      bool isCritical = thinkingState.combatIsCritical;
      int skillId = thinkingState.combatSkillId;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
      Debug.Log($"EnemyAISystem: Dispatching {action} from entity {executor.Index} with pre-calculated data");
#endif

      // Dispatch the appropriate combat action
      DispatchCombatAction(executor, action, target, damage, isCritical, skillId);

      // Advance turn after action
      AdvanceToNextTurn();
    }

    /// <summary>
    /// Dispatch the appropriate combat action with pre-calculated details.
    /// All values come from state (calculated by AIExecutionReducer).
    /// </summary>
    private void DispatchCombatAction(
        Entity executor,
        ActionType action,
        Entity target,
        int damage,
        bool isCritical,
        int skillId)
    {
      switch (action) {
        case ActionType.Attack:
          if (target == Entity.Null) {
            Debug.LogWarning($"Attack action has no target! Executor: {executor.Index}");
            return;
          }

          ECSActionDispatcher.Dispatch(new AttackAction
          {
            attackerEntity = executor,
            targetEntity = target,
            baseDamage = damage,          // Pre-calculated!
            isCritical = isCritical       // Pre-calculated!
          });
          break;

        case ActionType.Defend:
          ECSActionDispatcher.Dispatch(new SelectActionTypeAction
          {
            actionType = ActionType.Defend,
            actingCharacter = executor
          });
          break;

        case ActionType.Skill:
          // TODO: Implement proper skill system
          Debug.Log($"Skill action dispatched (skill ID: {skillId}, damage: {damage}) - not yet fully implemented");

          // For now, treat as attack
          if (target != Entity.Null) {
            ECSActionDispatcher.Dispatch(new AttackAction
            {
              attackerEntity = executor,
              targetEntity = target,
              baseDamage = damage,
              isCritical = isCritical
            });
          }
          break;

        case ActionType.Run:
          Debug.Log("Enemy attempted to run (unusual behavior)");
          break;

        default:
          Debug.LogWarning($"Unknown action type: {action}");
          break;
      }
    }

    /// <summary>
    /// Advance to next turn after combat action is dispatched.
    /// </summary>
    private void AdvanceToNextTurn()
    {
      // Get fresh state for turn advancement
      if (!SystemAPI.TryGetSingleton<BattleState>(out var battleState))
        return;
      if (!SystemAPI.TryGetSingleton<PartyState>(out var partyState))
        return;

      // Calculate next turn index
      var nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      if (nextIndex >= battleState.turnOrder.Length)
        return;

      var nextEntity = battleState.turnOrder[nextIndex];

      // Determine if next turn is player or enemy
      bool isPlayerTurn = false;
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == nextEntity) {
          isPlayerTurn = !partyState.characters[i].isEnemy;
          break;
        }
      }

      // Dispatch turn advance action
      ECSActionDispatcher.Dispatch(new NextTurnAction
      {
        skipAnimation = false,
        isPlayerTurn = isPlayerTurn
      });
    }
  }
}