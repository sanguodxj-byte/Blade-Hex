// RegionNameOverlay.cs
// 区域名称覆盖层 — 纯文字显示，无背景无边框，不拦截输入
using Godot;
using BladeHex.Map;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 区域名称覆盖层 — 屏幕顶部居中显示当前区域名称
///
/// 特点：
/// 1. 纯文字，无背景无边框
/// 2. 根据语言设置显示英文或中文
/// 3. 平滑淡入淡出动画
/// 4. 不拦截任何输入（MouseFilter.Ignore）
/// </summary>
public partial class RegionNameOverlay : CanvasLayer
{
    // ========================================
    // 常量
    // ========================================

    private const float FadeInDuration = 0.8f;
    private const float FadeOutDuration = 1.2f;
    private const float MinDisplayTime = 2.0f;

    // ========================================
    // 样式
    // ========================================

    private static readonly Color TextColor = new Color(0.95f, 0.85f, 0.65f);
    private static readonly Color ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.6f);

    // ========================================
    // 字段
    // ========================================

    private Label? _nameLabel;

    private string _currentRegionName = "";
    private float _targetAlpha = 0.0f;
    private float _currentAlpha = 0.0f;
    private float _displayTimer = 0.0f;

    // 所有地区名称统一大小，不再按区域面积区分
    private int _fontSizeLarge = 38;
    private int _fontSizeMedium = 38;
    private int _fontSizeSmall = 38;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        Name = "RegionNameOverlay";
        Layer = 8; // 在地图内容之上（CanvasLayer > Node2D），在主UI之下（OverworldUI Layer=10）

        CreateUI();
        SetAlpha(0.0f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_displayTimer > 0)
            _displayTimer -= dt;

        if (_currentAlpha == _targetAlpha) return;

        float speed = _targetAlpha > _currentAlpha
            ? 1.0f / FadeInDuration
            : 1.0f / FadeOutDuration;

        _currentAlpha = Mathf.MoveToward(_currentAlpha, _targetAlpha, speed * dt);
        SetAlpha(_currentAlpha);

        if (Mathf.Abs(_currentAlpha - _targetAlpha) < 0.01f)
        {
            _currentAlpha = _targetAlpha;
            if (_targetAlpha <= 0.0f)
                _currentRegionName = "";
        }
    }

    // ========================================
    // UI 创建
    // ========================================

    private void CreateUI()
    {
        // 根容器 — 全屏，不拦截输入
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        // 顶部居中容器 — 向下偏移至时间轮盘（Y=-96~144）下方，避免互相遮挡
        var topCenter = new VBoxContainer();
        topCenter.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topCenter.OffsetTop = 190;
        topCenter.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(topCenter);

        // 名称标签 — 使用 Godot 原生字体阴影，避免用上下两个 Label 导致重叠错位
        _nameLabel = new Label();
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.AddThemeColorOverride("font_color", TextColor);
        _nameLabel.AddThemeFontSizeOverride("font_size", _fontSizeMedium);
        
        // 🌟 字体美化：配置优雅的中世纪衬线系统字体（优先使用 Georgia 或 Palatino，呈现古籍质感）
        var customFont = new SystemFont();
        customFont.FontNames = new string[] { "Georgia", "Palatino Linotype", "Times New Roman", "serif" };
        _nameLabel.AddThemeFontOverride("font", customFont);

        // 🌟 字体美化：配置精美的黑色外描边，使地名在复杂地图背景下都极度清晰且高品质
        _nameLabel.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.05f, 0.07f, 0.95f));
        _nameLabel.AddThemeConstantOverride("outline_size", 6);

        // 🌟 字体美化：配置柔和的背阴影，营造微浮雕视觉效果
        _nameLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.65f));
        _nameLabel.AddThemeConstantOverride("shadow_offset_x", 3);
        _nameLabel.AddThemeConstantOverride("shadow_offset_y", 3);
        _nameLabel.AddThemeConstantOverride("shadow_outline_size", 4);
        
        _nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        topCenter.AddChild(_nameLabel);
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 显示区域名称
    /// </summary>
    /// <param name="name">显示的名称（根据语言已经是英文或中文）</param>
    /// <param name="sizeClass">区域面积等级</param>
    public void ShowRegion(string name, RegionSize sizeClass = RegionSize.Medium)
    {
        if (name == _currentRegionName && _targetAlpha > 0.5f)
        {
            _displayTimer = MinDisplayTime;
            return;
        }

        _currentRegionName = name;
        _displayTimer = MinDisplayTime;

        // 所有地区名称统一字体大小
        if (_nameLabel != null)
        {
            _nameLabel.Text = name;
            _nameLabel.AddThemeFontSizeOverride("font_size", _fontSizeMedium);
        }

        _targetAlpha = 1.0f;
    }

    /// <summary>隐藏区域名称</summary>
    public void HideRegion()
    {
        if (_displayTimer > 0) return;
        _targetAlpha = 0.0f;
    }

    /// <summary>立即隐藏（无动画）</summary>
    public void HideImmediate()
    {
        _targetAlpha = 0.0f;
        _currentAlpha = 0.0f;
        _currentRegionName = "";
        SetAlpha(0.0f);
    }

    public string GetCurrentRegionName() => _currentRegionName;
    public bool IsShowing() => _targetAlpha > 0.5f;

    // ========================================
    // 内部方法
    // ========================================

    private void SetAlpha(float alpha)
    {
        if (_nameLabel != null)
        {
            var c = _nameLabel.Modulate;
            c.A = alpha;
            _nameLabel.Modulate = c;
        }
    }
}
