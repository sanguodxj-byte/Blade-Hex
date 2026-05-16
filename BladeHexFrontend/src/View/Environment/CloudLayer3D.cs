// CloudLayer3D.cs
// 大地图云层 — CanvasLayer 方案，绑定世界坐标
// 云团扎堆分布，随风缓慢飘动，相机移动时云相对地面静止
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Environment;

/// <summary>
/// 云层组件 — CanvasLayer 上的多个云团 Sprite。
/// 
/// 设计：
/// - 云团是预生成的 Sprite2D，位置绑定世界坐标
/// - 每帧根据相机位置计算云团的屏幕坐标（视差滚动）
/// - 云团缓慢随风飘动（世界空间），相机移动时云相对地面静止
/// - 云团扎堆分布：用 Poisson disk 或随机聚类生成位置
/// </summary>
[GlobalClass]
public partial class CloudLayer3D : CanvasLayer
{
    // ========================================
    // 配置
    // ========================================

    [Export] public float CloudCoverage { get; set; } = 0.45f;
    [Export] public float CloudOpacity { get; set; } = 0.35f;
    [Export] public Color CloudColor { get; set; } = new(0.92f, 0.92f, 0.97f);
    [Export] public bool Enabled { get; set; } = true;

    // ========================================
    // 内部
    // ========================================

    private readonly List<CloudInstance> _clouds = new();
    private Texture2D? _cloudTexture;
    private float _worldWidthPx;
    private float _worldHeightPx;
    private RandomNumberGenerator _rng = new();

    // 外部注入：相机位置（像素坐标）和缩放
    private Vector2 _cameraPixelPos;
    private float _cameraZoom = 1.0f;
    private Vector2 _viewportSize = new(1920, 1080);

    private class CloudInstance
    {
        public Sprite2D Sprite = null!;
        public Vector2 WorldPos;    // 世界像素坐标
        public float Size;
        public float BaseAlpha;
    }

    // ========================================
    // 初始化
    // ========================================

    public void Initialize(float worldWidthPx, float worldHeightPx)
    {
        _worldWidthPx = worldWidthPx;
        _worldHeightPx = worldHeightPx;

        Layer = 5;
        FollowViewportEnabled = false;

        _rng.Seed = (ulong)(worldWidthPx * 7 + worldHeightPx * 13);
        _cloudTexture = GenerateCloudTexture();

        GenerateCloudClusters();

        GD.Print($"[CloudLayer3D] 初始化: {_clouds.Count} 个云团, world={worldWidthPx:F0}×{worldHeightPx:F0}px");
    }

    /// <summary>生成扎堆分布的云团</summary>
    private void GenerateCloudClusters()
    {
        // 先生成聚类中心（少量），再在每个中心周围撒云团
        int clusterCount = Mathf.Max(3, (int)(CloudCoverage * 12));
        int cloudsPerCluster = Mathf.Max(2, (int)(CloudCoverage * 6));

        for (int c = 0; c < clusterCount; c++)
        {
            // 聚类中心：随机分布在地图上
            float cx = _rng.RandfRange(_worldWidthPx * 0.05f, _worldWidthPx * 0.95f);
            float cy = _rng.RandfRange(_worldHeightPx * 0.05f, _worldHeightPx * 0.95f);
            float clusterRadius = _rng.RandfRange(2000, 5000); // 聚类半径（像素）

            int count = cloudsPerCluster + _rng.RandiRange(-1, 2);
            for (int i = 0; i < count; i++)
            {
                // 在聚类中心周围高斯分布
                float angle = _rng.Randf() * Mathf.Tau;
                float dist = _rng.Randf() * _rng.Randf() * clusterRadius; // 二次分布：中心密集
                float x = cx + Mathf.Cos(angle) * dist;
                float y = cy + Mathf.Sin(angle) * dist;

                // 边界检查
                x = Mathf.Clamp(x, -2000, _worldWidthPx + 2000);
                y = Mathf.Clamp(y, -2000, _worldHeightPx + 2000);

                float size = _rng.RandfRange(0.8f, 2.0f);
                float alpha = CloudOpacity * _rng.RandfRange(0.6f, 1.0f);

                var sprite = new Sprite2D();
                sprite.Texture = _cloudTexture;
                sprite.Scale = new Vector2(size, size);
                sprite.Modulate = new Color(CloudColor.R, CloudColor.G, CloudColor.B, alpha);
                sprite.Visible = false; // 初始隐藏，_Process 中根据视野显示
                AddChild(sprite);

                _clouds.Add(new CloudInstance
                {
                    Sprite = sprite,
                    WorldPos = new Vector2(x, y),
                    Size = size,
                    BaseAlpha = alpha,
                });
            }
        }
    }

    // ========================================
    // 每帧更新 — 世界坐标 → 屏幕坐标
    // ========================================

    public override void _Process(double delta)
    {
        if (!Enabled) return;

        // 视口大小
        var vp = GetViewport()?.GetVisibleRect().Size;
        if (vp.HasValue && vp.Value.X > 0)
            _viewportSize = vp.Value;

        // 计算可见范围（像素空间）
        float halfViewW = (_viewportSize.X * 0.5f) / _cameraZoom;
        float halfViewH = (_viewportSize.Y * 0.5f) / _cameraZoom;
        float margin = 300.0f; // 额外边距，让云在进入视野前就开始渲染

        float viewMinX = _cameraPixelPos.X - halfViewW - margin;
        float viewMaxX = _cameraPixelPos.X + halfViewW + margin;
        float viewMinY = _cameraPixelPos.Y - halfViewH - margin;
        float viewMaxY = _cameraPixelPos.Y + halfViewH + margin;

        foreach (var cloud in _clouds)
        {
            // 判断是否在视野内
            bool inView = cloud.WorldPos.X >= viewMinX && cloud.WorldPos.X <= viewMaxX &&
                          cloud.WorldPos.Y >= viewMinY && cloud.WorldPos.Y <= viewMaxY;

            cloud.Sprite.Visible = inView;
            if (!inView) continue;

            // 世界坐标 → 屏幕坐标
            float screenX = (cloud.WorldPos.X - _cameraPixelPos.X) * _cameraZoom + _viewportSize.X * 0.5f;
            float screenY = (cloud.WorldPos.Y - _cameraPixelPos.Y) * _cameraZoom + _viewportSize.Y * 0.5f;
            cloud.Sprite.Position = new Vector2(screenX, screenY);
            cloud.Sprite.Scale = new Vector2(cloud.Size * _cameraZoom, cloud.Size * _cameraZoom);
        }
    }

    // ========================================
    // 外部驱动 API
    // ========================================

    /// <summary>每帧由场景调用，同步相机状态</summary>
    public void UpdateCamera(Vector2 cameraPixelPos, float zoom)
    {
        _cameraPixelPos = cameraPixelPos;
        _cameraZoom = zoom;
    }

    /// <summary>风力驱动云团移动（由 WindSystem 调用）</summary>
    public void SetWind(Vector2 direction, float speed)
    {
        // 风力直接偏移所有云团的世界坐标（在 _Process 之外由 WindSystem 每帧调用）
        // 这里只存储参数，实际移动在 ApplyWind 中
        _windDirection = direction;
        _windSpeed = speed;
    }

    private Vector2 _windDirection = new(1, 0);
    private float _windSpeed = 0.4f;

    /// <summary>应用风力移动（由 WindSystem 每帧调用）</summary>
    public void ApplyWind(float dt)
    {
        if (!Enabled) return;

        Vector2 drift = _windDirection * _windSpeed * dt * 30.0f; // 30 像素/秒 at strength=1

        foreach (var cloud in _clouds)
        {
            cloud.WorldPos += drift;

            // 环绕边界
            if (cloud.WorldPos.X > _worldWidthPx + 3000) cloud.WorldPos.X = -3000;
            if (cloud.WorldPos.X < -3000) cloud.WorldPos.X = _worldWidthPx + 3000;
            if (cloud.WorldPos.Y > _worldHeightPx + 3000) cloud.WorldPos.Y = -3000;
            if (cloud.WorldPos.Y < -3000) cloud.WorldPos.Y = _worldHeightPx + 3000;
        }
    }

    // ========================================
    // 公共 API
    // ========================================

    public void SetCoverage(float coverage)
    {
        CloudCoverage = Mathf.Clamp(coverage, 0.0f, 1.0f);
        // 覆盖率变化时调整可见云数量（简单方案：隐藏部分云）
        int visibleCount = (int)(_clouds.Count * (CloudCoverage / 0.45f));
        for (int i = 0; i < _clouds.Count; i++)
            _clouds[i].Sprite.Visible = i < visibleCount;
    }

    public void SetOpacity(float opacity)
    {
        CloudOpacity = Mathf.Clamp(opacity, 0.0f, 1.0f);
        foreach (var cloud in _clouds)
        {
            float a = cloud.BaseAlpha * (opacity / 0.35f);
            cloud.Sprite.Modulate = new Color(CloudColor.R, CloudColor.G, CloudColor.B, Mathf.Clamp(a, 0, 1));
        }
    }

    public void SetCloudColor(Color color)
    {
        CloudColor = color;
        foreach (var cloud in _clouds)
            cloud.Sprite.Modulate = new Color(color.R, color.G, color.B, cloud.Sprite.Modulate.A);
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Visible = enabled;
    }

    // ========================================
    // 程序化云朵纹理
    // ========================================

    private static ImageTexture GenerateCloudTexture()
    {
        const int size = 256;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        float center = (size - 1) * 0.5f;

        var blobs = new (float x, float y, float r)[]
        {
            (0.0f, 0.0f, 0.48f),
            (-0.2f, 0.06f, 0.36f),
            (0.22f, -0.04f, 0.34f),
            (0.08f, 0.18f, 0.30f),
            (-0.14f, -0.12f, 0.28f),
            (0.28f, 0.12f, 0.24f),
            (-0.25f, 0.15f, 0.22f),
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - center) / center;
                float ny = (y - center) / center;

                float alpha = 0.0f;
                foreach (var (bx, by, br) in blobs)
                {
                    float dx = nx - bx;
                    float dy = ny - by;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / br;
                    float contribution = Mathf.Clamp(1.0f - dist, 0.0f, 1.0f);
                    contribution = contribution * contribution * (3.0f - 2.0f * contribution);
                    alpha = Mathf.Max(alpha, contribution * 0.65f);
                }

                img.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
