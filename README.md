# ECSReact for Unity

**A React-inspired unidirectional data flow architecture built on Unity's Entity Component System (ECS)**

ECSReact offers predictable state management for Unity game development with deterministic simulation, responsive UI, and clear separation of concerns.

### The Problem

Unity games struggle with state management at scale. As projects grow, developers face:

* **Spaghetti Code** - GameObjects directly calling methods on each other across the scene hierarchy
* **Update Order** - Critical game logic scattered across random `Update()` methods with unpredictable execution timing
* **Performance** - Expensive `FindObjectOfType()` calls and uncontrolled UI updates
* **Debugging** - No clear data flow makes it impossible to trace how state changes propagate through the system

### The Solution

ECSReact leverages unidirectional data flow for predictable, performant state management in Unity:

```
UI Events  →  Actions  →  ECS Systems  →  State  →  UI Updates
    ↑                                                   ↓
    └──────────────── Unidirectional Flow ──────────────┘
```

* **Deterministic** - ECS ensures identical simulation across all clients
* **Performant** - Frame-budgeted UI updates + ECS batch processing
* **Debuggable** - Clear action → state → UI data flow with built-in dev tools
* **Scalable** - Follows patterns of React state management and is inherently scalable
* **Team-Friendly** - UI and gameplay developers work independently with cross-coupling

## About

1. [Overview](Documentation/Overview.md)
2. [Architecture](Documentation/Architecture.md)
3. [Setup](Documentation/Setup.md)
4. [API](Documentation/API.md)
5. [Debugging Tools](Documentation/Debugging.md)
6. [Examples & Patterns](Documentation/Examples.md)
7. [Best Practices](Documentation/BestPractices.md)
