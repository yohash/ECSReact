using System;
using System.Linq;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using ECSReact.Core;

namespace ECSReact.Tests
{
  /// <summary>
  /// Timing rules of the fixed-step action lane (issue #12), checked at a low and a high
  /// render rate against a 50 Hz fixed step.
  ///
  /// Each test builds its own world containing only the ECSReact action pipeline and the test
  /// systems in FixedLaneTestFixtures.cs, and drives time manually (no UpdateWorldTimeSystem),
  /// so frame and step counts are exact and repeatable.
  /// </summary>
  public class FixedLaneTimingTests
  {
    private const float FixedTimestep = 0.02f; // 50 Hz

    private static readonly Type[] SystemTypes =
    {
      // Unity
      typeof(BeginInitializationEntityCommandBufferSystem),
      typeof(FixedStepSimulationSystemGroup),

      // ECSReact frame lane
      typeof(ECSActionDispatcherInitSystem),
      typeof(JobActionCollectorSystem),
      typeof(MiddlewareSystemGroup),
      typeof(ReducerSystemGroup),
      typeof(ActionCleanupSystemGroup),
      typeof(ActionCleanupSystem),

      // ECSReact fixed lane
      typeof(FixedActionSystemGroup),
      typeof(FixedActionCommandBufferSystem),
      typeof(FixedMiddlewareSystemGroup),
      typeof(FixedReducerSystemGroup),
      typeof(FixedActionCleanupSystem),

      // Test systems
      typeof(StepCounterSystem),
      typeof(FixedStepDispatchSystem),
      typeof(ChainingFixedMiddlewareSystem),
      typeof(FrameMiddlewareDispatchingFixedSystem),
      typeof(FixedLaneRecorderSystem),
      typeof(FrameLaneRecorderSystem),
    };

    private World world;
    private double elapsed;
    private EntityQuery fixedTagQuery;
    private EntityQuery actionTagQuery;

    [SetUp]
    public void SetUp()
    {
      TestLog.Reset();
      elapsed = 0;

      world = new World("ECSReact Fixed Lane Test World");
      DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, SystemTypes);
      world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep = FixedTimestep;

      fixedTagQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FixedActionTag>());
      actionTagQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ActionTag>());
    }

    [TearDown]
    public void TearDown()
    {
      if (world != null && world.IsCreated)
        world.Dispose();
      world = null;
    }

    // ─────────────────────────────── Helpers ───────────────────────────────

    /// <summary>Runs one rendered frame of length <paramref name="deltaTime"/>.</summary>
    private void RunFrame(double deltaTime)
    {
      TestLog.Frame++;
      elapsed += deltaTime;
      world.SetTime(new TimeData(elapsed, (float)deltaTime));
      world.Update();

      // Action entities never outlive their phase: fixed-lane ones only exist inside
      // FixedActionSystemGroup, frame-lane ones are destroyed by ActionCleanupSystem.
      Assert.AreEqual(0, fixedTagQuery.CalculateEntityCount(), "FixedActionTag entities left after a frame");
      Assert.AreEqual(0, actionTagQuery.CalculateEntityCount(), "ActionTag entities left after a frame");
    }

    /// <summary>Runs <paramref name="seconds"/> of frames at <paramref name="fps"/>, calling afterFrame after each.</summary>
    private void RunSeconds(int fps, double seconds, Action afterFrame = null)
    {
      int frames = (int)Math.Round(seconds * fps);
      for (int i = 0; i < frames; i++) {
        RunFrame(1.0 / fps);
        afterFrame?.Invoke();
      }
    }

    private static void AssertLanesIsolated()
    {
      Assert.AreEqual(0, TestLog.FixedActionsSeenByFrameLane, "A fixed-lane action was visible to the frame lane");
    }

    // ──────────────────────────────── Tests ────────────────────────────────

    [Test]
    public void ActionLane_ReportsLanePerType()
    {
      // The registry is filled when the test world's dispatcher context is created
      Assert.IsTrue(ActionLane<TestFixedAction>.IsFixed);
      Assert.IsTrue(ActionLane<TestChainedAction>.IsFixed);
      Assert.IsFalse(ActionLane<TestFrameAction>.IsFixed);
    }

    [TestCase(30)]
    [TestCase(170)]
    public void FixedAction_DispatchedFromFixedStep_IsProcessedInSameStep(int fps)
    {
      TestLog.DispatchFixedFromFixedStep = true;

      RunSeconds(fps, 2.0);

      Assert.That(TestLog.Step, Is.GreaterThanOrEqualTo(100), "expected ~2 s of 50 Hz steps");
      Assert.AreEqual(TestLog.Step, TestLog.FixedActions.Count, "one action dispatched and processed per step");
      foreach (var p in TestLog.FixedActions)
        Assert.AreEqual(p.dispatched, p.processed, $"dispatched in step {p.dispatched}");
      AssertLanesIsolated();
    }

    [TestCase(30)]
    [TestCase(170)]
    public void ChainedFixedAction_FromFixedMiddleware_IsProcessedNextStep(int fps)
    {
      TestLog.DispatchFixedFromFixedStep = true;
      TestLog.ChainFromFixedMiddleware = true;

      RunSeconds(fps, 2.0);

      // The chain dispatched in the final step is still waiting for the next one
      Assert.AreEqual(TestLog.Step - 1, TestLog.ChainedActions.Count);
      foreach (var p in TestLog.ChainedActions)
        Assert.AreEqual(p.dispatched + 1, p.processed, $"chained in step {p.dispatched}");
      AssertLanesIsolated();
    }

    [TestCase(30)]
    [TestCase(170)]
    public void FixedAction_DispatchedFromFrameCode_IsProcessedAtNextStep_AcrossZeroStepFrames(int fps)
    {
      int dispatched = 0;
      int framesWithoutStep = 0;
      int stepBeforeFrame = 0;

      RunSeconds(fps, 2.0, afterFrame: () => {
        if (TestLog.Step == stepBeforeFrame)
          framesWithoutStep++;
        stepBeforeFrame = TestLog.Step;

        // Like a UI click: dispatched between frames, from frame-rate code
        ECSActionDispatcher.Dispatch(new TestFixedAction { dispatchedStep = TestLog.Step }, world);
        dispatched++;
      });

      if (fps > 50)
        Assert.That(framesWithoutStep, Is.GreaterThan(0), "test must cover frames that run no fixed step");

      // Dispatches made after the final step are still waiting
      int pending = dispatched - TestLog.FixedActions.Count;
      Assert.That(pending, Is.GreaterThanOrEqualTo(1));
      Assert.That(pending, Is.LessThanOrEqualTo((int)Math.Ceiling(fps * FixedTimestep) + 1), "nothing may be dropped");

      foreach (var p in TestLog.FixedActions)
        Assert.AreEqual(p.dispatched + 1, p.processed, $"dispatched after step {p.dispatched}");
      AssertLanesIsolated();
    }

    [TestCase(30)]
    [TestCase(170)]
    public void FrameAction_FromFixedMiddleware_IsProcessedNextFrame(int fps)
    {
      TestLog.DispatchFixedFromFixedStep = true;
      TestLog.CrossLaneFromFixedMiddleware = true;

      RunSeconds(fps, 1.0);

      var fromMiddleware = TestLog.FrameActions.Where(p => p.fromFixedMiddleware).ToList();
      Assert.That(fromMiddleware.Count, Is.GreaterThan(0));
      foreach (var p in fromMiddleware)
        Assert.AreEqual(p.dispatched + 1, p.processed, $"dispatched in frame {p.dispatched}");
      AssertLanesIsolated();
    }

    [TestCase(30)]
    [TestCase(170)]
    public void FixedAction_FromFrameMiddleware_IsProcessedAtNextStep(int fps)
    {
      TestLog.FixedFromFrameMiddleware = true;
      int framesWithoutStep = 0;
      int stepBeforeFrame = 0;

      RunSeconds(fps, 2.0, afterFrame: () => {
        if (TestLog.Step == stepBeforeFrame)
          framesWithoutStep++;
        stepBeforeFrame = TestLog.Step;

        // Frame-lane action from frame code; the frame middleware turns it into a fixed-lane one
        ECSActionDispatcher.Dispatch(new TestFrameAction { dispatchedFrame = TestLog.Frame }, world);
      });

      if (fps > 50)
        Assert.That(framesWithoutStep, Is.GreaterThan(0), "test must cover frames that run no fixed step");

      Assert.That(TestLog.FixedDispatchedByFrameMiddleware, Is.GreaterThan(0));
      Assert.That(TestLog.FixedActions.Count, Is.GreaterThan(0));

      // Frame middleware runs after this frame's fixed steps, so its dispatches wait for the next step.
      // Those made after the final step are still pending; nothing may be dropped.
      int pending = TestLog.FixedDispatchedByFrameMiddleware - TestLog.FixedActions.Count;
      Assert.That(pending, Is.GreaterThanOrEqualTo(1));
      Assert.That(pending, Is.LessThanOrEqualTo((int)Math.Ceiling(fps * FixedTimestep) + 1), "nothing may be dropped");

      foreach (var p in TestLog.FixedActions)
        Assert.AreEqual(p.dispatched + 1, p.processed, $"dispatched by frame middleware after step {p.dispatched}");
      AssertLanesIsolated();
    }

    [TestCase(30)]
    [TestCase(170)]
    public void FrameLane_IsUnchanged(int fps)
    {
      TestLog.DispatchFrameFromFixedStep = true;
      int dispatchedFromFrameCode = 0;

      RunSeconds(fps, 1.0, afterFrame: () => {
        ECSActionDispatcher.Dispatch(new TestFrameAction { dispatchedFrame = TestLog.Frame }, world);
        dispatchedFromFrameCode++;
      });

      // Every frame-lane action, from frame code or from inside a fixed step, is processed next frame
      Assert.That(TestLog.FrameActions.Count, Is.GreaterThan(0));
      foreach (var p in TestLog.FrameActions)
        Assert.AreEqual(p.dispatched + 1, p.processed, $"dispatched in frame {p.dispatched}");

      // Frame code dispatched once per frame and only its last dispatch is still pending,
      // so at least that many were processed (plus those dispatched from fixed steps)
      Assert.That(TestLog.FrameActions.Count, Is.GreaterThanOrEqualTo(dispatchedFromFrameCode - 1));
      AssertLanesIsolated();
    }

    [Test]
    public void Paused_TimeScaleZero_DispatchesAreStillProcessed()
    {
      RunFrame(1.0 / 60);
      RunFrame(1.0 / 60);
      int stepWhenPaused = TestLog.Step;

      // Paused: delta 0, ElapsedTime doesn't advance, no fixed steps run.
      // The pre-0.2.0 dispatcher reused an already played-back buffer here and threw.
      for (int i = 0; i < 5; i++) {
        ECSActionDispatcher.Dispatch(new TestFrameAction { dispatchedFrame = TestLog.Frame }, world);
        ECSActionDispatcher.Dispatch(new TestFixedAction { dispatchedStep = TestLog.Step }, world);
        Assert.DoesNotThrow(() => RunFrame(0));
      }

      Assert.AreEqual(stepWhenPaused, TestLog.Step, "no fixed steps while paused");
      Assert.AreEqual(5, TestLog.FrameActions.Count, "frame lane keeps running while paused");
      foreach (var p in TestLog.FrameActions)
        Assert.AreEqual(p.dispatched + 1, p.processed);
      Assert.AreEqual(0, TestLog.FixedActions.Count, "fixed-lane actions wait for the next step");

      // Unpause: all five fixed-lane actions resolve together in the first step
      while (TestLog.Step == stepWhenPaused)
        RunFrame(1.0 / 60);

      Assert.AreEqual(5, TestLog.FixedActions.Count, "nothing dropped while paused");
      foreach (var p in TestLog.FixedActions)
        Assert.AreEqual(stepWhenPaused + 1, p.processed);
    }

    // ───────────────────────── Job-handle race fix ─────────────────────────

    private struct DispatchFrameActionJob : IJob
    {
      public EntityCommandBuffer.ParallelWriter Ecb;
      public int Frame;
      public void Execute() => Ecb.DispatchAction(0, new TestFrameAction { dispatchedFrame = Frame });
    }

    private struct DispatchFixedActionJob : IJob
    {
      public EntityCommandBuffer.ParallelWriter Ecb;
      public int Step;
      public void Execute() => Ecb.DispatchAction(0, new TestFixedAction { dispatchedStep = Step });
    }

    [Test]
    public void RegisteredJob_IsCompletedBeforeFrameLanePlayback()
    {
      RunFrame(1.0 / 60);

      // Scheduled but never completed by the test: playback must wait for it
      var handle = new DispatchFrameActionJob {
        Ecb = ECSActionDispatcher.GetJobCommandBuffer(world),
        Frame = TestLog.Frame
      }.Schedule();
      ECSActionDispatcher.RegisterJobHandle(handle, world);

      Assert.DoesNotThrow(() => RunFrame(1.0 / 60));
      Assert.AreEqual(1, TestLog.FrameActions.Count);
      Assert.AreEqual(TestLog.FrameActions[0].dispatched + 1, TestLog.FrameActions[0].processed);
    }

    [Test]
    public void RegisteredJob_IsCompletedBeforeFixedLanePlayback()
    {
      RunFrame(1.0 / 60);
      int stepAtSchedule = TestLog.Step;

      var handle = new DispatchFixedActionJob {
        Ecb = ECSActionDispatcher.GetFixedJobCommandBuffer(world),
        Step = stepAtSchedule
      }.Schedule();
      ECSActionDispatcher.RegisterJobHandle(handle, world);

      Assert.DoesNotThrow(() => {
        while (TestLog.Step == stepAtSchedule)
          RunFrame(1.0 / 60);
      });
      Assert.AreEqual(1, TestLog.FixedActions.Count);
      Assert.AreEqual(stepAtSchedule + 1, TestLog.FixedActions[0].processed);
    }
  }
}
