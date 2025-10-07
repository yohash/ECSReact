using ECSReact.Core;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // CHARACTER HEALTH STATE
  // ============================================================================

  /// <summary>
  /// Normalized health data for all characters.
  /// Provides O(1) lookup by Entity.
  /// </summary>
  public struct CharacterHealthState : IGameState, IEquatable<CharacterHealthState>
  {
    public NativeHashMap<Entity, HealthData> health;

    // Cached aggregates for quick checks (updated by reducers)
    public int totalAliveCount;
    public int alivePlayerCount;
    public int aliveEnemyCount;

    public bool Equals(CharacterHealthState other)
    {
      if (totalAliveCount != other.totalAliveCount)
        return false;
      if (alivePlayerCount != other.alivePlayerCount)
        return false;
      if (aliveEnemyCount != other.aliveEnemyCount)
        return false;

      // Deep compare hash map
      if (!health.IsCreated && !other.health.IsCreated)
        return true;
      if (health.IsCreated != other.health.IsCreated)
        return false;
      if (health.Count() != other.health.Count())
        return false;

      var keys = health.GetKeyArray(Allocator.Temp);
      foreach (var key in keys) {
        if (!other.health.TryGetValue(key, out var otherValue))
          return false;
        if (!health[key].Equals(otherValue))
          return false;
      }
      keys.Dispose();

      return true;
    }
  }

  [Serializable]
  public struct HealthData : IEquatable<HealthData>
  {
    public int current;
    public int max;
    public bool isAlive;

    public bool Equals(HealthData other) =>
      current == other.current &&
      max == other.max &&
      isAlive == other.isAlive;
  }

  // ============================================================================
  // CHARACTER MANA STATE
  // ============================================================================

  /// <summary>
  /// Normalized mana/resource data for all characters.
  /// Provides O(1) lookup by Entity.
  /// </summary>
  public struct CharacterManaState : IGameState, IEquatable<CharacterManaState>
  {
    public NativeHashMap<Entity, ManaData> mana;

    public bool Equals(CharacterManaState other)
    {
      if (!mana.IsCreated && !other.mana.IsCreated)
        return true;
      if (mana.IsCreated != other.mana.IsCreated)
        return false;
      if (mana.Count() != other.mana.Count())
        return false;

      var keys = mana.GetKeyArray(Allocator.Temp);
      foreach (var key in keys) {
        if (!other.mana.TryGetValue(key, out var otherValue))
          return false;
        if (!mana[key].Equals(otherValue))
          return false;
      }
      keys.Dispose();

      return true;
    }
  }

  [Serializable]
  public struct ManaData : IEquatable<ManaData>
  {
    public int current;
    public int max;

    public bool Equals(ManaData other) =>
      current == other.current &&
      max == other.max;
  }

  // ============================================================================
  // CHARACTER STATUS STATE
  // ============================================================================

  /// <summary>
  /// Normalized status effect data for all characters.
  /// Provides O(1) lookup by Entity.
  /// </summary>
  public struct CharacterStatusState : IGameState, IEquatable<CharacterStatusState>
  {
    public NativeHashMap<Entity, CharacterStatus> statuses;

    public bool Equals(CharacterStatusState other)
    {
      if (!statuses.IsCreated && !other.statuses.IsCreated)
        return true;
      if (statuses.IsCreated != other.statuses.IsCreated)
        return false;
      if (statuses.Count() != other.statuses.Count())
        return false;

      var keys = statuses.GetKeyArray(Allocator.Temp);
      foreach (var key in keys) {
        if (!other.statuses.TryGetValue(key, out var otherValue))
          return false;
        if (statuses[key] != otherValue)
          return false;
      }
      keys.Dispose();

      return true;
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

  // ============================================================================
  // CHARACTER IDENTITY STATE
  // ============================================================================

  /// <summary>
  /// Normalized identity data for all characters (name, team affiliation).
  /// This data rarely changes after initialization.
  /// Provides O(1) lookup by Entity.
  /// </summary>
  public struct CharacterIdentityState : IGameState, IEquatable<CharacterIdentityState>
  {
    public NativeHashMap<Entity, FixedString32Bytes> names;
    public NativeHashMap<Entity, bool> isEnemy; // true = enemy, false = player

    public bool Equals(CharacterIdentityState other)
    {
      // Compare names
      if (!names.IsCreated && !other.names.IsCreated) { /* continue */ } else if (names.IsCreated != other.names.IsCreated)
        return false;
      else if (names.Count() != other.names.Count())
        return false;
      else {
        var keys = names.GetKeyArray(Allocator.Temp);
        foreach (var key in keys) {
          if (!other.names.TryGetValue(key, out var otherValue))
            return false;
          if (names[key] != otherValue)
            return false;
        }
        keys.Dispose();
      }

      // Compare isEnemy
      if (!isEnemy.IsCreated && !other.isEnemy.IsCreated)
        return true;
      if (isEnemy.IsCreated != other.isEnemy.IsCreated)
        return false;
      if (isEnemy.Count() != other.isEnemy.Count())
        return false;

      var enemyKeys = isEnemy.GetKeyArray(Allocator.Temp);
      foreach (var key in enemyKeys) {
        if (!other.isEnemy.TryGetValue(key, out var otherValue))
          return false;
        if (isEnemy[key] != otherValue)
          return false;
      }
      enemyKeys.Dispose();

      return true;
    }
  }

  // ============================================================================
  // CHARACTER ROSTER STATE
  // ============================================================================

  /// <summary>
  /// Normalized roster/categorization of all characters.
  /// Stores only Entity references for efficient categorization and filtering.
  /// UI components can use these lists to iterate specific groups.
  /// </summary>
  public struct CharacterRosterState : IGameState, IEquatable<CharacterRosterState>
  {
    // All characters in the battle
    public FixedList128Bytes<Entity> allCharacters;

    // Team categorization
    public FixedList32Bytes<Entity> players;
    public FixedList32Bytes<Entity> enemies;

    // Status categorization (for quick filtering)
    public FixedList128Bytes<Entity> aliveCharacters;
    public FixedList128Bytes<Entity> deadCharacters;

    // Cached counts for UI display
    public int totalCharacterCount;
    public int playerCount;
    public int enemyCount;

    public bool Equals(CharacterRosterState other)
    {
      if (totalCharacterCount != other.totalCharacterCount)
        return false;
      if (playerCount != other.playerCount)
        return false;
      if (enemyCount != other.enemyCount)
        return false;

      // Compare all characters list
      if (allCharacters.Length != other.allCharacters.Length)
        return false;
      for (int i = 0; i < allCharacters.Length; i++) {
        if (allCharacters[i] != other.allCharacters[i])
          return false;
      }

      // Compare players list
      if (players.Length != other.players.Length)
        return false;
      for (int i = 0; i < players.Length; i++) {
        if (players[i] != other.players[i])
          return false;
      }

      // Compare enemies list
      if (enemies.Length != other.enemies.Length)
        return false;
      for (int i = 0; i < enemies.Length; i++) {
        if (enemies[i] != other.enemies[i])
          return false;
      }

      // Compare alive characters
      if (aliveCharacters.Length != other.aliveCharacters.Length)
        return false;
      for (int i = 0; i < aliveCharacters.Length; i++) {
        if (aliveCharacters[i] != other.aliveCharacters[i])
          return false;
      }

      // Compare dead characters
      if (deadCharacters.Length != other.deadCharacters.Length)
        return false;
      for (int i = 0; i < deadCharacters.Length; i++) {
        if (deadCharacters[i] != other.deadCharacters[i])
          return false;
      }

      return true;
    }
  }
}