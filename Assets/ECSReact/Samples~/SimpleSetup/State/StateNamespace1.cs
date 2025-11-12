using ECSReact.Core;
using System;

namespace ECSReact.Samples.SimpleSetup.Namespace1.State
{
  public struct StateNamespace1 : IGameState, IEquatable<StateNamespace1>
  {
    public int Value;
    public bool WasReset;
    public bool Equals(StateNamespace1 other)
    {
      return Value == other.Value && WasReset == other.WasReset;
    }
  }
}