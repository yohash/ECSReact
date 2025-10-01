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

Use for general game logic that needs flexibility:

```csharp
[Reducer]  // Burst-compiled by default
public struct GameReducer : IReducer<GameState, TakeDamageAction>
{
    public void Execute(ref GameState state, in TakeDamageAction action, ref SystemState systemState)
    {
        // Access time through systemState
        float time = systemState.WorldUnmanaged.Time.ElapsedTime;
        
        // Access singletons using extension methods
        var config = systemState.GetSingleton<GameConfig>();
        
        // Apply state mutation
        state.health = math.max(0, state.health - action.damage * config.damageMultiplier);
    }
}
```

#### Option B: Sequential with Entity Creation

Use when you need to create/destroy entities:


```csharp
[Reducer(DisableBurst = true)]  // Required for EntityManager access
public struct SpawnReducer : IReducer<GameState, SpawnEnemyAction>
{
    public void Execute(ref GameState state, in SpawnEnemyAction action, ref SystemState systemState)
    {
        // Create entity (requires DisableBurst = true)
        var entity = systemState.EntityManager.CreateEntity();
        systemState.EntityManager.AddComponentData(entity, new Enemy
        {
            health = action.health,
            damage = action.damage
        });

        // UnityEngine calls (requires DisableBurst = true)
        UnityEngine.Debug.Log($"Created entity: {entity}");  

        state.enemyCount++;
    }
}
```

#### Option C: Parallel Reducer (Maximum Performance)

Use for math-heavy operations that run frequently:

```csharp
[Reducer]  // Burst-compiled for 10-100x performance
public struct PhysicsReducer : IParallelReducer<PhysicsState, ForceAction, PhysicsReducer.FrameData>
{
    // Data prepared once per frame on main thread
    public struct FrameData
    {
        public float deltaTime;
        public float3 gravity;
        public ComponentLookup<Mass> massLookup;
    }
    
    public FrameData PrepareData(ref SystemState systemState)
    {
        // Full SystemAPI access here
        var config = systemState.GetSingleton<PhysicsConfig>();
        
        return new FrameData
        {
            deltaTime = systemState.WorldUnmanaged.Time.DeltaTime,
            gravity = config.gravity,
            massLookup = SystemAPI.GetComponentLookup<Mass>(true)
        };
    }
    
    public void Execute(ref PhysicsState state, in ForceAction action, in FrameData data)
    {
        // Pure parallel computation - no SystemAPI access
        var mass = data.massLookup[action.targetEntity];
        var acceleration = (action.force + data.gravity) / mass.value;
        
        state.velocity += acceleration * data.deltaTime;
        state.position += state.velocity * data.deltaTime;
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
    public bool Process(ref TakeDamageAction action, ref SystemState systemState)
    {
        // Validate action
        if (action.damage < 0)
        {
            // Dispatch error feedback
            ECSActionDispatcher.Dispatch(new ShowErrorAction 
            { 
                message = "Invalid damage value" 
            });
            
            return false;  // Filter out this action - reducers won't see it
        }
        
        // Clamp damage to max
        var config = systemState.GetSingleton<GameConfig>();
        action.damage = math.min(action.damage, config.maxDamage);
        
        return true;  // Action continues to reducers
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
8. [Performance Optimization Guide](Performance.md)
