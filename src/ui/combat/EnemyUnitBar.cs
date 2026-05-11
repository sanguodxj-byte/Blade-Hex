using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 3D战场上敌方单位头顶的HP条 + 士气指示器
/// 迁移自 GDScript EnemyUnitBar.gd
/// </summary>
public partial class EnemyUnitBar : Node3D
{
    private Label3D _nameLabel = null!;
    private Label3D _hpLabel = null!;
    private MeshInstance3D _moraleSphere = null!;
    
    private int _lastHp = -1;
    private int _lastMaxHp = -1;
    private int _lastMoraleLevel = -1;
    private Tween? _flashTween;

    private static readonly Color HpHigh = new(0.2f, 0.75f, 0.2f);
    private static readonly Color HpMid = new(0.85f, 0.75f, 0.1f);
    private static readonly Color HpLow = new(0.9f, 0.15f, 0.1f);

    public override void _Ready()
    {
        CreateBar();
    }

    private void CreateBar()
    {
        // 名称标签
        _nameLabel = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            PixelSize = 0.0025f, // 原 GDScript 中 2.5 可能对应 0.0025
            FontSize = 32,
            OutlineSize = 4,
            Modulate = new Color(0.95f, 0.75f, 0.7f),
            Position = new Vector3(0, 0.6f, 0) // 向上偏移
        };
        AddChild(_nameLabel);

        // HP标签
        _hpLabel = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            PixelSize = 0.0025f,
            FontSize = 24,
            OutlineSize = 3,
            Position = new Vector3(0, 0.35f, 0)
        };
        AddChild(_hpLabel);

        // 士气球
        _moraleSphere = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f },
            Position = new Vector3(0.4f, 0.35f, 0)
        };
        AddChild(_moraleSphere);
    }

    public void UpdateDisplay(Unit unit)
    {
        if (!GodotObject.IsInstanceValid(unit) || unit.Data == null) return;

        int maxHp = unit.GetMaxHp();
        int currentHp = unit.CurrentHp;

        if (currentHp == _lastHp && maxHp == _lastMaxHp) return;
        _lastHp = currentHp;
        _lastMaxHp = maxHp;

        _nameLabel.Text = unit.Data.UnitName;
        _hpLabel.Text = $"{currentHp}/{maxHp}";

        float ratio = (float)currentHp / Math.Max(maxHp, 1);
        Color color = ratio > 0.6f ? HpHigh : (ratio > 0.3f ? HpMid : HpLow);
        _hpLabel.Modulate = color;

        // 低血量闪烁
        if (ratio < 0.25f && currentHp > 0) StartFlash();
        else StopFlash();

        if (unit.Data.IsEnemy) UpdateMorale((int)unit.Data.GetMoraleLevel());
    }

    private void UpdateMorale(int level)
    {
        if (level == _lastMoraleLevel) return;
        _lastMoraleLevel = level;

        var mat = new StandardMaterial3D
        {
            AlbedoColor = level switch
            {
                0 => Colors.Cyan,
                2 => Colors.Yellow,
                3 => Colors.OrangeRed,
                4 => Colors.Red,
                _ => Colors.Gray
            },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _moraleSphere.MaterialOverride = mat;
    }

    private void StartFlash()
    {
        if (_flashTween?.IsValid() == true) return;
        _flashTween = CreateTween();
        _flashTween.SetLoops();
        _flashTween.TweenProperty(_hpLabel, "modulate", Colors.White, 0.4f);
        _flashTween.TweenProperty(_hpLabel, "modulate", HpLow, 0.4f);
    }

    private void StopFlash() => _flashTween?.Kill();
}
