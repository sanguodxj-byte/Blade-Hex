// WorldTemplates.cs
// 世界生成模板系统 — 预设的地形宏观结构，每种产生独特的"世界形状"。
// 模板修改 elev、coastline 形状、山脉分布、内海等宏观特征，
// 产生有性格的世界（地中海、群岛、环火山等）。
using Godot;

namespace BladeHex.Map;

/// <summary>世界模板枚举</summary>
public enum WorldTemplate
{
    Continental,      // 默认：单一大陆中心（"面包"）
    Archipelago,      // 群岛：分散小岛
    Mediterranean,    // 地中海：陆地环绕中央内海
    RingOfFire,       // 环火山：环形山脉围绕中心
    Maelstrom,        // 大漩涡：中心深渊海，外围环形大陆
    Pangaea,          // 泛大陆：单块巨大陆，少海岸
    TwinContinents,   // 双大陆：两块相对大陆隔海相望
    InlandSea,        // 内陆海：单一大湖被山脉环绕
    HighlandFortress, // 高地堡垒：中央高山高原，四周低地
    BrokenIsles,      // 破碎诸岛：大量小型陆地碎片
}

/// <summary>
/// 世界模板参数 — 由模板生成的归一化坐标 [-1,1] 计算修正值。
/// 修正值会与 ChunkGenerator 的基础 falloff/elev 复合。
/// </summary>
public struct TemplateParams
{
    /// <summary>边缘衰减乘数（0=强制水域, 1=完全保留陆地）</summary>
    public float FalloffMul;

    /// <summary>高程偏移（-0.5 到 +0.5）</summary>
    public float ElevBias;

    /// <summary>山脉强度乘数（0=平地, 2=放大山脊）</summary>
    public float RidgeMul;

    public static TemplateParams Default => new()
    {
        FalloffMul = 1.0f,
        ElevBias = 0.0f,
        RidgeMul = 1.0f,
    };
}

/// <summary>
/// 模板生成器 — 静态函数，输入归一化坐标 [-1,1]，输出修正参数。
/// </summary>
public static class WorldTemplateGenerator
{
    /// <summary>从种子选一个模板（带权重）</summary>
    public static WorldTemplate PickFromSeed(int seed)
    {
        // 把权重直接编码：常见模板权重高，奇异模板权重低
        var pool = new (WorldTemplate t, int w)[]
        {
            (WorldTemplate.Continental, 22),
            (WorldTemplate.Archipelago, 6),     // 水多，权重降低
            (WorldTemplate.Mediterranean, 12),
            (WorldTemplate.RingOfFire, 10),
            (WorldTemplate.Maelstrom, 4),        // 水多，权重降低
            (WorldTemplate.Pangaea, 14),         // 陆多，权重提高
            (WorldTemplate.TwinContinents, 12),
            (WorldTemplate.InlandSea, 12),
            (WorldTemplate.HighlandFortress, 10),
            (WorldTemplate.BrokenIsles, 4),      // 水极多，权重降低
        };
        int total = 0;
        foreach (var (_, w) in pool) total += w;
        // 用 seed 的某些位作为随机数
        int roll = (int)((uint)(seed ^ (seed >> 13)) % (uint)total);
        int acc = 0;
        foreach (var (t, w) in pool)
        {
            acc += w;
            if (roll < acc) return t;
        }
        return WorldTemplate.Continental;
    }

    /// <summary>
    /// 根据模板和归一化坐标 (nx, ny) ∈ [-1, 1] 计算修正参数。
    /// </summary>
    public static TemplateParams Sample(WorldTemplate template, float nx, float ny, int seed)
    {
        return template switch
        {
            WorldTemplate.Continental => SampleContinental(nx, ny),
            WorldTemplate.Archipelago => SampleArchipelago(nx, ny, seed),
            WorldTemplate.Mediterranean => SampleMediterranean(nx, ny),
            WorldTemplate.RingOfFire => SampleRingOfFire(nx, ny),
            WorldTemplate.Maelstrom => SampleMaelstrom(nx, ny),
            WorldTemplate.Pangaea => SamplePangaea(nx, ny),
            WorldTemplate.TwinContinents => SampleTwinContinents(nx, ny),
            WorldTemplate.InlandSea => SampleInlandSea(nx, ny),
            WorldTemplate.HighlandFortress => SampleHighlandFortress(nx, ny),
            WorldTemplate.BrokenIsles => SampleBrokenIsles(nx, ny, seed),
            _ => TemplateParams.Default,
        };
    }

    // ─────────────────────────────────────────────
    // 各模板实现
    // ─────────────────────────────────────────────

    /// <summary>大陆：默认行为，无修正</summary>
    private static TemplateParams SampleContinental(float nx, float ny) => TemplateParams.Default;

    /// <summary>群岛：用周期性正弦切割大陆为多个小岛</summary>
    private static TemplateParams SampleArchipelago(float nx, float ny, int seed)
    {
        // 用两个不同方向的正弦交叉，谷底是水，峰顶是岛
        float islandPattern =
            Mathf.Sin(nx * 6.0f + seed * 0.1f) * Mathf.Sin(ny * 6.0f + seed * 0.07f) * 0.5f
            + Mathf.Sin(nx * 4.0f - ny * 4.0f) * 0.3f;
        // 偏向负值时降低 falloff，让岛之间是海
        float falloffMul = islandPattern > 0.0f ? 1.0f : Mathf.Max(0.2f, 1.0f + islandPattern * 1.5f);
        return new TemplateParams { FalloffMul = falloffMul, ElevBias = 0.0f, RidgeMul = 0.7f };
    }

    /// <summary>地中海：陆地环绕中央内海</summary>
    private static TemplateParams SampleMediterranean(float nx, float ny)
    {
        float dist = Mathf.Sqrt(nx * nx + ny * ny * 1.4f); // 椭圆扁
        float falloffMul;
        if (dist < 0.35f)
        {
            // 中央内海强制水
            falloffMul = 0.0f;
        }
        else if (dist < 0.45f)
        {
            // 渐变到陆地
            falloffMul = (dist - 0.35f) / 0.10f * 0.6f;
        }
        else if (dist < 0.85f)
        {
            // 陆地环带（强化）
            falloffMul = 1.2f;
        }
        else if (dist < 1.0f)
        {
            falloffMul = Mathf.Lerp(1.2f, 0.0f, (dist - 0.85f) / 0.15f);
        }
        else
        {
            falloffMul = 0.0f;
        }
        return new TemplateParams { FalloffMul = falloffMul, ElevBias = 0.0f, RidgeMul = 1.0f };
    }

    /// <summary>环火山：环形山脉围绕中心，中心是低地</summary>
    private static TemplateParams SampleRingOfFire(float nx, float ny)
    {
        float dist = Mathf.Sqrt(nx * nx + ny * ny);
        // 山环 dist ≈ 0.5
        float ringMask = Mathf.Exp(-Mathf.Pow((dist - 0.5f) / 0.18f, 2.0f));
        // 中心高程压低
        float centerLow = dist < 0.30f ? -0.15f : 0.0f;
        return new TemplateParams
        {
            FalloffMul = 1.0f,
            ElevBias = centerLow,
            RidgeMul = 1.0f + ringMask * 2.5f, // 环上山脊放大
        };
    }

    /// <summary>大漩涡：中心是深海，外围环形大陆</summary>
    private static TemplateParams SampleMaelstrom(float nx, float ny)
    {
        float dist = Mathf.Sqrt(nx * nx + ny * ny);
        // 中心 0~0.35 强制水
        // 0.35~0.85 是陆地环
        // >0.85 又渐变水
        float falloffMul;
        if (dist < 0.30f) falloffMul = 0.0f; // 中心深海
        else if (dist < 0.45f) falloffMul = (dist - 0.30f) / 0.15f; // 渐变到陆地
        else if (dist < 0.85f) falloffMul = 1.0f;
        else falloffMul = Mathf.Max(0.0f, 1.0f - (dist - 0.85f) * 5.0f);

        return new TemplateParams { FalloffMul = falloffMul, ElevBias = 0.0f, RidgeMul = 1.0f };
    }

    /// <summary>泛大陆：放大陆地，挤压海洋</summary>
    private static TemplateParams SamplePangaea(float nx, float ny)
    {
        float dist = Mathf.Sqrt(nx * nx + ny * ny);
        // 陆地占比 0~0.92，海洋只在最外
        float falloffMul = dist < 0.92f ? 1.3f : Mathf.Max(0.0f, 1.0f - (dist - 0.92f) * 12.0f);
        return new TemplateParams { FalloffMul = falloffMul, ElevBias = 0.05f, RidgeMul = 1.0f };
    }

    /// <summary>双大陆：东西两块陆地</summary>
    private static TemplateParams SampleTwinContinents(float nx, float ny)
    {
        // 两个陆地中心 (-0.45, 0) 和 (0.45, 0)
        float distA = Mathf.Sqrt((nx + 0.45f) * (nx + 0.45f) + ny * ny);
        float distB = Mathf.Sqrt((nx - 0.45f) * (nx - 0.45f) + ny * ny);
        float dist = Mathf.Min(distA, distB);
        // 陆地半径 0.35
        float falloffMul = 1.0f - Mathf.SmoothStep(0.30f, 0.50f, dist);
        return new TemplateParams { FalloffMul = Mathf.Max(0.0f, falloffMul + 0.4f), ElevBias = 0.0f, RidgeMul = 1.0f };
    }

    /// <summary>内陆海：中央大湖+环山</summary>
    private static TemplateParams SampleInlandSea(float nx, float ny)
    {
        float dist = Mathf.Sqrt(nx * nx + ny * ny);
        // 中央湖 0~0.30
        float lakeFalloff;
        if (dist < 0.30f) lakeFalloff = 0.0f;            // 强制水
        else if (dist < 0.40f) lakeFalloff = (dist - 0.30f) / 0.10f * 0.6f; // 渐变
        else if (dist < 0.85f) lakeFalloff = 1.0f + 0.2f; // 陆地环（强化保证不沉没）
        else lakeFalloff = 1.0f;

        // 环山在 dist 0.40~0.55（紧靠湖岸内侧）
        float mountainRing = Mathf.Exp(-Mathf.Pow((dist - 0.45f) / 0.08f, 2.0f));
        return new TemplateParams
        {
            FalloffMul = lakeFalloff,
            ElevBias = 0.0f,
            RidgeMul = 1.0f + mountainRing * 2.5f,  // 加强山环
        };
    }

    /// <summary>高地堡垒：中央高地高原，四周低地</summary>
    private static TemplateParams SampleHighlandFortress(float nx, float ny)
    {
        float dist = Mathf.Sqrt(nx * nx + ny * ny);
        // 中央高原：dist 0~0.45 强制高
        // 边缘悬崖：dist 0.45~0.55
        // 外围低地：dist > 0.55
        float elevBias;
        if (dist < 0.40f) elevBias = 0.20f;
        else if (dist < 0.55f) elevBias = Mathf.Lerp(0.20f, -0.05f, (dist - 0.40f) / 0.15f);
        else elevBias = -0.05f;

        // 边缘悬崖位置加强山脊
        float cliffMask = Mathf.Exp(-Mathf.Pow((dist - 0.50f) / 0.06f, 2.0f));

        return new TemplateParams
        {
            FalloffMul = 1.0f,
            ElevBias = elevBias,
            RidgeMul = 1.0f + cliffMask * 2.0f,
        };
    }

    /// <summary>破碎诸岛：噪声+周期混合产生大量小岛</summary>
    private static TemplateParams SampleBrokenIsles(float nx, float ny, int seed)
    {
        float pattern =
            Mathf.Sin(nx * 9.0f + seed * 0.13f) * 0.4f +
            Mathf.Cos(ny * 9.0f + seed * 0.17f) * 0.4f +
            Mathf.Sin((nx + ny) * 12.0f + seed * 0.07f) * 0.3f;
        float falloffMul = pattern > 0.1f ? 1.0f : Mathf.Max(0.0f, 1.0f + (pattern - 0.1f) * 2.0f);
        return new TemplateParams { FalloffMul = falloffMul, ElevBias = -0.05f, RidgeMul = 0.6f };
    }

    /// <summary>模板的人类可读名（用于UI展示）</summary>
    public static string GetDisplayName(WorldTemplate t) => t switch
    {
        WorldTemplate.Continental => "大陆世界",
        WorldTemplate.Archipelago => "群岛之海",
        WorldTemplate.Mediterranean => "环海之地",
        WorldTemplate.RingOfFire => "环火山带",
        WorldTemplate.Maelstrom => "大漩涡",
        WorldTemplate.Pangaea => "泛大陆",
        WorldTemplate.TwinContinents => "双生大陆",
        WorldTemplate.InlandSea => "内陆之海",
        WorldTemplate.HighlandFortress => "高原堡垒",
        WorldTemplate.BrokenIsles => "破碎诸岛",
        _ => t.ToString(),
    };
}
