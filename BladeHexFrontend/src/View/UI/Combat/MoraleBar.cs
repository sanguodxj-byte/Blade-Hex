// MoraleBar.cs
// 士气条UI — 显示单位士气值和士气等级，颜色编码
// 对应策划案 03-战术战斗系统 → 六、士气系统
using Godot;
using BladeHex.Data;

namespace BladeHex.UI.Combat;

/// <summary>
/// 士气条 — 显示单位士气值和士气等级，颜色编码
/// </summary>
[GlobalClass]
public partial class MoraleBar : Control
{
    // ============================================================================
    // 内部组件
    // ============================================================================
    private ProgressBar _bar = null!;
    private Label _label = null!;

    // ============================================================================
    // _Ready
    // ============================================================================
    public override void _Ready()
    {
        _Setup();
    }

    // ============================================================================
    // 初始化 UI 结构
    // ============================================================================
    private void _Setup()
    {
        // 布局
        CustomMinimumSize = new Vector2(120, 18);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // 士气条
        _bar = new ProgressBar();
        _bar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _bar.ShowPercentage = false;
        _bar.MinValue = -60;
        _bar.MaxValue = 40;
        _bar.Value = 0;

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        bgStyle.SetCornerRadiusAll(3);
        _bar.AddThemeStyleboxOverride("background", bgStyle);

        AddChild(_bar);

        // 数值标签
        _label = new Label();
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 11);
        _label.Text = "士气: 0";
        _label.ZIndex = 1;
        AddChild(_label);
    }

    // ============================================================================
    // 更新显示
    // ============================================================================
    /// <summary>
    /// 更新士气条显示，包含颜色编码。
    /// </summary>
    /// <param name="moraleValue">当前士气值 (-60 ~ +40)</param>
    public void UpdateMorale(int moraleValue)
    {
        _bar.Value = moraleValue;
        _label.Text = $"士气: {moraleValue}";

        // 颜色编码
        var fillStyle = new StyleBoxFlat();
        fillStyle.SetCornerRadiusAll(3);

        var level = _GetMoraleLevel(moraleValue);
        switch (level)
        {
            case MoraleLevel.High:
                fillStyle.BgColor = new Color(0.2f, 0.8f, 0.3f);
                _label.AddThemeColorOverride("font_color", Colors.White);
                break;

            case MoraleLevel.Normal:
                fillStyle.BgColor = new Color(0.7f, 0.7f, 0.7f);
                _label.AddThemeColorOverride("font_color", Colors.White);
                break;

            case MoraleLevel.Low:
                fillStyle.BgColor = new Color(0.9f, 0.7f, 0.1f);
                _label.AddThemeColorOverride("font_color", Colors.Black);
                break;

            case MoraleLevel.Broken:
                fillStyle.BgColor = new Color(0.9f, 0.2f, 0.1f);
                _label.AddThemeColorOverride("font_color", Colors.White);
                break;

            case MoraleLevel.Routing:
                fillStyle.BgColor = new Color(0.9f, 0.0f, 0.0f);
                // 溃逃时闪烁效果预留
                _label.AddThemeColorOverride("font_color", Colors.Yellow);
                break;
        }

        _bar.AddThemeStyleboxOverride("fill", fillStyle);
    }

    // ============================================================================
    // 士气等级判定
    // ============================================================================
    /// <summary>根据士气值返回对应士气等级</summary>
    private static MoraleLevel _GetMoraleLevel(int morale)
    {
        if (morale >= 20) return MoraleLevel.High;
        if (morale >= -19) return MoraleLevel.Normal;
        if (morale >= -39) return MoraleLevel.Low;
        if (morale >= -59) return MoraleLevel.Broken;
        return MoraleLevel.Routing;
    }
}
