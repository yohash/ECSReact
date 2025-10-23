using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Category tab component for filtering skills
  /// </summary>
  public class SkillCategoryTabs : ReactiveUIComponent<UIBattleState>, IElementChild
  {
    [Header("Tab References")]
    [SerializeField] private Toggle allTab;
    [SerializeField] private Toggle physicalTab;
    [SerializeField] private Toggle magicTab;
    [SerializeField] private Toggle supportTab;

    private CategoryTabProps currentProps;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as CategoryTabProps;
      UpdateTabStates();
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as CategoryTabProps;
      UpdateTabStates();
    }

    public override void OnStateChanged(UIBattleState newState)
    {
      // Tabs don't need to respond to UI state in this case
    }

    protected override void Start()
    {
      base.Start();

      // Hook up tab listeners
      if (allTab)
        allTab.onValueChanged.AddListener((on) => { if (on) SelectCategory(SkillCategory.All); });
      if (physicalTab)
        physicalTab.onValueChanged.AddListener((on) => { if (on) SelectCategory(SkillCategory.Physical); });
      if (magicTab)
        magicTab.onValueChanged.AddListener((on) => { if (on) SelectCategory(SkillCategory.Magic); });
      if (supportTab)
        supportTab.onValueChanged.AddListener((on) => { if (on) SelectCategory(SkillCategory.Support); });
    }

    private void UpdateTabStates()
    {
      if (currentProps == null)
        return;

      // Set active tab based on selected category
      if (allTab)
        allTab.SetIsOnWithoutNotify(currentProps.SelectedCategory == SkillCategory.All);
      if (physicalTab)
        physicalTab.SetIsOnWithoutNotify(currentProps.SelectedCategory == SkillCategory.Physical);
      if (magicTab)
        magicTab.SetIsOnWithoutNotify(currentProps.SelectedCategory == SkillCategory.Magic);
      if (supportTab)
        supportTab.SetIsOnWithoutNotify(currentProps.SelectedCategory == SkillCategory.Support);
    }

    private void SelectCategory(SkillCategory category)
    {
      currentProps?.OnCategorySelected?.Invoke(category);
    }
  }
}