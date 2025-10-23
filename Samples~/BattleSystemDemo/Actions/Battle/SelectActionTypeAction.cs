using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// UI action for selecting action type (Attack, Skill, Item, etc)
  /// </summary>
  public struct SelectActionTypeAction : IGameAction
  {
    public ActionType actionType;
    public Entity actingCharacter;
  }
}