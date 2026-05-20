// TurnOrderBar.cs
// 回合顺序显示栏 — 显示当前回合所有单位的行动顺序
// 对应策划案 09-UI设计.md → 回合信息栏（当前回合数/阶段指示）
// 对应策划案 03-战术战斗系统 → 先攻系统
using Godot;
using System.Collections.Generic;
using BladeHex.UI;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 回合顺序显示栏 — 显示当前回合数、阶段指示及所有单位的行动顺序图标
/// </summary>
[GlobalClass]
public partial class TurnOrderBar : HBoxContainer
{
    // ============================================================================
    // 信号
    // ============================================================================
    [Signal] public delegate void UnitClickedEventHandler(Unit unit);

    // ============================================================================
    // 内部控件
    // ============================================================================
    private readonly List<Control> _unitIcons = new();
    private int _activeIndex = -1;
    private UIFactory _factory = null!;
    private HBoxContainer _iconContainer = null!;

    // ============================================================================
    // 主题 (UITheme 单例)
    // ============================================================================
    private UITheme _theme => UITheme.Instance!;

    // ============================================================================
    // _Ready
    // ============================================================================
    public override void _Ready()
    {
        _factory = new UIFactory();
        _Setup();
    }

    // ============================================================================
    // 初始化
    // ============================================================================
    private void _Setup()
    {
        // 确保顶栏有可见背景
        var panelBg = new PanelContainer();

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.06f, 0.06f, 0.10f, 0.9f);
        style.SetBorderWidthAll(1);
        style.BorderColor = new Color(0.35f, 0.30f, 0.22f);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(6);
        panelBg.AddThemeStyleboxOverride("panel", style);
        panelBg.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panelBg.CustomMinimumSize = new Vector2(580, 0);
        AddChild(panelBg);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);
        panelBg.AddChild(hbox);

        // 滚动区域显示单位图标（不再显示回合文字，已移至顶部）
        var scroll = _factory.CreateScrollContainer(true);
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(scroll);

        _iconContainer = new HBoxContainer();
        _iconContainer.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_iconContainer);
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>设置回合数（已移至顶部，此处保留兼容）</summary>
    public void SetTurnNumber(int turn)
    {
    }

    /// <summary>设置回合阶段文字（已移至顶部，此处保留兼容）</summary>
    public void SetPhaseText(string text, Color? color = null)
    {
    }

    /// <summary>设置单位顺序列表</summary>
    public void SetUnitOrder(List<Unit> units, Unit? activeUnit = null)
    {
        if (_iconContainer == null)
            return;

        // 清除旧图标
        foreach (Node child in _iconContainer.GetChildren())
        {
            child.QueueFree();
        }
        _unitIcons.Clear();

        // 重排列表：当前行动单位放最左侧，后续按先攻顺序排列
        var reordered = new List<Unit>();
        if (activeUnit != null && units.Contains(activeUnit))
        {
            int activeIdx = units.IndexOf(activeUnit);
            // 从 active 开始循环排列
            for (int i = 0; i < units.Count; i++)
            {
                int idx = (activeIdx + i) % units.Count;
                reordered.Add(units[idx]);
            }
        }
        else
        {
            reordered.AddRange(units);
        }

        foreach (var unit in reordered)
        {
            if (unit == null || !GodotObject.IsInstanceValid(unit))
                continue;
            if (unit.Data == null)
                continue;

            var icon = _CreateUnitIcon(unit, unit == activeUnit);
            _iconContainer.AddChild(icon);
            _unitIcons.Add(icon);
        }

        // 滚动到最左侧（确保当前行动单位可见）
        ScrollToStart();
    }

    /// <summary>滚动到列表最左侧</summary>
    private void ScrollToStart()
    {
        // 找到父 ScrollContainer 并重置滚动位置
        if (_iconContainer?.GetParent() is ScrollContainer scroll)
            scroll.ScrollHorizontal = 0;
    }

    /// <summary>高亮当前行动单位</summary>
    public void SetActiveUnit(Unit unit)
    {
        if (_iconContainer == null)
            return;

        foreach (Node child in _iconContainer.GetChildren())
        {
            if (child is not Control icon)
                continue;

            bool isActive = false;
            if (GodotObject.IsInstanceValid(unit) && icon.HasMeta("unit_ref"))
            {
                var refUnit = icon.GetMeta("unit_ref").As<Unit>();
                isActive = refUnit == unit;
            }
            _UpdateIconStyle(icon, isActive);
        }
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    /// <summary>创建单个单位图标条目</summary>
    private PanelContainer _CreateUnitIcon(Unit unit, bool isActive)
    {
        var icon = new PanelContainer();
        icon.CustomMinimumSize = new Vector2(48, 48);
        icon.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        icon.SetMeta("unit_ref", unit);

        StyleBoxFlat style;
        if (isActive)
        {
            style = _theme.MakePanelStyle(
                new Color(0.3f, 0.28f, 0.1f, 0.9f), _theme.BorderHighlight, 2, _theme.RadiusSm, 2);
        }
        else
        {
            bool isEnemy = unit.Data?.IsEnemy ?? false;
            var bg = isEnemy
                ? new Color(0.2f, 0.08f, 0.08f, 0.7f)
                : new Color(0.08f, 0.12f, 0.2f, 0.7f);
            var border = isEnemy ? _theme.BorderEnemy : _theme.BorderFriendly;
            style = _theme.MakePanelStyle(bg, border, 1, _theme.RadiusSm, 2);
        }
        icon.AddThemeStyleboxOverride("panel", style);

        // 缩略信息
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        icon.AddChild(vbox);

        // 名称缩写
        var nameLbl = new Label();
        nameLbl.Text = unit.Data != null ? unit.Data.UnitName.Left(2) : "??";
        nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
        nameLbl.AddThemeFontSizeOverride("font_size", _theme.FontSizeXs);
        nameLbl.AddThemeColorOverride("font_color", _theme.TextPrimary);
        vbox.AddChild(nameLbl);

        // HP比例条
        var hpBar = new ProgressBar();
        hpBar.CustomMinimumSize = new Vector2(30, 3);
        hpBar.ShowPercentage = false;
        hpBar.MinValue = 0;

        if (GodotObject.IsInstanceValid(unit))
        {
            hpBar.MaxValue = unit.GetMaxHp();
            hpBar.Value = unit.CurrentHp;
            float ratio = (float)unit.CurrentHp / Mathf.Max(unit.GetMaxHp(), 1);
            _theme.ApplyBarTheme(hpBar, _theme.GetHpColor(ratio), _theme.HpBarBg);
        }
        else
        {
            hpBar.MaxValue = 1;
            hpBar.Value = 0;
            _theme.ApplyBarTheme(hpBar, _theme.HpBarBg, _theme.HpBarBg);
        }
        vbox.AddChild(hpBar);

        // 点击事件
        icon.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton btn && btn.Pressed && btn.ButtonIndex == MouseButton.Left)
            {
                EmitSignal(SignalName.UnitClicked, unit);
            }
        };

        return icon;
    }

    /// <summary>更新图标样式（高亮/普通）</summary>
    private void _UpdateIconStyle(Control icon, bool isActive)
    {
        var style = icon.GetThemeStylebox("panel") as StyleBoxFlat;
        if (style == null)
            return;

        if (isActive)
        {
            style.BgColor = new Color(0.3f, 0.28f, 0.1f, 0.9f);
            style.BorderColor = _theme.BorderHighlight;
            style.SetBorderWidthAll(2);
        }
        else
        {
            style.SetBorderWidthAll(1);
        }
    }
}
