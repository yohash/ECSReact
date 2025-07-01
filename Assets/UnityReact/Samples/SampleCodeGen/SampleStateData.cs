using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.SampleState
{
  // ===========================================
  // TEST GAME STATES
  // ===========================================

  /// <summary>
  /// Core game state with resources and progression.
  /// Tests: Basic state with primitive types + IEquatable
  /// </summary>
  public struct GameState : IGameState, IEquatable<GameState>
  {
    public int health;
    public int maxHealth;
    public int matter;
    public int techPoints;
    public float gameTime;
    public bool gameInProgress;
    public bool isPaused;

    public bool Equals(GameState other)
    {
      return health == other.health &&
             maxHealth == other.maxHealth &&
             matter == other.matter &&
             techPoints == other.techPoints &&
             math.abs(gameTime - other.gameTime) < 0.01f &&
             gameInProgress == other.gameInProgress &&
             isPaused == other.isPaused;
    }
  }

  /// <summary>
  /// Player state with spatial data and status.
  /// Tests: Unity math types (float3) + IEquatable
  /// </summary>
  public struct PlayerState : IGameState, IEquatable<PlayerState>
  {
    public Entity playerEntity;
    public float3 position;
    public float3 velocity;
    public quaternion rotation;
    public bool isAlive;
    public int level;
    public float experience;
    public int inventoryCount;

    public bool Equals(PlayerState other)
    {
      return playerEntity.Equals(other.playerEntity) &&
             math.all(math.abs(position - other.position) < 0.01f) &&
             math.all(math.abs(velocity - other.velocity) < 0.01f) &&
             math.all(math.abs(rotation.value - other.rotation.value) < 0.01f) &&
             isAlive == other.isAlive &&
             level == other.level &&
             math.abs(experience - other.experience) < 0.01f &&
             inventoryCount == other.inventoryCount;
    }
  }

  /// <summary>
  /// Save game action for async operations.
  /// Tests: FixedString + boolean options
  /// </summary>
  public struct SaveGameAction : IGameAction
  {
    public FixedString128Bytes fileName;
    public bool includeSettings;
    public bool compressData;
    public bool createBackup;
  }
}