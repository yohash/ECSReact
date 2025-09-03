using Unity.Entities;
using Unity.Mathematics;
using ECSReact.Core;
using Unity.Burst;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Handles adding new characters to the party state
  /// </summary>
  [ReducerSystem]
  public partial class AddCharacterReducer : ReducerSystem<PartyState, AddCharacterAction>
  {
    public override void ReduceState(ref PartyState state, AddCharacterAction action)
    {
      // Create new entity for the character
      var newEntity = EntityManager.CreateEntity();

      // Create character data with full health/mana
      var newCharacter = new CharacterData
      {
        entity = newEntity,
        name = action.name,
        maxHealth = action.maxHealth,
        currentHealth = action.maxHealth, // Start at full health
        maxMana = action.maxMana,
        currentMana = action.maxMana,     // Start at full mana
        isEnemy = action.isEnemy,
        isAlive = true,                   // New characters start alive
        status = action.initialStatus
      };

      // Add to characters array
      state.characters.Add(newCharacter);

      // Update party counters
      if (action.isEnemy) {
        state.enemyCount++;
        state.aliveEnemyCount++; // New enemies start alive
      } else {
        state.activePartySize++;
        state.aliveCount++; // New party members start alive
      }
    }
  }


  /// <summary>
  /// HIGH-PERFORMANCE damage application and health updates.
  /// This is a hot path that runs for every attack in battle - perfect for Burst!
  /// Handles damage application and health updates
  /// </summary>
  [ReducerSystem]
  public partial class DamageReducer : BurstReducerSystem<PartyState, AttackAction, DamageReducer.Logic>
  {
    [BurstCompile]
    public struct Logic : IBurstReducer<PartyState, AttackAction>
    {
      public void Execute(ref PartyState state, in AttackAction action)
      {
        // Find target character in party state
        for (int i = 0; i < state.characters.Length; i++) {
          if (state.characters[i].entity == action.targetEntity) {
            var character = state.characters[i];

            // BURST-OPTIMIZED damage calculation
            // These operations are now SIMD-optimized
            int finalDamage = action.baseDamage;

            // Burst compiles this to efficient branch-free code
            finalDamage = action.isCritical ? finalDamage * 2 : finalDamage;

            // Check defending status (bit operations are fast in Burst)
            bool isDefending = (character.status & CharacterStatus.Defending) != 0;
            finalDamage = isDefending ? finalDamage / 2 : finalDamage;

            // Apply damage with math.max (SIMD optimized)
            character.currentHealth = math.max(0, character.currentHealth - finalDamage);

            // Update alive status
            if (character.currentHealth <= 0 && character.isAlive) {
              character.isAlive = false;

              // Update counters (branch-predicted by CPU)
              if (character.isEnemy)
                state.aliveEnemyCount--;
              else
                state.aliveCount--;
            }

            // Clear defending status after being attacked (bit operation)
            character.status &= ~CharacterStatus.Defending;

            // Update the character back in the array
            state.characters[i] = character;
            break;
          }
        }
      }
    }
  }

  /// <summary>
  /// Handles defend action - sets defending status
  /// </summary>
  [ReducerSystem]
  public partial class DefendReducer : ReducerSystem<PartyState, SelectActionTypeAction>
  {
    public override void ReduceState(ref PartyState state, SelectActionTypeAction action)
    {
      if (action.actionType != ActionType.Defend)
        return;

      // Find acting character and set defending status
      for (int i = 0; i < state.characters.Length; i++) {
        if (state.characters[i].entity == action.actingCharacter) {
          var character = state.characters[i];
          character.status |= CharacterStatus.Defending;
          state.characters[i] = character;
          break;
        }
      }
    }
  }
}