using Unity.Entities;
using Unity.Mathematics;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Handles damage application and health updates
  /// </summary>
  [ReducerSystem]
  public partial class DamageReducer : ReducerSystem<PartyState, AttackAction>
  {
    protected override void ReduceState(ref PartyState state, AttackAction action)
    {
      // Find target character in party state
      for (int i = 0; i < state.characters.Length; i++) {
        if (state.characters[i].entity == action.targetEntity) {
          var character = state.characters[i];

          // Apply damage
          int finalDamage = action.baseDamage;
          if (action.isCritical)
            finalDamage *= 2;
          if (character.status.HasFlag(CharacterStatus.Defending))
            finalDamage /= 2;

          character.currentHealth = math.max(0, character.currentHealth - finalDamage);

          // Update alive status
          if (character.currentHealth <= 0 && character.isAlive) {
            character.isAlive = false;

            if (character.isEnemy)
              state.aliveEnemyCount--;
            else
              state.aliveCount--;
          }

          // Clear defending status after being attacked
          character.status &= ~CharacterStatus.Defending;

          // Update the character back in the array
          state.characters[i] = character;
          break;
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
    protected override void ReduceState(ref PartyState state, SelectActionTypeAction action)
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