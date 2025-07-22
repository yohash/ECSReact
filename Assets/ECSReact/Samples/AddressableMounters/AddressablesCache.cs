using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
using UnityEngine;

namespace ECSReact.AddressableUtils
{
  public static class AddressablesCache
  {
    private static Dictionary<string, TaskCompletionSource<object>> loadingAssets
      = new Dictionary<string, TaskCompletionSource<object>>();

    private static Dictionary<string, object> loadedAssets
      = new Dictionary<string, object>();

    public static async Task<T> LoadAsync<T>(string key)
    {
      // first test if the asset is already loaded, and return
      if (loadedAssets.ContainsKey(key)) {
        return (T)loadedAssets[key];
      }
      // if assets are loading already, we should provide a link to the
      // awaitable task handle to return
      if (loadingAssets.ContainsKey(key)) {
        try {
          return (T)await loadingAssets[key].Task;
        } catch (Exception ex) {
          loadingAssets.Remove(key);
          Debug.LogError($"Failed to load asset '{key}': {ex.Message}");
          return default;
        }
      }

      // finally, perform the asset loading from the addressables API
      var tcs = new TaskCompletionSource<object>();
      var handle = Addressables.LoadAssetAsync<T>(key);
      loadingAssets.Add(key, tcs);

      handle.Completed += (op) =>
      {
        if (op.Status == AsyncOperationStatus.Succeeded) {
          tcs.SetResult(op.Result);
          loadedAssets.Add(key, op.Result);
        } else {
          tcs.SetException(op.OperationException);
        }
        loadingAssets.Remove(key);
      };

      try {
        return (T)await tcs.Task;
      } catch (Exception ex) {
        loadingAssets.Remove(key);
        Debug.LogError($"Failed to load asset '{key}': {ex.Message}");
        return default;
      }
    }

    public static async Task Release(string key)
    {
      if (loadingAssets.ContainsKey(key)) {
        await loadingAssets[key].Task;
      }

      // first test if the asset is already loaded, and return
      if (!loadedAssets.ContainsKey(key)) { return; }

      Addressables.Release(loadedAssets[key]);
      loadedAssets.Remove(key);
    }

    public static void ReleaseAll()
    {
      foreach (var handle in loadedAssets.Values) {
        Addressables.Release(handle);
      }
      loadedAssets.Clear();

      foreach (var tcs in loadingAssets.Values) {
        tcs.SetCanceled(); // Cancel any pending loads
      }
      loadingAssets.Clear();

      // TBD - do we need to call this?
      Resources.UnloadUnusedAssets();
    }
  }
}
