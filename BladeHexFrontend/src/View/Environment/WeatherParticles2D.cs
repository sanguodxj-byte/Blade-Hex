// WeatherParticles2D.cs
// 2D 大地图天气粒子 — 使用 CPUParticles2D（最大兼容性）
// 作为 CanvasLayer 子节点，固定在屏幕空间
using Godot;
using System;

namespace BladeHex.View.Environment;

/// <summary>
/// 2D 天气粒子系统 — CPUParticles2D 实现雨/雪/沙尘暴。
/// CPUParticles2D 兼容所有渲染器（Forward+/Mobile/Compatibility）。
/// </summary>
[GlobalClass]
public partial class WeatherParticles2D : CanvasLayer
{
    private CpuParticles2D? _rainParticles;
    private CpuParticles2D? _snowParticles;
    private CpuParticles2D? _sandParticles;

    private WeatherType _activeWeather = WeatherType.Clear;

    public override void _Ready()
    {
        Layer = 8; // 在游戏内容之上(0)，UI之下(10)
        FollowViewportEnabled = false; // 固定在屏幕空间

        _rainParticles = CreateRain();
        _snowParticles = CreateSnow();
        _sandParticles = CreateSand();

        AddChild(_rainParticles);
        AddChild(_snowParticles);
        AddChild(_sandParticles);

        _rainParticles.Emitting = false;
        _snowParticles.Emitting = false;
        _sandParticles.Emitting = false;

        GD.Print($"[WeatherParticles2D] 初始化完成 (CPUParticles2D)");
    }

    // ========================================
    // 每帧检查：父场景不可见时自动隐藏
    // ========================================

    public override void _Process(double delta)
    {
        var owScene = GetParent();
        // 父场景可见性检查：兼容 Node3D（OverworldScene3D）和 Node2D（旧版）
        bool parentVisible = owScene switch
        {
            Node3D n3 => n3.Visible,
            Node2D n2 => n2.Visible,
            _ => true, // 父级既非 Node3D 也非 Node2D 时默认显示
        };
        bool shouldShow = owScene != null && parentVisible && _activeWeather != WeatherType.Clear;
        Visible = shouldShow;
    }

    // ========================================
    // 公共 API
    // ========================================

    public void SetWeather(WeatherType weather, float intensity)
    {
        _activeWeather = weather;
        GD.Print($"[WeatherParticles2D] SetWeather: {weather}, intensity={intensity:F2}");

        _rainParticles!.Emitting = weather == WeatherType.Rain;
        _snowParticles!.Emitting = weather == WeatherType.Snow;
        _sandParticles!.Emitting = weather == WeatherType.Sandstorm;

        // 通过 Amount 控制密度
        if (weather == WeatherType.Rain)
            _rainParticles.Amount = (int)(200 * intensity);
        else if (weather == WeatherType.Snow)
            _snowParticles.Amount = (int)(150 * intensity);
        else if (weather == WeatherType.Sandstorm)
            _sandParticles.Amount = (int)(180 * intensity);
    }

    public void StopAll()
    {
        _activeWeather = WeatherType.Clear;
        if (_rainParticles != null) _rainParticles.Emitting = false;
        if (_snowParticles != null) _snowParticles.Emitting = false;
        if (_sandParticles != null) _sandParticles.Emitting = false;
    }

    // ========================================
    // 粒子创建
    // ========================================

    private static CpuParticles2D CreateRain()
    {
        var p = new CpuParticles2D();
        p.Name = "Rain";
        p.Amount = 200;
        p.Lifetime = 0.7f;
        p.Preprocess = 0.3f;
        p.SpeedScale = 1.0f;
        p.Explosiveness = 0.0f;
        p.Randomness = 0.2f;

        // 发射区域：屏幕顶部全宽
        p.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        p.EmissionRectExtents = new Vector2(960, 5); // 1920/2 宽
        p.Position = new Vector2(960, -10); // 屏幕顶部中心

        // 方向：向下略倾斜
        p.Direction = new Vector2(0.05f, 1.0f);
        p.Spread = 5.0f;
        p.InitialVelocityMin = 900.0f;
        p.InitialVelocityMax = 1300.0f;
        p.Gravity = new Vector2(20, 200);

        // 外观
        p.ScaleAmountMin = 1.0f;
        p.ScaleAmountMax = 1.5f;
        p.Color = new Color(0.7f, 0.82f, 1.0f, 0.45f);

        // 纹理：细长竖线
        p.Texture = CreateLineTexture(2, 12, new Color(0.8f, 0.88f, 1.0f, 0.6f));

        return p;
    }

    private static CpuParticles2D CreateSnow()
    {
        var p = new CpuParticles2D();
        p.Name = "Snow";
        p.Amount = 150;
        p.Lifetime = 5.0f;
        p.Preprocess = 2.0f;
        p.SpeedScale = 1.0f;
        p.Randomness = 0.5f;

        p.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        p.EmissionRectExtents = new Vector2(960, 5);
        p.Position = new Vector2(960, -10);

        p.Direction = new Vector2(0.1f, 1.0f);
        p.Spread = 30.0f;
        p.InitialVelocityMin = 30.0f;
        p.InitialVelocityMax = 80.0f;
        p.Gravity = new Vector2(10, 20);

        // 水平摇摆
        p.AngularVelocityMin = -30.0f;
        p.AngularVelocityMax = 30.0f;

        p.ScaleAmountMin = 1.5f;
        p.ScaleAmountMax = 3.0f;
        p.Color = new Color(1.0f, 1.0f, 1.0f, 0.7f);

        // 纹理：小圆点
        p.Texture = CreateCircleTexture(4, new Color(1, 1, 1, 0.85f));

        return p;
    }

    private static CpuParticles2D CreateSand()
    {
        var p = new CpuParticles2D();
        p.Name = "Sand";
        p.Amount = 180;
        p.Lifetime = 1.5f;
        p.Preprocess = 0.5f;
        p.SpeedScale = 1.0f;
        p.Randomness = 0.3f;

        // 从左侧发射
        p.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        p.EmissionRectExtents = new Vector2(5, 540); // 全高
        p.Position = new Vector2(-10, 540); // 左侧中心

        p.Direction = new Vector2(1.0f, 0.15f); // 水平为主
        p.Spread = 12.0f;
        p.InitialVelocityMin = 400.0f;
        p.InitialVelocityMax = 800.0f;
        p.Gravity = new Vector2(0, 30);

        p.ScaleAmountMin = 0.5f;
        p.ScaleAmountMax = 1.5f;
        p.Color = new Color(0.85f, 0.72f, 0.45f, 0.45f);

        // 纹理：小椭圆
        p.Texture = CreateLineTexture(4, 2, new Color(0.9f, 0.78f, 0.5f, 0.6f));

        return p;
    }

    // ========================================
    // 程序化纹理
    // ========================================

    private static Texture2D CreateLineTexture(int width, int height, Color color)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        img.Fill(color);
        return ImageTexture.CreateFromImage(img);
    }

    private static Texture2D CreateCircleTexture(int size, Color color)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        img.Fill(Colors.Transparent);
        float center = (size - 1) / 2.0f;
        float radiusSq = center * center;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                if (dx * dx + dy * dy <= radiusSq)
                    img.SetPixel(x, y, color);
            }
        return ImageTexture.CreateFromImage(img);
    }
}
