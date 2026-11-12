using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using ECSReact.Core;

// Test-only actions and systems for FixedLaneTimingTests.
// Every system is [DisableAutoCreation] so it only runs in the test world, never in the
// default world that play mode creates. Systems are placed with plain
// [UpdateInGroup(typeof(...))]: Entities matches that attribute by exact type, so
// UpdateInGroupAttribute subclasses are ignored.
namespace ECSReact.Tests
{
  // ─────────────────────────────── Actions ───────────────────────────────

  [FixedStepAction]
  public struct TestFixedAction : IGameAction
  {
    public int dispatchedStep;
  }

  [FixedStepAction]
  public struct TestChainedAction : IGameAction
  {
    public int dispatchedStep;
  }

  public struct TestFrameAction : IGameAction
  {
    public int dispatchedFrame;
    public bool fromFixedMiddleware;
  }

  // ───────────────────────────── Shared log ──────────────────────────────

  public struct Processed
  {
    public int dispatched; // step or frame index at dispatch
    public int processed;  // step or frame index when a reducer saw it
    public bool fromFixedMiddleware;
  }

  /// <summary>Clock, switches and records shared by the test systems. Reset per test.</summary>
  public static class TestLog
  {
    // Clock
    public static int Frame; // incremented by the test before every World.Update
    public static int Step;  // incremented by StepCounterSystem at the start of every fixed step

    // Switches
    public static bool DispatchFixedFromFixedStep;
    public static bool DispatchFrameFromFixedStep;
    public static bool ChainFromFixedMiddleware;
    public static bool CrossLaneFromFixedMiddleware;
    public static bool FixedFromFrameMiddleware;

    // Records
    public static readonly List<Processed> FixedActions = new List<Processed>();
    public static readonly List<Processed> ChainedActions = new List<Processed>();
    public static readonly List<Processed> FrameActions = new List<Processed>();
    public static int FixedActionsSeenByFrameLane;
    public static int FixedDispatchedByFrameMiddleware;

    public static void Reset()
    {
      Frame = 0;
      Step = 0;
      DispatchFixedFromFixedStep = false;
      DispatchFrameFromFixedStep = false;
      ChainFromFixedMiddleware = false;
      CrossLaneFromFixedMiddleware = false;
      FixedFromFrameMiddleware = false;
      FixedActions.Clear();
      ChainedActions.Clear();
      FrameActions.Clear();
      FixedActionsSeenByFrameLane = 0;
      FixedDispatchedByFrameMiddleware = 0;
    }
  }

  // ─────────────────────────────── Systems ───────────────────────────────

  /// <summary>Counts fixed steps. Runs first in every step.</summary>
  [DisableAutoCreation]
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
  public partial class StepCounterSystem : SystemBase
  {
    protected override void OnUpdate() => TestLog.Step++;
  }

  /// <summary>A gameplay system inside the fixed step, before the fixed action phase.</summary>
  [DisableAutoCreation]
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  public partial class FixedStepDispatchSystem : SystemBase
  {
    protected override void OnUpdate()
    {
      if (TestLog.DispatchFixedFromFixedStep)
        ECSActionDispatcher.Dispatch(new TestFixedAction { dispatchedStep = TestLog.Step }, World);

      if (TestLog.DispatchFrameFromFixedStep)
        ECSActionDispatcher.Dispatch(new TestFrameAction { dispatchedFrame = TestLog.Frame }, World);
    }
  }

  /// <summary>
  /// Hand-written fixed-lane middleware, equivalent to a generated one: dispatches through an
  /// ActionWriter obtained for the fixed lane.
  /// </summary>
  [DisableAutoCreation]
  [UpdateInGroup(typeof(FixedMiddlewareSystemGroup))]
  public partial class ChainingFixedMiddlewareSystem : SystemBase
  {
    private EntityQuery query;

    protected override void OnCreate()
    {
      query = GetEntityQuery(ComponentType.ReadOnly<TestFixedAction>(), ComponentType.ReadOnly<FixedActionTag>());
    }

    protected override void OnUpdate()
    {
      if (query.IsEmpty)
        return;

      var dispatch = ECSActionDispatcher.GetActionWriter(World, isFixedLane: true);
      int count = query.CalculateEntityCount();
      for (int i = 0; i < count; i++) {
        if (TestLog.ChainFromFixedMiddleware)
          dispatch.Dispatch(new TestChainedAction { dispatchedStep = TestLog.Step });
        if (TestLog.CrossLaneFromFixedMiddleware)
          dispatch.Dispatch(new TestFrameAction { dispatchedFrame = TestLog.Frame, fromFixedMiddleware = true });
      }
    }
  }

  /// <summary>
  /// Hand-written frame-lane middleware, equivalent to a generated one: for every frame-lane
  /// action it sees, dispatches a fixed-lane action through an ActionWriter obtained for the
  /// frame lane.
  /// </summary>
  [DisableAutoCreation]
  [UpdateInGroup(typeof(MiddlewareSystemGroup))]
  public partial class FrameMiddlewareDispatchingFixedSystem : SystemBase
  {
    private EntityQuery query;

    protected override void OnCreate()
    {
      query = GetEntityQuery(ComponentType.ReadOnly<TestFrameAction>(), ComponentType.ReadOnly<ActionTag>());
    }

    protected override void OnUpdate()
    {
      if (!TestLog.FixedFromFrameMiddleware || query.IsEmpty)
        return;

      var dispatch = ECSActionDispatcher.GetActionWriter(World, isFixedLane: false);
      int count = query.CalculateEntityCount();
      for (int i = 0; i < count; i++) {
        dispatch.Dispatch(new TestFixedAction { dispatchedStep = TestLog.Step });
        TestLog.FixedDispatchedByFrameMiddleware++;
      }
    }
  }

  /// <summary>Fixed-lane "reducer": records the step each fixed-lane action is processed in.</summary>
  [DisableAutoCreation]
  [UpdateInGroup(typeof(FixedReducerSystemGroup))]
  public partial class FixedLaneRecorderSystem : SystemBase
  {
    private EntityQuery fixedQuery;
    private EntityQuery chainedQuery;

    protected override void OnCreate()
    {
      fixedQuery = GetEntityQuery(ComponentType.ReadOnly<TestFixedAction>(), ComponentType.ReadOnly<FixedActionTag>());
      chainedQuery = GetEntityQuery(ComponentType.ReadOnly<TestChainedAction>(), ComponentType.ReadOnly<FixedActionTag>());
    }

    protected override void OnUpdate()
    {
      using (var actions = fixedQuery.ToComponentDataArray<TestFixedAction>(Allocator.Temp)) {
        foreach (var a in actions)
          TestLog.FixedActions.Add(new Processed { dispatched = a.dispatchedStep, processed = TestLog.Step });
      }
      using (var actions = chainedQuery.ToComponentDataArray<TestChainedAction>(Allocator.Temp)) {
        foreach (var a in actions)
          TestLog.ChainedActions.Add(new Processed { dispatched = a.dispatchedStep, processed = TestLog.Step });
      }
    }
  }

  /// <summary>Frame-lane "reducer": records the frame each frame-lane action is processed in.</summary>
  [DisableAutoCreation]
  [UpdateInGroup(typeof(ReducerSystemGroup))]
  public partial class FrameLaneRecorderSystem : SystemBase
  {
    private EntityQuery frameQuery;
    private EntityQuery leakedFixedQuery;

    protected override void OnCreate()
    {
      frameQuery = GetEntityQuery(ComponentType.ReadOnly<TestFrameAction>(), ComponentType.ReadOnly<ActionTag>());
      // A fixed-lane action must never be visible to the frame lane
      leakedFixedQuery = GetEntityQuery(ComponentType.ReadOnly<TestFixedAction>());
    }

    protected override void OnUpdate()
    {
      using (var actions = frameQuery.ToComponentDataArray<TestFrameAction>(Allocator.Temp)) {
        foreach (var a in actions)
          TestLog.FrameActions.Add(new Processed {
            dispatched = a.dispatchedFrame,
            processed = TestLog.Frame,
            fromFixedMiddleware = a.fromFixedMiddleware
          });
      }
      TestLog.FixedActionsSeenByFrameLane += leakedFixedQuery.CalculateEntityCount();
    }
  }
}
