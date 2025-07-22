namespace ECSReact.Core
{
  /// <summary>
  /// Base class for props passed between UI components
  /// </summary>
  public class UIProps
  {
    public static readonly UIProps Empty = new UIProps();
    public virtual UIProps Clone() => MemberwiseClone() as UIProps;
  }
}
