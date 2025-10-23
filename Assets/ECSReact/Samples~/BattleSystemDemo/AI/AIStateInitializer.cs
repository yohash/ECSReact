using Unity.Entities;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Helper methods for initializing AI state at battle start - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState dependency
  /// - Uses CharacterRosterState and CharacterIdentityState
  /// - Fetches enemies from roster instead of looping all characters
  /// </summary>
  public static class AIStateInitializer
  {
    /// <summary>
    /// Complete AI initialization - call this once at battle start.
    /// Sets up thinking state singleton and ensures all enemies have AI.
    /// NEW: Uses normalized states to find enemies.
    /// </summary>
    public static void InitializeAIForBattle()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      var entityManager = world.EntityManager;

      EnsureAIThinkingStateExists(entityManager);
      EnsureEnemiesHaveAIBehavior(entityManager);

      Debug.Log("AI initialization complete");
    }

    /// <summary>
    /// Initialize the AIThinkingState singleton.
    /// Call this once at battle start.
    /// </summary>
    private static void EnsureAIThinkingStateExists(EntityManager entityManager)
    {
      // Check if singleton already exists
      var query = entityManager.CreateEntityQuery(typeof(AIThinkingState));
      if (query.CalculateEntityCount() > 0) {
        query.Dispose();
        return; // Already exists
      }
      query.Dispose();

      // Create the singleton entity
      var singletonEntity = entityManager.CreateEntity();
      entityManager.AddComponentData(singletonEntity, new AIThinkingState
      {
        thinkingEnemy = Entity.Null,
        thinkingStartTime = 0,
        thinkDuration = 0,
        isThinking = false,
        decisionsMadeThisBattle = 0,
        hasPendingDecision = false,
        decidingEnemy = Entity.Null,
        chosenAction = ActionType.None,
        chosenTarget = Entity.Null,
        chosenSkillId = 0,
        readyToExecuteCombat = false,
        combatExecutor = Entity.Null,
        combatAction = ActionType.None,
        combatTarget = Entity.Null,
        combatDamage = 0
      });

      Debug.Log("AIThinkingState singleton created");
    }

    /// <summary>
    /// Initialize AIBehavior for all enemies in the roster.
    /// NEW: Uses CharacterRosterState to get enemy list directly (O(n) but filtered list).
    /// OLD: Looped through all characters in PartyState.
    /// </summary>
    private static void EnsureEnemiesHaveAIBehavior(EntityManager entityManager)
    {
      // Fetch roster state to get enemy list
      if (!SceneStateManager.Instance.GetState<CharacterRosterState>(out var rosterState)) {
        Debug.LogError("Cannot initialize AI - CharacterRosterState not found!");
        return;
      }

      // Fetch identity state to get enemy names (for logging)
      bool hasIdentity = SceneStateManager.Instance.GetState<CharacterIdentityState>(out var identityState);

      Debug.Log($"Initializing AI for {rosterState.enemies.Length} enemies...");

      // Process each enemy in the roster
      for (int i = 0; i < rosterState.enemies.Length; i++) {
        Entity enemyEntity = rosterState.enemies[i];

        if (enemyEntity == Entity.Null)
          continue;

        // Add AIBehavior if missing
        if (!entityManager.HasComponent<AIBehavior>(enemyEntity)) {
          // Assign default behavior (in real game, this would be data-driven)
          var behavior = AIBehavior.CreateBalanced();
          entityManager.AddComponentData(enemyEntity, behavior);

          // Log with enemy name if available
          if (hasIdentity && identityState.names.IsCreated &&
              identityState.names.TryGetValue(enemyEntity, out var name)) {
            Debug.Log($"Added AIBehavior to enemy: {name}");
          } else {
            Debug.Log($"Added AIBehavior to enemy entity {enemyEntity.Index}");
          }
        }
      }

      Debug.Log($"AI behaviors assigned to {rosterState.enemies.Length} enemies");
    }
  }
}