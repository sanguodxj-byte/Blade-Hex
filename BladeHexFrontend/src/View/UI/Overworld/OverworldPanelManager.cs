// OverworldPanelManager.cs
// 大地图子面板管理器 — 解耦重构自 OverworldUI.cs
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.UI;
using BladeHex.UI.Common;
using BladeHex.Data;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Scenes.Overworld;

namespace BladeHex.View.UI.Overworld;

public class OverworldPanelManager
{
    private readonly Control _root;
    private readonly OverworldUI _ui;

    public PartyPanel PartyPanel { get; private set; } = null!;
    public EconomyPanel? EconomyPanel { get; private set; }
    public KingdomPanel? KingdomPanel { get; private set; }
    public WorldNewsPanel? NewsPanel { get; private set; }
    public BladeHex.View.UI.Encyclopedia.EncyclopediaIndexPanel? EncyclopediaPanel { get; private set; }
    public BladeHex.UI.SkillTreeUI? SkillTreeUi { get; private set; }
    public BladeHex.UI.QuestLog? QuestLog { get; private set; }

    public OverworldPanelManager(Control root, OverworldUI ui)
    {
        _root = root;
        _ui = ui;
        InitPanels();
    }

    private void InitPanels()
    {
        PartyPanel = new PartyPanel();
        PartyPanel.Visible = false;
        PartyPanel.PanelClosed += () => _ui.EmitSignal(OverworldUI.SignalName.PanelDismissed);
        _root.AddChild(PartyPanel);

        KingdomPanel = new KingdomPanel();
        KingdomPanel.Visible = false;
        _root.AddChild(KingdomPanel);
        OverlayPanelLayout.Center(KingdomPanel);

        NewsPanel = new WorldNewsPanel();
        NewsPanel.Visible = false;
        _root.AddChild(NewsPanel);
        OverlayPanelLayout.Center(NewsPanel);

        EncyclopediaPanel = new BladeHex.View.UI.Encyclopedia.EncyclopediaIndexPanel();
        EncyclopediaPanel.Visible = false;
        _root.AddChild(EncyclopediaPanel);
        OverlayPanelLayout.Center(EncyclopediaPanel);
    }

    public void CloseAllPanels(bool emitDismissSignal = true)
    {
        bool anyWasOpen = (PartyPanel != null && PartyPanel.Visible) ||
            (SkillTreeUi != null && SkillTreeUi.Get("visible").AsBool()) ||
            (QuestLog != null && QuestLog is Control qlc2 && qlc2.Visible) ||
            (KingdomPanel != null && KingdomPanel.Visible) ||
            (NewsPanel != null && NewsPanel.Visible) ||
            (EconomyPanel != null && EconomyPanel.Visible) ||
            (EncyclopediaPanel != null && EncyclopediaPanel.Visible);

        if (PartyPanel != null) PartyPanel.Visible = false;
        if (SkillTreeUi != null) SkillTreeUi.Set("visible", false);
        if (QuestLog != null && QuestLog is Control qlCtrl) qlCtrl.Visible = false;
        if (KingdomPanel != null) KingdomPanel.Visible = false;
        if (NewsPanel != null) NewsPanel.Visible = false;
        if (EconomyPanel != null) EconomyPanel.Visible = false;
        if (EncyclopediaPanel != null) EncyclopediaPanel.Visible = false;

        if (anyWasOpen && emitDismissSignal)
            _ui.EmitSignal(OverworldUI.SignalName.PanelDismissed);
    }

    public bool IsPanelVisible(string actionName)
    {
        return actionName switch
        {
            "army" or "inventory" or "party" => PartyPanel != null && PartyPanel.Visible,
            "skill_tree" => SkillTreeUi != null && SkillTreeUi.Get("visible").AsBool(),
            "quests" => QuestLog is Control qlCtrl && qlCtrl.Visible,
            "economy_panel" => EconomyPanel != null && EconomyPanel.Visible,
            "kingdom_panel" => KingdomPanel != null && KingdomPanel.Visible,
            "news_panel" => NewsPanel != null && NewsPanel.Visible,
            "encyclopedia_panel" => EncyclopediaPanel != null && EncyclopediaPanel.Visible,
            _ => false
        };
    }

    public void OpenPartyPanel()
    {
        var ctx = _ui.GetContextInternal();
        PartyRoster? roster = null;
        PartyInventory? inventory = null;

        if (ctx?.PlayerParty != null)
        {
            roster = ctx.PlayerParty.Roster;
            inventory = ctx.PlayerParty.Inventory;
        }

        if (roster != null && inventory != null)
            PartyPanel.Open(roster, inventory);
        else
            PartyPanel.OpenTab("party");
    }

    public void OpenPartyShop(string shopName, EconomyManager economy, List<ItemData> stock, int prosperity, OverworldPOI? poi, ReputationTracker? reputation, WorldEventEngine? worldEngine, IOverworldContext? overworldScene)
    {
        CloseAllPanels();
        var ctx = _ui.GetContextInternal();
        PartyRoster? roster = ctx?.PlayerParty?.Roster;
        PartyInventory? inventory = ctx?.PlayerParty?.Inventory;

        if (roster != null && inventory != null)
            PartyPanel.OpenShop(roster, inventory, shopName, economy, stock, prosperity, poi, reputation, worldEngine, overworldScene);
        else
            PartyPanel.OpenTab("party");
    }

    public void OpenPartyLoot(List<ItemData> loot, int goldGranted, int xpGranted)
    {
        CloseAllPanels();
        var ctx = _ui.GetContextInternal();
        PartyRoster? roster = ctx?.PlayerParty?.Roster;
        PartyInventory? inventory = ctx?.PlayerParty?.Inventory;

        if (roster != null && inventory != null)
            PartyPanel.OpenLoot(roster, inventory, loot, goldGranted, xpGranted);
        else
            PartyPanel.OpenTab("party");
    }

    public void ToggleEconomyPanel()
    {
        if (EconomyPanel == null)
        {
            EconomyPanel = new EconomyPanel();
            if (_ui.EconomyManager is BladeHex.Data.EconomyManager em)
                EconomyPanel.Economy = em;
            _root.AddChild(EconomyPanel);
        }

        if (EconomyPanel.Visible)
        {
            EconomyPanel.Visible = false;
        }
        else
        {
            if (_ui.EconomyManager is BladeHex.Data.EconomyManager economy)
                EconomyPanel.Economy = economy;
            EconomyPanel.Refresh();
            EconomyPanel.Visible = true;
        }
    }

    public void OpenQuestLog()
    {
        if (QuestLog == null)
        {
            QuestLog = new BladeHex.UI.QuestLog();
            if (QuestLog is Control questCtrl)
            {
                questCtrl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _root.AddChild(questCtrl);
            }
        }

        if (QuestLog is Control qc)
            qc.Visible = true;
        else if (QuestLog.HasMethod("show_log"))
            QuestLog.Call("show_log");
    }

    public void OpenSkillTree()
    {
        if (SkillTreeUi == null)
        {
            SkillTreeUi = new BladeHex.UI.SkillTreeUI();
            if (SkillTreeUi is Control skillCtrl)
            {
                skillCtrl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _root.AddChild(skillCtrl);
            }
        }

        var stm = BladeHex.Data.Globals.SkillTreesOrNull;
        if (stm == null || stm.TreeData == null)
        {
            GD.PrintErr("[OverworldPanelManager] SkillTreeManager 未初始化");
            if (SkillTreeUi is Control sc2) sc2.Visible = true;
            return;
        }

        CharacterSkillTree? charTree = null;
        UnitData? leader = null;
        IReadOnlyList<UnitData>? roster = null;
        var ctx = _ui.GetContextInternal();
        if (ctx?.PlayerParty?.Roster != null && ctx.PlayerParty.Roster.Count > 0)
        {
            leader = ctx.PlayerParty.Roster.Members[0];
            roster = ctx.PlayerParty.Roster.Members;
            long charId = (long)leader.GetInstanceId();

            charTree = stm.GetSkillTree(charId) ?? stm.CreateSkillTree(charId, leader.Level);
            stm.InitCharacterLevel(charId, leader.Level);
        }

        if (charTree == null)
        {
            charTree = new CharacterSkillTree(stm.TreeData, 1);
        }

        if (SkillTreeUi is BladeHex.UI.SkillTreeUI skillTreeUi)
        {
            skillTreeUi.OpenSkillTree(charTree, stm.TreeData, leader, roster);
        }
        else if (SkillTreeUi is Control sc)
        {
            sc.Visible = true;
        }
    }

    public void ToggleKingdomPanel()
    {
        if (KingdomPanel == null) return;

        if (KingdomPanel.Visible)
        {
            KingdomPanel.Visible = false;
        }
        else
        {
            var ctx = _ui.GetContextInternal();
            if (_ui.EntityMgr != null && ctx?.ReputationTracker != null)
            {
                int currentDay = ctx.CurrentDay;
                KingdomPanel.Initialize(_ui.EntityMgr, ctx.ReputationTracker, currentDay);
                OverlayPanelLayout.Center(KingdomPanel);
                KingdomPanel.Visible = true;
            }
        }
    }

    public void ToggleNewsPanel()
    {
        if (NewsPanel == null) return;

        if (NewsPanel.Visible)
        {
            NewsPanel.Visible = false;
        }
        else
        {
            if (_ui.EntityMgr != null)
            {
                NewsPanel.Initialize(_ui.EntityMgr);
                OverlayPanelLayout.Center(NewsPanel);
                NewsPanel.Visible = true;
            }
        }
    }

    public void ToggleEncyclopediaPanel()
    {
        if (EncyclopediaPanel == null) return;

        if (EncyclopediaPanel.Visible)
        {
            EncyclopediaPanel.Visible = false;
        }
        else
        {
            if (_ui.EntityMgr != null)
            {
                EncyclopediaPanel.Initialize(_ui.EntityMgr, _ui.EntityMgr.Journal);
                OverlayPanelLayout.Center(EncyclopediaPanel);
                EncyclopediaPanel.Visible = true;
            }
        }
    }
}
