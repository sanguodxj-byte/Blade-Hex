// MapLabelLayer.cs
// 多层级地图标签系统 — 根据摄像机缩放级别显示不同层级的地名
// 特性：LOD 淡入淡出、标签避让、动态字号、缩放补偿
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using BladeHex.Map;
using BladeHex.Data;

namespace BladeHex.View.Map;

/// <summary>
/// 地图标签层 — 根据摄像机缩放显示不同层级的地名标签
/// </summary>
[GlobalClass]
public partial class MapLabelLayer : Node2D
{
    // ========================================
    // 缩放阈值
    // ========================================

    private const float ZoomWorldName = 0.12f;
    private const float ZoomRegionNames = 0.22f;
    private const float ZoomNationNames = 0.38f;
    private const float ZoomPoiNames = 0.60f;

    // 淡入淡出范围（阈值 ± 这个值内做 alpha 过渡）
    private const float FadeRange = 0.03f;

    // ========================================
    // 标签数据
    // ========================================

    private readonly List<MapLabel> _allLabels = new();
    private Camera2D? _camera;
    private float _lastZoom = -1f;
    private int _worldTileW;
    private int _worldTileH;
    private int _worldSeed;

    // ========================================
    // 初始化
    // ========================================

    public void Initialize(
        Camera2D camera,
        Vector2 worldCenter,
        List<OverworldPOI> pois,
        Dictionary<string, NationTerritory>? territories,
        List<NationConfig>? nations,
        int worldTileW = 576,
        int worldTileH = 384,
        int worldSeed = 0)
    {
        _camera = camera;
        _worldTileW = worldTileW;
        _worldTileH = worldTileH;
        _worldSeed = worldSeed;
        Name = "MapLabelLayer";
        ZIndex = 55; // 在迷雾(50)之上

        // 世界名
        string worldName = GeographicNameGenerator.GenerateWorldName(_worldSeed);
        AddLabel(worldName, worldCenter, 80, new Color(0.95f, 0.85f, 0.6f), LabelTier.World);

        // 地理区域 + 海洋
        CreateRegionLabels();

        // 国家名
        if (territories != null && nations != null)
            CreateNationLabels(territories, nations);

        // POI 名
        CreatePoiLabels(pois);

        // 标签避让（静态，初始化时一次性计算）
        ResolveOverlaps();
    }

    // ========================================
    // 每帧更新
    // ========================================

    public override void _Process(double delta)
    {
        if (_camera == null) return;

        float zoom = _camera.Zoom.X;
        if (Mathf.Abs(zoom - _lastZoom) < 0.002f) return;
        _lastZoom = zoom;

        foreach (var label in _allLabels)
        {
            // 计算该标签在当前缩放下的目标 alpha
            float targetAlpha = GetTargetAlpha(label.Tier, zoom);

            // 平滑过渡 alpha
            float currentAlpha = label.Node.Modulate.A;
            float newAlpha = Mathf.Lerp(currentAlpha, targetAlpha, 0.15f);
            label.Node.Modulate = new Color(label.Node.Modulate.R, label.Node.Modulate.G, label.Node.Modulate.B, newAlpha);
            label.Node.Visible = newAlpha > 0.01f;

            // 缩放补偿 — 标签在屏幕上保持固定大小
            if (label.Node.Visible)
            {
                float s = (1.0f / zoom) * label.BaseScale;
                label.Node.Scale = new Vector2(s, s);
            }
        }
    }

    /// <summary>根据层级和缩放计算目标透明度（含淡入淡出）</summary>
    private static float GetTargetAlpha(LabelTier tier, float zoom)
    {
        float showMin, showMax;
        switch (tier)
        {
            case LabelTier.World:
                showMin = 0f; showMax = ZoomWorldName;
                break;
            case LabelTier.Region:
                showMin = ZoomWorldName; showMax = ZoomRegionNames;
                break;
            case LabelTier.Nation:
                showMin = ZoomRegionNames; showMax = ZoomNationNames;
                break;
            case LabelTier.Poi:
                showMin = ZoomNationNames; showMax = 2.0f; // 永远显示到最近
                break;
            default:
                return 0f;
        }

        // 在范围内完全可见
        if (zoom > showMin + FadeRange && zoom < showMax - FadeRange)
            return 1.0f;

        // 淡入（从 showMin 开始）
        if (zoom >= showMin && zoom <= showMin + FadeRange)
            return (zoom - showMin) / FadeRange;

        // 淡出（到 showMax 结束）
        if (zoom >= showMax - FadeRange && zoom <= showMax)
            return (showMax - zoom) / FadeRange;

        return 0f;
    }

    // ========================================
    // 标签创建
    // ========================================

    private void CreateRegionLabels()
    {
        int idx = 0;
        foreach (var region in RegionRegistry.Regions)
        {
            int tileQ = (int)(region.CenterQ * _worldTileW);
            int tileR = (int)(region.CenterR * _worldTileH);
            var pos = HexOverworldTile.AxialToPixel(tileQ, tileR);

            var dominantBiome = region.PreferredTerrains.Length > 0
                ? TerrainToBiome.Map(region.PreferredTerrains[0])
                : BiomeType.Plains;
            string name = GeographicNameGenerator.GenerateRegionName(dominantBiome, _worldSeed, idx);

            var color = region.DangerLevel > 0.5f
                ? new Color(0.9f, 0.5f, 0.4f)
                : new Color(0.7f, 0.85f, 0.65f);

            AddLabel(name, pos, 38, color, LabelTier.Region);
            idx++;
        }

        // 海洋
        var top = HexOverworldTile.AxialToPixel(_worldTileW / 2, 5);
        var bot = HexOverworldTile.AxialToPixel(_worldTileW / 2, _worldTileH - 5);
        var left = HexOverworldTile.AxialToPixel(5, _worldTileH / 2);
        var right = HexOverworldTile.AxialToPixel(_worldTileW - 5, _worldTileH / 2);

        var seaColor = new Color(0.4f, 0.6f, 0.9f);
        AddLabel(GeographicNameGenerator.GenerateOceanName(_worldSeed, 0), top, 30, seaColor, LabelTier.Region);
        AddLabel(GeographicNameGenerator.GenerateOceanName(_worldSeed, 1), bot, 30, seaColor, LabelTier.Region);
        AddLabel(GeographicNameGenerator.GenerateOceanName(_worldSeed, 2), left, 30, seaColor, LabelTier.Region);
        AddLabel(GeographicNameGenerator.GenerateOceanName(_worldSeed, 3), right, 30, seaColor, LabelTier.Region);
    }

    private void CreateNationLabels(
        Dictionary<string, NationTerritory> territories,
        List<NationConfig> nations)
    {
        foreach (var nation in nations)
        {
            if (!territories.TryGetValue(nation.Id, out var territory)) continue;
            if (territory.AllTiles.Count == 0) continue;

            var centroid = territory.CoreZone.Centroid;
            var pos = HexOverworldTile.AxialToPixel(centroid.X, centroid.Y);

            // 字号根据领土面积动态调整
            float areaRatio = (float)territory.AllTiles.Count / (_worldTileW * _worldTileH);
            int fontSize = nation.IsMajorNation
                ? 22 + (int)(areaRatio * 80) // 大国：22~30
                : 16 + (int)(areaRatio * 40); // 小国：16~20

            var color = nation.IsMajorNation
                ? new Color(0.95f, 0.9f, 0.7f)
                : new Color(0.8f, 0.7f, 0.6f);

            AddLabel(nation.DisplayName, pos, fontSize, color, LabelTier.Nation);
        }
    }

    private void CreatePoiLabels(List<OverworldPOI> pois)
    {
        foreach (var poi in pois)
        {
            if (poi.PoiTypeEnum != OverworldPOI.POIType.Town &&
                poi.PoiTypeEnum != OverworldPOI.POIType.Village &&
                poi.PoiTypeEnum != OverworldPOI.POIType.Castle)
                continue;

            var color = poi.PoiTypeEnum switch
            {
                OverworldPOI.POIType.Town => new Color(1.0f, 0.95f, 0.8f),
                OverworldPOI.POIType.Castle => new Color(0.9f, 0.8f, 0.5f),
                _ => new Color(0.8f, 0.85f, 0.8f),
            };

            int fontSize = poi.PoiTypeEnum == OverworldPOI.POIType.Village ? 11 : 14;
            float scale = poi.PoiTypeEnum == OverworldPOI.POIType.Village ? 0.8f : 1.0f;

            var labelPos = poi.Position + new Vector2(0, 55);
            AddLabel(poi.PoiName, labelPos, fontSize, color, LabelTier.Poi, scale);
        }
    }

    // ========================================
    // 标签避让（简易版 — 同层级标签不重叠）
    // ========================================

    /// <summary>
    /// 简易标签避让：同层级内，如果两个标签距离太近，偏移较小的那个。
    /// </summary>
    private void ResolveOverlaps()
    {
        var tiers = new[] { LabelTier.Region, LabelTier.Nation, LabelTier.Poi };

        foreach (var tier in tiers)
        {
            var labels = _allLabels.Where(l => l.Tier == tier && !l.IsBgRect).ToList();
            float minDist = tier switch
            {
                LabelTier.Region => 3000f,
                LabelTier.Nation => 2000f,
                LabelTier.Poi => 400f,
                _ => 1000f,
            };

            for (int i = 0; i < labels.Count; i++)
            {
                for (int j = i + 1; j < labels.Count; j++)
                {
                    float dist = labels[i].WorldPosition.DistanceTo(labels[j].WorldPosition);
                    if (dist < minDist && dist > 0.1f)
                    {
                        var smaller = labels[i].FontSize <= labels[j].FontSize ? labels[i] : labels[j];
                        var other = smaller == labels[i] ? labels[j] : labels[i];
                        var dir = (smaller.WorldPosition - other.WorldPosition).Normalized();
                        if (dir.Length() < 0.1f) dir = new Vector2(1, 0);
                        float push = (minDist - dist) * 0.6f;
                        smaller.Node.Position += dir * push;
                        smaller.WorldPosition += dir * push;
                    }
                }
            }
        }
    }

    // ========================================
    // 工具
    // ========================================

    private void AddLabel(string text, Vector2 position, int fontSize, Color color, LabelTier tier, float baseScale = 1.0f)
    {
        var label = new Label();

        // 大区域名和国家名用字间距拉伸（视觉上"铺开"覆盖区域）
        if (tier == LabelTier.World || tier == LabelTier.Region || tier == LabelTier.Nation)
        {
            label.Text = string.Join("\u2009", text.ToCharArray()); // thin space 分隔
        }
        else
        {
            label.Text = text;
        }

        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.GrowHorizontal = Control.GrowDirection.Both;
        label.Position = position;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        label.Modulate = new Color(1, 1, 1, 0); // 初始透明
        label.Visible = false;

        // 海洋名用斜体效果（通过轻微 skew 模拟）
        bool isOcean = tier == LabelTier.Region && (color.B > 0.7f);
        if (isOcean)
        {
            label.AddThemeColorOverride("font_color", new Color(color.R, color.G, color.B, 0.7f));
            // Godot Label 不支持原生斜体，用 skew 模拟
            label.Rotation = -0.05f; // 微微倾斜
        }

        AddChild(label);

        // 国家名加底色条（半透明色带）
        if (tier == LabelTier.Nation)
        {
            var bg = new ColorRect();
            bg.Color = new Color(color.R * 0.3f, color.G * 0.3f, color.B * 0.3f, 0.25f);
            bg.CustomMinimumSize = new Vector2(fontSize * text.Length * 1.8f, fontSize * 1.6f);
            bg.Position = position + new Vector2(-fontSize * text.Length * 0.9f, -fontSize * 0.3f);
            bg.MouseFilter = Control.MouseFilterEnum.Ignore;
            bg.Modulate = new Color(1, 1, 1, 0);
            bg.Visible = false;
            AddChild(bg);
            // 将 bg 存入标签列表以便同步 alpha
            _allLabels.Add(new MapLabel
            {
                Node = bg,
                WorldPosition = position,
                Tier = tier,
                FontSize = fontSize,
                BaseScale = baseScale,
                IsBgRect = true,
            });
        }

        _allLabels.Add(new MapLabel
        {
            Node = label,
            WorldPosition = position,
            Tier = tier,
            FontSize = fontSize,
            BaseScale = baseScale,
        });
    }

    // ========================================
    // 数据结构
    // ========================================

    private enum LabelTier { World, Region, Nation, Poi }

    private class MapLabel
    {
        public Control Node { get; set; } = null!;
        public Vector2 WorldPosition { get; set; }
        public LabelTier Tier { get; set; }
        public int FontSize { get; set; }
        public float BaseScale { get; set; } = 1.0f;
        public bool IsBgRect { get; set; } = false;
    }
}
