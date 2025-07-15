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
└── DispatchAction<T>(T action)                      // Helper: dispatch from UI

SingleStateUIComponent<T>
└── OnStateChanged(T newState)                       // Override: handle state updates

MultiStateUIComponent<T1,T2>
MultiStateUIComponent<T1,T2,T3>
MultiStateUIComponent<T1,T2,T3,T4>
MultiStateUIComponent<T1,T2,T3,T4,T5>
└── OnStateChanged(T1 newState)                       // Override: handle state1 updates
└── OnStateChanged(T2 newState)                       // Override: handle state2 updates
└── OnStateChanged(T3 newState)                       // Override: handle state3 updates
└── OnStateChanged(T4 newState)                       // Override: handle state4 updates
└── OnStateChanged(T5 newState)                       // Override: handle state5 updates

IStateSubscriber<T>
└── OnStateChanged(T newState)                       // Implement: type-safe state handling
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
└── VerifySingletonStates()                          // Debug: check existence
```

## System Base Classes

```csharp
StateReducerSystem<TState, TAction>
└── ReduceState(ref TState state, TAction action)    // Override: pure state logic

MiddlewareSystem<T>    
└── ProcessAction(T action, Entity entity)           // Override: side effects

StateChangeNotificationSystem<T>
└── CreateStateChangeEvent(T new, T old, bool hasOld) // Override: create UI events
```

## UI Events

```csharp
UIEventQueue
├── QueueEvent(UIEvent uiEvent)                      // Queue UI event for processing
└── GetQueueStats()                                  // Debug: queue performance stats

UIStateNotifier    
├── ProcessEvent(UIEvent uiEvent)                    // Process queued UI events
└── RegisterEventProcessor<T>(Action<T> processor)   // Register event handlers
```
