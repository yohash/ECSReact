using Unity.Entities;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Helper methods for initializing AI state at battle start.
  /// Use these during battle setup to create the AIThinkingState singleton.
  /// </summary>
  public static class AIStateInitializer
  {
    /// <summary>
    /// Initialize the AIThinkingState singleton.
    /// Call this once at battle start.
    /// 
    /// This will be done through a reducer in Phase 4, but this helper
    /// is useful for manual initialization during development.
    /// </summary>
    public static void EnsureAIThinkingStateExists(EntityManager entityManager)
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
        decisionsMadeThisBattle = 0
      });

      UnityEngine.Debug.Log("AIThinkingState singleton created");
    }

    /// <summary>
    /// Initialize AIBehavior for all enemies in the party.
    /// Call this during battle setup.
    /// 
    /// Note: This will eventually be done through reducers (Phase 4),
    /// but this helper is useful during development.
    /// </summary>
    public static void EnsureEnemiesHaveAIBehavior(
        EntityManager entityManager,
        PartyState partyState)
    {
      for (int i = 0; i < partyState.characters.Length; i++) {
        var character = partyState.characters[i];

        // Only process enemies
        if (!character.isEnemy)
          continue;

        // Add AIBehavior if missing
        if (!entityManager.HasComponent<AIBehavior>(character.entity)) {
          // Assign default behavior (in real game, this would be data-driven)
          var behavior = AIBehavior.CreateBalanced();
          entityManager.AddComponentData(character.entity, behavior);

          UnityEngine.Debug.Log($"Added AIBehavior to enemy: {character.name}");
        }
      }
    }

    /// <summary>
    /// Complete AI initialization - call this once at battle start.
    /// Sets up thinking state singleton and ensures all enemies have AI.
    /// </summary>
    public static void InitializeAIForBattle(
        EntityManager entityManager,
        PartyState partyState)
    {
      EnsureAIThinkingStateExists(entityManager);
      EnsureEnemiesHaveAIBehavior(entityManager, partyState);

      UnityEngine.Debug.Log("AI initialization complete");
    }
  }
}