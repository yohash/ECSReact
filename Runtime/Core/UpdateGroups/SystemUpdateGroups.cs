using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// System update groups that define the execution order for the ECS-React architecture.
  /// These groups ensure proper data flow: Actions → Middleware → Reducers → Cleanup → UI Notifications
  /// </summary>

  /// <summary>
  /// Group for middleware systems that process actions before reducers.
  /// Middleware handles cross-cutting concerns like validation, logging, and async operations.
  /// Runs in SimulationSystemGroup, once per frame, before ReducerSystemGroup.
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateBefore(typeof(ReducerSystemGroup))]
  public partial class MiddlewareSystemGroup : ComponentSystemGroup
  {
    protected override void OnCreate()
    {
      base.OnCreate();

      // Enable sorting to ensure deterministic middleware execution order
      EnableSystemSorting = true;
    }
  }

  /// <summary>
  /// Group for reducer systems that process actions and update game state.
  /// Reducers run after middleware to ensure actions are validated and ready for state changes.
  /// Reducers should ONLY mutate state and not perform side effects.
  /// </summary>
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateBefore(typeof(ActionCleanupSystemGroup))]
  public partial class ReducerSystemGroup : ComponentSystemGroup
  {
    protected override void OnCreate()
    {
      base.OnCreate();

      // Reducers should run after middleware but before cleanup
      EnableSystemSorting = true;
    }
  }

  /// <summary>
  /// Group for systems that clean up processed actions.
  /// Runs after simulation systems to ensure actions can be processed by multiple reducers.
  /// This is where ActionCleanupSystem runs to destroy all ActionTag entities.
  /// </summary>
  [UpdateAfter(typeof(ReducerSystemGroup))]
  public partial class ActionCleanupSystemGroup : ComponentSystemGroup
  {
    protected override void OnCreate()
    {
      base.OnCreate();

      // Action cleanup should be fast and deterministic
      EnableSystemSorting = true;
    }
  }

  /// <summary>
  /// Group for systems that detect state changes and generate UI events.
  /// Runs in PresentationSystemGroup to handle UI concerns separately from simulation.
  /// This ensures UI processing doesn't affect deterministic simulation timing.
  /// </summary>
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  public partial class UINotificationSystemGroup : ComponentSystemGroup
  {
    protected override void OnCreate()
    {
      base.OnCreate();

      // UI notifications should be processed in consistent order
      EnableSystemSorting = true;
    }
  }

  // ───────────────────────────── Fixed-step action lane ─────────────────────────────

  /// <summary>
  /// The fixed action phase: processes actions marked [FixedStepAction] at the end of
  /// every fixed step. OrderLast, plus UpdateAfter EndFixedStepSimulationEntityCommandBufferSystem
  /// (also OrderLast), places it after every other system in the step, including physics and
  /// that ECB's playback. So "processed this step" means after this step's physics, and fixed
  /// reducers see the structural changes gameplay systems queued into the End-fixed-step ECB
  /// in the same step. Commands fixed middleware or reducers queue into that ECB play back in
  /// the next step; use ActionWriter.Ecb or dispatch actions instead.
  ///
  /// Order inside the phase:
  ///   FixedActionCommandBufferSystem  → plays back fixed-lane dispatches (OrderFirst)
  ///   FixedMiddlewareSystemGroup
  ///   FixedReducerSystemGroup
  ///   FixedActionCleanupSystem        → destroys FixedActionTag entities (OrderLast)
  ///
  /// Fixed-lane action entities therefore only exist inside this phase. On frames that
  /// run zero fixed steps, pending dispatches simply wait in the command buffer.
  /// </summary>
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
  [UpdateAfter(typeof(EndFixedStepSimulationEntityCommandBufferSystem))]
  public partial class FixedActionSystemGroup : ComponentSystemGroup
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      EnableSystemSorting = true;
    }
  }

  /// <summary>
  /// Group for middleware systems whose action type is marked [FixedStepAction].
  /// Runs inside FixedActionSystemGroup, once per fixed step, before FixedReducerSystemGroup.
  /// </summary>
  [UpdateInGroup(typeof(FixedActionSystemGroup))]
  [UpdateAfter(typeof(FixedActionCommandBufferSystem))]
  [UpdateBefore(typeof(FixedReducerSystemGroup))]
  public partial class FixedMiddlewareSystemGroup : ComponentSystemGroup
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      EnableSystemSorting = true;
    }
  }

  /// <summary>
  /// Group for reducer systems whose action type is marked [FixedStepAction].
  /// Runs inside FixedActionSystemGroup, once per fixed step, after FixedMiddlewareSystemGroup.
  /// </summary>
  [UpdateInGroup(typeof(FixedActionSystemGroup))]
  [UpdateAfter(typeof(FixedActionCommandBufferSystem))]
  public partial class FixedReducerSystemGroup : ComponentSystemGroup
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      EnableSystemSorting = true;
    }
  }

  // Note: place systems in these groups with the plain attribute, e.g.
  // [UpdateInGroup(typeof(ReducerSystemGroup))]. Entities matches UpdateInGroupAttribute by
  // exact type and silently ignores subclasses, so convenience attributes deriving from it
  // don't work (#16).
}
