# Performance Optimization Guide

## Overview

ECSReact provides two performance levels for both reducers and middleware:
- **Standard Systems**: Zero allocations, good performance, maximum flexibility
- **Burst Systems**: Zero allocations, 5-10x faster, some restrictions

## Bridge System Architecture

All reducer and middleware systems use generated bridges that provide:
- **Zero allocations** via `SystemAPI.Query` instead of `ToEntityArray`
- **Linear memory access** for cache-friendly iteration
- **Automatic system disabling** to prevent double execution

## Performance Comparison

### Benchmark: 1000 Actions/Frame, 300 Reducers

| System Type | Time/Frame | Allocations | Use Case |
|------------|------------|-------------|-----------|
| Original (No Bridges) | 12ms | 1000/frame | ❌ Not recommended |
| Standard Reducer | 8ms | 0/frame | ✅ General logic |
| Burst Reducer | 2ms | 0/frame | 🚀 Hot paths |

## When to Use Burst Systems

### ✅ Perfect for Burst

- Physics simulations
- Particle systems  
- Damage calculations
- Movement/steering
- Procedural generation
- Mathematical operations
- High-frequency validation

### ❌ Use Standard Instead

- File I/O operations
- Network communication
- Unity API calls
- Debug logging
- String manipulation
- Complex business logic
- Async operations

## Migration Guide

### Converting Standard → Burst Reducer

**Before (Standard):**
```csharp
[ReducerSystem]
public partial class PhysicsReducer : ReducerSystem<PhysicsState, ForceAction>
{
    public override void ReduceState(ref PhysicsState state, ForceAction action)
    {
        state.velocity += action.force / state.mass * action.deltaTime;
        state.position += state.velocity * action.deltaTime;
    }
}
```

**After (Burst - 10x faster):**
```csharp
[ReducerSystem]
public partial class PhysicsReducer : BurstReducerSystem<PhysicsState, ForceAction, PhysicsReducer.Logic>
{
    [BurstCompile]
    public struct Logic : IBurstReducer<PhysicsState, ForceAction>
    {
        public void Execute(ref PhysicsState state, in ForceAction action)
        {
            // Exact same logic, just in a struct!
            state.velocity += action.force / state.mass * action.deltaTime;
            state.position += state.velocity * action.deltaTime;
        }
    }
}
```

### Converting Standard → Burst Middleware

**Before (Standard):**
```csharp
[MiddlewareSystem]
public partial class ValidationMiddleware : MiddlewareSystem<InputAction>
{
    public override void ProcessAction(InputAction action, Entity entity)
    {
        if (!IsValidInput(action))
        {
            // Can dispatch new actions
            DispatchAction(new InvalidInputAction());
        }
    }
}
```

**After (Burst - 5x faster):**
```csharp
[MiddlewareSystem]
public partial class ValidationMiddleware : BurstMiddlewareSystem<InputAction, ValidationMiddleware.Logic>
{
    [BurstCompile]
    public struct Logic : IBurstMiddleware<InputAction>
    {
        public void Execute(in InputAction action, Entity entity)
        {
            // Validation only - cannot dispatch actions from Burst
            bool isValid = math.length(action.movement) <= 1f;
            // Would need to mark entity with invalid component instead
        }
    }
}
```

## Profiling Your Optimizations

### Using Unity Profiler

1. Open **Window → Analysis → Profiler**
2. Enable **Deep Profile** for detailed timing
3. Look for your reducer systems
4. Compare before/after Burst conversion:
   - Standard: `GameReducer_DamageAction_Bridge.OnUpdate`: 0.5ms
   - Burst: `PhysicsReducer_ForceAction_Bridge.OnUpdate`: 0.05ms

### Key Metrics to Track

- **Frame Time**: Target < 16.67ms for 60fps
- **GC Allocations**: Should be 0 in reducer hot path
- **Cache Misses**: Lower with burst (linear access)
- **System Count**: 600+ systems is fine with bridges

## Best Practices

### 1. Start Simple
- Begin with standard reducers
- Profile to find bottlenecks
- Convert only hot paths to Burst

### 2. Batch Similar Operations
- Group math-heavy reducers together
- Convert them to Burst as a set
- Share logic structs when possible

### 3. Use Unity.Mathematics
```csharp
// ❌ Slower
Vector3 position = new Vector3(x, y, z);

// ✅ Faster with Burst
float3 position = new float3(x, y, z);
```

### 4. Profile Everything
- Measure before optimization
- Measure after optimization
- Only keep changes that matter

## Common Pitfalls

### ❌ Don't Burst Everything
Not everything benefits from Burst. Simple property updates may be fast enough already.

### ❌ Avoid Managed Types in Burst
```csharp
// This won't compile in Burst
string message = $"Damage: {damage}";  // ❌ No strings
List<int> items = new List<int>();     // ❌ No managed collections
Debug.Log("Processing");                // ❌ No Unity APIs
```

### ✅ Use Unmanaged Alternatives
```csharp
// Burst-compatible alternatives
FixedString32Bytes message;            // ✅ Fixed string
FixedList32Bytes<int> items;           // ✅ Fixed list
// Logging must be done outside Burst
```

## Performance Checklist

- [ ] Run code generation to create bridges
- [ ] Profile baseline performance
- [ ] Identify hot-path reducers (run most frequently)
- [ ] Convert hot-path reducers to Burst
- [ ] Profile again to verify improvement
- [ ] Document which systems use Burst and why

## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. [Setup](Setup.md)
4. [API](API.md)
5. [Debugging Tools](Debugging.md)
6. [Examples & Patterns](Examples.md)
7. [Best Practices](BestPractices.md)
8. Performance Optimization Guide
