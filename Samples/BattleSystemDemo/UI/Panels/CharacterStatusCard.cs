using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using ECSReact.Core;
using TMPro;
using Unity.Entities;

namespace ECSReact.Samples.BattleSystem
{
  // Props for passing data to character cards
  public class CharacterStatusProps : UIProps
  {
    public CharacterData Character { get; set; }
    public bool IsActive { get; set; }
    public bool IsTargeted { get; set; }
    public int CardIndex { get; set; }
    public bool ShowMana { get; set; }
    public bool AnimateChanges { get; set; }
  }

  /// <summary>
  /// Individual character status display that receives props from PartyStatusBar.
  /// Now handles its own targeting interactions and visual feedback.
  /// </summary>
  public class CharacterStatusCard : ReactiveUIComponent<PartyState, BattleState, UIBattleState>,
    IElementChild, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
  {
    [Header("UI References")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Slider healthBar;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider manaBar;
    [SerializeField] private TextMeshProUGUI manaText;
    [SerializeField] private GameObject manaContainer;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject activeIndicator;
    [SerializeField] private GameObject targetIndicator;
    [SerializeField] private GameObject deathOverlay;

    [Header("Status Effect Display")]
    [SerializeField] private Transform statusEffectContainer;

    [Header("Targeting Visuals")]
    [SerializeField] private GameObject targetableBorder;
    [SerializeField] private GameObject hoveredBorder;
    [SerializeField] private GameObject selectedBorder;
    [SerializeField] private Image targetableOverlay;

    [Header("Visual Configuration")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = Color.yellow;
    [SerializeField] private Color targetedColor = Color.red;
    [SerializeField] private Color deadColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color targetableColor = new Color(1f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color hoveredColor = new Color(1f, 1f, 0.8f, 1f);
    [SerializeField] private Color invalidTargetColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private CharacterStatusProps currentProps;
    private CharacterData previousCharacterData;
    private PartyState partyState;
    private BattleState battleState;
    private UIBattleState uiState;

    // Interaction state
    private bool isHovered = false;
    private bool isValidTarget = false;
    private bool isSelected = false;

    // For animations
    private float healthAnimationVelocity;
    private float manaAnimationVelocity;
    private float currentDisplayHealth;
    private float currentDisplayMana;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as CharacterStatusProps;
      if (currentProps != null) {
        currentDisplayHealth = currentProps.Character.currentHealth;
        currentDisplayMana = currentProps.Character.currentMana;
        previousCharacterData = currentProps.Character;
        UpdateDisplay();
      }
    }

    public void UpdateProps(UIProps props)
    {
      var newProps = props as CharacterStatusProps;
      if (newProps != null) {
        previousCharacterData = currentProps?.Character ?? newProps.Character;
        currentProps = newProps;
        UpdateDisplay();
      }
    }

    public override void OnStateChanged(PartyState newState)
    {
      partyState = newState;

      if (currentProps != null) {
        // Find our character in the new state
        for (int i = 0; i < newState.characters.Length; i++) {
          if (newState.characters[i].entity == currentProps.Character.entity) {
            // Trigger damage/heal animation if health changed
            if (newState.characters[i].currentHealth != currentProps.Character.currentHealth) {
              PlayHealthChangeAnimation(
                  currentProps.Character.currentHealth,
                  newState.characters[i].currentHealth
              );
            }
            break;
          }
        }
      }
    }

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateTargetingVisuals();
    }

    public override void OnStateChanged(UIBattleState newState)
    {
      uiState = newState;

      // Check if we're the selected target
      isSelected = (newState.selectedTarget == currentProps?.Character.entity);

      UpdateTargetingVisuals();
    }

    // IPointerEnterHandler - Mouse enters this card
    public void OnPointerEnter(PointerEventData eventData)
    {
      isHovered = true;

      // Only dispatch hover if we're a valid target
      if (IsValidTarget()) {
        DispatchAction(new SelectTargetAction
        {
          targetEntity = currentProps.Character.entity,
          confirmSelection = false
        });
      }

      UpdateTargetingVisuals();
    }

    // IPointerExitHandler - Mouse leaves this card
    public void OnPointerExit(PointerEventData eventData)
    {
      isHovered = false;

      // Clear hover if we were the hovered target
      if (uiState.selectedTarget == currentProps?.Character.entity && !isSelected) {
        DispatchAction(new SelectTargetAction
        {
          targetEntity = Entity.Null,
          confirmSelection = false
        });
      }

      UpdateTargetingVisuals();
    }

    // IPointerClickHandler - Card is clicked
    public void OnPointerClick(PointerEventData eventData)
    {
      // Only process clicks during target selection phase
      if (!IsInTargetingPhase())
        return;

      // Only process if we're a valid target
      if (!IsValidTarget())
        return;

      // Dispatch selection action
      DispatchAction(new SelectTargetAction
      {
        targetEntity = currentProps.Character.entity,
        confirmSelection = false
      });

      isSelected = true;
      UpdateTargetingVisuals();
    }

    private bool IsInTargetingPhase()
    {
      // Check if we're in a phase that allows targeting
      return battleState.currentPhase == BattlePhase.PlayerSelectTarget ||
             (battleState.currentPhase == BattlePhase.PlayerSelectAction &&
              uiState.activePanel == MenuPanel.TargetSelection);
    }

    private bool IsValidTarget()
    {
      if (currentProps == null || !currentProps.Character.isAlive)
        return false;

      if (!IsInTargetingPhase())
        return false;

      // Determine if this character is a valid target based on action type
      bool isEnemy = currentProps.Character.isEnemy;

      switch (uiState.selectedAction) {
        case ActionType.Attack:
          // Can only attack enemies
          return isEnemy;

        case ActionType.Skill:
          // Depends on skill type - for now assume offensive skills target enemies
          // Healing skills (IDs 2, 5) target allies
          if (uiState.selectedSkillId == 2 || uiState.selectedSkillId == 5)
            return !isEnemy; // Healing targets allies
          else
            return isEnemy; // Offensive skills target enemies

        case ActionType.Item:
          // Most items target allies
          return !isEnemy;

        default:
          return false;
      }
    }

    private void UpdateTargetingVisuals()
    {
      isValidTarget = IsValidTarget();
      bool inTargetingPhase = IsInTargetingPhase();

      // Update targetable border (shows who can be targeted)
      if (targetableBorder != null) {
        targetableBorder.SetActive(inTargetingPhase && isValidTarget);
      }

      // Update hovered border (shows current hover)
      if (hoveredBorder != null) {
        hoveredBorder.SetActive(isHovered && isValidTarget);
      }

      // Update selected border (shows selected target)
      if (selectedBorder != null) {
        selectedBorder.SetActive(isSelected);
      }

      // Update background color based on state
      if (backgroundImage != null) {
        if (!currentProps.Character.isAlive) {
          backgroundImage.color = deadColor;
        } else if (isSelected) {
          backgroundImage.color = targetedColor;
        } else if (isHovered && isValidTarget) {
          backgroundImage.color = hoveredColor;
        } else if (inTargetingPhase && isValidTarget) {
          backgroundImage.color = targetableColor;
        } else if (inTargetingPhase && !isValidTarget) {
          backgroundImage.color = invalidTargetColor;
        } else if (currentProps.IsActive) {
          backgroundImage.color = activeColor;
        } else {
          backgroundImage.color = normalColor;
        }
      }

      // Update overlay for invalid targets during targeting
      if (targetableOverlay != null) {
        if (inTargetingPhase && !isValidTarget) {
          targetableOverlay.gameObject.SetActive(true);
          targetableOverlay.color = new Color(0, 0, 0, 0.5f); // Darken invalid targets
        } else {
          targetableOverlay.gameObject.SetActive(false);
        }
      }

      // Update cursor when hovering
      if (isHovered && inTargetingPhase) {
        Cursor.SetCursor(isValidTarget ? null : null, Vector2.zero, CursorMode.Auto);
        // In real implementation, set attack cursor for valid targets, X cursor for invalid
      }
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      if (currentProps == null)
        yield break;

      // Generate status effect icons as child elements
      var statusFlags = System.Enum.GetValues(typeof(CharacterStatus)) as CharacterStatus[];
      int effectIndex = 0;

      foreach (var status in statusFlags) {
        if (status == CharacterStatus.None)
          continue;

        if (currentProps.Character.status.HasFlag(status)) {
          yield return Mount.Element.FromResources(
              key: $"status_{status}",
              prefabPath: "UI/StatusEffectIcon",
              props: new StatusEffectProps
              {
                StatusType = status,
                Duration = GetStatusDuration(status)
              },
              index: effectIndex++,
              parentTransform: statusEffectContainer
          );
        }
      }
    }

    private void UpdateDisplay()
    {
      if (currentProps == null)
        return;

      var character = currentProps.Character;

      // Update name
      if (nameText)
        nameText.text = character.name.ToString();

      // Update health
      if (healthBar) {
        float healthPercent = character.maxHealth > 0
            ? (float)character.currentHealth / character.maxHealth
            : 0f;

        if (currentProps.AnimateChanges) {
          // Smooth animation will happen in Update()
        } else {
          healthBar.value = healthPercent;
          currentDisplayHealth = character.currentHealth;
        }
      }

      if (healthText)
        healthText.text = $"{character.currentHealth}/{character.maxHealth}";

      // Update mana (only for party members)
      if (manaContainer)
        manaContainer.SetActive(currentProps.ShowMana);

      if (currentProps.ShowMana && manaBar) {
        float manaPercent = character.maxMana > 0
            ? character.currentMana / character.maxMana
            : 0f;
        manaBar.value = manaPercent;
      }

      if (manaText)
        manaText.text = $"{character.currentMana}/{character.maxMana}";

      // Update death state
      if (deathOverlay)
        deathOverlay.SetActive(!character.isAlive);

      // Update targeting visuals
      UpdateTargetingVisuals();
    }

    private void PlayHealthChangeAnimation(int oldHealth, int newHealth)
    {
      if (newHealth < oldHealth) {
        Debug.Log($"{currentProps.Character.name} took {oldHealth - newHealth} damage!");
      } else if (newHealth > oldHealth) {
        Debug.Log($"{currentProps.Character.name} healed for {newHealth - oldHealth}!");
      }
    }

    private float GetStatusDuration(CharacterStatus status)
    {
      // In real implementation, would track status effect durations
      return 0f;
    }
  }

  // Props for status effect icons
  public class StatusEffectProps : UIProps
  {
    public CharacterStatus StatusType { get; set; }
    public float Duration { get; set; }
  }
}