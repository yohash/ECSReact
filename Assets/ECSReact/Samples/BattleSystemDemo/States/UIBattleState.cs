using System;
using Unity.Entities;
using Unity.Collections;
using ECSReact.Core;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// UI-specific state for menu navigation and selection
  /// </summary>
  public struct UIBattleState : IGameState, IEquatable<UIBattleState>
  {
    public ActionType selectedAction;
    public Entity selectedTarget;
    public int selectedSkillId;
    public int selectedItemId;
    public bool showTargetingMode;
    public bool showActionMenu;
    public MenuPanel activePanel;
    public FixedString128Bytes lastMessage;

    public bool Equals(UIBattleState other)
    {
      return selectedAction == other.selectedAction &&
             selectedTarget == other.selectedTarget &&
             selectedSkillId == other.selectedSkillId &&
             selectedItemId == other.selectedItemId &&
             showTargetingMode == other.showTargetingMode &&
             showActionMenu == other.showActionMenu &&
             activePanel == other.activePanel &&
             lastMessage == other.lastMessage;
    }
  }

  public enum ActionType
  {
    None,
    Attack,
    Skill,
    Item,
    Defend,
    Run
  }

  public enum MenuPanel
  {
    None,
    MainActions,
    SkillList,
    ItemList,
    TargetSelection
  }
}