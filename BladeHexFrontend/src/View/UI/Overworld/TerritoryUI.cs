﻿// TerritoryUI.cs
// Territory management UI - Manage castles, taxes, construction, faction relations
// Corresponds to design doc 04-StrategicLayer.md - Territory management
// Corresponds to design doc 11-ArmySystem.md - Recruit & maintain
// Corresponds to design doc 05 Phase5 - Territory management
using Godot;
using Godot.Collections;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class TerritoryUI : PanelContainer
{
    // ============================================================================
    // 信号
    // ============================================================================
    [Signal]
    public delegate void CloseRequestedEventHandler();

    [Signal]
    public delegate void BuildClickedEventHandler(string buildingId);

    [Signal]
    public delegate void UpgradeCastleClickedEventHandler();

    [Signal]
    public delegate void TaxChangedEventHandler(float rate);

    // ============================================================================
    // Castle level (corresponds to design doc 04-StrategicLayer.md)
    // ============================================================================
    public enum CastleLevel
    {
        WoodFence,
        StoneFort,
        Citadel,
    }

    

    // ============================================================================
    // 主题常量
    // ============================================================================
    private static readonly Color BgPrimary = new(0.08f, 0.08f, 0.10f, 0.85f);
    private static readonly Color BgSecondary = new(0.12f, 0.12f, 0.14f, 0.80f);
    private static readonly Color BgTertiary = new(0.06f, 0.06f, 0.08f, 0.75f);
    private static readonly Color BgCard = new(0.15f, 0.14f, 0.18f, 0.75f);
    private static readonly Color BgCardHover = new(0.25f, 0.22f, 0.30f, 0.90f);
    private static readonly Color BorderDefault = new(0.3f, 0.3f, 0.35f, 0.6f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color BorderFriendly = new(0.2f, 0.5f, 0.8f, 0.8f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPositive = new(0.3f, 0.85f, 0.3f);
    private static readonly Color TextNegative = new(0.9f, 0.3f, 0.25f);

    private const int FontSizeXxl = 24;
    private const int FontSizeXl = 20;
    private const int FontSizeLg = 16;
    private const int FontSizeMd = 14;
    private const int FontSizeSm = 12;
    private const int FontSizeXs = 10;
    private const int SpacingMd = 8;
    private const int SpacingSm = 4;
    private const int SpacingLg = 12;
    private const int SpacingXs = 2;
    private const int SpacingXl = 16;
    private const int SpacingXxl = 24;
    private const int RadiusMd = 8;
    private const int RadiusSm = 4;
    private const int RadiusLg = 12;
    private const int ButtonHeight = 36;
    private const int ButtonHeightLg = 45;

    // ============================================================================
    // 城堡信息（对应策划案 04-战略层系统.md）
    // ============================================================================
    private static readonly Dictionary<CastleLevel, Dictionary> CastleInfo = new()
    {
        [CastleLevel.WoodFence] = new Dictionary { ["name"] = "\u6728\u6805\u6751\u5e84", ["defense"] = "\u6728\u5899\u3001\u62d2\u9a6c", ["garrison"] = 50, ["upgrade_cost"] = 500 },
        [CastleLevel.StoneFort] = new Dictionary { ["name"] = "\u77f3\u5821", ["defense"] = "\u77f3\u5899\u3001\u7bad\u5854", ["garrison"] = 150, ["upgrade_cost"] = 2000 },
        [CastleLevel.Citadel] = new Dictionary { ["name"] = "\u8981\u585e", ["defense"] = "\u9ad8\u5899\u3001\u53cc\u5854\u3001\u62a4\u57ce\u6cb3", ["garrison"] = 300, ["upgrade_cost"] = -1 },
    };

    // ============================================================================
    // 建筑类型
    // ============================================================================
    private static readonly Dictionary<string, Dictionary> Buildings = new()
    {
        ["training_ground"] = new Dictionary { ["name"] = "\u8bad\u7ec3\u573a", ["desc"] = "\u63d0\u5347\u65b0\u5175\u8d28\u91cf", ["cost"] = 300, ["level"] = 1 },
        ["magic_tower"] = new Dictionary { ["name"] = "\u6cd5\u5854", ["desc"] = "\u6cd5\u5e08\u62db\u52df\u548c\u6cd5\u672f\u7814\u7a76", ["cost"] = 800, ["level"] = 2 },
        ["trade_route"] = new Dictionary { ["name"] = "\u5546\u8def", ["desc"] = "\u589e\u52a0\u6536\u5165", ["cost"] = 400, ["level"] = 1 },
        ["iron_mine"] = new Dictionary { ["name"] = "\u94c1\u77ff", ["desc"] = "\u63d0\u4f9b\u94c1\u8d44\u6e90", ["cost"] = 350, ["level"] = 1 },
        ["gold_mine"] = new Dictionary { ["name"] = "\u91d1\u77ff", ["desc"] = "\u63d0\u4f9b\u91d1\u8d44\u6e90", ["cost"] = 600, ["level"] = 2 },
        ["barracks"] = new Dictionary { ["name"] = "\u5175\u8425", ["desc"] = "\u62db\u52df\u58eb\u5175", ["cost"] = 250, ["level"] = 1 },
        ["smithy"] = new Dictionary { ["name"] = "\u94c1\u5320\u94fa", ["desc"] = "\u6253\u9020\u88c5\u5907", ["cost"] = 200, ["level"] = 1 },
        ["tavern"] = new Dictionary { ["name"] = "\u9152\u9986", ["desc"] = "\u62db\u52df\u82f1\u96c4", ["cost"] = 150, ["level"] = 1 },
        ["wall_repair"] = new Dictionary { ["name"] = "\u57ce\u5899\u4fee\u590d", ["desc"] = "\u4fee\u590d\u6218\u6597\u635f\u6bc1", ["cost"] = 100, ["level"] = 1 },
    };

    // ============================================================================
    // 字段
    // ============================================================================
    private CastleLevel _castleLevel = CastleLevel.WoodFence;
    private Label _territoryNameLabel = null!;
    private RichTextLabel _castleInfoLabel = null!;
    private Button _upgradeBtn = null!;
    private Dictionary _incomeLabels = new();
    private Dictionary _expenseLabels = new();
    private GridContainer _buildingGrid = null!;
    private VBoxContainer _factionContainer = null!;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        _Setup();
        Visible = false;
    }

    // ============================================================================
    // UI 构建
    // ============================================================================

    private void _Setup()
    {
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddThemeStyleboxOverride("panel", MakePanelStyle(BgPrimary, BorderHighlight, 3, RadiusLg, 0));

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", 40);
        rootMargin.AddThemeConstantOverride("margin_right", 40);
        rootMargin.AddThemeConstantOverride("margin_top", 30);
        rootMargin.AddThemeConstantOverride("margin_bottom", 30);
        AddChild(rootMargin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", SpacingMd);
        rootMargin.AddChild(mainVbox);

        // —— 顶部 ——
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", SpacingMd);
        mainVbox.AddChild(header);

        _territoryNameLabel = CreateTitleLabel("\u9886\u5730\u7ba1\u7406", FontSizeXxl);
        _territoryNameLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        header.AddChild(_territoryNameLabel);

        var closeBtn = CreateButton("\u8fd4\u56de (ESC)", new Vector2(120, 36));
        closeBtn.Pressed += () => { Visible = false; EmitSignal(SignalName.CloseRequested); };
        header.AddChild(closeBtn);

        mainVbox.AddChild(CreateSeparator());

        // —— 主体：三栏 ——
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", SpacingLg);
        body.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        mainVbox.AddChild(body);

        // —— 左栏：城堡 收入/支出 ——
        var leftCol = new VBoxContainer();
        leftCol.CustomMinimumSize = new Vector2(280, 0);
        leftCol.AddThemeConstantOverride("separation", SpacingMd);
        body.AddChild(leftCol);

        var castleTitle = CreateTitleLabel("\u57ce\u5821", FontSizeLg);
        leftCol.AddChild(castleTitle);

        _castleInfoLabel = CreateRichText(new Vector2(0, 0));
        leftCol.AddChild(_castleInfoLabel);

        _upgradeBtn = CreateButton("\u5347\u7ea7\u57ce\u5821 (500\u91d1)", new Vector2(0, 36));
        _upgradeBtn.Pressed += () => EmitSignal(SignalName.UpgradeCastleClicked);
        leftCol.AddChild(_upgradeBtn);

        leftCol.AddChild(CreateSeparator());

        var incomeTitle = CreateTitleLabel("\u6536\u5165", FontSizeMd);
        leftCol.AddChild(incomeTitle);

        _CreateIncomeEntry(leftCol, "village_tax", "\u6751\u5e84\u7a0e\u6536", "+0\u91d1/\u5929");
        _CreateIncomeEntry(leftCol, "trade_route", "\u5546\u8def\u5173\u7a0e", "+0\u91d1/\u5929");
        _CreateIncomeEntry(leftCol, "mine_output", "\u77ff\u573a\u4ea7\u51fa", "+0\u91d1/\u5929");
        _CreateIncomeEntry(leftCol, "quest_income", "\u96c7\u4f63\u59d4\u6258", "+0\u91d1/\u5929");

        var totalIncome = CreateBodyLabel("\u603b\u6536\u5165: +0\u91d1/\u5929", TextPositive);
        totalIncome.SetMeta("stat_key", "total_income");
        leftCol.AddChild(totalIncome);
        _incomeLabels["total_income"] = totalIncome;

        leftCol.AddChild(CreateSeparator());

        var expenseTitle = CreateTitleLabel("\u652f\u51fa", FontSizeMd);
        leftCol.AddChild(expenseTitle);

        _CreateExpenseEntry(leftCol, "army_pay", "\u519b\u961f\u85aa\u4fe8", "-0\u91d1/\u5929");
        _CreateExpenseEntry(leftCol, "supply", "\u8865\u7ed9\u6d88\u8017", "-0\u91d1/\u5929");
        _CreateExpenseEntry(leftCol, "castle_maint", "\u57ce\u5821\u7ef4\u62a4", "-0\u91d1/\u5929");
        _CreateExpenseEntry(leftCol, "hero_salary", "\u82f1\u96c4\u85aa\u8d44", "-0\u91d1/\u5929");

        var totalExpense = CreateBodyLabel("\u603b\u652f\u51fa: -0\u91d1/\u5929", TextNegative);
        totalExpense.SetMeta("stat_key", "total_expense");
        leftCol.AddChild(totalExpense);
        _expenseLabels["total_expense"] = totalExpense;

        // —— 中栏：建设 ——
        var centerCol = new VBoxContainer();
        centerCol.CustomMinimumSize = new Vector2(300, 0);
        centerCol.AddThemeConstantOverride("separation", SpacingMd);
        body.AddChild(centerCol);

        var buildTitle = CreateTitleLabel("\u9886\u5730\u5efa\u8bbe", FontSizeLg);
        centerCol.AddChild(buildTitle);

        _buildingGrid = new GridContainer();
        _buildingGrid.Columns = 3;
        _buildingGrid.AddThemeConstantOverride("h_separation", SpacingMd);
        _buildingGrid.AddThemeConstantOverride("v_separation", SpacingMd);
        centerCol.AddChild(_buildingGrid);

        _PopulateBuildings();

        // —— 右栏：势力关系 ——
        var rightCol = new VBoxContainer();
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        rightCol.AddThemeConstantOverride("separation", SpacingMd);
        body.AddChild(rightCol);

        var factionTitle = CreateTitleLabel("\u52bf\u529b\u5173\u7cfb", FontSizeLg);
        rightCol.AddChild(factionTitle);

        _factionContainer = new VBoxContainer();
        _factionContainer.AddThemeConstantOverride("separation", SpacingSm);
        rightCol.AddChild(_factionContainer);

        _AddFactionEntry("\u4e2d\u592e\u738b\u56fd", "\u4e2d\u7acb", new Color(0.5f, 0.5f, 0.5f));
        _AddFactionEntry("\u94f6\u53f6\u7cbe\u7075", "\u53cb\u597d", new Color(0.0f, 1.0f, 0.0f));
        _AddFactionEntry("\u971c\u51a0\u77ee\u4eba", "\u51b7\u6de1", new Color(1.0f, 1.0f, 0.0f));
        _AddFactionEntry("\u6697\u5f71\u6559\u56e2", "\u654c\u5bf9", new Color(1.0f, 0.0f, 0.0f));
        _AddFactionEntry("\u7126\u571f\u517d\u4eba", "\u4ea4\u6218", new Color(1.0f, 0.0f, 0.0f));

        rightCol.AddChild(CreateSeparator());

        var phaseTitle = CreateTitleLabel("\u5f53\u524d\u9636\u6bb5", FontSizeMd);
        rightCol.AddChild(phaseTitle);

        var phaseInfo = CreateRichText(new Vector2(0, 0));
        phaseInfo.Text = "[color=yellow]\u96c7\u4f63\u5175\u56e2\u957f[/color]\n\n" +
                         "[color=gray]\u664b\u5347\u6761\u4ef6:[/color]\n" +
                         "\u2022 \u9a91\u58eb\uff1a\u58f0\u671b\u8fbe\u6807 + \u5b8c\u6210\u4e3b\u7ebf\u59d4\u6258\n" +
                         "\u2022 \u9886\u4e3b\uff1a\u5360\u9886\u57ce\u5821 + \u9ad8\u58f0\u671b\n\n" +
                         "[color=gray]\u5f53\u524d\u529f\u80fd:[/color]\n" +
                         "\u5c0f\u961fRPG\u6218\u6597 / \u9152\u9986\u62db\u52df / \u59d4\u6258";
        phaseInfo.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        rightCol.AddChild(phaseInfo);
    }

    // ============================================================================
    // 辅助创建
    // ============================================================================

    private void _CreateIncomeEntry(Control parent, string key, string name, string defaultValue)
    {
        var lbl = CreateBodyLabel($"{name}: {defaultValue}");
        parent.AddChild(lbl);
        _incomeLabels[key] = lbl;
    }

    private void _CreateExpenseEntry(Control parent, string key, string name, string defaultValue)
    {
        var lbl = CreateBodyLabel($"{name}: {defaultValue}");
        parent.AddChild(lbl);
        _expenseLabels[key] = lbl;
    }

    private void _PopulateBuildings()
    {
        foreach (var kvp in Buildings)
        {
            string buildId = kvp.Key;
            var buildInfo = kvp.Value;
            string bName = buildInfo["name"].AsString();
            int bCost = buildInfo["cost"].AsInt32();
            int bLevel = buildInfo["level"].AsInt32();
            string bDesc = buildInfo["desc"].AsString();

            var btn = CreateButton($"{bName}\n({bCost}\u91d1)", new Vector2(90, 64));
            btn.TooltipText = $"{bName}\n{bDesc}\n\u9700\u6c42: \u9886\u5730\u7b49\u7ea7{bLevel}";
            btn.SetMeta("build_id", buildId);

            string capturedId = buildId;
            btn.Pressed += () => EmitSignal(SignalName.BuildClicked, capturedId);
            _buildingGrid.AddChild(btn);
        }
    }

    private void _AddFactionEntry(string factionName, string relation, Color color)
    {
        var entry = new HBoxContainer();
        entry.AddThemeConstantOverride("separation", SpacingMd);
        _factionContainer.AddChild(entry);

        var nameL = CreateBodyLabel(factionName);
        nameL.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        entry.AddChild(nameL);

        var relL = CreateBodyLabel(relation, color);
        entry.AddChild(relL);
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>
    /// 打开领地管理
    /// </summary>
    public void OpenTerritory(string territoryName = "\u6211\u7684\u9886\u5730", CastleLevel castleLevel = CastleLevel.WoodFence)
    {
        _territoryNameLabel.Text = territoryName;
        _castleLevel = castleLevel;
        Visible = true;
        _UpdateCastleInfo();
    }

    /// <summary>
    /// 关闭
    /// </summary>
    public void CloseTerritory()
    {
        Visible = false;
    }

    private void _UpdateCastleInfo()
    {
        if (!CastleInfo.TryGetValue(_castleLevel, out var info))
            return;

        string name = info["name"].AsString();
        string defense = info["defense"].AsString();
        int garrison = info["garrison"].AsInt32();
        int upgradeCost = info["upgrade_cost"].AsInt32();

        _castleInfoLabel.Text = $"[b]{name}[/b]\n";
        _castleInfoLabel.Text += $"[color=gray]\u9632\u5fa1:[/color] {defense}\n";
        _castleInfoLabel.Text += $"[color=gray]\u5b88\u519b\u4e0a\u9650:[/color] {garrison}\u4eba\n";

        if (upgradeCost > 0)
        {
            _upgradeBtn.Text = $"\u5347\u7ea7\u57ce\u5821 ({upgradeCost}\u91d1)";
            _upgradeBtn.Disabled = false;
        }
        else
        {
            _upgradeBtn.Text = "\u5df2\u8fbe\u6700\u9ad8\u7b49\u7ea7";
            _upgradeBtn.Disabled = true;
        }
    }

    // ============================================================================
    // UI 组件工厂
    // ============================================================================

    private static StyleBoxFlat MakePanelStyle(Color bg, Color border, int borderWidth = 1, int radius = 8, int margin = 8)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.SetBorderWidthAll(borderWidth);
        s.BorderColor = border;
        s.SetCornerRadiusAll(radius);
        s.SetContentMarginAll(margin);
        return s;
    }

    private static Button CreateButton(string text, Vector2 minSize)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = minSize;

        var normal = new StyleBoxFlat { BgColor = new Color(0.18f, 0.17f, 0.22f) };
        normal.SetBorderWidthAll(1);
        normal.BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
        normal.SetCornerRadiusAll(8);
        normal.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = new Color(0.28f, 0.26f, 0.34f) };
        hover.SetBorderWidthAll(1);
        hover.BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f);
        hover.SetCornerRadiusAll(8);
        hover.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("hover", hover);

        var pressed = new StyleBoxFlat { BgColor = new Color(0.12f, 0.11f, 0.15f) };
        pressed.SetBorderWidthAll(1);
        pressed.BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f);
        pressed.SetCornerRadiusAll(8);
        pressed.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.88f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.9f, 0.8f, 0.5f));
        btn.AddThemeColorOverride("font_pressed_color", TextSecondary);

        return btn;
    }

    private static Label CreateTitleLabel(string text, int fontSize = FontSizeXl)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", TextAccent);
        return lbl;
    }

    private static Label CreateBodyLabel(string text, Color? color = null)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", FontSizeMd);
        lbl.AddThemeColorOverride("font_color", color ?? TextPrimary);
        return lbl;
    }

    private static RichTextLabel CreateRichText(Vector2 minSize)
    {
        var rt = new RichTextLabel();
        rt.CustomMinimumSize = minSize;
        rt.BbcodeEnabled = true;
        rt.ScrollActive = false;
        rt.FitContent = true;
        rt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return rt;
    }

    private static HSeparator CreateSeparator()
    {
        var sep = new HSeparator();
        var style = new StyleBoxFlat { BgColor = BorderDefault };
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }
}
