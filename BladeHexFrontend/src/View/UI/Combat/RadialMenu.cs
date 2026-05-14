// RadialMenu.cs
// 径向菜单 — 圆形排列的操作选择菜单
// 在单位位置弹出，提供防御、等待、取消等选项
using Godot;
using System.Collections.Generic;
using BladeHex.UI;

namespace BladeHex.UI.Combat;

/// <summary>
/// 径向菜单 — 圆形排列的操作选择菜单
/// 在单位位置弹出，提供防御、等待、取消等选项
/// </summary>
[GlobalClass]
public partial class RadialMenu : Control
{
    // ============================================================================
    // 信号
    // ============================================================================
    [Signal]
    public delegate void ActionSelectedEventHandler(string action);

    [Signal]
    public delegate void ActionHoveredEventHandler(string action);

    // ============================================================================
    // 配置
    // ============================================================================
    public float Radius { get; set; } = 70.0f;

    // ============================================================================
    // 内部状态
    // ============================================================================
    private readonly List<Button> _buttons = new();

    // ============================================================================
    // 主题引用 (private — 避免与 Control.Theme 冲突)
    // ============================================================================
    private UITheme ThemeRef => UITheme.Instance!;

    // ============================================================================
    // _Ready
    // ============================================================================
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        // 如果点击空白处，隐藏菜单
        GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
                Hide();
        };
    }

    // ============================================================================
    // 设置菜单项
    // ============================================================================
    /// <summary>
    /// 设置径向菜单选项。
    /// Dictionary 格式: {"显示标签": "动作标识", ...}
    /// </summary>
    public void Setup(Godot.Collections.Dictionary options)
    {
        // 清除旧按钮
        foreach (var b in _buttons)
            b.QueueFree();
        _buttons.Clear();

        int count = options.Count;
        if (count == 0)
            return;

        float angleStep = Mathf.Tau / count;
        float currentAngle = -Mathf.Pi / 2; // 从顶部开始

        foreach (var key in options.Keys)
        {
            string label = key.AsString();
            string actionName = options[key].AsString();

            var btn = new Button();
            btn.Text = label;
            btn.CustomMinimumSize = new Vector2(60, 40);

            // 美化按钮
            var style = new StyleBoxFlat();
            style.BgColor = ThemeRef.BgPrimary;
            style.SetBorderWidthAll(1);
            style.BorderColor = ThemeRef.BorderDefault;
            style.SetCornerRadiusAll(ThemeRef.RadiusRound);
            style.SetContentMarginAll(ThemeRef.SpacingSm);
            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeFontSizeOverride("font_size", ThemeRef.FontSizeSm);

            // 计算位置
            var pos = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * Radius;
            btn.Position = pos - btn.CustomMinimumSize / 2;

            btn.Pressed += () =>
            {
                var audio = GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager");
                audio?.PlaySfxName("ui_click");
                EmitSignal(SignalName.ActionSelected, actionName);
                Hide();
            };

            string capturedAction = actionName;
            btn.MouseEntered += () =>
            {
                EmitSignal(SignalName.ActionHovered, capturedAction);
            };

            AddChild(btn);
            _buttons.Add(btn);
            currentAngle += angleStep;
        }
    }

    // ============================================================================
    // _Draw
    // ============================================================================
    public override void _Draw()
    {
        // 绘制半透明轮盘底图
        DrawCircle(Vector2.Zero, Radius + 30, new Color(0.05f, 0.05f, 0.1f, 0.8f));
        DrawArc(Vector2.Zero, Radius + 30, 0, Mathf.Tau, 32, ThemeRef.BorderDefault, 2.0f, true);
    }

    // ============================================================================
    // 显示菜单
    // ============================================================================
    public void ShowMenu(Vector2 pos)
    {
        Position = pos;
        Show();
    }
}
