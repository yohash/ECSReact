using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Interface for custom UI element mounting strategies.
  /// Implement this to create custom asset loading and instantiation behavior.
  /// </summary>
  public interface IUIElementMounter
  {
    Task<GameObject> MountAsync(UIProps props);
  }

  /// <summary>
  /// Static factory for creating UIElements with various mounting strategies.
  /// Extensible through the IUIElementMounter interface.
  /// </summary>
  public static class Mount
  {
    /// <summary>
    /// Create a UIElement with a custom mounting strategy.
    /// </summary>
    public static UIElement Custom(string key, IUIElementMounter mounter, Type componentType = null, UIProps props = null, int index = 0)
    {
      return new UIElement(key, async (p) => await mounter.MountAsync(p), componentType, props, index);
    }

    /// <summary>
    /// Load a prefab from Resources folder.
    /// </summary>
    public static UIElement FromPrefab(string key, string prefabPath, UIProps props = null, int index = 0)
      => Custom(key, new PrefabMounter(prefabPath), null, props, index);

    /// <summary>
    /// Create a new GameObject with the specified component type.
    /// </summary>
    public static UIElement FromComponent<T>(string key, UIProps props = null, int index = 0)
      where T : ReactiveUIComponent
      => Custom(key, new ComponentMounter<T>(), typeof(T), props, index);
  }

  /// <summary>
  /// Mounts prefabs from the Resources folder.
  /// </summary>
  public class PrefabMounter : IUIElementMounter
  {
    private readonly string prefabPath;
    private readonly float timeoutSeconds;

    public PrefabMounter(string prefabPath, float timeoutSeconds = 10f)
    {
      this.prefabPath = prefabPath ?? throw new ArgumentNullException(nameof(prefabPath));
      this.timeoutSeconds = timeoutSeconds;
    }

    public async Task<GameObject> MountAsync(UIProps props)
    {
      try {
        var request = Resources.LoadAsync<GameObject>(prefabPath);

        // Wait for the async operation with timeout
        float elapsedTime = 0f;
        while (!request.isDone && elapsedTime < timeoutSeconds) {
          await Task.Yield();
          elapsedTime += Time.unscaledDeltaTime;
        }

        // Check for timeout
        if (!request.isDone) {
          throw new TimeoutException($"Resource loading timed out after {timeoutSeconds}s for path: {prefabPath}");
        }

        // Check for successful load
        var prefab = request.asset as GameObject;
        if (prefab == null) {
          throw new InvalidOperationException($"Failed to load prefab from Resources at path: {prefabPath}. " +
            $"Asset exists: {request.asset != null}, Asset type: {request.asset?.GetType().Name ?? "null"}");
        }

        return UnityEngine.Object.Instantiate(prefab);
      } catch {
        throw;
      }
    }
  }

  /// <summary>
  /// Creates a new GameObject with the specified component type.
  /// </summary>
  public class ComponentMounter<T> : IUIElementMounter where T : ReactiveUIComponent
  {
    public async Task<GameObject> MountAsync(UIProps props)
    {
      await Task.Yield();

      var go = new GameObject($"UIElement_{typeof(T).Name}");
      go.AddComponent<T>();
      return go;
    }
  }
}
