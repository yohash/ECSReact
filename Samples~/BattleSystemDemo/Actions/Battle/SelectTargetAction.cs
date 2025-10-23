using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// UI action for selecting a target
  /// </summary>
  public struct SelectTargetAction : IGameAction
  {
    public Entity targetEntity;
    public bool confirmSelection;
  }
}