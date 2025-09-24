using System;

namespace ECSReact.Core
{
  /// <summary>
  /// Marks a struct as middleware for automatic ISystem generation.
  /// The code generator will create all necessary boilerplate to integrate
  /// this middleware with Unity ECS.
  /// 
  /// Middleware executes before reducers, allowing for action validation,
  /// transformation, and side effect generation.
  /// 
  /// By default, all middleware are Burst-compiled for maximum performance.
  /// Set DisableBurst = true if your middleware needs to use managed code,
  /// Unity API calls, or other non-Burstable operations.
  /// </summary>
  [AttributeUsage(AttributeTargets.Struct)]
  public class MiddlewareAttribute : Attribute
  {
    /// <summary>
    /// When true, disables Burst compilation for this middleware.
    /// Use this for middleware that need to:
    /// - Call Unity API methods
    /// - Use managed objects or strings
    /// - Perform file I/O
    /// - Make network calls
    /// Default: false (Burst is enabled)
    /// </summary>
    public bool DisableBurst { get; set; } = false;

    /// <summary>
    /// Execution order within the middleware group.
    /// Lower values execute first. Middleware always runs before reducers.
    /// Default: 0
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Optional custom name for the generated system.
    /// If null, uses the middleware struct name + "_System".
    /// </summary>
    public string SystemName { get; set; } = null;
  }
}