using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Editor
{
  /// <summary>
  /// Utility component that can be added to GameObjects to debug UI state subscriptions.
  /// Shows when subscriptions happen and can log state changes.
  /// </summary>
  public class UISubscriptionDebugger : MonoBehaviour
  {
    [SerializeField] private bool logSubscriptions = true;
    [SerializeField] private bool logStateChanges = false;
    [SerializeField] private string componentFilter = ""; // Filter logs by component name

    private void Awake()
    {
      if (logSubscriptions) {
        var reactiveComponents = GetComponentsInChildren<ReactiveUIComponent>();
        foreach (var component in reactiveComponents) {
          if (string.IsNullOrEmpty(componentFilter) ||
              component.GetType().Name.Contains(componentFilter)) {
            Debug.Log($"Found ReactiveUIComponent: {component.GetType().Name} on {component.gameObject.name}");
          }
        }
      }
    }

    /// <summary>
    /// Call this method from your state change handlers to log state changes.
    /// </summary>
    public void LogStateChange<T>(T newState) where T : unmanaged, IGameState
    {
      if (logStateChanges &&
          (string.IsNullOrEmpty(componentFilter) || typeof(T).Name.Contains(componentFilter))) {
        Debug.Log($"State changed: {typeof(T).Name} = {newState}");
      }
    }
  }
}
