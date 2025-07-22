using System;
using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Party and enemy state - health, mana, status for all combatants
  /// </summary>
  public struct PartyState : IGameState, IEquatable<PartyState>
  {
    public FixedList512Bytes<CharacterData> characters; // Max 8 characters
    public int activePartySize;
    public int aliveCount;
    public int enemyCount;
    public int aliveEnemyCount;

    public bool Equals(PartyState other)
    {
      if (activePartySize != other.activePartySize) {
        return false;
      }
      if (aliveCount != other.aliveCount) {
        return false;
      }
      if (enemyCount != other.enemyCount) {
        return false;
      }
      if (aliveEnemyCount != other.aliveEnemyCount) {
        return false;
      }

      // Deep compare character data
      if (characters.Length != other.characters.Length) {
        return false;
      }

      for (int i = 0; i < characters.Length; i++) {
        if (!characters[i].Equals(other.characters[i])) {
          return false;
        }
      }

      return true;
    }
  }

  [Serializable]
  public struct CharacterData : IEquatable<CharacterData>
  {
    public Entity entity;
    public FixedString32Bytes name;
    public int currentHealth;
    public int maxHealth;
    public int currentMana;
    public int maxMana;
    public bool isEnemy;
    public bool isAlive;
    public CharacterStatus status;

    public bool Equals(CharacterData other)
    {
      return entity == other.entity &&
             name == other.name &&
             currentHealth == other.currentHealth &&
             maxHealth == other.maxHealth &&
             currentMana == other.currentMana &&
             maxMana == other.maxMana &&
             isEnemy == other.isEnemy &&
             isAlive == other.isAlive &&
             status == other.status;
    }
  }

  [Flags]
  public enum CharacterStatus
  {
    None = 0,
    Poisoned = 1 << 0,
    Stunned = 1 << 1,
    Defending = 1 << 2,
    Buffed = 1 << 3,
    Weakened = 1 << 4
  }
}