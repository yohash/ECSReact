using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // INTERNAL ACTION - Dispatched by middleware after entity creation
  // ============================================================================

  /// <summary>
  /// Internal action dispatched after character entity is created.
  /// Contains all data needed by normalized state reducers.
  /// </summary>
  public struct CharacterCreatedAction : IGameAction
  {
    public Entity entity;                    // The newly created entity
    public FixedString32Bytes name;
    public int maxHealth;
    public int maxMana;
    public bool isEnemy;
    public CharacterStatus initialStatus;
  }
}