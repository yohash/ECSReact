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

#### Option A: Standard Reducer (Simple, Flexible)

Use for general game logic that doesn't require maximum performance:

```csharp
[ReducerSystem]
public partial class GameReducer : ReducerSystem<GameState, TakeDamageAction>
{
    public override void ReduceState(ref GameState state, TakeDamageAction action)
    {
        state.health = math.max(0, state.health - action.damage);
    }
}
```

#### Option B: Burst Reducer (Maximum Performance)

Use for performance-critical logic (physics, particles, AI):

```csharp
[ReducerSystem]
public partial class PhysicsReducer : BurstReducerSystem<PhysicsState, ApplyForceAction, PhysicsReducer.Logic>
{
    [BurstCompile]
    public struct Logic : IBurstReducer<PhysicsState, ApplyForceAction>
    {
        public void Execute(ref PhysicsState state, in ApplyForceAction action)
        {
            var acceleration = action.force / state.mass;
            state.velocity += acceleration * action.deltaTime;
            state.position += state.velocity * action.deltaTime;
        }
    }
}
```

#### When to Use Which?

| Use **Standard Reducer** when: | Use **Burst Reducer** when: |
|-------------------------------|----------------------------|
| • Logic involves Unity APIs | • Processing many entities |
| • Need debug logging | • Math-heavy calculations |
| • Complex game rules | • Physics simulations |
| • String operations | • Particle systems |
| • File I/O or networking | • Combat damage calculations |

Convert a standard reducer to Burst:
1. Move logic to a struct implementing `IBurstReducer`
2. Change base class to `BurstReducerSystem`
3. Add `[BurstCompile]` attribute

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
        // Can also respond to global state if needed
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
public partial class DamageValidation : MiddlewareSystem<TakeDamageAction>
{
    protected override void ProcessAction(TakeDamageAction action, Entity entity)
    {
        if (action.damage < 0) 
        {
            EntityManager.AddComponent<InvalidActionTag>(entity);
            DispatchAction(new ShowErrorAction { message = "Invalid damage value" });
        }
    }
}
```

## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. Setup
4. [API](API.md)
5. [Debugging Tools](Debugging.md)
6. [Examples & Patterns](Examples.md)
7. [Best Practices](BestPractices.md)
