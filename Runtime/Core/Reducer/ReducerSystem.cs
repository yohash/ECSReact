using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Abstract base class for reducer systems that process actions and update state.
  /// 
  /// IMPORTANT: This base system is automatically DISABLED on creation. A generated bridge system
  /// handles the actual execution using optimal SystemAPI.Query patterns. This avoids Unity ECS's 
  /// generic type limitations while maintaining zero allocations in the hot path.
  /// 
  /// Use this for general game logic that doesn't require maximum performance.
  /// For performance-critical reducers, use BurstReducerSystem instead.
  /// </summary>
  [ReducerSystem]
  public abstract partial class ReducerSystem<TState, TAction> : SystemBase
      where TState : unmanaged, IGameState
      where TAction : unmanaged, IGameAction
  {
    protected override void OnCreate()
    {
      base.OnCreate();

      // CRITICAL: Disable this system - the generated bridge will handle execution
      Enabled = false;
    }

    protected override void OnUpdate()
    {
      // This should never run in production - the bridge system handles execution
      throw new System.InvalidOperationException(
        $"ReducerSystem {GetType().Name} should never run directly. " +
        $"Ensure code generation has created the bridge system."
      );
    }

    /// <summary>
    /// Override this method to implement your state reduction logic.
    /// This is called once for each action of the specified type.
    /// </summary>
    public abstract void ReduceState(ref TState state, TAction action);
  }
}