using System;

namespace ECSReact.Core
{
  /// <summary>
  /// Marks an action type as belonging to the fixed-step lane.
  ///
  /// Fixed-lane actions are processed at the end of every fixed step, inside
  /// <see cref="FixedActionSystemGroup"/> (OrderLast in FixedStepSimulationSystemGroup),
  /// instead of once per rendered frame. Use this for gameplay actions that must resolve
  /// a fixed number of steps after they're dispatched, independent of render rate.
  ///
  /// Timing (counted in steps, never frames):
  /// - Dispatched from a fixed-step system in step N → processed in step N.
  /// - Dispatched by fixed-lane middleware in step N (a chain) → processed in step N+1.
  /// - Dispatched from frame-rate code (UI, input, frame-lane middleware) → processed
  ///   in the next fixed step. Nothing is dropped across frames that run zero steps.
  ///
  /// Each action type lives in exactly one lane. If you need both, split it into two types.
  /// The code generator reads this attribute to emit middleware and reducers for the type
  /// into the fixed-lane groups. Regenerate after adding or removing it.
  /// </summary>
  /// <example>
  /// <code>
  /// [FixedStepAction]
  /// public struct TakeDamageAction : IGameAction { public Entity target; public int amount; }
  /// </code>
  /// </example>
  [AttributeUsage(AttributeTargets.Struct, Inherited = false)]
  public sealed class FixedStepActionAttribute : Attribute
  {
  }
}
