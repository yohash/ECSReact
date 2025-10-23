using System;
using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Core battle flow state - manages turns, phase, and victory conditions
  /// </summary>
  public struct BattleState : IGameState, IEquatable<BattleState>
  {
    public BattlePhase currentPhase;
    public int activeCharacterIndex;
    public FixedList128Bytes<Entity> turnOrder; // Max 16 combatants
    public float turnTimer;
    public int turnCount;
    public bool battleActive;

    public bool Equals(BattleState other)
    {
      if (currentPhase != other.currentPhase) {
        return false;
      }
      if (activeCharacterIndex != other.activeCharacterIndex) {
        return false;
      }
      if (turnTimer != other.turnTimer) {
        return false;
      }
      if (turnCount != other.turnCount) {
        return false;
      }
      if (battleActive != other.battleActive) {
        return false;
      }

      // Compare turn order
      if (turnOrder.Length != other.turnOrder.Length) {
        return false;
      }
      for (int i = 0; i < turnOrder.Length; i++) {
        if (turnOrder[i] != other.turnOrder[i]) {
          return false;
        }
      }

      return true;
    }
  }

  public enum BattlePhase
  {
    Initializing,
    PlayerSelectAction,
    PlayerSelectTarget,
    ExecutingAction,
    EnemyTurn,
    TurnTransition,
    Victory,
    Defeat
  }
}