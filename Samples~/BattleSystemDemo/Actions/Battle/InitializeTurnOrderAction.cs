using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Advances to the next character's turn
  /// </summary>
  public struct InitializeTurnOrderAction : IGameAction
  {
    public FixedList128Bytes<Entity> turnOrder; // Max 16 combatants
  }
}