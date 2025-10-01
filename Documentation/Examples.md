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

## Reducer Patterns

<details>
<summary>Sequential Reducer with Entity Creation</summary>

```csharp
[Reducer(DisableBurst = true)]  // Required for EntityManager access
public struct AddCharacterReducer : IReducer<PartyState, AddCharacterAction>
{
    public void Execute(ref PartyState state, in AddCharacterAction action, ref SystemState systemState)
    {
        // Create entity (requires DisableBurst = true)
        var entity = systemState.EntityManager.CreateEntity();
        
        // Create character data
        var character = new CharacterData
        {
            entity = entity,
            name = action.name,
            maxHealth = action.maxHealth,
            currentHealth = action.maxHealth,
            isEnemy = action.isEnemy,
            isAlive = true
        };
        
        state.characters.Add(character);
        
        // Update counters
        if (action.isEnemy)
        {
            state.enemyCount++;
            state.aliveEnemyCount++;
        }
        else
        {
            state.activePartySize++;
            state.aliveCount++;
        }
    }
}
```
</details>

<details>
<summary>Parallel Reducer for Physics Calculations</summary>

```csharp
[Reducer]  // Burst-compiled for maximum performance
public struct PhysicsReducer : IParallelReducer<PhysicsState, ForceAction, PhysicsReducer.FrameData>
{
    public struct FrameData
    {
        public float deltaTime;
        public float3 gravity;
        public float airResistance;
        public ComponentLookup<Mass> massLookup;
        public ComponentLookup<Drag> dragLookup;
    }
    
    public FrameData PrepareData(ref SystemState systemState)
    {
        // Fetch all needed data ONCE per frame
        var config = systemState.GetSingleton<PhysicsConfig>();
        
        return new FrameData
        {
            deltaTime = systemState.WorldUnmanaged.Time.DeltaTime,
            gravity = config.gravity,
            airResistance = config.airResistance,
            massLookup = SystemAPI.GetComponentLookup<Mass>(true),
            dragLookup = SystemAPI.GetComponentLookup<Drag>(true)
        };
    }
    
    public void Execute(ref PhysicsState state, in ForceAction action, in FrameData data)
    {
        // Pure parallel computation - 10-100x faster than sequential
        var mass = data.massLookup[action.targetEntity];
        var drag = data.dragLookup.HasComponent(action.targetEntity) 
            ? data.dragLookup[action.targetEntity].value 
            : 0f;
        
        // Apply forces
        var acceleration = action.force / mass.value;
        acceleration += data.gravity;
        
        // Apply drag
        state.velocity *= (1f - drag * data.airResistance * data.deltaTime);
        
        // Update state
        state.velocity += acceleration * data.deltaTime;
        state.position += state.velocity * data.deltaTime;
    }
}
```
</details>

<details>
<summary>Batch Processing Pattern</summary>

```csharp    
[Reducer]
public struct BulletReducer : IParallelReducer<BulletState, UpdateBulletsAction, BulletReducer.FrameData>
{
    public struct FrameData
    {
        public float deltaTime;
        public float3 worldBounds;
        public ComponentLookup<Transform> transformLookup;
    }
    
    public FrameData PrepareData(ref SystemState systemState)
    {
        var config = systemState.GetSingleton<WorldConfig>();
        return new FrameData
        {
            deltaTime = systemState.WorldUnmanaged.Time.DeltaTime,
            worldBounds = config.bounds,
            transformLookup = SystemAPI.GetComponentLookup<Transform>(false) // writable
        };
    }
    
    public void Execute(ref BulletState state, in UpdateBulletsAction action, in FrameData data)
    {
        // Process hundreds of bullets in parallel
        for (int i = 0; i < state.activeBullets.Length; i++)
        {
            ref var bullet = ref state.activeBullets[i];
            
            // Update position
            bullet.position += bullet.velocity * data.deltaTime;
            
            // Check bounds
            if (math.any(math.abs(bullet.position) > data.worldBounds))
            {
                bullet.isActive = false;
                state.activeCount--;
            }
            
            // Update visual transform if entity exists
            if (data.transformLookup.HasComponent(bullet.entity))
            {
                data.transformLookup[bullet.entity] = new Transform
                {
                    position = bullet.position,
                    rotation = quaternion.LookRotation(bullet.velocity, math.up())
                };
            }
        }
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
