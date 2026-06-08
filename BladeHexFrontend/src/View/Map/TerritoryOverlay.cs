// TerritoryOverlay.cs
// 国家领土覆盖层 — 缩小地图时显示半透明国家色块 + 国名标签
// 使用 CanvasLayer（避免 3D 深度排序问题），根据相机缩放级别淡入淡出
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.Strategic.Kingdom;

namespace BladeHex.View.Map;

/// <summary>
/// 国家领土覆盖层 — 缩小到宏观视角时显示。
/// 
/// 工作原理：
/// - 每个国家有一个预渲染的 Image（对应其领土形状）
/// - 用 Sprite2D 显示在 CanvasLayer 上
/// - 相机缩放超过阈值时淡入，缩放回去时淡出
/// - 国家名称用 Label 显示在领土中心
/// </summary>
[GlobalClass]
public partial class TerritoryOverlay : CanvasLayer
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>开始显示的缩放阈值（正交 Size 超过此值时淡入）</summary>
    [Export] public float ShowZoomThreshold { get; set; } = 25.0f;

    /// <summary>完全显示的缩放阈值</summary>
    [Export] public float FullZoomThreshold { get; set; } = 40.0f;

    /// <summary>领土色块不透明度</summary>
    [Export] public float TerritoryAlpha { get; set; } = 0.25f;

    // ========================================
    // 内部
    // ========================================

    private readonly List<NationVisual> _nationVisuals = new();
    private float _currentAlpha;
    private float _worldWidthPx;
    private float _worldHeightPx;
    private Vector2 _cameraPixelPos;
    private float _cameraZoom = 1.0f;
    private Vector2 _viewportSize = new(1920, 1080);
    private bool _initialized;

    // 国家颜色表（固定分配，不重复）
    private static readonly Color[] NationColors = [
        new(0.2f, 0.5f, 0.8f),   // 蓝
        new(0.8f, 0.3f, 0.3f),   // 红
        new(0.3f, 0.7f, 0.3f),   // 绿
        new(0.7f, 0.5f, 0.2f),   // 橙
        new(0.6f, 0.3f, 0.7f),   // 紫
        new(0.2f, 0.7f, 0.7f),   // 青
        new(0.8f, 0.7f, 0.2f),   // 黄
        new(0.5f, 0.5f, 0.5f),   // 灰
    ];

    private class NationVisual
    {
        public string NationId = "";
        public string DisplayName = "";
        public Color NationColor;
        public Vector2 CenterPixel;   // 领土中心（像素坐标）
        public float RotationAngle;   // 根据领土形状计算的旋转角
        public Sprite2D? TerritorySprite;
        public Label3D? NameLabel3D;  // 3D 标签，固定在世界坐标
    }

    // ========================================
    // 初始化
    // ========================================

    public void Initialize(
        Dictionary<string, NationTerritory> territories,
        List<NationConfig> nations,
        float worldWidthPx, float worldHeightPx,
        Node3D? sceneRoot = null,
        PlayerKingdom? playerKingdom = null)
    {
        _worldWidthPx = worldWidthPx;
        _worldHeightPx = worldHeightPx;

        Layer = 4; // 在 3D 之上，云层(5)之下
        FollowViewportEnabled = false;

        int colorIdx = 0;
        foreach (var nation in nations)
        {
            if (!territories.TryGetValue(nation.Id, out var territory)) continue;
            if (territory.TotalTiles < 10) continue;

            // M7: 玩家王国使用自定义旗帜色
            Color color;
            if (playerKingdom != null && nation.Id == playerKingdom.KingdomId)
            {
                color = playerKingdom.BannerColor;
            }
            else
            {
                color = NationColors[colorIdx % NationColors.Length];
                colorIdx++;
            }

            // 计算领土中心
            var centroid = territory.CoreZone?.Centroid ?? Vector2I.Zero;
            var centerPx = HexOverworldTile.AxialToPixel(centroid.X, centroid.Y);

            // 计算领土主轴方向（用于旋转标签）
            float rotation = ComputeTerritoryRotation(territory);

            // 创建领土色块
            var sprite = new Sprite2D();
            sprite.Texture = GenerateTerritoryTexture(territory, color, worldWidthPx, worldHeightPx);
            sprite.Modulate = new Color(1, 1, 1, 0);
            sprite.Centered = false;
            AddChild(sprite);

            // 国名标签 — Label3D 固定在世界坐标，不随镜头缩放
            Label3D? label3D = null;
            if (sceneRoot != null)
            {
                label3D = new Label3D();
                label3D.Text = nation.DisplayName;
                label3D.FontSize = 280;
                label3D.Modulate = new Color(color.R, color.G, color.B, 0);
                label3D.OutlineModulate = new Color(0, 0, 0, 0.6f);
                label3D.OutlineSize = 12;
                label3D.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled; // 不面向相机
                label3D.FixedSize = false; // 随距离缩小（固定在世界中）
                label3D.NoDepthTest = true;
                label3D.RenderPriority = 20;

                // 固定位置：领土中心的世界坐标
                var worldPos = CoordConverter.PixelToWorld3D(centerPx);
                label3D.Position = new Vector3(worldPos.X, 1.5f, worldPos.Z);

                // 旋转：平躺在地面上（-90° X）+ 领土主轴旋转（Y 轴）
                label3D.RotationDegrees = new Vector3(-90, Mathf.RadToDeg(rotation), 0);

                sceneRoot.AddChild(label3D);
            }

            _nationVisuals.Add(new NationVisual
            {
                NationId = nation.Id,
                DisplayName = nation.DisplayName,
                NationColor = color,
                CenterPixel = centerPx,
                RotationAngle = rotation,
                TerritorySprite = sprite,
                NameLabel3D = label3D,
            });
        }

        _initialized = true;
        GD.Print($"[TerritoryOverlay] 初始化: {_nationVisuals.Count} 个国家领土");
    }

    // ========================================
    // 每帧更新
    // ========================================

    public override void _Process(double delta)
    {
        if (!_initialized) return;

        var vp = GetViewport()?.GetVisibleRect().Size;
        if (vp.HasValue && vp.Value.X > 0)
            _viewportSize = vp.Value;

        // 计算当前透明度（基于缩放级别）
        float targetAlpha = 0.0f;
        float orthoSize = _cameraZoom > 0 ? 8.0f / _cameraZoom : 8.0f;

        if (orthoSize >= FullZoomThreshold)
            targetAlpha = TerritoryAlpha;
        else if (orthoSize >= ShowZoomThreshold)
            targetAlpha = TerritoryAlpha * (orthoSize - ShowZoomThreshold) / (FullZoomThreshold - ShowZoomThreshold);

        _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, (float)delta * 4.0f);

        bool shouldShow = _currentAlpha > 0.01f;

        foreach (var nv in _nationVisuals)
        {
            // 领土色块
            if (nv.TerritorySprite != null)
            {
                nv.TerritorySprite.Visible = shouldShow;
                if (shouldShow)
                {
                    // 世界坐标 → 屏幕坐标
                    float screenX = (0 - _cameraPixelPos.X) * _cameraZoom + _viewportSize.X * 0.5f;
                    float screenY = (0 - _cameraPixelPos.Y) * _cameraZoom + _viewportSize.Y * 0.5f;
                    nv.TerritorySprite.Position = new Vector2(screenX, screenY);
                    nv.TerritorySprite.Scale = new Vector2(_cameraZoom, _cameraZoom);
                    nv.TerritorySprite.Modulate = new Color(1, 1, 1, _currentAlpha);
                }
            }

            // 国名标签（Label3D — 固定在世界坐标，只控制可见性）
            if (nv.NameLabel3D != null)
            {
                nv.NameLabel3D.Visible = shouldShow;
                if (shouldShow)
                    nv.NameLabel3D.Modulate = new Color(
                        nv.NationColor.R, nv.NationColor.G, nv.NationColor.B,
                        Mathf.Min(_currentAlpha * 3.0f, 0.8f));
            }
        }
    }

    // ========================================
    // 外部驱动
    // ========================================

    /// <summary>每帧由场景调用同步相机状态</summary>
    public void UpdateCamera(Vector2 cameraPixelPos, float zoom)
    {
        _cameraPixelPos = cameraPixelPos;
        _cameraZoom = zoom;
    }

    // ========================================
    // 领土形状分析
    // ========================================

    /// <summary>计算领土主轴方向（用 PCA 近似：找最长跨度方向）</summary>
    private static float ComputeTerritoryRotation(NationTerritory territory)
    {
        if (territory.TotalTiles < 5) return 0f;

        // 采样领土边界点，计算主轴
        var tiles = territory.AllTiles;
        float sumX = 0, sumY = 0;
        int count = 0;

        foreach (var coord in tiles)
        {
            var px = HexOverworldTile.AxialToPixel(coord.X, coord.Y);
            sumX += px.X;
            sumY += px.Y;
            count++;
            if (count > 200) break; // 采样上限
        }

        if (count == 0) return 0f;
        float cx = sumX / count, cy = sumY / count;

        // 计算协方差矩阵的主特征向量（简化 PCA）
        float cxx = 0, cxy = 0, cyy = 0;
        count = 0;
        foreach (var coord in tiles)
        {
            var px = HexOverworldTile.AxialToPixel(coord.X, coord.Y);
            float dx = px.X - cx, dy = px.Y - cy;
            cxx += dx * dx;
            cxy += dx * dy;
            cyy += dy * dy;
            count++;
            if (count > 200) break;
        }

        // 主轴角度 = 0.5 * atan2(2*cxy, cxx - cyy)
        float angle = 0.5f * Mathf.Atan2(2 * cxy, cxx - cyy);
        return angle;
    }

    // ========================================
    // 领土纹理生成
    // ========================================

    /// <summary>生成国家领土的低分辨率色块纹理</summary>
    private static ImageTexture GenerateTerritoryTexture(
        NationTerritory territory, Color color,
        float worldWidthPx, float worldHeightPx)
    {
        // 低分辨率：每 4 像素一个采样点（性能）
        const int scale = 8; // 1 像素 = 8 世界像素
        int texW = (int)(worldWidthPx / scale);
        int texH = (int)(worldHeightPx / scale);
        texW = Mathf.Min(texW, 512); // 上限
        texH = Mathf.Min(texH, 384);

        var img = Image.CreateEmpty(texW, texH, false, Image.Format.Rgba8);
        img.Fill(Colors.Transparent);

        float scaleX = worldWidthPx / texW;
        float scaleY = worldHeightPx / texH;

        // 将领土 tile 坐标映射到纹理像素
        foreach (var tileCoord in territory.AllTiles)
        {
            var pixelPos = HexOverworldTile.AxialToPixel(tileCoord.X, tileCoord.Y);
            int px = (int)(pixelPos.X / scaleX);
            int py = (int)(pixelPos.Y / scaleY);

            // 画一个小圆（hex 大小 ~2 纹理像素）
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int fx = px + dx, fy = py + dy;
                    if (fx >= 0 && fx < texW && fy >= 0 && fy < texH)
                        img.SetPixel(fx, fy, color);
                }
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
