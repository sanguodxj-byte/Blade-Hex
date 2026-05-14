// TerrainCostTable.cs
// 地形代价权威数据源 — 所有地形移动代价的唯一真相来源
// 寻路系统 (ChunkAStar) 和移速系统 (MovementSpeedComponent) 均从此表获取数据
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 地形代价权威表 — 静态单例，定义 21 种地形类型的移动代价和通行性。
/// 所有需要地形代价的系统（寻路、移速、道路生成）都应从此表获取数据。
/// </summary>
public static class TerrainCostTable
{
    // ========================================
    // 地形代价定义
    // ========================================

    private static readonly Dictionary<HexOverworldTile.TerrainType, (float MoveCost, bool IsPassable)> _table = new()
    {
        [HexOverworldTile.TerrainType.DeepWater]    = (99.0f, false),
        [HexOverworldTile.TerrainType.ShallowWater] = (3.0f,  true),
        [HexOverworldTile.TerrainType.Sand]         = (1.5f,  true),
        [HexOverworldTile.TerrainType.Plains]       = (1.0f,  true),
        [HexOverworldTile.TerrainType.Grassland]    = (1.0f,  true),
        [HexOverworldTile.TerrainType.Forest]       = (1.5f,  true),
        [HexOverworldTile.TerrainType.DenseForest]  = (2.5f,  true),
        [HexOverworldTile.TerrainType.Jungle]       = (2.5f,  true),
        [HexOverworldTile.TerrainType.Taiga]        = (1.5f,  true),
        [HexOverworldTile.TerrainType.Bog]          = (3.0f,  true),
        [HexOverworldTile.TerrainType.Swamp]        = (2.5f,  true),
        [HexOverworldTile.TerrainType.Savanna]      = (1.0f,  true),
        [HexOverworldTile.TerrainType.Wasteland]    = (1.2f,  true),
        [HexOverworldTile.TerrainType.Rocky]        = (1.8f,  true),
        [HexOverworldTile.TerrainType.Hills]        = (2.0f,  true),
        [HexOverworldTile.TerrainType.Mountain]     = (99.0f, false),
        [HexOverworldTile.TerrainType.MountainSnow] = (99.0f, false),
        [HexOverworldTile.TerrainType.Snow]         = (2.0f,  true),
        [HexOverworldTile.TerrainType.Ice]          = (2.0f,  true),
        [HexOverworldTile.TerrainType.Road]         = (0.2f,  true),
        [HexOverworldTile.TerrainType.River]        = (99.0f, false),
    };

    /// <summary>道路覆盖代价 — IsRoad=true 的 tile 统一使用此值</summary>
    public const float RoadMoveCost = 0.2f;

    /// <summary>不可通行哨兵值</summary>
    public const float ImpassableCost = 99.0f;

    /// <summary>默认代价（未知地形类型）</summary>
    public const float DefaultMoveCost = 1.0f;

    /// <summary>海上航行代价（有船时水域的代价）</summary>
    public const float SeaMoveCost = 0.8f;

    /// <summary>海上模式下陆地的代价（不可登陆，除非是港口/海岸）</summary>
    public const float LandFromSeaCost = 99.0f;

    // ========================================
    // 代价查询
    // ========================================

    /// <summary>获取指定地形类型的移动代价</summary>
    public static float GetMoveCost(HexOverworldTile.TerrainType terrain)
    {
        return _table.TryGetValue(terrain, out var entry) ? entry.MoveCost : DefaultMoveCost;
    }

    /// <summary>
    /// 获取指定 tile 的移动代价（考虑 IsRoad 覆盖）。
    /// IsRoad=true 时返回 0.5，否则返回地形基础代价。
    /// </summary>
    public static float GetMoveCost(HexOverworldTile tile)
    {
        if (tile.IsRoad) return RoadMoveCost;
        return GetMoveCost(tile.Terrain);
    }

    /// <summary>判断指定地形类型是否可通行</summary>
    public static bool IsPassable(HexOverworldTile.TerrainType terrain)
    {
        return _table.TryGetValue(terrain, out var entry) ? entry.IsPassable : true;
    }

    /// <summary>判断指定 tile 是否可通行</summary>
    public static bool IsPassable(HexOverworldTile tile)
    {
        return tile.IsPassable; // tile 自身已有此属性，保持一致
    }

    // ========================================
    // 海上模式代价查询
    // ========================================

    /// <summary>
    /// 获取海上模式的移动代价（有船时）。
    /// 水域可通行（代价低），陆地不可通行（除非是海岸/港口）。
    /// </summary>
    public static float GetSeaMoveCost(HexOverworldTile tile)
    {
        // 水域：可航行
        if (tile.Terrain == HexOverworldTile.TerrainType.DeepWater) return SeaMoveCost;
        if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater) return SeaMoveCost * 1.5f; // 浅水稍慢
        // 海岸线（可登陆的陆地）：允许通行但代价高（表示靠岸）
        if (tile.Terrain == HexOverworldTile.TerrainType.Sand) return 2.0f; // 沙滩可登陆
        // 其他陆地：不可通行
        return LandFromSeaCost;
    }

    /// <summary>
    /// 获取海上模式的移动代价（按地形类型）。
    /// </summary>
    public static float GetSeaMoveCost(HexOverworldTile.TerrainType terrain)
    {
        return terrain switch
        {
            HexOverworldTile.TerrainType.DeepWater => SeaMoveCost,
            HexOverworldTile.TerrainType.ShallowWater => SeaMoveCost * 1.5f,
            HexOverworldTile.TerrainType.Sand => 2.0f, // 沙滩可登陆
            _ => LandFromSeaCost,
        };
    }

    /// <summary>判断指定 tile 在海上模式下是否可通行</summary>
    public static bool IsSeaPassable(HexOverworldTile tile)
    {
        return tile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
               tile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
               tile.Terrain == HexOverworldTile.TerrainType.Sand; // 沙滩=可登陆点
    }

    /// <summary>判断 tile 是否为港口可停靠点（海岸线陆地格）</summary>
    public static bool IsCoastalLanding(HexOverworldTile tile)
    {
        if (!tile.IsPassable) return false;
        // 沙滩或有港口标记的格子
        return tile.Terrain == HexOverworldTile.TerrainType.Sand || tile.HasSettlement;
    }

    // ========================================
    // 速度因子查询 (供 MovementSpeedComponent 使用)
    // 注意: 速度因子与寻路代价是独立的两套数值！
    // 寻路代价决定"路径选择"（道路 0.2 让 A* 强烈偏好道路）
    // 速度因子决定"实际移速"（道路 1.5x 是合理的加速幅度）
    // ========================================

    /// <summary>
    /// 获取地形移速因子（独立于寻路代价）。
    /// 道路=1.5x, 平原=1.0x, 森林=0.7x, 沼泽=0.5x 等。
    /// </summary>
    public static float GetSpeedFactor(HexOverworldTile.TerrainType terrain)
    {
        return terrain switch
        {
            HexOverworldTile.TerrainType.Road => 1.5f,
            HexOverworldTile.TerrainType.Plains => 1.0f,
            HexOverworldTile.TerrainType.Grassland => 1.0f,
            HexOverworldTile.TerrainType.Savanna => 1.0f,
            HexOverworldTile.TerrainType.Sand => 0.8f,
            HexOverworldTile.TerrainType.Wasteland => 0.85f,
            HexOverworldTile.TerrainType.Rocky => 0.7f,
            HexOverworldTile.TerrainType.Forest => 0.7f,
            HexOverworldTile.TerrainType.Taiga => 0.7f,
            HexOverworldTile.TerrainType.Snow => 0.6f,
            HexOverworldTile.TerrainType.Ice => 0.6f,
            HexOverworldTile.TerrainType.Hills => 0.6f,
            HexOverworldTile.TerrainType.DenseForest => 0.5f,
            HexOverworldTile.TerrainType.Jungle => 0.5f,
            HexOverworldTile.TerrainType.Swamp => 0.4f,
            HexOverworldTile.TerrainType.Bog => 0.4f,
            HexOverworldTile.TerrainType.ShallowWater => 0.3f,
            // 不可通行地形不应出现在速度计算中，但以防万一
            HexOverworldTile.TerrainType.DeepWater => 0.1f,
            HexOverworldTile.TerrainType.Mountain => 0.1f,
            HexOverworldTile.TerrainType.MountainSnow => 0.1f,
            HexOverworldTile.TerrainType.River => 0.1f,
            _ => 1.0f,
        };
    }

    /// <summary>获取 tile 的速度因子（考虑 IsRoad 覆盖）</summary>
    public static float GetSpeedFactor(HexOverworldTile tile)
    {
        if (tile.IsRoad) return 1.5f;
        return GetSpeedFactor(tile.Terrain);
    }
}
