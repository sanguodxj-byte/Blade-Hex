// QuestLog.cs
// 任务日志界面 — 管理正在进行和已完成的任务
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.UI;

[GlobalClass]
public partial class QuestLog : Control
{
    [Signal] public delegate void CloseRequestedEventHandler();

    private UIFactory _factory = null!;
    private new UITheme Theme => UITheme.Instance!;

    // UI 引用
    private ItemList _activeList = null!;
    private ItemList _completedList = null!;
    private RichTextLabel _detailLabel = null!;
    private Button _abandonBtn = null!;
    private TabContainer _tabContainer = null!;

    private QuestManager? _manager;
    private QuestData? _selectedQuest;

    public override void _Ready()
    {
        _factory = new UIFactory();
        _setup_internal_references();
        _apply_theme();
    }

    private void _setup_internal_references()
    {
        _activeList = GetNode<ItemList>("Panel/VBoxContainer/TabContainer/进行中/ActiveQuestList");
        _completedList = GetNode<ItemList>("Panel/VBoxContainer/TabContainer/已完成/CompletedQuestList");
        _detailLabel = GetNode<RichTextLabel>("Panel/VBoxContainer/DetailPanel/VBox/QuestDetail");
        _abandonBtn = GetNode<Button>("Panel/VBoxContainer/DetailPanel/VBox/AbandonButton");
        _tabContainer = GetNode<TabContainer>("Panel/VBoxContainer/TabContainer");

        _activeList.ItemSelected += (idx) => _on_quest_selected(idx, true);
        _completedList.ItemSelected += (idx) => _on_quest_selected(idx, false);
        _abandonBtn.Pressed += _on_abandon_pressed;
        GetNode<Button>("Panel/VBoxContainer/CloseButton").Pressed += () => { Visible = false; EmitSignal(SignalName.CloseRequested); };
    }

    private void _apply_theme()
    {
        var panel = GetNode<Panel>("Panel");
        panel.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(Theme.BgPrimary, Theme.BorderHighlight, 2, Theme.RadiusLg));

        var title = GetNode<Label>("Panel/VBoxContainer/Title");
        title.AddThemeFontSizeOverride("font_size", Theme.FontSizeXl);
        title.AddThemeColorOverride("font_color", Theme.TextAccent);

        Theme.ApplyButtonTheme(_abandonBtn, Theme.MakeButtonStyle(Theme.BgNegative, Theme.BorderDefault));
        Theme.ApplyButtonTheme(GetNode<Button>("Panel/VBoxContainer/CloseButton"));
    }

    public void Open(QuestManager manager)
    {
        _manager = manager;
        Refresh();
        Visible = true;
    }

    public void Refresh()
    {
        if (_manager == null) return;

        _activeList.Clear();
        foreach (var quest in _manager.ActiveQuests)
        {
            int idx = _activeList.AddItem(quest.Title);
            _activeList.SetItemMetadata(idx, quest);
            _activeList.SetItemCustomFgColor(idx, Theme.TextAccent);
        }

        _completedList.Clear();
        foreach (var questId in _manager.CompletedQuestIds)
        {
            int idx = _completedList.AddItem(questId);
            _completedList.SetItemCustomFgColor(idx, Theme.TextPositive);
        }

        _selectedQuest = null;
        _update_detail();
    }

    private void _on_quest_selected(long index, bool active)
    {
        var list = active ? _activeList : _completedList;
        _selectedQuest = list.GetItemMetadata((int)index).As<QuestData>();
        _update_detail();
    }

    private void _update_detail()
    {
        if (_selectedQuest == null)
        {
            _detailLabel.Text = "[center][color=gray]请选择一个任务查看详情[/color][/center]";
            _abandonBtn.Visible = false;
            return;
        }

        string d = $"[b][size={Theme.FontSizeLg}]{_selectedQuest.Title}[/size][/b]\n";
        d += $"[color=gray]类型: {_selectedQuest.Type} | 推荐等级: {_selectedQuest.RecommendedLevel}[/color]\n\n";
        d += $"{_selectedQuest.Description}\n\n";
        
        d += "[b]任务目标:[/b]\n";
        d += $"{_selectedQuest.Objectives}\n";

        d += $"\n[b]奖励:[/b]\n💰 金币: {_selectedQuest.RewardGold} | ✨ 经验: {_selectedQuest.RewardReputation}\n";
        if (_selectedQuest.RewardReputation != 0)
            d += $"🤝 声望: {_selectedQuest.RewardReputation}\n";

        _detailLabel.Text = d;
        _abandonBtn.Visible = _manager?.ActiveQuests.Contains(_selectedQuest) ?? false;
        _abandonBtn.Disabled = false; // All active quests can be abandoned
    }

    private void _on_abandon_pressed()
    {
        if (_manager != null && _selectedQuest != null)
        {
            _manager.FailQuest(_selectedQuest);
            Refresh();
        }
    }
}
