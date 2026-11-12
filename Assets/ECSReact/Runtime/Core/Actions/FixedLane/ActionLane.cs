using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Burst-compatible, per-type cache of which lane an action type belongs to.
  ///
  /// The value lives in a SharedStatic, so it can be read from Burst-compiled code and jobs
  /// (reflection can't run there). It is filled once from managed code by
  /// <see cref="ActionLaneRegistry.EnsureInitialized"/>, which ECSActionDispatcher calls
  /// when a world's dispatcher is created. Until then every type reads as frame lane.
  /// </summary>
  public static class ActionLane<T> where T : struct
  {
    private static readonly SharedStatic<bool> isFixed =
      SharedStatic<bool>.GetOrCreate<ActionLaneContext, T>();

    /// <summary>True when T is marked with [FixedStepAction].</summary>
    public static bool IsFixed
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => isFixed.Data;
    }

    // Called by ActionLaneRegistry via reflection, from managed code only.
    internal static void Set(bool value) => isFixed.Data = value;
  }

  // Context key for the ActionLane<T> SharedStatic.
  internal struct ActionLaneContext { }

  /// <summary>
  /// Fills <see cref="ActionLane{T}"/> for every IGameAction type. Managed-only, runs once per
  /// domain load. Every action type is written explicitly (true or false), so a stale value
  /// can't survive in SharedStatic memory after the attribute is removed.
  /// </summary>
  public static class ActionLaneRegistry
  {
    private static bool initialized;
    private static readonly object initLock = new object();

    public static void EnsureInitialized()
    {
      if (initialized)
        return;

      lock (initLock) {
        if (initialized)
          return;

        TypeManager.Initialize();
        var setterDefinition = typeof(ActionLane<>);

        foreach (var typeInfo in TypeManager.AllTypes) {
          var type = typeInfo.Type;
          if (type == null || !type.IsValueType || !typeof(IGameAction).IsAssignableFrom(type))
            continue;

          bool isFixed = type.GetCustomAttribute<FixedStepActionAttribute>(false) != null;
          try {
            var setter = setterDefinition.MakeGenericType(type)
              .GetMethod(nameof(ActionLane<int>.Set), BindingFlags.Static | BindingFlags.NonPublic);
            setter.Invoke(null, new object[] { isFixed });
          } catch (Exception ex) {
            Debug.LogWarning($"[ECSReact] Could not register action lane for {type.FullName}: {ex.Message}");
          }
        }

        initialized = true;
      }
    }
  }
}
