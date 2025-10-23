using Unity.Entities;
using Unity.Mathematics;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // NORMALIZED REDUCERS - Each updates its own state slice
  // ============================================================================

  /// <summary>
  /// Applies damage to character health in CharacterHealthState.
  /// Updates alive status and cached counters when character dies.
  /// Pure function - all context provided in action.
  /// </summary>
  [Reducer]
  public struct HealthDamageReducer : IReducer<CharacterHealthState, ApplyDamageAction>
  {
    public void Execute(
      ref CharacterHealthState state,
      in ApplyDamageAction action,
      ref SystemState systemState)
    {
      // Verify health HashMap exists
      if (!state.health.IsCreated)
        return;

      // Get current health data
      if (!state.health.TryGetValue(action.targetEntity, out var healthData))
        return;

      // Apply damage (using math.max for SIMD optimization)
      healthData.current = math.max(0, healthData.current - action.finalDamage);

      // Check for death
      bool justDied = healthData.current <= 0 && healthData.isAlive;
      if (justDied) {
        healthData.isAlive = false;

        // Update cached alive counters
        state.totalAliveCount--;
        if (action.isTargetEnemy)
          state.aliveEnemyCount--;
        else
          state.alivePlayerCount--;
      }

      // Update health data
      state.health[action.targetEntity] = healthData;
    }
  }

  /// <summary>
  /// Clears Defending status after character takes damage.
  /// Pure function - action tells us if we need to clear the flag.
  /// </summary>
  [Reducer]
  public struct StatusDamageReducer : IReducer<CharacterStatusState, ApplyDamageAction>
  {
    public void Execute(
      ref CharacterStatusState state,
      in ApplyDamageAction action,
      ref SystemState systemState)
    {
      // Only clear Defending flag if character was defending
      if (!action.wasDefending)
        return;

      // Verify status HashMap exists
      if (!state.statuses.IsCreated)
        return;

      // Get current status
      if (!state.statuses.TryGetValue(action.targetEntity, out var status))
        return;

      // Clear Defending flag (bitwise operation)
      status &= ~CharacterStatus.Defending;

      // Update status
      state.statuses[action.targetEntity] = status;
    }
  }
}