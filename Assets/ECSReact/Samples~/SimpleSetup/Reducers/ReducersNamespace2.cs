using ECSReact.Core;
using ECSReact.Samples.SimpleSetup.Namespace1.Actions;
using ECSReact.Samples.SimpleSetup.Namespace2.Actions;
using ECSReact.Samples.SimpleSetup.Namespace2.State;
using Unity.Entities;

namespace ECSReact.Samples.SimpleSetup.Namespace2.Reducers
{
  [Reducer]
  public struct IterateReducerNamespace2 : IReducer<StateNamespace2, ActionIterateNamespace2>
  {
    public void Execute(ref StateNamespace2 state, in ActionIterateNamespace2 action, ref SystemState systemState)
    {
      if (!state.IsStarted)
        return;
      state.Count += 1;
    }
  }

  [Reducer]
  public struct StartReducerNamespace2 : IReducer<StateNamespace2, ActionStartNamespace2>
  {
    public void Execute(ref StateNamespace2 state, in ActionStartNamespace2 action, ref SystemState systemState)
    {
      state.IsStarted = true;
    }
  }

  [Reducer]
  public struct ResetReducerNamespace2 : IReducer<StateNamespace2, ActionResetNamespace1>
  {
    public void Execute(ref StateNamespace2 state, in ActionResetNamespace1 action, ref SystemState systemState)
    {
      state.Count = 0;
      state.IsStarted = false;
    }
  }
}