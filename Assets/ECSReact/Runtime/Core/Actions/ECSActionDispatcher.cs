using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// ECS entities shouldn't really call the Store directly since that's meant to be a UI→ECS bridge.
  /// This is a preferred pattern.
  /// The Store.Instance.Dispatch() should only be used from UI/MonoBehaviour code.
  /// </summary>
  public static class ECSActionDispatcher
  {
    /// <summary>
    /// Dispatch an action from within ECS systems. Thread-safe.
    /// </summary>
    public static void Dispatch<T>(EntityCommandBuffer commandBuffer, T action)
        where T : unmanaged, IGameAction
    {
      var entity = commandBuffer.CreateEntity();
      commandBuffer.AddComponent(entity, action);
      commandBuffer.AddComponent(entity, new ActionTag());
    }

    /// <summary>
    /// Dispatch an action immediately (use only in main thread systems).
    /// </summary>
    public static void DispatchImmediate<T>(EntityManager entityManager, T action)
        where T : unmanaged, IGameAction
    {
      var entity = entityManager.CreateEntity();
      entityManager.AddComponentData(entity, action);
      entityManager.AddComponentData(entity, new ActionTag());
    }
  }

  // Usage in systems:
  // ECSActionDispatcher.Dispatch(commandBuffer, new ExplosionAction { /* ... */ });
}
