using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Static utility for deterministic random number generation in AI decisions.
  /// 
  /// BURST + PARALLEL JOB COMPATIBLE:
  /// - All methods are [BurstCompile] compatible
  /// - Thread-safe (no shared state)
  /// - Can be used in IJobEntity and parallel jobs
  /// - Aggressive inlining for performance
  /// 
  /// USAGE PATTERN:
  /// Create a fresh RNG for each decision - don't store state!
  /// 
  /// Example in a reducer:
  /// var rng = AIRandomUtility.CreateForDecision(enemyEntity, turnCount);
  /// float roll = rng.NextFloat();
  /// bool useSkill = rng.NextBool(behavior.skillUseChance);
  /// 
  /// Example in a Burst job:
  /// [BurstCompile]
  /// public void Execute(Entity entity, ref AIBehavior behavior) {
  ///     var rng = AIRandomUtility.CreateForDecision(entity, turnCount);
  ///     // Make decisions...
  /// }
  /// 
  /// Same entity + same turn = same random sequence (deterministic)
  /// Different turn = different sequence (varied behavior)
  /// </summary>
  [BurstCompile]
  public static class DeterministicRandom
  {
    // Prime numbers for hash mixing - constants are Burst-compatible
    private const uint ENTITY_INDEX_PRIME = 7919;
    private const uint ENTITY_VERSION_PRIME = 6871;
    private const uint TURN_COUNT_PRIME = 4561;
    private const uint BASE_SEED_PRIME = 2503;
    private const uint CONTEXT_PRIME = 1009;

    /// <summary>
    /// Create a deterministic RNG for AI decision-making.
    /// Same entity + same turn = same RNG sequence.
    /// 
    /// This is the primary method to use in AI decision logic.
    /// Thread-safe and Burst-compatible.
    /// </summary>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Random CreateForDecision(int entityIndex, int entityVersion, int turnCount)
    {
      uint seed = CalculateSeed(entityIndex, entityVersion, turnCount);
      return Random.CreateFromIndex(seed);
    }

    /// <summary>
    /// Create a deterministic RNG for a specific entity only.
    /// Same entity = same RNG sequence (doesn't vary by turn).
    /// 
    /// Use this for initialization or when turn count isn't relevant.
    /// Thread-safe and Burst-compatible.
    /// </summary>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Random CreateForEntity(int entityIndex, int entityVersion)
    {
      uint seed = (uint)entityIndex * ENTITY_INDEX_PRIME +
                  (uint)entityVersion * ENTITY_VERSION_PRIME +
                  BASE_SEED_PRIME;
      return Random.CreateFromIndex(seed);
    }

    /// <summary>
    /// Create a deterministic RNG with custom additional context.
    /// Useful for sub-decisions within a turn (target selection, skill choice, etc.)
    /// Thread-safe and Burst-compatible.
    /// </summary>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Random CreateForDecisionWithContext(
        int entityIndex,
        int entityVersion,
        int turnCount,
        int contextId)
    {
      uint seed = CalculateSeed(entityIndex, entityVersion, turnCount) + (uint)contextId * CONTEXT_PRIME;
      return Random.CreateFromIndex(seed);
    }

    /// <summary>
    /// Core seed calculation from entity and turn count.
    /// Uses prime number mixing for good distribution.
    /// Inlined for maximum performance in hot paths.
    /// </summary>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CalculateSeed(int entityIndex, int entityVersion, int turnCount)
    {
      return (uint)entityIndex * ENTITY_INDEX_PRIME +
             (uint)entityVersion * ENTITY_VERSION_PRIME +
             (uint)turnCount * TURN_COUNT_PRIME +
             BASE_SEED_PRIME;
    }
  }

  /// <summary>
  /// Extension methods for Unity.Mathematics.Random to make AI code cleaner.
  /// All methods are Burst-compatible and can be used in jobs.
  /// 
  /// Separated from AIRandomUtility class for better Burst compatibility.
  /// </summary>
  [BurstCompile]
  public static class RandomExtensions
  {
    /// <summary>
    /// Get random bool with given probability of being true.
    /// Burst-compatible and thread-safe.
    /// </summary>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NextBool(this ref Random rng, float probability)
    {
      return rng.NextFloat() < probability;
    }

    /// <summary>
    /// Select random element from FixedList64Bytes.
    /// Burst-compatible and thread-safe.
    /// </summary>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NextElement<T>(this ref Random rng, FixedList64Bytes<T> list)
        where T : unmanaged
    {
      if (list.Length == 0)
        return default;

      int index = rng.NextInt(0, list.Length);
      return list[index];
    }

    /// <summary>
    /// Select random element from FixedList128Bytes.
    /// Burst-compatible and thread-safe.
    /// </summary>
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NextElement<T>(this ref Random rng, FixedList128Bytes<T> list)
        where T : unmanaged
    {
      if (list.Length == 0)
        return default;

      int index = rng.NextInt(0, list.Length);
      return list[index];
    }

    /// <summary>
    /// Get a weighted random selection from 0 to count-1.
    /// Higher weight = more likely to be selected.
    /// Burst-compatible and thread-safe.
    /// 
    /// Note: Uses managed array for weights, so not Burst-compatible in current form.
    /// For Burst jobs, pass weights as NativeArray instead.
    /// </summary>
    public static int NextWeightedIndex(this ref Random rng, float[] weights, int count)
    {
      if (count <= 0)
        return 0;
      if (count == 1)
        return 0;

      // Calculate total weight
      float totalWeight = 0f;
      for (int i = 0; i < count; i++) {
        totalWeight += weights[i];
      }

      if (totalWeight <= 0f)
        return rng.NextInt(0, count); // Fallback to uniform if weights are invalid

      // Random value in [0, totalWeight)
      float randomValue = rng.NextFloat() * totalWeight;

      // Find which weight bucket it falls into
      float accumulatedWeight = 0f;
      for (int i = 0; i < count; i++) {
        accumulatedWeight += weights[i];
        if (randomValue < accumulatedWeight)
          return i;
      }

      return count - 1; // Should rarely hit this, but handles edge cases
    }
  }
}