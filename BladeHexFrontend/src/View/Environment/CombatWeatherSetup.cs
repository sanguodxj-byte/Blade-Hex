// CombatWeatherSetup.cs
// 战斗场景天气粒子 — 静态发射器覆盖整个战场（相机已被限制在战场范围内，不会移出粒子覆盖区）
using Godot;

namespace BladeHex.View.Environment;

/// <summary>
/// 战斗场景天气工具。
/// 设计：粒子在世界空间，发射区域静态覆盖整个战场 + 边距；
/// 相机由 CameraBoundsClamp 限制在战场范围内，所以视野始终在粒子覆盖区。
/// </summary>
public static class CombatWeatherSetup
{
    public static void Setup(Node3D scene, int mapWidth, int mapHeight)
    {
        // 从 Autoload 读取当前天气（与大地图实时同步）
        var weatherMgr = BladeHex.Data.Globals.WeatherOrNull;
        if (weatherMgr == null) return;

        var weather = weatherMgr.GetActiveWeatherType();
        if (weather == WeatherType.Clear) return;

        if (weather == WeatherType.Sandstorm)
            SetupSandstorm(scene, mapWidth, mapHeight);
        else
            SetupRainOrSnow(scene, weather, mapWidth, mapHeight);

        GD.Print($"[CombatWeather] 天气粒子已创建: {weather}");
    }

    // ============================================================
    // 雨/雪 — 静态发射器，覆盖整个战场
    // ============================================================

    private static void SetupRainOrSnow(Node3D scene, WeatherType weather, int mapWidth, int mapHeight)
    {
        var (centerX, centerZ, halfX, halfZ) = ComputeBattlefieldEmissionRegion(mapWidth, mapHeight, 600f);

        var p = new GpuParticles3D();
        p.Name = "CombatWeatherFX";
        float areaFactor = (halfX * 2 * halfZ * 2) / (1500f * 1500f);
        p.Amount = weather == WeatherType.Snow
            ? Mathf.Clamp((int)(800 * areaFactor), 800, 5000)
            : Mathf.Clamp((int)(1200 * areaFactor), 1200, 8000);
        // 增大 lifetime 让粒子从高空落到地面（覆盖屏幕底部）
        p.Lifetime = weather == WeatherType.Snow ? 10.0f : 2.5f;
        p.Preprocess = weather == WeatherType.Snow ? 8.0f : 1.5f;
        p.LocalCoords = false;

        p.VisibilityAabb = new Aabb(
            new Vector3(-halfX - 500, -500, -halfZ - 1000),
            new Vector3(halfX * 2 + 1000, 3000, halfZ * 2 + 2000));

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        // Z 方向额外 +800：-45° 相机下屏幕底部对应世界 Z 更大的区域
        mat.EmissionBoxExtents = new Vector3(halfX, 200, halfZ + 800);

        if (weather == WeatherType.Rain)
        {
            mat.Direction = new Vector3(0.05f, -1, 0);
            mat.Spread = 5;
            mat.InitialVelocityMin = 800;
            mat.InitialVelocityMax = 1200;
            mat.Gravity = new Vector3(20, -1000, 0);
            mat.ScaleMin = 1.0f;
            mat.ScaleMax = 1.5f;
            mat.Color = new Color(0.7f, 0.82f, 1.0f, 0.55f);
        }
        else // Snow
        {
            mat.Direction = new Vector3(0, -1, 0);
            mat.Spread = 30;
            mat.InitialVelocityMin = 50;
            mat.InitialVelocityMax = 100;
            mat.Gravity = new Vector3(15, -50, 0);
            mat.ScaleMin = 0.5f;
            mat.ScaleMax = 1.5f;
            mat.Color = new Color(1, 1, 1, 0.85f);
            mat.TurbulenceEnabled = true;
            mat.TurbulenceNoiseStrength = 25;
            mat.TurbulenceNoiseScale = 2;
        }

        mat.AlphaCurve = CreateAlphaFadeCurve();
        p.ProcessMaterial = mat;

        var stdMat = CreateSoftParticleMaterial();
        var qm = new QuadMesh
        {
            Size = weather == WeatherType.Rain
                ? new Vector2(2.0f, 30.0f)
                : new Vector2(8.0f, 8.0f),
            Material = stdMat
        };
        p.DrawPass1 = qm;

        // 发射器位置：战场中心，Y=1500（远高于相机 Y=800，确保粒子覆盖整个屏幕含底部）
        p.GlobalPosition = new Vector3(centerX, 1500, centerZ);
        scene.AddChild(p);

        GD.Print($"[CombatWeather/RainSnow] type={weather} center=({centerX:F0},1500,{centerZ:F0}) amount={p.Amount}");
    }

    // ============================================================
    // 沙尘暴 — 横向气流（覆盖整个战场宽度） + 屏幕色调
    // ============================================================

    private static void SetupSandstorm(Node3D scene, int mapWidth, int mapHeight)
    {
        var (centerX, centerZ, halfX, halfZ) = ComputeBattlefieldEmissionRegion(mapWidth, mapHeight, 600f);

        // === 第 1 层：大块朦胧雾气 ===
        var fog = CreateSandstormLayer(
            name: "SandstormFog",
            amount: Mathf.Clamp((int)(80 * (halfZ / 800f)), 80, 300),
            lifetime: 4.0f,
            preprocess: 2.5f,
            // 发射区从战场左侧整条带，向右流动
            emissionExtents: new Vector3(50, 300, halfZ),
            direction: new Vector3(1, 0, 0),
            velocityMin: 200, velocityMax: 350,
            gravity: new Vector3(0, -3, 0),
            scaleMin: 8.0f, scaleMax: 16.0f,
            color: new Color(0.78f, 0.65f, 0.42f, 0.35f),
            turbulence: 60, turbulenceScale: 1.5f,
            quadSize: new Vector2(60.0f, 60.0f));
        // 位置：战场最左侧 - halfX，让粒子向右穿越整个战场
        fog.GlobalPosition = new Vector3(centerX - halfX, 300, centerZ);
        scene.AddChild(fog);

        // === 第 2 层：细密沙粒 ===
        var sand = CreateSandstormLayer(
            name: "SandstormGrains",
            amount: Mathf.Clamp((int)(600 * (halfZ / 800f)), 600, 2000),
            lifetime: 2.5f,
            preprocess: 1.2f,
            emissionExtents: new Vector3(50, 300, halfZ),
            direction: new Vector3(1, -0.1f, 0),
            velocityMin: 500, velocityMax: 800,
            gravity: new Vector3(0, -25, 0),
            scaleMin: 0.6f, scaleMax: 1.5f,
            color: new Color(0.92f, 0.78f, 0.48f, 0.6f),
            turbulence: 80, turbulenceScale: 3,
            quadSize: new Vector2(6.0f, 4.0f));
        sand.GlobalPosition = new Vector3(centerX - halfX, 200, centerZ);
        scene.AddChild(sand);

        // === 第 3 层：屏幕色调雾罩（CanvasLayer，永远不黏屏） ===
        var canvas = new CanvasLayer { Name = "SandstormTint", Layer = 5 };
        scene.AddChild(canvas);
        var rect = new ColorRect
        {
            Name = "TintRect",
            Color = new Color(0.78f, 0.62f, 0.38f, 0.18f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1, AnchorBottom = 1
        };
        canvas.AddChild(rect);

        GD.Print($"[CombatWeather/Sandstorm] center=({centerX:F0},_,{centerZ:F0}) extents=(50,300,{halfZ:F0})");
    }

    private static GpuParticles3D CreateSandstormLayer(
        string name, int amount, float lifetime, float preprocess,
        Vector3 emissionExtents, Vector3 direction,
        float velocityMin, float velocityMax, Vector3 gravity,
        float scaleMin, float scaleMax, Color color,
        float turbulence, float turbulenceScale, Vector2 quadSize)
    {
        var p = new GpuParticles3D
        {
            Name = name,
            Amount = amount,
            Lifetime = lifetime,
            Preprocess = preprocess,
            LocalCoords = false,
            VisibilityAabb = new Aabb(new Vector3(-2000, -1000, -2000), new Vector3(4000, 3000, 4000))
        };
        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = emissionExtents,
            Direction = direction,
            Spread = 12,
            InitialVelocityMin = velocityMin,
            InitialVelocityMax = velocityMax,
            Gravity = gravity,
            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            Color = color,
            TurbulenceEnabled = true,
            TurbulenceNoiseStrength = turbulence,
            TurbulenceNoiseScale = turbulenceScale,
            AlphaCurve = CreateAlphaFadeCurve()
        };
        p.ProcessMaterial = mat;
        p.DrawPass1 = new QuadMesh { Size = quadSize, Material = CreateSoftParticleMaterial() };
        return p;
    }

    // ============================================================
    // 共享工具
    // ============================================================

    /// <summary>计算战场实际世界尺寸 + 发射区域（中心 + 半宽半深）</summary>
    private static (float centerX, float centerZ, float halfX, float halfZ)
        ComputeBattlefieldEmissionRegion(int mapWidth, int mapHeight, float margin)
    {
        const float HexSize = 96.0f;
        const float xSpacing = HexSize * 1.5f;
        const float zSpacing = HexSize * 1.7320508f;

        float battlefieldWidth = mapWidth * xSpacing;
        float battlefieldDepth = mapHeight * zSpacing;
        return (
            centerX: battlefieldWidth * 0.5f,
            centerZ: battlefieldDepth * 0.5f,
            halfX: battlefieldWidth * 0.5f + margin,
            halfZ: battlefieldDepth * 0.5f + margin);
    }

    private static StandardMaterial3D CreateSoftParticleMaterial()
    {
        return new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale = true,
            AlbedoTexture = CreateSoftCircleTexture()
        };
    }

    private static CurveTexture CreateAlphaFadeCurve()
    {
        var c = new Curve();
        c.AddPoint(new Vector2(0.0f, 0.0f));
        c.AddPoint(new Vector2(0.15f, 1.0f));
        c.AddPoint(new Vector2(0.85f, 1.0f));
        c.AddPoint(new Vector2(1.0f, 0.0f));
        return new CurveTexture { Curve = c };
    }

    private static ImageTexture CreateSoftCircleTexture()
    {
        const int size = 64;
        const float center = 31.5f;
        const float maxRadius = 31.5f;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float t = Mathf.Clamp(Mathf.Sqrt(dx * dx + dy * dy) / maxRadius, 0f, 1f);
                float alpha = Mathf.Pow(1.0f - t, 2.0f);
                img.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}
