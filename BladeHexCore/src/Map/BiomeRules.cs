// BiomeRules.cs
// 生物群落决策规则 — 全局唯一事实来源 (SSOT)
// 所有地形生成器（HexOverworldGenerator / ChunkGenerator / RiverRoadGenerator）
// 都应调用此类的 Decide 方法，而非各自维护副本
namespace BladeHex.Map;

/// <summary>
/// 生物群落决策规则 — 从高程/湿度/温度三维噪声决定地形类型
/// 这是唯一的地形决策逻辑来源，消除了之前三处重复的 BiomeDecision 方法
/// </summary>
public static class BiomeRules
{
    // ========================================
    // 高程阈值常量
    // ========================================

    public const float SeaLevel = 0.22f;       // 降低：只有真正低洼处才是深水
    public const float ShallowLevel = 0.27f;   // 收窄浅水带
    public const float BeachLevel = 0.30f;     // 沙滩带更窄
    public const float MountainLevel = 0.78f;

    // ========================================
    // 噪声频率常量（供所有生成器共享）
    // ========================================

    public const float ElevationFreq = 0.06f;
    public const float MoistureFreq = 0.07f;
    public const float TemperatureFreq = 0.025f;

    // ========================================
    // 核心决策方法
    // ========================================

    /// <summary>
    /// 根据高程、湿度、温度决定地形类型
    /// 所有值范围 [0, 1]
    /// </summary>
    public static HexOverworldTile.TerrainType Decide(float elevation, float moisture, float temperature)
    {
        // 水域判定
        if (elevation < SeaLevel) return HexOverworldTile.TerrainType.DeepWater;
        if (elevation < ShallowLevel) return HexOverworldTile.TerrainType.ShallowWater;
        if (elevation < BeachLevel)
            return temperature < 0.15f ? HexOverworldTile.TerrainType.Ice : HexOverworldTile.TerrainType.Sand;

        // 高山判定
        if (elevation > MountainLevel)
            return (elevation > 0.88f || temperature < 0.25f)
                ? HexOverworldTile.TerrainType.MountainSnow
                : HexOverworldTile.TerrainType.Mountain;

        // 温度带
        bool freezing = temperature < 0.15f;
        bool cold = temperature >= 0.15f && temperature < 0.35f;
        bool temperate = temperature >= 0.35f && temperature < 0.70f;
        bool hot = temperature >= 0.70f;

        // 湿度带（收窄干旱判定，让干旱地形更难出现）
        bool arid = moisture < 0.18f;
        bool dry = moisture >= 0.18f && moisture < 0.42f;
        bool wet = moisture >= 0.42f && moisture < 0.70f;
        // humid = moisture >= 0.70f

        var baseTerrain = HexOverworldTile.TerrainType.Plains;

        if (freezing)
            baseTerrain = arid ? HexOverworldTile.TerrainType.Ice : HexOverworldTile.TerrainType.Snow;
        else if (cold)
            baseTerrain = arid
                ? HexOverworldTile.TerrainType.Rocky
                : (dry || wet)
                    ? HexOverworldTile.TerrainType.Taiga
                    : HexOverworldTile.TerrainType.Bog;
        else if (temperate)
            baseTerrain = arid
                ? HexOverworldTile.TerrainType.Wasteland
                : dry
                    ? HexOverworldTile.TerrainType.Plains
                    : wet
                        ? HexOverworldTile.TerrainType.Forest
                        : HexOverworldTile.TerrainType.DenseForest;
        else if (hot)
            baseTerrain = arid
                ? HexOverworldTile.TerrainType.Sand
                : dry
                    ? HexOverworldTile.TerrainType.Savanna
                    : wet
                        ? HexOverworldTile.TerrainType.Jungle
                        : HexOverworldTile.TerrainType.Swamp;

        // 高程微调：丘陵带
        if (elevation > 0.65f && elevation <= MountainLevel)
        {
            if (baseTerrain == HexOverworldTile.TerrainType.Snow || baseTerrain == HexOverworldTile.TerrainType.Ice)
                return HexOverworldTile.TerrainType.Snow;
            return HexOverworldTile.TerrainType.Hills;
        }

        return baseTerrain;
    }
}
