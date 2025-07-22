using ECSReact.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;

namespace ECSReact.AddressableUtils
{
  /// <summary>
  /// Static factory for creating UIElements with various mounting strategies.
  /// Extensible through the IUIElementMounter interface.
  /// </summary>
  public static class MountAddressable
  {
    /// <summary>
    /// Load a prefab from Addressables system.
    /// </summary>
    public static UIElement FromAddress(string key, string address, UIProps props = null, int index = 0)
      => Mount.Custom(key, new AddressableMounter(address), null, props, index);
  }

  /// <summary>
  /// Mounts prefabs from the Addressables system.
  /// </summary>
  public class AddressableMounter : IUIElementMounter
  {
    private readonly string address;

    // Add cache management to your existing Mount class
    private static readonly Dictionary<string, GameObject> _addressableCache = new();
    private static readonly Dictionary<string, AsyncOperationHandle<GameObject>> _addressableHandles = new();

    public AddressableMounter(string address)
    {
      this.address = address ?? throw new ArgumentNullException(nameof(address));
    }

    public async Task<GameObject> MountAsync(UIProps props)
    {
      try {
        var prefab = await getCachedOrLoadAsync(address);
        if (prefab == null)
          throw new InvalidOperationException($"Failed to load addressable asset: {address}");

        return prefab;
      } catch (Exception ex) {
        throw new InvalidOperationException($"Failed to load addressable asset '{address}': {ex.Message}", ex);
      }
    }

    private async Task<GameObject> getCachedOrLoadAsync(string address)
    {
      if (!_addressableCache.ContainsKey(address)) {
        await Task.Run(async () =>
        {
          var handle = Addressables.LoadAssetAsync<GameObject>(address);
          var asset = await handle.Task;
          if (asset != null) {
            _addressableCache[address] = asset;
            _addressableHandles[address] = handle;
          }
        });
      }

      return _addressableCache.TryGetValue(address, out var cached) ? cached : null;
    }

    public static void ReleaseAddressableCache()
    {
      foreach (var handle in _addressableHandles.Values) {
        Addressables.Release(handle);
      }
      _addressableCache.Clear();
      _addressableHandles.Clear();
    }
  }
}
