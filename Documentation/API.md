# API Reference

## Core Dispatch

```csharp
Store.Dispatch<T>(T action)                          // Dispatch action to ECS
Store.Instance.Dispatch<T>(T action)                 // Singleton access
```

## UI Components

```csharp
ReactiveUIComponent
├── SubscribeToStateChanges()                        // Override: define subscriptions
├── UnsubscribeFromStateChanges()                    // Override: cleanup subscriptions    
├── DispatchAction<T>(T action)                      // Helper: dispatch from UI
├── DeclareElements()                                 // Override: define child elements
└── UpdateElements()                                  // Call: trigger element reconciliation

ReactiveUIComponent<T>
└── OnStateChanged(T newState)                       // Override: handle state updates

ReactiveUIComponent<T1,T2>
├── OnStateChanged(T1 newState)                      // Override: handle state1 updates
└── OnStateChanged(T2 newState)                      // Override: handle state2 updates

ReactiveUIComponent<T1,T2,T3>
├── OnStateChanged(T1 newState)                      // Override: handle state1 updates
├── OnStateChanged(T2 newState)                      // Override: handle state2 updates
└── OnStateChanged(T3 newState)                      // Override: handle state3 updates

ReactiveUIComponent<T1,T2,T3,T4>
├── OnStateChanged(T1 newState)                      // Override: handle state1 updates
├── OnStateChanged(T2 newState)                      // Override: handle state2 updates
├── OnStateChanged(T3 newState)                      // Override: handle state3 updates
└── OnStateChanged(T4 newState)                      // Override: handle state4 updates

ReactiveUIComponent<T1,T2,T3,T4,T5>
├── OnStateChanged(T1 newState)                      // Override: handle state1 updates
├── OnStateChanged(T2 newState)                      // Override: handle state2 updates
├── OnStateChanged(T3 newState)                      // Override: handle state3 updates
├── OnStateChanged(T4 newState)                      // Override: handle state4 updates
└── OnStateChanged(T5 newState)                      // Override: handle state5 updates

IStateSubscriber<T>
└── OnStateChanged(T newState)                       // Implement: type-safe state handling
```

## Element Composition

```csharp
UIElement
├── FromPrefab(key, prefabPath, props, index)        // Static: create from prefab resource
├── FromComponent<T>(key, props, index)              // Static: create component instance
├── Key                                              // Property: unique identifier
├── Index                                            // Property: sibling index in parent
├── Props                                            // Property: data passed to child
├── Component                                        // Property: mounted ReactiveUIComponent
└── GameObject                                       // Property: mounted GameObject

UIProps
├── Clone()                                          // Virtual: create copy for updates
└── Empty                                            // Static: default empty props

IElement
├── InitializeWithProps(UIProps props)               // Implement: receive initial props
└── UpdateProps(UIProps props)                       // Implement: receive updated props
```

## State Management

```csharp
StateSubscriptionHelper
├── Subscribe<T>(IStateSubscriber<T> subscriber)     // Subscribe to state changes
├── Unsubscribe<T>(IStateSubscriber<T> subscriber)   // Unsubscribe from changes
└── RegisterStateSubscriptionHandlers<T>()           // Generated: register handlers

SceneStateManager
├── DiscoverStates()                                 // Find all IGameState types
├── CreateEnabledSingletons()                        // Create state entities    
├── RemoveDisabledSingletons()                       // Remove state entities
├── VerifySingletonStates()                          // Debug: check existence
└── GetStatesByNamespace()                           // Get discovered states by namespace
```

## System Base Classes

```csharp
StateReducerSystem<TState, TAction>
└── ReduceState(ref TState state, TAction action)    // Override: pure state logic

MiddlewareSystem<T>    
├── ProcessAction(T action, Entity entity)           // Override: side effects
└── DispatchAction<TNew>(TNew newAction)             // Helper: dispatch additional actions

BurstMiddlewareSystem<T>    
├── ProcessAction(T action, Entity entity)           // Override: burst-compatible side effects
└── DispatchAction<TNew>(TNew newAction)             // Helper: dispatch additional actions

StateChangeNotificationSystem<T>
└── CreateStateChangeEvent(T new, T old, bool hasOld) // Override: create UI events
```

## UI Events

```csharp
UIEventQueue
├── QueueEvent(UIEvent uiEvent)                      // Queue UI event for processing
├── GetQueueStats()                                  // Debug: queue performance stats
└── Instance                                         // Static: singleton access

UIStateNotifier    
├── ProcessEvent(UIEvent uiEvent)                    // Process queued UI events
└── RegisterEventProcessor<T>(Action<T> processor)   // Register event handlers

UIEvent
├── priority                                         // Property: Normal/High/Critical
└── timestamp                                        // Property: creation time

UIEventPriority
├── Normal                                           // Enum: standard priority
├── High                                             // Enum: processed first
└── Critical                                         // Enum: no time budget limit
```

## Generated Extensions (Code Generation)

```csharp
// Generated Store Extensions (per action type)
Store.ActionName(param1, param2, ...)               // Typed dispatch methods

// Generated State Events (per state type)  
StateNotificationEvents.OnStateNameChanged          // C# events for state changes
StateNotificationEvents.InitializeEvents();         // Initialize all events

// Generated Subscription Registration
StateSubscriptionRegistration.InitializeSubscriptions() // Initialize all state subscriptions
```

## Element Lifecycle

```csharp
// Element Creation
UIElement.FromPrefab("unique_key", "UI/PrefabPath", props, index)
UIElement.FromComponent<ComponentType>("unique_key", props, index)

// Element Mounting (automatic)
element.mount(parentTransform)                       // Internal: async mounting
element.Component                                    // Access: mounted component reference
element.GameObject                                   // Access: mounted GameObject

// Element Updates (automatic)
element.updateProps(newProps)                       // Internal: props change handling
IElement.UpdateProps(newProps)                      // Implement: receive prop updates

// Element Unmounting (automatic)
element.unmount()                                    // Internal: cleanup and destroy
```

## Usage Patterns

```csharp
// Single State Component
public class HealthDisplay : ReactiveUIComponent<HealthState>
{
    public override void OnStateChanged(HealthState state) { /* update UI */ }
}

// Multi-State Component  
public class GameHUD : ReactiveUIComponent<GameState, PlayerState>
{
    public override void OnStateChanged(GameState state) { /* update game UI */ }
    public override void OnStateChanged(PlayerState state) { /* update player UI */ }
}

// Dynamic Element Composition
public class InventoryGrid : ReactiveUIComponent<InventoryState>
{
    protected override IEnumerable<UIElement> DeclareElements()
    {
        foreach (var item in state.items)
            yield return UIElement.FromPrefab($"item_{item.id}", "UI/Item", itemProps);
    }
}

// Props-Based Communication
public class ItemDisplay : ReactiveUIComponent<InventoryState>, IElement
{
    public void InitializeWithProps(UIProps props) { /* setup with initial data */ }
    public void UpdateProps(UIProps props) { /* handle props changes */ }
}
```# API Reference

## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. [Setup](Setup.md)
4. API
5. [Debugging Tools](Debugging.md)
6. [Examples & Patterns](Examples.md)