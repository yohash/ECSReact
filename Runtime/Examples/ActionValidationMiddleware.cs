using Unity.Entities;

namespace ECSReact.Core
{
  /// <summary>
  /// Example middleware that validates actions and can reject them.
  /// Shows how middleware can dispatch error actions for invalid requests.
  /// </summary>
  public partial class ActionValidationMiddleware<T> : MiddlewareSystem<T>
      where T : unmanaged, IGameAction, IValidatable
  {
    protected override void ProcessAction(T action, Entity actionEntity)
    {
      if (!action.IsValid()) {
        // Dispatch a validation error action
        DispatchAction(new ValidationErrorAction
        {
          originalActionType = typeof(T).Name,
          errorMessage = action.GetValidationError()
        });

        // Mark the original action as invalid (could add a component)
        EntityManager.AddComponent<InvalidActionTag>(actionEntity);
      }
    }
  }

  /// <summary>
  /// Optional interface for actions that support validation.
  /// Implement this on your action structs to enable validation middleware.
  /// </summary>
  public interface IValidatable
  {
    bool IsValid();
    string GetValidationError();
  }

  /// <summary>
  /// Tag component to mark invalid actions that should be ignored by reducers.
  /// </summary>
  public struct InvalidActionTag : IComponentData { }

  /// <summary>
  /// Action dispatched when validation fails.
  /// Can be processed by UI systems to show error messages.
  /// </summary>
  public struct ValidationErrorAction : IGameAction
  {
    public Unity.Collections.FixedString128Bytes originalActionType;
    public Unity.Collections.FixedString512Bytes errorMessage;
  }
}
