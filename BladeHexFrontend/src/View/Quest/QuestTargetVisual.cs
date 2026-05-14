// QuestTargetVisual.cs
// 委托目标点大地图可视化组件
// 在 OverworldScene 中渲染任务标记: 脉冲光圈 + 图标 + 任务名称
//
// 画风: 战场兄弟式古朴风格, 使用几何形状+低饱和色, 不使用花哨特效
//   - 标记底座: 与目标点类型匹配的几何形状
//   - 脉冲光圈: 缓慢扩大/淡出的圆环, 提示"这里有个任务"
//   - 名称标签: 任务目标描述
using Godot;
using System;
using BladeHex.Strategic;

namespace BladeHex.View.Quest;

/// <summary>
/// 委托目标点大地图可视化组件。
/// 渲染脉冲光圈 + 几何底座 + 名称标签。
/// </summary>
[GlobalClass]
public partial class QuestTargetVisual : Node2D
{
    // ========================================================================
    // 配置
    // ========================================================================

    /// <summary>脉冲动画周期（秒）</summary>
    private const float PulsePeriod = 2.0f;
    /// <summary>脉冲最大半径</summary>
    private const float PulseMaxRadius = 40.0f;
    /// <summary>脉冲最小半径</summary>
    private const float PulseMinRadius = 15.0f;
    /// <summary>检测玩家接近的距离（像素）</summary>
    private const float ApproachDist = 60.0f;

    // ========================================================================
    // 引用
    // ========================================================================

    /// <summary>关联的目标点数据</summary>
    public QuestTargetSite? TargetSite { get; private set; }

    // 视觉子节点
    private Polygon2D? _basePoly;
    private Polygon2D? _pulseRing;
    public Label? NameLabel { get; private set; }
    private Label? _dangerLabel;

    // 脉冲计时器
    private float _pulseTime;

    // ========================================================================
    // 生命周期
    // ========================================================================

    public override void _Ready()
    {
        SetupVisuals();
    }

    public override void _Process(double delta)
    {
        _pulseTime = (_pulseTime + (float)delta) % PulsePeriod;
        UpdatePulse();
    }

    // ========================================================================
    // 公共接口
    // ========================================================================

    /// <summary>用目标点数据初始化视觉</summary>
    public void Setup(QuestTargetSite site)
    {
        TargetSite = site;
        Position = site.WorldPosition;

        if (!IsNodeReady())
        {
            // 延迟到 Ready 后应用样式
            Ready += () => ApplySiteStyle(site);
            return;
        }

        ApplySiteStyle(site);
    }

    /// <summary>标记为已完成（淡出效果）</summary>
    public void MarkCleared()
    {
        if (_pulseRing != null)
            _pulseRing.Visible = false;

        if (_basePoly != null)
            _basePoly.Color = new Color(0.3f, 0.3f, 0.3f, 0.4f);

        NameLabel?.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 0.5f));

        if (_dangerLabel != null)
            _dangerLabel.Visible = false;
    }

    // ========================================================================
    // 内部: 视觉搭建
    // ========================================================================

    private void SetupVisuals()
    {
        // 底座几何（目标点主体标记）
        _basePoly = new Polygon2D();
        AddChild(_basePoly);

        // 脉冲光圈（外环动画）
        _pulseRing = new Polygon2D();
        AddChild(_pulseRing);

        // 任务名称标签
        NameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-60, 20),
            CustomMinimumSize = new Vector2(120, 20)
        };
        NameLabel.AddThemeFontSizeOverride("font_size", 12);
        AddChild(NameLabel);

        // 危险度标签（星级）
        _dangerLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-30, -35),
            CustomMinimumSize = new Vector2(60, 16)
        };
        _dangerLabel.AddThemeFontSizeOverride("font_size", 11);
        AddChild(_dangerLabel);

        // 初始形状
        SetBaseShapeHex(16.0f);
        UpdateRingShape(PulseMinRadius, Colors.White);
    }

    /// <summary>应用目标点类型的视觉样式</summary>
    private void ApplySiteStyle(QuestTargetSite site)
    {
        Color color = site.GetDisplayColor();

        // 底座颜色
        if (_basePoly != null)
            _basePoly.Color = color;

        // 底座形状（按类型区分）
        switch (site.CurrentSiteType)
        {
            case QuestTargetSite.SiteType.GoblinCamp:
            case QuestTargetSite.SiteType.BanditCamp:
                SetBaseShapeTriangle(18.0f);
                break;
            case QuestTargetSite.SiteType.KoboldMine:
            case QuestTargetSite.SiteType.DungeonEntrance:
            case QuestTargetSite.SiteType.Tomb:
                SetBaseShapeDiamond(16.0f);
                break;
            case QuestTargetSite.SiteType.MinotaurFort:
            case QuestTargetSite.SiteType.DragonLair:
                SetBaseShapeHex(22.0f);
                break;
            case QuestTargetSite.SiteType.CultHideout:
                SetBaseShapePentagon(18.0f);
                break;
            default:
                SetBaseShapeHex(16.0f);
                break;
        }

        // 名称
        if (NameLabel != null)
        {
            NameLabel.Text = site.SiteName;
            NameLabel.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 0.85f));
        }

        // 危险度星级
        if (_dangerLabel != null)
        {
            string stars = new string('*', site.DangerStars);
            _dangerLabel.Text = stars;
            _dangerLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.8f, 0.3f));
        }
    }

    // ========================================================================
    // 内部: 形状生成
    // ========================================================================

    /// <summary>六边形底座</summary>
    private void SetBaseShapeHex(float radius)
    {
        var points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Tau * i / 6.0f - Mathf.Pi / 6.0f;
            points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
        if (_basePoly != null)
            _basePoly.Polygon = points;
    }

    /// <summary>三角形底座（营地类）</summary>
    private void SetBaseShapeTriangle(float size)
    {
        var points = new Vector2[]
        {
            new(0, -size),
            new(size * 0.866f, size * 0.5f),
            new(-size * 0.866f, size * 0.5f)
        };
        if (_basePoly != null)
            _basePoly.Polygon = points;
    }

    /// <summary>菱形底座（洞穴/遗迹类）</summary>
    private void SetBaseShapeDiamond(float size)
    {
        var points = new Vector2[]
        {
            new(0, -size),
            new(size * 0.7f, 0),
            new(0, size),
            new(-size * 0.7f, 0)
        };
        if (_basePoly != null)
            _basePoly.Polygon = points;
    }

    /// <summary>五边形底座（教团类）</summary>
    private void SetBaseShapePentagon(float radius)
    {
        var points = new Vector2[5];
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Tau * i / 5.0f - Mathf.Pi / 2.0f;
            points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
        if (_basePoly != null)
            _basePoly.Polygon = points;
    }

    // ========================================================================
    // 内部: 脉冲动画
    // ========================================================================

    private void UpdatePulse()
    {
        if (_pulseRing == null) return;

        if (TargetSite == null || TargetSite.IsCleared)
        {
            _pulseRing.Visible = false;
            return;
        }

        _pulseRing.Visible = true;

        // 正弦波驱动脉冲半径和透明度
        float t = _pulseTime / PulsePeriod;
        float radius = Mathf.Lerp(PulseMinRadius, PulseMaxRadius, t);
        float alpha = Mathf.Lerp(0.6f, 0.0f, t);

        Color ringColor = TargetSite.GetDisplayColor();
        ringColor.A = alpha;
        UpdateRingShape(radius, ringColor);
    }

    /// <summary>更新光圈形状（正多边形）</summary>
    private void UpdateRingShape(float radius, Color color)
    {
        if (_pulseRing == null) return;

        const int segments = 24;
        var points = new Vector2[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        _pulseRing.Polygon = points;
        _pulseRing.Color = color;
    }
}
