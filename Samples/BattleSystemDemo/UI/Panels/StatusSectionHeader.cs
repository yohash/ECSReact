using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Simple header component for separating party and enemy sections
  /// </summary>
  public class StatusSectionHeader : ReactiveUIComponent<PartyState>, IElementChild
  {
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color partyColor = new Color(0.2f, 0.4f, 0.8f, 0.5f);
    [SerializeField] private Color enemyColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);

    private SectionHeaderProps currentProps;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as SectionHeaderProps;
      UpdateDisplay();
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as SectionHeaderProps;
      UpdateDisplay();
    }

    public override void OnStateChanged(PartyState newState)
    {
      // Headers don't need to respond to party state changes
    }

    private void UpdateDisplay()
    {
      if (currentProps == null)
        return;

      if (titleText)
        titleText.text = currentProps.Title;

      if (backgroundImage)
        backgroundImage.color = currentProps.IsEnemy ? enemyColor : partyColor;
    }
  }
}