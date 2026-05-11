// HexOverworldGrid.cs
// 六边形大地图网格管理器 — 存储和查询整个大地图的瓦片数据
// 纯数据容器, 渲染由 HexOverworldRenderer 负责
// 迁移自 GDScript HexOverworldGrid.gd
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 大地图六边形网格 — 存储和查询瓦片数据
/// </summary>
[GlobalClass]
public partial class HexOverworldGrid : RefCounted
{
    // ========================================
    // 网格数据
    // ========================================

    /// <summary>所有瓦片: Vector2I(q, r) → HexOverworldTile</summary>
    public Dictionary<Vector2I, HexOverworldTile> Tiles { get; set; } = new();

    /// <summary>q 轴方向格数</summary>
    public int GridWidth { get; set; } = 0;

    /// <summary>r 轴方向格数</summary>
    public int GridHeight { get; set; } = 0;

    /// <summary>地图像素边界宽度</summary>
    public float MapPixelWidth { get; set; } = 0.0f;

    /// <summary>地图像素边界高度</summary>
    public float MapPixelHeight { get; set; } = 0.0f;

    /// <summary>生成种子</summary>
    public int SeedValue { get; set; } = 0;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 创建指定大小的空网格 (矩形六边形网格)
    /// Odd-r offset → axial 转换
    /// </summary>
    public void Initialize(int width, int height)
    {
        GridWidth = width;
        GridHeight = height;
        Tiles.Clear();

        for (int col = 0; col < width; col++)
        {
            for (int row = 0; row < height; row++)
            {
                int q = col - (int)(row / 2.0);
                int r = row;
                var tile = HexOverworldTile.CreateEmpty(q, r);
                Tiles[new Vector2I(q, r)] = tile;
            }
        }

        CalculatePixelBounds();
    }

    // ========================================
    // 瓦片查询
    // ========================================

    /// <summary>获取指定坐标的瓦片 (不存在返回null)</summary>
    public HexOverworldTile? GetTile(int q, int r)
    {
        return Tiles.GetValueOrDefault(new Vector2I(q, r));
    }

    /// <summary>获取指定坐标的瓦片</summary>
    public HexOverworldTile? GetTileAtCoord(Vector2I coord)
    {
        return Tiles.GetValueOrDefault(coord);
    }

    /// <summary>检查坐标是否在网格内</summary>
    public bool HasTile(int q, int r)
    {
        return Tiles.ContainsKey(new Vector2I(q, r));
    }

    /// <summary>获取指定瓦片的6个邻居 (仅返回存在的)</summary>
    public HexOverworldTile[] GetNeighbors(int q, int r)
    {
        var result = new List<HexOverworldTile>();
        for (int dir = 0; dir < 6; dir++)
        {
            var nCoord = HexOverworldTile.GetNeighbor(q, r, dir);
            if (Tiles.TryGetValue(nCoord, out var tile))
                result.Add(tile);
        }
        return result.ToArray();
    }

    /// <summary>获取指定瓦片的可通行邻居</summary>
    public HexOverworldTile[] GetPassableNeighbors(int q, int r)
    {
        var result = new List<HexOverworldTile>();
        for (int dir = 0; dir < 6; dir++)
        {
            var nCoord = HexOverworldTile.GetNeighbor(q, r, dir);
            if (Tiles.TryGetValue(nCoord, out var tile) && tile.IsPassable)
                result.Add(tile);
        }
        return result.ToArray();
    }

    /// <summary>获取指定范围内的所有瓦片 (BFS扩展)</summary>
    public HexOverworldTile[] GetTilesInRange(int q, int r, int maxRange)
    {
        var result = new List<HexOverworldTile>();
        var visited = new HashSet<Vector2I>();
        var queue = new Queue<Vector2I>();
        var start = new Vector2I(q, r);
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!Tiles.TryGetValue(current, out var tile)) continue;

            if (current != start)
                result.Add(tile);

            if (HexOverworldTile.HexDistance(q, r, current.X, current.Y) >= maxRange)
                continue;

            for (int dir = 0; dir < 6; dir++)
            {
                var nCoord = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (!visited.Contains(nCoord) && Tiles.ContainsKey(nCoord))
                {
                    visited.Add(nCoord);
                    queue.Enqueue(nCoord);
                }
            }
        }

        return result.ToArray();
    }

    // ========================================
    // 空间查询
    // ========================================

    /// <summary>通过像素坐标获取最近的瓦片</summary>
    public HexOverworldTile? GetTileAtPixel(float px, float py)
    {
        var coord = HexOverworldTile.PixelToAxial(px, py);
        return Tiles.GetValueOrDefault(coord);
    }

    /// <summary>获取指定像素位置附近的可通行瓦片 (Cube环形搜索)</summary>
    public HexOverworldTile? FindPassableNearPixel(float px, float py, int maxSearchRadius = 10)
    {
        var center = GetTileAtPixel(px, py);
        if (center != null && center.IsPassable)
            return center;

        var startCoord = HexOverworldTile.PixelToAxial(px, py);
        if (center != null)
            startCoord = center.Coord;

        var startCube = HexOverworldTile.AxialToCube(startCoord.X, startCoord.Y);
        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            var ring = HexOverworldTile.CubeRing(startCube, radius);
            foreach (var cubeCoord in ring)
            {
                var axial = HexOverworldTile.CubeToAxial(cubeCoord);
                if (Tiles.TryGetValue(axial, out var tile) && tile.IsPassable)
                    return tile;
            }
        }

        return null;
    }

    // ========================================
    // 统计查询
    // ========================================

    /// <summary>获取所有可通行瓦片</summary>
    public HexOverworldTile[] GetPassableTiles()
    {
        var result = new List<HexOverworldTile>();
        foreach (var tile in Tiles.Values)
            if (tile.IsPassable) result.Add(tile);
        return result.ToArray();
    }

    /// <summary>获取指定地形类型的所有瓦片</summary>
    public HexOverworldTile[] GetTilesByTerrain(HexOverworldTile.TerrainType terrainType)
    {
        var result = new List<HexOverworldTile>();
        foreach (var tile in Tiles.Values)
            if (tile.Terrain == terrainType) result.Add(tile);
        return result.ToArray();
    }

    /// <summary>获取所有道路瓦片</summary>
    public HexOverworldTile[] GetRoadTiles()
    {
        var result = new List<HexOverworldTile>();
        foreach (var tile in Tiles.Values)
            if (tile.IsRoad) result.Add(tile);
        return result.ToArray();
    }

    /// <summary>获取所有河流瓦片</summary>
    public HexOverworldTile[] GetRiverTiles()
    {
        var result = new List<HexOverworldTile>();
        foreach (var tile in Tiles.Values)
            if (tile.IsRiver) result.Add(tile);
        return result.ToArray();
    }

    /// <summary>获取所有定居点瓦片</summary>
    public HexOverworldTile[] GetSettlementTiles()
    {
        var result = new List<HexOverworldTile>();
        foreach (var tile in Tiles.Values)
            if (tile.HasSettlement) result.Add(tile);
        return result.ToArray();
    }

    /// <summary>瓦片总数</summary>
    public int TileCount() => Tiles.Count;

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var tilesData = new Godot.Collections.Array();
        foreach (var tile in Tiles.Values)
            tilesData.Add(tile.Serialize());

        return new Godot.Collections.Dictionary
        {
            ["grid_width"] = GridWidth,
            ["grid_height"] = GridHeight,
            ["seed"] = SeedValue,
            ["tiles"] = tilesData,
        };
    }

    public static HexOverworldGrid Deserialize(Godot.Collections.Dictionary data)
    {
        var grid = new HexOverworldGrid();
        grid.GridWidth = data.ContainsKey("grid_width") ? (int)data["grid_width"] : 0;
        grid.GridHeight = data.ContainsKey("grid_height") ? (int)data["grid_height"] : 0;
        grid.SeedValue = data.ContainsKey("seed") ? (int)data["seed"] : 0;

        if (data.ContainsKey("tiles") && data["tiles"].Obj is Godot.Collections.Array tilesData)
        {
            foreach (var tileData in tilesData)
            {
                var tile = HexOverworldTile.Deserialize((Godot.Collections.Dictionary)tileData);
                grid.Tiles[tile.Coord] = tile;
            }
        }

        grid.CalculatePixelBounds();
        return grid;
    }

    // ========================================
    // 兼容接口
    // ========================================

    /// <summary>返回大地图地形类型 (映射到 OverworldTerrain.Type)</summary>
    public int SampleTerrainAtPixel(float px, float py)
    {
        var tile = GetTileAtPixel(px, py);
        if (tile == null) return 0;
        return HexTerrainToOverworld(tile.Terrain);
    }

    /// <summary>检查像素位置是否可通行</summary>
    public bool IsPassableAtPixel(float px, float py)
    {
        var tile = GetTileAtPixel(px, py);
        return tile != null && tile.IsPassable;
    }

    /// <summary>获取地图中心像素坐标</summary>
    public Vector2 GetCenterPixel()
    {
        return new Vector2(MapPixelWidth * 0.5f, MapPixelHeight * 0.5f);
    }

    /// <summary>获取有效起始位置</summary>
    public Vector2 GetValidStartPos()
    {
        foreach (var tile in Tiles.Values)
            if (tile.HasSettlement && tile.IsPassable)
                return tile.PixelPos;

        var center = GetCenterPixel();
        HexOverworldTile? best = null;
        float bestDist = 999999.0f;
        foreach (var tile in Tiles.Values)
        {
            if (tile.IsPassable)
            {
                float d = tile.PixelPos.DistanceTo(center);
                if (d < bestDist) { bestDist = d; best = tile; }
            }
        }
        return best?.PixelPos ?? center;
    }

    // ========================================
    // 内部方法
    // ========================================

    private void CalculatePixelBounds()
    {
        float minX = 999999.0f, maxX = -999999.0f;
        float minY = 999999.0f, maxY = -999999.0f;

        foreach (var tile in Tiles.Values)
        {
            if (tile.PixelPos.X < minX) minX = tile.PixelPos.X;
            if (tile.PixelPos.X > maxX) maxX = tile.PixelPos.X;
            if (tile.PixelPos.Y < minY) minY = tile.PixelPos.Y;
            if (tile.PixelPos.Y > maxY) maxY = tile.PixelPos.Y;
        }

        MapPixelWidth = maxX - minX + HexOverworldTile.HexSize * 2.0f;
        MapPixelHeight = maxY - minY + HexOverworldTile.HexSize * 2.0f;
    }

    /// <summary>HexOverworldTile.TerrainType → OverworldTerrain.Type 映射</summary>
    private static int HexTerrainToOverworld(HexOverworldTile.TerrainType hexTerrain) => hexTerrain switch
    {
        HexOverworldTile.TerrainType.DeepWater => 4,
        HexOverworldTile.TerrainType.ShallowWater => 4,
        HexOverworldTile.TerrainType.Sand => 6,
        HexOverworldTile.TerrainType.Plains => 0,
        HexOverworldTile.TerrainType.Grassland => 0,
        HexOverworldTile.TerrainType.Forest => 1,
        HexOverworldTile.TerrainType.DenseForest => 1,
        HexOverworldTile.TerrainType.Hills => 2,
        HexOverworldTile.TerrainType.Mountain => 2,
        HexOverworldTile.TerrainType.Snow => 2,
        HexOverworldTile.TerrainType.Swamp => 3,
        HexOverworldTile.TerrainType.Savanna => 0,
        HexOverworldTile.TerrainType.Road => 5,
        HexOverworldTile.TerrainType.River => 4,
        _ => 0,
    };
}
