using System;
using System.Collections.Generic;
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

    private readonly Func<UIProps, Task<GameObject>> _mount;
    private readonly Type _componentType;

    // Reference to the instantiated component
    public ReactiveUIComponent Component { get; internal set; }
    public GameObject GameObject { get; internal set; }

    public UIElement(string key, Func<UIProps, Task<GameObject>> mount, Type componentType = null, UIProps props = null, int index = 0)
    {
      Key = key ?? throw new ArgumentNullException(nameof(key));
      _mount = mount ?? throw new ArgumentNullException(nameof(mount));
      _componentType = componentType;
      Props = props ?? UIProps.Empty;
      Index = index;
    }

    internal async Task<ReactiveUIComponent> mount(Transform parent)
    {
      var go = await _mount(Props);
      if (go == null)
        throw new InvalidOperationException($"Mount function returned null for element {Key}");

      go.transform.SetParent(parent, false);
      go.transform.SetSiblingIndex(Index);

      GameObject = go;
      Component = go.GetComponent<ReactiveUIComponent>();

      if (Component == null && _componentType != null) {
        Component = go.AddComponent(_componentType) as ReactiveUIComponent;
      }

      if (Component is IElement child) {
        child.InitializeWithProps(Props);
      }

      return Component;
    }

    internal void updateProps(UIProps newProps)
    {
      if (Component is IElement child) {
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

    /// <summary>
    /// Create an element from a prefab path
    /// </summary>
    public static UIElement FromPrefab(string key, string prefabPath, UIProps props = null, int index = 0)
    {
      return new UIElement(key, async (p) =>
      {
        var prefab = await loadPrefabAsync(prefabPath);
        return UnityEngine.Object.Instantiate(prefab);
      }, null, props, index);
    }

    /// <summary>
    /// Create an element that instantiates a specific component type
    /// </summary>
    public static UIElement FromComponent<T>(string key, UIProps props = null, int index = 0)
      where T : ReactiveUIComponent
    {
      return new UIElement(key, async (p) =>
      {
        var go = new GameObject($"UIElement_{typeof(T).Name}");
        go.AddComponent<T>();
        return go;
      }, typeof(T), props, index);
    }

    private static async Task<GameObject> loadPrefabAsync(string path)
    {
      // TODO - use Addressables or Resources.LoadAsync
      await Task.Yield();
      var prefab = Resources.Load<GameObject>(path);
      if (prefab == null)
        throw new InvalidOperationException($"Failed to load prefab at path: {path}");
      return prefab;
    }
  }

  /// <summary>
  /// Base class for props passed between UI components
  /// </summary>
  public class UIProps
  {
    public static readonly UIProps Empty = new UIProps();
    public virtual UIProps Clone() => MemberwiseClone() as UIProps;
  }

  /// <summary>
  /// Interface for components that can receive props from parent elements
  /// </summary>
  public interface IElement
  {
    void InitializeWithProps(UIProps props);
    void UpdateProps(UIProps props);
  }
}

// ===== EXAMPLE USAGE =====
// Element composition is now built into ALL ReactiveUIComponents!
// Just override DeclareElements() if you need dynamic children.
/*
namespace ECSReact.Examples
{
  // Example props for passing data to children
  public class ItemDisplayProps : UIProps
  {
    public string ItemName { get; set; }
    public int ItemCount { get; set; }
    public Sprite ItemIcon { get; set; }
  }

  // Example: An inventory UI that creates child elements for each item
  public class InventoryUI : SingleStateUIComponent<InventoryState>
  {
    private InventoryState _currentState;

    public override void OnStateChanged(InventoryState newState)
    {
      _currentState = newState;
      UpdateElements(); // Trigger element reconciliation
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      if (_currentState.items == null)
        yield break;

      // Create an element for each inventory item
      int index = 0;
      foreach (var item in _currentState.items) {
        yield return UIElement.FromPrefab(
          key: $"item_{item.id}",
          prefabPath: "UI/InventoryItemDisplay",
          props: new ItemDisplayProps
          {
            ItemName = item.name,
            ItemCount = item.count,
            ItemIcon = GetItemIcon(item.id)
          },
          index: index++
        );
      }

      // Conditionally show "empty" message
      if (_currentState.items.Count == 0) {
        yield return UIElement.FromComponent<EmptyInventoryMessage>(
          key: "empty_message"
        );
      }
    }

    private Sprite GetItemIcon(int itemId)
    {
      // Load icon based on item ID
      return null; // Placeholder
    }
  }

  // Example: A child component that receives props
  public class InventoryItemDisplay : SingleStateUIComponent<InventoryState>, IElementChild
  {
    [SerializeField] private UnityEngine.UI.Text nameText;
    [SerializeField] private UnityEngine.UI.Text countText;
    [SerializeField] private UnityEngine.UI.Image iconImage;

    private ItemDisplayProps _props;

    public void InitializeWithProps(UIProps props)
    {
      _props = props as ItemDisplayProps;
      UpdateDisplay();
    }

    public void UpdateProps(UIProps props)
    {
      _props = props as ItemDisplayProps;
      UpdateDisplay();
    }

    public override void OnStateChanged(InventoryState newState)
    {
      // Can also respond to global state changes if needed
    }

    private void UpdateDisplay()
    {
      if (_props == null)
        return;

      if (nameText)
        nameText.text = _props.ItemName;
      if (countText)
        countText.text = _props.ItemCount.ToString();
      if (iconImage)
        iconImage.sprite = _props.ItemIcon;
    }
  }

  // Example: Complex nested composition
  public class GameMenuUI : SingleStateUIComponent<GameState>
  {
    private GameState _gameState;

    public override void OnStateChanged(GameState newState)
    {
      _gameState = newState;
      UpdateElements();
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      // Header is always shown
      yield return UIElement.FromComponent<MenuHeader>("header");

      // Conditionally show different panels based on game state
      if (_gameState.isInCombat) {
        yield return UIElement.FromComponent<CombatControlsPanel>("combat_controls");
      } else {
        yield return UIElement.FromComponent<ExplorationPanel>("exploration");

        if (_gameState.canCraft) {
          yield return UIElement.FromComponent<CraftingPanel>("crafting");
        }
      }

      // Always show player status at the bottom
      yield return UIElement.FromComponent<PlayerStatusBar>("status",
        props: null,
        index: 999); // Force to bottom
    }
  }

  // Example: Component that doesn't need elements
  public class SimpleHealthBar : SingleStateUIComponent<PlayerState>
  {
    [SerializeField] private UnityEngine.UI.Slider healthSlider;

    public override void OnStateChanged(PlayerState newState)
    {
      // Just update the slider - no child elements needed
      healthSlider.value = (float)newState.health / newState.maxHealth;
    }

    // DeclareElements() returns empty by default - no override needed!
  }
}
*/
