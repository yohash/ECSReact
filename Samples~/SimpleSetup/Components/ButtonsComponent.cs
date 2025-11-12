using ECSReact.Core;
using ECSReact.Samples.SimpleSetup.Namespace1.State;
using ECSReact.Samples.SimpleSetup.Namespace1.Actions;
using ECSReact.Samples.SimpleSetup.Namespace2.Actions;
using UnityEngine.UI;
using TMPro;

namespace ECSReact.Samples.SimpleSetup.Components
{
  public class ButtonsComponent : ReactiveUIComponent<StateNamespace1>
  {
    public Button ActionIntNamespace1_Button;
    public Button ActionResetNamespace1_Button;
    public Button ActionIterateNamespace2_Button;
    public Button ActionStartNamespace2_Button;

    public TMP_InputField ActionInt_InputField;

    private void Awake()
    {
      ActionIntNamespace1_Button.onClick.AddListener(IntNamespace1_OnClick);
      ActionResetNamespace1_Button.onClick.AddListener(ResetNamespace1_OnClick);
      ActionIterateNamespace2_Button.onClick.AddListener(IterateNamespace2_OnClick);
      ActionStartNamespace2_Button.onClick.AddListener(StartNamespace2_OnClick);

      ActionInt_InputField.onValueChanged.AddListener(InputField_OnValueChanged);
      ActionIntNamespace1_Button.interactable = false;
    }

    public override void OnStateChanged(StateNamespace1 newState)
    {
      if (newState.WasReset) {
        ActionInt_InputField.text = 0.ToString();
      }
    }

    private void InputField_OnValueChanged(string arg)
    {
      ActionIntNamespace1_Button.interactable = !string.IsNullOrWhiteSpace(arg);
    }

    private void IntNamespace1_OnClick()
    {
      if (int.TryParse(ActionInt_InputField.text, out int value)) {
        DispatchAction(new ActionIntNamespace1() { Amount = value });
      } else {
        UnityEngine.Debug.LogError("Cannot parse an int from input value: " + ActionInt_InputField.text);
      }
    }
    private void ResetNamespace1_OnClick()
    {
      DispatchAction(new ActionResetNamespace1());
    }
    private void IterateNamespace2_OnClick()
    {
      DispatchAction(new ActionIterateNamespace2());
    }
    private void StartNamespace2_OnClick()
    {
      DispatchAction(new ActionStartNamespace2());
    }
  }
}