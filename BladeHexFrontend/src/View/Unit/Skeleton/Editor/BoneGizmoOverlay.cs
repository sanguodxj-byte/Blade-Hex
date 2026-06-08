// BoneGizmoOverlay.cs
// 骨骼关节 gizmo 绘制层 — 在屏幕空间（CanvasLayer）用 draw_circle 画关节点
// 通过 3D 相机投影将骨骼 2D 位置转换为屏幕坐标，确保 gizmo 始终像素级清晰
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>
/// 骨骼关节 gizmo 覆盖层（屏幕空间版）。
/// <para>作为 CanvasLayer 的子节点，每帧将骨骼 2D 位置投影到屏幕坐标后绘制。</para>
/// <para>不经过 SubViewport 缩放，始终清晰。</para>
/// </summary>
public partial class BoneGizmoOverlay : Control
{
    /// <summary>关节圆点半径（屏幕像素）</summary>
    private const float JointRadius = 6.0f;

    /// <summary>选中骨骼的高亮半径</summary>
    private const float SelectedRadius = 8.0f;

    /// <summary>连线宽度</summary>
    private const float LineWidth = 2.0f;

    /// <summary>当前选中的骨骼名（高亮显示）</summary>
    public string? SelectedBone { get; set; }

    /// <summary>是否处于位移模式</summary>
    public bool DisplaceMode { get; set; }

    /// <summary>位移模式下的骨骼名</summary>
    public string? DisplaceBone { get; set; }

    /// <summary>是否处于旋转模式</summary>
    public bool RotationMode { get; set; }

    /// <summary>旋转模式下的骨骼名</summary>
    public string? RotationBone { get; set; }

    /// <summary>3D 相机引用（用于投影计算）</summary>
    public Camera3D? Camera { get; set; }

    /// <summary>骨骼 billboard 的 3D 世界位置</summary>
    public Sprite3D? Billboard { get; set; }

    /// <summary>骨骼配置（用于 PixelSize 换算）</summary>
    public BoneConfig? Config { get; set; }

    private readonly Dictionary<string, Node2D> _boneNodes;

    /// <summary>骨骼颜色映射</summary>
    private static readonly Dictionary<string, Color> BoneColors = new()
    {
        ["Torso"]    = new Color(0.2f, 0.8f, 0.3f),    // 绿色 — 躯干
        ["Head"]     = new Color(1.0f, 0.85f, 0.2f),   // 黄色 — 头部
        ["ArmL"]     = new Color(0.3f, 0.6f, 1.0f),    // 蓝色 — 左臂
        ["ForearmL"] = new Color(0.5f, 0.75f, 1.0f),   // 浅蓝 — 左前臂
        ["ArmR"]     = new Color(1.0f, 0.4f, 0.3f),    // 红色 — 右臂
        ["ForearmR"] = new Color(1.0f, 0.6f, 0.5f),    // 浅红 — 右前臂
        ["Weapon"]   = new Color(1.0f, 0.5f, 0.0f),    // 橙色 — 武器
        ["Shield"]   = new Color(0.6f, 0.4f, 1.0f),    // 紫色 — 盾牌
    };

    /// <summary>连线颜色（半透明白）</summary>
    private static readonly Color LineColor = new(1.0f, 1.0f, 1.0f, 0.35f);

    /// <summary>骨骼层级关系（parent → children）用于画连线</summary>
    private static readonly Dictionary<string, string[]> BoneHierarchy = new()
    {
        ["Torso"]    = new[] { "Head", "ArmL", "ArmR" },
        ["ArmL"]     = new[] { "ForearmL" },
        ["ArmR"]     = new[] { "ForearmR" },
        ["ForearmL"] = new[] { "Shield" },
        ["ForearmR"] = new[] { "Weapon" },
    };

    public BoneGizmoOverlay(Dictionary<string, Node2D> boneNodes)
    {
        _boneNodes = boneNodes;
        Name = "BoneGizmoOverlay";
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        // 铺满整个屏幕
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_boneNodes.Count == 0 || Camera == null || Billboard == null || Config == null) return;

        // billboard 的 3D 世界位置投影到屏幕
        var billboardWorldPos = Billboard.GlobalPosition;
        if (Camera.IsPositionBehind(billboardWorldPos)) return;
        var billboardScreenPos = Camera.UnprojectPosition(billboardWorldPos);

        // 世界单位到屏幕像素的换算系数
        float worldToScreen = GetViewportRect().Size.Y / Camera.Size;

        // 缓存各骨骼的屏幕位置
        var screenPositions = new Dictionary<string, Vector2>();
        foreach (var (name, node) in _boneNodes)
        {
            // 骨骼在 SubViewport 内的 2D 全局位置（相对于画布原点）
            var bone2DPos = node.GlobalPosition - UpperBodySkeleton.CanvasCenter;
            // 转换为屏幕偏移：
            // 2D 中 Y 向下为正，骨骼在角色上方时 bone2DPos.Y 为负
            // 屏幕空间 Y 向下为正，骨骼在上方时 screenOffset.Y 应为负
            // 因此 Y 方向直接映射（不取反）
            var screenOffset = new Vector2(bone2DPos.X, bone2DPos.Y) * Config.PixelSize * worldToScreen;
            screenPositions[name] = billboardScreenPos + screenOffset;
        }

        // 画骨骼连线
        foreach (var (parentName, children) in BoneHierarchy)
        {
            if (!screenPositions.TryGetValue(parentName, out var parentPos)) continue;
            foreach (var childName in children)
            {
                if (!screenPositions.TryGetValue(childName, out var childPos)) continue;
                DrawLine(parentPos, childPos, LineColor, LineWidth, true);
            }
        }

        // 画关节圆点
        foreach (var (name, screenPos) in screenPositions)
        {
            var color = BoneColors.GetValueOrDefault(name, new Color(0.8f, 0.8f, 0.8f));
            bool isSelected = name == SelectedBone;
            bool isDisplace = DisplaceMode && name == DisplaceBone;
            bool isRotate = RotationMode && name == RotationBone;
            float radius = isSelected ? SelectedRadius : JointRadius;

            // 外圈（深色边框）
            DrawCircle(screenPos, radius + 2.0f, new Color(0, 0, 0, 0.7f));
            // 填充
            DrawCircle(screenPos, radius, color);

            // 选中时额外画一个高亮环
            if (isSelected)
            {
                DrawArc(screenPos, radius + 4.0f, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.9f), 2.0f);
            }

            // 位移模式：画十字箭头
            if (isDisplace)
            {
                float arrowLen = 12.0f;
                var arrowColor = new Color(0.2f, 1.0f, 0.2f, 0.9f); // 绿色
                DrawLine(screenPos, screenPos + new Vector2(arrowLen, 0), arrowColor, 2.0f, true);
                DrawLine(screenPos, screenPos + new Vector2(-arrowLen, 0), arrowColor, 2.0f, true);
                DrawLine(screenPos, screenPos + new Vector2(0, arrowLen), arrowColor, 2.0f, true);
                DrawLine(screenPos, screenPos + new Vector2(0, -arrowLen), arrowColor, 2.0f, true);
                // 外圈高亮
                DrawArc(screenPos, radius + 6.0f, 0, Mathf.Tau, 24, arrowColor, 2.0f);
            }

            // 旋转模式：画旋转箭头
            if (isRotate)
            {
                float arcRadius = radius + 8.0f;
                var arcColor = new Color(1.0f, 0.5f, 0.0f, 0.9f); // 橙色
                DrawArc(screenPos, arcRadius, 0, Mathf.Tau * 0.75f, 24, arcColor, 2.5f, true);
                // 箭头头部
                float arrowAngle = Mathf.Tau * 0.75f;
                var arrowHead = screenPos + new Vector2(
                    Mathf.Cos(arrowAngle) * arcRadius,
                    Mathf.Sin(arrowAngle) * arcRadius
                );
                DrawCircle(arrowHead, 3.0f, arcColor);
            }
        }
    }
}
