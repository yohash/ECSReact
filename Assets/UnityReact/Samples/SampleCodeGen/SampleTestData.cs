using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.SampleCodeGen
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
  /// UI-specific state for interface management.
  /// Tests: Mixed types with FixedString + IEquatable
  /// </summary>
  public struct UIState : IGameState, IEquatable<UIState>
  {
    public bool showInventory;
    public bool showTechTree;
    public bool showSettings;
    public Entity selectedEntity;
    public FixedString64Bytes currentScreen;
    public float uiScale;
    public int notificationCount;

    public bool Equals(UIState other)
    {
      return showInventory == other.showInventory &&
             showTechTree == other.showTechTree &&
             showSettings == other.showSettings &&
             selectedEntity.Equals(other.selectedEntity) &&
             currentScreen == other.currentScreen &&
             math.abs(uiScale - other.uiScale) < 0.01f &&
             notificationCount == other.notificationCount;
    }
  }

  /// <summary>
  /// Combat system state.
  /// Tests: Complex state without IEquatable (should show warning)
  /// </summary>
  public struct CombatState : IGameState
  {
    public int enemyCount;
    public float combatTimer;
    public bool inCombat;
    public Entity currentTarget;
    public float lastDamageTime;

    // Intentionally NOT implementing IEquatable to test warning system
  }

  /// <summary>
  /// Simple inventory state.
  /// Tests: Minimal state for basic functionality
  /// </summary>
  public struct InventoryState : IGameState, IEquatable<InventoryState>
  {
    public int itemCount;
    public int maxCapacity;

    public bool Equals(InventoryState other)
    {
      return itemCount == other.itemCount && maxCapacity == other.maxCapacity;
    }
  }

  // ===========================================
  // TEST GAME ACTIONS
  // ===========================================

  /// <summary>
  /// Simple resource spending action.
  /// Tests: Basic action with primitive parameters
  /// </summary>
  public struct SpendMatterAction : IGameAction
  {
    public int amount;
    public int itemId;
    public bool allowOverspend;
  }

  /// <summary>
  /// Player movement action with advanced parameters.
  /// Tests: Unity math types + Entity references
  /// </summary>
  public struct MovePlayerAction : IGameAction
  {
    public Entity playerEntity;
    public float3 targetPosition;
    public float moveSpeed;
    public bool useDash;
    public quaternion targetRotation;
  }

  /// <summary>
  /// Tech tree progression action.
  /// Tests: Mixed parameter types
  /// </summary>
  public struct UnlockTechAction : IGameAction
  {
    public int techId;
    public int cost;
    public Entity researchEntity;
    public bool skipPrerequisites;
  }

  /// <summary>
  /// Combat action with complex parameters.
  /// Tests: Multiple Entity references + FixedString
  /// </summary>
  public struct AttackTargetAction : IGameAction
  {
    public Entity attackerEntity;
    public Entity targetEntity;
    public float damage;
    public float3 attackPosition;
    public FixedString32Bytes attackType;
    public bool isCritical;
  }

  /// <summary>
  /// UI state management action.
  /// Tests: FixedString parameters + boolean flags
  /// </summary>
  public struct ChangeScreenAction : IGameAction
  {
    public FixedString64Bytes screenName;
    public bool addToHistory;
    public float transitionDuration;
  }

  /// <summary>
  /// Inventory management action.
  /// Tests: Simple action perfect for fluent naming
  /// </summary>
  public struct AddItemAction : IGameAction
  {
    public int itemId;
    public int quantity;
    public Entity targetInventory;
  }

  /// <summary>
  /// Player level up action.
  /// Tests: Action with minimal parameters
  /// </summary>
  public struct LevelUpAction : IGameAction
  {
    public Entity playerEntity;
    public int newLevel;
  }

  /// <summary>
  /// Complex building/construction action.
  /// Tests: Many parameters of different types
  /// </summary>
  public struct BuildStructureAction : IGameAction
  {
    public Entity builderEntity;
    public int structureType;
    public float3 buildPosition;
    public quaternion buildRotation;
    public int materialCost;
    public float buildTime;
    public bool autoStart;
    public FixedString128Bytes structureName;
    public Entity targetFoundation;
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

  /// <summary>
  /// Damage dealing action.
  /// Tests: Float parameters + Entity targeting
  /// </summary>
  public struct TakeDamageAction : IGameAction
  {
    public Entity targetEntity;
    public float damage;
    public float3 damagePosition;
    public Entity damageSource;
  }

  /// <summary>
  /// Example reducer that processes SpendMatterAction.
  /// Shows how the generated code integrates with actual game systems.
  /// </summary>
  [Unity.Burst.BurstCompile]
  public partial class GameStateReducer : ReducerSystem<GameState, SpendMatterAction>
  {
    protected override void ReduceState(ref GameState state, SpendMatterAction action)
    {
      if (action.allowOverspend || state.matter >= action.amount) {
        state.matter -= action.amount;
        UnityEngine.Debug.Log($"Spent {action.amount} matter for item {action.itemId}. Remaining: {state.matter}");
      } else {
        UnityEngine.Debug.LogWarning($"Not enough matter to spend {action.amount}. Current: {state.matter}");
      }
    }
  }

  /// <summary>
  /// Example reducer for player movement.
  /// </summary>
  [Unity.Burst.BurstCompile]
  public partial class PlayerMovementReducer : ReducerSystem<PlayerState, MovePlayerAction>
  {
    protected override void ReduceState(ref PlayerState state, MovePlayerAction action)
    {
      if (state.playerEntity.Equals(action.playerEntity)) {
        state.position = action.targetPosition;
        state.rotation = action.targetRotation;

        if (action.useDash) {
          // Could add dash logic here
          UnityEngine.Debug.Log($"Player dashed to {action.targetPosition}");
        } else {
          UnityEngine.Debug.Log($"Player moved to {action.targetPosition}");
        }
      }
    }
  }

  /// <summary>
  /// Example validation middleware.
  /// </summary>
  public partial class SpendMatterValidation : MiddlewareSystem<SpendMatterAction>
  {
    protected override void ProcessAction(SpendMatterAction action, Entity actionEntity)
    {
      if (action.amount <= 0) {
        UnityEngine.Debug.LogError($"Invalid SpendMatterAction: amount must be positive, got {action.amount}");

        // Could add InvalidActionTag here to prevent processing
        EntityManager.AddComponent<InvalidActionTag>(actionEntity);
      }

      if (action.itemId < 0) {
        UnityEngine.Debug.LogError($"Invalid SpendMatterAction: itemId must be non-negative, got {action.itemId}");
        EntityManager.AddComponent<InvalidActionTag>(actionEntity);
      }
    }
  }
}