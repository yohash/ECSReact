using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Reducer for handling save state transitions
  /// </summary>
  [ReducerSystem]
  public partial class SaveStateReducer : ReducerSystem<SaveState, SaveBattleStartedAction>
  {
    public override void ReduceState(ref SaveState state, SaveBattleStartedAction action)
    {
      state.currentStatus = SaveStatus.InProgress;
      state.currentFileName = action.fileName;
      state.saveStartTime = action.timestamp;
      state.totalSavesAttempted++;
    }
  }

  [ReducerSystem]
  public partial class SaveCompletedReducer : ReducerSystem<SaveState, SaveBattleCompletedAction>
  {
    public override void ReduceState(ref SaveState state, SaveBattleCompletedAction action)
    {
      state.currentStatus = SaveStatus.Completed;
      state.lastSaveCompletedTime = (float)SystemAPI.Time.ElapsedTime;
      state.totalSavesCompleted++;

      // Clear any previous error
      state.lastErrorMessage = "";
    }
  }

  [ReducerSystem]
  public partial class SaveFailedReducer : ReducerSystem<SaveState, SaveBattleFailedAction>
  {
    public override void ReduceState(ref SaveState state, SaveBattleFailedAction action)
    {
      state.currentStatus = SaveStatus.Failed;
      state.lastErrorMessage = action.errorMessage;
    }
  }

  /// <summary>
  /// Action to clear save error state
  /// </summary>
  public struct ClearSaveErrorAction : IGameAction { }

  [ReducerSystem]
  public partial class ClearSaveErrorReducer : ReducerSystem<SaveState, ClearSaveErrorAction>
  {
    public override void ReduceState(ref SaveState state, ClearSaveErrorAction action)
    {
      if (state.currentStatus == SaveStatus.Failed) {
        state.currentStatus = SaveStatus.Idle;
        state.lastErrorMessage = "";
      }
    }
  }
}