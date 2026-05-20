// BiomeRules.cs
// 生物群落决策规则 — 全局唯一事实来源 (SSOT)
// 所有地形生成器（ChunkGenerator / RiverRoadGenerator）
// 都应调用此类的 Decide 方法，而非各自维护副本
namespace BladeHex.Map;

/// <summary>
/// 生物群落决策规则 — 从高程/湿度/温度三维噪声决定地形类型
/// 这是唯一的地形决策逻辑来源
/// 
/// 设计原则：
/// 1. 温度带宽广（3 带而非 4 带），减少纬度方向碎片
/// 2. 湿度阈值使用宽过渡区，避免边界抖动
/// 3. 平原/草地作为默认填充，占比最大（类似真实世界）
/// 4. 极端地形（沙漠/冰原/沼泽）只在极端条件下出现
/// </summary>
public static class BiomeRules
{
    // ========================================
    // 高程阈值常量
    // ========================================

    public const float SeaLevel = 0.20f;       // 深水阈值（略降低，让内海更容易形成深水）
    public const float ShallowLevel = 0.26f;   // 浅水带（加宽：0.20-0.26 = 6% 范围）
    public const float BeachLevel = 0.31f;     // 沙滩/海岸带（加宽：0.26-0.31 = 5% 范围）
    public const float HillLevel = 0.55f;      // 丘陵起始（从 0.68 → 0.55，让丘陵更常见）
    public const float MountainLevel = 0.68f;  // 山地起始（从 0.80 → 0.68，让山脉真的会出现）

    // ========================================
    // 温度带阈值（3 带：冷/温/热）
    // ========================================

    public const float ColdThreshold = 0.30f;       // < 0.30 = 寒冷
    public const float HotThreshold = 0.72f;        // > 0.72 = 炎热
    // 0.30 ~ 0.72 = 温带（占比最大，约 42% 的温度空间）

    // ========================================
    // 湿度带阈值（3 带：干/中/湿）
    // ========================================

    public const float DryThreshold = 0.32f;        // < 0.32 = 干旱
    public const float WetThreshold = 0.62f;        // > 0.62 = 湿润
    public const float VeryDryThreshold = 0.18f;    // < 0.18 = 极度干旱（才出 Wasteland/Sand）
    // 0.32 ~ 0.62 = 中等（占比最大，约 30% 的湿度空间）

    // ========================================
    // 核心决策方法
    // ========================================

    /// <summary>
    /// 根据高程、湿度、温度决定地形类型
    /// 所有值范围 [0, 1]
    /// </summary>
    public static HexOverworldTile.TerrainType Decide(float elevation, float moisture, float temperature)
    {
        // ========== 水域判定 ==========
        if (elevation < SeaLevel) return HexOverworldTile.TerrainType.DeepWater;
        if (elevation < ShallowLevel) return HexOverworldTile.TerrainType.ShallowWater;
        if (elevation < BeachLevel)
            return temperature < ColdThreshold ? HexOverworldTile.TerrainType.Ice : HexOverworldTile.TerrainType.Sand;

        // ========== 高山判定 ==========
        if (elevation > MountainLevel)
            return (elevation > 0.90f || temperature < 0.30f)
                ? HexOverworldTile.TerrainType.MountainSnow
                : HexOverworldTile.TerrainType.Mountain;

        // ========== 丘陵带 ==========
        if (elevation > HillLevel)
        {
            // 只有极寒（temp < 0.15）才出雪地丘陵，普通寒冷仍然是丘陵
            if (temperature < 0.15f)
                return HexOverworldTile.TerrainType.Snow;
            return HexOverworldTile.TerrainType.Hills;
        }

        // ========== 平地生物群落（高程 0.30 ~ 0.68）==========
        // 使用 3×3 温度-湿度矩阵，每格对应一种主要地形

        // --- 寒冷带 (temp < 0.30) ---
        if (temperature < ColdThreshold)
        {
            if (moisture < DryThreshold)
                return HexOverworldTile.TerrainType.Rocky;      // 寒冷干旱 = 岩石荒地
            if (moisture > WetThreshold)
                return HexOverworldTile.TerrainType.Bog;        // 寒冷湿润 = 冻土沼泽
            return HexOverworldTile.TerrainType.Taiga;          // 寒冷中等 = 针叶林
        }

        // --- 炎热带 (temp > 0.72) ---
        if (temperature > HotThreshold)
        {
            if (moisture < VeryDryThreshold)
                return HexOverworldTile.TerrainType.Sand;       // 炎热极干 = 沙漠
            if (moisture < DryThreshold)
                return HexOverworldTile.TerrainType.Savanna;    // 炎热干旱 = 稀树草原
            if (moisture > WetThreshold)
                return HexOverworldTile.TerrainType.Jungle;     // 炎热湿润 = 丛林
            return HexOverworldTile.TerrainType.Savanna;        // 炎热中等 = 稀树草原
        }

        // --- 温带 (0.30 ~ 0.72) — 占比最大 ---
        if (moisture < DryThreshold)
        {
            // 极度干旱才是荒原，普通干旱仍是平原（避免 Wasteland 泛滥）
            if (moisture < VeryDryThreshold)
                return HexOverworldTile.TerrainType.Wasteland;
            return HexOverworldTile.TerrainType.Plains;
        }

        if (moisture > WetThreshold)
        {
            // 温带湿润：根据温度细分
            if (temperature > 0.55f)
                return HexOverworldTile.TerrainType.Swamp;      // 温暖湿润 = 沼泽
            return HexOverworldTile.TerrainType.DenseForest;    // 凉爽湿润 = 密林
        }

        // 温带中等湿度（最大区域）：平原 vs 森林 vs 草地
        // 用湿度的精细位置决定：偏干 = 草地/平原，偏湿 = 森林
        float midMoisture = (moisture - DryThreshold) / (WetThreshold - DryThreshold); // 归一化到 [0,1]

        if (midMoisture < 0.4f)
            return HexOverworldTile.TerrainType.Plains;         // 偏干的温带 = 平原
        if (midMoisture < 0.7f)
            return HexOverworldTile.TerrainType.Grassland;      // 中间 = 草地
        return HexOverworldTile.TerrainType.Forest;             // 偏湿的温带 = 森林
    }
}
