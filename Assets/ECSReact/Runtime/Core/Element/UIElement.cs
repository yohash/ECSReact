using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ECSReact.Core
{
  /// <summary>
  /// Represents a UI element that can be dynamically mounted/unmounted by parent components.
  /// Similar to React's element concept - describes what UI should exist.
  /// </summary>
  public class UIElement
  {
    public string Key { get; }
    public int Index { get; set; }
    public UIProps Props { get; }
    public Transform ParentTransform { get; set; }

    private readonly Func<UIProps, Task<GameObject>> _mount;
    private readonly Type _componentType;

    // Reference to the instantiated component
    public ReactiveUIComponent Component { get; internal set; }
    public GameObject GameObject { get; internal set; }

    public UIElement(
      string key,
      Func<UIProps, Task<GameObject>> mount,
      Type componentType = null,
      UIProps props = null,
      int index = 0,
      Transform parentTransform = null
    )
    {
      Key = key ?? throw new ArgumentNullException(nameof(key));
      _mount = mount ?? throw new ArgumentNullException(nameof(mount));
      _componentType = componentType;
      Props = props ?? UIProps.Empty;
      Index = index;
      ParentTransform = parentTransform;
    }

    internal async Task<ReactiveUIComponent> mount(Transform defaultParent)
    {
      var go = await _mount(Props);
      if (go == null)
        throw new InvalidOperationException($"Mount function returned null for element {Key}");

      // Use custom parent transform if specified, otherwise use default
      var parent = ParentTransform != null ? ParentTransform : defaultParent;
      go.transform.SetParent(parent, false);
      go.transform.SetSiblingIndex(Index);

      GameObject = go;
      Component = go.GetComponent<ReactiveUIComponent>();

      if (Component == null && _componentType != null) {
        Component = go.AddComponent(_componentType) as ReactiveUIComponent;
      }

      if (Component is IElementChild child) {
        child.InitializeWithProps(Props);
      }

      return Component;
    }

    internal void updateProps(UIProps newProps)
    {
      if (Component is IElementChild child) {
        child.UpdateProps(newProps);
      }
    }

    internal void unmount()
    {
      if (GameObject != null) {
        UnityEngine.Object.Destroy(GameObject);
      }
      Component = null;
      GameObject = null;
    }
  }
}
