using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Base class for all UI components with integrated child element management.
  /// Now ALL ReactiveUIComponents can optionally declare child elements!
  /// </summary>
  public abstract class ReactiveUIComponent : MonoBehaviour
  {
    private readonly Dictionary<string, UIElement> _children = new Dictionary<string, UIElement>();
    private readonly SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1, 1);
    private CancellationTokenSource _cancellationTokenSource;
    private Task _currentUpdateTask;

    protected virtual void Start()
    {
      _cancellationTokenSource = new CancellationTokenSource();
      SubscribeToStateChanges();

      // Trigger initial element update
      queueElementUpdate();
    }

    protected virtual void OnDestroy()
    {
      UnsubscribeFromStateChanges();

      // Cancel any pending operations
      _cancellationTokenSource?.Cancel();
      _cancellationTokenSource?.Dispose();

      // Cleanup all children
      foreach (var child in _children.Values) {
        child.unmount();
      }
      _children.Clear();
    }

    /// <summary>
    /// Override this method to subscribe to state changes you care about.
    /// Use StateSubscriptionHelper for type-safe subscriptions.
    /// </summary>
    protected abstract void SubscribeToStateChanges();

    /// <summary>
    /// Override this method to unsubscribe from state changes.
    /// Must match exactly with SubscribeToStateChanges().
    /// </summary>
    protected abstract void UnsubscribeFromStateChanges();

    /// <summary>
    /// Helper method to dispatch actions to the ECS world.
    /// Provides convenient access to Store.Instance.Dispatch().
    /// </summary>
    protected void DispatchAction<T>(T action) where T : unmanaged, IGameAction
    {
      if (Store.Instance != null) {
        Store.Instance.Dispatch(action);
      } else {
        Debug.LogError("Store instance not found! Make sure Store is in the scene.");
      }
    }

    /// <summary>
    /// Virtual method to declare what child elements should exist.
    /// Override this to create dynamic child UI based on state.
    /// Default implementation returns no elements.
    /// </summary>
    protected virtual IEnumerable<UIElement> DeclareElements()
    {
      return Array.Empty<UIElement>();
    }

    /// <summary>
    /// Queue an element update. Updates are processed in order and won't be dropped.
    /// Call this when your state changes and you need to update child elements.
    /// </summary>
    protected void UpdateElements()
    {
      queueElementUpdate();
    }

    /// <summary>
    /// Internal method to queue element updates with proper ordering
    /// </summary>
    private void queueElementUpdate()
    {
      if (_cancellationTokenSource.Token.IsCancellationRequested)
        return;

      // Create the update task
      var updateTask = processNextUpdateAsync(_cancellationTokenSource.Token);

      // Chain it to the current task if one exists
      if (_currentUpdateTask != null && !_currentUpdateTask.IsCompleted) {
        _currentUpdateTask = _currentUpdateTask.ContinueWith(
          _ => updateTask,
          _cancellationTokenSource.Token,
          TaskContinuationOptions.None,
          TaskScheduler.FromCurrentSynchronizationContext()
        ).Unwrap();
      } else {
        _currentUpdateTask = updateTask;
      }
    }

    /// <summary>
    /// Process a single update within the semaphore lock
    /// </summary>
    private async Task processNextUpdateAsync(CancellationToken cancellationToken)
    {
      await _updateSemaphore.WaitAsync(cancellationToken);

      try {
        await updateElementsInternalAsync(cancellationToken);
      } catch (OperationCanceledException) {
        // Expected when component is destroyed
      } catch (Exception ex) {
        Debug.LogError($"Error updating elements in {GetType().Name}: {ex.Message}");
      } finally {
        _updateSemaphore.Release();
      }
    }

    /// <summary>
    /// Internal implementation of element updates
    /// </summary>
    private async Task updateElementsInternalAsync(CancellationToken cancellationToken)
    {
      var desiredElements = DeclareElements()?.ToList() ?? new List<UIElement>();
      var desiredKeys = new HashSet<string>(desiredElements.Select(e => e.Key));

      // Remove elements that are no longer desired
      var keysToRemove = _children.Keys.Where(k => !desiredKeys.Contains(k)).ToList();
      foreach (var key in keysToRemove) {
        if (cancellationToken.IsCancellationRequested)
          break;

        var element = _children[key];
        _children.Remove(key);
        element.unmount();
      }

      // Add or update elements
      var mountTasks = new List<Task>();

      foreach (var element in desiredElements) {
        if (cancellationToken.IsCancellationRequested)
          break;

        if (_children.TryGetValue(element.Key, out var existing)) {
          // Update existing element
          existing.Index = element.Index;
          if (existing.GameObject != null) {
            existing.GameObject.transform.SetSiblingIndex(element.Index);
            existing.updateProps(element.Props);
          }
        } else {
          // Mount new element
          _children[element.Key] = element;
          mountTasks.Add(mountElementAsync(element, cancellationToken));
        }
      }

      // Wait for all mounts to complete
      if (mountTasks.Count > 0) {
        await Task.WhenAll(mountTasks);
      }
    }

    private async Task mountElementAsync(UIElement element, CancellationToken cancellationToken)
    {
      try {
        await element.mount(transform);

        // Ensure we're still on the main thread after async operation
        if (!cancellationToken.IsCancellationRequested && element.GameObject != null) {
          element.GameObject.transform.SetSiblingIndex(element.Index);
        }
      } catch (Exception ex) {
        Debug.LogError($"Failed to mount element {element.Key}: {ex.Message}");

        // Remove from children if mount failed
        _children.Remove(element.Key);
      }
    }
  }
}
