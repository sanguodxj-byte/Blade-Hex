// WeatherParticles3D.cs
// 3D 战斗场景天气 — 使用 2D 粒子覆盖层（已验证可用）+ 3D 积水平面
using Godot;
using System;

namespace BladeHex.View.Environment;

/// <summary>
/// 3D 战斗场景天气系统。
/// 粒子效果使用 CPUParticles2D 在 CanvasLayer 上渲染（和大地图相同方案，已验证可用）。
/// 积水使用 3D MeshInstance3D + StandardMaterial3D。
/// </summary>
[GlobalClass]
public partial class WeatherParticles3D : Node3D
{
    private CpuParticles2D? _rain;
    private CpuParticles2D? _snow;
    private CpuParticles2D? _sand;
    private MeshInstance3D? _puddle;
    private CanvasLayer? _layer;
    private StandardMaterial3D? _puddleMat;
    private WeatherType _active = WeatherType.Clear;
    private float _puddleAlpha;

    public override void _Ready()
    {
        // CanvasLayer 加到自身（Node3D 的子 CanvasLayer 会正确渲染在 3D 之上）
        _layer = new CanvasLayer { Layer = 2, Name = "WeatherOverlay3D" };
        _layer.FollowViewportEnabled = false;
        AddChild(_layer);

        // 使用固定的设计分辨率
        var vp = new Vector2(1920, 1080);

        _rain = MakeRain(vp);
        _snow = MakeSnow(vp);
        _sand = MakeSand(vp);
        _layer.AddChild(_rain);
        _layer.AddChild(_snow);
        _layer.AddChild(_sand);

        _puddle = MakePuddle();
        AddChild(_puddle);

        StopAll();
        GD.Print("[WeatherParticles3D] 初始化完成 (2D覆盖+3D积水)");
    }

    public override void _Process(double delta)
    {
        float target = _active == WeatherType.Rain ? 0.45f : 0.0f;
        _puddleAlpha = Mathf.MoveToward(_puddleAlpha, target, (float)delta * 0.08f);
        if (_puddleMat != null)
            _puddleMat.AlbedoColor = new Color(0.08f, 0.12f, 0.25f, _puddleAlpha);
        if (_puddle != null)
            _puddle.Visible = _puddleAlpha > 0.01f;
    }

    public void SetWeather(WeatherType weather, float intensity)
    {
        _active = weather;
        GD.Print($"[WeatherParticles3D] SetWeather: {weather}, intensity={intensity:F2}");
        _rain!.Emitting = weather == WeatherType.Rain;
        _snow!.Emitting = weather == WeatherType.Snow;
        _sand!.Emitting = weather == WeatherType.Sandstorm;
        if (weather == WeatherType.Rain) _rain.Amount = (int)(250 * intensity);
        else if (weather == WeatherType.Snow) _snow.Amount = (int)(150 * intensity);
        else if (weather == WeatherType.Sandstorm) _sand.Amount = (int)(200 * intensity);
    }

    public void StopAll()
    {
        _active = WeatherType.Clear;
        if (_rain != null) _rain.Emitting = false;
        if (_snow != null) _snow.Emitting = false;
        if (_sand != null) _sand.Emitting = false;
    }

    public override void _ExitTree()
    {
        // CanvasLayer 是子节点，会随父节点自动释放
    }

    // ========================================

    private static CpuParticles2D MakeRain(Vector2 vp)
    {
        var p = new CpuParticles2D { Name = "Rain", Amount = 250, Lifetime = 0.6f, Preprocess = 0.3f };
        p.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        p.EmissionRectExtents = new Vector2(vp.X / 2, 5);
        p.Position = new Vector2(vp.X / 2, -10);
        p.Direction = new Vector2(0.05f, 1);
        p.Spread = 5;
        p.InitialVelocityMin = 900;
        p.InitialVelocityMax = 1300;
        p.Gravity = new Vector2(20, 200);
        p.ScaleAmountMin = 1;
        p.ScaleAmountMax = 1.5f;
        p.Color = new Color(0.7f, 0.82f, 1, 0.45f);
        p.Texture = Tex(2, 12, new Color(0.8f, 0.88f, 1, 0.6f));
        p.Emitting = false;
        return p;
    }

    private static CpuParticles2D MakeSnow(Vector2 vp)
    {
        var p = new CpuParticles2D { Name = "Snow", Amount = 150, Lifetime = 5, Preprocess = 2 };
        p.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        p.EmissionRectExtents = new Vector2(vp.X / 2, 5);
        p.Position = new Vector2(vp.X / 2, -10);
        p.Direction = new Vector2(0.1f, 1);
        p.Spread = 30;
        p.InitialVelocityMin = 30;
        p.InitialVelocityMax = 80;
        p.Gravity = new Vector2(10, 20);
        p.ScaleAmountMin = 1.5f;
        p.ScaleAmountMax = 3;
        p.Color = new Color(1, 1, 1, 0.7f);
        p.Texture = Circle(4, new Color(1, 1, 1, 0.85f));
        p.Emitting = false;
        return p;
    }

    private static CpuParticles2D MakeSand(Vector2 vp)
    {
        var p = new CpuParticles2D { Name = "Sand", Amount = 200, Lifetime = 1.5f, Preprocess = 0.5f };
        p.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        p.EmissionRectExtents = new Vector2(5, vp.Y / 2);
        p.Position = new Vector2(-10, vp.Y / 2);
        p.Direction = new Vector2(1, 0.15f);
        p.Spread = 12;
        p.InitialVelocityMin = 400;
        p.InitialVelocityMax = 800;
        p.Gravity = new Vector2(0, 30);
        p.ScaleAmountMin = 0.5f;
        p.ScaleAmountMax = 1.5f;
        p.Color = new Color(0.85f, 0.72f, 0.45f, 0.45f);
        p.Texture = Tex(4, 2, new Color(0.9f, 0.78f, 0.5f, 0.6f));
        p.Emitting = false;
        return p;
    }

    private MeshInstance3D MakePuddle()
    {
        var m = new MeshInstance3D { Name = "Puddle" };
        var plane = new PlaneMesh { Size = new Vector2(25, 25) };
        m.Mesh = plane;
        _puddleMat = new StandardMaterial3D();
        _puddleMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _puddleMat.AlbedoColor = new Color(0.08f, 0.12f, 0.25f, 0);
        _puddleMat.Metallic = 0.3f;
        _puddleMat.Roughness = 0.05f;
        m.MaterialOverride = _puddleMat;
        m.Position = new Vector3(0, 0.02f, 0);
        m.Visible = false;
        return m;
    }

    private static Texture2D Tex(int w, int h, Color c)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(c);
        return ImageTexture.CreateFromImage(img);
    }

    private static Texture2D Circle(int s, Color c)
    {
        var img = Image.CreateEmpty(s, s, false, Image.Format.Rgba8);
        img.Fill(Colors.Transparent);
        float r = (s - 1) / 2.0f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                if ((x - r) * (x - r) + (y - r) * (y - r) <= r * r)
                    img.SetPixel(x, y, c);
        return ImageTexture.CreateFromImage(img);
    }
}
