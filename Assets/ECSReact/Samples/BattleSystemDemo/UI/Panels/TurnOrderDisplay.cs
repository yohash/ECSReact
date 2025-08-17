using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using Unity.Entities;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Displays upcoming turn order with smooth animations.
  /// Demonstrates element reordering and dynamic updates based on state changes.
  /// </summary>
  public class TurnOrderDisplay : ReactiveUIComponent<BattleState, PartyState>
  {
    [Header("UI Configuration")]
    [SerializeField] private Transform turnSlotContainer;
    //[SerializeField] private Transform currentTurnHighlight;
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
    private PartyState partyState;
    private Dictionary<Entity, TurnSlotAnimationData> animationData = new Dictionary<Entity, TurnSlotAnimationData>();
    private int previousActiveIndex = -1;

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;

      UpdateElements();
      UpdateTurnCounter();
    }

    public override void OnStateChanged(PartyState newState)
    {
      partyState = newState;
      UpdateElements(); // Refresh portraits if character data changes
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

        // Find character data
        CharacterData? characterData = GetCharacterData(characterEntity);
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

      // Turn prediction preview (shows what happens after current visible turns)
      if (turnsToShow < battleState.turnOrder.Length) {
        //yield return UIElement.FromComponent<TurnCycleIndicator>(
        //    key: "turn_cycle",
        //    props: new TurnCycleProps
        //    {
        //      RemainingTurns = battleState.turnOrder.Length - turnsToShow,
        //      CycleNumber = battleState.turnCount / battleState.turnOrder.Length
        //    },
        //    index: turnsToShow,
        //    parentTransform: turnSlotContainer
        //);
      }
    }

    private CharacterData? GetCharacterData(Entity entity)
    {
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].entity == entity)
          return partyState.characters[i];
      }
      return null;
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
}