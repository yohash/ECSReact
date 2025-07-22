using ECSReact.Core;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ECSReact.AddressableUtils
{
  /// <summary>
  /// Static factory for creating UIElements with various mounting strategies.
  /// Extensible through the IUIElementMounter interface.
  /// </summary>
  public static class Mount
  {
    public static class Element
    {
      /// <summary>
      /// Load a prefab from Addressables system.
      /// </summary>
      public static UIElement FromAddress(
        string key,
        string address,
        UIProps props = null,
        int index = 0,
        Transform parentTransform = null
      )
        => Core.Mount.Element.Custom(key, new AddressableMounter(address), null, props, index, parentTransform);
    }

    public static async Task PreloadAddressable(string address)
    {
      await AddressablesCache.LoadAsync<GameObject>(address);
    }
  }

  /// <summary>
  /// Mounts prefabs from the Addressables system.
  /// </summary>
  public class AddressableMounter : IUIElementMounter
  {
    private readonly string address;

    public AddressableMounter(string address)
    {
      this.address = address ?? throw new ArgumentNullException(nameof(address));
    }

    public async Task<GameObject> MountAsync(UIProps props)
    {
      try {
        var prefab = await AddressablesCache.LoadAsync<GameObject>(address);
        if (prefab == null) {
          throw new InvalidOperationException($"Failed to load addressable asset: {address}");
        }
        return prefab;
      } catch (Exception ex) {
        throw new InvalidOperationException($"Failed to load addressable asset '{address}': {ex.Message}", ex);
      }
    }
  }
}
