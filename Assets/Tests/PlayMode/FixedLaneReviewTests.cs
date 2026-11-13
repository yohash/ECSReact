using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Tests
{
  /// <summary>
  /// Review coverage for PR #15 (issue #12), filling gaps left by FixedLaneTimingTests:
  /// - the fixtures' [UpdateInGroup] attributes really place systems in their groups;
  /// - the dispatcher can initialise whatever order a world creates its systems in;
  /// - ActionLane&lt;T&gt; reads correctly from Burst-compiled jobs, and DispatchAction tags by lane there;
  /// - frames that run several fixed steps process each step's dispatch exactly once;
  /// - a fixed-lane action recorded into the frame-lane writer is still processed once, in the fixed lane.
  ///
  /// Recorder systems are placed in their groups by hand here, so the timing tests don't depend on the
  /// attribute behaviour that GroupAttributes_PlaceSystemsInTheirGroups checks.
  /// </summary>
  public class FixedLaneReviewTests
  {
    private const float FixedTimestep = 0.02f; // 50 Hz

    // Created in an order where every EntityCommandBufferSystem precedes ECSActionDispatcherInitSystem,
    // so the dispatcher's OnCreate-time CreateCommandBuffer() call works (see InitSystemFirst test).
    private static readonly Type[] PipelineTypes =
    {
      typeof(BeginInitializationEntityCommandBufferSystem),
      typeof(FixedStepSimulationSystemGroup),
      typeof(JobActionCollectorSystem),
      typeof(MiddlewareSystemGroup),
      typeof(ReducerSystemGroup),
      typeof(ActionCleanupSystemGroup),
      typeof(ActionCleanupSystem),
      typeof(FixedActionSystemGroup),
      typeof(FixedActionCommandBufferSystem),
      typeof(FixedMiddlewareSystemGroup),
      typeof(FixedReducerSystemGroup),
      typeof(FixedActionCleanupSystem),
      typeof(ECSActionDispatcherInitSystem),
      typeof(StepCounterSystem),
      typeof(FixedStepDispatchSystem),
    };

    private World world;
    private double elapsed;
    private readonly List<string> loggedErrors = new List<string>();

    [SetUp]
    public void SetUp()
    {
      TestLog.Reset();
      elapsed = 0;
      loggedErrors.Clear();
      Application.logMessageReceived += OnLog;
    }

    [TearDown]
    public void TearDown()
    {
      Application.logMessageReceived -= OnLog;
      if (world != null && world.IsCreated)
        world.Dispose();
      world = null;
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
      if (type == LogType.Exception || type == LogType.Error)
        loggedErrors.Add(condition);
    }

    // ─────────────────────────────── Helpers ───────────────────────────────

    /// <summary>Builds the action pipeline plus the recorder systems, each placed in its group by hand.</summary>
    private void BuildWorld()
    {
      world = new World("ECSReact Fixed Lane Review World");
      DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, PipelineTypes);
      world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep = FixedTimestep;

      AddTo<FixedReducerSystemGroup>(world.CreateSystemManaged<FixedLaneRecorderSystem>());
      AddTo<ReducerSystemGroup>(world.CreateSystemManaged<FrameLaneRecorderSystem>());
      world.GetExistingSystemManaged<SimulationSystemGroup>().SortSystems();
    }

    private void AddTo<TGroup>(ComponentSystemBase system) where TGroup : ComponentSystemGroup
    {
      var group = world.GetExistingSystemManaged<TGroup>();
      group.AddSystemToUpdateList(system);
      group.SortSystems();
    }

    private void RunFrame(double deltaTime)
    {
      TestLog.Frame++;
      elapsed += deltaTime;
      world.SetTime(new TimeData(elapsed, (float)deltaTime));
      world.Update();
    }

    private static List<Type> ManagedSystemsIn<TGroup>(World w) where TGroup : ComponentSystemGroup =>
      w.GetExistingSystemManaged<TGroup>().ManagedSystems.Select(s => s.GetType()).ToList();

    // ──────────────────────── Group attribute placement ────────────────────────

    /// <summary>
    /// Entities' codegen type registry matches system attributes by exact type, so a subclass of
    /// UpdateInGroupAttribute is ignored and the system lands in SimulationSystemGroup instead (the
    /// removed [Fixed*UpdateGroup] / [ReducerUpdateGroup] attributes did exactly that). This checks
    /// where the fixtures' plain [UpdateInGroup] attributes put the systems.
    /// </summary>
    [Test]
    public void GroupAttributes_PlaceSystemsInTheirGroups()
    {
      world = new World("ECSReact Group Attribute World");
      var types = PipelineTypes.Concat(new[]
      {
        typeof(ChainingFixedMiddlewareSystem), // [UpdateInGroup(typeof(FixedMiddlewareSystemGroup))]
        typeof(FixedLaneRecorderSystem),       // [UpdateInGroup(typeof(FixedReducerSystemGroup))]
        typeof(FrameLaneRecorderSystem),       // [UpdateInGroup(typeof(ReducerSystemGroup))]
      }).ToArray();
      DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, types);

      // Report every misplaced system at once (this NUnit version has no Assert.Multiple)
      var misplaced = new List<string>();
      if (!ManagedSystemsIn<FixedMiddlewareSystemGroup>(world).Contains(typeof(ChainingFixedMiddlewareSystem)))
        misplaced.Add("ChainingFixedMiddlewareSystem is not in FixedMiddlewareSystemGroup");
      if (!ManagedSystemsIn<FixedReducerSystemGroup>(world).Contains(typeof(FixedLaneRecorderSystem)))
        misplaced.Add("FixedLaneRecorderSystem is not in FixedReducerSystemGroup");
      if (!ManagedSystemsIn<ReducerSystemGroup>(world).Contains(typeof(FrameLaneRecorderSystem)))
        misplaced.Add("FrameLaneRecorderSystem is not in ReducerSystemGroup");
      CollectionAssert.IsEmpty(misplaced, string.Join("; ", misplaced));
    }

    // ─────────────────────────── Initialisation order ───────────────────────────

    /// <summary>
    /// ECSActionDispatcherInitSystem.OnCreate creates the job command buffer from JobActionCollectorSystem.
    /// When both are created in one batch with the init system first (as FixedLaneTimingTests.SystemTypes
    /// lists them), the collector's OnCreate hasn't run yet and CreateCommandBuffer() throws. A world may
    /// create its systems in any order, so initialisation should not depend on it.
    /// </summary>
    [Test]
    public void Dispatcher_Initialises_WhenInitSystemIsCreatedBeforeItsCommandBufferSystems()
    {
      world = new World("ECSReact Init Order World");
      DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, new[]
      {
        typeof(ECSActionDispatcherInitSystem),
        typeof(JobActionCollectorSystem),
        typeof(FixedActionCommandBufferSystem),
        typeof(FixedActionSystemGroup),
        typeof(FixedStepSimulationSystemGroup),
      });

      CollectionAssert.IsEmpty(loggedErrors, "creating the dispatcher logged errors");
      Assert.DoesNotThrow(() => ECSActionDispatcher.Dispatch(new TestFixedAction(), world));
    }

    // ───────────────────────────────── Burst ─────────────────────────────────

    [BurstCompile(CompileSynchronously = true)]
    private struct ReadLanesJob : IJob
    {
      public NativeArray<bool> Lanes;
      public void Execute()
      {
        Lanes[0] = ActionLane<TestFixedAction>.IsFixed;
        Lanes[1] = ActionLane<TestChainedAction>.IsFixed;
        Lanes[2] = ActionLane<TestFrameAction>.IsFixed;
      }
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct BurstDispatchJob : IJob
    {
      public EntityCommandBuffer.ParallelWriter Ecb;
      public int Step;
      public void Execute() => Ecb.DispatchAction(0, new TestFixedAction { dispatchedStep = Step });
    }

    /// <summary>The SharedStatic lane cache must read the same from Burst code as from managed code.</summary>
    [Test]
    public void ActionLane_IsReadableFromBurstJob()
    {
      BuildWorld();

      using (var lanes = new NativeArray<bool>(3, Allocator.TempJob)) {
        new ReadLanesJob { Lanes = lanes }.Schedule().Complete();
        Assert.IsTrue(lanes[0], "TestFixedAction read as frame lane in Burst");
        Assert.IsTrue(lanes[1], "TestChainedAction read as frame lane in Burst");
        Assert.IsFalse(lanes[2], "TestFrameAction read as fixed lane in Burst");
      }
    }

    /// <summary>
    /// A Burst job dispatching through GetFixedJobCommandBuffer tags the entity FixedActionTag
    /// (decided inside Burst) and the action is processed exactly once, at the next step.
    /// </summary>
    [Test]
    public void BurstJob_FixedDispatch_IsTaggedFixedAndProcessedOnce()
    {
      BuildWorld();
      RunFrame(1.0 / 60);
      int stepAtSchedule = TestLog.Step;

      var handle = new BurstDispatchJob {
        Ecb = ECSActionDispatcher.GetFixedJobCommandBuffer(world),
        Step = stepAtSchedule
      }.Schedule();
      ECSActionDispatcher.RegisterJobHandle(handle, world);

      for (int i = 0; i < 10; i++)
        RunFrame(1.0 / 60);

      Assert.AreEqual(1, TestLog.FixedActions.Count, "processed exactly once");
      Assert.AreEqual(stepAtSchedule + 1, TestLog.FixedActions[0].processed);
      Assert.AreEqual(0, TestLog.FixedActionsSeenByFrameLane, "a Burst-dispatched fixed action reached the frame lane");
    }

    // ───────────────────────── Multi-step frames ─────────────────────────

    /// <summary>
    /// At 10 fps a frame runs 5 fixed steps. The cached fixed buffer must be replaced after every
    /// step's playback, so each step's dispatch is processed once, in its own step: no duplicate
    /// playback and nothing carried into a later step.
    /// </summary>
    [TestCase(10)]
    [TestCase(20)]
    public void LowFrameRate_EachStepsDispatch_IsProcessedOnceInItsOwnStep(int fps)
    {
      BuildWorld();
      TestLog.DispatchFixedFromFixedStep = true;

      for (int i = 0; i < fps * 2; i++)
        RunFrame(1.0 / fps);

      Assert.That(TestLog.Step, Is.GreaterThanOrEqualTo(95), "expected ~2 s of 50 Hz steps");
      Assert.AreEqual(TestLog.Step, TestLog.FixedActions.Count, "one action processed per step");
      CollectionAssert.AllItemsAreUnique(TestLog.FixedActions.Select(p => p.dispatched), "an action was played back twice");
      foreach (var p in TestLog.FixedActions)
        Assert.AreEqual(p.dispatched, p.processed, $"dispatched in step {p.dispatched}");
    }

    // ─────────────────────── Tag follows the type ───────────────────────

    /// <summary>
    /// Design §2: "the tag follows the action type, not the buffer". A fixed-lane action written through
    /// the frame-lane job writer becomes a FixedActionTag entity at the next frame's playback. The frame
    /// ActionCleanupSystem must not destroy it, and the fixed lane must process it once, even when frames
    /// run zero fixed steps in between.
    /// </summary>
    [Test]
    public void FixedAction_ThroughFrameLaneWriter_IsProcessedOnceByFixedLane_AcrossZeroStepFrames()
    {
      BuildWorld();
      RunFrame(1.0 / 60);

      // 200 fps: most frames run no fixed step
      ECSActionDispatcher.GetJobCommandBuffer(world).DispatchAction(0, new TestFixedAction { dispatchedStep = TestLog.Step });
      for (int i = 0; i < 40; i++)
        RunFrame(1.0 / 200);

      // (FixedActionsSeenByFrameLane isn't asserted here: the entity legitimately exists, tagged
      // FixedActionTag, during the frames it waits for a step. Frame-lane reducers query ActionTag.)
      Assert.AreEqual(1, TestLog.FixedActions.Count, "dropped by the frame lane, or processed twice");
    }
  }
}
