using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Adds each tick to the active turn's timer. TurnClockTickAction is a [FixedStepAction],
  /// so the generator emits this reducer into FixedReducerSystemGroup.
  /// BattleStateReducer resets turnTimer to 0 at the start of every turn.
  /// </summary>
  [Reducer]
  public struct TurnClockReducer : IReducer<BattleState, TurnClockTickAction>
  {
    public void Execute(ref BattleState state, in TurnClockTickAction action, ref SystemState systemState)
    {
      if (!state.battleActive)
        return;

      state.turnTimer += action.elapsed;
    }
  }
}
