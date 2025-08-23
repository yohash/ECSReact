using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  // State for battle log
  public struct BattleLogState : IGameState, System.IEquatable<BattleLogState>
  {
    public Unity.Collections.FixedList4096Bytes<BattleLogEntry> entries;
    public int totalEntriesLogged;

    public bool Equals(BattleLogState other)
    {
      if (totalEntriesLogged != other.totalEntriesLogged)
        return false;
      if (entries.Length != other.entries.Length)
        return false;

      for (int i = 0; i < entries.Length; i++) {
        if (!entries[i].Equals(other.entries[i]))
          return false;
      }

      return true;
    }
  }


  [System.Serializable]
  public struct BattleLogEntry : System.IEquatable<BattleLogEntry>
  {
    public LogType logType;
    public Unity.Collections.FixedString128Bytes message;
    public float timestamp;
    public int damageAmount; // For damage/healing entries

    public bool Equals(BattleLogEntry other)
    {
      return logType == other.logType &&
             message == other.message &&
             timestamp == other.timestamp &&
             damageAmount == other.damageAmount;
    }
  }
}