// HexOverworldGenerator.cs
// 六边形大地图结构化生成器 — 战场兄弟风格
// 9步管线: 噪声 → 生物群落 → 平滑 → 海岸线 → 区域 → 河流 → 道路 → 后处理
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 地理区域定义 — 参见 RegionRegistry.cs（SSOT）
/// </summary>
// RegionDef 已移至 RegionRegistry.cs

/// <summary>
/// 六边形大地图生成器 — 9步程序化生成管线
/// </summary>
[GlobalClass]
public partial class HexOverworldGenerator : RefCounted
{
    // ========================================
    // 生成配置常量
    // ========================================

    public const int DefaultWidth = 64;
    public const int DefaultHeight = 48;

    public const float ElevationFreq = 0.06f;
    public const float MoistureFreq = 0.07f;
    public const float TemperatureFreq = 0.025f;

    public const float SeaLevel = 0.30f;
    public const float ShallowLevel = 0.35f;
    public const float BeachLevel = 0.38f;
    public const float MountainLevel = 0.78f;

    public const float DryThreshold = 0.35f;
    public const float WetThreshold = 0.65f;

    public const int SmoothPasses = 2;

    public const int RiverCountMin = 3;
    public const int RiverCountMax = 6;
    public const int RiverMinLength = 15;

    public const float RoadPenaltyForest = 3.0f;
    public const float RoadPenaltyHill = 5.0f;
    public const float RoadPenaltySwamp = 8.0f;

    // ========================================
    // 运行时状态
    // ========================================

    private FastNoiseLite? _noiseElev;
    private FastNoiseLite? _noiseMoist;
    private FastNoiseLite? _noiseTemp;
    private FastNoiseLite? _noiseDetail;

    public HexOverworldGrid? Grid { get; private set; }
    private readonly List<RegionDef> _regions = [];
    public int Seed { get; private set; } = 0;

    // ========================================
    // 主入口
    // ========================================

    /// <summary>
    /// 生成完整的大地图
    /// </summary>
    public HexOverworldGrid Generate(int width = DefaultWidth, int height = DefaultHeight, int worldSeed = -1)
    {
        Seed = worldSeed >= 0 ? worldSeed : (int)(GD.Randi());
        GD.Seed((ulong)Seed);

        InitNoise();

        Grid = new HexOverworldGrid();
        Grid.Initialize(width, height);
        Grid.SeedValue = Seed;

        GenerateBaseLayers(width, height);
        AssignBiomeTerrains();
        SmoothTerrain(SmoothPasses);
        FixCoastlines();
        DefineRegions();
        AssignRegionNames();
        GenerateRivers();
        GenerateRoads();
        FinalizeTerrain();

        GD.Print($"[HexOverworldGenerator] 生成完成: {width}×{height} 瓦片, 种子={Seed}, 河流/道路已生成");
        return Grid;
    }

    // ========================================
    // 第0步: 噪声初始化
    // ========================================

    private void InitNoise()
    {
        _noiseElev = new FastNoiseLite();
        _noiseElev.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseElev.Seed = Seed;
        _noiseElev.Frequency = ElevationFreq;
        _noiseElev.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseElev.FractalOctaves = 6;
        _noiseElev.FractalLacunarity = 2.0f;
        _noiseElev.FractalGain = 0.5f;

        _noiseMoist = new FastNoiseLite();
        _noiseMoist.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseMoist.Seed = Seed + 1000;
        _noiseMoist.Frequency = MoistureFreq;
        _noiseMoist.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseMoist.FractalOctaves = 4;
        _noiseMoist.FractalLacunarity = 2.0f;
        _noiseMoist.FractalGain = 0.5f;

        _noiseTemp = new FastNoiseLite();
        _noiseTemp.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseTemp.Seed = Seed + 2000;
        _noiseTemp.Frequency = TemperatureFreq;
        _noiseTemp.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseTemp.FractalOctaves = 3;

        _noiseDetail = new FastNoiseLite();
        _noiseDetail.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
        _noiseDetail.Seed = Seed + 3000;
        _noiseDetail.Frequency = 0.08f;
        _noiseDetail.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
    }

    // ========================================
    // 第1步: 基础数据层生成
    // ========================================

    private void GenerateBaseLayers(int width, int height)
    {
        foreach (var t in Grid!.Tiles.Values)
        {
            int q = t.Coord.X;
            int r = t.Coord.Y;

            float rawElev = _noiseElev!.GetNoise2D(q, r);
            float edgeFalloff = CalcEdgeFalloff(q, r, width, height);
            float elev = (rawElev + 1.0f) * 0.5f * edgeFalloff;
            t.Elevation = Mathf.Clamp(elev, 0.0f, 1.0f);

            float rawMoist = _noiseMoist!.GetNoise2D(q, r);
            t.Moisture = Mathf.Clamp((rawMoist + 1.0f) * 0.5f, 0.0f, 1.0f);

            float latitudeFactor = (float)r / height;
            float tempNoise = _noiseTemp!.GetNoise2D(q, r) * 0.2f;
            // 高海拔降温：海拔超过 0.5 时开始降温，最大降低 0.3
            float altitudePenalty = Mathf.Clamp(t.Elevation - 0.5f, 0.0f, 0.5f) * 0.6f;
            t.Temperature = Mathf.Clamp(latitudeFactor + tempNoise - altitudePenalty, 0.0f, 1.0f);
        }
    }

    private static float CalcEdgeFalloff(int q, int r, int width, int height)
    {
        float nx = (float)(q + height / 2) / width;
        float ny = (float)r / height;

        float dx = Mathf.Min(nx, 1.0f - nx);
        float dy = Mathf.Min(ny, 1.0f - ny);
        float edgeDist = Mathf.Min(dx, dy);

        return edgeDist < 0.15f ? edgeDist / 0.15f : 1.0f;
    }

    // ========================================
    // 第2步: 生物群落规则
    // ========================================

    private void AssignBiomeTerrains()
    {
        foreach (var t in Grid!.Tiles.Values)
        {
            t.Terrain = BiomeDecision(t);
            t.UpdateTerrainProperties();
        }
    }

    private static HexOverworldTile.TerrainType BiomeDecision(HexOverworldTile tile)
    {
        // 委托到 BiomeRules.Decide (SSOT) — 消除重复逻辑
        return BiomeRules.Decide(tile.Elevation, tile.Moisture, tile.Temperature);
    }

    // ========================================
    // 第3步: 地形平滑
    // ========================================

    private void SmoothTerrain(int passes)
    {
        var immune = new HashSet<HexOverworldTile.TerrainType>
        {
            HexOverworldTile.TerrainType.DeepWater,
            HexOverworldTile.TerrainType.ShallowWater,
            HexOverworldTile.TerrainType.Road,
            HexOverworldTile.TerrainType.River,
        };

        for (int pass = 0; pass < passes; pass++)
        {
            var changes = new Dictionary<Vector2I, HexOverworldTile.TerrainType>();

            foreach (var tile in Grid!.Tiles.Values)
            {
                if (immune.Contains(tile.Terrain)) continue;

                var counts = new Dictionary<HexOverworldTile.TerrainType, int>();
                foreach (var nTile in Grid.GetNeighbors(tile.Coord.X, tile.Coord.Y))
                {
                    if (!counts.ContainsKey(nTile.Terrain)) counts[nTile.Terrain] = 0;
                    counts[nTile.Terrain]++;
                }

                foreach (var kv in counts)
                {
                    if (kv.Value >= 4 && kv.Key != tile.Terrain)
                    {
                        if (GD.Randf() < 0.6)
                            changes[tile.Coord] = kv.Key;
                        break;
                    }
                }
            }

            foreach (var kv in changes)
            {
                var tile = Grid.GetTile(kv.Key.X, kv.Key.Y);
                tile?.SetTerrain(kv.Value);
            }
        }
    }

    // ========================================
    // 第4步: 海岸线修正
    // ========================================

    private void FixCoastlines()
    {
        foreach (var t in Grid!.Tiles.Values)
        {
            if (t.Terrain == HexOverworldTile.TerrainType.DeepWater)
            {
                bool hasLand = false;
                foreach (var nTile in Grid.GetNeighbors(t.Coord.X, t.Coord.Y))
                {
                    if (nTile.Elevation >= BeachLevel) { hasLand = true; break; }
                }
                if (hasLand && GD.Randf() < 0.7)
                    t.SetTerrain(HexOverworldTile.TerrainType.ShallowWater);
            }
        }

        foreach (var t in Grid.Tiles.Values)
        {
            if (t.Terrain == HexOverworldTile.TerrainType.Sand)
            {
                foreach (var nTile in Grid.GetNeighbors(t.Coord.X, t.Coord.Y))
                {
                    if (nTile.Terrain == HexOverworldTile.TerrainType.DeepWater && GD.Randf() < 0.6)
                        nTile.SetTerrain(HexOverworldTile.TerrainType.ShallowWater);
                }
            }
        }
    }

    // ========================================
    // 第5步: 地理区域定义与分配
    // ========================================

    private void DefineRegions()
    {
        _regions.Clear();
        foreach (var def in RegionRegistry.Regions)
            _regions.Add(def);
    }

    private void AssignRegionNames()
    {
        int width = Grid!.GridWidth;
        int height = Grid.GridHeight;
        var preferredSet = new HashSet<HexOverworldTile.TerrainType>();

        foreach (var t in Grid.Tiles.Values)
        {
            if (t.Terrain == HexOverworldTile.TerrainType.DeepWater || t.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                continue;

            float nq = Mathf.Clamp((float)(t.Coord.X + height / 2) / width, 0.0f, 1.0f);
            float nr = Mathf.Clamp((float)t.Coord.Y / height, 0.0f, 1.0f);

            RegionDef? bestRegion = null;
            float bestScore = -1.0f;

            foreach (var region in _regions)
            {
                float dq = (nq - region.CenterQ) / Mathf.Max(region.RadiusQ, 0.01f);
                float dr = (nr - region.CenterR) / Mathf.Max(region.RadiusR, 0.01f);
                float distSq = dq * dq + dr * dr;
                float score = Mathf.Exp(-distSq * 2.0f);

                preferredSet.Clear();
                foreach (var pt in region.PreferredTerrains) preferredSet.Add(pt);
                if (preferredSet.Contains(t.Terrain)) score *= 1.5f;

                if (score > bestScore) { bestScore = score; bestRegion = region; }
            }

            if (bestRegion != null && bestScore > 0.3f)
                t.RegionName = bestRegion.Name;
        }
    }

    // ========================================
    // 第6步: 河流生成
    // ========================================

    private void GenerateRivers()
    {
        int riverCount = (int)GD.RandRange(RiverCountMin, RiverCountMax);
        var astar = new HexOverworldAStar { Grid = Grid, IgnorePassability = true, ImpassablePenalty = 15.0f };

        var highTiles = new List<HexOverworldTile>();
        foreach (var t in Grid!.Tiles.Values)
            if (t.Elevation > 0.65f && t.IsPassable && !t.IsRiver)
                highTiles.Add(t);

        var waterEdgeTiles = new List<HexOverworldTile>();
        foreach (var t in Grid.Tiles.Values)
        {
            if (t.Terrain == HexOverworldTile.TerrainType.ShallowWater)
            {
                foreach (var n in Grid.GetNeighbors(t.Coord.X, t.Coord.Y))
                {
                    if (n.IsPassable && n.Terrain != HexOverworldTile.TerrainType.Sand)
                    { waterEdgeTiles.Add(n); break; }
                }
            }
        }

        if (highTiles.Count == 0 || waterEdgeTiles.Count == 0) return;

        int riversPlaced = 0;
        var usedSources = new HashSet<Vector2I>();
        var rng = new Random();

        for (int attempt = 0; attempt < riverCount * 5; attempt++)
        {
            if (riversPlaced >= riverCount) break;

            var source = highTiles[rng.Next(highTiles.Count)];
            if (usedSources.Contains(source.Coord)) continue;

            var target = waterEdgeTiles[rng.Next(waterEdgeTiles.Count)];
            var path = astar.FindLowestElevationPath(source.Coord, target.Coord);

            if (path.Length < RiverMinLength) continue;

            MarkRiverPath(path);
            usedSources.Add(source.Coord);
            riversPlaced++;
        }

        GD.Print($"[HexOverworldGenerator] 生成 {riversPlaced} 条河流");
    }

    private void MarkRiverPath(Vector2I[] path)
    {
        for (int i = 0; i < path.Length; i++)
        {
            var tile = Grid!.GetTile(path[i].X, path[i].Y);
            if (tile == null) continue;

            tile.IsRiver = true;
            tile.SetTerrain(HexOverworldTile.TerrainType.River);

            if (i > 0)
            {
                int dirFrom = GetDirection(path[i - 1], path[i]);
                if (dirFrom >= 0) tile.RiverDirections = tile.SetDirectionBit(tile.RiverDirections, dirFrom);
            }
            if (i < path.Length - 1)
            {
                int dirTo = GetDirection(path[i], path[i + 1]);
                if (dirTo >= 0) tile.RiverDirections = tile.SetDirectionBit(tile.RiverDirections, dirTo);
            }

            if (GD.Randf() < 0.2f)
            {
                for (int dir = 0; dir < 6; dir++)
                {
                    var nCoord = HexOverworldTile.GetNeighbor(path[i].X, path[i].Y, dir);
                    var nTile = Grid.GetTile(nCoord.X, nCoord.Y);
                    if (nTile != null && nTile.IsPassable && !nTile.IsRiver && !nTile.IsRoad && GD.Randf() < 0.3f)
                    {
                        nTile.SetTerrain(HexOverworldTile.TerrainType.ShallowWater);
                        nTile.IsRiver = true;
                    }
                }
            }
        }
    }

    // ========================================
    // 第7步: 道路生成
    // ========================================

    private void GenerateRoads()
    {
        var roadNodes = new List<Vector2I>();

        foreach (var region in _regions)
        {
            if (region.DangerLevel > 0.6f) continue;

            var regionTiles = new List<HexOverworldTile>();
            foreach (var t in Grid!.Tiles.Values)
                if (t.RegionName == region.Name && t.IsPassable && !t.IsRiver)
                    regionTiles.Add(t);

            if (regionTiles.Count == 0) continue;
            roadNodes.Add(regionTiles[regionTiles.Count / 2].Coord);
        }

        var passable = Grid!.GetPassableTiles();
        var rng = new Random();
        // 固定额外节点数（旧代码 for 循环与 Count 同时增长导致无限循环 → OOM）
        int initialCount = roadNodes.Count;
        int extraCount = Math.Min(initialCount, 5);
        for (int i = 0; i < extraCount; i++)
        {
            if (passable.Length == 0) break;
            var candidate = passable[rng.Next(passable.Length)];
            if (!candidate.IsRiver)
                roadNodes.Add(candidate.Coord);
        }

        if (roadNodes.Count < 2) return;

        var astar = new HexOverworldAStar { Grid = Grid, IgnorePassability = false };
        ApplyRoadCostModifier();

        var targets = roadNodes.GetRange(1, roadNodes.Count - 1).ToArray();
        var paths = astar.FindPathsToMultiple(roadNodes[0], targets);

        int roadsPlaced = 0;
        foreach (var key in paths.Keys)
        {
            var pathArray = (Godot.Collections.Array)paths[key];
            if (pathArray.Count == 0) continue;
            var path = new Vector2I[pathArray.Count];
            for (int i = 0; i < pathArray.Count; i++) path[i] = (Vector2I)pathArray[i];
            MarkRoadPath(path);
            roadsPlaced++;
        }

        RestoreOriginalCosts();
        GD.Print($"[HexOverworldGenerator] 生成 {roadsPlaced} 条道路");
    }

    private void ApplyRoadCostModifier()
    {
        foreach (var t in Grid!.Tiles.Values)
        {
            t.SetMeta("original_move_cost", t.MoveCost);
            t.MoveCost = t.Terrain switch
            {
                HexOverworldTile.TerrainType.Forest => RoadPenaltyForest,
                HexOverworldTile.TerrainType.DenseForest => RoadPenaltyForest * 1.5f,
                HexOverworldTile.TerrainType.Hills => RoadPenaltyHill,
                HexOverworldTile.TerrainType.Swamp => RoadPenaltySwamp,
                HexOverworldTile.TerrainType.Plains => 1.0f,
                HexOverworldTile.TerrainType.Grassland => 1.0f,
                HexOverworldTile.TerrainType.Savanna => 1.2f,
                _ => t.MoveCost,
            };
        }
    }

    private void RestoreOriginalCosts()
    {
        foreach (var t in Grid!.Tiles.Values)
        {
            if (t.HasMeta("original_move_cost"))
            {
                t.MoveCost = (float)t.GetMeta("original_move_cost");
                t.RemoveMeta("original_move_cost");
            }
        }
    }

    private void MarkRoadPath(Vector2I[] path)
    {
        for (int i = 0; i < path.Length; i++)
        {
            var tile = Grid!.GetTile(path[i].X, path[i].Y);
            if (tile == null || tile.IsRiver) continue;

            tile.IsRoad = true;

            if (i > 0)
            {
                int dirFrom = GetDirection(path[i - 1], path[i]);
                if (dirFrom >= 0) tile.RoadDirections = tile.SetDirectionBit(tile.RoadDirections, dirFrom);
            }
            if (i < path.Length - 1)
            {
                int dirTo = GetDirection(path[i], path[i + 1]);
                if (dirTo >= 0) tile.RoadDirections = tile.SetDirectionBit(tile.RoadDirections, dirTo);
            }

            tile.MoveCost = 0.5f;
        }
    }

    // ========================================
    // 第8步: 后处理
    // ========================================

    private void FinalizeTerrain()
    {
        foreach (var t in Grid!.Tiles.Values)
        {
            t.UpdateTerrainProperties();
            if (t.IsRoad && t.IsPassable) t.MoveCost = 0.5f;
            if (t.IsRiver) { t.IsPassable = false; t.MoveCost = 99.0f; }
        }
    }

    // ========================================
    // 辅助方法
    // ========================================

    private static int GetDirection(Vector2I from, Vector2I to)
    {
        var diffCube = HexOverworldTile.AxialToCube(to.X, to.Y) - HexOverworldTile.AxialToCube(from.X, from.Y);
        for (int i = 0; i < 6; i++)
            if (diffCube == HexOverworldTile.CubeDirections[i]) return i;
        return -1;
    }

    public RegionDef[] GetRegions() => _regions.ToArray();

    public HexOverworldGrid? GetGrid() => Grid;

    // ========================================
    // 增量操作: POI放置
    // ========================================

    /// <summary>在指定位置放置定居点</summary>
    public HexOverworldTile? PlaceSettlementAt(int q, int r, int poiType, string poiName)
    {
        var tile = Grid!.GetTile(q, r);
        if (tile == null) return null;

        if (!tile.IsPassable)
        {
            tile = Grid.FindPassableNearPixel(tile.PixelPos.X, tile.PixelPos.Y, 5);
            if (tile == null) return null;
        }

        tile.HasSettlement = true;
        tile.SettlementType = poiType;
        tile.PoiId = poiName;
        return tile;
    }

    /// <summary>在指定区域内随机找适合放置定居点的瓦片</summary>
    public HexOverworldTile? FindSettlementPosition(string regionName, int minDistance = 10, HexOverworldTile.TerrainType[]? preferredTerrains = null)
    {
        var candidates = new List<HexOverworldTile>();
        var terrainsToMatch = preferredTerrains ?? [
            HexOverworldTile.TerrainType.Plains,
            HexOverworldTile.TerrainType.Grassland,
            HexOverworldTile.TerrainType.Savanna,
        ];
        var matchSet = new HashSet<HexOverworldTile.TerrainType>(terrainsToMatch);

        foreach (var t in Grid!.Tiles.Values)
        {
            if (t.RegionName != regionName || !t.IsPassable || t.IsRiver || t.HasSettlement) continue;
            if (matchSet.Contains(t.Terrain)) candidates.Add(t);
        }

        if (candidates.Count == 0)
        {
            foreach (var t in Grid.Tiles.Values)
                if (t.RegionName == regionName && t.IsPassable && !t.HasSettlement && !t.IsRiver)
                    candidates.Add(t);
        }

        if (candidates.Count == 0) return null;

        // Shuffle
        var rng = new Random();
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        var placed = new List<Vector2I>();
        foreach (var st in Grid.GetSettlementTiles()) placed.Add(st.Coord);

        foreach (var candidate in candidates)
        {
            bool tooClose = false;
            foreach (var p in placed)
            {
                if (HexOverworldTile.HexDistance(candidate.Coord.X, candidate.Coord.Y, p.X, p.Y) < minDistance)
                { tooClose = true; break; }
            }
            if (!tooClose) return candidate;
        }

        return candidates[0];
    }
}
