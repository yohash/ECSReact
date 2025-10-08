using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using ECSReact.Core;
using TMPro;
using Unity.Entities;
using Unity.Collections;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// NEW Props structure - Entity reference only
  /// OLD: Embedded full CharacterData struct
  /// NEW: Just Entity - card does all lookups
  /// </summary>
  public class CharacterStatusProps : UIProps
  {
    public Entity CharacterEntity { get; set; }  // Changed from CharacterData
    public bool IsActive { get; set; }
    public bool IsTargeted { get; set; }
    public int CardIndex { get; set; }
    public bool ShowMana { get; set; }
    public bool AnimateChanges { get; set; }
  }

  /// <summary>
  /// Individual character status display - NORMALIZED VERSION
  /// 
  /// MAJOR CHANGES FROM OLD:
  /// - Removed PartyState subscription
  /// - Added 4 normalized state subscriptions:
  ///   * CharacterHealthState (health, alive status)
  ///   * CharacterManaState (mana)
  ///   * CharacterStatusState (status effects)
  ///   * CharacterIdentityState (name, team)
  /// - Props now contain only Entity reference
  /// - All character data fetched via O(1) HashMap lookups
  /// - Removed O(n) loop to find character in state
  /// </summary>
  public class CharacterStatusCard :
    ReactiveUIComponent<CharacterHealthState, CharacterManaState, CharacterStatusState, CharacterIdentityState, BattleState, UIBattleState>,
    IElementChild,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
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

    // Cached state references
    private CharacterHealthState healthState;
    private CharacterManaState manaState;
    private CharacterStatusState statusState;
    private CharacterIdentityState identityState;
    private BattleState battleState;
    private UIBattleState uiState;

    // Cached character data for animation detection
    private int previousHealth = 0;
    private bool wasAlive = true;

    // Interaction state
    private bool isHovered = false;
    private bool isValidTarget = false;
    private bool isSelected = false;

    // For animations
    private float healthAnimationVelocity;
    private float manaAnimationVelocity;
    private float currentDisplayHealth;
    private float currentDisplayMana;

    // ========================================================================
    // IELEMENT CHILD - Props Interface
    // ========================================================================

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as CharacterStatusProps;
      if (currentProps != null && currentProps.CharacterEntity != Entity.Null) {
        // Initialize animation values from current state
        if (healthState.health.IsCreated &&
            healthState.health.TryGetValue(currentProps.CharacterEntity, out var health)) {
          currentDisplayHealth = health.current;
          previousHealth = health.current;
          wasAlive = health.isAlive;
        }

        if (manaState.mana.IsCreated &&
            manaState.mana.TryGetValue(currentProps.CharacterEntity, out var mana)) {
          currentDisplayMana = mana.current;
        }

        UpdateDisplay();
      }
    }

    public void UpdateProps(UIProps props)
    {
      currentProps = props as CharacterStatusProps;
      UpdateDisplay();
    }

    // ========================================================================
    // STATE CHANGE HANDLERS
    // ========================================================================

    public override void OnStateChanged(CharacterHealthState newState)
    {
      healthState = newState;

      // Detect health changes for animation
      if (currentProps != null && currentProps.CharacterEntity != Entity.Null) {
        if (newState.health.IsCreated &&
            newState.health.TryGetValue(currentProps.CharacterEntity, out var health)) {
          if (health.current != previousHealth) {
            PlayHealthChangeAnimation(previousHealth, health.current);
            previousHealth = health.current;
          }

          if (health.isAlive != wasAlive) {
            wasAlive = health.isAlive;
          }
        }
      }

      UpdateDisplay();
    }

    public override void OnStateChanged(CharacterManaState newState)
    {
      manaState = newState;
      UpdateDisplay();
    }

    public override void OnStateChanged(CharacterStatusState newState)
    {
      statusState = newState;
      UpdateElements(); // Status effects are child elements
    }

    public override void OnStateChanged(CharacterIdentityState newState)
    {
      identityState = newState;
      UpdateDisplay(); // In case name changes (rare)
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
      isSelected = (newState.selectedTarget == currentProps?.CharacterEntity);

      UpdateTargetingVisuals();
    }

    // ========================================================================
    // DISPLAY UPDATE - Using Normalized State Lookups
    // ========================================================================

    private void UpdateDisplay()
    {
      if (currentProps == null || currentProps.CharacterEntity == Entity.Null)
        return;

      Entity entity = currentProps.CharacterEntity;

      // ====================================================================
      // NEW: Fetch name from CharacterIdentityState (O(1))
      // ====================================================================
      if (nameText && identityState.names.IsCreated) {
        if (identityState.names.TryGetValue(entity, out var name)) {
          nameText.text = name.ToString();
          SetPortrait(name.ToString());
        }
      }

      // ====================================================================
      // NEW: Fetch health from CharacterHealthState (O(1))
      // ====================================================================
      if (healthState.health.IsCreated &&
          healthState.health.TryGetValue(entity, out var healthData)) {
        // Update health bar
        if (healthBar) {
          float healthPercent = healthData.max > 0
              ? (float)healthData.current / healthData.max
              : 0f;

          if (currentProps.AnimateChanges) {
            // Smooth animation will happen in Update()
          } else {
            healthBar.value = healthPercent;
            currentDisplayHealth = healthData.current;
          }
        }

        if (healthText)
          healthText.text = $"{healthData.current}/{healthData.max}";

        // Update death overlay
        if (deathOverlay)
          deathOverlay.SetActive(!healthData.isAlive);
      }

      // ====================================================================
      // NEW: Fetch mana from CharacterManaState (O(1))
      // ====================================================================
      if (manaContainer)
        manaContainer.SetActive(currentProps.ShowMana);

      if (currentProps.ShowMana && manaState.mana.IsCreated &&
          manaState.mana.TryGetValue(entity, out var manaData)) {
        if (manaBar) {
          float manaPercent = manaData.max > 0
              ? (float)manaData.current / manaData.max
              : 0f;
          manaBar.value = manaPercent;
        }

        if (manaText)
          manaText.text = $"{manaData.current}/{manaData.max}";
      }

      // Update elements (for status effects)
      UpdateElements();

      // Update targeting visuals
      UpdateTargetingVisuals();
    }

    private void SetPortrait(string characterName)
    {
      switch (characterName.ToLower()) {
        case "hero":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/hero");
          break;
        case "mage":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/wizard");
          break;
        case "warrior":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/warrior");
          break;
        case "goblin":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/goblin");
          break;
        case "orc":
          if (portraitImage)
            portraitImage.sprite = Resources.Load<Sprite>("Sprites/Characters/orc");
          break;

        default:
          break;
      }
    }

    // ========================================================================
    // CHILD ELEMENTS - Status Effect Icons
    // ========================================================================

    protected override IEnumerable<UIElement> DeclareElements()
    {
      if (currentProps == null || currentProps.CharacterEntity == Entity.Null)
        yield break;

      // NEW: Fetch status from CharacterStatusState (O(1))
      if (!statusState.statuses.IsCreated ||
          !statusState.statuses.TryGetValue(currentProps.CharacterEntity, out var status))
        yield break;

      // Generate status effect icons as child elements
      int effectIndex = 0;

      // Check each status flag
      if ((status & CharacterStatus.Poisoned) != 0) {
        yield return Mount.Element.FromResources(
            key: "status_poisoned",
            prefabPath: "UI/StatusEffectIcon",
            props: new StatusEffectProps { StatusType = CharacterStatus.Poisoned },
            index: effectIndex++,
            parentTransform: statusEffectContainer
        );
      }

      if ((status & CharacterStatus.Stunned) != 0) {
        yield return Mount.Element.FromResources(
            key: "status_stunned",
            prefabPath: "UI/StatusEffectIcon",
            props: new StatusEffectProps { StatusType = CharacterStatus.Stunned },
            index: effectIndex++,
            parentTransform: statusEffectContainer
        );
      }

      if ((status & CharacterStatus.Defending) != 0) {
        yield return Mount.Element.FromResources(
            key: "status_defending",
            prefabPath: "UI/StatusEffectIcon",
            props: new StatusEffectProps { StatusType = CharacterStatus.Defending },
            index: effectIndex++,
            parentTransform: statusEffectContainer
        );
      }

      if ((status & CharacterStatus.Buffed) != 0) {
        yield return Mount.Element.FromResources(
            key: "status_buffed",
            prefabPath: "UI/StatusEffectIcon",
            props: new StatusEffectProps { StatusType = CharacterStatus.Buffed },
            index: effectIndex++,
            parentTransform: statusEffectContainer
        );
      }

      if ((status & CharacterStatus.Weakened) != 0) {
        yield return Mount.Element.FromResources(
            key: "status_weakened",
            prefabPath: "UI/StatusEffectIcon",
            props: new StatusEffectProps { StatusType = CharacterStatus.Weakened },
            index: effectIndex++,
            parentTransform: statusEffectContainer
        );
      }
    }

    // ========================================================================
    // POINTER INTERACTION HANDLERS
    // ========================================================================

    public void OnPointerEnter(PointerEventData eventData)
    {
      isHovered = true;
      UpdateTargetingVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
      isHovered = false;
      UpdateTargetingVisuals();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
      // Only process clicks during target selection
      if (uiState.showTargetingMode && currentProps != null) {
        DispatchAction(new SelectTargetAction
        {
          targetEntity = currentProps.CharacterEntity,
          confirmSelection = true
        });
      }
    }

    // ========================================================================
    // VISUAL HELPERS
    // ========================================================================

    private void UpdateTargetingVisuals()
    {
      // Update active indicator
      if (activeIndicator)
        activeIndicator.SetActive(currentProps?.IsActive ?? false);

      // Update selection visuals
      if (selectedBorder)
        selectedBorder.SetActive(isSelected);

      if (hoveredBorder)
        hoveredBorder.SetActive(isHovered);

      // Update background color based on state
      if (backgroundImage) {
        if (!wasAlive)
          backgroundImage.color = deadColor;
        else if (currentProps?.IsActive ?? false)
          backgroundImage.color = activeColor;
        else if (isSelected)
          backgroundImage.color = targetedColor;
        else if (isHovered)
          backgroundImage.color = hoveredColor;
        else
          backgroundImage.color = normalColor;
      }
    }

    private void PlayHealthChangeAnimation(int oldHealth, int newHealth)
    {
      if (newHealth < oldHealth) {
        Debug.Log($"Character took {oldHealth - newHealth} damage!");
        // Could trigger damage animation here
      } else if (newHealth > oldHealth) {
        Debug.Log($"Character healed for {newHealth - oldHealth}!");
        // Could trigger healing animation here
      }
    }
  }

  // Props for status effect icons
  public class StatusEffectProps : UIProps
  {
    public CharacterStatus StatusType { get; set; }
  }
}