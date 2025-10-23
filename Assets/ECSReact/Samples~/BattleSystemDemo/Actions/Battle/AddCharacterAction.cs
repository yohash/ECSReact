using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Action to add a new character to the party state
  /// </summary>
  public struct AddCharacterAction : IGameAction
  {
    public FixedString32Bytes name;
    public int maxHealth;
    public int maxMana;
    public bool isEnemy;
    public CharacterStatus initialStatus;
  }
}
