using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 右侧敌方信息面板 - 显示所有可见敌方单位的列表
/// 迁移自 GDScript EnemyInfoPanel.gd
/// </summary>
public partial class EnemyInfoPanel : PanelContainer
{
    [Signal] public delegate void EnemyHoveredEventHandler(Unit unit);
    [Signal] public delegate void EnemyUnhoveredEventHandler();

    private VBoxContainer _enemyList = null!;
    private readonly Dictionary<string, Control> _enemyEntries = new();
    private readonly UIFactory _factory = new();

    public override void _Ready()
    {
        SetupPanel();
    }

    private void SetupPanel()
    {
        CustomMinimumSize = new Vector2(220, 0);
        AddThemeStyleboxOverride("panel", UITheme.Instance.MakePanelStyle(new Color(0.08f, 0.06f, 0.1f, 0.92f), new Color(0.4f, 0.15f, 0.15f, 0.8f), 2, 4, 6));

        var mainVbox = new VBoxContainer();
        AddChild(mainVbox);

        var title = new Label
        {
            Text = "— 敌 方 —",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.4f));
        mainVbox.AddChild(title);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        mainVbox.AddChild(scroll);

        _enemyList = new VBoxContainer();
        _enemyList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_enemyList);
    }

    public void AddEnemy(Unit unit)
    {
        if (!GodotObject.IsInstanceValid(unit) || unit.Data == null || _enemyEntries.ContainsKey(unit.Name)) return;

        var entry = CreateEnemyEntry(unit);
        _enemyList.AddChild(entry);
        _enemyEntries[unit.Name] = entry;
    }

    public void RemoveEnemy(Unit unit)
    {
        if (!GodotObject.IsInstanceValid(unit) || !_enemyEntries.TryGetValue(unit.Name, out var entry)) return;

        _enemyList.RemoveChild(entry);
        entry.QueueFree();
        _enemyEntries.Remove(unit.Name);
    }

    public void UpdateEnemy(Unit unit)
    {
        if (!GodotObject.IsInstanceValid(unit) || !_enemyEntries.TryGetValue(unit.Name, out var entry)) return;

        var hpBar = entry.GetNodeOrNull<ProgressBar>("HPBar");
        if (hpBar != null)
        {
            hpBar.MaxValue = unit.GetMaxHp();
            hpBar.Value = unit.CurrentHp;
        }

        var hpLabel = entry.GetNodeOrNull<Label>("HPLabel");
        if (hpLabel != null)
        {
            hpLabel.Text = $"HP {unit.CurrentHp}/{unit.GetMaxHp()}";
        }
    }

    private PanelContainer CreateEnemyEntry(Unit unit)
    {
        var entry = new PanelContainer();
        entry.AddThemeStyleboxOverride("panel", UITheme.Instance.MakePanelStyle(new Color(0.15f, 0.08f, 0.1f, 0.6f), new Color(0.3f, 0.15f, 0.15f, 0.4f), 1, 3, 5));

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        entry.AddChild(vbox);

        var nameLabel = new Label
        {
            Text = unit.Data.UnitName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(nameLabel);

        var hpBar = new ProgressBar
        {
            Name = "HPBar",
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(120, 12),
            MaxValue = unit.GetMaxHp(),
            Value = unit.CurrentHp
        };
        UITheme.Instance.ApplyBarTheme(hpBar, UITheme.Instance.HpHigh, new Color(0.2f, 0.1f, 0.1f, 0.6f));
        vbox.AddChild(hpBar);

        var hpLabel = new Label { Name = "HPLabel", Text = $"HP {unit.CurrentHp}/{unit.GetMaxHp()}" };
        hpLabel.AddThemeFontSizeOverride("font_size", 10);
        vbox.AddChild(hpLabel);

        entry.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseMotion)
            {
                EmitSignal(SignalName.EnemyHovered, unit);
            }
        };

        return entry;
    }
}
