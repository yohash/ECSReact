namespace ECSReact.Core
{
  /// <summary>
  /// Interface for components that can receive props from parent elements
  /// </summary>
  public interface IElementChild
  {
    void InitializeWithProps(UIProps props);
    void UpdateProps(UIProps props);
  }
}
