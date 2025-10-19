using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
using UnityEngine;

namespace ECSReact.AddressableUtils
{
  public static class AddressablesCache
  {
    private class LoadOperation
    {
      public TaskCompletionSource<object> TaskSource;
      public AsyncOperationHandle Handle;
      public CancellationTokenSource CancellationSource;
      public bool IsCompleted;
    }

    private static readonly Dictionary<string, LoadOperation> _loadingOperations = new();
    private static readonly Dictionary<string, object> _loadedAssets = new();
    private static readonly Dictionary<string, AsyncOperationHandle> _loadedHandles = new();

    private static readonly object _lock = new object();

    public static async Task<T> LoadAsync<T>(string key)
    {
      if (string.IsNullOrEmpty(key)) {
        throw new ArgumentException("Key cannot be null or empty", nameof(key));
      }

      LoadOperation operation = null;

      lock (_lock) {
        // Return already loaded asset immediately
        if (_loadedAssets.TryGetValue(key, out var cachedAsset)) {
          return (T)cachedAsset;
        }

        // Join existing load operation
        if (_loadingOperations.TryGetValue(key, out var existingOp)) {
          // Don't join if it's been canceled
          if (existingOp.CancellationSource.Token.IsCancellationRequested) {
            // Remove the canceled operation and start a new one
            _loadingOperations.Remove(key);
          } else {
            operation = existingOp;
          }
        }

        // Start new load operation if needed
        if (operation == null) {
          operation = startLoadOperation<T>(key);
          _loadingOperations[key] = operation;
        }
      }

      // await the operation outside the lock
      return await waitForOperation<T>(operation);
    }

    private static async Task<T> waitForOperation<T>(LoadOperation operation)
    {
      try {
        var result = await operation.TaskSource.Task;
        return (T)result;
      } catch (OperationCanceledException) {
        Debug.Log($"Asset load canceled for key: {operation.Handle.DebugName}");
        return default(T);
      } catch (Exception ex) {
        Debug.LogError($"Failed to load asset: {ex.Message}");
        return default(T);
      }
    }

    private static LoadOperation startLoadOperation<T>(string key)
    {
      var operation = new LoadOperation
      {
        TaskSource = new TaskCompletionSource<object>(),
        CancellationSource = new CancellationTokenSource(),
        IsCompleted = false
      };

      // Start the Unity Addressable load
      var handle = Addressables.LoadAssetAsync<T>(key);
      operation.Handle = handle;

      // Set up completion callback with proper cancellation handling
      handle.Completed += (op) => handleLoadCompleted(key, operation, op);

      return operation;
    }

    private static void handleLoadCompleted(string key, LoadOperation operation, AsyncOperationHandle completedHandle)
    {
      lock (_lock) {
        // Check if operation was canceled before completion
        if (operation.CancellationSource.Token.IsCancellationRequested) {
          // Operation was canceled - release the handle and don't cache
          Addressables.Release(completedHandle);
          operation.IsCompleted = true;
          return;
        }

        operation.IsCompleted = true;

        if (completedHandle.Status == AsyncOperationStatus.Succeeded) {
          // Cache the loaded asset and handle
          _loadedAssets[key] = completedHandle.Result;
          _loadedHandles[key] = completedHandle;

          // Complete the task
          operation.TaskSource.SetResult(completedHandle.Result);
        } else {
          // Release failed handle
          Addressables.Release(completedHandle);
          operation.TaskSource.SetException(
            completedHandle.OperationException ??
              new InvalidOperationException($"Failed to load addressable asset: {key}")
          );
        }

        // Remove from loading operations
        _loadingOperations.Remove(key);
      }
    }

    public static void Release(string key)
    {
      if (string.IsNullOrEmpty(key)) {
        return;
      }

      lock (_lock) {
        // Cancel any ongoing load operation
        if (_loadingOperations.TryGetValue(key, out var loadOp)) {
          cancelOperation(key, loadOp);
        }

        // Release loaded asset
        if (_loadedHandles.TryGetValue(key, out var handle)) {
          Addressables.Release(handle);
          _loadedAssets.Remove(key);
          _loadedHandles.Remove(key);
        }
      }
    }

    public static void ReleaseAll()
    {
      lock (_lock) {
        // Cancel all loading operations
        foreach (var kvp in _loadingOperations) {
          cancelOperation(kvp.Key, kvp.Value);
        }
        _loadingOperations.Clear();

        // Release all loaded assets
        foreach (var handle in _loadedHandles.Values) {
          Addressables.Release(handle);
        }
        _loadedAssets.Clear();
        _loadedHandles.Clear();

        // Optional: Force Unity to unload unused assets
        Resources.UnloadUnusedAssets();
      }
    }

    private static void cancelOperation(string key, LoadOperation operation)
    {
      if (operation.IsCompleted) {
        return;
      }

      // Cancel the task source
      operation.CancellationSource.Cancel();
      operation.TaskSource.SetCanceled();

      // Note: We don't release the handle here because the Unity operation 
      // might still be running. The completion callback will handle cleanup.
    }

    // Utility methods for debugging
    public static int LoadedAssetCount {
      get { lock (_lock) { return _loadedAssets.Count; } }
    }

    public static int PendingLoadCount {
      get { lock (_lock) { return _loadingOperations.Count; } }
    }

    public static IEnumerable<string> GetLoadedKeys()
    {
      lock (_lock) {
        return new List<string>(_loadedAssets.Keys);
      }
    }

    public static IEnumerable<string> GetPendingKeys()
    {
      lock (_lock) {
        return new List<string>(_loadingOperations.Keys);
      }
    }
  }
}