using ECSReact.Core;
using ECSReact.Samples.SimpleSetup.Namespace1.Actions;
using ECSReact.Samples.SimpleSetup.Namespace1.State;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Samples.SimpleSetup.Namespace1.Middleware
{
  [Middleware(DisableBurst = true)]
  public struct MiddlewareNamespace1 : IMiddleware<ActionResetNamespace1>
  {
    public bool Process(ref ActionResetNamespace1 action, ref SystemState systemState, EntityCommandBuffer.ParallelWriter ecb, int sortKey)
    {
      if (!systemState.TryGetSingleton<StateNamespace1>(out var state)) {
        Debug.LogError("StateNamespace1 singleton not found!");
        return false;
      }

      if (state.WasReset) {
        // Perform logging. Alternately, we can return false to block the action.
        Debug.Log("MiddlewareNamespace1: State was already reset.");
      } else {
        Debug.Log("MiddlewareNamespace1: ActionResetNamespace1 proceeding.");
      }

      return true;
    }
  }
}