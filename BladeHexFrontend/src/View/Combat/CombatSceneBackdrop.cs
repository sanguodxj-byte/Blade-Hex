// CombatSceneBackdrop.cs
// 战斗场景背景占位符 — 上方天空 + 周围地面，两者颜色显著不同。
// 在没有真实地形/天空盒资源前，用作可视边界，避免相机平移到战场外时出现黑洞。
using Godot;

namespace BladeHex.View.Combat;

/// <summary>
/// 战斗场景占位背景。挂到场景根节点下，根据战场 AABB 自动放置：
/// - 「上面」：WorldEnvironment 的程序天空（冷蓝灰）
/// - 「侧面」：覆盖战场外围的大型地面平面（暖土黄）
/// 两者用不同的色温区分，让玩家在视角拉远 / 平移到战场边缘时仍有方向感。
/// </summary>
[GlobalClass]
public partial class CombatSceneBackdrop : Node3D
{
    // 颜色：上下用对比色温
    private static readonly Color SkyTopColor = new(0.45f, 0.55f, 0.72f);     // 冷蓝（高空）
    private static readonly Color SkyHorizonColor = new(0.78f, 0.80f, 0.78f); // 浅灰（地平线）
    private static readonly Color GroundColor = new(0.38f, 0.32f, 0.22f);    // 暖土黄（侧面/远景）
    private static readonly Color GroundEdgeTint = new(0.22f, 0.18f, 0.14f); // 远景压暗

    // 地面平面相对战场 AABB 的扩展倍数（相机平移外延约 6 hex；这里给 20 倍以确保任何缩放下都看不到边）
    private const float GroundExtendMultiplier = 20f;

    private MeshInstance3D? _groundMesh;
    private WorldEnvironment? _worldEnv;

    /// <summary>根据战场 AABB 配置背景几何（首次或战场重生成时调用）</summary>
    public void Configure(Aabb battlefieldBounds)
    {
        EnsureSky();
        EnsureGround(battlefieldBounds);
    }

    private void EnsureSky()
    {
        if (_worldEnv != null) return;

        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = SkyTopColor,
            SkyHorizonColor = SkyHorizonColor,
            GroundBottomColor = GroundEdgeTint,
            GroundHorizonColor = GroundColor,
            SunAngleMax = 30f,
            SunCurve = 0.15f,
        };
        var skyRes = new Sky { SkyMaterial = sky };

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = skyRes,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 0.6f,
            AmbientLightEnergy = 0.8f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,

            // === SSAO(屏幕空间环境光遮蔽)— 让凹陷/角落自然变暗 ===
            SsaoEnabled = true,
            SsaoRadius = 2.0f,          // 遮蔽半径(世界单位)
            SsaoIntensity = 2.0f,       // 遮蔽强度
            SsaoPower = 1.5f,           // 衰减曲线
            SsaoLightAffect = 0.3f,     // 对直接光的影响(0=只影响环境光)

            // === SSIL(屏幕空间间接光)— 让亮面反弹光到暗面 ===
            SsilEnabled = true,
            SsilRadius = 5.0f,
            SsilIntensity = 1.0f,
            SsilNormalRejection = 1.0f,

            // === Glow(泛光)— 让高亮物体(魔法阵/伤害数字)产生光晕 ===
            GlowEnabled = true,
            GlowIntensity = 1.0f,
            GlowStrength = 0.8f,
            GlowBloom = 0.05f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
            GlowHdrThreshold = 0.9f,

            // === 色调映射 ===
            TonemapExposure = 0.9f,     // 略微压暗整体(配合 SSAO 更有层次)
        };

        _worldEnv = new WorldEnvironment { Environment = env, Name = "BackdropEnvironment" };
        AddChild(_worldEnv);
    }

    private void EnsureGround(Aabb battlefieldBounds)
    {
        // 计算覆盖范围 — 取战场 AABB 中心，向外扩展 GroundExtendMultiplier 倍
        var center = battlefieldBounds.Position + battlefieldBounds.Size * 0.5f;
        float extentX = Mathf.Max(battlefieldBounds.Size.X, battlefieldBounds.Size.Z) * GroundExtendMultiplier;
        float extentZ = extentX;

        if (_groundMesh == null)
        {
            var plane = new PlaneMesh
            {
                Size = new Vector2(extentX, extentZ),
                SubdivideWidth = 0,
                SubdivideDepth = 0,
            };

            var mat = new StandardMaterial3D
            {
                AlbedoColor = GroundColor,
                Roughness = 0.95f,
                Metallic = 0f,
                // 用顶点距离做径向暗化效果不可行（无 UV 渐变），改为 vertex_color；
                // 这里保持纯色，靠 SkyHorizonColor 与 GroundColor 的对比形成"上下差异"。
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            plane.Material = mat;

            _groundMesh = new MeshInstance3D
            {
                Mesh = plane,
                Name = "BackdropGround",
                // 略低于战场地面（战场 hex 在 Y=0 渲染），避免 z-fighting
                Position = new Vector3(center.X, -2f, center.Z),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_groundMesh);
        }
        else
        {
            if (_groundMesh.Mesh is PlaneMesh plane)
                plane.Size = new Vector2(extentX, extentZ);
            _groundMesh.Position = new Vector3(center.X, -2f, center.Z);
        }
    }
}
