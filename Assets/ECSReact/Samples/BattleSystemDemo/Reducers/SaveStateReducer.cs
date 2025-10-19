using Unity.Entities;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Reducer for handling save state transitions
  /// </summary>
  [Reducer]
  public struct SaveStateReducer : IReducer<SaveState, SaveBattleStartedAction>
  {
    public void Execute(ref SaveState state, in SaveBattleStartedAction action, ref SystemState systemState)
    {
      state.currentStatus = SaveStatus.InProgress;
      state.currentFileName = action.fileName;
      state.saveStartTime = action.timestamp;
      state.totalSavesAttempted++;
    }
  }

  [Reducer]
  public struct SaveCompletedReducer : IReducer<SaveState, SaveBattleCompletedAction>
  {
    public void Execute(ref SaveState state, in SaveBattleCompletedAction action, ref SystemState systemState)
    {
      state.currentStatus = SaveStatus.Completed;

      state.lastSaveCompletedTime = (float)systemState.WorldUnmanaged.Time.ElapsedTime;
      state.totalSavesCompleted++;

      // Clear any previous error
      state.lastErrorMessage = "";
    }
  }

  [Reducer]
  public struct SaveFailedReducer : IReducer<SaveState, SaveBattleFailedAction>
  {
    public void Execute(ref SaveState state, in SaveBattleFailedAction action, ref SystemState systemState)
    {
      state.currentStatus = SaveStatus.Failed;
      state.lastErrorMessage = action.errorMessage;
    }
  }

  /// <summary>
  /// Action to clear save error state
  /// </summary>
  public struct ClearSaveErrorAction : IGameAction { }

  [Reducer]
  public struct ClearSaveErrorReducer : IReducer<SaveState, ClearSaveErrorAction>
  {
    public void Execute(ref SaveState state, in ClearSaveErrorAction action, ref SystemState systemState)
    {
      if (state.currentStatus == SaveStatus.Failed) {
        state.currentStatus = SaveStatus.Idle;
        state.lastErrorMessage = "";
      }
    }
  }
}