using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // MIDDLEWARE - Creates entity and dispatches enriched action
  // ============================================================================

  /// <summary>
  /// Middleware that handles entity creation for new characters.
  /// Dispatches CharacterCreatedAction so all normalized reducers can respond.
  /// </summary>
  [Middleware(DisableBurst = true, Order = 10)]
  public struct AddCharacterMiddleware : IMiddleware<AddCharacterAction>
  {
    public void OnCreate(ref SystemState state) { }

    public bool Process(
      in AddCharacterAction action,
      ref SystemState systemState,
      ref ActionWriter dispatch)
    {
      // Create the character entity
      // Raw ECB work goes through dispatch.Ecb (this middleware's own lane, the frame lane here),
      // the same buffer CharacterCreatedAction is dispatched into, so the deferred entity resolves.
      var newEntity = dispatch.Ecb.CreateEntity(dispatch.NextSortKey());

      // Dispatch enriched internal action with the created entity
      dispatch.Dispatch(new CharacterCreatedAction
        {
          entity = newEntity,
          name = action.name,
          maxHealth = action.maxHealth,
          maxMana = action.maxMana,
          isEnemy = action.isEnemy,
          initialStatus = action.initialStatus
        });

      // Return false to prevent original action from reaching reducers
      // (only the enriched CharacterCreatedAction should be processed)
      return false;
    }
  }
}