// EnemyUnitBar.cs
// 3D战场上敌方单位头顶的HP条 + 士气指示器
// 使用 Label3D + MeshInstance3D 实现3D空间中的单位状态显示
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 3D战场单位头顶信息 — HP数值、士气球、状态效果标签
/// </summary>
[GlobalClass]
public partial class EnemyUnitBar : Node3D
{
    // ============================================================================
    // HP条颜色梯度
    // ============================================================================
    private static readonly Color HP_HIGH = new(0.2f, 0.75f, 0.2f);
    private static readonly Color HP_MID = new(0.85f, 0.75f, 0.1f);
    private static readonly Color HP_LOW = new(0.9f, 0.15f, 0.1f);
    private static readonly Color HP_BG = new(0.15f, 0.08f, 0.08f, 0.7f);
    private static readonly Color HP_BORDER = new(0.3f, 0.15f, 0.15f, 0.6f);

    // ============================================================================
    // 控件引用
    // ============================================================================
    private Label3D _nameLabel = null!;
    private Label3D _hpLabel = null!;

    // ============================================================================
    // 条目尺寸
    // ============================================================================
    private const float BAR_WIDTH = 70.0f;
    private const float BAR_HEIGHT = 7.0f;

    // ============================================================================
    // 状态跟踪
    // ============================================================================
    private int _lastHp = -1;
    private int _lastMaxHp = -1;
    private readonly Dictionary<string, Label3D> _activeStatusEffects = new();

    // ============================================================================
    // 低血量闪烁动画
    // ============================================================================
    private Tween? _flashTween;

    // ============================================================================
    // _Ready
    // ============================================================================
    public override void _Ready()
    {
        _CreateBar();
    }

    // ============================================================================
    // 创建3D信息显示
    // ============================================================================
    private void _CreateBar()
    {
        // 名称标签（最上层）
        _nameLabel = new Label3D();
        _nameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        _nameLabel.PixelSize = 2.5f;
        _nameLabel.FontSize = 16;
        _nameLabel.OutlineSize = 3;
        _nameLabel.Modulate = new Color(0.95f, 0.75f, 0.7f);
        _nameLabel.Position = new Vector3(0, 0, 0);
        AddChild(_nameLabel);

        // HP数值标签
        _hpLabel = new Label3D();
        _hpLabel.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        _hpLabel.PixelSize = 2.5f;
        _hpLabel.FontSize = 14;
        _hpLabel.OutlineSize = 2;
        _hpLabel.Modulate = Colors.White;
        _hpLabel.Position = new Vector3(0, -7, 0);
        AddChild(_hpLabel);

    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>更新血条显示</summary>
    public void UpdateDisplay(Unit unit)
    {
        if (unit == null || !GodotObject.IsInstanceValid(unit))
            return;
        if (unit.Data == null)
            return;

        int maxHp = unit.GetMaxHp();
        int currentHp = unit.CurrentHp;

        // 避免无变化时重复更新
        if (currentHp == _lastHp && maxHp == _lastMaxHp)
            return;
        _lastHp = currentHp;
        _lastMaxHp = maxHp;

        // 更新名称
        _nameLabel.Text = unit.Data.UnitName;

        // 更新HP文本
        float hpRatio = (float)currentHp / Mathf.Max(maxHp, 1);
        Color hpColor;
        if (hpRatio > 0.6f)
            hpColor = HP_HIGH;
        else if (hpRatio > 0.3f)
            hpColor = HP_MID;
        else
            hpColor = HP_LOW;

        _hpLabel.Text = $"{currentHp}/{maxHp}";
        _hpLabel.Modulate = hpColor;

        // 低血量闪烁效果 (HP < 25%)
        if (hpRatio < 0.25f && currentHp > 0)
            _StartLowHpFlash();
        else
            _StopLowHpFlash();

    }

    /// <summary>添加状态效果指示（在名称旁显示简短文字）</summary>
    public void AddStatusEffect(string effectName, string displayText, Color color)
    {
        if (_activeStatusEffects.ContainsKey(effectName))
            return;

        var effectLabel = new Label3D();
        effectLabel.Text = displayText;
        effectLabel.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        effectLabel.PixelSize = 2.0f;
        effectLabel.FontSize = 12;
        effectLabel.OutlineSize = 2;
        effectLabel.Modulate = color;
        // 状态效果显示在名称上方，向右偏移排列
        effectLabel.Position = new Vector3(_activeStatusEffects.Count * 1.2f, 5, 0);
        AddChild(effectLabel);
        _activeStatusEffects[effectName] = effectLabel;
    }

    /// <summary>移除状态效果</summary>
    public void RemoveStatusEffect(string effectName)
    {
        if (!_activeStatusEffects.TryGetValue(effectName, out var label))
            return;

        label.QueueFree();
        _activeStatusEffects.Remove(effectName);

        // 重新排列剩余状态效果
        int idx = 0;
        foreach (var kvp in _activeStatusEffects)
        {
            kvp.Value.Position = new Vector3(idx * 1.2f, 5, 0);
            idx++;
        }
    }

    // ============================================================================
    // 低血量闪烁
    // ============================================================================

    private void _StartLowHpFlash()
    {
        if (_flashTween != null && _flashTween.IsValid())
            return;

        _flashTween = CreateTween();
        if (_flashTween == null)
            return;

        _flashTween.SetLoops();
        _flashTween.TweenProperty(_hpLabel, "modulate", new Color(1.0f, 0.3f, 0.3f), 0.4);
        _flashTween.TweenProperty(_hpLabel, "modulate", HP_LOW, 0.4);
    }

    private void _StopLowHpFlash()
    {
        if (_flashTween != null && _flashTween.IsValid())
        {
            _flashTween.Kill();
            _flashTween = null;
        }
    }
}
