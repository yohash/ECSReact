using Unity.Entities;
using Unity.Burst;

namespace ECSReact.Core
{
  /// <summary>
  /// Interface for Burst-compatible middleware logic.
  /// Implement this in a struct for zero-allocation, Burst-compiled middleware.
  /// </summary>
  public interface IBurstMiddleware<TAction>
      where TAction : unmanaged
  {
    /// <summary>
    /// Execute the middleware logic. This method will be Burst-compiled.
    /// Use 'in' for the action parameter to avoid copies.
    /// Note: Cannot dispatch new actions from Burst middleware (use standard middleware for that).
    /// </summary>
    void Execute(in TAction action, Entity actionEntity);
  }

  /// <summary>
  /// Burst-optimized middleware using struct-based logic.
  /// Use this for high-performance middleware like input validation or metrics.
  /// 
  /// LIMITATION: Burst middleware cannot dispatch new actions or use managed code.
  /// For middleware that needs to dispatch actions or use Unity APIs, use standard MiddlewareSystem.
  /// 
  /// Usage:
  /// 1. Create a struct implementing IBurstMiddleware
  /// 2. Inherit from BurstMiddlewareSystem with your struct as TLogic
  /// 3. That's it! No methods to override, no boilerplate.
  /// </summary>
  [MiddlewareSystem]
  [BurstCompile]
  public abstract partial class BurstMiddlewareSystem<TAction, TLogic> : SystemBase
      where TAction : unmanaged, IGameAction
      where TLogic : struct, IBurstMiddleware<TAction>
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

    // No methods to override: the logic is in the struct.
    // Note: No DispatchAction helper - Burst middleware can't create entities.
  }
}