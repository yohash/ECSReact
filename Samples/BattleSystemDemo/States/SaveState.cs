using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// State tracking save operations and their status
  /// </summary>
  public struct SaveState : IGameState, System.IEquatable<SaveState>
  {
    public SaveStatus currentStatus;
    public FixedString128Bytes currentFileName;
    public FixedString512Bytes lastErrorMessage;
    public float saveStartTime;
    public float lastSaveCompletedTime;
    public int totalSavesAttempted;
    public int totalSavesCompleted;

    public readonly bool IsSaving => currentStatus == SaveStatus.InProgress;
    public readonly bool HasError => currentStatus == SaveStatus.Failed;
    public readonly float TimeSinceLastSave => lastSaveCompletedTime > 0 ?
        UnityEngine.Time.realtimeSinceStartup - lastSaveCompletedTime : float.MaxValue;

    public bool Equals(SaveState other)
    {
      return currentStatus == other.currentStatus &&
             currentFileName == other.currentFileName &&
             lastErrorMessage == other.lastErrorMessage &&
             saveStartTime == other.saveStartTime &&
             lastSaveCompletedTime == other.lastSaveCompletedTime &&
             totalSavesAttempted == other.totalSavesAttempted &&
             totalSavesCompleted == other.totalSavesCompleted;
    }
  }

  public enum SaveStatus
  {
    Idle,
    InProgress,
    Completed,
    Failed
  }

  /// <summary>
  /// Serializable container for battle data that gets saved to file
  /// </summary>
  [System.Serializable]
  public struct BattleSaveData
  {
    public SerializableBattleState battleState;
    public SerializablePartyState partyState;
    public SerializableUIBattleState uiState;
    public SaveMetadata metadata;
  }

  [System.Serializable]
  public struct SaveMetadata
  {
    public string saveVersion;
    public float gameTime;
    public float realTime;
    public int turnCount;
    public string sceneName;
  }

  // Serializable versions of our ECS states
  [System.Serializable]
  public struct SerializableBattleState
  {
    public BattlePhase currentPhase;
    public int activeCharacterIndex;
    public int turnCount;
    public bool battleActive;
    public float turnTimer;
    // Note: Entity references can't be serialized directly
    // In real implementation, we'd use IDs or indices
  }

  [System.Serializable]
  public struct SerializablePartyState
  {
    public SerializableCharacterData[] characters;
  }

  [System.Serializable]
  public struct SerializableCharacterData
  {
    public string name;
    public int currentHealth;
    public int maxHealth;
    public int currentMana;
    public int maxMana;
    public CharacterStatus statusEffects;
  }

  [System.Serializable]
  public struct SerializableUIBattleState
  {
    public MenuPanel activePanel;
    public ActionType selectedAction;
    public int selectedSkillIndex;
    public int selectedItemIndex;
  }
}