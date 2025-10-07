using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Middleware that intercepts all battle actions and creates log entries.
  /// Demonstrates how middleware can handle cross-cutting concerns without modifying reducers.
  /// </summary>
  [Middleware(DisableBurst = true)]
  public struct AttackLoggingMiddleware : IMiddleware<AttackAction>
  {
    public bool Process(
      ref AttackAction action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter dispatcher,
      int sortKey
    )
    {
      // Get character names for the log message
      var attackerName = GetCharacterName(action.attackerEntity);
      var targetName = GetCharacterName(action.targetEntity);

      // Create descriptive log message
      FixedString128Bytes message;
      if (action.isCritical) {
        message = $"{attackerName} lands a CRITICAL hit on {targetName}!";
      } else {
        message = $"{attackerName} attacks {targetName}";
      }

      // Dispatch log action
      ECSActionDispatcher.Dispatch(new BattleLogAction
      {
        logType = LogType.Action,
        message = message,
        sourceEntity = action.attackerEntity,
        targetEntity = action.targetEntity,
        numericValue = action.baseDamage,
        timestamp = (float)systemState.WorldUnmanaged.Time.ElapsedTime
      });


      // Also log the damage separately
      ECSActionDispatcher.Dispatch(new BattleLogAction
      {
        logType = LogType.Damage,
        message = $"{targetName} takes {action.baseDamage} damage!",
        sourceEntity = action.attackerEntity,
        targetEntity = action.targetEntity,
        numericValue = action.baseDamage,
        timestamp = (float)systemState.WorldUnmanaged.Time.ElapsedTime
      });

      return true; // Continue processing the action
    }

    private FixedString32Bytes GetCharacterName(Entity entity)
    {
      // In real implementation, would query PartyState
      // For demo, return placeholder
      return new FixedString32Bytes($"Entity_{entity.Index}");
    }
  }

  /// <summary>
  /// Logs turn changes
  /// </summary>
  [Middleware(DisableBurst = true)]
  public struct TurnChangeLoggingMiddleware : IMiddleware<NextTurnAction>
  {
    public bool Process(
      ref NextTurnAction action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter dispatcher,
      int sortKey
    )
    {
      // Get current battle state to determine whose turn it is
      if (systemState.TryGetSingleton<BattleState>(out var battleState)) {
        var nextCharacter = GetNextCharacterName(battleState, ref systemState);

        ECSActionDispatcher.Dispatch(new BattleLogAction
        {
          logType = LogType.TurnChange,
          message = $"=== {nextCharacter}'s turn ===",
          sourceEntity = Entity.Null,
          targetEntity = Entity.Null,
          numericValue = battleState.turnCount + 1,
          timestamp = (float)systemState.WorldUnmanaged.Time.ElapsedTime
        });
      }

      return true;
    }

    private FixedString32Bytes GetNextCharacterName(BattleState battleState, ref SystemState systemState)
    {
      // Get next character in turn order
      int nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      if (nextIndex < battleState.turnOrder.Length) {
        var entity = battleState.turnOrder[nextIndex];
        return GetCharacterName(entity, ref systemState);
      }
      return "Unknown";
    }

    private FixedString32Bytes GetCharacterName(Entity entity, ref SystemState systemState)
    {
      if (systemState.TryGetSingleton<PartyState>(out var partyState)) {
        for (int i = 0; i < partyState.characters.Length; i++) {
          if (partyState.characters[i].entity == entity)
            return partyState.characters[i].name;
        }
      }
      return new FixedString32Bytes($"Entity_{entity.Index}");
    }
  }

  /// <summary>
  /// Logs action selections for UI feedback
  /// </summary>
  [Middleware(DisableBurst = true)]
  public struct ActionSelectionLoggingMiddleware : IMiddleware<SelectActionTypeAction>
  {
    public bool Process(
      ref SelectActionTypeAction action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter dispatcher,
      int sortKey
    )
    {
      if (action.actionType == ActionType.None)
        return true;

      var characterName = GetCharacterName(action.actingCharacter, ref systemState);
      FixedString128Bytes message = action.actionType switch
      {
        ActionType.Attack => $"{characterName} prepares to attack!",
        ActionType.Skill => $"{characterName} opens the skill menu...",
        ActionType.Item => $"{characterName} reaches for an item...",
        ActionType.Defend => $"{characterName} takes a defensive stance!",
        ActionType.Run => $"{characterName} attempts to flee!",
        _ => $"{characterName} selects {action.actionType}"
      };

      ECSActionDispatcher.Dispatch(new BattleLogAction
      {
        logType = LogType.Action,
        message = message,
        sourceEntity = action.actingCharacter,
        targetEntity = Entity.Null,
        numericValue = 0,
        timestamp = (float)systemState.WorldUnmanaged.Time.ElapsedTime
      });

      return true;
    }

    private FixedString32Bytes GetCharacterName(Entity entity, ref SystemState systemState)
    {
      if (systemState.TryGetSingleton<PartyState>(out var partyState)) {
        for (int i = 0; i < partyState.characters.Length; i++) {
          if (partyState.characters[i].entity == entity)
            return partyState.characters[i].name;
        }
      }
      return "Unknown";
    }
  }
}