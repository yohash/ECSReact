using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Action dispatched by middleware to log battle events
  /// </summary>
  public struct BattleLogAction : IGameAction
  {
    public LogType logType;
    public FixedString128Bytes message;
    public Entity sourceEntity;
    public Entity targetEntity;
    public int numericValue; // For damage, healing, etc.
    public float timestamp;
  }

  public enum LogType
  {
    Action,
    Damage,
    Healing,
    StatusEffect,
    TurnChange,
    Victory,
    Defeat,
    System
  }

  // Action to clear the log
  public struct ClearBattleLogAction : IGameAction { }
}