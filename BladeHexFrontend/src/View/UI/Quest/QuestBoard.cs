// QuestBoard.cs
// 任务布告栏界面 — 展示可用任务并允许玩家接取
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Localization;

namespace BladeHex.UI;

[GlobalClass]
public partial class QuestBoard : Control
{
    [Signal] public delegate void CloseRequestedEventHandler();
    [Signal] public delegate void QuestAcceptedEventHandler(string questId);

    private UIFactory _factory = null!;
    private new UITheme Theme => UITheme.Instance!;

    // UI 引用
    private ItemList _questList = null!;
    private RichTextLabel _detailLabel = null!;
    private Button _acceptBtn = null!;

    private QuestManager? _manager;
    private List<QuestData> _availableQuests = new();
    private QuestData? _selectedQuest;

    public override void _Ready()
    {
        _factory = new UIFactory();
        _setup_internal_references();
        _apply_theme();
    }

    private void _setup_internal_references()
    {
        _questList = GetNode<ItemList>("Panel/VBoxContainer/QuestList");
        _detailLabel = GetNode<RichTextLabel>("Panel/VBoxContainer/DetailPanel/VBox/QuestDetail");
        _acceptBtn = GetNode<Button>("Panel/VBoxContainer/DetailPanel/VBox/AcceptButton");

        _questList.ItemSelected += _on_quest_selected;
        _acceptBtn.Pressed += _on_accept_pressed;
        GetNode<Button>("Panel/VBoxContainer/CloseButton").Pressed += () => { Visible = false; EmitSignal(SignalName.CloseRequested); };
    }

    private void _apply_theme()
    {
        var panel = GetNode<Panel>("Panel");
        panel.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(Theme.BgPrimary, Theme.BorderHighlight, 2, Theme.RadiusLg));

        var title = GetNode<Label>("Panel/VBoxContainer/Title");
        title.AddThemeFontSizeOverride("font_size", Theme.FontSizeXl);
        title.AddThemeColorOverride("font_color", Theme.TextAccent);

        Theme.ApplyButtonTheme(_acceptBtn, Theme.MakeButtonStyle(Theme.BgPositive, Theme.BorderDefault));
        Theme.ApplyButtonTheme(GetNode<Button>("Panel/VBoxContainer/CloseButton"));
    }

    public void Open(QuestManager manager, List<QuestData> available)
    {
        _manager = manager;
        _availableQuests = available;
        Refresh();
        Visible = true;
    }

    public void Refresh()
    {
        _questList.Clear();
        foreach (var quest in _availableQuests)
        {
            int idx = _questList.AddItem(quest.Title);
            _questList.SetItemMetadata(idx, quest);
            _questList.SetItemCustomFgColor(idx, Theme.TextPrimary);
            
            // 如果玩家等级不足，变灰
            var gs = BladeHex.Data.Globals.StateOrNull;
            if (gs?.OriginContext.Data.ContainsKey("unit_data") == true)
            {
                var player = gs.OriginContext.Data["unit_data"].As<UnitData>();
                if (player != null && player.Level < quest.RecommendedLevel)
                {
                    _questList.SetItemCustomFgColor(idx, Theme.TextMuted);
                }
            }
        }

        _selectedQuest = null;
        _update_detail();
    }

    private void _on_quest_selected(long index)
    {
        _selectedQuest = _questList.GetItemMetadata((int)index).As<QuestData>();
        _update_detail();
    }

    private void _update_detail()
    {
        if (_selectedQuest == null)
        {
            _detailLabel.Text = L10n.Tr("QUEST_BOARD_SELECT_PROMPT");
            _acceptBtn.Visible = false;
            return;
        }

        string d = $"[b][size={Theme.FontSizeLg}]{_selectedQuest.Title}[/size][/b]\n";
        d += $"[color=gray]{L10n.Tr("QUEST_RECOMMENDED_LEVEL", _selectedQuest.RecommendedLevel)}[/color]\n\n";
        d += $"{_selectedQuest.Description}\n\n";
        
        d += L10n.Tr("QUEST_PAY_HEADER") + "\n";
        d += L10n.Tr("QUEST_REWARD_GOLD_EXP", _selectedQuest.RewardGold, _selectedQuest.RewardReputation) + "\n";
        if (_selectedQuest.RewardReputation != 0)
            d += L10n.Tr("QUEST_REWARD_REPUTATION", _selectedQuest.RewardReputation) + "\n";

        _detailLabel.Text = d;
        _acceptBtn.Visible = true;
        
        // 检查接取条件
        bool canAccept = true;
        string reason = "";
        
        if (_manager?.ActiveQuests.Count >= 10)
        {
            canAccept = false;
            reason = L10n.Tr("QUEST_REASON_LOG_FULL");
        }
        else
        {
            var gs2 = BladeHex.Data.Globals.StateOrNull;
            if (gs2?.OriginContext.Data.ContainsKey("unit_data") == true)
            {
                var player = gs2.OriginContext.Data["unit_data"].As<UnitData>();
                if (player != null && player.Level < _selectedQuest.RecommendedLevel - 2)
                {
                    canAccept = false;
                    reason = L10n.Tr("QUEST_REASON_LEVEL_TOO_LOW");
                }
            }
        }

        _acceptBtn.Disabled = !canAccept;
        if (!canAccept)
            _detailLabel.Text += "\n\n[color=red]" + L10n.Tr("QUEST_CANNOT_ACCEPT", reason) + "[/color]";
    }

    private void _on_accept_pressed()
    {
        if (_manager != null && _selectedQuest != null)
        {
            if (_manager.AcceptQuest(_selectedQuest))
            {
                _availableQuests.Remove(_selectedQuest);
                EmitSignal(SignalName.QuestAccepted, _selectedQuest.QuestId);
                Refresh();
            }
        }
    }
}
