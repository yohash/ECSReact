using System.Collections.Generic;
using UnityEngine;
using ECSReact.Core;
using Unity.Entities;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// Displays all party members' status in a horizontal bar - NORMALIZED VERSION
  /// 
  /// CHANGES FROM OLD:
  /// - Removed PartyState subscription
  /// - Added CharacterRosterState subscription
  /// - Replaced character filtering with direct roster list iteration
  /// - Now passes Entity to CharacterStatusCard props (card does lookups)
  /// </summary>
  public class PartyStatusBar : ReactiveUIComponent<CharacterRosterState, BattleState>
  {
    [Header("Layout Configuration")]
    [SerializeField] private RectTransform allyHeaderContainer;
    [SerializeField] private RectTransform allyLayoutGroup;
    [SerializeField] private RectTransform enemyHeaderContainer;
    [SerializeField] private RectTransform enemyLayoutGroup;

    [Header("Visual Configuration")]
    [SerializeField] private bool showEnemies = true;
    [SerializeField] private bool animateChanges = true;

    private CharacterRosterState rosterState;
    private BattleState battleState;

    public override void OnStateChanged(CharacterRosterState newState)
    {
      rosterState = newState;
      UpdateElements(); // Trigger reconciliation
    }

    public override void OnStateChanged(BattleState newState)
    {
      battleState = newState;
      UpdateElements(); // Update active character highlight
    }

    protected override IEnumerable<UIElement> DeclareElements()
    {
      // Create party section header
      yield return Mount.Element.FromResources(
          key: "party_header",
          props: new SectionHeaderProps { Title = "Your Party", IsEnemy = false },
          prefabPath: "UI/StatusSectionHeader",
          index: 0,
          parentTransform: allyHeaderContainer
      );

      // Generate character cards for PLAYERS from roster
      // OLD: Loop through all characters, filter by isEnemy flag
      // NEW: Directly iterate player list - O(n) but n is smaller!
      int cardIndex = 1;
      for (int i = 0; i < rosterState.players.Length; i++) {
        Entity playerEntity = rosterState.players[i];

        // Skip if entity is null (shouldn't happen, but defensive)
        if (playerEntity == Entity.Null)
          continue;

        // Create card with Entity reference
        // Card will look up its own data from normalized states
        yield return Mount.Element.FromResources(
            key: $"player_{playerEntity.Index}_{playerEntity.Version}",
            prefabPath: "UI/CharacterStatusCard",
            props: new CharacterStatusProps
            {
              CharacterEntity = playerEntity,  // NEW: Just Entity, not full data
              IsActive = IsCharacterActive(playerEntity),
              IsTargeted = IsCharacterTargeted(playerEntity),
              CardIndex = i,
              ShowMana = true,
              AnimateChanges = animateChanges
            },
            index: cardIndex++,
            parentTransform: allyLayoutGroup
        );
      }

      // Enemy section (if enabled)
      if (showEnemies && HasEnemies()) {
        yield return Mount.Element.FromResources(
            key: "enemy_header",
            props: new SectionHeaderProps { Title = "Enemies", IsEnemy = true },
            prefabPath: "UI/StatusSectionHeader",
            index: cardIndex++,
            parentTransform: enemyHeaderContainer
        );

        // Generate character cards for ENEMIES from roster
        // OLD: Loop through all characters, filter by isEnemy flag
        // NEW: Directly iterate enemy list
        for (int i = 0; i < rosterState.enemies.Length; i++) {
          Entity enemyEntity = rosterState.enemies[i];

          if (enemyEntity == Entity.Null)
            continue;

          yield return Mount.Element.FromResources(
              key: $"enemy_{enemyEntity.Index}_{enemyEntity.Version}",
              prefabPath: "UI/CharacterStatusCard",
              props: new CharacterStatusProps
              {
                CharacterEntity = enemyEntity,  // NEW: Just Entity
                IsActive = IsCharacterActive(enemyEntity),
                IsTargeted = IsCharacterTargeted(enemyEntity),
                CardIndex = i,
                ShowMana = false,  // Enemies don't show mana
                AnimateChanges = animateChanges
              },
              index: cardIndex++,
              parentTransform: enemyLayoutGroup
          );
        }
      }
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    private bool IsCharacterActive(Entity entity)
    {
      if (battleState.activeCharacterIndex < 0 ||
          battleState.activeCharacterIndex >= battleState.turnOrder.Length)
        return false;

      return battleState.turnOrder[battleState.activeCharacterIndex] == entity;
    }

    private bool IsCharacterTargeted(Entity entity)
    {
      // Would check UIBattleState.selectedTarget in full implementation
      return false;
    }

    private bool HasEnemies()
    {
      // NEW: Simple check using cached count
      return rosterState.enemyCount > 0;
    }
  }
}