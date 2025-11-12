using ECSReact.Core;
using ECSReact.Samples.SimpleSetup.Namespace1.State;
using TMPro;

namespace ECSReact.Samples.SimpleSetup.Components
{
  public class State1Component : ReactiveUIComponent<StateNamespace1>
  {
    public TextMeshProUGUI ValueText;

    public override void OnStateChanged(StateNamespace1 newState)
    {
      ValueText.text = newState.Value.ToString();
    }
  }
}