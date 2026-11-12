using Unity.Entities;
using Unity.Mathematics;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Fixed-step lane example. Counts fixed steps during the active turn and dispatches a
  /// TurnClockTickAction once per simulated second.
  ///
  /// Because it runs in FixedStepSimulationSystemGroup and the action is a [FixedStepAction],
  /// the tick is processed in the same step (in FixedActionSystemGroup, which runs last in
  /// every step). The turn clock therefore advances in whole simulated seconds counted in
  /// steps, independent of frame rate. Ticking once per second instead of every step keeps
  /// BattleState change notifications (and the dashboard's action history) quiet.
  /// </summary>
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  public partial class TurnClockSystem : SystemBase
  {
    private int stepsThisTurn;
    private int observedTurn = -1;

    protected override void OnCreate()
    {
      RequireForUpdate<BattleState>();
    }

    protected override void OnUpdate()
    {
      var battleState = SystemAPI.GetSingleton<BattleState>();
      if (!battleState.battleActive)
        return;

      // The turn timer restarts with every turn, so the step count does too
      if (battleState.turnCount != observedTurn) {
        observedTurn = battleState.turnCount;
        stepsThisTurn = 0;
      }

      // Inside the fixed step, DeltaTime is the fixed timestep
      float fixedDelta = SystemAPI.Time.DeltaTime;
      int stepsPerTick = math.max(1, (int)math.round(1f / fixedDelta));

      stepsThisTurn++;
      if (stepsThisTurn % stepsPerTick == 0) {
        ECSActionDispatcher.Dispatch(new TurnClockTickAction { elapsed = stepsPerTick * fixedDelta });
      }
    }
  }
}
