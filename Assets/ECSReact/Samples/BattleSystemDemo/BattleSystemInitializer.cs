using ECSReact.Core;
using System.Collections;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Initializes the battle system with default states and party setup - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed CharacterData/PartySetup structs
  /// - Dispatches AddCharacterAction directly (middleware creates entities)
  /// - Fetches entities from CharacterRosterState after creation
  /// - AI initialization uses normalized states
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
        _ = Initialize();
      }
    }

    /// <summary>
    /// Initialize all battle system states with default values.
    /// NEW: Dispatches actions to create characters, waits for entities, then sets up battle.
    /// </summary>
    public async Task Initialize()
    {
      Debug.Log("Battle System Initializing...");

      StateNotificationEvents.InitializeEvents();
      StateSubscriptionRegistration.InitializeSubscriptions();

      // ========================================================================
      // STEP 1: Dispatch AddCharacter actions for all combatants
      // ========================================================================
      Debug.Log("Creating party characters...");
      CreateAllCharacters();

      // Wait a frame for middleware to create entities and reducers to populate states
      await Task.Yield();

      // Additional safety delay to ensure all state updates complete
      await Task.Delay(100);

      // ========================================================================
      // STEP 2: Fetch created entities from CharacterRosterState
      // ========================================================================
      Debug.Log("Fetching created entities...");
      var turnOrder = CreateTurnOrderFromRoster();

      if (turnOrder.Length == 0) {
        Debug.LogError("Failed to create turn order - no entities found in roster!");
        return;
      }

      // ========================================================================
      // STEP 3: Initialize AI for enemies
      // ========================================================================
      Debug.Log("Initializing AI...");
      AIStateInitializer.InitializeAIForBattle();

      // ========================================================================
      // STEP 4: Initialize battle state with turn order
      // ========================================================================
      Debug.Log("Initializing battle state...");
      InitializeBattleState(turnOrder);

      Debug.Log($"Battle System initialized with {playerNames.Length} players vs {enemyNames.Length} enemies");
    }

    // ========================================================================
    // STEP 1: CHARACTER CREATION - Dispatch AddCharacter actions
    // ========================================================================

    /// <summary>
    /// NEW: Dispatches AddCharacter actions for all players and enemies.
    /// Middleware will create entities and reducers will populate normalized states.
    /// </summary>
    private void CreateAllCharacters()
    {
      // Create player characters
      foreach (var name in playerNames) {
        Store.Instance.Dispatch(new AddCharacterAction()
        {
          name = new FixedString32Bytes(name),
          maxHealth = 100,
          maxMana = 50,
          isEnemy = false,
          initialStatus = CharacterStatus.None
        });
      }

      // Create enemy characters
      foreach (var name in enemyNames) {
        Store.Instance.Dispatch(new AddCharacterAction()
        {
          name = new FixedString32Bytes(name),
          maxHealth = 75,
          maxMana = 25,
          isEnemy = true,
          initialStatus = CharacterStatus.None
        });
      }

      Debug.Log($"Dispatched {playerNames.Length + enemyNames.Length} AddCharacter actions");
    }

    // ========================================================================
    // STEP 2: TURN ORDER CREATION - Fetch from CharacterRosterState
    // ========================================================================

    /// <summary>
    /// NEW: Creates turn order by fetching entities from CharacterRosterState.
    /// Uses simple alternating pattern: Player, Enemy, Player, Enemy, etc.
    /// OLD: Relied on PartyState.characters array
    /// </summary>
    private Entity[] CreateTurnOrderFromRoster()
    {
      // Fetch roster state
      if (!SceneStateManager.Instance.GetState<CharacterRosterState>(out var rosterState)) {
        Debug.LogError("CharacterRosterState not found! Cannot create turn order.");
        return new Entity[0];
      }

      // Validate we have entities
      if (rosterState.players.Length == 0 && rosterState.enemies.Length == 0) {
        Debug.LogError("No characters in roster! Make sure AddCharacter actions were processed.");
        return new Entity[0];
      }

      Debug.Log($"Roster contains {rosterState.players.Length} players and {rosterState.enemies.Length} enemies");

      // Calculate turn order size
      int totalCharacters = rosterState.players.Length + rosterState.enemies.Length;
      var turnOrder = new Entity[totalCharacters];

      // Simple alternating pattern: Player, Enemy, Player, Enemy...
      int playerIndex = 0;
      int enemyIndex = 0;
      int turnIndex = 0;

      while (playerIndex < rosterState.players.Length || enemyIndex < rosterState.enemies.Length) {
        // Add player if available
        if (playerIndex < rosterState.players.Length) {
          turnOrder[turnIndex++] = rosterState.players[playerIndex++];
        }

        // Add enemy if available
        if (enemyIndex < rosterState.enemies.Length) {
          turnOrder[turnIndex++] = rosterState.enemies[enemyIndex++];
        }
      }

      Debug.Log($"Created turn order with {turnOrder.Length} combatants");
      return turnOrder;
    }

    // ========================================================================
    // STEP 4: BATTLE STATE INITIALIZATION
    // ========================================================================

    /// <summary>
    /// Initialize battle state with turn order via Store action.
    /// </summary>
    private void InitializeBattleState(Entity[] turnOrder)
    {
      var order = new FixedList128Bytes<Entity>();

      // Add entities to the turn order (limited by FixedList capacity)
      for (int i = 0; i < turnOrder.Length && i < 32; i++) {
        order.Add(turnOrder[i]);
      }

      // Dispatch action to initialize turn order
      Store.Instance.Dispatch(new InitializeTurnOrderAction() { turnOrder = order });

      Debug.Log($"Battle state initialized with {order.Length} combatants in turn order");
    }
  }
}