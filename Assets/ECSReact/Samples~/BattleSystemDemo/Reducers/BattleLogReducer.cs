using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Processes battle log actions and updates log state
  /// </summary>
  [Reducer]
  public struct BattleLogReducer : IReducer<BattleLogState, BattleLogAction>
  {
    public void Execute(ref BattleLogState state, in BattleLogAction action, ref SystemState systemState)
    {
      // Create new log entry
      var entry = new BattleLogEntry
      {
        logType = action.logType,
        message = action.message,
        timestamp = action.timestamp,
        damageAmount = action.numericValue
      };

      // Add to entries (with circular buffer behavior)
      if (state.entries.Length >= state.entries.Capacity) {
        // Remove oldest entry
        for (int i = 0; i < state.entries.Length - 1; i++) {
          state.entries[i] = state.entries[i + 1];
        }
        state.entries[state.entries.Length - 1] = entry;
      } else {
        state.entries.Add(entry);
      }

      state.totalEntriesLogged++;
    }
  }

  /// <summary>
  /// Clears the battle log
  /// </summary>
  [Reducer]
  public struct ClearBattleLogReducer : IReducer<BattleLogState, ClearBattleLogAction>
  {
    public void Execute(ref BattleLogState state, in ClearBattleLogAction action, ref SystemState systemState)
    {
      state.entries.Clear();
      // Keep total count for statistics
    }
  }
}