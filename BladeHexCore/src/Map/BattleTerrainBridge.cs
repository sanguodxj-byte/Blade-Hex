// BattleTerrainBridge.cs
// 战斗地图 BattleCellData.TerrainType ↔ 大地图 HexOverworldTile.TerrainType 的单向桥接
//
// 设计考虑：
// - 战斗地图枚举多了 Wall / Ruins / PoisonMushroom / LuckyGrass 等战场专用类型
// - 大地图枚举多了 DeepWater / Jungle / Ice / MountainSnow 等世界专用类型
// - 视觉配置挂在大地图枚举上；战斗格子用 ToOverworld() 取 profile
using BladeHex.Data;

namespace BladeHex.Map;

public static class BattleTerrainBridge
{
    /// <summary>战斗地形 → 大地图地形（为取 TerrainVisualProfile 用）</summary>
    public static HexOverworldTile.TerrainType ToOverworld(BattleCellData.TerrainType t) => t switch
    {
        BattleCellData.TerrainType.Plains => HexOverworldTile.TerrainType.Plains,
        BattleCellData.TerrainType.Grassland => HexOverworldTile.TerrainType.Grassland,
        BattleCellData.TerrainType.Savanna => HexOverworldTile.TerrainType.Savanna,
        BattleCellData.TerrainType.Forest => HexOverworldTile.TerrainType.Forest,
        BattleCellData.TerrainType.DenseForest => HexOverworldTile.TerrainType.DenseForest,
        BattleCellData.TerrainType.Hills => HexOverworldTile.TerrainType.Hills,
        BattleCellData.TerrainType.Mountain => HexOverworldTile.TerrainType.Mountain,
        BattleCellData.TerrainType.ShallowWater => HexOverworldTile.TerrainType.ShallowWater,
        BattleCellData.TerrainType.DeepWater => HexOverworldTile.TerrainType.DeepWater,
        BattleCellData.TerrainType.Swamp => HexOverworldTile.TerrainType.Swamp,
        BattleCellData.TerrainType.Road => HexOverworldTile.TerrainType.Road,
        BattleCellData.TerrainType.Sand => HexOverworldTile.TerrainType.Sand,
        BattleCellData.TerrainType.Snow => HexOverworldTile.TerrainType.Snow,
        // 战场专用类型——没有直接对应的 overworld 类型，映射到最接近的底色地形
        BattleCellData.TerrainType.Wall => HexOverworldTile.TerrainType.Rocky,
        BattleCellData.TerrainType.Ruins => HexOverworldTile.TerrainType.Rocky,
        BattleCellData.TerrainType.PoisonMushroom => HexOverworldTile.TerrainType.Swamp,
        BattleCellData.TerrainType.LuckyGrass => HexOverworldTile.TerrainType.Grassland,
        _ => HexOverworldTile.TerrainType.Plains,
    };

    /// <summary>战斗格子直接取视觉 profile 的便捷方法</summary>
    public static TerrainVisualProfile GetProfile(BattleCellData.TerrainType t)
    {
        return TerrainVisualRegistry.Get(ToOverworld(t));
    }
}
