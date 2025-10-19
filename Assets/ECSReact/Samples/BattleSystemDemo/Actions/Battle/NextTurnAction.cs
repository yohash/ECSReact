using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Advances to the next character's turn
  /// </summary>
  public struct NextTurnAction : IGameAction
  {
    public int nextCharacterIndex;
    public bool isPlayerTurn;
  }

  /// <summary>
  /// Dispatch this when we're ready to advance our turn
  /// </summary>
  public struct ReadyForNextTurn : IGameAction { }
}