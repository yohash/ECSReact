using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // NORMALIZED REDUCERS - Each updates its own state slice
  // ============================================================================

  /// <summary>
  /// Adds character health data to CharacterHealthState.
  /// Characters start at full health and alive.
  /// </summary>
  [Reducer]
  public struct CharacterHealthReducer : IReducer<CharacterHealthState, CharacterCreatedAction>
  {
    public void Execute(
      ref CharacterHealthState state,
      in CharacterCreatedAction action,
      ref SystemState systemState)
    {
      // Initialize health HashMap if needed
      if (!state.health.IsCreated) {
        state.health = new NativeHashMap<Entity, HealthData>(16, Allocator.Persistent);
      }

      // Add new character with full health
      state.health.Add(action.entity, new HealthData
      {
        current = action.maxHealth,
        max = action.maxHealth,
        isAlive = true
      });

      // Update cached aggregates
      state.totalAliveCount++;
      if (action.isEnemy)
        state.aliveEnemyCount++;
      else
        state.alivePlayerCount++;
    }
  }

  /// <summary>
  /// Adds character mana data to CharacterManaState.
  /// Characters start at full mana.
  /// </summary>
  [Reducer]
  public struct CharacterManaReducer : IReducer<CharacterManaState, CharacterCreatedAction>
  {
    public void Execute(
      ref CharacterManaState state,
      in CharacterCreatedAction action,
      ref SystemState systemState)
    {
      // Initialize mana HashMap if needed
      if (!state.mana.IsCreated) {
        state.mana = new NativeHashMap<Entity, ManaData>(16, Allocator.Persistent);
      }

      // Add new character with full mana
      state.mana.Add(action.entity, new ManaData
      {
        current = action.maxMana,
        max = action.maxMana
      });
    }
  }

  /// <summary>
  /// Adds character status effects to CharacterStatusState.
  /// </summary>
  [Reducer]
  public struct CharacterStatusReducer : IReducer<CharacterStatusState, CharacterCreatedAction>
  {
    public void Execute(
      ref CharacterStatusState state,
      in CharacterCreatedAction action,
      ref SystemState systemState)
    {
      // Initialize status HashMap if needed
      if (!state.statuses.IsCreated) {
        state.statuses = new NativeHashMap<Entity, CharacterStatus>(16, Allocator.Persistent);
      }

      // Add character with initial status
      state.statuses.Add(action.entity, action.initialStatus);
    }
  }

  /// <summary>
  /// Adds character identity data to CharacterIdentityState.
  /// Identity (name, team affiliation) rarely changes after creation.
  /// </summary>
  [Reducer]
  public struct CharacterIdentityReducer : IReducer<CharacterIdentityState, CharacterCreatedAction>
  {
    public void Execute(
      ref CharacterIdentityState state,
      in CharacterCreatedAction action,
      ref SystemState systemState)
    {
      // Initialize HashMaps if needed
      if (!state.names.IsCreated) {
        state.names = new NativeHashMap<Entity, FixedString32Bytes>(16, Allocator.Persistent);
      }
      if (!state.isEnemy.IsCreated) {
        state.isEnemy = new NativeHashMap<Entity, bool>(16, Allocator.Persistent);
      }

      // Add character identity
      state.names.Add(action.entity, action.name);
      state.isEnemy.Add(action.entity, action.isEnemy);
    }
  }

  /// <summary>
  /// Adds character to roster categorization lists in CharacterRosterState.
  /// Maintains lists for efficient UI filtering and iteration.
  /// </summary>
  [Reducer]
  public struct CharacterRosterReducer : IReducer<CharacterRosterState, CharacterCreatedAction>
  {
    public void Execute(
      ref CharacterRosterState state,
      in CharacterCreatedAction action,
      ref SystemState systemState)
    {
      // Add to all characters list
      state.allCharacters.Add(action.entity);

      // Add to team-specific list
      if (action.isEnemy) {
        state.enemies.Add(action.entity);
        state.enemyCount++;
      } else {
        state.players.Add(action.entity);
        state.playerCount++;
      }

      // Update total count
      state.totalCharacterCount++;
    }
  }
}