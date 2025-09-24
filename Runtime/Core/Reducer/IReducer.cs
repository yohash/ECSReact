using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Interface for reducer logic with full SystemAPI access.
  /// Use this when you need time, queries, singletons, or other ECS resources.
  /// Processes actions sequentially on the main thread.
  /// 
  /// All reducers are Burst-compiled by default for maximum performance.
  /// Use the [Reducer(DisableBurst = true)] attribute if your logic requires
  /// managed code, Unity API calls, or other non-Burstable operations.
  /// 
  /// Example:
  /// <code>
  /// [Reducer]
  /// public struct PhysicsReducer : IReducer&lt;PhysicsState, ForceAction&gt;
  /// {
  ///     public void Execute(ref PhysicsState state, in ForceAction action, ref SystemState systemState)
  ///     {
  ///         var deltaTime = SystemAPI.Time.DeltaTime;
  ///         state.velocity += action.force / state.mass * deltaTime;
  ///     }
  /// }
  /// </code>
  /// </summary>
  /// <typeparam name="TState">The state type this reducer modifies (must implement IGameState)</typeparam>
  /// <typeparam name="TAction">The action type this reducer processes (must implement IGameAction)</typeparam>
  public interface IReducer<TState, TAction>
      where TState : unmanaged, IGameState
      where TAction : unmanaged, IGameAction
  {
    /// <summary>
    /// Executes the reduction logic with full SystemAPI access.
    /// This method is called once for each action of the specified type.
    /// 
    /// Use 'ref' for the state parameter to modify it directly.
    /// Use 'in' for the action parameter to avoid unnecessary copies.
    /// Use 'ref' for the systemState to access SystemAPI features.
    /// </summary>
    /// <param name="state">The current state to be modified</param>
    /// <param name="action">The action containing the data for this state change</param>
    /// <param name="systemState">The system state providing access to SystemAPI</param>
    void Execute(ref TState state, in TAction action, ref SystemState systemState);
  }
}