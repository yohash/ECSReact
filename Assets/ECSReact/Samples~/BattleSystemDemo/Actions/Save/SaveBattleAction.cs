using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Action to trigger saving the battle state to file.
  /// Demonstrates fire-and-forget async operations.
  /// </summary>
  public struct SaveBattleAction : IGameAction
  {
    public FixedString128Bytes fileName;  // Optional custom filename
    public SaveFormat format;             // JSON, Binary, etc.
  }

  /// <summary>
  /// Action dispatched when save operation starts
  /// </summary>
  public struct SaveBattleStartedAction : IGameAction
  {
    public FixedString128Bytes fileName;
    public float timestamp;
  }

  /// <summary>
  /// Action dispatched when save operation completes successfully
  /// </summary>
  public struct SaveBattleCompletedAction : IGameAction
  {
    public FixedString128Bytes fileName;
    public FixedString512Bytes filePath;
    public long fileSizeBytes;
    public float duration;       // How long the save took
  }

  /// <summary>
  /// Action dispatched when save operation fails
  /// </summary>
  public struct SaveBattleFailedAction : IGameAction
  {
    public FixedString128Bytes fileName;
    public FixedString512Bytes errorMessage;
    public SaveErrorType errorType;
  }

  public enum SaveFormat
  {
    JSON,
    Binary,
    Compressed
  }

  public enum SaveErrorType
  {
    FileSystemError,
    SerializationError,
    InsufficientSpace,
    PermissionDenied,
    Unknown
  }
}