using System;

namespace ECSReact.Core
{
  /// <summary>
  /// Marks a struct as a reducer for automatic ISystem generation.
  /// The code generator will create all necessary boilerplate to integrate
  /// this reducer with Unity ECS.
  /// 
  /// By default, all reducers are Burst-compiled for maximum performance.
  /// Set DisableBurst = true if your reducer needs to use managed code,
  /// Unity API calls, or other non-Burstable operations.
  /// </summary>
  [AttributeUsage(AttributeTargets.Struct)]
  public class ReducerAttribute : Attribute
  {
    /// <summary>
    /// When true, disables Burst compilation for this reducer.
    /// Use this for reducers that need to:
    /// - Call Unity API methods
    /// - Use managed objects or strings
    /// - Perform file I/O
    /// - Make network calls
    /// Default: false (Burst is enabled)
    /// </summary>
    public bool DisableBurst { get; set; } = false;

    /// <summary>
    /// Execution order within the reducer group.
    /// Lower values execute first.
    /// Default: 0
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Optional custom name for the generated system.
    /// If null, uses the reducer struct name + "_System".
    /// </summary>
    public string SystemName { get; set; } = null;
  }
}