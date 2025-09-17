using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Abstract base class for middleware systems that process actions before reducers.
  /// Middleware systems can handle cross-cutting concerns like validation, logging,
  /// analytics, and async operations without consuming the actions.
  /// 
  /// IMPORTANT: Like ReducerSystem, this base system is automatically DISABLED on creation.
  /// A generated bridge system handles the actual execution using optimal SystemAPI.Query patterns.
  /// 
  /// Use this for middleware that needs managed code access (file I/O, Unity APIs, etc).
  /// For performance-critical middleware, use BurstMiddlewareSystem instead.
  /// </summary>
  [MiddlewareSystem]
  public abstract partial class MiddlewareSystem<T> : SystemBase
      where T : unmanaged, IGameAction
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      // CRITICAL: Disable this system - the generated bridge will handle execution
      Enabled = false;
    }

    protected override void OnUpdate()
    {
      // Should never run - bridge handles execution
      throw new System.InvalidOperationException(
        $"BurstMiddlewareSystem {GetType().Name} should never run directly. " +
        $"Ensure code generation has created the bridge system."
      );
    }

    /// <summary>
    /// Override this method to implement your middleware logic.
    /// This is called for each action of the specified type.
    /// Do NOT destroy the action entity - that's handled by ActionCleanupSystem.
    /// </summary>
    public abstract void ProcessAction(T action, Entity actionEntity);

    /// <summary>
    /// Helper method to dispatch additional actions from middleware.
    /// Useful for triggering side effects or validation failures.
    /// </summary>
    protected void DispatchAction<TNewAction>(TNewAction newAction)
        where TNewAction : unmanaged, IGameAction
    {
      ECSActionDispatcher.Dispatch(newAction);
    }
  }
}