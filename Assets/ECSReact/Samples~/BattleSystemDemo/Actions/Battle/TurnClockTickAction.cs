using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Advances the active turn's clock. A fixed-lane action: it is dispatched from
  /// TurnClockSystem inside the fixed step, and TurnClockReducer processes it at the end of
  /// that same step, whatever the render rate.
  /// </summary>
  [FixedStepAction]
  public struct TurnClockTickAction : IGameAction
  {
    /// <summary>Simulated seconds to add to BattleState.turnTimer.</summary>
    public float elapsed;
  }
}
