// BiomeType.cs
// 粗粒度生态类型枚举 — 用于国家偏好匹配和生态区聚类
// 不同于细粒度的 TerrainType（21种），这里是 8 种宏观生态分类
namespace BladeHex.Map;

/// <summary>
/// 宏观生态类型 — 用于国家领土偏好匹配
/// 每种生态类型对应一组 TerrainType，代表一种宏观生态环境
/// </summary>
public enum BiomeType
{
    /// <summary>平原/草地 — 适合人类定居</summary>
    Plains,

    /// <summary>森林/密林 — 适合精灵</summary>
    Forest,

    /// <summary>山地/丘陵 — 适合矮人</summary>
    Mountain,

    /// <summary>荒原/沙漠/焦土 — 适合兽人</summary>
    Wasteland,

    /// <summary>沼泽/湿地 — 适合蜥蜴人/暗影教团</summary>
    Swamp,

    /// <summary>冻土/雪原 — 适合冰霜生物</summary>
    Tundra,

    /// <summary>丛林 — 中立/危险区域</summary>
    Jungle,

    /// <summary>沿海 — 适合商贸/海盗</summary>
    Coastal,
}

/// <summary>
/// TerrainType → BiomeType 映射 — 将 21 种细粒度地形归类到 8 种宏观生态
/// </summary>
public static class TerrainToBiome
{
    /// <summary>
    /// 将细粒度地形类型映射到宏观生态类型
    /// </summary>
    public static BiomeType Map(HexOverworldTile.TerrainType terrain)
    {
        return terrain switch
        {
            // 平原系
            HexOverworldTile.TerrainType.Plains => BiomeType.Plains,
            HexOverworldTile.TerrainType.Grassland => BiomeType.Plains,
            HexOverworldTile.TerrainType.Road => BiomeType.Plains,

            // 森林系
            HexOverworldTile.TerrainType.Forest => BiomeType.Forest,
            HexOverworldTile.TerrainType.DenseForest => BiomeType.Forest,
            HexOverworldTile.TerrainType.Taiga => BiomeType.Forest,

            // 山地系
            HexOverworldTile.TerrainType.Hills => BiomeType.Mountain,
            HexOverworldTile.TerrainType.Mountain => BiomeType.Mountain,
            HexOverworldTile.TerrainType.MountainSnow => BiomeType.Mountain,
            HexOverworldTile.TerrainType.Rocky => BiomeType.Mountain,

            // 荒原系
            HexOverworldTile.TerrainType.Sand => BiomeType.Wasteland,
            HexOverworldTile.TerrainType.Savanna => BiomeType.Wasteland,
            HexOverworldTile.TerrainType.Wasteland => BiomeType.Wasteland,

            // 沼泽系
            HexOverworldTile.TerrainType.Swamp => BiomeType.Swamp,
            HexOverworldTile.TerrainType.Bog => BiomeType.Swamp,

            // 冻土系
            HexOverworldTile.TerrainType.Snow => BiomeType.Tundra,
            HexOverworldTile.TerrainType.Ice => BiomeType.Tundra,

            // 丛林系
            HexOverworldTile.TerrainType.Jungle => BiomeType.Jungle,

            // 沿海系
            HexOverworldTile.TerrainType.ShallowWater => BiomeType.Coastal,

            // 深水/河流 — 不属于任何陆地生态，返回 Coastal 作为最近似
            HexOverworldTile.TerrainType.DeepWater => BiomeType.Coastal,
            HexOverworldTile.TerrainType.River => BiomeType.Coastal,

            _ => BiomeType.Plains,
        };
    }

    /// <summary>
    /// 判断地形是否为陆地（可作为国家领土）
    /// </summary>
    public static bool IsLandTerrain(HexOverworldTile.TerrainType terrain)
    {
        return terrain switch
        {
            HexOverworldTile.TerrainType.DeepWater => false,
            HexOverworldTile.TerrainType.ShallowWater => false,
            HexOverworldTile.TerrainType.River => false,
            HexOverworldTile.TerrainType.Mountain => false,
            HexOverworldTile.TerrainType.MountainSnow => false,
            _ => true,
        };
    }
}
