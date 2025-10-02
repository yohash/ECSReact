using ECSReact.Core;
using System.Collections;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Initializes the battle system with default states and party setup.
  /// Add this MonoBehaviour to your scene or call Initialize() from your scene startup.
  /// </summary>
  public class BattleSystemInitializer : MonoBehaviour
  {
    [Header("Party Configuration")]
    [SerializeField] private string[] playerNames = { "Hero", "Mage", "Warrior" };
    [SerializeField] private string[] enemyNames = { "Goblin", "Orc" };

    [Header("Battle Settings")]
    [SerializeField] private bool initializeOnStart = true;

    private void Start()
    {
      if (initializeOnStart) {
        Initialize();
      }
    }

    /// <summary>
    /// Initialize all battle system states with default values.
    /// Call this method to set up the battle system programmatically.
    /// </summary>
    public async Task Initialize()
    {
      Debug.Log("Battle System Initializing...");

      StateNotificationEvents.InitializeEvents();
      StateSubscriptionRegistration.InitializeSubscriptions();

      var world = World.DefaultGameObjectInjectionWorld;
      var entityManager = world.EntityManager;

      // Create party characters and entities
      var partySetup = CreatePartySetup();

      // Initialize party state with character data
      InitializePartyState(entityManager, partySetup);
      Debug.Log("Initialized PartyState");

      await Task.Delay(1000);

      var turnOrder = CreateTurnOrder(partySetup);

      // After InitializeTurnOrderAction is dispatched and battle starts
      var hasState = SceneStateManager.Instance.GetState<PartyState>(out var partyState);
      if (hasState) {
        AIStateInitializer.InitializeAIForBattle(entityManager, partyState);
      } else {
        Debug.LogError("BattleSystemInitializer - PartyState not found, cannot initialize AI");
      }

      // Initialize core battle state
      InitializeBattleState(entityManager, turnOrder);
      Debug.Log("Initialized BattleState");

      //// Initialize UI state
      //InitializeUIBattleState(entityManager);
      //Debug.Log("Initialized UIBattleState");

      //// Initialize save system state
      //InitializeSaveState(entityManager);
      //Debug.Log("Initialized SaveState");

      //// Initialize battle log state
      //InitializeBattleLogState(entityManager);

      //Debug.Log($"Battle System initialized with {playerNames.Length} players vs {enemyNames.Length} enemies");
    }

    private PartySetup CreatePartySetup()
    {
      var setup = new PartySetup();

      // Create player entities
      setup.playerCharacters = new CharacterData[playerNames.Length];

      for (int i = 0; i < playerNames.Length; i++) {
        setup.playerCharacters[i] = new CharacterData
        {
          entity = Entity.Null,
          name = playerNames[i],
          maxHealth = 100,
          currentHealth = 100,
          maxMana = 50,
          currentMana = 50,
          status = CharacterStatus.None,
          isEnemy = false,
        };
      }

      // Create enemy entities  
      setup.enemyCharacters = new CharacterData[enemyNames.Length];

      for (int i = 0; i < enemyNames.Length; i++) {
        setup.enemyCharacters[i] = new CharacterData
        {
          entity = Entity.Null,
          name = enemyNames[i],
          maxHealth = 75,
          currentHealth = 75,
          maxMana = 25,
          currentMana = 25,
          status = CharacterStatus.None,
          isEnemy = true,
        };
      }

      return setup;
    }

    private Entity[] CreateTurnOrder(PartySetup setup)
    {
      // Simple alternating turn order: Player, Enemy, Player, Enemy, etc.
      var turnOrder = new Entity[setup.playerCharacters.Length + setup.enemyCharacters.Length];

      var players = new Entity[setup.playerCharacters.Length];
      var enemies = new Entity[setup.enemyCharacters.Length];

      var hasState = SceneStateManager.Instance.GetState<PartyState>(out var partyState);
      if (hasState) {

        int pIndex = 0;
        int eIndex = 0;

        foreach (var character in partyState.characters) {
          if (character.isEnemy) {
            if (eIndex < setup.enemyCharacters.Length) {
              enemies[eIndex++] = character.entity;
            }
          } else {
            if (pIndex < setup.playerCharacters.Length) {
              players[pIndex++] = character.entity;
            }
          }
        }
      } else {
        Debug.LogError("BattleSystemInitializer - PartyState not found, cannot create Turn Order");
        return new Entity[0];
      }

      int playerIndex = 0;
      int enemyIndex = 0;
      int turnIndex = 0;

      while (playerIndex < setup.playerCharacters.Length || enemyIndex < setup.enemyCharacters.Length) {
        if (playerIndex < setup.playerCharacters.Length) {
          turnOrder[turnIndex++] = players[playerIndex++];
        }
        if (enemyIndex < setup.enemyCharacters.Length) {
          turnOrder[turnIndex++] = enemies[enemyIndex++];
        }
      }

      return turnOrder;
    }

    private void InitializeBattleState(EntityManager entityManager, Entity[] turnOrder)
    {
      var order = new FixedList128Bytes<Entity>();

      // Add entities to the turn order
      for (int i = 0; i < turnOrder.Length && i < 32; i++) {
        order.Add(turnOrder[i]);
      }

      Store.Instance.InitializeTurnOrder(order);

      //var battleState = new BattleState
      //{
      //  battleActive = true,
      //  currentPhase = startingPhase,
      //  activeCharacterIndex = 0,
      //  turnCount = 1,
      //  turnTimer = 0f,
      //  turnOrder = new FixedList32Bytes<Entity>()
      //};

      //// Add entities to turn order
      //for (int i = 0; i < turnOrder.Length && i < 32; i++) {
      //  battleState.turnOrder.Add(turnOrder[i]);
      //}

      //var entity = entityManager.CreateSingleton(battleState, "Battle State");
    }

    private void InitializePartyState(EntityManager entityManager, PartySetup setup)
    {
      var partyState = new PartyState
      {
        characters = new FixedList32Bytes<CharacterData>()
      };

      // Add all characters (players first, then enemies)
      foreach (var character in setup.playerCharacters) {
        Store.Instance.AddCharacter(
          character.name,
          character.maxHealth,
          character.maxMana,
          character.isEnemy,
          character.status
        );
        //partyState.characters.Add(character);
      }
      foreach (var character in setup.enemyCharacters) {
        Store.Instance.AddCharacter(
          character.name,
          character.maxHealth,
          character.maxMana,
          character.isEnemy,
          character.status
        );
        //partyState.characters.Add(character);
      }

      //var entity = entityManager.CreateSingleton(partyState, "Party State");
    }

    private void InitializeUIBattleState(EntityManager entityManager)
    {
      var uiState = new UIBattleState
      {
        activePanel = MenuPanel.None,
        selectedAction = ActionType.None,
        selectedTarget = Entity.Null,
        selectedSkillId = -1,
        selectedItemId = -1,
      };

      var entity = entityManager.CreateSingleton(uiState, "UI State");
    }

    private void InitializeSaveState(EntityManager entityManager)
    {
      var saveState = new SaveState
      {
        currentStatus = SaveStatus.Idle,
        currentFileName = "",
        lastErrorMessage = "",
        saveStartTime = 0f,
        lastSaveCompletedTime = 0f,
        totalSavesAttempted = 0,
        totalSavesCompleted = 0
      };

      var entity = entityManager.CreateSingleton(saveState, "Save State");
    }

    private void InitializeBattleLogState(EntityManager entityManager)
    {
      var logState = new BattleLogState
      {
        entries = new FixedList512Bytes<BattleLogEntry>(),
        totalEntriesLogged = 0
      };

      var entity = entityManager.CreateSingleton(logState, "Log State");

      // Add initial log entry
      var initialEntry = new BattleLogEntry
      {
        logType = LogType.System,
        message = "=== Battle Started ===",
        timestamp = Time.realtimeSinceStartup,
        damageAmount = 0
      };

      FixedString128Bytes initialMessage = "=== Battle Started ===";

      //Store.Instance.BattleLog(
      //  LogType.System,
      //  initialMessage,
      //  Entity.Null, // No source entity for system messages
      //  Entity.Null, // No target entity for system messages
      //  0,
      //  Time.realtimeSinceStartup
      //);
    }

    /// <summary>
    /// Helper struct to organize party setup data
    /// </summary>
    private struct PartySetup
    {
      public CharacterData[] playerCharacters;
      public CharacterData[] enemyCharacters;
    }

    /// <summary>
    /// Reset the battle system to initial state (useful for testing)
    /// </summary>
    [ContextMenu("Reset Battle System")]
    public void ResetBattleSystem()
    {
      Initialize();
      Debug.Log("Battle System reset to initial state");
    }

    /// <summary>
    /// Add a test log entry (useful for testing the log system)
    /// </summary>
    [ContextMenu("Add Test Log Entry")]
    public void AddTestLogEntry()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      var entityManager = world.EntityManager;

      var ecb = world.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>()
                     .CreateCommandBuffer();

      var logEntity = ecb.CreateEntity();
      ecb.AddComponent(logEntity, new BattleLogAction
      {
        logType = LogType.System,
        message = "Test log entry from initializer",
        sourceEntity = Entity.Null,
        targetEntity = Entity.Null,
        numericValue = 42,
        timestamp = Time.realtimeSinceStartup
      });
      ecb.AddComponent(logEntity, new ECSReact.Core.ActionTag());
    }

    /// <summary>
    /// Editor helper to validate setup
    /// </summary>
    [ContextMenu("Validate Setup")]
    private void ValidateSetup()
    {
      if (playerNames.Length == 0) {
        Debug.LogWarning("No player names configured!");
      }
      if (enemyNames.Length == 0) {
        Debug.LogWarning("No enemy names configured!");
      }
      if (playerNames.Length + enemyNames.Length > 32) {
        Debug.LogError("Too many characters! FixedList32Bytes supports max 32 entries.");
      }

      Debug.Log($"Setup validation complete: {playerNames.Length} players, {enemyNames.Length} enemies");
    }
  }
}