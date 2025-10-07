using ECSReact.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEngine;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Middleware that handles async file I/O for battle saves - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState dependency
  /// - Uses CharacterRosterState to get character list
  /// - Uses CharacterHealthState, CharacterManaState, CharacterStatusState, CharacterIdentityState
  /// - Builds SerializableCharacterData from O(1) HashMap lookups
  /// </summary>
  [Middleware(DisableBurst = true)]
  public struct SaveBattleMiddleware : IMiddleware<SaveBattleAction>
  {
    public bool Process(
      ref SaveBattleAction action,
      ref SystemState systemState,
      EntityCommandBuffer.ParallelWriter dispatcher,
      int sortKey
    )
    {
      // Generate filename if not provided
      string fileName = action.fileName.IsEmpty
          ? GenerateFileName()
          : action.fileName.ToString();

      // Immediately dispatch "save started" action
      ECSActionDispatcher.Dispatch(new SaveBattleStartedAction
      {
        fileName = fileName,
        timestamp = (float)systemState.GetElapsedTime()
      });

      // Start async save operation (fire-and-forget)
      var elapsedTime = (float)systemState.GetElapsedTime();

      // Collect current game state from normalized states
      var saveData = CollectSaveData(ref systemState);

      _ = PerformSaveAsync(fileName, action.format, saveData, elapsedTime);

      return true; // Continue processing the action
    }

    private async Task PerformSaveAsync(
      string fileName,
      SaveFormat format,
      BattleSaveData saveData,
      float elapsedTime)
    {
      string saveDirectory = Path.Combine(Application.persistentDataPath, "BattleSaves");

      // Ensure save directory exists
      if (!Directory.Exists(saveDirectory)) {
        Directory.CreateDirectory(saveDirectory);
      }

      var startTime = DateTime.Now;
      string filePath = Path.Combine(saveDirectory, fileName);

      try {
        // Simulate some processing time for demo purposes
        await Task.Delay(UnityEngine.Random.Range(500, 1500));

        // Serialize and write to file
        await WriteToFileAsync(filePath, saveData, format);

        // Get file info
        var fileInfo = new FileInfo(filePath);
        var duration = DateTime.Now - startTime;

        // Dispatch success action
        ECSActionDispatcher.Dispatch(new SaveBattleCompletedAction
        {
          fileName = fileName,
          filePath = filePath,
          fileSizeBytes = fileInfo.Length,
          duration = (float)duration.TotalSeconds
        });

        Debug.Log($"Save completed: {fileName} ({fileInfo.Length} bytes)");
      } catch (Exception ex) {
        // Dispatch failure action
        ECSActionDispatcher.Dispatch(new SaveBattleFailedAction
        {
          fileName = fileName,
          errorMessage = ex.Message,
          errorType = SaveErrorType.FileSystemError
        });

        // Also log to battle log
        var worldTime = Time.realtimeSinceStartup;
        ECSActionDispatcher.Dispatch(new BattleLogAction
        {
          logType = LogType.System,
          message = $"Save failed: {ex.Message}",
          sourceEntity = Entity.Null,
          targetEntity = Entity.Null,
          numericValue = 0,
          timestamp = (float)worldTime
        });

        Debug.LogError($"Save operation failed: {ex}");
      }
    }

    // ========================================================================
    // COLLECT SAVE DATA - NORMALIZED VERSION
    // ========================================================================

    /// <summary>
    /// NEW: Collects save data from normalized states.
    /// Fetches character data via O(1) HashMap lookups instead of array iteration.
    /// </summary>
    private BattleSaveData CollectSaveData(ref SystemState systemState)
    {
      var saveData = new BattleSaveData
      {
        metadata = new SaveMetadata
        {
          saveVersion = "1.0",
          gameTime = (float)systemState.GetElapsedTime(),
          realTime = DateTime.Now,
          sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        }
      };

      // ====================================================================
      // Collect BattleState (unchanged)
      // ====================================================================
      if (systemState.TryGetSingleton<BattleState>(out var battleState)) {
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

      // ====================================================================
      // Collect Character Data - NEW: From Normalized States
      // ====================================================================
      saveData.partyState = CollectCharacterDataFromNormalizedStates(ref systemState);

      // ====================================================================
      // Collect UI State (unchanged)
      // ====================================================================
      if (systemState.TryGetSingleton<UIBattleState>(out var uiState)) {
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

    /// <summary>
    /// NEW: Collects character data from normalized states using O(1) lookups.
    /// OLD: Iterated through PartyState.characters array.
    /// </summary>
    private SerializablePartyState CollectCharacterDataFromNormalizedStates(ref SystemState systemState)
    {
      // Get roster state for character list
      if (!systemState.TryGetSingleton<CharacterRosterState>(out var rosterState)) {
        Debug.LogWarning("CharacterRosterState not found - cannot save character data");
        return new SerializablePartyState { characters = new SerializableCharacterData[0] };
      }

      // Get all normalized state singletons
      bool hasIdentity = systemState.TryGetSingleton<CharacterIdentityState>(out var identityState);
      bool hasHealth = systemState.TryGetSingleton<CharacterHealthState>(out var healthState);
      bool hasMana = systemState.TryGetSingleton<CharacterManaState>(out var manaState);
      bool hasStatus = systemState.TryGetSingleton<CharacterStatusState>(out var statusState);

      // Calculate total character count
      int totalCharacters = rosterState.allCharacters.Length;
      var characters = new SerializableCharacterData[totalCharacters];

      // Iterate all characters and build serializable data from lookups
      for (int i = 0; i < totalCharacters; i++) {
        Entity entity = rosterState.allCharacters[i];

        if (entity == Entity.Null)
          continue;

        // Build SerializableCharacterData from multiple O(1) lookups
        characters[i] = new SerializableCharacterData();

        // Lookup name (O(1))
        if (hasIdentity && identityState.names.IsCreated &&
            identityState.names.TryGetValue(entity, out var name)) {
          characters[i].name = name.ToString();
        }

        // Lookup health (O(1))
        if (hasHealth && healthState.health.IsCreated &&
            healthState.health.TryGetValue(entity, out var health)) {
          characters[i].currentHealth = health.current;
          characters[i].maxHealth = health.max;
        }

        // Lookup mana (O(1))
        if (hasMana && manaState.mana.IsCreated &&
            manaState.mana.TryGetValue(entity, out var mana)) {
          characters[i].currentMana = mana.current;
          characters[i].maxMana = mana.max;
        }

        // Lookup status (O(1))
        if (hasStatus && statusState.statuses.IsCreated &&
            statusState.statuses.TryGetValue(entity, out var status)) {
          characters[i].statusEffects = status;
        }
      }

      return new SerializablePartyState { characters = characters };
    }

    // ========================================================================
    // FILE I/O HELPERS (unchanged)
    // ========================================================================

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