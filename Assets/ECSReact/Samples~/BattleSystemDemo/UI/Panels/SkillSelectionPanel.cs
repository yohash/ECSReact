using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using Unity.Collections;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  // ============================================================================
  // SKILL SELECTION PANEL - NORMALIZED VERSION
  // ============================================================================

  /// <summary>
  /// Skill selection panel - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState subscription
  /// - Added CharacterManaState, CharacterIdentityState subscriptions
  /// - Replaced loop to find active character with O(1) lookups
  /// </summary>
  public class SkillSelectionPanel : ReactiveUIComponent<CharacterManaState, CharacterIdentityState, UIBattleState>, IElementChild
  {
    [Header("Panel Configuration")]
    [SerializeField] private Transform skillGridContainer;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI manaText;

    [SerializeField] private bool showCategories = true;
    [SerializeField] private Transform categoryTabContainer;

    private CharacterManaState manaState;
    private CharacterIdentityState identityState;
    private UIBattleState uiState;
    private SkillPanelProps currentProps;
    private SkillCategory selectedCategory = SkillCategory.All;

    // Cached character data
    private FixedString32Bytes characterName;
    private int characterCurrentMana;
    private int characterMaxMana;

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

    public override void OnStateChanged(CharacterManaState newState)
    {
      manaState = newState;
      UpdateCharacterInfo();
      UpdateElements(); // Refresh skill buttons (mana affects usability)
    }

    public override void OnStateChanged(CharacterIdentityState newState)
    {
      identityState = newState;
      UpdateCharacterInfo();
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
        bool canUse = characterCurrentMana >= skill.manaCost;

        yield return Mount.Element.FromResources(
            key: $"skill_{skill.id}",
            prefabPath: "UI/SkillButton",
            props: new SkillButtonProps
            {
              Skill = skill,
              CanUse = canUse,
              CharacterMana = characterCurrentMana,
              OnSkillSelected = OnSkillSelected
            },
            index: buttonIndex++
        );
      }

      // Empty state message if no skills
      if (filteredSkills.Count == 0) {
        // Empty message could go here
      }
    }

    /// <summary>
    /// NEW: Fetch character info using O(1) lookups
    /// OLD: O(n) loop through PartyState.characters
    /// </summary>
    private void UpdateCharacterInfo()
    {
      if (currentProps == null || currentProps.CharacterEntity == Entity.Null)
        return;

      Entity entity = currentProps.CharacterEntity;

      // Lookup name (O(1))
      if (identityState.names.IsCreated &&
          identityState.names.TryGetValue(entity, out var name)) {
        characterName = name;

        if (titleText)
          titleText.text = $"{characterName}'s Skills";
      }

      // Lookup mana (O(1))
      if (manaState.mana.IsCreated &&
          manaState.mana.TryGetValue(entity, out var manaData)) {
        characterCurrentMana = manaData.current;
        characterMaxMana = manaData.max;

        if (manaText)
          manaText.text = $"MP: {characterCurrentMana}/{characterMaxMana}";
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
      UpdateElements();
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
    public Entity actingCharacter;
    public bool targetRequired;
  }

  public struct ShowTargetingAction : IGameAction
  {
    public ActionType actionType;
    public bool allowMultiTarget;
  }

  public class SkillPanelProps : UIProps
  {
    public Entity CharacterEntity { get; set; }  // Changed from full character data
  }
}