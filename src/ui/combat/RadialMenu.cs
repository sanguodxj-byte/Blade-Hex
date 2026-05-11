using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.UI.Combat;

/// <summary>
/// 环形菜单 —— 用于在点击单位时弹出操作选项
/// 迁移自 GDScript RadialMenu.gd
/// </summary>
public partial class RadialMenu : Control
{
    [Signal] public delegate void ActionSelectedEventHandler(string action);

    public float Radius = 70.0f;
    private readonly List<Button> _buttons = new();
    private readonly UIFactory _factory = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        
        // 点击背景隐藏
        GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                Hide();
            }
        };
    }

    public void Setup(Dictionary<string, string> options)
    {
        // options: Label -> ActionName
        foreach (var btn in _buttons)
        {
            btn.QueueFree();
        }
        _buttons.Clear();

        int count = options.Count;
        if (count == 0) return;

        float angleStep = (float)Math.PI * 2.0f / count;
        float currentAngle = -(float)Math.PI / 2.0f; // 从顶部开始

        foreach (var pair in options)
        {
            string label = pair.Key;
            string actionName = pair.Value;

            var btn = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(60, 40)
            };

            // 样式
            var style = UITheme.Instance.MakePanelStyle(UITheme.Instance.BgPrimary, UITheme.Instance.BorderDefault, 1, UITheme.Instance.RadiusRound);
            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeFontSizeOverride("font_size", UITheme.Instance.FontSizeSm);

            // 位置计算
            Vector2 pos = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * Radius;
            btn.Position = pos - btn.CustomMinimumSize / 2.0f;

            btn.Pressed += () =>
            {
                EmitSignal(SignalName.ActionSelected, actionName);
                Hide();
            };

            AddChild(btn);
            _buttons.Add(btn);
            currentAngle += angleStep;
        }
    }

    public override void _Draw()
    {
        // 绘制轮盘背景（更平滑的64段）
        DrawCircle(Vector2.Zero, Radius + 30, UITheme.Instance.BgTooltip);
        DrawArc(Vector2.Zero, Radius + 30, 0, (float)Math.PI * 2, 64, UITheme.Instance.BorderHighlight, 1.5f, true);
    }

    public void ShowMenu(Vector2 pos)
    {
        Position = pos;
        Show();
    }
}
