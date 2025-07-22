using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using ECSReact.Core;
using TMPro;

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
  /// Demonstrates IElement pattern for prop-based updates.
  /// </summary>
  public class CharacterStatusCard : ReactiveUIComponent<PartyState>, IElementChild
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

    [Header("Visual Configuration")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = Color.yellow;
    [SerializeField] private Color targetedColor = Color.red;
    [SerializeField] private Color deadColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    private CharacterStatusProps currentProps;
    private CharacterData previousCharacterData;

    // For animations
    private float healthAnimationVelocity;
    private float manaAnimationVelocity;
    private float currentDisplayHealth;
    private float currentDisplayMana;

    public void InitializeWithProps(UIProps props)
    {
      currentProps = props as CharacterStatusProps;
      if (currentProps != null) {
        // Initialize display values for smooth animation
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
        // Store previous data for animation
        previousCharacterData = currentProps?.Character ?? newProps.Character;
        currentProps = newProps;

        UpdateDisplay();
      }
    }

    public override void OnStateChanged(PartyState newState)
    {
      // We primarily update through props, but we can still respond
      // to global party state changes if needed
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
                Duration = GetStatusDuration(status) // Would track in real implementation
              },
              index: effectIndex++
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
            ? (float)character.currentMana / character.maxMana
            : 0f;

        if (currentProps.AnimateChanges) {
          // Smooth animation will happen in Update()
        } else {
          manaBar.value = manaPercent;
          currentDisplayMana = character.currentMana;
        }
      }

      if (manaText && currentProps.ShowMana)
        manaText.text = $"{character.currentMana}/{character.maxMana}";

      // Update visual states
      if (activeIndicator)
        activeIndicator.SetActive(currentProps.IsActive);

      if (targetIndicator)
        targetIndicator.SetActive(currentProps.IsTargeted);

      if (deathOverlay)
        deathOverlay.SetActive(!character.isAlive);

      // Update background color
      if (backgroundImage) {
        if (!character.isAlive)
          backgroundImage.color = deadColor;
        else if (currentProps.IsActive)
          backgroundImage.color = activeColor;
        else if (currentProps.IsTargeted)
          backgroundImage.color = targetedColor;
        else
          backgroundImage.color = normalColor;
      }

      // Update status effects through element system
      UpdateElements();
    }

    private void Update()
    {
      if (currentProps == null || !currentProps.AnimateChanges)
        return;

      // Smooth health bar animation
      if (healthBar && currentProps.Character.currentHealth != currentDisplayHealth) {
        currentDisplayHealth = Mathf.SmoothDamp(
            currentDisplayHealth,
            currentProps.Character.currentHealth,
            ref healthAnimationVelocity,
            0.3f
        );

        float healthPercent = currentProps.Character.maxHealth > 0
            ? currentDisplayHealth / currentProps.Character.maxHealth
            : 0f;

        healthBar.value = healthPercent;
      }

      // Smooth mana bar animation
      if (manaBar && currentProps.ShowMana && currentProps.Character.currentMana != currentDisplayMana) {
        currentDisplayMana = Mathf.SmoothDamp(
            currentDisplayMana,
            currentProps.Character.currentMana,
            ref manaAnimationVelocity,
            0.3f
        );

        float manaPercent = currentProps.Character.maxMana > 0
            ? currentDisplayMana / currentProps.Character.maxMana
            : 0f;

        manaBar.value = manaPercent;
      }
    }

    private void PlayHealthChangeAnimation(int oldHealth, int newHealth)
    {
      // In full implementation, could trigger particle effects,
      // screen shake, damage numbers, etc.
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