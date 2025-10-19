using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Interface for high-performance parallel middleware logic.
  /// Use this for validation and transformation that needs maximum throughput.
  /// 
  /// PrepareData runs once per frame on the main thread with full SystemAPI access.
  /// Process runs many times in parallel jobs with the prepared data.
  /// 
  /// Note: Parallel middleware can only transform actions, not filter them.
  /// If you need to filter actions, use IMiddleware instead.
  /// 
  /// Example:
  /// <code>
  /// [Middleware]
  /// public struct DamageValidationMiddleware : IParallelMiddleware&lt;DamageAction, DamageValidationMiddleware.ValidationData&gt;
  /// {
  ///     public struct ValidationData
  ///     {
  ///         public int maxDamage;
  ///         public float damageMultiplier;
  ///     }
  ///     
  ///     public ValidationData PrepareData(ref SystemState systemState)
  ///     {
  ///         var config = SystemAPI.GetSingleton&lt;GameConfig&gt;();
  ///         return new ValidationData
  ///         {
  ///             maxDamage = config.maxDamage,
  ///             damageMultiplier = config.damageMultiplier
  ///         };
  ///     }
  ///     
  ///     public void Process(ref DamageAction action, in ValidationData data)
  ///     {
  ///         // Transform only - cannot filter
  ///         action.damage = math.clamp(action.damage, 0, data.maxDamage);
  ///         action.damage = (int)(action.damage * data.damageMultiplier);
  ///     }
  /// }
  /// </code>
  /// </summary>
  /// <typeparam name="TAction">The action type this middleware processes (must implement IGameAction)</typeparam>
  /// <typeparam name="TData">The data type prepared from SystemAPI (must be unmanaged)</typeparam>
  public interface IParallelMiddleware<TAction, TData>
      where TAction : unmanaged, IGameAction
      where TData : unmanaged
  {
    /// <summary>
    /// Prepares data from SystemAPI on the main thread.
    /// This runs ONCE per frame before parallel processing.
    /// 
    /// Use this to fetch:
    /// - Validation rules and limits
    /// - Configuration settings
    /// - Component/Buffer lookups for validation
    /// - Any other SystemAPI data needed for processing
    /// </summary>
    /// <param name="systemState">The system state providing full SystemAPI access</param>
    /// <returns>Data structure to be used in parallel execution</returns>
    TData PrepareData(ref SystemState systemState);

    /// <summary>
    /// Processes an action in parallel.
    /// This runs MANY times in parallel jobs for each action.
    /// 
    /// Can only transform the action, not filter it.
    /// Parallel middleware cannot prevent actions from reaching reducers.
    /// 
    /// Use 'ref' for the action parameter to modify it.
    /// Use 'in' for the data parameter to avoid unnecessary copies.
    /// </summary>
    /// <param name="action">The action to transform</param>
    /// <param name="data">The data prepared from SystemAPI</param>
    void Process(ref TAction action, in TData data);
  }
}