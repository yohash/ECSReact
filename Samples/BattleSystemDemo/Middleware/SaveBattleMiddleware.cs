using ECSReact.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Middleware that handles async file I/O for battle saves.
  /// Demonstrates fire-and-forget async operations with proper error handling.
  /// </summary>
  [UpdateInGroup(typeof(MiddlewareSystemGroup))]
  [UpdateBefore(typeof(SimulationSystemGroup))]
  public partial class SaveBattleMiddleware : MiddlewareSystem<SaveBattleAction>
  {
    private string saveDirectory;

    protected override void OnCreate()
    {
      base.OnCreate();
      saveDirectory = Path.Combine(Application.persistentDataPath, "BattleSaves");

      // Ensure save directory exists
      if (!Directory.Exists(saveDirectory)) {
        Directory.CreateDirectory(saveDirectory);
      }
    }

    protected override void ProcessAction(SaveBattleAction action, Entity actionEntity)
    {
      // Generate filename if not provided
      string fileName = action.fileName.IsEmpty
          ? GenerateFileName()
          : action.fileName.ToString();

      // Immediately dispatch "save started" action
      DispatchAction(new SaveBattleStartedAction
      {
        fileName = fileName,
        timestamp = (float)SystemAPI.Time.ElapsedTime
      });

      // Start async save operation (fire-and-forget)
      _ = PerformSaveAsync(fileName, action.format);
    }

    private async Task PerformSaveAsync(string fileName, SaveFormat format)
    {
      float startTime = (float)SystemAPI.Time.ElapsedTime;
      string filePath = Path.Combine(saveDirectory, fileName);

      try {
        // Collect current game state
        var saveData = CollectSaveData();

        // Simulate some processing time for demo purposes
        await Task.Delay(UnityEngine.Random.Range(500, 1500));

        // Serialize and write to file
        await WriteToFileAsync(filePath, saveData, format);

        // Get file info
        var fileInfo = new FileInfo(filePath);
        float duration = (float)SystemAPI.Time.ElapsedTime - startTime;

        // Dispatch success action
        DispatchAction(new SaveBattleCompletedAction
        {
          fileName = fileName,
          filePath = filePath,
          fileSizeBytes = fileInfo.Length,
          duration = duration
        });

        // Log the successful save
        DispatchAction(new BattleLogAction
        {
          logType = LogType.System,
          message = $"Battle saved to {fileName} ({fileInfo.Length} bytes)",
          sourceEntity = Entity.Null,
          targetEntity = Entity.Null,
          numericValue = (int)fileInfo.Length,
          timestamp = (float)SystemAPI.Time.ElapsedTime
        });

      } catch (System.Exception ex) {
        // Determine error type
        SaveErrorType errorType = ex switch
        {
          UnauthorizedAccessException => SaveErrorType.PermissionDenied,
          DirectoryNotFoundException => SaveErrorType.FileSystemError,
          IOException => SaveErrorType.InsufficientSpace,
          _ => SaveErrorType.Unknown
        };

        // Dispatch failure action
        DispatchAction(new SaveBattleFailedAction
        {
          fileName = fileName,
          errorMessage = ex.Message,
          errorType = errorType
        });

        // Log the error
        DispatchAction(new BattleLogAction
        {
          logType = LogType.System,
          message = $"Save failed: {ex.Message}",
          sourceEntity = Entity.Null,
          targetEntity = Entity.Null,
          numericValue = 0,
          timestamp = (float)SystemAPI.Time.ElapsedTime
        });

        Debug.LogError($"Save operation failed: {ex}");
      }
    }

    private BattleSaveData CollectSaveData()
    {
      var saveData = new BattleSaveData
      {
        metadata = new SaveMetadata
        {
          saveVersion = "1.0",
          gameTime = (float)SystemAPI.Time.ElapsedTime,
          realTime = (float)SystemAPI.Time.ElapsedTime,
          sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        }
      };

      // Collect battle state
      if (SystemAPI.TryGetSingleton<BattleState>(out var battleState)) {
        saveData.battleState = new SerializableBattleState
        {
          currentPhase = battleState.currentPhase,
          activeCharacterIndex = battleState.activeCharacterIndex,
          turnCount = battleState.turnCount,
          battleActive = battleState.battleActive,
          turnTimer = battleState.turnTimer
        };
        saveData.metadata.turnCount = battleState.turnCount;
      }

      // Collect party state
      if (SystemAPI.TryGetSingleton<PartyState>(out var partyState)) {
        var characters = new SerializableCharacterData[partyState.characters.Length];
        for (int i = 0; i < characters.Length; i++) {
          var character = partyState.characters[i];
          characters[i] = new SerializableCharacterData
          {
            name = character.name.ToString(),
            currentHealth = character.currentHealth,
            maxHealth = character.maxHealth,
            currentMana = character.currentMana,
            maxMana = character.maxMana,
            statusEffects = character.status
          };
        }
        saveData.partyState = new SerializablePartyState { characters = characters };
      }

      // Collect UI state
      if (SystemAPI.TryGetSingleton<UIBattleState>(out var uiState)) {
        saveData.uiState = new SerializableUIBattleState
        {
          activePanel = uiState.activePanel,
          selectedAction = uiState.selectedAction,
          selectedSkillIndex = uiState.selectedSkillId,
          selectedItemIndex = uiState.selectedItemId
        };
      }

      return saveData;
    }

    private async Task WriteToFileAsync(string filePath, BattleSaveData saveData, SaveFormat format)
    {
      string content = format switch
      {
        SaveFormat.JSON => JsonUtility.ToJson(saveData, true),
        SaveFormat.Compressed => CompressJson(JsonUtility.ToJson(saveData)),
        _ => JsonUtility.ToJson(saveData, true)
      };

      await File.WriteAllTextAsync(filePath, content);
    }

    private string CompressJson(string json)
    {
      // Simple compression demo - in real implementation, use GZip or similar
      return json.Replace(" ", "").Replace("\n", "").Replace("\t", "");
    }

    private string GenerateFileName()
    {
      string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
      return $"BattleSave_{timestamp}.json";
    }
  }
}
