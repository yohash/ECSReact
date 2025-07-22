using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ECSReact.Core;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Individual skill button component that receives props from SkillSelectionPanel.
  /// Demonstrates IElement pattern and interactive UI with state-based styling.
  /// </summary>
  public class SkillButton : ReactiveUIComponent<PartyState>, IElementChild, IPointerEnterHandler, IPointerExitHandler
  {
    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI manaCostText;
    [SerializeField] private Image skillIcon;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject cooldownOverlay;
    [SerializeField] private TextMeshProUGUI cooldownText;

    [Header("Visual States")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 0.8f, 1f);
    [SerializeField] private Sprite[] categoryIcons; // Physical, Magic, Support

    private SkillButtonProps currentProps;
    private bool isHovering = false;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as SkillButtonProps;
      UpdateDisplay();
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as SkillButtonProps;
      UpdateDisplay();
    }

    public override void OnStateChanged(PartyState newState)
    {
      // Could update if character mana changes
      UpdateDisplay();
    }

    protected override void Start()
    {
      base.Start();

      if (button)
        button.onClick.AddListener(OnButtonClicked);
    }

    private void UpdateDisplay()
    {
      if (currentProps == null)
        return;

      var skill = currentProps.Skill;

      // Update texts
      if (skillNameText)
        skillNameText.text = skill.name.ToString();
      if (manaCostText) {
        manaCostText.text = $"{skill.manaCost} MP";
        manaCostText.color = currentProps.CanUse ? Color.cyan : Color.red;
      }

      // Update icon based on category
      if (skillIcon && categoryIcons.Length > 0) {
        int iconIndex = (int)skill.category - 1; // Skip "All"
        if (iconIndex >= 0 && iconIndex < categoryIcons.Length)
          skillIcon.sprite = categoryIcons[iconIndex];
      }

      // Update button state
      if (button) {
        button.interactable = currentProps.CanUse;
      }

      // Update background color
      if (backgroundImage) {
        if (!currentProps.CanUse)
          backgroundImage.color = disabledColor;
        else if (isHovering)
          backgroundImage.color = hoverColor;
        else
          backgroundImage.color = normalColor;
      }

      // Hide cooldown for this demo
      if (cooldownOverlay)
        cooldownOverlay.SetActive(false);
    }

    private void OnButtonClicked()
    {
      if (currentProps?.OnSkillSelected != null && currentProps.CanUse) {
        currentProps.OnSkillSelected(currentProps.Skill);
      }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
      isHovering = true;
      if (currentProps?.CanUse == true && backgroundImage)
        backgroundImage.color = hoverColor;

      // Could show skill tooltip here
    }

    public void OnPointerExit(PointerEventData eventData)
    {
      isHovering = false;
      UpdateDisplay();
    }
  }
}