// POIRenderer2D.cs
// 2D POI 渲染器 — 替代 POIController 的 3D 渲染部分
// 为每个已揭示的 POI 创建 2D 标记（Sprite2D + Label + Polygon2D 脚印覆盖层）
//
// 职责：
//   - 渲染 POI 标记（Sprite2D 圆形图标 + Label 名称）
//   - 渲染 POI 脚印覆盖层（Polygon2D，每个 occupied hex 一个多边形）
//   - 围攻闪烁效果（_Process 中 sin 脉冲）
//   - 动态增删 POI（阵营变更时重建）
//
// 注意：玩家进入检测仍由 POIController.CheckEnter() 负责（纯 hex 命中逻辑）
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld2d.Components;

[GlobalClass]
public partial class POIRenderer2D : Node2D
{
    // ========================================
    // 常量
    // ========================================

    private const float HexSize = 156.0f; // 与 HexOverworldTile.HexSize 一致
    private const int CircleTexSize = 64; // 共享圆形纹理尺寸

    // ========================================
    // 配色（与 POIController 一致）
    // ========================================

    private static readonly Color ColorTown = new(0.9f, 0.85f, 0.3f);
    private static readonly Color ColorVillage = new(0.7f, 0.6f, 0.3f);
    private static readonly Color ColorCastle = new(0.5f, 0.5f, 0.7f);
    private static readonly Color ColorPort = new(0.4f, 0.55f, 0.7f);
    private static readonly Color ColorShrine = new(0.7f, 0.5f, 0.7f);
    private static readonly Color ColorDefault = new(0.6f, 0.4f, 0.2f);

    // 脚印覆盖层颜色（半透明）
    private static readonly Color OverlayTown = new(0.9f, 0.85f, 0.3f, 0.25f);
    private static readonly Color OverlayVillage = new(0.7f, 0.6f, 0.3f, 0.25f);
    private static readonly Color OverlayCastle = new(0.5f, 0.5f, 0.7f, 0.25f);
    private static readonly Color OverlayPort = new(0.4f, 0.55f, 0.7f, 0.25f);
    private static readonly Color OverlayShrine = new(0.7f, 0.5f, 0.7f, 0.25f);
    private static readonly Color OverlayDefault = new(0.6f, 0.4f, 0.2f, 0.25f);

    // ========================================
    // 引用
    // ========================================

    private List<OverworldPOI>? _worldPois;
    private FogOfWar? _fog;

    // ========================================
    // 渲染节点
    // ========================================

    /// <summary>每个 POI 的渲染节点组</summary>
    private readonly Dictionary<string, POINodeSet> _poiNodeSets = new();

    /// <summary>共享圆形纹理（白色，用于 Modulate 着色）</summary>
    private static ImageTexture? _sharedCircleTexture;

    // ========================================
    // 状态
    // ========================================

    private float _blinkTime;

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>初始化渲染器</summary>
    public void Initialize(List<OverworldPOI> worldPois, FogOfWar? fog)
    {
        _worldPois = worldPois;
        _fog = fog;
        ZIndex = 55; // 高于 terrain 和 props，低于 fog(100)
    }

    /// <summary>渲染所有已揭示 POI</summary>
    public void RenderAll()
    {
        if (_worldPois == null) return;

        foreach (var poi in _worldPois)
        {
            // 只渲染已揭示的 POI
            if (_fog != null && !_fog.IsRevealed(poi.Position.X, poi.Position.Y))
                continue;

            AddPOI(poi);
        }

        GD.Print($"[POIRenderer2D] 渲染 {_poiNodeSets.Count} 个 POI（含脚印覆盖层）");
    }

    /// <summary>添加单个 POI 的渲染节点（已存在则重建）</summary>
    public void AddPOI(OverworldPOI poi)
    {
        if (_poiNodeSets.ContainsKey(poi.PoiName))
            RemovePOI(poi.PoiName);

        var nodes = new POINodeSet();

        // --- 脚印覆盖层（一个 Polygon2D per occupied hex）---
        if (poi.OccupiedHexes.Length > 0)
        {
            nodes.OverlayPolygons = new List<Polygon2D>();
            Color overlayColor = GetOverlayColor(poi);

            foreach (var hex in poi.OccupiedHexes)
            {
                var hexCenter = HexOverworldTile.AxialToPixel(hex.X, hex.Y);
                var vertices = BuildHexVertices(Vector2.Zero);

                var poly = new Polygon2D
                {
                    Polygon = vertices,
                    Color = overlayColor,
                    Position = hexCenter,
                };
                AddChild(poly);
                nodes.OverlayPolygons.Add(poly);
            }
        }

        // --- 中心标记（Sprite2D 圆形图标）---
        float markerSize = GetMarkerPixelSize(poi.Scale);
        Color color = GetMarkerColor(poi);

        var marker = new Sprite2D
        {
            Texture = GetCircleTexture(),
            Modulate = color,
            Position = poi.Position,
            Scale = new Vector2(markerSize / CircleTexSize, markerSize / CircleTexSize),
        };
        AddChild(marker);
        nodes.Marker = marker;
        nodes.MarkerBaseColor = color;

        // --- 名称标签 ---
        float labelWidth = 250f;
        float labelHeight = 28f;
        var label = new Label
        {
            Text = poi.PoiName,
            Size = new Vector2(labelWidth, labelHeight),
            Position = poi.Position + new Vector2(-labelWidth * 0.5f, -markerSize * 0.75f - labelHeight - 4f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var labelSettings = new LabelSettings
        {
            FontSize = 14,
            FontColor = new Color(1.0f, 0.95f, 0.85f),
            OutlineSize = 3,
            OutlineColor = new Color(0.05f, 0.05f, 0.05f),
        };
        label.LabelSettings = labelSettings;
        AddChild(label);
        nodes.Label = label;

        _poiNodeSets[poi.PoiName] = nodes;
    }

    /// <summary>移除单个 POI 的渲染节点</summary>
    public void RemovePOI(string poiName)
    {
        if (!_poiNodeSets.TryGetValue(poiName, out var nodes)) return;

        nodes.Marker?.QueueFree();
        nodes.Label?.QueueFree();
        if (nodes.OverlayPolygons != null)
        {
            foreach (var poly in nodes.OverlayPolygons)
                poly.QueueFree();
        }

        _poiNodeSets.Remove(poiName);
    }

    /// <summary>重建单个 POI（阵营变更后调用）</summary>
    public void RebuildPOI(OverworldPOI poi)
    {
        AddPOI(poi); // 内部会先 Remove 再 Add
    }

    // ========================================
    // 每帧更新（围攻闪烁）
    // ========================================

    public override void _Process(double delta)
    {
        if (_worldPois == null) return;

        _blinkTime += (float)delta;
        float factor = 0.5f + 0.5f * Mathf.Sin(_blinkTime * 10f); // ~1.5Hz 红色呼吸

        foreach (var poi in _worldPois)
        {
            if (!_poiNodeSets.TryGetValue(poi.PoiName, out var nodes)) continue;
            if (nodes.Marker == null || !IsInstanceValid(nodes.Marker)) continue;

            if (poi.IsUnderSiege)
            {
                // 围攻闪烁：红色脉冲
                nodes.Marker.Modulate = new Color(
                    0.9f,
                    0.1f * (1f - factor),
                    0.1f * (1f - factor)
                );
            }
            else
            {
                // 恢复默认颜色
                nodes.Marker.Modulate = nodes.MarkerBaseColor;
            }
        }
    }

    // ========================================
    // 辅助方法
    // ========================================

    /// <summary>获取 POI 类型对应的标记颜色</summary>
    private static Color GetMarkerColor(OverworldPOI poi)
    {
        if (poi.PoiTypeEnum == OverworldPOI.POIType.Town && poi.IsPortCity)
            return ColorPort;

        return poi.PoiTypeEnum switch
        {
            OverworldPOI.POIType.Town => ColorTown,
            OverworldPOI.POIType.Village => ColorVillage,
            OverworldPOI.POIType.Castle => ColorCastle,
            _ => ColorDefault,
        };
    }

    /// <summary>获取 POI 类型对应的覆盖层颜色（半透明）</summary>
    private static Color GetOverlayColor(OverworldPOI poi)
    {
        if (poi.PoiTypeEnum == OverworldPOI.POIType.Town && poi.IsPortCity)
            return OverlayPort;

        return poi.PoiTypeEnum switch
        {
            OverworldPOI.POIType.Town => OverlayTown,
            OverworldPOI.POIType.Village => OverlayVillage,
            OverworldPOI.POIType.Castle => OverlayCastle,
            _ => OverlayDefault,
        };
    }

    /// <summary>MarkerSize (3D unit) → 像素尺寸</summary>
    private static float GetMarkerPixelSize(POIScale scale)
    {
        float markerSize = POIScaleTable.Get(scale).MarkerSize;
        return markerSize * HexSize; // Tiny≈55px, Small≈70px, Medium≈94px, Large≈125px
    }

    /// <summary>生成六边形顶点（相对坐标，用于 Polygon2D）</summary>
    private static Vector2[] BuildHexVertices(Vector2 center)
    {
        var vertices = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI / 3f; // 60° * i
            vertices[i] = center + new Vector2(
                HexSize * MathF.Cos(angle),
                HexSize * MathF.Sin(angle)
            );
        }
        return vertices;
    }

    /// <summary>获取共享的白色圆形纹理（抗锯齿）</summary>
    private static ImageTexture GetCircleTexture()
    {
        if (_sharedCircleTexture != null) return _sharedCircleTexture;

        int size = CircleTexSize;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float center = size / 2f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = new Vector2(x - center + 0.5f, y - center + 0.5f).Length();
                if (dist <= radius)
                {
                    float alpha = dist <= radius - 1.5f ? 1.0f : Mathf.Clamp(radius - dist + 0.5f, 0f, 1f);
                    img.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else
                {
                    img.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }

        _sharedCircleTexture = ImageTexture.CreateFromImage(img);
        return _sharedCircleTexture;
    }

    // ========================================
    // 内部数据
    // ========================================

    /// <summary>单个 POI 的渲染节点集合</summary>
    private class POINodeSet
    {
        public Sprite2D? Marker;
        public Label? Label;
        public List<Polygon2D>? OverlayPolygons;
        public Color MarkerBaseColor;
    }
}
