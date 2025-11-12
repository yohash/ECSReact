using ECSReact.Core;
using ECSReact.Samples.SimpleSetup.Namespace1.Actions;
using ECSReact.Samples.SimpleSetup.Namespace1.State;
using Unity.Entities;

namespace ECSReact.Samples.SimpleSetup.Namespace1.Reducers
{
  [Reducer]
  public struct IntReducerNamespace1 : IReducer<StateNamespace1, ActionIntNamespace1>
  {
    public void Execute(ref StateNamespace1 state, in ActionIntNamespace1 action, ref SystemState systemState)
    {
      state.Value = action.Amount;
      state.WasReset = false;
    }
  }

  [Reducer]
  public struct ResetReducerNamespace1 : IReducer<StateNamespace1, ActionResetNamespace1>
  {
    public void Execute(ref StateNamespace1 state, in ActionResetNamespace1 action, ref SystemState systemState)
    {
      state.Value = 0;
      state.WasReset = true;
    }
  }
}