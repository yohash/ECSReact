using Unity.Entities;
using Unity.Burst;

namespace ECSReact.Core
{
  /// <summary>
  /// Interface for Burst-compatible reducer logic.
  /// Implement this in a struct for zero-allocation, Burst-compiled state reduction.
  /// </summary>
  public interface IBurstReducer<TState, TAction>
      where TState : unmanaged
      where TAction : unmanaged
  {
    /// <summary>
    /// Execute the reduction logic. This method will be Burst-compiled.
    /// Use 'in' for the action parameter to avoid copies.
    /// </summary>
    void Execute(ref TState state, in TAction action);
  }

  /// <summary>
  /// Burst-optimized reducer using struct-based logic.
  /// The struct pattern allows full Burst compilation without any boilerplate.
  /// 
  /// Usage:
  /// 1. Create a struct implementing IBurstReducer
  /// 2. Inherit from BurstReducerSystem with your struct as TLogic
  /// 
  /// The generated bridge will use your struct's Execute method with Burst optimization.
  /// </summary>
  [ReducerUpdateGroup]
  [BurstCompile]
  public abstract partial class BurstReducerSystem<TState, TAction, TLogic> : SystemBase
      where TState : unmanaged, IGameState
      where TAction : unmanaged, IGameAction
      where TLogic : struct, IBurstReducer<TState, TAction>
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
        $"BurstReducerSystem {GetType().Name} should never run directly. " +
        $"Ensure code generation has created the bridge system."
      );
    }

    // No abstract methods: the logic is in the struct.
  }
}