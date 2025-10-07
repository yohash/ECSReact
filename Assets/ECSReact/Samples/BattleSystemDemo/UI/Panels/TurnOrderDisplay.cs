using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using Unity.Entities;
using Unity.Collections;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Displays upcoming turn order with smooth animations - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState subscription
  /// - Added CharacterIdentityState, CharacterHealthState subscriptions
  /// - Replaced GetCharacterData() O(n) loop with O(1) HashMap lookups
  /// - Builds CharacterData from multiple state lookups
  /// </summary>
  public class TurnOrderDisplay : ReactiveUIComponent<BattleState, CharacterIdentityState, CharacterHealthState>
  {
    [Header("UI Configuration")]
    [SerializeField] private Transform turnSlotContainer;
    [SerializeField] private TextMeshProUGUI turnCounterText;
    [SerializeField] private int maxVisibleTurns = 5;

    [Header("Animation Settings")]
    [SerializeField] private float slotSpacing = 80f;
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Visual Settings")]
    [SerializeField] private Vector2 currentTurnScale = new Vector2(1.2f, 1.2f);
    [SerializeField] private Vector2 normalTurnScale = Vector2.one;
    [SerializeField] private float currentTurnYOffset = -10f;

    private BattleState battleState;
    private CharacterIdentityState identityState;
    private CharacterHealthState healthState;

    private Dictionary<Entity, TurnSlotAnimationData> animationData = new Dictionary<Entity, TurnSlotAnimationData>();
    private int previousActiveIndex = -1;

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateElements();
      UpdateTurnCounter();
    }

    public override void OnStateChanged(CharacterIdentityState newState)
    {
      identityState = newState;
      UpdateElements();
    }

    public override void OnStateChanged(CharacterHealthState newState)
    {
      healthState = newState;
      UpdateElements(); // Refresh if character dies/heals
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      if (battleState.turnOrder.Length == 0)
        yield break;

      // Calculate visible turns (current + next X turns)
      int turnsToShow = Mathf.Min(maxVisibleTurns, battleState.turnOrder.Length);

      for (int i = 0; i < turnsToShow; i++) {
        // Calculate actual turn index (wrapping around)
        int turnIndex = (battleState.activeCharacterIndex + i) % battleState.turnOrder.Length;
        Entity characterEntity = battleState.turnOrder[turnIndex];

        // NEW: Build character data from normalized state lookups
        CharacterData? characterData = GetCharacterDataFromStates(characterEntity);
        if (!characterData.HasValue)
          continue;

        // Calculate target position for this slot
        float targetX = i * slotSpacing + 120;
        float targetY = (i == 0) ? currentTurnYOffset : 0f;
        Vector2 targetScale = (i == 0) ? currentTurnScale : normalTurnScale;

        // Create turn slot with specific parent transform
        yield return Mount.Element.FromResources(
            key: $"turn_slot_{characterEntity.Index}_{characterEntity.Version}",
            prefabPath: "UI/TurnOrderSlot",
            props: new TurnOrderSlotProps
            {
              Character = characterData.Value,
              SlotIndex = i,
              IsCurrent = (i == 0),
              TargetPosition = new Vector3(targetX, targetY, 0),
              TargetScale = targetScale,
              TurnNumber = CalculateTurnNumber(i)
            },
            index: i,
            parentTransform: turnSlotContainer
        );
      }
    }

    // ========================================================================
    // HELPER METHODS - NORMALIZED VERSION
    // ========================================================================

    /// <summary>
    /// Builds CharacterData from normalized states using O(1) lookups.
    /// OLD: O(n) loop through PartyState.characters array
    /// NEW: O(1) HashMap lookups across multiple states
    /// </summary>
    private CharacterData? GetCharacterDataFromStates(Entity entity)
    {
      // Lookup name from identity state
      if (!identityState.names.IsCreated ||
          !identityState.names.TryGetValue(entity, out var name))
        return null;

      // Lookup team affiliation
      bool isEnemy = false;
      if (identityState.isEnemy.IsCreated) {
        identityState.isEnemy.TryGetValue(entity, out isEnemy);
      }

      // Lookup health data
      if (!healthState.health.IsCreated ||
          !healthState.health.TryGetValue(entity, out var healthData))
        return null;

      // Build CharacterData struct for props
      // Note: TurnOrderSlot doesn't need all fields, but we provide them for compatibility
      return new CharacterData
      {
        entity = entity,
        name = name,
        currentHealth = healthData.current,
        maxHealth = healthData.max,
        isAlive = healthData.isAlive,
        isEnemy = isEnemy,

        // Fields not used by TurnOrderSlot (defaults)
        currentMana = 0,
        maxMana = 0,
        status = CharacterStatus.None
      };
    }

    private void UpdateTurnCounter()
    {
      if (turnCounterText) {
        turnCounterText.text = $"Turn {battleState.turnCount}";
      }
    }

    private int CalculateTurnNumber(int slotIndex)
    {
      // Calculate the absolute turn number for speed indicators
      return battleState.turnCount + slotIndex;
    }

    // Animation data tracking
    private class TurnSlotAnimationData
    {
      public Vector3 startPosition;
      public Vector3 targetPosition;
      public Vector2 startScale;
      public Vector2 targetScale;
      public float animationTime;
      public bool isAnimating;
    }
  }

  public struct CharacterData
  {
    public Entity entity;
    public FixedString32Bytes name;
    public int currentHealth;
    public int maxHealth;
    public int currentMana;
    public int maxMana;
    public bool isEnemy;
    public bool isAlive;
    public CharacterStatus status;
  }

  // Props for TurnOrderSlot child component
  public class TurnOrderSlotProps : UIProps
  {
    public CharacterData Character { get; set; }
    public int SlotIndex { get; set; }
    public bool IsCurrent { get; set; }
    public Vector3 TargetPosition { get; set; }
    public Vector2 TargetScale { get; set; }
    public int TurnNumber { get; set; }
  }
}