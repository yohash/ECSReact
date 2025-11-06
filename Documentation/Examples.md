# Examples & Patterns

<details>
<summary>Basic Game Loop</summary>
    
```csharp
// State
public struct GameState : IGameState, IEquatable<GameState>
{
    public int score;
    public bool gameActive;
    public float timeRemaining;

    public bool Equals(GameState other) =>
        score == other.score && gameActive == other.gameActive &&
        Math.Abs(timeRemaining - other.timeRemaining) < 0.01f;
}

// Actions    
public struct AddScoreAction : IGameAction { public int points; }
public struct StartGameAction : IGameAction { public float duration; }

// Reducer
public partial class GameReducer : StateReducerSystem<GameState, AddScoreAction>
{
    protected override void ReduceState(ref GameState state, AddScoreAction action)
    {
        if (state.gameActive) state.score += action.points;
    }
}

// UI
public class ScoreDisplay : ReactiveUIComponent<GameState>
{
    [SerializeField] private Text scoreText;

    public override void OnStateChanged(GameState newState)
    {
        scoreText.text = $"Score: {newState.score}";
    }
}
```
</details>

<details>
<summary>Nested Component Composition</summary>

```csharp
// Top-level game container
public class GameContainer : ReactiveUIComponent<GameState>
{
    protected override IEnumerable<UIElement> DeclareElements()
    {
        // Create major UI sections, each with their own children
        yield return UIElement.FromComponent<TopBarContainer>("top_bar");
        yield return UIElement.FromComponent<GameContentContainer>("content");
        yield return UIElement.FromComponent<BottomBarContainer>("bottom_bar");
    }
}

// Content area that itself manages children
public class GameContentContainer : ReactiveUIComponent<GameState, UIState>
{
    private GameState gameState;
    private UIState uiState;

    public override void OnStateChanged(GameState newState)
    {
        gameState = newState;
        UpdateElements();
    }

    public override void OnStateChanged(UIState newState)
    {
        uiState = newState;
        UpdateElements();
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
        // Main game view
        yield return UIElement.FromComponent<GameWorldView>("world_view");

        // Overlay panels - each can have their own children
        if (uiState.showCharacterSheet)
        {
            yield return UIElement.FromComponent<CharacterSheetPanel>("character_sheet");
        }

        if (uiState.showInventory)
        {
            yield return UIElement.FromComponent<InventoryContainer>("inventory_container");
        }

        // Modal dialogs always on top
        if (uiState.activeDialog != DialogType.None)
        {
            yield return UIElement.FromComponent<DialogContainer>(
                key: "dialog",
                index: 999
            );
        }
    }
}

// Even leaf components can have dynamic children
public class CharacterSheetPanel : ReactiveUIComponent<PlayerState>
{
    protected override IEnumerable<UIElement> DeclareElements()
    {
        // Static sections
        yield return UIElement.FromComponent<AttributesSection>("attributes");
        yield return UIElement.FromComponent<SkillsSection>("skills");

        // Dynamic equipment slots
        var equipment = GetPlayerEquipment();
        foreach (var slot in equipment.slots)
        {
            yield return UIElement.FromComponent<EquipmentSlot>(
                key: $"equipment_{slot.type}",
                props: new EquipmentSlotProps { SlotType = slot.type, Item = slot.item }
            );
        }
    }
}
```
</details>

## Dispatching From ECS Context

There are three recommended patterns for dispatching actions from within ECS systems, based on your execution context and performance requirements.

<details>
<summary>Main-Thread, Non-Burst</summary>

### Direct Dispatch Pattern
 
Use `ECSActionDispatcher.Dispatch()` directly when you're in a non-Burst context and don't need parallel processing. This is the simplest approach for systems that run on the main thread.

```csharp
// Simple system that dispatches actions based on game logic
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HealthMonitorSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Direct query and dispatch - not Burst compiled
        foreach (var (health, entity) in 
            SystemAPI.Query<RefRO<Health>>()
                .WithEntityAccess())
        {
            if (health.ValueRO.current <= 0 && health.ValueRO.current > -100)
            {
                // Mark as processed to avoid repeated dispatches
                EntityManager.SetComponentData(entity, new Health 
                { 
                    current = -100, 
                    max = health.ValueRO.max 
                });
                
                // Dispatch death action directly
                ECSActionDispatcher.Dispatch(new EntityDeathAction
                {
                    entity = entity,
                    timestamp = (float)SystemAPI.Time.ElapsedTime
                });
            }
        }
    }
}

// Another example: Spawning system
public partial class WaveSpawnerSystem : SystemBase
{
    private float nextSpawnTime;
    
    protected override void OnUpdate()
    {
        if (SystemAPI.Time.ElapsedTime >= nextSpawnTime)
        {
            var config = SystemAPI.GetSingleton<WaveConfig>();
            
            // Dispatch spawn action for each enemy in wave
            for (int i = 0; i < config.enemiesPerWave; i++)
            {
                ECSActionDispatcher.Dispatch(new SpawnEnemyAction
                {
                    prefabId = config.enemyPrefabId,
                    position = CalculateSpawnPosition(i),
                    level = config.currentWaveLevel
                });
            }
            
            nextSpawnTime = (float)SystemAPI.Time.ElapsedTime + config.waveInterval;
        }
    }
    
    private float3 CalculateSpawnPosition(int index)
    {
        // Calculate spawn position logic
        return new float3(index * 2f, 0, 10f);
    }
}
```
</details>

<details>
<summary>Main-Thread, Burst</summary>

### Pre-fetch ECB Pattern
    
For Burst-compiled systems that need to dispatch actions, pre-fetch the `EntityCommandBuffer.ParallelWriter` before entering Burst context, then use it within your Burst-compiled methods.

```csharp
[BurstCompile]
public partial struct CollisionResponseSystem : ISystem
{
    // Store the ECB writer as a field
    private EntityCommandBuffer.ParallelWriter ecbWriter;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        // CRITICAL: Get the ECB BEFORE entering Burst context
        // This happens on main thread before Burst compilation kicks in
        ecbWriter = ECSActionDispatcher.GetJobCommandBuffer(state.World);
        
        // Now we can use it in Burst-compiled code
        ProcessCollisions(ref state);
        
        // Register dependency for proper synchronization
        ECSActionDispatcher.RegisterJobHandle(state.Dependency, state.World);
    }

    [BurstCompile]
    private void ProcessCollisions(ref SystemState state)
    {
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        
        // Process collision events - fully Burst compiled
        foreach (var (velocity, transform, entity) in 
            SystemAPI.Query<RefRW<Velocity>, RefRO<LocalTransform>>()
                .WithEntityAccess())
        {
            // Raycast to check for collisions
            var rayInput = new RaycastInput
            {
                Start = transform.ValueRO.Position,
                End = transform.ValueRO.Position + velocity.ValueRO.value * 0.1f,
                Filter = CollisionFilter.Default
            };
            
            if (physicsWorld.CastRay(rayInput, out var hit))
            {
                // Dispatch collision action using the pre-fetched ECB
                int sortKey = entity.Index; // Use entity index as sort key
                
                ecbWriter.DispatchAction(sortKey, new CollisionAction
                {
                    entity1 = entity,
                    entity2 = hit.Entity,
                    impactPoint = hit.Position,
                    impactNormal = hit.SurfaceNormal,
                    relativeVelocity = velocity.ValueRO.value
                });
                
                // Reflect velocity
                velocity.ValueRW.value = math.reflect(velocity.ValueRO.value, hit.SurfaceNormal);
            }
        }
    }
}

// Another example: Damage over time system
[BurstCompile]
public partial struct DamageOverTimeSystem : ISystem
{
    private EntityCommandBuffer.ParallelWriter ecbWriter;
    public void OnUpdate(ref SystemState state)
    {
        // Pre-fetch ECB
        ecbWriter = ECSActionDispatcher.GetJobCommandBuffer(state.World);
    
        float deltaTime = state.WorldUnmanaged.Time.DeltaTime;
        float currentTime = (float)state.WorldUnmanaged.Time.ElapsedTime;
    
        // Process all DoT effects in Burst
        ProcessDoTEffects(ref state, deltaTime, currentTime);
    
        ECSActionDispatcher.RegisterJobHandle(state.Dependency, state.World);
    }

    [BurstCompile]
    private void ProcessDoTEffects(ref SystemState state, float deltaTime, float currentTime)
    {
        int sortKey = 0;
        
        foreach (var (dot, entity) in 
            SystemAPI.Query<RefRW<DamageOverTime>>()
                .WithEntityAccess())
        {    
            if (currentTime >= dot.ValueRO.nextTickTime)
            {
                // Dispatch damage action
                ecbWriter.DispatchAction(sortKey++, new DamageAction
                {
                    targetEntity = entity,
                    amount = dot.ValueRO.damagePerTick,    
                    source = dot.ValueRO.sourceEntity,
                    damageType = DamageType.Poison
                });
            
                // Update next tick time    
                dot.ValueRW.nextTickTime = currentTime + dot.ValueRO.tickInterval;
                dot.ValueRW.remainingTicks--;
            
                // If expired, dispatch removal action
                if (dot.ValueRO.remainingTicks <= 0)
                {
                    ecbWriter.DispatchAction(sortKey++, new RemoveEffectAction
                    {
                        entity = entity,
                        effectType = EffectType.DamageOverTime
                    });
                }
            }
        }
    }
}
```
</details>

<details>
<summary>Parallel-Thread, Burst</summary>

### Parallel Job Dispatch Pattern
    
For maximum performance with parallel jobs, pass the `EntityCommandBuffer.ParallelWriter` to your job and use the extension methods to dispatch actions from within the job.

```csharp
[BurstCompile]
public partial struct AIDecisionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AIConfig>();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<AIConfig>();
        var ecb = ECSActionDispatcher.GetJobCommandBuffer(state.World);
        
        // Schedule parallel job for AI decisions
        var jobHandle = new AIDecisionJob
        {
            ECB = ecb,
            DeltaTime = state.WorldUnmanaged.Time.DeltaTime,
            CurrentTime = (float)state.WorldUnmanaged.Time.ElapsedTime,
            DetectionRange = config.detectionRange,
            AttackCooldown = config.attackCooldown,
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            HealthLookup = SystemAPI.GetComponentLookup<Health>(true)
        }.ScheduleParallel(state.Dependency);
        
        // Register the job handle for synchronization
        ECSActionDispatcher.RegisterJobHandle(jobHandle, state.World);
        state.Dependency = jobHandle;
    }
    
    [BurstCompile]
    private partial struct AIDecisionJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        public float DeltaTime;
        public float CurrentTime;
        public float DetectionRange;
        public float AttackCooldown;
        
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        
        public void Execute(
            [EntityIndexInQuery] int index,
            Entity entity,
            ref AIState ai,
            in LocalTransform transform)
        {
            // Find nearest enemy
            Entity nearestEnemy = FindNearestEnemy(transform.Position);
            
            if (nearestEnemy != Entity.Null)
            {
                float distance = math.distance(
                    transform.Position,
                    TransformLookup[nearestEnemy].Position
                );
                
                // Dispatch different actions based on AI state and distance
                if (distance <= DetectionRange)
                {
                    if (distance <= 2f && CurrentTime >= ai.nextAttackTime)
                    {
                        // Close enough to attack
                        ECB.DispatchAction(index, new AttackAction
                        {
                            attacker = entity,
                            target = nearestEnemy,
                            damage = ai.attackDamage
                        });
                        
                        ai.nextAttackTime = CurrentTime + AttackCooldown;
                    }
                    else
                    {
                        // Move towards enemy
                        ECB.DispatchAction(index, new MoveToTargetAction
                        {
                            entity = entity,
                            targetEntity = nearestEnemy,
                            speed = ai.moveSpeed
                        });
                    }
                    
                    ai.currentTarget = nearestEnemy;
                }
                else if (ai.currentTarget != Entity.Null)
                {
                    // Lost target, dispatch search action
                    ECB.DispatchAction(index, new SearchForTargetAction
                    {
                        entity = entity,
                        lastKnownPosition = TransformLookup[ai.currentTarget].Position
                    });
                    
                    ai.currentTarget = Entity.Null;
                }
            }
        }
        
        private Entity FindNearestEnemy(float3 position)
        {
            // Simplified - in real implementation, use spatial queries
            // This is just to demonstrate the pattern
            return Entity.Null;
        }
    }
}

// Another example: Batch processing pattern
[BurstCompile]
public partial struct ProjectileSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = ECSActionDispatcher.GetJobCommandBuffer(state.World);
        var deltaTime = state.WorldUnmanaged.Time.DeltaTime;
        
        // Process all projectiles in parallel
        var moveHandle = new MoveProjectilesJob
        {
            DeltaTime = deltaTime
        }.ScheduleParallel(state.Dependency);
        
        // Check for impacts in parallel
        var impactHandle = new CheckProjectileImpactsJob
        {
            ECB = ecb,
            TerrainHeight = 0f, // Ground level
            MaxDistance = 100f
        }.ScheduleParallel(moveHandle);
        
        ECSActionDispatcher.RegisterJobHandle(impactHandle, state.World);
        state.Dependency = impactHandle;
    }
    
    [BurstCompile]
    private partial struct MoveProjectilesJob : IJobEntity
    {
        public float DeltaTime;
        
        public void Execute(
            ref LocalTransform transform,
            ref Velocity velocity,
            in Projectile projectile)
        {
            // Apply gravity
            velocity.value.y -= 9.81f * DeltaTime;
            
            // Update position
            transform.Position += velocity.value * DeltaTime;
            
            // Rotate to face direction
            if (math.lengthsq(velocity.value) > 0.01f)
            {
                transform.Rotation = quaternion.LookRotation(
                    math.normalize(velocity.value),
                    math.up()
                );
            }
        }
    }
    
    [BurstCompile]
    private partial struct CheckProjectileImpactsJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        public float TerrainHeight;
        public float MaxDistance;
        
        public void Execute(
            [EntityIndexInQuery] int index,
            Entity entity,
            in LocalTransform transform,
            in Projectile projectile)
        {
            // Check ground impact
            if (transform.Position.y <= TerrainHeight)
            {
                ECB.DispatchAction(index * 2, new ProjectileImpactAction
                {
                    projectile = entity,
                    impactPoint = new float3(transform.Position.x, TerrainHeight, transform.Position.z),
                    impactType = ImpactType.Terrain,
                    damage = projectile.damage
                });
                
                ECB.DispatchAction(index * 2 + 1, new DestroyEntityAction
                {
                    entity = entity,
                    reason = DestroyReason.Impact
                });
            }
            // Check max range
            else if (math.length(transform.Position) > MaxDistance)
            {
                ECB.DispatchAction(index, new DestroyEntityAction
                {
                    entity = entity,
                    reason = DestroyReason.OutOfRange
                });
            }
        }
    }
}
```
</details>

<details>
<summary>Key Patterns Summary</summary>


    
| Context | Burst | Method | Use Case |
| --- | --- | --- | --- |
| Main Thread | No |  ECSActionDispatcher.Dispatch() | Simple systems, UI  integration | 
| Main Thread | Yes | Pre-fetch ECB + ECB.DispatchAction() | High-performance single-threaded | 
| Job Thread | Yes | Pass ECB to job + ECB.DispatchAction() | Maximum throughput, parallel processing | 

#### Important Notes:

- Always call `GetJobCommandBuffer()` from main thread before Burst context
- Use unique sort keys for ParallelWriter (entity index, query index, etc.)
- Register job handles with `RegisterJobHandle()` for proper synchronization
- The extension methods in `ECBActionExtensions` provide one-line dispatch
</details>

## Elements and Props

<details>
<summary>Props-Based Communication</summary>

```csharp
// Complex props with nested data
public class PlayerStatusProps : UIProps
{
    public string PlayerName { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Level { get; set; }
    public float Experience { get; set; }
    public List<StatusEffect> ActiveEffects { get; set; }
}

// Parent creates child with rich props
public class PlayerHUD : ReactiveUIComponent<PlayerState>
{
    private PlayerState currentState;
    
    public override void OnStateChanged(PlayerState newState)
    {
        currentState = newState;
        UpdateElements();
    }
    
    protected override IEnumerable<UIElement> DeclareElements()
    {
        // Main player status display
        yield return UIElement.FromComponent<PlayerStatusDisplay>(
            key: "player_status",
            props: new PlayerStatusProps
            {
                PlayerName = currentState.playerName.ToString(),
                Health = currentState.health,
                MaxHealth = currentState.maxHealth,
                Level = currentState.level,
                Experience = currentState.experience,
                ActiveEffects = GetActiveEffects()
            }
        );
        
        // Conditional elements based on state
        if (currentState.isInCombat)
        {
            yield return UIElement.FromComponent<CombatActionBar>("combat_actions");
        }
        
        if (currentState.hasUnreadMessages)
        {
            yield return UIElement.FromComponent<MessageNotification>("messages");
        }
    }
    
    private List<StatusEffect> GetActiveEffects()
    {
        // Convert from native arrays or other data structures
        return currentState.statusEffects.ToArray().ToList();
    }
}
```
</details>

<details>
<summary>Conditional UI Elements</summary>

```csharp
public class GameMenuSystem : ReactiveUIComponent<GameState, UIState>
{
    private GameState gameState;
    private UIState uiState;
    
    public override void OnStateChanged(GameState newState)
    {
        gameState = newState;
        UpdateElements();
    }
    
    public override void OnStateChanged(UIState newState)
    {
        uiState = newState;
        UpdateElements();
    }
    
    protected override IEnumerable<UIElement> DeclareElements()
    {
        // Always show main header
        yield return UIElement.FromComponent<GameHeader>("header");
        
        // Show different panels based on game state
        if (gameState.isInMainMenu)
        {
            yield return UIElement.FromComponent<MainMenuPanel>("main_menu");
        }
        else if (gameState.isInGame)
        {
            yield return UIElement.FromComponent<GameplayHUD>("gameplay_hud");
            
            // Conditional sub-panels
            if (gameState.isInCombat)
            {
                yield return UIElement.FromComponent<CombatInterface>("combat");
            }
            else if (gameState.canCraft)
            {
                yield return UIElement.FromComponent<CraftingPanel>("crafting");
            }
        }
        else if (gameState.isPaused)
        {
            yield return UIElement.FromComponent<PauseMenu>("pause_menu");
        }
        
        // UI state driven elements
        if (uiState.showInventory)
        {
            yield return UIElement.FromComponent<InventoryPanel>("inventory");
        }
        
        if (uiState.showSettings)
        {
            yield return UIElement.FromComponent<SettingsPanel>("settings");
        }
        
        // Always show notifications at the top layer
        if (uiState.notifications.Length > 0)
        {
            yield return UIElement.FromComponent<NotificationOverlay>(
                key: "notifications", 
                index: 1000 // Force to top
            );
        }
    }
}
```
</details>

<details>
<summary>Dynamic Element Composition</summary>

```csharp
// Inventory state with items
public struct InventoryState : IGameState, IEquatable<InventoryState>
{
    public NativeArray<ItemData> items;
    public int selectedSlot;

    public bool Equals(InventoryState other) =>
        items.SequenceEqual(other.items) && selectedSlot == other.selectedSlot;
}

// Props for passing item data to children
public class ItemDisplayProps : UIProps
{
    public string ItemName { get; set; }
    public int ItemCount { get; set; }
    public Sprite ItemIcon { get; set; }
    public bool IsSelected { get; set; }
}

// Parent component that creates child elements per item
public class InventoryPanel : ReactiveUIComponent<InventoryState>
{
    private InventoryState currentState;

    public override void OnStateChanged(InventoryState newState)
    {
        currentState = newState;
        UpdateElements(); // Trigger element reconciliation
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
        if (!currentState.items.IsCreated) yield break;

        // Create an element for each inventory item
        for (int i = 0; i < currentState.items.Length; i++)
        {
            var item = currentState.items[i];
            yield return UIElement.FromPrefab(
                key: $"item_{item.id}",
                prefabPath: "UI/InventorySlot",
                props: new ItemDisplayProps
                {
                    ItemName = item.name.ToString(),
                    ItemCount = item.stackCount,
                    ItemIcon = GetItemIcon(item.id),
                    IsSelected = i == currentState.selectedSlot
                },
                index: i
            );
        }

        // Show empty message when no items
        if (currentState.items.Length == 0)
        {
            yield return UIElement.FromComponent<EmptyInventoryMessage>(
                key: "empty_message"
            );
        }
    }

    private Sprite GetItemIcon(int itemId) => Resources.Load<Sprite>($"Icons/Item_{itemId}");
}

// Child component that receives props from parent
public class InventorySlotDisplay : ReactiveUIComponent<InventoryState>, IElement
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text countText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject selectedBorder;

    private ItemDisplayProps itemProps;

    public void InitializeWithProps(UIProps props)
    {
        itemProps = props as ItemDisplayProps;
        UpdateDisplay();
    }

    public void UpdateProps(UIProps props)
    {
        itemProps = props as ItemDisplayProps;
        UpdateDisplay();
    }

    public override void OnStateChanged(InventoryState newState)
    {
        // Can respond to global inventory changes if needed
        // Props updates are handled separately
    }

    private void UpdateDisplay()
    {
        if (itemProps == null) return;

        nameText.text = itemProps.ItemName;
        countText.text = itemProps.ItemCount > 1 ? itemProps.ItemCount.ToString() : "";
        iconImage.sprite = itemProps.ItemIcon;
        selectedBorder.SetActive(itemProps.IsSelected);
    }

    public void OnSlotClicked()
    {
        // Dispatch action when slot is clicked
        DispatchAction(new SelectItemSlotAction { slotIndex = transform.GetSiblingIndex() });
    }
}
```
</details>

## Next

1. [Overview](Overview.md)
2. [Architecture](Architecture.md)
3. [Setup](Setup.md)
4. [API](API.md)
5. [Debugging Tools](Debugging.md)
6. Examples & Patterns
7. [Best Practices](BestPractices.md)
8. [Performance Optimization Guide](Performance.md)
