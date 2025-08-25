# Best Practices

## General

### Action Design

- **Actions are Events** - Name them as past-tense events: `CharacterAttacked`, not `AttackCharacter`
- **Complete Context** - Include all data needed by reducers to process the action
- **Immutable Data** - Use value types and fixed collections for action data
- **Single Responsibility** - Each action represents one logical event

### Reducer Design

- **Pure Functions** - No side effects, no external dependencies
- **Defensive Copying** - Always work with copies of state data
- **Predictable Updates** - Same state + same action = same result
- **Fast Execution** - Keep reducers lightweight, defer heavy computation

### State Design

- **Normalized Structure** - Avoid deeply nested state trees
- **Fixed-Size Collections** - Use `FixedList` types for predictable memory layout
- **Value Equality** - Implement `IEquatable<T>` for efficient change detection
- **Minimal State** - Store only essential data, compute derived values as needed

## State Normalization

### The Problem

A common anti-pattern in state management occurs when complex data structures are embedded directly within state, leading to duplicate data, inefficient lookups, and difficult updates. For example, when `PartyState` contains a `FixedList<CharacterData>` where each `CharacterData` includes an Entity reference, finding a specific character requires O(n) iteration. Meanwhile, `BattleState` maintains its own `turnOrder` list of the same entities, creating redundancy and potential synchronization issues.

This creates multiple problems: how do we efficiently look up entity data? Should we duplicate entity information across states? How do we handle relationships between entities without creating deeply nested structures?

### React Best Practices for State Normalization

In React/Redux architecture, state should be **normalized like a database** - flat structure with entities stored in lookup tables and relationships expressed through IDs. Redux documentation explicitly recommends:

1. **Store entities in objects keyed by ID** - Enables O(1) lookups instead of array searches
2. **Store relationships as arrays of IDs** - Prevents duplication and ensures single source of truth
3. **Keep state shape flat** - Avoid nested data structures that are hard to update immutably
4. **Derive computed data in selectors** - Don't store values that can be calculated from existing state

The principle is: **"Store state in a normalized shape, then denormalize as needed for the UI"** - the storage format optimizes for updates, while views compute the shape they need.

### Unity ECS Performance Best Practices

For optimal ECS performance with normalized state:

- **Use Entity as the natural primary key** - Entities are already unique, stable identifiers perfect for lookups
- **Prefer NativeHashMap for lookups** - O(1) access with excellent cache performance for small key types
- **Keep data structures small and focused** - Better cache locality when iterating related data
- **Minimize component lookups in hot paths** - Store frequently accessed data in dedicated lookup tables
- **Leverage Burst compilation** - NativeHashMap operations are Burst-compatible for maximum performance

### Entity-as-Key Normalization

The optimal pattern that leverages both React principles and ECS strengths is **Entity-as-Key Normalization**. States store data in NativeHashMaps keyed by Entity, with separate states for different aspects of the data.

#### Example Implementation

```csharp
// ❌ BAD: Embedded data with linear lookups
public struct PartyState : IGameState
{
    public FixedList512Bytes<CharacterData> characters; // Large embedded structs
    
    // Finding a character requires O(n) iteration
    public CharacterData? GetCharacter(Entity entity)
    {
        for (int i = 0; i < characters.Length; i++)
            if (characters[i].entity == entity)
                return characters[i];
        return null;
    }
}

// ✅ GOOD: Normalized with entity lookups
public struct CharacterHealthState : IGameState
{
    public NativeHashMap<Entity, Health> healths;      // O(1) lookup
    public FixedList32Bytes<Entity> aliveCharacters;   // Categorization only
}

public struct CharacterStatsState : IGameState
{
    public NativeHashMap<Entity, Stats> stats;         // Separate concerns
    public FixedList32Bytes<Entity> playerCharacters;  // Just IDs
    public FixedList32Bytes<Entity> enemyCharacters;   // Just IDs
}

// Reducer works with normalized state efficiently
public partial class DamageReducer : StateReducerSystem<CharacterHealthState, DealDamageAction>
{
    protected override void ReduceState(ref CharacterHealthState state, DealDamageAction action)
    {
        // Direct O(1) lookup and update
        if (state.healths.TryGetValue(action.target, out var health))
        {
            health.current = math.max(0, health.current - action.damage);
            state.healths[action.target] = health;
            
            // Update categorization if needed
            if (health.current == 0)
                state.aliveCharacters.Remove(action.target);
        }
    }
}
```

#### Why This Pattern?

1. **Optimal Performance** - O(1) lookups instead of O(n) searches through lists
2. **Single Source of Truth** - Each piece of data exists in exactly one place
3. **Cache-Friendly Updates** - Small focused structures improve memory access patterns
4. **Composable States** - Different aspects can be updated independently by different systems
5. **Natural ECS Integration** - Entity is already the perfect unique identifier
6. **Scalability** - Adding new data aspects doesn't affect existing state structures

## Cross-Reducer State Dependencies

### The Problem

A common challenge in the ECS-React architecture occurs when a reducer processing one action needs information from a different state to make decisions. For example, when `BattleStateReducer` processes a `NextTurnAction`, it needs to know whether the entity in question is a player or enemy character - information that lives in `PartyState`, not `BattleState`.

This creates a dilemma: should reducers read from other states, violating React's pure function principle? Should entities carry their own metadata? Or should actions carry all necessary context?

### React Best Practices for Cross-Reducer State

In React/Redux architecture, reducers must be **pure functions** - they should only depend on their input parameters (current state and action) and produce no side effects. When a reducer needs information from another slice of state, React provides clear patterns:

1. **Action Enrichment** - Actions contain all necessary data for the reducer to make decisions
2. **Selector Pattern** - Compute derived state outside reducers, in the view layer
3. **Saga/Thunk Pattern** - Middleware reads multiple states and dispatches enriched actions

React explicitly discourages reducers from reading other state slices directly. The principle is: **"Actions describe what happened with full context"** - they are complete event records that carry all information needed to update state.

### Unity ECS Performance Best Practices

For optimal ECS performance in hot paths like reducers:

- **Minimize random Entity lookups** - Random access patterns hurt CPU cache performance
- **Keep related data together** - Improves cache locality and reduces memory jumps
- **Prefer data in actions** over entity queries - Avoids runtime lookups in performance-critical code
- **Use tag components sparingly** - While fast, they still require component lookups
- **Batch similar operations** - Process all similar state changes together

### Action Enrichment

The optimal pattern that satisfies both React principles and ECS performance is **Action Enrichment**. The system dispatching an action has the context and should include all necessary information in the action itself.

#### Example Implementation

```csharp
// ❌ BAD: Reducer looks up information from other states
public partial class BattleStateReducer : ReducerSystem<BattleState, NextTurnAction>
{
    protected override void ReduceState(ref BattleState state, NextTurnAction action)
    {
        // Anti-pattern: Reducer reaching out to find information
        var partyState = SceneStateManager.Instance.GetState<PartyState>(); 
        bool isPlayer = LookupIfPlayer(action.nextEntity, partyState);
        state.currentPhase = isPlayer ? BattlePhase.PlayerTurn : BattlePhase.EnemyTurn;
    }
}

// ✅ GOOD: Action carries all necessary context
public struct NextTurnAction : IGameAction
{
    public Entity nextEntity;
    public bool isPlayerTurn;     // Context included in action
    public int turnIndex;          // Any other needed context
}

public partial class BattleStateReducer : ReducerSystem<BattleState, NextTurnAction>
{
    protected override void ReduceState(ref BattleState state, NextTurnAction action)
    {
        // Pure function: only uses state and action parameters
        state.currentPhase = action.isPlayerTurn 
            ? BattlePhase.PlayerTurn 
            : BattlePhase.EnemyTurn;
        state.currentTurnIndex = action.turnIndex;
    }
}
```

#### Why This Pattern?

1. **Maintains Reducer Purity** - Reducers remain pure functions that only depend on their parameters
2. **Optimal Performance** - No lookups or queries in the reducer hot path
3. **Single Source of Truth** - The dispatching system has the context and provides it once
4. **Testability** - Reducers can be unit tested without ECS infrastructure
5. **Clear Data Flow** - Easy to trace where decisions are made and data originates
6. **Scalability** - Pattern extends naturally to complex multi-state scenarios


## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. [Setup](Setup.md)
4. [API](API.md)
5. [Debugging Tools](Debugging.md)
6. [Examples & Patterns](Examples.md)
7. Best Practices
