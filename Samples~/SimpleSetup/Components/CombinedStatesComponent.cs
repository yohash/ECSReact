using ECSReact.Core;
using ECSReact.Samples.SimpleSetup.Namespace1.State;
using ECSReact.Samples.SimpleSetup.Namespace2.State;
using TMPro;

namespace ECSReact.Samples.SimpleSetup.Components
{
  public class CombinedStatesComponent : ReactiveUIComponent<StateNamespace1, StateNamespace2>
  {
    public TextMeshProUGUI State1_ResetText;
    public TextMeshProUGUI State2_StartText;

    public override void OnStateChanged(StateNamespace1 newState)
    {
      State1_ResetText.text = newState.WasReset.ToString();
    }

    public override void OnStateChanged(StateNamespace2 newState)
    {
      State2_StartText.text = newState.IsStarted.ToString();
    }
  }
}