// QuestLog.cs
// 任务日志界面 — 管理正在进行和已完成的任务
// 纯代码构建 UI（无 .tscn 依赖）
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Localization;

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
        _BuildUI();
        _apply_theme();
    }

    // ========================================
    // UI 构建（纯代码）
    // ========================================

    private void _BuildUI()
    {
        // 根面板
        var panel = new Panel();
        panel.Name = "Panel";
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.OffsetLeft = 80; panel.OffsetRight = -80;
        panel.OffsetTop = 40; panel.OffsetBottom = -40;
        AddChild(panel);

        // 主垂直布局
        var vbox = new VBoxContainer();
        vbox.Name = "VBoxContainer";
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 16; vbox.OffsetRight = -16;
        vbox.OffsetTop = 16; vbox.OffsetBottom = -16;
        vbox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(vbox);

        // 标题
        var title = new Label();
        title.Name = "Title";
        title.Text = L10n.Tr("QUEST_LOG_TITLE");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // Tab 容器（进行中 / 已完成）
        _tabContainer = new TabContainer();
        _tabContainer.Name = "TabContainer";
        _tabContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(_tabContainer);

        // 进行中 tab
        var activeTab = new VBoxContainer();
        activeTab.Name = L10n.Tr("QUEST_TAB_ACTIVE");
        _tabContainer.AddChild(activeTab);

        _activeList = new ItemList();
        _activeList.Name = "ActiveQuestList";
        _activeList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _activeList.SelectMode = ItemList.SelectModeEnum.Single;
        activeTab.AddChild(_activeList);

        // 已完成 tab
        var completedTab = new VBoxContainer();
        completedTab.Name = L10n.Tr("QUEST_TAB_COMPLETED");
        _tabContainer.AddChild(completedTab);

        _completedList = new ItemList();
        _completedList.Name = "CompletedQuestList";
        _completedList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _completedList.SelectMode = ItemList.SelectModeEnum.Single;
        completedTab.AddChild(_completedList);

        // 详情面板
        var detailPanel = new PanelContainer();
        detailPanel.Name = "DetailPanel";
        detailPanel.CustomMinimumSize = new Vector2(0, 200);
        vbox.AddChild(detailPanel);

        var detailVbox = new VBoxContainer();
        detailVbox.Name = "VBox";
        detailVbox.AddThemeConstantOverride("separation", 8);
        detailPanel.AddChild(detailVbox);

        _detailLabel = new RichTextLabel();
        _detailLabel.Name = "QuestDetail";
        _detailLabel.BbcodeEnabled = true;
        _detailLabel.ScrollActive = true;
        _detailLabel.FitContent = false;
        _detailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detailLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _detailLabel.Text = L10n.Tr("QUEST_LOG_SELECT_PROMPT");
        detailVbox.AddChild(_detailLabel);

        _abandonBtn = new Button();
        _abandonBtn.Name = "AbandonButton";
        _abandonBtn.Text = L10n.Tr("QUEST_ABANDON");
        _abandonBtn.Visible = false;
        detailVbox.AddChild(_abandonBtn);

        // 关闭按钮
        var closeBtn = new Button();
        closeBtn.Name = "CloseButton";
        closeBtn.Text = L10n.Tr("QUEST_CLOSE");
        closeBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        vbox.AddChild(closeBtn);

        // 连接信号
        _activeList.ItemSelected += (idx) => _on_quest_selected(idx, true);
        _completedList.ItemSelected += (idx) => _on_quest_selected(idx, false);
        _abandonBtn.Pressed += _on_abandon_pressed;
        closeBtn.Pressed += () => { Visible = false; EmitSignal(SignalName.CloseRequested); };
    }

    // ========================================
    // 主题
    // ========================================

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

    // ========================================
    // 公共 API
    // ========================================

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

    // ========================================
    // 内部逻辑
    // ========================================

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
            _detailLabel.Text = L10n.Tr("QUEST_LOG_SELECT_PROMPT");
            _abandonBtn.Visible = false;
            return;
        }

        string d = $"[b][size={Theme.FontSizeLg}]{_selectedQuest.Title}[/size][/b]\n";
        d += $"[color=gray]{L10n.Tr("QUEST_DETAIL_META", _selectedQuest.Type, _selectedQuest.RecommendedLevel)}[/color]\n\n";
        d += $"{_selectedQuest.Description}\n\n";

        d += L10n.Tr("QUEST_OBJECTIVES_HEADER") + "\n";
        d += $"{_selectedQuest.Objectives}\n";

        d += "\n" + L10n.Tr("QUEST_REWARD_HEADER") + "\n";
        d += L10n.Tr("QUEST_REWARD_GOLD_EXP", _selectedQuest.RewardGold, _selectedQuest.RewardReputation) + "\n";
        if (_selectedQuest.RewardReputation != 0)
            d += L10n.Tr("QUEST_REWARD_REPUTATION", _selectedQuest.RewardReputation) + "\n";

        _detailLabel.Text = d;
        _abandonBtn.Visible = _manager?.ActiveQuests.Contains(_selectedQuest) ?? false;
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
