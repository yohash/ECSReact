using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Middleware that intercepts all battle actions and creates log entries.
  /// Demonstrates how middleware can handle cross-cutting concerns without modifying reducers.
  /// </summary>
  [MiddlewareSystem]
  public partial class AttackLoggingMiddleware : MiddlewareSystem<AttackAction>
  {
    public override void ProcessAction(AttackAction action, Entity actionEntity)
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
      DispatchAction(new BattleLogAction
      {
        logType = LogType.Action,
        message = message,
        sourceEntity = action.attackerEntity,
        targetEntity = action.targetEntity,
        numericValue = action.baseDamage,
        timestamp = (float)SystemAPI.Time.ElapsedTime
      });

      // Also log the damage separately
      DispatchAction(new BattleLogAction
      {
        logType = LogType.Damage,
        message = $"{targetName} takes {action.baseDamage} damage!",
        sourceEntity = action.attackerEntity,
        targetEntity = action.targetEntity,
        numericValue = action.baseDamage,
        timestamp = (float)SystemAPI.Time.ElapsedTime
      });
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
  [MiddlewareSystem]
  public partial class TurnChangeLoggingMiddleware : MiddlewareSystem<NextTurnAction>
  {
    public override void ProcessAction(NextTurnAction action, Entity actionEntity)
    {
      // Get current battle state to determine whose turn it is
      if (SystemAPI.TryGetSingleton<BattleState>(out var battleState)) {
        var nextCharacter = GetNextCharacterName(battleState);

        DispatchAction(new BattleLogAction
        {
          logType = LogType.TurnChange,
          message = $"=== {nextCharacter}'s turn ===",
          sourceEntity = Entity.Null,
          targetEntity = Entity.Null,
          numericValue = battleState.turnCount + 1,
          timestamp = (float)SystemAPI.Time.ElapsedTime
        });
      }
    }

    private FixedString32Bytes GetNextCharacterName(BattleState battleState)
    {
      // Get next character in turn order
      int nextIndex = (battleState.activeCharacterIndex + 1) % battleState.turnOrder.Length;
      if (nextIndex < battleState.turnOrder.Length) {
        var entity = battleState.turnOrder[nextIndex];
        return GetCharacterName(entity);
      }
      return "Unknown";
    }

    private FixedString32Bytes GetCharacterName(Entity entity)
    {
      if (SystemAPI.TryGetSingleton<PartyState>(out var partyState)) {
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
  [MiddlewareSystem]
  public partial class ActionSelectionLoggingMiddleware : MiddlewareSystem<SelectActionTypeAction>
  {
    public override void ProcessAction(SelectActionTypeAction action, Entity actionEntity)
    {
      if (action.actionType == ActionType.None)
        return;

      var characterName = GetCharacterName(action.actingCharacter);
      FixedString128Bytes message = action.actionType switch
      {
        ActionType.Attack => $"{characterName} prepares to attack!",
        ActionType.Skill => $"{characterName} opens the skill menu...",
        ActionType.Item => $"{characterName} reaches for an item...",
        ActionType.Defend => $"{characterName} takes a defensive stance!",
        ActionType.Run => $"{characterName} attempts to flee!",
        _ => $"{characterName} selects {action.actionType}"
      };

      DispatchAction(new BattleLogAction
      {
        logType = LogType.Action,
        message = message,
        sourceEntity = action.actingCharacter,
        targetEntity = Entity.Null,
        numericValue = 0,
        timestamp = (float)SystemAPI.Time.ElapsedTime
      });
    }

    private FixedString32Bytes GetCharacterName(Entity entity)
    {
      if (SystemAPI.TryGetSingleton<PartyState>(out var partyState)) {
        for (int i = 0; i < partyState.characters.Length; i++) {
          if (partyState.characters[i].entity == entity)
            return partyState.characters[i].name;
        }
      }
      return "Unknown";
    }
  }

  /// <summary>
  /// Universal action logger for debugging - logs ALL actions
  /// </summary>
  [MiddlewareSystem]
  [UpdateAfter(typeof(AttackLoggingMiddleware))] // Run after specific loggers
  public partial class UniversalActionLogger : SystemBase
  {
    private bool debugLoggingEnabled = false; // Can be toggled in inspector

    protected override void OnUpdate()
    {
      if (!debugLoggingEnabled)
        return;

      // Get the command buffer
      var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
          .CreateCommandBuffer(World.Unmanaged);

      // Query for all entities with ActionTag using modern SystemAPI
      foreach (var (actionTag, entity) in SystemAPI.Query<RefRO<ActionTag>>()
          .WithEntityAccess()) {
        // Use reflection to get action type name
        var actionTypeName = GetActionTypeName(entity);
        if (!string.IsNullOrEmpty(actionTypeName)) {
          var logAction = new BattleLogAction
          {
            logType = LogType.System,
            message = $"[DEBUG] Action: {actionTypeName}",
            sourceEntity = Entity.Null,
            targetEntity = Entity.Null,
            numericValue = 0,
            timestamp = (float)SystemAPI.Time.ElapsedTime
          };

          var logEntity = ecb.CreateEntity();
          ecb.AddComponent(logEntity, logAction);
          ecb.AddComponent(logEntity, new ActionTag());
        }
      }
    }

    private string GetActionTypeName(Entity entity)
    {
      // In real implementation, would use reflection or type registry
      // For demo, return generic name
      return "GenericAction";
    }
  }
}