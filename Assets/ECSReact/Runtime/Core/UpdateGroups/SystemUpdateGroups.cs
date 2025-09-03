using Unity.Entities;
using Unity.Physics.Systems;

namespace ECSReact.Core
{
  /// <summary>
  /// System update groups that define the execution order for the ECS-React architecture.
  /// These groups ensure proper data flow: Actions → Middleware → Reducers → Cleanup → UI Notifications
  /// </summary>

  /// <summary>
  /// Group for middleware systems that process actions before reducers.
  /// Middleware handles cross-cutting concerns like validation, logging, and async operations.
  /// Runs in InitializationSystemGroup to process actions as early as possible.
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
  [UpdateAfter(typeof(SimulationSystemGroup))]
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

  /// <summary>
  /// Specialized system group for physics-related middleware.
  /// Provides a dedicated update group for physics preprocessing that runs
  /// before the main physics simulation but after regular middleware.
  /// </summary>
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  [UpdateBefore(typeof(PhysicsSystemGroup))]
  public partial class PhysicsMiddlewareSystemGroup : ComponentSystemGroup
  {
    protected override void OnCreate()
    {
      base.OnCreate();

      // Physics middleware needs deterministic execution for multiplayer
      EnableSystemSorting = true;
    }
  }

  /// <summary>
  /// Update group attributes that can be applied to user systems for proper ordering.
  /// These provide convenient, semantic attributes for common system types.
  /// </summary>

  /// <summary>
  /// Attribute for reducer systems that process actions and update state.
  /// Ensures reducers run in SimulationSystemGroup after middleware but before cleanup.
  /// </summary>
  public class ReducerSystemAttribute : UpdateInGroupAttribute
  {
    public ReducerSystemAttribute() : base(typeof(ReducerSystemGroup)) { }
  }

  /// <summary>
  /// Attribute for general middleware systems that process actions before reducers.
  /// Ensures middleware runs early in the pipeline.
  /// </summary>
  public class MiddlewareSystemAttribute : UpdateInGroupAttribute
  {
    public MiddlewareSystemAttribute() : base(typeof(MiddlewareSystemGroup)) { }
  }

  /// <summary>
  /// Attribute for validation middleware systems.
  /// Ensures validation runs early in the middleware pipeline.
  /// </summary>
  public class ValidationMiddlewareAttribute : UpdateInGroupAttribute
  {
    public ValidationMiddlewareAttribute() : base(typeof(MiddlewareSystemGroup)) { }
  }

  /// <summary>
  /// Attribute for async middleware systems (file I/O, network, etc).
  /// These run after validation but before simulation.
  /// </summary>
  public class AsyncMiddlewareAttribute : UpdateInGroupAttribute
  {
    public AsyncMiddlewareAttribute() : base(typeof(MiddlewareSystemGroup)) { }
  }

  /// <summary>
  /// Attribute for UI state notification systems.
  /// Ensures these systems run in the presentation layer.
  /// </summary>
  public class UINotificationSystemAttribute : UpdateInGroupAttribute
  {
    public UINotificationSystemAttribute() : base(typeof(UINotificationSystemGroup)) { }
  }
}
