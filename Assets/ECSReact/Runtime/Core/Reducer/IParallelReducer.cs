using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Interface for high-performance parallel reducer logic.
  /// Use this for pure data transformations that need maximum throughput.
  /// 
  /// PrepareData runs once per frame on the main thread with full SystemAPI access.
  /// Execute runs many times in parallel jobs with the prepared data.
  /// 
  /// This pattern allows you to fetch time, singletons, and lookups from SystemAPI,
  /// then use them efficiently in parallel processing.
  /// 
  /// Example:
  /// <code>
  /// [Reducer]
  /// public struct PhysicsReducer : IParallelReducer&lt;PhysicsState, ForceAction, PhysicsReducer.FrameData&gt;
  /// {
  ///     public struct FrameData
  ///     {
  ///         public float deltaTime;
  ///         public float3 gravity;
  ///     }
  ///     
  ///     public FrameData PrepareData(ref SystemState systemState)
  ///     {
  ///         var config = SystemAPI.GetSingleton&lt;PhysicsConfig&gt;();
  ///         return new FrameData
  ///         {
  ///             deltaTime = SystemAPI.Time.DeltaTime,
  ///             gravity = config.gravity
  ///         };
  ///     }
  ///     
  ///     public void Execute(ref PhysicsState state, in ForceAction action, in FrameData data)
  ///     {
  ///         state.velocity += (action.force + data.gravity) * data.deltaTime;
  ///         state.position += state.velocity * data.deltaTime;
  ///     }
  /// }
  /// </code>
  /// </summary>
  /// <typeparam name="TState">The state type this reducer modifies (must implement IGameState)</typeparam>
  /// <typeparam name="TAction">The action type this reducer processes (must implement IGameAction)</typeparam>
  /// <typeparam name="TData">The data type prepared from SystemAPI (must be unmanaged)</typeparam>
  public interface IParallelReducer<TState, TAction, TData>
      where TState : unmanaged, IGameState
      where TAction : unmanaged, IGameAction
      where TData : unmanaged
  {
    /// <summary>
    /// Prepares data from SystemAPI on the main thread.
    /// This runs ONCE per frame before parallel processing.
    /// 
    /// Use this to fetch:
    /// - Time data (deltaTime, elapsedTime)
    /// - Singletons (configuration, game settings)
    /// - Component/Buffer lookups (for accessing other entities)
    /// - Any other SystemAPI data needed for processing
    /// </summary>
    /// <param name="systemState">The system state providing full SystemAPI access</param>
    /// <returns>Data structure to be used in parallel execution</returns>
    TData PrepareData(ref SystemState systemState);

    /// <summary>
    /// Executes the reduction logic in parallel.
    /// This runs MANY times in parallel jobs for each action.
    /// 
    /// Cannot access SystemAPI directly - use the prepared data instead.
    /// 
    /// Use 'ref' for the state parameter to modify it directly.
    /// Use 'in' for the action and data parameters to avoid unnecessary copies.
    /// </summary>
    /// <param name="state">The current state to be modified</param>
    /// <param name="action">The action containing the data for this state change</param>
    /// <param name="data">The data prepared from SystemAPI</param>
    void Execute(ref TState state, in TAction action, in TData data);
  }
}