using Unity.Entities;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Turn Advancement System - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState dependency
  /// - Added CharacterIdentityState dependency
  /// - Replaced O(n) loop with O(1) HashMap lookup to determine player/enemy
  /// 
  /// Handles turn progression and combat action dispatching after AI execution.
  /// This is NOT an AI decision system - all AI logic happens in reducers.
  /// 
  /// Responsibilities:
  /// - Watch for readyToExecuteCombat flag in AIThinkingState
  /// - Dispatch pre-calculated combat actions
  /// - Advance to next turn after action execution
  /// 
  /// Clean separation: AI thinks → reducer calculates → this dispatches → turn advances
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(ReducerSystemGroup))]
  public partial class TurnAdvancementSystem : SystemBase
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      RequireForUpdate<AIThinkingState>();
      RequireForUpdate<BattleState>();
      RequireForUpdate<CharacterIdentityState>(); // Changed from PartyState
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
      Debug.Log($"TurnAdvancementSystem: Dispatching {action} from entity {executor.Index} with pre-calculated data");
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
    /// NEW: Uses CharacterIdentityState for O(1) player/enemy lookup.
    /// OLD: Looped through PartyState.characters array (O(n)).
    /// </summary>
    private void AdvanceToNextTurn()
    {
      // Dispatch turn advance action
      ECSActionDispatcher.Dispatch(new ReadyForNextTurn());
    }
  }
}