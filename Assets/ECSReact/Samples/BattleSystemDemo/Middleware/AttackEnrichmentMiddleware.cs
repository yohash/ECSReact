using Unity.Entities;
using Unity.Mathematics;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // MIDDLEWARE - Enriches AttackAction with calculated damage
  // ============================================================================

  /// <summary>
  /// Middleware that calculates final damage considering:
  /// - Base damage from action
  /// - Critical hit multiplier
  /// - Defending status (halves damage)
  /// Dispatches ApplyDamageAction so reducers can remain pure.
  /// </summary>
  [Middleware(Order = 20)]
  public struct AttackEnrichmentMiddleware : IMiddleware<AttackAction>
  {
    public bool Process(
      ref AttackAction action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter ecb,
      int sortKey)
    {
      // Get required states for damage calculation
      if (!systemState.TryGetSingleton<CharacterStatusState>(out var statusState))
        return true; // No status state, let original action through

      if (!systemState.TryGetSingleton<CharacterIdentityState>(out var identityState))
        return true; // No identity state, let original action through

      // Calculate final damage
      int finalDamage = action.baseDamage;

      // Apply critical multiplier
      if (action.isCritical) {
        finalDamage *= 2;
      }

      // Check if target is defending
      bool wasDefending = false;
      if (statusState.statuses.IsCreated &&
          statusState.statuses.TryGetValue(action.targetEntity, out var status)) {
        wasDefending = (status & CharacterStatus.Defending) != 0;
        if (wasDefending) {
          finalDamage /= 2; // Defending halves damage
        }
      }

      // Get target team affiliation
      bool isTargetEnemy = false;
      if (identityState.isEnemy.IsCreated &&
          identityState.isEnemy.TryGetValue(action.targetEntity, out var enemyFlag)) {
        isTargetEnemy = enemyFlag;
      }

      // Dispatch enriched action with calculated damage
      ecb.DispatchAction(sortKey,
        new ApplyDamageAction
        {
          targetEntity = action.targetEntity,
          finalDamage = finalDamage,
          wasDefending = wasDefending,
          isTargetEnemy = isTargetEnemy,
          wasCritical = action.isCritical
        });

      // Allow original AttackAction through as well
      // (other systems like BattleStateReducer may need it)
      return true;
    }
  }
}