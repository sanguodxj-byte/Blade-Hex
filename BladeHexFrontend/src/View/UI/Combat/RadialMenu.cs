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
    /// ≤ 5 个选项:圆形排列(径向菜单)
    /// > 5 个选项:垂直列表排列(避免重叠)
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

        if (count <= 5)
            SetupRadial(options, count);
        else
            SetupList(options, count);
    }

    private void SetupRadial(Godot.Collections.Dictionary options, int count)
    {
        float angleStep = Mathf.Tau / count;
        float currentAngle = -Mathf.Pi / 2; // 从顶部开始

        foreach (var key in options.Keys)
        {
            string label = key.AsString();
            string actionName = options[key].AsString();

            var btn = CreateMenuButton(label, actionName);

            // 计算位置(圆形排列)
            var pos = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * Radius;
            btn.Position = pos - btn.CustomMinimumSize / 2;

            AddChild(btn);
            _buttons.Add(btn);
            currentAngle += angleStep;
        }
    }

    private void SetupList(Godot.Collections.Dictionary options, int count)
    {
        // 垂直列表:从中心上方开始向下排列,居中对齐
        float btnHeight = 36f;
        float btnWidth = 180f;
        float gap = 4f;
        float totalHeight = count * btnHeight + (count - 1) * gap;
        float startY = -totalHeight / 2f;

        int idx = 0;
        foreach (var key in options.Keys)
        {
            string label = key.AsString();
            string actionName = options[key].AsString();

            var btn = CreateMenuButton(label, actionName);
            btn.CustomMinimumSize = new Vector2(btnWidth, btnHeight);

            // 垂直排列,水平居中
            float y = startY + idx * (btnHeight + gap);
            btn.Position = new Vector2(-btnWidth / 2f, y);

            AddChild(btn);
            _buttons.Add(btn);
            idx++;
        }
    }

    private Button CreateMenuButton(string label, string actionName)
    {
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

        btn.Pressed += () =>
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_click");
            EmitSignal(SignalName.ActionSelected, actionName);
            Hide();
        };

        string capturedAction = actionName;
        btn.MouseEntered += () =>
        {
            EmitSignal(SignalName.ActionHovered, capturedAction);
        };

        return btn;
    }

    // ============================================================================
    // _Draw
    // ============================================================================
    public override void _Draw()
    {
        if (_buttons.Count <= 5)
        {
            // 径向模式:圆形底图
            float bgRadius = Radius + 30;
            if (ThemeRef.CombatRadialMenuBg != null)
            {
                var rect = new Rect2(new Vector2(-bgRadius, -bgRadius), new Vector2(bgRadius * 2.0f, bgRadius * 2.0f));
                DrawTextureRect(ThemeRef.CombatRadialMenuBg, rect, false, new Color(1.0f, 1.0f, 1.0f, 0.78f));
            }
            else
            {
                DrawCircle(Vector2.Zero, bgRadius, new Color(0.05f, 0.05f, 0.1f, 0.8f));
            }
            DrawArc(Vector2.Zero, Radius + 30, 0, Mathf.Tau, 32, ThemeRef.BorderDefault, 2.0f, true);
        }
        else
        {
            // 列表模式:圆角矩形底图
            float btnHeight = 36f;
            float gap = 4f;
            float totalHeight = _buttons.Count * btnHeight + (_buttons.Count - 1) * gap + 20f;
            float width = 200f;
            var rect = new Rect2(-width / 2f, -totalHeight / 2f, width, totalHeight);
            DrawRect(rect, new Color(0.05f, 0.05f, 0.1f, 0.85f));
            DrawRect(rect, ThemeRef.BorderDefault, false, 1.5f);
        }
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
