using Unity.Entities;
using Unity.Mathematics;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // ENRICHED ACTION - Dispatched by middleware with calculated damage
  // ============================================================================

  /// <summary>
  /// Internal action dispatched after damage calculation.
  /// Contains all context needed by normalized state reducers.
  /// </summary>
  public struct ApplyDamageAction : IGameAction
  {
    public Entity targetEntity;
    public int finalDamage;        // Pre-calculated with all modifiers
    public bool wasDefending;      // Was target defending? (for clearing flag)
    public bool isTargetEnemy;     // Is target an enemy? (for counter updates)
    public bool wasCritical;       // Was it a critical hit? (for UI feedback)
  }
}