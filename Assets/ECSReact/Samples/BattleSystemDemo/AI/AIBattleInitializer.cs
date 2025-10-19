using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;
using UnityEngine;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Helper class to initialize AI behaviors for enemies in battle - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState dependency
  /// - Uses CharacterRosterState, CharacterIdentityState, CharacterHealthState
  /// - Enemy lookup via roster instead of filtering all characters
  /// </summary>
  public static class AIBattleInitializer
  {
    /// <summary>
    /// Initialize AI for all enemies in the current battle.
    /// NEW: Uses CharacterRosterState to get enemy entities directly.
    /// </summary>
    public static void InitializeEnemyAI()
    {
      // Get roster state to find enemies
      if (!SceneStateManager.Instance.GetState<CharacterRosterState>(out var rosterState)) {
        Debug.LogWarning("Cannot initialize AI - CharacterRosterState not found");
        return;
      }

      // Get identity state for enemy names (used in behavior assignment)
      bool hasIdentity = SceneStateManager.Instance.GetState<CharacterIdentityState>(out var identityState);

      // Get health state to check alive status
      bool hasHealth = SceneStateManager.Instance.GetState<CharacterHealthState>(out var healthState);

      // Process each enemy
      for (int i = 0; i < rosterState.enemies.Length; i++) {
        Entity enemyEntity = rosterState.enemies[i];

        if (enemyEntity == Entity.Null)
          continue;

        // Skip dead enemies if we have health state
        if (hasHealth && healthState.health.IsCreated &&
            healthState.health.TryGetValue(enemyEntity, out var health)) {
          if (!health.isAlive)
            continue;
        }

        // Get enemy name for behavior assignment
        FixedString32Bytes enemyName = default;
        if (hasIdentity && identityState.names.IsCreated) {
          identityState.names.TryGetValue(enemyEntity, out enemyName);
        }

        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;

        // Assign AI behavior based on enemy type
        AssignAIBehavior(entityManager, enemyEntity, enemyName);
      }

      Debug.Log($"Enemy AI initialized for {rosterState.enemies.Length} enemies");
    }

    /// <summary>
    /// Assign appropriate AI behavior to an enemy based on their name/type.
    /// NEW: Takes Entity and name separately instead of CharacterData struct.
    /// </summary>
    private static void AssignAIBehavior(
        EntityManager entityManager,
        Entity enemyEntity,
        FixedString32Bytes enemyName)
    {
      AIBehavior behavior;

      // Determine behavior based on enemy name/type
      // In a real game, this would be data-driven
      string nameStr = enemyName.ToString().ToLower();

      if (nameStr.Contains("boss")) {
        // Boss enemies are tactical
        behavior = CreateBossBehavior();
      } else if (nameStr.Contains("goblin")) {
        // Goblins are aggressive but weak
        behavior = AIBehavior.CreateAggressive();
        behavior.defendThreshold = 0.15f; // Only defend when nearly dead
        behavior.thinkingDuration = 0.6f; // Quick decisions
      } else if (nameStr.Contains("orc")) {
        // Orcs are balanced fighters
        behavior = AIBehavior.CreateBalanced();
        behavior.skillUseChance = 0.3f;
        behavior.thinkingDuration = 0.8f;
      } else if (nameStr.Contains("mage") || nameStr.Contains("wizard")) {
        // Mages prefer skills and tactical targeting
        behavior = CreateMageBehavior();
      } else if (nameStr.Contains("tank") || nameStr.Contains("guardian")) {
        // Tanks are defensive
        behavior = AIBehavior.CreateDefensive();
        behavior.defendThreshold = 0.6f; // Defend often
      } else {
        // Default behavior for unknown enemies
        behavior = AIBehavior.CreateRandom();
      }

      // Add the behavior component to the entity
      if (!entityManager.HasComponent<AIBehavior>(enemyEntity)) {
        entityManager.AddComponentData(enemyEntity, behavior);
      } else {
        entityManager.SetComponentData(enemyEntity, behavior);
      }

      if (!string.IsNullOrEmpty(nameStr)) {
        Debug.Log($"Assigned {behavior.strategy} AI to {enemyName}");
      } else {
        Debug.Log($"Assigned {behavior.strategy} AI to entity {enemyEntity.Index}");
      }
    }

    private static AIBehavior CreateBossBehavior()
    {
      return new AIBehavior
      {
        strategy = AIStrategy.Boss,
        aggressionLevel = 0.7f,
        skillUseChance = 0.6f,
        defendThreshold = 0.25f,
        thinkingDuration = 1.5f, // Bosses think longer for dramatic effect
        targetLowestHealthWeight = 0.5f,
        targetHighestThreatWeight = 0.3f,
        targetRandomWeight = 0.2f
      };
    }

    private static AIBehavior CreateMageBehavior()
    {
      return new AIBehavior
      {
        strategy = AIStrategy.Tactical,
        aggressionLevel = 0.4f,
        skillUseChance = 0.7f, // Prefer skills
        defendThreshold = 0.4f,
        thinkingDuration = 1.0f,
        targetLowestHealthWeight = 0.6f, // Try to finish off weak enemies
        targetHighestThreatWeight = 0.3f,
        targetRandomWeight = 0.1f
      };
    }

    /// <summary>
    /// Modify AI difficulty for all enemies.
    /// Uses entity queries instead of state lookups.
    /// </summary>
    public static void SetGlobalAIDifficulty(EntityManager entityManager, float difficultyModifier)
    {
      // Get all entities with AI behavior
      var query = entityManager.CreateEntityQuery(typeof(AIBehavior));
      var entities = query.ToEntityArray(Allocator.Temp);

      foreach (var entity in entities) {
        var behavior = entityManager.GetComponentData<AIBehavior>(entity);

        // Adjust AI parameters based on difficulty
        behavior.aggressionLevel = Mathf.Clamp01(behavior.aggressionLevel * difficultyModifier);
        behavior.skillUseChance = Mathf.Clamp01(behavior.skillUseChance * difficultyModifier);
        behavior.thinkingDuration = Mathf.Max(0.3f, behavior.thinkingDuration / difficultyModifier);

        // Harder difficulty = smarter targeting
        if (difficultyModifier > 1f) {
          behavior.targetLowestHealthWeight *= difficultyModifier;
          behavior.targetRandomWeight /= difficultyModifier;
        }

        entityManager.SetComponentData(entity, behavior);
      }

      entities.Dispose();
      Debug.Log($"AI difficulty adjusted by factor of {difficultyModifier}");
    }

    /// <summary>
    /// Change AI behavior for a specific enemy (useful for boss phases).
    /// </summary>
    public static void ChangeEnemyAIBehavior(
        EntityManager entityManager,
        Entity enemy,
        AIStrategy newStrategy)
    {
      if (!entityManager.HasComponent<AIBehavior>(enemy)) {
        Debug.LogWarning($"Entity {enemy} does not have AI behavior");
        return;
      }

      var behavior = entityManager.GetComponentData<AIBehavior>(enemy);
      behavior.strategy = newStrategy;

      // Adjust other parameters based on new strategy
      switch (newStrategy) {
        case AIStrategy.Aggressive:
          behavior.aggressionLevel = 0.9f;
          behavior.defendThreshold = 0.1f;
          break;

        case AIStrategy.Defensive:
          behavior.aggressionLevel = 0.2f;
          behavior.defendThreshold = 0.7f;
          break;

        case AIStrategy.Boss:
          // Boss phase change - become more aggressive
          behavior.aggressionLevel = Mathf.Min(1f, behavior.aggressionLevel + 0.2f);
          behavior.skillUseChance = Mathf.Min(1f, behavior.skillUseChance + 0.2f);
          break;
      }

      entityManager.SetComponentData(enemy, behavior);

      // Dispatch action for UI feedback
      ECSActionDispatcher.Dispatch(new ModifyAIBehaviorAction
      {
        targetEntity = enemy,
        newStrategy = newStrategy,
        aggressionModifier = behavior.aggressionLevel
      });
    }
  }
}