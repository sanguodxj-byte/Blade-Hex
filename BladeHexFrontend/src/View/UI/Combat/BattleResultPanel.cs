// BattleResultPanel.cs
// 战斗结算面板 — 胜利/失败后显示经验/金币/掉落物/队员状态
// 通用组件：支持战役模式和大地图模式
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.UI;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战斗结算面板。在战斗结束后弹出,显示:
/// - 胜利/失败标题（带动画）
/// - 获得经验值/金币
/// - 掉落物品列表（带稀有度颜色）
/// - 队员状态（存活/重伤/阵亡/升级）
/// - "继续"按钮关闭面板
/// </summary>
[GlobalClass]
public partial class BattleResultPanel : CanvasLayer
{
    [Signal] public delegate void ContinueClickedEventHandler();

    private UITheme Theme => UITheme.Instance!;
    private UIFactory _factory = null!;

    // UI引用
    private PanelContainer _mainPanel = null!;
    private VBoxContainer _contentVbox = null!;
    private Label _titleLabel = null!;
    private RichTextLabel _descLabel = null!;
    private VBoxContainer _rewardsSection = null!;
    private VBoxContainer _unitStatusSection = null!;
    private VBoxContainer _lootSection = null!;
    private Button _continueBtn = null!;

    // 动画相关
    private float _animTimer = 0f;
    private readonly List<Control> _animQueue = new();
    private int _animIndex = 0;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _factory = new UIFactory();
    }

    public override void _Process(double delta)
    {
        if (_animIndex < _animQueue.Count)
        {
            _animTimer += (float)delta;
            if (_animTimer >= 0.1f)
            {
                _animTimer = 0f;
                var ctrl = _animQueue[_animIndex];
                ctrl.Visible = true;
                ctrl.Modulate = new Color(1, 1, 1, 0);
                CreateTween().TweenProperty(ctrl, "modulate", new Color(1, 1, 1, 1), 0.2f);
                _animIndex++;
            }
        }
    }

    // ============================================================
    // 公开接口 — 简单模式（向后兼容）
    // ============================================================

    /// <summary>显示结算面板（简单模式，兼容旧调用）</summary>
    public void Show(bool victory, int xp, int gold, string[] lootNames)
    {
        var lootEntries = new List<LootEntry>();
        foreach (var name in lootNames)
            lootEntries.Add(new LootEntry(name, LootEntry.LootType.Material));

        ShowResult(victory, xp, gold, lootEntries);
    }

    // ============================================================
    // 公开接口 — 完整模式
    // ============================================================

    /// <summary>显示结算面板（完整模式，带BattleOutcome数据）</summary>
    public void ShowResult(bool victory, int xp, int gold, List<LootEntry>? loot = null,
        List<UnitStatusEntry>? unitStatuses = null, string? battleDescription = null)
    {
        BuildUI();

        // 设置标题
        _titleLabel.Text = victory ? "⚔ 战斗胜利!" : "✘ 战斗失败";
        _titleLabel.AddThemeColorOverride("font_color",
            victory ? Theme.TextPositive : Theme.TextNegative);

        // 设置描述
        if (!string.IsNullOrEmpty(battleDescription))
        {
            _descLabel.Text = $"[i]{battleDescription}[/i]";
            _descLabel.Visible = true;
        }

        // 奖励区（仅胜利时显示）
        if (victory)
        {
            BuildRewardsSection(xp, gold);

            if (loot != null && loot.Count > 0)
                BuildLootSection(loot);
        }
        else
        {
            BuildDefeatSection();
        }

        // 队员状态（如果提供了数据）
        if (unitStatuses != null && unitStatuses.Count > 0)
            BuildUnitStatusSection(unitStatuses);

        // 启动动画
        StartAnimation();
    }

    /// <summary>使用BattleOutcome显示结算面板</summary>
    public void ShowFromOutcome(BattleOutcome outcome, List<UnitStatusEntry>? unitStatuses = null)
    {
        ShowResult(
            outcome.AttackerWon,
            outcome.XpGranted,
            outcome.GoldGranted,
            outcome.LootEntries,
            unitStatuses,
            outcome.BattleDescription
        );
    }

    // ============================================================
    // UI构建
    // ============================================================

    private void BuildUI()
    {
        // 全屏半透明遮罩
        var overlay = new ColorRect();
        overlay.Color = new Color(0, 0, 0, 0.7f);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(overlay);

        // 居中容器 — 提供完美且动态自适应的居中布局
        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.AddChild(centerContainer);

        // 居中面板
        _mainPanel = new PanelContainer();
        _mainPanel.CustomMinimumSize = new Vector2(520, 400);
        _mainPanel.AddThemeStyleboxOverride("panel",
            Theme.MakePanelStyle(Theme.BgPrimary, Theme.BorderHighlight, 2, Theme.RadiusLg, Theme.SpacingXl));
        centerContainer.AddChild(_mainPanel);

        // 滚动容器（内容可能很长）
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        _mainPanel.AddChild(scroll);

        _contentVbox = new VBoxContainer();
        _contentVbox.AddThemeConstantOverride("separation", Theme.SpacingLg);
        _contentVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_contentVbox);

        // 标题
        _titleLabel = new Label();
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", 32);
        _contentVbox.AddChild(_titleLabel);

        // 描述（初始隐藏）
        _descLabel = new RichTextLabel();
        _descLabel.BbcodeEnabled = true;
        _descLabel.ScrollActive = false;
        _descLabel.FitContent = true;
        _descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _descLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _descLabel.AddThemeFontSizeOverride("normal_font_size", Theme.FontSizeMd);
        _descLabel.AddThemeColorOverride("default_color", Theme.TextSecondary);
        _descLabel.Visible = false;
        _contentVbox.AddChild(_descLabel);

        // 分隔线
        _contentVbox.AddChild(_factory.CreateSeparatorH(Theme.BorderHighlight));

        // 各区块占位（动态添加）
        _rewardsSection = new VBoxContainer();
        _rewardsSection.AddThemeConstantOverride("separation", Theme.SpacingMd);
        _contentVbox.AddChild(_rewardsSection);

        _lootSection = new VBoxContainer();
        _lootSection.AddThemeConstantOverride("separation", Theme.SpacingSm);
        _contentVbox.AddChild(_lootSection);

        _unitStatusSection = new VBoxContainer();
        _unitStatusSection.AddThemeConstantOverride("separation", Theme.SpacingMd);
        _contentVbox.AddChild(_unitStatusSection);

        // 弹性占位
        var spacer = new Control();
        spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _contentVbox.AddChild(spacer);

        // 继续按钮
        _continueBtn = new Button();
        _continueBtn.Text = "继续";
        _continueBtn.CustomMinimumSize = new Vector2(160, 48);
        _continueBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        Theme.ApplyButtonTheme(_continueBtn, Theme.MakeButtonStyle(
            new Color(0.2f, 0.3f, 0.2f), new Color(0.3f, 0.45f, 0.3f)));
        _continueBtn.Pressed += OnContinueClicked;
        _contentVbox.AddChild(_continueBtn);

        // 初始隐藏所有区块（动画用）
        _rewardsSection.Visible = false;
        _lootSection.Visible = false;
        _unitStatusSection.Visible = false;
        _continueBtn.Visible = false;
    }

    // ============================================================
    // 奖励区
    // ============================================================

    private void BuildRewardsSection(int xp, int gold)
    {
        var title = _factory.CreateTitleLabel("战利品", Theme.FontSizeLg);
        _rewardsSection.AddChild(title);

        var grid = new GridContainer();
        grid.Columns = 2;
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 8);
        _rewardsSection.AddChild(grid);

        AddRewardRow(grid, "经验值", $"+{xp} XP", Theme.XpFill);
        AddRewardRow(grid, "金币", $"+{gold} 金", Theme.TextAccent);
    }

    private void BuildDefeatSection()
    {
        var msg = new Label();
        msg.Text = "队伍被击败，被迫撤退...";
        msg.HorizontalAlignment = HorizontalAlignment.Center;
        msg.AddThemeFontSizeOverride("font_size", Theme.FontSizeLg);
        msg.AddThemeColorOverride("font_color", Theme.TextSecondary);
        _rewardsSection.AddChild(msg);
    }

    // ============================================================
    // 战利品区
    // ============================================================

    private void BuildLootSection(List<LootEntry> loot)
    {
        // 分隔线
        _lootSection.AddChild(_factory.CreateSeparatorH(Theme.BorderDefault));

        var title = _factory.CreateTitleLabel("掉落物品", Theme.FontSizeLg);
        _lootSection.AddChild(title);

        foreach (var entry in loot)
        {
            var row = CreateLootRow(entry);
            _lootSection.AddChild(row);
        }
    }

    private Control CreateLootRow(LootEntry entry)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", Theme.SpacingMd);

        // 类型图标（文字占位）
        var icon = new Label();
        icon.Text = GetLootTypeIcon(entry.Type);
        icon.AddThemeFontSizeOverride("font_size", Theme.FontSizeLg);
        icon.CustomMinimumSize = new Vector2(28, 0);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        hbox.AddChild(icon);

        // 物品名
        var nameLabel = new Label();
        nameLabel.Text = entry.ItemName;
        nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nameLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
        nameLabel.AddThemeColorOverride("font_color", Theme.TextPrimary);
        hbox.AddChild(nameLabel);

        // 数量（如果>1）
        if (entry.Quantity > 1)
        {
            var qtyLabel = new Label();
            qtyLabel.Text = $"×{entry.Quantity}";
            qtyLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
            qtyLabel.AddThemeColorOverride("font_color", Theme.TextSecondary);
            hbox.AddChild(qtyLabel);
        }

        // 价值
        if (entry.Value > 0)
        {
            var valueLabel = new Label();
            valueLabel.Text = $"{entry.Value}金";
            valueLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
            valueLabel.AddThemeColorOverride("font_color", Theme.TextAccent);
            hbox.AddChild(valueLabel);
        }

        return hbox;
    }

    private static string GetLootTypeIcon(LootEntry.LootType type)
    {
        return type switch
        {
            LootEntry.LootType.Weapon => "⚔",
            LootEntry.LootType.Armor => "🛡",
            LootEntry.LootType.Shield => "🛡",
            LootEntry.LootType.Helmet => "⛑",
            LootEntry.LootType.Consumable => "🧪",
            LootEntry.LootType.Gold => "💰",
            LootEntry.LootType.Material => "📦",
            _ => "•",
        };
    }

    // ============================================================
    // 队员状态区
    // ============================================================

    private void BuildUnitStatusSection(List<UnitStatusEntry> statuses)
    {
        // 分隔线
        _unitStatusSection.AddChild(_factory.CreateSeparatorH(Theme.BorderDefault));

        var title = _factory.CreateTitleLabel("队员状态", Theme.FontSizeLg);
        _unitStatusSection.AddChild(title);

        // 网格布局（每行2个）
        var grid = new GridContainer();
        grid.Columns = 2;
        grid.AddThemeConstantOverride("h_separation", Theme.SpacingMd);
        grid.AddThemeConstantOverride("v_separation", Theme.SpacingMd);
        _unitStatusSection.AddChild(grid);

        foreach (var status in statuses)
        {
            var card = CreateUnitStatusCard(status);
            grid.AddChild(card);
        }
    }

    private Control CreateUnitStatusCard(UnitStatusEntry status)
    {
        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(220, 80);
        card.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
            Theme.BgCard, GetStatusBorderColor(status.Status), 1, Theme.RadiusMd, Theme.SpacingMd));

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", Theme.SpacingMd);
        card.AddChild(hbox);

        // 左侧：状态图标
        var statusIcon = new Label();
        statusIcon.Text = GetStatusIcon(status.Status);
        statusIcon.AddThemeFontSizeOverride("font_size", 24);
        statusIcon.CustomMinimumSize = new Vector2(32, 0);
        statusIcon.HorizontalAlignment = HorizontalAlignment.Center;
        statusIcon.VerticalAlignment = VerticalAlignment.Center;
        hbox.AddChild(statusIcon);

        // 右侧：信息
        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        infoVbox.AddThemeConstantOverride("separation", Theme.SpacingXs);
        hbox.AddChild(infoVbox);

        // 名字 + 等级
        var nameLabel = new Label();
        nameLabel.Text = status.UnitName;
        nameLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
        nameLabel.AddThemeColorOverride("font_color", Theme.TextPrimary);
        infoVbox.AddChild(nameLabel);

        var levelLabel = new Label();
        levelLabel.Text = $"Lv.{status.Level}";
        levelLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
        levelLabel.AddThemeColorOverride("font_color", Theme.TextSecondary);
        infoVbox.AddChild(levelLabel);

        // HP条（如果存活）
        if (status.Status != UnitStatus.Dead)
        {
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, Theme.BarHeightSm);
            hpBar.ShowPercentage = false;
            float hpRatio = status.MaxHp > 0 ? (float)status.CurrentHp / status.MaxHp : 0;
            hpBar.Value = hpRatio * 100;
            Theme.ApplyBarTheme(hpBar, Theme.GetHpColor(hpRatio), Theme.HpBarBg);
            infoVbox.AddChild(hpBar);

            var hpText = new Label();
            hpText.Text = $"{status.CurrentHp}/{status.MaxHp}";
            hpText.AddThemeFontSizeOverride("font_size", Theme.FontSizeXs);
            hpText.AddThemeColorOverride("font_color", Theme.TextMuted);
            infoVbox.AddChild(hpText);
        }

        // 升级标记
        if (status.LeveledUp)
        {
            var levelUpLabel = new Label();
            levelUpLabel.Text = "⬆ 升级!";
            levelUpLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
            levelUpLabel.AddThemeColorOverride("font_color", Theme.TextAccent);
            infoVbox.AddChild(levelUpLabel);
        }

        return card;
    }

    private Color GetStatusBorderColor(UnitStatus status)
    {
        return status switch
        {
            UnitStatus.Alive => Theme.BorderFriendly,
            UnitStatus.Wounded => Theme.TextWarning,
            UnitStatus.Dead => Theme.BorderEnemy,
            _ => Theme.BorderDefault,
        };
    }

    private static string GetStatusIcon(UnitStatus status)
    {
        return status switch
        {
            UnitStatus.Alive => "✓",
            UnitStatus.Wounded => "⚠",
            UnitStatus.Dead => "✝",
            _ => "?",
        };
    }

    // ============================================================
    // 辅助
    // ============================================================

    private void AddRewardRow(GridContainer grid, string label, string value, Color valueColor)
    {
        var lbl = _factory.CreateBodyLabel(label, Theme.TextSecondary);
        grid.AddChild(lbl);

        var val = new Label();
        val.Text = value;
        val.AddThemeFontSizeOverride("font_size", Theme.FontSizeLg);
        val.AddThemeColorOverride("font_color", valueColor);
        grid.AddChild(val);
    }

    private void StartAnimation()
    {
        _animQueue.Clear();
        _animQueue.Add(_rewardsSection);
        _animQueue.Add(_lootSection);
        _animQueue.Add(_unitStatusSection);
        _animQueue.Add(_continueBtn);
        _animIndex = 0;
        _animTimer = 0f;
    }

    private void OnContinueClicked()
    {
        Globals.AudioOrNull?.PlaySfxName("ui_click");
        EmitSignal(SignalName.ContinueClicked);
        QueueFree();
    }
}

// ============================================================
// 数据结构
// ============================================================

/// <summary>队员状态枚举</summary>
public enum UnitStatus
{
    Alive,   // 存活
    Wounded, // 重伤（战役模式）
    Dead,    // 阵亡
}

/// <summary>队员状态条目</summary>
public class UnitStatusEntry
{
    public string UnitName { get; set; } = "";
    public int Level { get; set; } = 1;
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public UnitStatus Status { get; set; } = UnitStatus.Alive;
    public bool LeveledUp { get; set; }

    public UnitStatusEntry() { }

    public UnitStatusEntry(UnitData unit, UnitStatus status, bool leveledUp = false)
    {
        UnitName = unit.UnitName ?? "未知";
        Level = unit.Level;
        MaxHp = unit.BaseMaxHp;
        CurrentHp = PartyRoster.GetCurrentHp(unit);
        Status = status;
        LeveledUp = leveledUp;
    }
}
