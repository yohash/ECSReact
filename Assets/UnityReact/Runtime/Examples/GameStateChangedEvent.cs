namespace ECSReact.Core
{
  /// <summary>
  /// Example state change event for a hypothetical GameState.
  /// Shows the pattern for creating specific UI events.
  /// </summary>
  public class GameStateChangedEvent : UIEvent
  {
    public IGameState newState;
    public IGameState oldState;
    public bool hasOldState;

    public GameStateChangedEvent(IGameState newState, IGameState oldState, bool hasOldState)
    {
      this.newState = newState;
      this.oldState = oldState;
      this.hasOldState = hasOldState;
      this.priority = UIEventPriority.High; // State changes are usually high priority
    }
  }
}
