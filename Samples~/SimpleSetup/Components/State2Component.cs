using ECSReact.Core;
using ECSReact.Samples.SimpleSetup.Namespace2.State;
using TMPro;

namespace ECSReact.Samples.SimpleSetup.Components
{
  public class State2Component : ReactiveUIComponent<StateNamespace2>
  {
    public TextMeshProUGUI CountText;

    public override void OnStateChanged(StateNamespace2 newState)
    {
      CountText.text = newState.Count.ToString();
    }
  }
}