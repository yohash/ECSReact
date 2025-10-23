using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  public struct EndBattleAction : IGameAction
  {
    public bool PlayerWon;

    public EndBattleAction(bool playerWon)
    {
      PlayerWon = playerWon;
    }
  }
}