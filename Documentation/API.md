# API Reference

## Store Dispatch

```csharp
Store.Instance.Dispatch<T>(T action)                 // Dispatch action to ECS (from UI / MonoBehaviours)

ECSActionDispatcher
├── Dispatch<T>(T action)                            // Main thread: dispatch from systems; routed by T's lane
├── Dispatch<T>(T action, World world)               // Main thread: dispatch into a specific world
├── GetJobCommandBuffer(World world = null)          // Jobs: ParallelWriter for frame-lane dispatches
├── GetFixedJobCommandBuffer(World world = null)     // Jobs: ParallelWriter for [FixedStepAction] dispatches
├── RegisterJobHandle(JobHandle handle, World world = null)
│                                                    // Jobs: call after scheduling; playback waits for the job
└── GetActionWriter(World world, bool isFixedLane)   // Middleware: lane-routing writer (generated code calls this)

ECBActionExtensions                                  // Burst-compatible; the tag follows T's lane
├── ParallelWriter.DispatchAction<T>(int sortKey, T action)
└── EntityCommandBuffer.DispatchAction<T>(T action)
```

Every dispatch is deferred. Where it is processed depends on the action type's lane, see [Action Lanes](#action-lanes).

## Action Lanes

Each action type belongs to exactly one lane:

* **Frame lane** (default): processed once per rendered frame by `MiddlewareSystemGroup` and `ReducerSystemGroup`.
* **Fixed-step lane** (`[FixedStepAction]`): processed at the end of every fixed step by `FixedActionSystemGroup`, which runs last in `FixedStepSimulationSystemGroup`: after physics and after `EndFixedStepSimulationEntityCommandBufferSystem` plays back. Fixed reducers therefore see the structural changes gameplay systems queued into that ECB in the same step. Commands that fixed middleware or reducers queue into it play back in the next step.

```csharp
[FixedStepAction]
public struct TakeDamageAction : IGameAction { public Entity target; public int amount; }
```

Timing is counted in steps for the fixed lane, never in frames:

| Dispatched by | Action lane | Processed |
| --- | --- | --- |
| A fixed-step system, before the phase | fixed | **the same step (N)** |
| Fixed-lane middleware in step N (a chain) | fixed | step N+1, one step per hop |
| Fixed-lane middleware | frame | next frame |
| Frame-rate code (UI, input, frame-lane middleware) | fixed | the next fixed step; kept across frames that run no step |
| Frame-rate code | frame | next frame |

Fixed-lane action entities carry `FixedActionTag` instead of `ActionTag`, and only exist inside `FixedActionSystemGroup`. Hand-written systems that handle them should use `[UpdateInGroup(typeof(FixedReducerSystemGroup))]` / `[UpdateInGroup(typeof(FixedMiddlewareSystemGroup))]` and query `FixedActionTag`. Use the plain `UpdateInGroup` attribute: Entities matches it by exact type, so subclasses of it are ignored.

`ActionLane<T>.IsFixed` reports the lane of `T` and can be read from Burst code.

Playback order of dispatched actions is deterministic but is not dispatch order across systems: every middleware's `ActionWriter` starts at sort key 0 in the same lane buffer, so dispatches from systems A and B play back as A0, B0, A1, B1, … Don't rely on the relative order of actions dispatched by different systems.

Note: frame-dispatched fixed-lane actions are deterministic on one machine, but which step they land on depends on frame timing. Cross-machine lockstep input needs step-stamped input, which ECSReact doesn't provide.

## UI Components

```csharp
ReactiveUIComponent
├── SubscribeToStateChanges()                        // Override: define subscriptions
├── UnsubscribeFromStateChanges()                    // Override: cleanup subscriptions    
├── DispatchAction<T>(T action)                      // Helper: dispatch from UI
├── DeclareElements()                                // Override: define child elements
└── UpdateElements()                                 // Call: trigger element reconciliation

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
├── GetStateEntity<T>()                              // Helper: Return the state's Entity or Entity.Null
├── GetState<T>()                                    // Helper: Get the state type from the retrieved Entity
├── SetState<T>(T state)                             // Helper: Set the entities component data via Entity Manager
├── GetStateEntity(Type stateType)                   // Helper: Return the state's Entity or Entity.Null
├── GetState(Type stateType)                         // Helper: Get the state type from the State Registry
├── SetState(Type stateType, object state)           // Helper: Set the state using the State Registry
├── HasState<T>()                                    // Query: Check the state entities for a contained Type
├── HasState(Type stateType)                         // Query: Check the state entities for a contained Type
└── GetAllStateEntities()                            // Query: Returns the IReadOnlyDictionary<Type, Entity> of entities
```

## System Base Classes

```csharp
IReducer<TState, TAction>
└── Execute(ref TState state, in TAction action, ref SystemState systemState)
                                                     // Implement: mutate state with SystemAPI access via SystemState

IMiddleware<TAction>
├── OnCreate(ref SystemState state)                   // Implement: cache queries, lookups, or other one-time setup
└── bool Process(in TAction action, ref SystemState systemState, ref ActionWriter dispatch)
                                                     // Implement: Sequential processing, inspect or filter actions (read-only)
                                                     // Return false to prevent action from reaching reducers

ActionWriter                                         // Passed to middleware; Burst-compatible
├── Dispatch<T>(T action)                            // Dispatch into T's own lane (frame or fixed)
├── Ecb                                              // Raw ParallelWriter for this middleware's own lane
├── NextSortKey()                                    // Unused sort key for commands written to Ecb
└── IsFixedLane                                      // True when this middleware runs in the fixed lane

StateChangeNotificationSystem<T>
└── CreateStateChangeEvent(T new, T old, bool hasOld) // Override: create UI events
```

## Attributes

```csharp
[Reducer(
    Order = 100,                                      // Execution order (lower runs first)
    SystemName = "CustomName",                        // Optional: override generated system name
    DisableBurst = false                              // Optional: disable Burst compilation
)]

[Middleware(
    Order = 50,                                       // Execution order (lower runs first)
    SystemName = "CustomName",                        // Optional: override generated system name  
    DisableBurst = false                              // Optional: disable Burst compilation
)]

[FixedStepAction]                                     // On an IGameAction struct: process it on the fixed step
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

## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. [Setup](Setup.md)
4. API
5. [Debugging Tools](Debugging.md)
6. [Examples & Patterns](Examples.md)
7. [Best Practices](BestPractices.md)
8. [Performance Optimization Guide](Performance.md)
