
# Examples & Patterns

## Basic Game Loop

```csharp
// State
public struct GameState : IGameState, IEquatable<GameState>
{
    public int score;
    public bool gameActive;
    public float timeRemaining;
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
public class ScoreDisplay : SingleStateUIComponent<GameState>
{
    public override void OnStateChanged(GameState newState)
    {
        scoreText.text = $"Score: {newState.score}";
    }
}
```

## Validation Middleware

```csharp
public partial class PurchaseValidation : MiddlewareSystem<BuyItemAction>
{
    protected override void ProcessAction(BuyItemAction action, Entity entity)
    {
        var gameState = SystemAPI.GetSingleton<GameState>();

        if (gameState.currency < action.cost)
        {
            DispatchAction(new ShowErrorAction { message = "Insufficient funds" });
            EntityManager.AddComponent<InvalidActionTag>(entity);
        }
    }
}
```

## Async Operations

```csharp
public partial class SaveGameMiddleware : MiddlewareSystem<SaveGameAction>
{
    protected override void ProcessAction(SaveGameAction action, Entity entity)
    {
        // Immediate feedback
        DispatchAction(new SaveStartedAction { fileName = action.fileName });

        // Fire-and-forget async operation
        _ = Task.Run(async () =>
        {
            await PerformSaveAsync(action.fileName);
            // Queue completion result for main thread
            QueueCompletionResult(action.fileName, success: true);
        });
    }
}
```
