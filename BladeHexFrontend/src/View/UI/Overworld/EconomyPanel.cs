// EconomyPanel.cs
// 财务账本与生存预测面板 — 大地图底部"财务账本"按钮呼出
// 展示四大支柱资源存量、每日收支明细与生存危机警告
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 财务账本与生存预测面板
/// 继承自 PanelContainer，由 OverworldUI 管理显示/隐藏
/// </summary>
[GlobalClass]
public partial class EconomyPanel : PanelContainer
{
    // ========================================
    // 颜色常量
    // ========================================
    private static readonly Color ColorAccent     = new(0.95f, 0.82f, 0.45f); // 金色标题
    private static readonly Color ColorPositive   = new(0.45f, 0.82f, 0.55f); // 绿色（充足）
    private static readonly Color ColorWarning    = new(0.95f, 0.65f, 0.25f); // 橙色（警示）
    private static readonly Color ColorNegative   = new(0.90f, 0.30f, 0.30f); // 红色（危险）
    private static readonly Color ColorMuted      = new(0.65f, 0.65f, 0.70f); // 灰色（次要）
    private static readonly Color ColorPrimary    = new(0.92f, 0.90f, 0.88f); // 主文字

    // ========================================
    // UI 引用
    // ========================================
    private Label _goldValue     = null!;
    private Label _foodValue     = null!;
    private Label _toolsValue    = null!;
    private Label _medicineValue = null!;

    private Label _dailyWageDetail  = null!;
    private VBoxContainer _wageList = null!;
    private Label _dailyFoodDetail  = null!;
    private Label _dailyToolsDetail = null!;
    private Label _dailyMedDetail   = null!;

    private VBoxContainer _warningBox = null!;

    // ========================================
    // 数据源（由外部注入）
    // ========================================
    private EconomyManager? _economy;
    public EconomyManager? Economy
    {
        get => _economy;
        set
        {
            if (_economy == value) return;

            if (_economy != null)
                _economy.ResourcesChanged -= OnEconomyResourcesChanged;

            _economy = value;

            if (_economy != null)
                _economy.ResourcesChanged += OnEconomyResourcesChanged;
        }
    }

    // ========================================
    // 构建
    // ========================================

    public override void _Ready()
    {
        _BuildPanel();
    }

    public override void _ExitTree()
    {
        if (_economy != null)
            _economy.ResourcesChanged -= OnEconomyResourcesChanged;
    }

    private void _BuildPanel()
    {
        // 面板样式 — 半透明磨砂黑底 + 金色边框
        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.04f, 0.04f, 0.06f, 0.92f);
        bgStyle.SetBorderWidthAll(2);
        bgStyle.BorderColor = new Color(0.75f, 0.62f, 0.30f);
        bgStyle.SetCornerRadiusAll(6);
        bgStyle.SetContentMarginAll(20);
        AddThemeStyleboxOverride("panel", bgStyle);

        // 定位：屏幕右下角，向左展开
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        OffsetLeft = -440;
        OffsetTop  = -520;
        OffsetRight = 0;
        OffsetBottom = -60;
        CustomMinimumSize = new Vector2(420, 0);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        AddChild(vbox);

        // 标题
        var title = new Label { Text = "兵团财务账本与生存预测" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", ColorAccent);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        vbox.AddChild(_MakeSeparator());

        // ──────────────────────────────────
        // 资产存量区块
        // ──────────────────────────────────
        var assetHeader = new Label { Text = "[ 资产存量 ]" };
        assetHeader.AddThemeColorOverride("font_color", ColorAccent);
        assetHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(assetHeader);

        var assetGrid = new VBoxContainer();
        assetGrid.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(assetGrid);

        _goldValue     = _MakeAssetRow(assetGrid, "兵团金库");
        _foodValue     = _MakeAssetRow(assetGrid, "战友口粮");
        _toolsValue    = _MakeAssetRow(assetGrid, "修整工具");
        _medicineValue = _MakeAssetRow(assetGrid, "医疗物资");

        vbox.AddChild(_MakeSeparator());

        // ──────────────────────────────────
        // 每日收支明细区块
        // ──────────────────────────────────
        var incomeHeader = new Label { Text = "[ 每日收支明细 ]" };
        incomeHeader.AddThemeColorOverride("font_color", ColorAccent);
        incomeHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(incomeHeader);

        var detailBox = new VBoxContainer();
        detailBox.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(detailBox);

        _dailyWageDetail = _MakeDetailLabel("每日军饷: ---");
        detailBox.AddChild(_dailyWageDetail);

        _wageList = new VBoxContainer();
        _wageList.AddThemeConstantOverride("separation", 2);
        detailBox.AddChild(_wageList);

        _dailyFoodDetail  = _MakeDetailLabel("口粮每日消耗: ---");
        _dailyToolsDetail = _MakeDetailLabel("工具每日消耗: ---");
        _dailyMedDetail   = _MakeDetailLabel("药品每日消耗: ---");
        detailBox.AddChild(_dailyFoodDetail);
        detailBox.AddChild(_dailyToolsDetail);
        detailBox.AddChild(_dailyMedDetail);

        vbox.AddChild(_MakeSeparator());

        // ──────────────────────────────────
        // 生存危机警告区块
        // ──────────────────────────────────
        var warnHeader = new Label { Text = "[ 生存危机警告 ]" };
        warnHeader.AddThemeColorOverride("font_color", ColorAccent);
        warnHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(warnHeader);

        _warningBox = new VBoxContainer();
        _warningBox.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(_warningBox);

        var noWarning = new Label { Text = "兵团运营良好，暂无危机。", Name = "NoWarningLabel" };
        noWarning.AddThemeColorOverride("font_color", ColorPositive);
        noWarning.AddThemeFontSizeOverride("font_size", 13);
        _warningBox.AddChild(noWarning);

        // 关闭按钮
        vbox.AddChild(_MakeSeparator());
        var closeBtn = new Button { Text = "关闭" };
        closeBtn.CustomMinimumSize = new Vector2(100, 32);
        closeBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        closeBtn.Pressed += () => { Visible = false; };
        vbox.AddChild(closeBtn);
    }

    // ========================================
    // 刷新数据
    // ========================================

    private void OnEconomyResourcesChanged()
    {
        if (Visible && IsInsideTree())
            Refresh();
    }

    public void Refresh()
    {
        if (Economy == null) return;

        int gold        = Economy.Gold;
        float food      = Economy.Food;
        float tools     = Economy.Tools;
        float medicine  = Economy.Medicine;
        float foodMax   = Economy.MaxFood;
        float toolsMax  = Economy.MaxTools;
        float medMax    = Economy.MaxMedicine;

        // 预计天数
        int daysGold = Economy.GetDaysUntilBroke();
        int daysFood = Economy.GetDaysUntilStarving();
        int daysTool = Economy.GetDaysUntilToolsDepleted();
        int daysMed  = Economy.GetDaysUntilMedicineDepleted();

        // 更新资产存量
        _goldValue.Text     = $"{gold} 金币";
        _foodValue.Text     = $"{food:F1} / {foodMax:F0} 单位  (预计可支撑 {daysFood} 天)";
        _toolsValue.Text    = $"{tools:F1} / {toolsMax:F0} 单位  (预计可支撑 {daysTool} 天)";
        _medicineValue.Text = $"{medicine:F1} / {medMax:F0} 单位  (预计可支撑 {daysMed} 天)";

        _SetResourceColor(_goldValue, daysGold);
        _SetResourceColor(_foodValue, daysFood);
        _SetResourceColor(_toolsValue, daysTool);
        _SetResourceColor(_medicineValue, daysMed);

        // 每日收支明细
        int totalWage = Economy.GetDailyWageTotal();
        float dailyFood  = Economy.GetDailyFoodConsumption();
        float dailyTools = Economy.ActiveRoster != null ? Economy.ActiveRoster.Count * 0.1f : 0;
        float dailyMed   = Economy.ActiveRoster != null ? Economy.ActiveRoster.Count * 0.2f : 0;

        _dailyWageDetail.Text  = $"每日军饷动态预算: -{totalWage} 金币  (队长不计军饷)";
        _dailyFoodDetail.Text  = $"口粮每日消耗: -{dailyFood:F1} 单位  (共 {Economy.ActiveRoster?.Count ?? 0} 人)";
        _dailyToolsDetail.Text = $"工具保养消耗: -{dailyTools:F1} 单位";
        _dailyMedDetail.Text   = $"药品医疗消耗: -{dailyMed:F1} 单位";

        // 军饷明细列表
        _RefreshWageList();

        // 危机警告
        _RefreshWarnings(daysGold, daysFood, daysTool, daysMed);
    }

    private void _RefreshWageList()
    {
        // 清空旧列表
        foreach (var child in _wageList.GetChildren())
            child.QueueFree();

        if (Economy?.ActiveRoster == null) return;

        foreach (var member in Economy.ActiveRoster.Members)
        {
            if (Economy.ActiveRoster.IsLeader(member)) continue;
            int wage = Economy.WageSys.GetDailyWage(member);
            var row = new Label();
            row.Text = $"    {member.UnitName} (Lv.{member.Level})  : -{wage} 金币";
            row.AddThemeFontSizeOverride("font_size", 12);
            row.AddThemeColorOverride("font_color", ColorMuted);
            _wageList.AddChild(row);
        }
    }

    private void _RefreshWarnings(int daysGold, int daysFood, int daysTool, int daysMed)
    {
        // 清空旧警告
        foreach (var child in _warningBox.GetChildren())
            child.QueueFree();

        var warnings = new List<(string text, bool isCritical)>();

        int unpaidDays  = Economy?.WageSys.ConsecutiveUnpaidDays ?? 0;
        int starveDays  = Economy?.FoodSys.ConsecutiveStarveDays  ?? 0;

        if (unpaidDays > 0)
            warnings.Add(($"兵团由于金库空虚，已连续欠饷 {unpaidDays} 天！成员士气持续低落。", unpaidDays >= 3));
        else if (daysGold <= 3)
            warnings.Add(($"金库即将耗尽！预计仅剩 {daysGold} 天军饷可用。", true));

        if (starveDays > 0)
            warnings.Add(($"队伍已连续断粮 {starveDays} 天！每日 HP 恢复已阻断，士气暴跌。", true));
        else if (daysFood <= 3)
            warnings.Add(($"口粮即将耗尽！预计仅剩 {daysFood} 天的粮食储备。", true));

        if (daysTool <= 2)
            warnings.Add(("修整工具匮乏！装备生锈风险增加，战斗时护甲防御值将降低。", daysTool == 0));

        if (Economy?.Medicine <= 0)
            warnings.Add(("医疗物资已耗尽！伤员将停止自愈，重伤佣兵恢复时间倍增。", true));
        else if (daysMed <= 2)
            warnings.Add(($"医疗物资严重不足，预计仅剩 {daysMed} 天。", false));

        if (warnings.Count == 0)
        {
            var ok = new Label { Text = "兵团运营良好，暂无危机。" };
            ok.AddThemeColorOverride("font_color", ColorPositive);
            ok.AddThemeFontSizeOverride("font_size", 13);
            _warningBox.AddChild(ok);
        }
        else
        {
            foreach (var (text, critical) in warnings)
            {
                var lbl = new Label { Text = text };
                lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                lbl.AddThemeColorOverride("font_color", critical ? ColorNegative : ColorWarning);
                lbl.AddThemeFontSizeOverride("font_size", 13);
                _warningBox.AddChild(lbl);
            }
        }
    }

    // ========================================
    // 工具方法
    // ========================================

    private Label _MakeAssetRow(VBoxContainer parent, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);

        var nameLabel = new Label { Text = $"  {label}:" };
        nameLabel.CustomMinimumSize = new Vector2(120, 0);
        nameLabel.AddThemeColorOverride("font_color", ColorMuted);
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(nameLabel);

        var valueLabel = new Label { Text = "---" };
        valueLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        valueLabel.AddThemeColorOverride("font_color", ColorPrimary);
        valueLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(valueLabel);

        return valueLabel;
    }

    private static Label _MakeDetailLabel(string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeColorOverride("font_color", ColorMuted);
        lbl.AddThemeFontSizeOverride("font_size", 13);
        return lbl;
    }

    private static HSeparator _MakeSeparator()
    {
        var sep = new HSeparator();
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.75f, 0.62f, 0.30f, 0.3f);
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }

    private static void _SetResourceColor(Label lbl, int daysLeft)
    {
        if (daysLeft <= 0)
            lbl.AddThemeColorOverride("font_color", ColorNegative);
        else if (daysLeft <= 3)
            lbl.AddThemeColorOverride("font_color", ColorWarning);
        else
            lbl.AddThemeColorOverride("font_color", ColorPositive);
    }
}
