# Architecture Overview

## 1. **State** → Singleton Components

**Components:**

* `IGameState.cs` - Marker interface defining what constitutes game state
* `SceneStateManager.cs` - Discovers, creates, and manages state singleton entities. Responsible for bootstrapping ECS state singletons during startup.
* `StateSubscriptionHelper.cs` - Type-safe registration system for UI subscriptions

**Internal Flow:**

Discovery Phase:
```
IGameState Types   →   SceneStateManager   →   ECS Singleton Entities
       ↓                      ↓                         ↓
   Reflection          Auto-Registration          Authoritative
    Scanning            + JSON Defaults           State Storage
```

Runtime Phase:
```
State Changes   →   StateSubscriptionHelper   →   UI Components
      ↓                       ↓                         ↓
  ECS Updates          Type-Safe Events         Reactive Updates
```

**State** is the single source of truth that drives all UI updates and serves as the target for all reducer modifications.

---

## 2. **Middleware** → Pre-Reducer ECS Systems

**Components:**

* `MiddlewareSystem<T>.cs` - Abstract base processing actions without consuming them
* `BurstMiddlewareSystem<T>.cs` - High-performance variant for simple operations

**Internal Flow:**

```
Action Created   →   Middleware Pipeline     →   Continue to Reducers
     ↓                       ↓                            ↓
   Entity             Side Effects Only              Unmodified
                    [Validation] [Logging]           Action Entity
                    [Analytics] [Async Ops]        
                             ↓                     
                    (Additional Actions,
                      Error Handling,
                      External Calls)
```

**Middleware** intercepts and processes actions for cross-cutting concerns before they reach business logic, without blocking the core state update flow.

---

## 3. **Reducer** → ECS Systems

**Components:**

* `ReducerSystem<TState, TAction>.cs` - State transformation logic with no side effects
* `StateChangeNotificationSystem<T>.cs` - Detects changes and generates UI events

**Internal Flow:**

```
Action Entities    →     ReducerSystem        →       Updated Singleton State
     ↓                        ↓                               ↓
 Type-Filtered           Mutate State                     New State
  ECS Query             (No Side Effects)                in ECS World
                              ↓
                         StateChangeNotificationSystem
                              ↓
                         Compare with Previous
                              ↓
                         Queue UI Events
```

**Reducers** are the core business logic layer that transforms actions into state changes and triggers the UI update pipeline.

---

## 4. **Action** → Command Components

**Components:**

* `IGameAction.cs` - Marker interface for action data structures
* `Store.cs` - Main dispatch interface from UI to ECS
* `ActionTag.cs` - Cleanup identification component
* `ActionCleanupSystem.cs` - End-of-frame entity destruction

**Internal Flow:**

```
User Input
   ↓
UI Event   →   Store.Dispatch()   →   ECS Action Entity   →   Systems Process   →   Cleanup
                    ↓                       ↓                        ↓                 ↓
               Command Buffer         ActionTag Added           Middleware          Action
               (Thread-Safe)           + Action Data            + Reducers         Destroyed
```

**Actions** provide the command pipeline that carries user intentions from UI into the ECS world.

---

## 5. **Component** → MonoBehaviour UI

**Components:**

* `ReactiveUIComponent.cs` - Base class with integrated element management
* `ReactiveUIComponent<T>.cs` - Single-state specialization
* `ReactiveUIComponent<T1,T2>.cs` - Multi-state variants (up to T1,T2,T3,T4,T5)
* `UIEventQueue.cs` - Frame-budgeted event processing
* `UIStateNotifier.cs` - ECS-to-C# event bridge

**Internal Flow:**

```
State Change   →   UIStateNotifier   →   UIEventQueue   →   UI Components   →   User Action
     ↓                   ↓                    ↓                  ↓                  ↓
ECS Detection        Type-Safe          Frame Budget        OnStateChanged    Store.Dispatch()
                     C# Events          (0.5ms/frame)         Callbacks             ↓
                         ↓                    ↓                  ↓              Back to ECS
                    Event Bridge       Priority Queue       Visual Update      (New Action)
                                                                ↓
                                                           DeclareElements()
                                                                ↓
                                                           UpdateElements()
```

**Components** now feature unified state subscription with integrated element management, closing the loop between state and presentation.

---

## 6. **Element** → Dynamic UI Composition

**Components:**

* `UIElement.cs` - Declarative child element definitions with async mounting
* `UIProps.cs` - Data passing mechanism between parent and child components
* `IElement.cs` - Interface for components that receive props from parents

**Internal Flow:**

```
State Changes   →   DeclareElements()   →   Element Reconciliation   →   Child Updates
     ↓                     ↓                        ↓                        ↓
 Parent Component   Desired Elements         Mount/Unmount/Update         Props Passed
                           ↓                        ↓                        ↓
                    Key-Based Diffing         Async Operations           Child Renders
                           ↓                        ↓                        ↓
                   Current vs Desired        Transform Hierarchy      IElement.UpdateProps()
```

**Elements** enable React-like declarative child composition where parents describe what children should exist, and the system handles mounting/unmounting/updating automatically based on keys and props.

**Element Lifecycle:**

1. **Declaration** - Parent's `DeclareElements()` returns desired child elements
2. **Reconciliation** - System diffs current vs desired children by key
3. **Mounting** - New elements are instantiated asynchronously via `UIElement.FromPrefab()` or `UIElement.FromComponent<T>()`
4. **Props Update** - Existing elements receive new props via `IElement.UpdateProps()`
5. **Unmounting** - Removed elements are destroyed and cleaned up

---

## **Complete System Flow**

Each component contributes to the unidirectional data flow:

```
UI Input  →  Actions  →  Middleware  →  Reducers  →  State  →  UI Updates  →  UI Input
   ↑                                                               ↓
   └───────────────────────── Complete Loop ───────────────────────┘
```

### **System Update Groups**

Unity ECS system update groups enforce this flow with deterministic execution order:

1. `MiddlewareSystemGroup` - Processes actions first for validation, logging, and async operations without consuming them
2. `ReducerSystemGroup` - Transforms actions into state changes with pure, deterministic business logic  
3. `ActionCleanupSystemGroup` - Destroys processed action entities at frame end, ensuring clean lifecycle management
4. `UINotificationSystemGroup` - Detects state changes and queues UI events with frame budgeting for responsive presentation
5. `PhysicsMiddlewareSystemGroup` - Handles physics-specific preprocessing for deterministic simulation timing

Each component has clear responsibilities and clean interfaces, making the system both powerful and maintainable at scale.

## Next

1. [Overview](Documentation/Overview.md)
2. Architecture
3. [Setup](Documentation/Setup.md)
4. [API](Documentation/API.md)
5. [Debugging Tools](Documentation/Debugging.md)
6. [Examples & Patterns](Documentation/Examples.md)