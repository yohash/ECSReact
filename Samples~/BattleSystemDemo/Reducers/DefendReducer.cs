using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // DEFEND REDUCER - Sets Defending status flag
  // ============================================================================

  /// <summary>
  /// Sets Defending status when character chooses Defend action.
  /// Pure reducer - only modifies CharacterStatusState based on action.
  /// 
  /// Defending status:
  /// - Halves incoming damage (applied in AttackEnrichmentMiddleware)
  /// - Cleared after taking damage (StatusDamageReducer)
  /// </summary>
  [Reducer]
  public struct DefendReducer : IReducer<CharacterStatusState, SelectActionTypeAction>
  {
    public void Execute(
      ref CharacterStatusState state,
      in SelectActionTypeAction action,
      ref SystemState systemState)
    {
      // Only process Defend actions
      if (action.actionType != ActionType.Defend)
        return;

      // Verify status HashMap exists
      if (!state.statuses.IsCreated) {
        state.statuses = new NativeHashMap<Entity, CharacterStatus>(16, Allocator.Persistent);
      }

      // Get or create current status for this character
      CharacterStatus currentStatus = CharacterStatus.None;
      if (state.statuses.TryGetValue(action.actingCharacter, out var existingStatus)) {
        currentStatus = existingStatus;
      }

      // Set Defending flag (bitwise OR operation)
      currentStatus |= CharacterStatus.Defending;

      // Update status in HashMap
      state.statuses[action.actingCharacter] = currentStatus;
    }
  }
}