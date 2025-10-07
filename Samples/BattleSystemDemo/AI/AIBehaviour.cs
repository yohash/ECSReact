using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Component that defines how an enemy behaves in combat.
  /// Attach to enemy entities to control their AI decision making.
  /// </summary>
  public struct AIBehavior : IComponentData
  {
    public AIStrategy strategy;          // Overall behavior pattern
    public float aggressionLevel;        // 0.0 (defensive) to 1.0 (aggressive)
    public float skillUseChance;         // 0.0 to 1.0 probability of using skills
    public float defendThreshold;        // Health % below which to consider defending
    public float thinkingDuration;       // How long to "think" before acting (seconds)

    // Target preference weights
    public float targetLowestHealthWeight;    // Preference for weakest enemies
    public float targetHighestThreatWeight;   // Preference for dangerous enemies
    public float targetRandomWeight;          // Randomness in targeting

    // Create default aggressive behavior
    public static AIBehavior CreateAggressive()
    {
      return new AIBehavior
      {
        strategy = AIStrategy.Aggressive,
        aggressionLevel = 0.8f,
        skillUseChance = 0.3f,
        defendThreshold = 0.2f,
        thinkingDuration = 0.8f,
        targetLowestHealthWeight = 0.7f,
        targetHighestThreatWeight = 0.2f,
        targetRandomWeight = 0.1f
      };
    }

    // Create default defensive behavior
    public static AIBehavior CreateDefensive()
    {
      return new AIBehavior
      {
        strategy = AIStrategy.Defensive,
        aggressionLevel = 0.3f,
        skillUseChance = 0.2f,
        defendThreshold = 0.5f,
        thinkingDuration = 1.2f,
        targetLowestHealthWeight = 0.3f,
        targetHighestThreatWeight = 0.5f,
        targetRandomWeight = 0.2f
      };
    }

    // Create default balanced behavior
    public static AIBehavior CreateBalanced()
    {
      return new AIBehavior
      {
        strategy = AIStrategy.Balanced,
        aggressionLevel = 0.5f,
        skillUseChance = 0.4f,
        defendThreshold = 0.3f,
        thinkingDuration = 1.0f,
        targetLowestHealthWeight = 0.4f,
        targetHighestThreatWeight = 0.4f,
        targetRandomWeight = 0.2f
      };
    }

    // Create simple random behavior for basic enemies
    public static AIBehavior CreateRandom()
    {
      return new AIBehavior
      {
        strategy = AIStrategy.Random,
        aggressionLevel = 0.5f,
        skillUseChance = 0.1f,
        defendThreshold = 0.15f,
        thinkingDuration = 0.5f,
        targetLowestHealthWeight = 0.0f,
        targetHighestThreatWeight = 0.0f,
        targetRandomWeight = 1.0f
      };
    }
  }

  /// <summary>
  /// Defines the overall AI strategy pattern.
  /// </summary>
  public enum AIStrategy
  {
    Random,        // Completely random actions
    Aggressive,    // Prioritize maximum damage
    Defensive,     // Prioritize survival
    Balanced,      // Mix of offense and defense
    Tactical,      // Smart target selection based on game state
    Support,       // Prioritize healing/buffing allies
    Boss           // Special boss patterns with phases
  }

  /// <summary>
  /// Runtime context for AI decision making.
  /// Created each time an AI needs to make a decision.
  /// </summary>
  public struct AIDecisionContext
  {
    // Self assessment
    public Entity selfEntity;
    public float healthPercent;
    public int currentHealth;
    public int maxHealth;
    public int currentMana;
    public CharacterStatus statusEffects;

    // Battle assessment
    public int aliveAllies;
    public int aliveEnemies;
    public bool isOutnumbered;
    public bool isLastAlly;

    // Cached target information
    public FixedList64Bytes<AITargetInfo> potentialTargets;

    // Decision history (for pattern avoidance)
    public ActionType lastAction;
    public Entity lastTarget;
    public int turnsDefending;

    // Helper methods
    public bool IsHealthCritical => healthPercent < 0.3f;
    public bool ShouldConsiderDefending(float threshold) => healthPercent < threshold;
    public bool HasManaForSkills => currentMana >= 5; // Minimum mana for basic skills
    public bool HasStatusEffect(CharacterStatus status) => statusEffects.HasFlag(status);
  }

  /// <summary>
  /// Information about a potential target for AI consideration.
  /// </summary>
  public struct AITargetInfo
  {
    public Entity entity;
    public float healthPercent;
    public int currentHealth;
    public int threatLevel;       // Based on damage dealt recently
    public bool isDefending;
    public bool hasDebuffs;
    public float distance;         // For future spatial AI
    public float targetScore;      // Calculated desirability as target
  }
}