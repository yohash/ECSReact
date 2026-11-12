using Unity.Entities;
using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// MonoBehaviour that provides the main interface for dispatching actions from UI to ECS.
  /// Dispatches go through ECSActionDispatcher, which routes each action type to its lane
  /// (frame or fixed-step) and manages the command buffers.
  /// </summary>
  public class Store : MonoBehaviour
  {
    public static Store Instance { get; private set; }

    private World world;

    void Awake()
    {
      if (Instance == null) {
        Instance = this;
        DontDestroyOnLoad(gameObject);
      } else {
        Destroy(gameObject);
      }

      world = World.DefaultGameObjectInjectionWorld;
    }

    /// <summary>
    /// Dispatch an action to be processed by ECS systems.
    /// Frame-lane actions are processed next frame; [FixedStepAction] actions are
    /// processed in the next fixed step.
    /// </summary>
    public void Dispatch<T>(T action) where T : unmanaged, IGameAction
    {
      ECSActionDispatcher.Dispatch(action, world);
    }
  }
}
