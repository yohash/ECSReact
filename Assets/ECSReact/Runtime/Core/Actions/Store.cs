using Unity.Entities;
using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// MonoBehaviour that provides the main interface for dispatching actions from UI to ECS.
  /// Uses command buffers for thread-safe action creation.
  /// </summary>
  public class Store : MonoBehaviour
  {
    public static Store Instance { get; private set; }

    private EntityCommandBufferSystem commandBufferSystem;
    private EntityCommandBuffer currentFrameBuffer;
    private int lastFrameCreated = -1;

    void Awake()
    {
      if (Instance == null) {
        Instance = this;
        DontDestroyOnLoad(gameObject);
      } else {
        Destroy(gameObject);
      }
    }

    void Start()
    {
      var world = World.DefaultGameObjectInjectionWorld;
      commandBufferSystem = world.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();
    }

    /// <summary>
    /// Dispatch an action to be processed by ECS systems.
    /// Actions are created as entities with ActionTag for cleanup.
    /// </summary>
    public void Dispatch<T>(T action) where T : unmanaged, IGameAction
    {
      int currentFrame = Time.frameCount;
      if (lastFrameCreated != currentFrame) {
        currentFrameBuffer = commandBufferSystem.CreateCommandBuffer();
        lastFrameCreated = currentFrame;
      }

      // Store dispatches are synced with UI, and use non-parallel writer
      var entity = currentFrameBuffer.CreateEntity();
      currentFrameBuffer.AddComponent(entity, action);
      currentFrameBuffer.AddComponent(entity, new ActionTag());
    }
  }
}
