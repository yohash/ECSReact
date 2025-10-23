using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  public class TurnCycleProps : UIProps
  {
    public int RemainingTurns { get; set; }
    public int CycleNumber { get; set; }
  }

  /// <summary>
  /// Shows dots or indicator for remaining turns not visible in the display
  /// </summary>
  public class TurnCycleIndicator : ReactiveUIComponent<BattleState>, IElementChild
  {
    [SerializeField] private Text remainingText;
    [SerializeField] private Image[] dots;

    private TurnCycleProps currentProps;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as TurnCycleProps;
      UpdateDisplay();
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as TurnCycleProps;
      UpdateDisplay();
    }

    public override void OnStateChanged(BattleState newState)
    {
      // Updates through props
    }

    private void UpdateDisplay()
    {
      if (currentProps == null)
        return;

      if (remainingText)
        remainingText.text = $"+{currentProps.RemainingTurns}";

      // Show dots for visual indication
      for (int i = 0; i < dots.Length; i++) {
        if (dots[i])
          dots[i].gameObject.SetActive(i < currentProps.RemainingTurns);
      }
    }
  }
}