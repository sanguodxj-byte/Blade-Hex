// WeatherParticles3D.cs
// 3D 天气粒子系统 — GPUParticles3D 实现雨/雪/沙尘暴
// 跟随相机位置，在玩家头顶生成粒子
using Godot;

namespace BladeHex.View.Environment;

/// <summary>
/// 3D 天气粒子系统 — GPUParticles3D 方案。
/// 比 CpuParticles2D CanvasLayer 方案更自然：粒子在 3D 世界中，
/// 有深度感，与光照交互。
/// </summary>
[GlobalClass]
public partial class WeatherParticles3D : Node3D
{
    private GpuParticles3D? _rain;
    private GpuParticles3D? _snow;
    private GpuParticles3D? _sand;

    private WeatherType _activeWeather = WeatherType.Clear;

    // 粒子发射区域大小（跟随相机覆盖可见区域）
    private const float EmitAreaWidth = 30.0f;
    private const float EmitAreaDepth = 20.0f;
    private const float EmitHeight = 12.0f;

    public override void _Ready()
    {
        _rain = CreateRainParticles();
        _snow = CreateSnowParticles();
        _sand = CreateSandParticles();

        AddChild(_rain);
        AddChild(_snow);
        AddChild(_sand);

        _rain.Emitting = false;
        _snow.Emitting = false;
        _sand.Emitting = false;
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>设置天气类型和强度</summary>
    public void SetWeather(WeatherType weather, float intensity)
    {
        _activeWeather = weather;

        _rain!.Emitting = weather == WeatherType.Rain;
        _snow!.Emitting = weather == WeatherType.Snow;
        _sand!.Emitting = weather == WeatherType.Sandstorm;

        // 通过 Amount 控制密度
        if (weather == WeatherType.Rain)
            _rain.Amount = (int)(800 * intensity);
        else if (weather == WeatherType.Snow)
            _snow.Amount = (int)(400 * intensity);
        else if (weather == WeatherType.Sandstorm)
            _sand.Amount = (int)(600 * intensity);
    }

    /// <summary>停止所有粒子</summary>
    public void StopAll()
    {
        _activeWeather = WeatherType.Clear;
        if (_rain != null) _rain.Emitting = false;
        if (_snow != null) _snow.Emitting = false;
        if (_sand != null) _sand.Emitting = false;
    }

    /// <summary>更新粒子位置（跟随相机/玩家）</summary>
    public void UpdatePosition(Vector3 followTarget)
    {
        Position = followTarget + new Vector3(0, EmitHeight, 0);
    }

    // ========================================
    // 粒子创建
    // ========================================

    private GpuParticles3D CreateRainParticles()
    {
        var particles = new GpuParticles3D();
        particles.Name = "Rain3D";
        particles.Amount = 800;
        particles.Lifetime = 0.8f;
        particles.Preprocess = 0.5f;
        particles.SpeedScale = 1.0f;
        particles.VisibilityAabb = new Aabb(
            new Vector3(-EmitAreaWidth, -EmitHeight * 2, -EmitAreaDepth),
            new Vector3(EmitAreaWidth * 2, EmitHeight * 3, EmitAreaDepth * 2));

        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(0.05f, -1.0f, 0.02f);
        mat.Spread = 3.0f;
        mat.InitialVelocityMin = 25.0f;
        mat.InitialVelocityMax = 35.0f;
        mat.Gravity = new Vector3(0.5f, -15.0f, 0);
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        mat.EmissionBoxExtents = new Vector3(EmitAreaWidth * 0.5f, 0.2f, EmitAreaDepth * 0.5f);

        // 缩放：细长条
        mat.ScaleMin = 0.8f;
        mat.ScaleMax = 1.2f;

        particles.ProcessMaterial = mat;

        // 绘制材质：细长白色条
        var drawMat = new StandardMaterial3D();
        drawMat.AlbedoColor = new Color(0.75f, 0.82f, 0.95f, 0.5f);
        drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        drawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        drawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;

        var mesh = new QuadMesh();
        mesh.Size = new Vector2(0.02f, 0.25f);
        mesh.Material = drawMat;
        particles.DrawPass1 = mesh;

        return particles;
    }

    private GpuParticles3D CreateSnowParticles()
    {
        var particles = new GpuParticles3D();
        particles.Name = "Snow3D";
        particles.Amount = 400;
        particles.Lifetime = 5.0f;
        particles.Preprocess = 2.0f;
        particles.SpeedScale = 1.0f;
        particles.VisibilityAabb = new Aabb(
            new Vector3(-EmitAreaWidth, -EmitHeight * 2, -EmitAreaDepth),
            new Vector3(EmitAreaWidth * 2, EmitHeight * 3, EmitAreaDepth * 2));

        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(0.1f, -1.0f, 0.05f);
        mat.Spread = 25.0f;
        mat.InitialVelocityMin = 1.0f;
        mat.InitialVelocityMax = 3.0f;
        mat.Gravity = new Vector3(0.3f, -1.5f, 0);
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        mat.EmissionBoxExtents = new Vector3(EmitAreaWidth * 0.5f, 0.5f, EmitAreaDepth * 0.5f);
        mat.AngularVelocityMin = -45.0f;
        mat.AngularVelocityMax = 45.0f;
        mat.ScaleMin = 0.8f;
        mat.ScaleMax = 2.0f;

        particles.ProcessMaterial = mat;

        var drawMat = new StandardMaterial3D();
        drawMat.AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.8f);
        drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        drawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        drawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;

        var mesh = new QuadMesh();
        mesh.Size = new Vector2(0.08f, 0.08f);
        mesh.Material = drawMat;
        particles.DrawPass1 = mesh;

        return particles;
    }

    private GpuParticles3D CreateSandParticles()
    {
        var particles = new GpuParticles3D();
        particles.Name = "Sand3D";
        particles.Amount = 600;
        particles.Lifetime = 2.0f;
        particles.Preprocess = 0.5f;
        particles.SpeedScale = 1.0f;
        particles.VisibilityAabb = new Aabb(
            new Vector3(-EmitAreaWidth * 2, -EmitHeight, -EmitAreaDepth),
            new Vector3(EmitAreaWidth * 4, EmitHeight * 2, EmitAreaDepth * 2));

        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(1.0f, -0.1f, 0.2f); // 水平为主
        mat.Spread = 15.0f;
        mat.InitialVelocityMin = 8.0f;
        mat.InitialVelocityMax = 18.0f;
        mat.Gravity = new Vector3(0, -0.5f, 0);
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        mat.EmissionBoxExtents = new Vector3(2.0f, EmitHeight * 0.4f, EmitAreaDepth * 0.5f);
        mat.ScaleMin = 0.3f;
        mat.ScaleMax = 1.0f;

        particles.ProcessMaterial = mat;

        var drawMat = new StandardMaterial3D();
        drawMat.AlbedoColor = new Color(0.85f, 0.72f, 0.45f, 0.4f);
        drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        drawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        drawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;

        var mesh = new QuadMesh();
        mesh.Size = new Vector2(0.06f, 0.03f);
        mesh.Material = drawMat;
        particles.DrawPass1 = mesh;

        return particles;
    }
}
