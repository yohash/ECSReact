using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;
using UnityEngine;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Helper class to initialize AI behaviors for enemies in battle.
  /// Can be called from BattleSystemInitializer or other setup code.
  /// </summary>
  public static class AIBattleInitializer
  {
    /// <summary>
    /// Initialize AI for all enemies in the current battle.
    /// </summary>
    public static void InitializeEnemyAI(EntityManager entityManager)
    {
      // Get party state to find enemies
      var hasState = SceneStateManager.Instance.GetState<PartyState>(out var partyState);
      if (!hasState) {
        Debug.LogWarning("Cannot initialize AI - PartyState not found");
        return;
      }

      // Process each character
      for (int i = 0; i < partyState.characters.Length; i++) {
        var character = partyState.characters[i];

        // Skip non-enemies and dead characters
        if (!character.isEnemy || !character.isAlive)
          continue;

        // Assign AI behavior based on enemy type
        AssignAIBehavior(entityManager, character);
      }

      Debug.Log("Enemy AI initialized successfully");
    }

    /// <summary>
    /// Assign appropriate AI behavior to an enemy based on their characteristics.
    /// </summary>
    private static void AssignAIBehavior(EntityManager entityManager, CharacterData character)
    {
      AIBehavior behavior;

      // Determine behavior based on enemy name/type
      // In a real game, this would be data-driven
      string enemyName = character.name.ToString().ToLower();

      if (enemyName.Contains("boss")) {
        // Boss enemies are tactical
        behavior = CreateBossBehavior();
      } else if (enemyName.Contains("goblin")) {
        // Goblins are aggressive but weak
        behavior = AIBehavior.CreateAggressive();
        behavior.defendThreshold = 0.15f; // Only defend when nearly dead
        behavior.thinkingDuration = 0.6f; // Quick decisions
      } else if (enemyName.Contains("orc")) {
        // Orcs are balanced fighters
        behavior = AIBehavior.CreateBalanced();
        behavior.skillUseChance = 0.3f;
        behavior.thinkingDuration = 0.8f;
      } else if (enemyName.Contains("mage") || enemyName.Contains("wizard")) {
        // Mages prefer skills and tactical targeting
        behavior = CreateMageBehavior();
      } else if (enemyName.Contains("tank") || enemyName.Contains("guardian")) {
        // Tanks are defensive
        behavior = AIBehavior.CreateDefensive();
        behavior.defendThreshold = 0.6f; // Defend often
      } else {
        // Default behavior for unknown enemies
        behavior = AIBehavior.CreateRandom();
      }

      // Add the behavior component to the entity
      if (!entityManager.HasComponent<AIBehavior>(character.entity)) {
        entityManager.AddComponentData(character.entity, behavior);
      } else {
        entityManager.SetComponentData(character.entity, behavior);
      }

      Debug.Log($"Assigned {behavior.strategy} AI to {character.name}");
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
    public static void ChangeEnemyAIBehavior(EntityManager entityManager, Entity enemy, AIStrategy newStrategy)
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