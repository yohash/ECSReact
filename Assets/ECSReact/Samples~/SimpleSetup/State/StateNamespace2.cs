using ECSReact.Core;
using System;

namespace ECSReact.Samples.SimpleSetup.Namespace2.State
{
  public struct StateNamespace2 : IGameState, IEquatable<StateNamespace2>
  {
    public int Count;
    public bool IsStarted;

    public bool Equals(StateNamespace2 other)
    {
      return Count == other.Count && IsStarted == other.IsStarted;
    }
  }
}