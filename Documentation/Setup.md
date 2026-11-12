# Quick Start Guide

## Required Scene Setup

### 1. **MonoBehaviours in Scene (3 required)**

* `Store` - Action dispatch singleton from UI to ECS
* `UIEventQueue` - Frame-budgeted UI event processing
* `SceneStateManager` - State singleton discovery and creation

### 2. **Code Generation (one-time setup)**

* Run **ECSReact → Auto Generate All** from Unity menu
* Select your namespaces
* Generates all required code automatically

### 3. **Initialize Code (2 method calls)**

After running code generation, call these in your startup script:

```csharp
void Start()
{
    // Initialize UI event processors (generated)
    StateNotificationEvents.InitializeEvents();

    // Initialize state subscriptions (generated)
    StateSubscriptionRegistration.InitializeSubscriptions();
}
```

### 4. **State Bootstrapping**

To get your state into the ECS world, there are two options:

1. **`SceneStateManager`** - State singleton discovery and creation
    1. Place this `MonoBehaviour` into your scene
    2. Run State Discovery
    3. Check-box select the state that you'd like in your scene
    4. (optional) Edit the default values of your state
    5. The manager will load your state into the ECS world on start

2. **Manual code** - Use this pattern to initialize your desired state

```csharp
    private void InitializeState(EntityManager entityManager)
    {
      var uiState = new UIState
      {
        activePanel = MenuPanel.None,
        selectedTarget = Entity.Null,
        selectedItemId = -1,
      };

      var entity = entityManager.CreateSingleton(uiState, "UI State");
    }
```

### **Finish**

With these 3 MonoBehaviours in scene + 2 initialization calls + code generation + state bootstrapping, you have:

* **Action dispatch** from UI to ECS  
* **State change detection** and UI updates  
* **Frame-budgeted rendering** to maintain 60fps  
* **Type-safe subscriptions** between states and UI  
* **Dynamic element composition** with automatic lifecycle management
* **Automatic cleanup** of processed actions

The ECS systems (middleware, reducers, cleanup) are automatically discovered by Unity ECS - no manual registration needed.

---

# Full Setup Guide

## After Quick Start

### 1. Define Your State Types

```csharp
public struct GameState : IGameState, IEquatable<GameState>
{
    public int health;
    public int score;

    public bool Equals(GameState other) => health == other.health && score == other.score;
}
```

### 2. Define Your Action Types

```csharp
public struct TakeDamageAction : IGameAction
{
    public int damage;
    public Entity source;
}
```

### 3. Create Reducers

For general game logic that needs flexibility:

```csharp
[Reducer]  // Burst-compiled by default
public struct GameReducer : IReducer<GameState, TakeDamageAction>
{
    public void Execute(ref GameState state, in TakeDamageAction action, ref SystemState systemState)
    {
        // Access time through systemState
        float time = systemState.WorldUnmanaged.Time.ElapsedTime;
                
        // Mutate state
        state.health = math.max(0, state.health - action.damage);
    }
}
```

### 4. Create UI Components

**Single State:**

```csharp
public class HealthBar : ReactiveUIComponent<GameState>
{
    [SerializeField] private Slider healthSlider;

    public override void OnStateChanged(GameState newState)
    {
        healthSlider.value = (float)newState.health / 100f;
    }
}
```

**Multi-State:**

```csharp
public class HUD : ReactiveUIComponent<GameState, PlayerState>
{      
    public override void OnStateChanged(GameState state)
    {
        // Update health, score displays
        UpdateHealthDisplay(state.health);
        UpdateScoreDisplay(state.score);
    }

    public override void OnStateChanged(PlayerState state)
    {
        // Update level, XP displays
        UpdateLevelDisplay(state.level);
        UpdateXPBar(state.experience);
    }
}
```

**With Dynamic Elements:**

```csharp
public class InventoryPanel : ReactiveUIComponent<InventoryState>
{
    private InventoryState currentState;

    public override void OnStateChanged(InventoryState newState)
    {
        currentState = newState;
        UpdateElements(); // Trigger element reconciliation
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
        if (currentState.items == null) yield break;

        // Create element for each inventory item
        int index = 0;
        foreach (var item in currentState.items)
        {
            yield return UIElement.FromPrefab(
                key: $"item_{item.id}",
                prefabPath: "UI/InventoryItemPrefab",
                props: new ItemProps {
                    ItemName = item.name,
                    ItemCount = item.count
                },
                index: index++
            );
        }

        // Show empty message when no items
        if (currentState.items.Count == 0)
        {
            yield return UIElement.FromComponent<EmptyInventoryMessage>(
                key: "empty_message"
            );
        }
    }
}
```

**Props for Data Passing:**

```csharp
public class ItemProps : UIProps
{
    public string ItemName { get; set; }
    public int ItemCount { get; set; }
    public Sprite ItemIcon { get; set; }
}

public class InventoryItemDisplay : ReactiveUIComponent<InventoryState>, IElement
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text countText;

    private ItemProps itemProps;
    private InventoryState inventoryState;

    public void InitializeWithProps(UIProps props)
    {
        itemProps = props as ItemProps;
        UpdateDisplay();
    }

    public void UpdateProps(UIProps props)
    {
        itemProps = props as ItemProps;
        UpdateDisplay();
    }

    public override void OnStateChanged(InventoryState newState)
    {
        inventoryState = newState;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (itemProps == null) return;

        nameText.text = itemProps.ItemName;
        countText.text = itemProps.ItemCount.ToString();
    }
}
```

### 5. Optional: Add Middleware

```csharp
[Middleware(DisableBurst = true)]  // Allows managed operations
public struct DamageValidation : IMiddleware<TakeDamageAction>
{
    public void OnCreate(ref SystemState state) { }

    public bool Process(in TakeDamageAction action, ref SystemState systemState, ref ActionWriter dispatch)
    {
        // Validate action
        if (action.damage < 0)
        {
            // Dispatch error feedback, routed to ShowErrorAction's lane
            dispatch.Dispatch(new ShowErrorAction
            {
                message = "Invalid damage value"
            });

            return false;  // Filter out this action - reducers won't see it
        }

        // Fetch other state using SystemState extensions
        if (systemState.TryGetSingleton<ConfigState>(out var config) && action.damage > config.maxDamage)
        {
            // Actions are read-only: filter it, or dispatch a corrected action instead
            dispatch.Dispatch(new TakeDamageAction { target = action.target, damage = config.maxDamage });
            return false;
        }

        return true;  // Action continues to reducers
    }
}
```

### 6. Optional: Process Gameplay Actions on the Fixed Step

Mark an action type with `[FixedStepAction]` to process it at the end of every fixed step instead of once per frame. Its middleware and reducers are generated into the fixed-lane groups, so regenerate after adding or removing the attribute. See [API: Action Lanes](API.md#action-lanes) for the timing rules.

```csharp
[FixedStepAction]
public struct TakeDamageAction : IGameAction { public Entity target; public int damage; }
```

## Upgrading to 0.2.0

0.2.0 changes the middleware signature and the generated code.

1. **Update every `IMiddleware` implementation.** `Process` now takes a lane-routing `ActionWriter` instead of a `ParallelWriter` and sort key:

   ```csharp
   // 0.1.x
   public bool Process(in MyAction action, ref SystemState systemState,
                       EntityCommandBuffer.ParallelWriter ecb, int sortKey)
   {
       ecb.DispatchAction(sortKey, new OtherAction { ... });
       ...
   }

   // 0.2.0
   public bool Process(in MyAction action, ref SystemState systemState, ref ActionWriter dispatch)
   {
       dispatch.Dispatch(new OtherAction { ... });   // no sort key; routed by OtherAction's lane
       ...
   }
   ```

   For non-action command buffer work, use `dispatch.Ecb` with `dispatch.NextSortKey()` (for example `var e = dispatch.Ecb.CreateEntity(dispatch.NextSortKey());`). Drop `sortKey + 1` patterns; the writer gives each dispatch its own key.

2. **Regenerate.** Run *ECS React → Auto Generate All* (or the ISystem Bridge Generator). Generated middleware now passes an `ActionWriter`, and systems for `[FixedStepAction]` types go into the fixed-lane groups.

3. **Optional: move gameplay actions to the fixed step.** Add `[FixedStepAction]` to an action type and regenerate. Existing actions keep their frame-lane behavior until you mark them.

## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. Setup
4. [API](API.md)
5. [Debugging Tools](Debugging.md)
6. [Examples & Patterns](Examples.md)
7. [Best Practices](BestPractices.md)
8. [Performance Optimization Guide](Performance.md)
