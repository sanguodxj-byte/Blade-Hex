// BattleResultPanel.cs
// 战斗结算面板 — 战斗结束后显示战果总结
// 显示：胜负 / 阵亡名单 / 获得金币 / 获得经验 / 战利品
using Godot;
using BladeHex.Strategic;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战斗结算面板 — 战斗结束后显示战果总结
/// </summary>
[GlobalClass]
public partial class BattleResultPanel : CanvasLayer
{
    // ============================================================================
    // 信号
    // ============================================================================
    [Signal]
    public delegate void ResultAcknowledgedEventHandler();

    // ============================================================================
    // 内部
    // ============================================================================
    private readonly UIFactory _factory = new();
    private Control _root = null!;
    private Label _titleLabel = null!;
    private VBoxContainer _contentVBox = null!;
    private Button _continueBtn = null!;

    public override void _Ready()
    {
        Layer = 30;
        SetupUI();
    }

    private void SetupUI()
    {
        _root = new Control();
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.Visible = false;
        AddChild(_root);

        var overlay = new ColorRect();
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.Color = new Color(0, 0, 0, 0.7f);
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(overlay);

        var panel = _factory.CreatePanel(new Vector2(450, 400), UITheme.Instance!.BgPrimary, UITheme.Instance.BorderHighlight);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        panel.OffsetLeft = -225;
        panel.OffsetTop = -210;
        panel.OffsetRight = 225;
        panel.OffsetBottom = 210;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(panel);

        var margin = _factory.CreateMargin(20, 20, 15, 15);
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", UITheme.Instance.SpacingMd);
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddChild(vbox);

        _titleLabel = _factory.CreateTitleLabel("", 22);
        vbox.AddChild(_titleLabel);

        vbox.AddChild(_factory.CreateSeparatorH());

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(410, 260);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(scroll);

        _contentVBox = new VBoxContainer();
        _contentVBox.AddThemeConstantOverride("separation", UITheme.Instance.SpacingSm);
        _contentVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_contentVBox);

        _continueBtn = _factory.CreateButton("继续", new Vector2(410, 40));
        _continueBtn.Pressed += () =>
        {
            HidePanel();
            EmitSignal(SignalName.ResultAcknowledged);
        };
        vbox.AddChild(_continueBtn);
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>
    /// 显示战斗结果
    /// </summary>
    /// <param name="victory">是否胜利</param>
    /// <param name="outcome">战斗结算数据</param>
    public void ShowResult(bool victory, BattleOutcome? outcome)
    {
        foreach (Node child in _contentVBox.GetChildren())
        {
            child.QueueFree();
        }

        if (victory)
        {
            _titleLabel.Text = "战斗胜利！";
            _titleLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.9f, 0.3f));
        }
        else
        {
            _titleLabel.Text = "战斗失败...";
            _titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f));
        }

        if (outcome == null)
        {
            _root.Visible = true;
            return;
        }

        // 金币
        int gold = outcome.GoldGranted;
        if (gold > 0)
        {
            AddRow("获得金币", $"{gold} 金", new Color(1.0f, 0.85f, 0.0f));
        }

        // 经验
        int xp = outcome.XpGranted;
        if (xp > 0)
        {
            AddRow("获得经验", $"{xp} XP", new Color(0.4f, 0.8f, 1.0f));
        }

        // 存活
        var survivors = outcome.SurvivorHp;
        if (survivors != null && survivors.Count > 0)
        {
            AddSection("存活队员");
            foreach (var kvp in survivors)
            {
                AddRow("  " + kvp.Key, $"HP: {kvp.Value}", new Color(0.6f, 0.9f, 0.6f));
            }
        }

        // 阵亡
        var dead = outcome.DeadUnitNames;
        if (dead != null && dead.Count > 0)
        {
            AddSection("阵亡");
            foreach (string name in dead)
            {
                AddRow("  " + name, "永久阵亡", new Color(0.9f, 0.3f, 0.3f));
            }
        }

        // 战利品
        var loot = outcome.LootEntries;
        if (loot != null && loot.Count > 0)
        {
            AddSection("战利品");
            foreach (LootEntry entry in loot)
            {
                string typeIcon = GetLootTypeIcon(entry.Type);
                string qtyText = entry.Quantity > 1 ? $"×{entry.Quantity}" : "";
                string valueText = entry.Value > 0 ? $"{entry.Value}金" : "";
                AddRow($"  {typeIcon} {entry.ItemName}{qtyText}",
                    valueText,
                    new Color(0.9f, 0.8f, 0.4f));
            }
        }

        _root.Visible = true;
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void HidePanel()
    {
        _root.Visible = false;
    }

    /// <summary>
    /// 面板是否可见
    /// </summary>
    public bool IsPanelVisible()
    {
        return _root.Visible;
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    private void AddRow(string leftText, string rightText, Color color)
    {
        var row = new HBoxContainer();
        var left = _factory.CreateBodyLabel(leftText);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(left);
        var right = _factory.CreateBodyLabel(rightText);
        right.AddThemeColorOverride("font_color", color);
        row.AddChild(right);
        _contentVBox.AddChild(row);
    }

    private void AddSection(string title)
    {
        _contentVBox.AddChild(_factory.CreateSeparatorH());
        var lbl = _factory.CreateBodyLabel(title);
        lbl.AddThemeColorOverride("font_color", UITheme.Instance!.TextAccent);
        _contentVBox.AddChild(lbl);
    }

    private static string GetLootTypeIcon(LootEntry.LootType type)
    {
        return type switch
        {
            LootEntry.LootType.Weapon     => "⚔",
            LootEntry.LootType.Armor      => "🛡",
            LootEntry.LootType.Shield     => "🛡",
            LootEntry.LootType.Helmet     => "⛑",
            LootEntry.LootType.Consumable => "🧪",
            LootEntry.LootType.Gold       => "💰",
            LootEntry.LootType.Material   => "📦",
            _ => "📦",
        };
    }
}
