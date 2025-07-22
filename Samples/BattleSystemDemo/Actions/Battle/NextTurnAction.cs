using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Advances to the next character's turn
  /// </summary>
  public struct NextTurnAction : IGameAction
  {
    public bool skipAnimation;
  }
}