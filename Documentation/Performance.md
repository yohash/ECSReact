# Performance Optimization Guide

## Optimization Strategies

### 1. Start Sequential, Profile, Then Optimize

```csharp
// Step 1: Start with simple sequential reducer
[Reducer(DisableBurst = true)]
public struct MyReducer : IReducer<State, Action>
{
    public void Execute(ref State s, in Action a, ref SystemState sys)
    {
        // Simple, readable logic
        s.value += a.delta * sys.WorldUnmanaged.Time.DeltaTime;
    }
}

// Step 2: Profile and identify bottleneck

// Step 3: Convert to Burst if needed
[Reducer]
public struct MyReducer : IReducer<State, Action>
{
    public void Execute(ref State s, in Action a, ref SystemState sys)
    {
        // Simple, readable logic
        s.value += a.delta * sys.WorldUnmanaged.Time.DeltaTime;
    }
}
```

### 2. Batch Similar Operations

Group related reducers for better cache usage:

```csharp
// Instead of separate systems for each physics operation
[Reducer]
public struct PhysicsReducer : IParallelReducer<PhysicsState, PhysicsAction, PhysicsReducer.PhysicsData>
{
    public struct PhysicsData
    {
        public float deltaTime;
        public float3 gravity;
        public float damping;
    }
    
    public void Execute(ref PhysicsState state, in PhysicsAction action, in PhysicsData data)
    {
        // Process all physics in one cache-friendly pass
        switch (action.type)
        {
            case PhysicsActionType.Force:
                state.velocity += action.vector * data.deltaTime;
                break;
            case PhysicsActionType.Impulse:
                state.velocity += action.vector;
                break;
            case PhysicsActionType.Damping:
                state.velocity *= (1f - data.damping * data.deltaTime);
                break;
        }
        
        state.velocity += data.gravity * data.deltaTime;
        state.position += state.velocity * data.deltaTime;
    }
}
```

### 3. Use Unity.Mathematics for SIMD

```csharp
// Burst automatically vectorizes math operations
public void Execute(ref State state, in Action action, in Data data)
{
    // float3 operations use SIMD instructions
    float3 force = action.force + data.gravity;
    float3 acceleration = force / action.mass;
    
    // math library optimized for Burst
    state.velocity += acceleration * data.deltaTime;
    state.position += state.velocity * data.deltaTime;
    
    // Vectorized clamping
    state.position = math.clamp(state.position, data.minBounds, data.maxBounds);
}
```

## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. [Setup](Setup.md)
4. [API](API.md)
5. [Debugging Tools](Debugging.md)
6. [Examples & Patterns](Examples.md)
7. [Best Practices](BestPractices.md)
8. Performance Optimization Guide
