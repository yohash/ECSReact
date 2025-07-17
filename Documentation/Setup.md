
# Quick Start Guide

## Required Scene Setup

### 1. **MonoBehaviours in Scene (3 required)**

* `Store` - Action dispatch singleton from UI to ECS
* `UIEventQueue` - Frame-budgeted UI event processing
* `SceneStateManager` - State singleton discovery and creation
  * Enable "Auto Discover On Awake"
  * Enable "Create Singletons On Start"

### 2. **Code Generation (one-time setup)**

* Run **ECSReact → Auto Generate All** from Unity menu
* Select your namespaces (avoid `ECSReact.Core`)
* Generates all required glue code automatically

### 3. **Initialization Code (2 method calls)**

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

### **Finish**

With these 3 MonoBehaviours in scene + 2 initialization calls + code generation, you have:

* **Action dispatch** from UI to ECS  
* **State change detection** and UI updates  
* **Frame-budgeted rendering** to maintain 60fps  
* **Type-safe subscriptions** between states and UI  
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

```csharp
public partial class GameReducer : StateReducerSystem<GameState, TakeDamageAction>
{
    protected override void ReduceState(ref GameState state, TakeDamageAction action)
    {
        state.health = math.max(0, state.health - action.damage);
    }
}
```

### 4. Create UI Components

**Single State:**

```csharp
public class HealthBar : SingleStateUIComponent<GameState>
{
    public override void OnStateChanged(GameState newState)
    {
        healthSlider.value = (float)newState.health / 100f;
    }
}
```

**Multi-State:**

```csharp
public class HUD : MultiStateUIComponent<GameState, PlayerState>
{      
    public void OnStateChanged(GameState state) { /* handle game state */ }
    public void OnStateChanged(PlayerState state) { /* handle player state */ }
}
```

### 5. Optional: Add Middleware

```csharp
public partial class DamageValidation : MiddlewareSystem<TakeDamageAction>
{
    protected override void ProcessAction(TakeDamageAction action, Entity entity)
    {
        if (action.damage < 0) EntityManager.AddComponent<InvalidActionTag>(entity);
    }
}
```
