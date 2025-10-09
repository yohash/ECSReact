using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// This middleware processes NextTurnAction events 
  /// </summary>
  [Middleware]
  public struct NextTurnMiddleware : IMiddleware<ReadyForNextTurn>
  {
    public bool Process(
      ref ReadyForNextTurn action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter dispatcher,
      int sortKey
    )
    {
      if (!systemState.TryGetSingleton<CharacterIdentityState>(out var identityState)) {
        return true;
      }

      if (!systemState.TryGetSingleton<CharacterHealthState>(out var healthState)) {
        return true;
      }

      if (!systemState.TryGetSingleton<BattleState>(out var battleState)) {
        return true;
      }

      // iterater over the characters in the turn order established by battle state
      // skip any who are not alive, and find the next character who is alive
      var nextEntity = Entity.Null;
      var nextIndex = battleState.activeCharacterIndex;

      // Prevent infinite loop if no characters are alive
      int max = (healthState.health.IsCreated ? healthState.health.Count : 0) + 1;
      int iter = 0;
      while (nextEntity == Entity.Null && iter <= max) {
        // iterate the next index from the prior next index
        nextIndex = (nextIndex + 1) % battleState.turnOrder.Length;
        var potential = battleState.turnOrder[nextIndex];
        // Check if this potential character is alive, potentially ending the loop
        if (healthState.health.IsCreated &&
            healthState.health.TryGetValue(potential, out var hp) &&
            hp.isAlive) {
          nextEntity = potential;
        }
        iter++;
      }

      // No alive characters found, end battle
      if (nextEntity == Entity.Null) {
        dispatcher.DispatchAction(sortKey, new EndBattleAction());
        return false; // Prevent NextTurnAction from reaching reducers
      }

      // complete action enrichment and dispatch NextTurnAction
      bool isPlayer = false;
      if (identityState.isEnemy.IsCreated &&
          identityState.isEnemy.TryGetValue(nextEntity, out var isEnemy)) {
        isPlayer = !isEnemy;
      }

      dispatcher.DispatchAction(sortKey + 1,
        new NextTurnAction
        {
          nextCharacterIndex = nextIndex,
          isPlayerTurn = isPlayer,
        });

      return true;
    }
  }


  /// <summary>
  /// This middleware processes NextTurnAction events 
  /// </summary>
  [Middleware(DisableBurst = true)]
  public struct EndBattleMiddleware : IMiddleware<EndBattleAction>
  {
    public bool Process(ref EndBattleAction action, ref SystemState systemState, EntityCommandBuffer.ParallelWriter dispatcher, int sortKey)
    {
      Debug.LogWarning("*** BATTLE ENDED ***");
      return true;
    }
  }
}
