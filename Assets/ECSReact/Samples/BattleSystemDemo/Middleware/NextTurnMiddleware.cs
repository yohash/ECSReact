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

      if (!systemState.TryGetSingleton<BattleState>(out var battleState)) {
        return true;
      }

      // compute variables and dispatch next turn action
      var nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      var nextEntity = battleState.turnOrder[nextIndex];

      bool isPlayer = false;
      if (identityState.isEnemy.IsCreated &&
          identityState.isEnemy.TryGetValue(nextEntity, out var isEnemy)) {
        isPlayer = !isEnemy;
      }

      dispatcher.DispatchAction(sortKey + 1,
        new NextTurnAction
        {
          skipAnimation = false,
          isPlayerTurn = isPlayer
        });

      return true;
    }
  }
}
