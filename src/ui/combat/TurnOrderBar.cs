using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 回合顺序显示栏 — 显示当前回合所有单位的行动顺序
/// 迁移自 GDScript TurnOrderBar.gd
/// </summary>
public partial class TurnOrderBar : HBoxContainer
{
    [Signal] public delegate void UnitClickedEventHandler(Unit unit);

    private Label _turnLabel = null!;
    private readonly List<Control> _unitIcons = new();
    private readonly UIFactory _factory = new();
    private Control _iconContainer = null!;

    public override void _Ready()
    {
        Setup();
    }

    private void Setup()
    {
        _turnLabel = _factory.CreateBodyLabel("第1回合", UITheme.Instance.TextAccent);
        _turnLabel.CustomMinimumSize = new Vector2(80, 0);
        AddChild(_turnLabel);

        AddChild(_factory.CreateSeparatorV());

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(scroll);

        _iconContainer = new HBoxContainer();
        _iconContainer.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_iconContainer);
    }

    public void SetTurnNumber(int turn)
    {
        _turnLabel.Text = $"第{turn}回合";
    }

    public void SetUnitOrder(List<Unit> units, Unit? activeUnit = null)
    {
        foreach (Node child in _iconContainer.GetChildren())
        {
            child.QueueFree();
        }
        _unitIcons.Clear();

        foreach (var unit in units)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.Data == null) continue;
            var icon = CreateUnitIcon(unit, unit == activeUnit);
            _iconContainer.AddChild(icon);
            _unitIcons.Add(icon);
        }
    }

    private PanelContainer CreateUnitIcon(Unit unit, bool isActive)
    {
        var icon = new PanelContainer();
        icon.CustomMinimumSize = new Vector2(40, 40);
        icon.SetMeta("unit_ref", unit);

        StyleBoxFlat style;
        if (isActive)
        {
            style = UITheme.Instance.MakePanelStyle(new Color(0.3f, 0.28f, 0.1f, 0.9f), UITheme.Instance.BorderHighlight, 2, UITheme.Instance.RadiusSm, 2);
        }
        else
        {
            bool isEnemy = unit.Data.IsEnemy;
            Color bg = isEnemy ? new Color(0.2f, 0.08f, 0.08f, 0.7f) : new Color(0.08f, 0.12f, 0.2f, 0.7f);
            Color border = isEnemy ? UITheme.Instance.BorderEnemy : UITheme.Instance.BorderFriendly;
            style = UITheme.Instance.MakePanelStyle(bg, border, 1, UITheme.Instance.RadiusSm, 2);
        }
        icon.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        icon.AddChild(vbox);

        if (unit.Data.Portrait != null)
        {
            var rect = new TextureRect
            {
                Texture = unit.Data.Portrait,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(36, 36)
            };
            vbox.AddChild(rect);
        }
        else
        {
            var nameLabel = new Label
            {
                Text = unit.Data.UnitName.Length >= 2 ? unit.Data.UnitName.Substring(0, 2) : unit.Data.UnitName,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            nameLabel.AddThemeFontSizeOverride("font_size", UITheme.Instance.FontSizeXs);
            vbox.AddChild(nameLabel);
        }

        var hpBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(30, 3),
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = unit.GetMaxHp(),
            Value = unit.CurrentHp
        };
        UITheme.Instance.ApplyBarTheme(hpBar, UITheme.Instance.HpHigh, UITheme.Instance.HpBarBg);
        vbox.AddChild(hpBar);

        icon.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                EmitSignal(SignalName.UnitClicked, unit);
            }
        };

        return icon;
    }
}
