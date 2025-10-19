using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Simple header component for separating party and enemy sections
  /// </summary>
  public class StatusSectionHeader : ReactiveUIComponent, IElementChild
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

    // no state
    protected override void SubscribeToStateChanges() { }
    protected override void UnsubscribeFromStateChanges() { }

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

  public class SectionHeaderProps : UIProps
  {
    public string Title { get; set; }
    public bool IsEnemy { get; set; }
  }
}