# Core Concepts

ECSReact combines the familiar patterns of React state management with the performance and determinism of Unity ECS.

## React to ECS Mapping

ECSReact translates familiar React concepts into ECS equivalents:

| React Concept | ECS Implementation | Purpose |
| --- | --- | --- |
| **State** | Singleton Components | Centralized, authoritative game state storage |
| **Middleware** | Pre-Reducer ECS Systems | Cross-cutting concerns, validation, async operations |
| **Reducer** | ECS Systems | Pure functions that process actions and update state |
| **Action** | Command Components | Immutable data structures describing state changes |
| **Element** | UIElement + Props | Dynamic child component composition with data passing |
| **Component** | ReactiveUIComponent<T> | Unified state subscription with integrated element management |

## Unidirectional Data Flow

Actions flow from UI through middleware to reducers, while state changes propagate back to UI through frame-budgeted event queues. UIElements enable declarative child composition that updates automatically when state changes. The system provides deterministic game simulation through ECS while maintaining responsive UI through a decoupled presentation layer:

```
┌─────────────────┐    Actions      ┌──────────────────────────────────┐
│   UI Layer      │───────────────▶ │       ECS Game Layer             │
│  (Presentation) │                 │      (Simulation)                │
│                 │◀────────────────│                                  │
└─────────────────┘   State Events  │  ┌─────────────────────────────┐ │
     ▲                              │  │    Middleware Systems       │ │
     │ Render Updates               │  │   ┌─────────────────────┐   │ │
     │                              │  │   │ • Validation        │   │ │
┌────▼──────┐                       │  │   │ • Analytics         │   │ │
│  Device   │                       │  │   │ • File I/O (Async)  │   │ │
│ (Laggy)   │                       │  │   │ • Network Sync      │   │ │
└───────────┘                       │  │   │ • Error Handling    │   │ │
                                    │  │   └─────────────────────┘   │ │
                                    │  └─────────────┬───────────────┘ │
                                    │                │ Actions         │
                                    │                ▼                 │
                                    │  ┌─────────────────────────────┐ │
                                    │  │     Reducer Systems         │ │
                                    │  │   ┌─────────────────────┐   │ │
                                    │  │   │ • Pure State Logic  │   │ │
                                    │  │   │ • Deterministic     │   │ │
                                    │  │   │ • Multiplayer Safe  │   │ │
                                    │  │   └─────────────────────┘   │ │
                                    │  └─────────────────────────────┘ │
                                    └──────────────────────────────────┘
                                                     ▲
                                                     │ Network Sync
                                                     │ (Deterministic)
                                                ┌────▼────┐
                                                │  Other  │
                                                │ Player  │
                                                └─────────┘
```

## Architectural Motivation

### Driving Factors

**Deterministic Multiplayer**

* ECS simulation must run identically across all clients
* UI lag and device differences cannot affect game state
* Network synchronization requires predictable, reproducible game logic

**Performance Separation**

* ECS handles high-performance game simulation (physics, AI, combat)
* UI handles presentation concerns (animations, input, rendering)
* Async operations in middleware don't block or affect simulation timing
* Infrastructure async operations (file I/O, network calls) are isolated from deterministic simulation
* Each layer can be optimized independently

**Responsive UI Experience**

* UI can animate and provide feedback without blocking simulation
* Device performance variations don't impact game fairness
* UIElements enable efficient composition and updates
* Dynamic child management based on state changes

**Development Workflow**

* UI developers can work on presentation without understanding ECS
* Game logic developers can focus on deterministic systems
* Clear separation of concerns reduces coupling and bugs
* Declarative element composition simplifies complex UI hierarchies

## Next

1. Overview
2. [Architecture](Architecture.md)
3. [Setup](Setup.md)
4. [API](API.md)
5. [Debugging Tools](Debugging.md)
6. [Examples & Patterns](Examples.md)
7. [Best Practices](BestPractices.md)
8. [Performance Optimization Guide](Performance.md)
