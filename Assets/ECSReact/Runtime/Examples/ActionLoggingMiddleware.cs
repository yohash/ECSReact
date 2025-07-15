using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Example middleware that logs all actions for debugging.
  /// Shows how to implement basic middleware functionality.
  /// </summary>
  public partial class ActionLoggingMiddleware<T> : MiddlewareSystem<T>
      where T : unmanaged, IGameAction
  {
    protected override void ProcessAction(T action, Entity actionEntity)
    {
      UnityEngine.Debug.Log($"Action processed: {typeof(T).Name} on entity {actionEntity}");
    }
  }
}
