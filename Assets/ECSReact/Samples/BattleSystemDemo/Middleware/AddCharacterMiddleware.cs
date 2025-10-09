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
    public bool Process(
      ref AddCharacterAction action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter dispatcher,
      int sortKey)
    {
      // Create the character entity
      var entityManager = systemState.EntityManager;
      var newEntity = entityManager.CreateEntity();
      UnityEngine.Debug.Log("adding character middleware");
      // Dispatch enriched internal action with the created entity
      dispatcher.DispatchAction(sortKey,
        new CharacterCreatedAction
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