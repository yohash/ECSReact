using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using Unity.Collections;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Nested skill selection panel that appears when Skills is selected.
  /// Demonstrates conditional rendering and nested element composition.
  /// </summary>
  public class SkillSelectionPanel : ReactiveUIComponent<PartyState, UIBattleState>, IElementChild
  {
    [Header("Panel Configuration")]
    [SerializeField] private Transform skillGridContainer;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI manaText;

    [SerializeField] private bool showCategories = true;
    [SerializeField] private Transform categoryTabContainer;

    private PartyState partyState;
    private UIBattleState uiState;
    private SkillPanelProps currentProps;
    private CharacterData activeCharacter;
    private SkillCategory selectedCategory = SkillCategory.All;

    // Mock skill data for demo
    private readonly List<SkillData> availableSkills = new List<SkillData>
    {
      new SkillData { id = 1, name = "Fireball", manaCost = 10, category = SkillCategory.Magic, damage = 30 },
      new SkillData { id = 2, name = "Heal", manaCost = 5, category = SkillCategory.Support, healing = 25 },
      new SkillData { id = 3, name = "Lightning Strike", manaCost = 15, category = SkillCategory.Magic, damage = 45 },
      new SkillData { id = 4, name = "Shield Bash", manaCost = 3, category = SkillCategory.Physical, damage = 15 },
      new SkillData { id = 5, name = "Group Heal", manaCost = 20, category = SkillCategory.Support, healing = 15 },
    };

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as SkillPanelProps;
      UpdateCharacterInfo();
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as SkillPanelProps;
      UpdateCharacterInfo();
    }

    public override void OnStateChanged(PartyState newState)
    {
      partyState = newState;
      UpdateCharacterInfo();
      UpdateElements(); // Refresh skill buttons
    }

    public override void OnStateChanged(UIBattleState newState)
    {
      uiState = newState;
    }

    protected override void Start()
    {
      base.Start();

      if (backButton)
        backButton.onClick.AddListener(OnBackClicked);
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      // Category tabs (if enabled)
      if (showCategories) {
        yield return Mount.Element.FromResources(
            key: "category_tabs",
            prefabPath: "UI/SkillCategoryTabs",
            props: new CategoryTabProps
            {
              SelectedCategory = selectedCategory,
              OnCategorySelected = OnCategorySelected
            },
            index: 0
        );
      }

      // Skill buttons based on selected category
      int buttonIndex = 1;
      var filteredSkills = GetFilteredSkills();

      foreach (var skill in filteredSkills) {
        bool canUse = activeCharacter.currentMana >= skill.manaCost;

        yield return Mount.Element.FromResources(
            key: $"skill_{skill.id}",
            prefabPath: "UI/SkillButton",
            props: new SkillButtonProps
            {
              Skill = skill,
              CanUse = canUse,
              CharacterMana = activeCharacter.currentMana,
              OnSkillSelected = OnSkillSelected
            },
            index: buttonIndex++
        );
      }

      // Empty state message if no skills
      if (filteredSkills.Count == 0) {
        //yield return UIElement.FromComponent<EmptySkillMessage>(
        //    key: "empty_skills",
        //    props: new EmptyMessageProps
        //    {
        //      Message = selectedCategory == SkillCategory.All
        //            ? "No skills learned yet!"
        //            : $"No {selectedCategory} skills available"
        //    },
        //    index: buttonIndex
        //);
      }
    }

    private void UpdateCharacterInfo()
    {
      if (currentProps == null)
        return;

      // Find active character data
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == currentProps.CharacterEntity) {
          activeCharacter = partyState.characters[i];

          if (titleText)
            titleText.text = $"{activeCharacter.name}'s Skills";

          if (manaText)
            manaText.text = $"MP: {activeCharacter.currentMana}/{activeCharacter.maxMana}";

          break;
        }
      }
    }

    private List<SkillData> GetFilteredSkills()
    {
      if (selectedCategory == SkillCategory.All)
        return availableSkills;

      return availableSkills.FindAll(s => s.category == selectedCategory);
    }

    private void OnCategorySelected(SkillCategory category)
    {
      selectedCategory = category;
      UpdateElements(); // Refresh skill list
    }

    private void OnSkillSelected(SkillData skill)
    {
      // Dispatch skill selection action
      DispatchAction(new SelectSkillAction
      {
        skillId = skill.id,
        actingCharacter = currentProps.CharacterEntity,
        targetRequired = skill.category != SkillCategory.Support
      });

      // Update UI state to show targeting if needed
      if (skill.category != SkillCategory.Support) {
        DispatchAction(new ShowTargetingAction
        {
          actionType = ActionType.Skill,
          allowMultiTarget = skill.isAreaEffect
        });
      }
    }

    private void OnBackClicked()
    {
      // Return to main action menu
      DispatchAction(new SelectActionTypeAction
      {
        actionType = ActionType.None,
        actingCharacter = currentProps.CharacterEntity
      });
    }
  }

  // Skill data structures
  [System.Serializable]
  public struct SkillData
  {
    public int id;
    public FixedString32Bytes name;
    public int manaCost;
    public SkillCategory category;
    public int damage;
    public int healing;
    public bool isAreaEffect;
  }

  public enum SkillCategory
  {
    All,
    Physical,
    Magic,
    Support
  }

  // Props for skill components
  public class SkillButtonProps : UIProps
  {
    public SkillData Skill { get; set; }
    public bool CanUse { get; set; }
    public int CharacterMana { get; set; }
    public System.Action<SkillData> OnSkillSelected { get; set; }
  }

  public class CategoryTabProps : UIProps
  {
    public SkillCategory SelectedCategory { get; set; }
    public System.Action<SkillCategory> OnCategorySelected { get; set; }
  }

  public class EmptyMessageProps : UIProps
  {
    public string Message { get; set; }
  }

  // Additional actions for skill system
  public struct SelectSkillAction : IGameAction
  {
    public int skillId;
    public Unity.Entities.Entity actingCharacter;
    public bool targetRequired;
  }

  public struct ShowTargetingAction : IGameAction
  {
    public ActionType actionType;
    public bool allowMultiTarget;
  }
}