using System.Collections.Generic;
using UnityEngine;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Displays all party members' status in a horizontal bar.
  /// Demonstrates dynamic element creation based on party composition.
  /// </summary>
  public class PartyStatusBar : ReactiveUIComponent<PartyState, BattleState>
  {
    [Header("Layout Configuration")]
    [SerializeField] private RectTransform allyHeaderContainer;
    [SerializeField] private RectTransform allyLayoutGroup;
    [SerializeField] private RectTransform enemyHeaderContainer;
    [SerializeField] private RectTransform enemyLayoutGroup;

    [Header("Layout Configuration")]
    [SerializeField] private float cardSpacing = 10f;
    [SerializeField] private bool showEnemies = true;
    [SerializeField] private bool animateChanges = true;

    private PartyState partyState;
    private BattleState battleState;

    public override void OnStateChanged(PartyState newState)
    {
      partyState = newState;
      UpdateElements(); // Trigger reconciliation
    }

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateElements(); // Update active character highlight
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      // Create section headers
      yield return Mount.Element.FromResources(
          key: "party_header",
          props: new SectionHeaderProps { Title = "Your Party", IsEnemy = false },
          prefabPath: "UI/StatusSectionHeader",
          index: 0,
          parentTransform: allyHeaderContainer
      );

      // Generate character cards for party members
      int cardIndex = 1;
      for (int i = 0; i < partyState.characters.Length; i++) {
        var character = partyState.characters[i];

        if (character.isEnemy)
          continue;

        // Skip dead characters (unless we want to show them grayed out)
        if (!character.isAlive && !ShouldShowDeadCharacters())
          continue;

        // Create character card with props
        yield return Mount.Element.FromResources(
            key: $"character_{character.entity.Index}_{character.entity.Version}",
            prefabPath: "UI/CharacterStatusCard",
            props: new CharacterStatusProps
            {
              Character = character,
              IsActive = IsCharacterActive(character.entity),
              IsTargeted = IsCharacterTargeted(character.entity),
              CardIndex = i,
              ShowMana = !character.isEnemy, // Only show mana for party
              AnimateChanges = animateChanges
            },
            index: cardIndex++,
            parentTransform: allyLayoutGroup
        );
      }

      // Enemy section header (if showing enemies)
      if (showEnemies && HasEnemies()) {
        yield return Mount.Element.FromResources(
            key: "enemy_header",
            prefabPath: "UI/StatusSectionHeader",
            props: new SectionHeaderProps { Title = "Enemies", IsEnemy = true },
            index: cardIndex++,
            parentTransform: enemyHeaderContainer
        );

        // Generate enemy cards
        for (int i = 0; i < partyState.characters.Length; i++) {
          var character = partyState.characters[i];

          if (!character.isEnemy)
            continue;
          if (!character.isAlive && !ShouldShowDeadCharacters())
            continue;

          yield return Mount.Element.FromResources(
              key: $"enemy_{character.entity.Index}_{character.entity.Version}",
              prefabPath: "UI/CharacterStatusCard",
              props: new CharacterStatusProps
              {
                Character = character,
                IsActive = IsCharacterActive(character.entity),
                IsTargeted = IsCharacterTargeted(character.entity),
                CardIndex = i,
                ShowMana = false,
                AnimateChanges = animateChanges
              },
              index: cardIndex++,
              parentTransform: enemyLayoutGroup
          );
        }
      }
    }

    private bool IsCharacterActive(Unity.Entities.Entity entity)
    {
      if (battleState.activeCharacterIndex < 0 ||
          battleState.activeCharacterIndex >= battleState.turnOrder.Length)
        return false;

      return battleState.turnOrder[battleState.activeCharacterIndex] == entity;
    }

    private bool IsCharacterTargeted(Unity.Entities.Entity entity)
    {
      // Would check UIBattleState.selectedTarget in full implementation
      return false;
    }

    private bool HasEnemies()
    {
      for (int i = 0; i < partyState.characters.Length; i++) {
        if (partyState.characters[i].isEnemy && partyState.characters[i].isAlive)
          return true;
      }
      return false;
    }

    private bool ShouldShowDeadCharacters()
    {
      // In full implementation, might check a settings state
      return true; // Show them grayed out
    }
  }

  public class SectionHeaderProps : UIProps
  {
    public string Title { get; set; }
    public bool IsEnemy { get; set; }
  }
}