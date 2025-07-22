using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Basic attack action - deals damage from attacker to target
  /// </summary>
  public struct AttackAction : IGameAction
  {
    public Entity attackerEntity;
    public Entity targetEntity;
    public int baseDamage;
    public bool isCritical;
  }
}