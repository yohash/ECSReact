using System.Runtime.CompilerServices;
using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Writer passed to middleware for dispatching actions. Holds a command buffer writer for
  /// each lane and routes every dispatch by the dispatched action's type:
  /// - frame-lane actions → processed next frame;
  /// - [FixedStepAction] actions → processed in the next fixed step.
  ///
  /// A fixed-lane middleware can therefore emit frame-lane actions (VFX, audio, UI) and a
  /// frame-lane middleware can emit fixed-lane actions, each landing in the right lane.
  ///
  /// The writer advances its own sort key on every dispatch, so dispatching several actions
  /// for one input action never reuses a key. Burst-compatible.
  ///
  /// Obtain one per system update with <see cref="ECSActionDispatcher.GetActionWriter"/>
  /// (generated middleware systems do this for you) and pass it by ref.
  /// </summary>
  public struct ActionWriter
  {
    private EntityCommandBuffer.ParallelWriter frameLane;
    private EntityCommandBuffer.ParallelWriter fixedLane;
    private bool isFixedLane;
    private int nextSortKey;

    internal ActionWriter(
      EntityCommandBuffer.ParallelWriter frameLane,
      EntityCommandBuffer.ParallelWriter fixedLane,
      bool isFixedLane)
    {
      this.frameLane = frameLane;
      this.fixedLane = fixedLane;
      this.isFixedLane = isFixedLane;
      nextSortKey = 0;
    }

    /// <summary>True when the middleware holding this writer runs in the fixed-step lane.</summary>
    public bool IsFixedLane => isFixedLane;

    /// <summary>
    /// Dispatch an action into its own lane (decided by [FixedStepAction] on T).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispatch<T>(T action) where T : unmanaged, IGameAction
    {
      int sortKey = nextSortKey++;
      if (ActionLane<T>.IsFixed)
        fixedLane.DispatchAction(sortKey, action);
      else
        frameLane.DispatchAction(sortKey, action);
    }

    /// <summary>
    /// Raw command buffer writer for this middleware's own lane, for structural work that
    /// isn't an action (e.g. creating entities). Take a key from <see cref="NextSortKey"/>
    /// and use it for the related commands (e.g. CreateEntity and its AddComponent calls).
    /// Use <see cref="Dispatch{T}"/> for actions so they reach the right lane.
    /// </summary>
    public EntityCommandBuffer.ParallelWriter Ecb => isFixedLane ? fixedLane : frameLane;

    /// <summary>Returns a sort key not yet used by this writer, for use with <see cref="Ecb"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextSortKey() => nextSortKey++;
  }
}
