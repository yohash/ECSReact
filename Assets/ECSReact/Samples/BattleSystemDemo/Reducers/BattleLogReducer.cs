using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Processes battle log actions and updates log state
  /// </summary>
  [ReducerUpdateGroup]
  public partial class BattleLogReducer : ReducerSystem<BattleLogState, BattleLogAction>
  {
    public override void ReduceState(ref BattleLogState state, BattleLogAction action)
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
  [ReducerUpdateGroup]
  public partial class ClearBattleLogReducer : ReducerSystem<BattleLogState, ClearBattleLogAction>
  {
    public override void ReduceState(ref BattleLogState state, ClearBattleLogAction action)
    {
      state.entries.Clear();
      // Keep total count for statistics
    }
  }
}