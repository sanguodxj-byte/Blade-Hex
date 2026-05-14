// WorldCreator.cs
// 世界创建器 — 新游戏时一次性生成完整大陆
// 规则程序化：规则固定（精灵在森林、矮人在山里），布局随种子变化
// 生成后所有 chunk 序列化到磁盘，运行时只做流式加载
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 世界创建配置
/// </summary>
public class WorldCreationConfig
{
    /// <summary>世界大小枚举</summary>
    public enum WorldSize
    {
        Small = 0,   // ~65k tiles, 快速测试
        Medium = 1,  // ~221k tiles, 标准游戏
        Large = 2,   // ~459k tiles, 史诗规模
    }

    /// <summary>世界宽度（chunk 数）</summary>
    public int WorldChunksW { get; set; } = 64;

    /// <summary>世界高度（chunk 数）</summary>
    public int WorldChunksH { get; set; } = 48;

    /// <summary>国家配置列表</summary>
    public List<NationConfig> Nations { get; set; } = new();

    /// <summary>最小生态区面积（用于聚类）</summary>
    public int MinBiomeZoneSize { get; set; } = 200;

    /// <summary>世界 tile 总宽度</summary>
    public int WorldTileWidth => WorldChunksW * ChunkData.ChunkSize;

    /// <summary>世界 tile 总高度</summary>
    public int WorldTileHeight => WorldChunksH * ChunkData.ChunkSize;

    /// <summary>世界大小显示名称</summary>
    public static string[] GetSizeNames() => new[] { "小型", "中型", "大型" };

    /// <summary>世界大小描述</summary>
    public static string[] GetSizeDescriptions() => new[]
    {
        "约 6 万格 — 适合快速体验",
        "约 22 万格 — 标准冒险",
        "约 46 万格 — 史诗征途",
    };

    /// <summary>根据大小枚举创建配置</summary>
    public static WorldCreationConfig Create(WorldSize size, int seed)
    {
        return size switch
        {
            WorldSize.Small => Small(seed),
            WorldSize.Medium => Medium(seed),
            WorldSize.Large => Large(seed),
            _ => Medium(seed),
        };
    }

    /// <summary>大型世界 — 56×32 chunks ≈ 459k tiles</summary>
    public static WorldCreationConfig Large(int seed)
    {
        return new WorldCreationConfig
        {
            WorldChunksW = 56,
            WorldChunksH = 32,
            Nations = NationConfig.GetDefaultNations(),
            MinBiomeZoneSize = 200,
        };
    }

    /// <summary>中型世界 — 36×24 chunks ≈ 221k tiles</summary>
    public static WorldCreationConfig Medium(int seed)
    {
        return new WorldCreationConfig
        {
            WorldChunksW = 36,
            WorldChunksH = 24,
            Nations = NationConfig.GetDefaultNations(),
            MinBiomeZoneSize = 120,
        };
    }

    /// <summary>小型世界 — 21×12 chunks ≈ 65k tiles</summary>
    public static WorldCreationConfig Small(int seed)
    {
        return new WorldCreationConfig
        {
            WorldChunksW = 21,
            WorldChunksH = 12,
            Nations = NationConfig.GetDefaultNations(),
            MinBiomeZoneSize = 50,
        };
    }

    /// <summary>旧接口兼容</summary>
    public static WorldCreationConfig Default(int seed) => Large(seed);
}

/// <summary>
/// 世界数据 — 生成结果的完整容器
/// </summary>
public class WorldData
{
    public int Seed { get; set; }
    public int WorldChunksW { get; set; }
    public int WorldChunksH { get; set; }
    public Dictionary<Vector2I, ChunkData> Chunks { get; set; } = new();
    public List<OverworldPOI> Pois { get; set; } = new();
    public RiverRoadSkeleton? Skeleton { get; set; }
    public List<BiomeZone> Zones { get; set; } = new();
    public Dictionary<string, NationTerritory> Territories { get; set; } = new();
    public List<NationConfig> Nations { get; set; } = new();
    /// <summary>特殊角色（领主 + 冒险者），生成后应收容到 DormantEntityPool</summary>
    public List<OverworldEntity> SpecialCharacters { get; set; } = new();
}

/// <summary>
/// 世界创建器 — 新游戏时一次性生成完整大陆
/// 9 阶段流水线：地形 → 气候 → 生物群落 → 生态聚类 → 国家分配 → 河流道路 → POI → 遭遇 → 序列化
/// </summary>
public class WorldCreator
{
    /// <summary>生成进度回调（0~1）</summary>
    public Action<float, string>? OnProgress;

    /// <summary>
    /// 创建完整世界（新游戏入口）
    /// </summary>
    public WorldData CreateWorld(int seed, WorldCreationConfig config)
    {
        GD.Print($"[WorldCreator] 开始生成世界: seed={seed}, size={config.WorldChunksW}×{config.WorldChunksH} chunks");
        var startTime = Time.GetTicksMsec();

        // Stage 1-3: 生成全部 chunk 地形
        OnProgress?.Invoke(0.0f, "生成地形...");
        var chunks = GenerateAllTerrain(seed, config);
        GD.Print($"[WorldCreator] Stage 1-3 完成: {chunks.Count} chunks 地形生成");

        // Stage 3.5: 地形平滑 — 消除零散小块（< 4 chunk 的孤立地形合并到邻居）
        OnProgress?.Invoke(0.3f, "平滑地形...");
        SmoothIsolatedTerrainPatches(chunks, config);
        GD.Print($"[WorldCreator] Stage 3.5 完成: 地形平滑");

        // Stage 4: 生态区聚类
        OnProgress?.Invoke(0.4f, "分析生态区...");
        var analyzer = new BiomeZoneAnalyzer { MinZoneSize = config.MinBiomeZoneSize };
        var zones = analyzer.Analyze(chunks);
        GD.Print($"[WorldCreator] Stage 4 完成: {zones.Count} 个生态区");

        // Stage 5: 国家版图分配
        OnProgress?.Invoke(0.5f, "分配国家领土...");
        // 根据世界大小缩放 MinTerritoryTiles（小世界降低要求）
        float worldScale = (float)(config.WorldTileWidth * config.WorldTileHeight) / (64 * 16 * 48 * 16); // 相对于默认大世界
        foreach (var nation in config.Nations)
        {
            nation.MinTerritoryTiles = Math.Max(30, (int)(nation.MinTerritoryTiles * worldScale));
        }
        var allocator = new NationAllocator();
        var territories = allocator.AllocateTerritories(zones, config.Nations, seed);
        GD.Print($"[WorldCreator] Stage 5 完成: {territories.Count} 个国家获得领土");

        // Stage 6: 河流生成（直接在 chunk tiles 上，不再使用全局骨架）
        OnProgress?.Invoke(0.6f, "生成河流...");
        int riversGenerated = GenerateRiversDirect(chunks, seed, config);
        GD.Print($"[WorldCreator] Stage 6 完成: {riversGenerated} 条河流");

        // Stage 6.5: 海岛生成
        OnProgress?.Invoke(0.65f, "生成海岛...");
        int islandsGenerated = GenerateIslands(chunks, seed, config);
        GD.Print($"[WorldCreator] Stage 6.5 完成: {islandsGenerated} 个海岛");

        // Stage 7: POI 放置
        OnProgress?.Invoke(0.7f, "放置城镇与据点...");
        var pois = PlacePOIs(chunks, territories, config.Nations, zones, seed);
        GD.Print($"[WorldCreator] Stage 7 完成: {pois.Count} 个 POI");

        // Stage 7.2: 海岛 POI + 渡船航线
        OnProgress?.Invoke(0.74f, "建立港口航线...");
        int islandPois = PlaceIslandPOIs(chunks, pois, seed);
        ConnectFerryRoutes(pois);
        GD.Print($"[WorldCreator] Stage 7.2 完成: {islandPois} 个海岛据点, 渡船航线已连接");

        // Stage 7.5: POI 间道路连接 — 纯 MST，无三角形
        OnProgress?.Invoke(0.78f, "连接聚落道路...");
        ConnectSettlementRoads(chunks, pois, seed);
        GD.Print("[WorldCreator] Stage 7.5 完成: 聚落间道路连接");

        // Stage 7.8: 特殊角色生成（领主 + 冒险者）
        OnProgress?.Invoke(0.82f, "召唤英雄与领主...");
        var specialCharGen = new SpecialCharacterGenerator(seed);
        var specialCharacters = specialCharGen.GenerateAll(config.Nations, territories, pois, config.WorldTileWidth * config.WorldTileHeight);
        GD.Print($"[WorldCreator] Stage 7.8 完成: {specialCharacters.Count} 个特殊角色");

        // Stage 8: 遭遇密度预计算
        OnProgress?.Invoke(0.85f, "计算遭遇分布...");
        PrecomputeEncounterDensity(chunks, territories, zones);

        // Stage 9: 完成
        OnProgress?.Invoke(1.0f, "世界生成完成");
        var elapsed = Time.GetTicksMsec() - startTime;
        GD.Print($"[WorldCreator] 世界生成完成: 耗时 {elapsed}ms");

        return new WorldData
        {
            Seed = seed,
            WorldChunksW = config.WorldChunksW,
            WorldChunksH = config.WorldChunksH,
            Chunks = chunks,
            Pois = pois,
            Skeleton = null,
            Zones = zones,
            Territories = territories,
            Nations = config.Nations,
            SpecialCharacters = specialCharacters,
        };
    }

    // ========================================
    // Stage 1-3: 地形生成
    // ========================================

    private Dictionary<Vector2I, ChunkData> GenerateAllTerrain(int seed, WorldCreationConfig config)
    {
        var generator = new ChunkGenerator();
        generator.Initialize(seed, config.WorldTileWidth, config.WorldTileHeight);

        var chunks = new Dictionary<Vector2I, ChunkData>();
        int total = config.WorldChunksW * config.WorldChunksH;
        int count = 0;

        for (int cq = 0; cq < config.WorldChunksW; cq++)
        {
            for (int cr = 0; cr < config.WorldChunksH; cr++)
            {
                chunks[new Vector2I(cq, cr)] = generator.Generate(cq, cr);
                count++;
                if (count % 100 == 0)
                    OnProgress?.Invoke(0.4f * count / total, $"生成地形 ({count}/{total})...");
            }
        }

        return chunks;
    }

    // ========================================
    // Stage 3.5: 地形平滑 — 消除零散小块
    // ========================================

    /// <summary>
    /// 地形平滑 — 消除零散小块 + 强制逻辑过渡。
    /// 1. 任何小于 20 瓦片的孤立地形区域被合并到周围主导地形
    /// 2. 检查非法邻接（如沙地直接接雪地）并修正
    /// </summary>
    private void SmoothIsolatedTerrainPatches(Dictionary<Vector2I, ChunkData> chunks, WorldCreationConfig config)
    {
        // 收集所有瓦片
        var allTiles = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var chunk in chunks.Values)
            foreach (var kvp in chunk.Tiles)
                allTiles[kvp.Key] = kvp.Value;

        // ========================================
        // Pass 1: 消除小于 20 瓦片的孤立区域
        // ========================================
        // 对所有非水域/非山脉地形做连通分量分析
        var exemptTerrains = new HashSet<HexOverworldTile.TerrainType>
        {
            HexOverworldTile.TerrainType.DeepWater,
            HexOverworldTile.TerrainType.ShallowWater,
            HexOverworldTile.TerrainType.Mountain,
            HexOverworldTile.TerrainType.MountainSnow,
            HexOverworldTile.TerrainType.River,
            HexOverworldTile.TerrainType.Road,
        };

        const int MinClusterSize = 20;

        // 按地形类型分组
        var terrainGroups = new Dictionary<HexOverworldTile.TerrainType, HashSet<Vector2I>>();
        foreach (var (coord, tile) in allTiles)
        {
            if (exemptTerrains.Contains(tile.Terrain)) continue;
            if (!terrainGroups.ContainsKey(tile.Terrain))
                terrainGroups[tile.Terrain] = new HashSet<Vector2I>();
            terrainGroups[tile.Terrain].Add(coord);
        }

        int mergedCount = 0;
        foreach (var (terrainType, tileSet) in terrainGroups)
        {
            var visited = new HashSet<Vector2I>();

            foreach (var start in tileSet)
            {
                if (visited.Contains(start)) continue;

                // BFS 找连通分量
                var cluster = new List<Vector2I>();
                var queue = new Queue<Vector2I>();
                queue.Enqueue(start);
                visited.Add(start);

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    cluster.Add(cur);

                    for (int d = 0; d < 6; d++)
                    {
                        var nb = HexOverworldTile.GetNeighbor(cur.X, cur.Y, d);
                        if (!visited.Contains(nb) && tileSet.Contains(nb))
                        {
                            visited.Add(nb);
                            queue.Enqueue(nb);
                        }
                    }
                }

                // 小于阈值 → 合并到邻居主导地形
                if (cluster.Count < MinClusterSize)
                {
                    var replacement = FindDominantNeighborTerrain(cluster, allTiles, terrainType);
                    foreach (var pos in cluster)
                    {
                        if (allTiles.TryGetValue(pos, out var tile))
                        {
                            tile.Terrain = replacement;
                            tile.Elevation = AdjustElevationForTerrain(replacement, tile.Elevation);
                        }
                    }
                    mergedCount += cluster.Count;
                }
            }
        }

        // ========================================
        // Pass 2: 强制逻辑过渡 — 修正非法邻接
        // ========================================
        int fixedCount = 0;
        // 多轮迭代直到稳定
        for (int pass = 0; pass < 3; pass++)
        {
            int fixedThisPass = 0;
            foreach (var (coord, tile) in allTiles)
            {
                if (exemptTerrains.Contains(tile.Terrain)) continue;

                // 检查是否有非法邻居
                for (int d = 0; d < 6; d++)
                {
                    var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, d);
                    if (!allTiles.TryGetValue(nb, out var nbTile)) continue;

                    if (IsIllegalAdjacency(tile.Terrain, nbTile.Terrain))
                    {
                        // 少数派让步：统计当前瓦片和邻居各自的同类邻居数
                        int mySupport = CountSameTerrainNeighbors(coord, tile.Terrain, allTiles);
                        int nbSupport = CountSameTerrainNeighbors(nb, nbTile.Terrain, allTiles);

                        if (mySupport <= nbSupport)
                        {
                            // 当前瓦片让步 → 变成合法的过渡地形
                            tile.Terrain = GetTransitionTerrain(tile.Terrain, nbTile.Terrain);
                            fixedThisPass++;
                        }
                    }
                }
            }
            fixedCount += fixedThisPass;
            if (fixedThisPass == 0) break;
        }

        // 同步回 chunk
        foreach (var chunk in chunks.Values)
        {
            foreach (var (coord, tile) in chunk.Tiles)
            {
                if (allTiles.TryGetValue(coord, out var updated))
                    tile.Terrain = updated.Terrain;
            }
        }

        GD.Print($"[WorldCreator] 地形平滑: 合并 {mergedCount} 个零散瓦片, 修正 {fixedCount} 个非法邻接");
    }

    /// <summary>判断两种地形是否为非法邻接（不允许直接相邻）</summary>
    private static bool IsIllegalAdjacency(HexOverworldTile.TerrainType a, HexOverworldTile.TerrainType b)
    {
        // 排序确保对称
        if ((int)a > (int)b) (a, b) = (b, a);

        // 沙地/沙漠 不能直接接 雪/冰/针叶林/沼泽
        if (a == HexOverworldTile.TerrainType.Sand &&
            (b == HexOverworldTile.TerrainType.Snow || b == HexOverworldTile.TerrainType.Ice ||
             b == HexOverworldTile.TerrainType.Taiga || b == HexOverworldTile.TerrainType.Bog ||
             b == HexOverworldTile.TerrainType.DenseForest))
            return true;

        // 雪/冰 不能直接接 丛林/沼泽/稀树草原
        if ((a == HexOverworldTile.TerrainType.Snow || a == HexOverworldTile.TerrainType.Ice) &&
            (b == HexOverworldTile.TerrainType.Jungle || b == HexOverworldTile.TerrainType.Swamp ||
             b == HexOverworldTile.TerrainType.Savanna || b == HexOverworldTile.TerrainType.Sand))
            return true;

        // 丛林 不能直接接 针叶林/冻土
        if (a == HexOverworldTile.TerrainType.Jungle &&
            (b == HexOverworldTile.TerrainType.Taiga || b == HexOverworldTile.TerrainType.Bog ||
             b == HexOverworldTile.TerrainType.Rocky))
            return true;

        // 稀树草原 不能直接接 针叶林/冻土/雪
        if (a == HexOverworldTile.TerrainType.Savanna &&
            (b == HexOverworldTile.TerrainType.Taiga || b == HexOverworldTile.TerrainType.Bog ||
             b == HexOverworldTile.TerrainType.Snow || b == HexOverworldTile.TerrainType.Ice))
            return true;

        return false;
    }

    /// <summary>获取两种冲突地形之间的合法过渡地形</summary>
    private static HexOverworldTile.TerrainType GetTransitionTerrain(
        HexOverworldTile.TerrainType from, HexOverworldTile.TerrainType to)
    {
        // 热带 ↔ 寒带 冲突 → 温带过渡
        bool fromHot = from == HexOverworldTile.TerrainType.Sand ||
                       from == HexOverworldTile.TerrainType.Savanna ||
                       from == HexOverworldTile.TerrainType.Jungle ||
                       from == HexOverworldTile.TerrainType.Swamp;
        bool toCold = to == HexOverworldTile.TerrainType.Snow ||
                      to == HexOverworldTile.TerrainType.Ice ||
                      to == HexOverworldTile.TerrainType.Taiga ||
                      to == HexOverworldTile.TerrainType.Bog ||
                      to == HexOverworldTile.TerrainType.Rocky;

        if (fromHot && toCold) return HexOverworldTile.TerrainType.Plains;
        if (!fromHot && !toCold) return HexOverworldTile.TerrainType.Grassland;

        // 沙地 ↔ 森林 → 草地
        if (from == HexOverworldTile.TerrainType.Sand) return HexOverworldTile.TerrainType.Grassland;
        if (from == HexOverworldTile.TerrainType.Savanna) return HexOverworldTile.TerrainType.Plains;
        if (from == HexOverworldTile.TerrainType.Jungle) return HexOverworldTile.TerrainType.Forest;

        // 默认：草地是万能过渡
        return HexOverworldTile.TerrainType.Grassland;
    }

    /// <summary>统计指定坐标周围同类地形的邻居数</summary>
    private static int CountSameTerrainNeighbors(Vector2I coord, HexOverworldTile.TerrainType terrain,
        Dictionary<Vector2I, HexOverworldTile> allTiles)
    {
        int count = 0;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, d);
            if (allTiles.TryGetValue(nb, out var nbTile) && nbTile.Terrain == terrain)
                count++;
        }
        return count;
    }

    /// <summary>调整高程以匹配新地形（避免数据不一致）</summary>
    private static float AdjustElevationForTerrain(HexOverworldTile.TerrainType terrain, float currentElev)
    {
        return terrain switch
        {
            HexOverworldTile.TerrainType.DeepWater => Math.Min(currentElev, 0.25f),
            HexOverworldTile.TerrainType.ShallowWater => Math.Min(currentElev, 0.33f),
            HexOverworldTile.TerrainType.Mountain or HexOverworldTile.TerrainType.MountainSnow => Math.Max(currentElev, 0.8f),
            HexOverworldTile.TerrainType.Hills => Math.Max(currentElev, 0.66f),
            _ => currentElev,
        };
    }

    /// <summary>找到集群边界外最常见的地形类型</summary>
    private static HexOverworldTile.TerrainType FindDominantNeighborTerrain(
        List<Vector2I> cluster, Dictionary<Vector2I, HexOverworldTile> allTiles,
        HexOverworldTile.TerrainType excludeType)
    {
        var clusterSet = new HashSet<Vector2I>(cluster);
        var counts = new Dictionary<HexOverworldTile.TerrainType, int>();

        foreach (var pos in cluster)
        {
            for (int d = 0; d < 6; d++)
            {
                var nb = HexOverworldTile.GetNeighbor(pos.X, pos.Y, d);
                if (clusterSet.Contains(nb)) continue;
                if (!allTiles.TryGetValue(nb, out var nbTile)) continue;
                if (nbTile.Terrain == excludeType) continue;

                counts[nbTile.Terrain] = counts.GetValueOrDefault(nbTile.Terrain, 0) + 1;
            }
        }

        if (counts.Count == 0) return HexOverworldTile.TerrainType.Grassland;

        var best = HexOverworldTile.TerrainType.Grassland;
        int bestCount = 0;
        foreach (var kvp in counts)
        {
            if (kvp.Value > bestCount) { bestCount = kvp.Value; best = kvp.Key; }
        }
        return best;
    }

    // ========================================
    // Stage 6: 河流/道路
    // ========================================

    // ========================================
    // Stage 6: 河流直接生成（在 chunk tiles 上）
    // ========================================

    /// <summary>
    /// 直接在 chunk tiles 上生成河流。
    /// 从高地源头沿下坡路径流向海岸线，河道随流程渐进加宽。
    /// </summary>
    private int GenerateRiversDirect(Dictionary<Vector2I, ChunkData> chunks, int seed, WorldCreationConfig config)
    {
        var rng = new Random(seed ^ 0x52495645); // "RIVE"
        int riverCount = 3 + rng.Next(4); // 3-6 条河流

        // 收集所有 tile 用于寻路
        var allTiles = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var chunk in chunks.Values)
            foreach (var kvp in chunk.Tiles)
                allTiles[kvp.Key] = kvp.Value;

        // 收集高地源头和海岸终点
        var highTiles = new List<Vector2I>();
        var coastTiles = new List<Vector2I>();

        foreach (var (coord, tile) in allTiles)
        {
            if (tile.Elevation > 0.55f && tile.Elevation < 0.75f && tile.IsPassable &&
                tile.Terrain != HexOverworldTile.TerrainType.ShallowWater)
                highTiles.Add(coord);

            if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                coastTiles.Add(coord);
        }

        if (highTiles.Count == 0 || coastTiles.Count == 0) return 0;

        // 洗牌源头
        for (int i = highTiles.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (highTiles[i], highTiles[j]) = (highTiles[j], highTiles[i]);
        }

        int riversPlaced = 0;
        var allRiverTiles = new HashSet<Vector2I>();

        for (int attempt = 0; attempt < riverCount * 5 && riversPlaced < riverCount; attempt++)
        {
            if (attempt >= highTiles.Count) break;
            var source = highTiles[attempt];

            // 找最近海岸
            Vector2I bestCoast = coastTiles[0];
            int bestDist = int.MaxValue;
            foreach (var coast in coastTiles)
            {
                int d = HexOverworldTile.HexDistance(source.X, source.Y, coast.X, coast.Y);
                if (d < bestDist) { bestDist = d; bestCoast = coast; }
            }

            if (bestDist < 15) continue; // 太近海岸，跳过

            // 下坡寻路
            var path = RiverDownhillAStar(source, bestCoast, allTiles, allRiverTiles);
            if (path.Count < 15) continue;

            // 印章河流到 tiles（渐进加宽）
            StampRiverDirect(path, chunks, allRiverTiles);
            riversPlaced++;
        }

        return riversPlaced;
    }

    /// <summary>河流下坡 A* — 强烈偏好下坡，禁止穿越水域和已有河流</summary>
    private static List<Vector2I> RiverDownhillAStar(
        Vector2I start, Vector2I target,
        Dictionary<Vector2I, HexOverworldTile> allTiles,
        HashSet<Vector2I> existingRivers)
    {
        if (!allTiles.ContainsKey(start) || !allTiles.ContainsKey(target))
            return new List<Vector2I>();

        var openQueue = new PriorityQueue<Vector2I, float>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var closed = new HashSet<Vector2I>();

        float heuristic = HexOverworldTile.HexDistance(start.X, start.Y, target.X, target.Y);
        openQueue.Enqueue(start, heuristic);

        int maxIter = 100000;
        int iter = 0;

        while (openQueue.Count > 0 && iter < maxIter)
        {
            iter++;
            var current = openQueue.Dequeue();
            if (closed.Contains(current)) continue;
            closed.Add(current);

            if (current == target)
            {
                // 重建路径
                var path = new List<Vector2I>();
                var node = target;
                while (node != start)
                {
                    path.Add(node);
                    node = cameFrom[node];
                }
                path.Add(start);
                path.Reverse();
                return path;
            }

            float currentElev = allTiles[current].Elevation;
            float currentG = gScore[current];

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (closed.Contains(neighbor)) continue;
                if (!allTiles.TryGetValue(neighbor, out var nTile)) continue;

                // 禁止穿越深水、山脉、已有河流
                if (nTile.Terrain == HexOverworldTile.TerrainType.DeepWater) continue;
                if (nTile.Terrain == HexOverworldTile.TerrainType.Mountain ||
                    nTile.Terrain == HexOverworldTile.TerrainType.MountainSnow) continue;
                if (existingRivers.Contains(neighbor)) continue;

                float neighborElev = nTile.Elevation;
                float elevDiff = neighborElev - currentElev;

                // 代价：下坡便宜，上坡极贵
                float cost;
                if (elevDiff <= 0)
                    cost = 1.0f + neighborElev * 2.0f; // 下坡，偏好低地
                else
                    cost = 1.0f + elevDiff * 50.0f; // 上坡惩罚

                // 浅水（终点）代价低
                if (nTile.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                    cost = 0.5f;

                float tentativeG = currentG + cost;
                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    gScore[neighbor] = tentativeG;
                    cameFrom[neighbor] = current;
                    float h = HexOverworldTile.HexDistance(neighbor.X, neighbor.Y, target.X, target.Y) * 0.5f;
                    openQueue.Enqueue(neighbor, tentativeG + h);
                }
            }
        }

        return new List<Vector2I>();
    }

    /// <summary>将河流路径印章到 chunk tiles，渐进加宽</summary>
    private static void StampRiverDirect(
        List<Vector2I> path,
        Dictionary<Vector2I, ChunkData> chunks,
        HashSet<Vector2I> allRiverTiles)
    {
        int totalLen = path.Count;

        for (int i = 0; i < totalLen; i++)
        {
            var coord = path[i];
            var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(coord.X, coord.Y);
            if (tile == null) continue;

            // 不覆盖已有道路
            if (tile.IsRoad) continue;

            tile.IsRiver = true;
            tile.SetTerrain(HexOverworldTile.TerrainType.River);
            allRiverTiles.Add(coord);

            // 方向位掩码
            if (i > 0)
            {
                int dirFrom = GetRoadDirection(path[i - 1], coord);
                if (dirFrom >= 0) tile.RiverDirections = tile.SetDirectionBit(tile.RiverDirections, dirFrom);
            }
            if (i < totalLen - 1)
            {
                int dirTo = GetRoadDirection(coord, path[i + 1]);
                if (dirTo >= 0) tile.RiverDirections = tile.SetDirectionBit(tile.RiverDirections, dirTo);
            }

            // 渐进加宽
            float progress = (float)i / totalLen;
            int width = progress < 0.4f ? 1 : progress < 0.75f ? 2 : 3;

            if (width >= 2 && i < totalLen - 1)
            {
                int flowDir = GetRoadDirection(coord, path[i + 1]);
                if (flowDir < 0) flowDir = 0;
                int perpDir1 = (flowDir + 2) % 6;
                int perpDir2 = (flowDir + 4) % 6;

                var side1 = HexOverworldTile.GetNeighbor(coord.X, coord.Y, perpDir1);
                StampRiverTile(side1, chunks, allRiverTiles);

                if (width >= 3)
                {
                    var side2 = HexOverworldTile.GetNeighbor(coord.X, coord.Y, perpDir2);
                    StampRiverTile(side2, chunks, allRiverTiles);
                }
            }
        }
    }

    /// <summary>将单个 tile 标记为河流（辅助）</summary>
    private static void StampRiverTile(Vector2I coord, Dictionary<Vector2I, ChunkData> chunks, HashSet<Vector2I> allRiverTiles)
    {
        var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
        if (!chunks.TryGetValue(chunkCoord, out var chunk)) return;
        var tile = chunk.GetTile(coord.X, coord.Y);
        if (tile == null || tile.IsRoad || tile.IsRiver) return;
        if (tile.Terrain == HexOverworldTile.TerrainType.DeepWater) return;

        tile.IsRiver = true;
        tile.SetTerrain(HexOverworldTile.TerrainType.River);
        allRiverTiles.Add(coord);
    }

    // ========================================
    // Stage 6.5: 海岛生成
    // ========================================

    /// <summary>
    /// 在深水区域中生成小型可探索岛屿。
    /// 每 10000 格海洋生成约 1 个海岛，每个海岛 5-12 格。
    /// 返回生成的岛屿数量，岛屿中心坐标存入 _islandCenters 供后续 POI 放置使用。
    /// </summary>
    private readonly List<Vector2I> _islandCenters = new();

    private int GenerateIslands(Dictionary<Vector2I, ChunkData> chunks, int seed, WorldCreationConfig config)
    {
        var rng = new Random(seed ^ 0x49534C44); // "ISLD"
        _islandCenters.Clear();

        // 统计深水格数量
        int deepWaterCount = 0;
        var deepWaterTiles = new List<Vector2I>();

        foreach (var chunk in chunks.Values)
        {
            foreach (var (coord, tile) in chunk.Tiles)
            {
                if (tile.Terrain == HexOverworldTile.TerrainType.DeepWater)
                {
                    deepWaterCount++;
                    // 稀疏采样（每 50 格取一个候选点，减少内存）
                    if (deepWaterCount % 50 == 0)
                        deepWaterTiles.Add(coord);
                }
            }
        }

        // 每 10000 格深水生成 1 个海岛
        int targetIslands = Math.Max(1, deepWaterCount / 10000);
        targetIslands = Math.Min(targetIslands, 12); // 最多 12 个

        if (deepWaterTiles.Count == 0) return 0;

        int islandsPlaced = 0;
        var usedPositions = new HashSet<Vector2I>();

        for (int attempt = 0; attempt < targetIslands * 5 && islandsPlaced < targetIslands; attempt++)
        {
            var center = deepWaterTiles[rng.Next(deepWaterTiles.Count)];

            // 检查距离已有岛屿和大陆足够远
            if (!IsValidIslandPosition(center, chunks, usedPositions)) continue;

            // 生成岛屿（5-12 格）
            int islandSize = 5 + rng.Next(8);
            var islandTiles = GenerateIslandShape(center, islandSize, rng);

            // 将岛屿 tile 写入 chunk
            var islandTerrain = GetIslandTerrain(rng);
            foreach (var coord in islandTiles)
            {
                var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
                if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
                var tile = chunk.GetTile(coord.X, coord.Y);
                if (tile == null) continue;

                tile.SetTerrain(islandTerrain);
                tile.IsPassable = true;
                tile.MoveCost = 1.0f;
                usedPositions.Add(coord);
            }

            _islandCenters.Add(center);
            islandsPlaced++;
        }

        return islandsPlaced;
    }

    /// <summary>检查位置是否适合放置海岛（远离大陆和其他岛屿）</summary>
    private bool IsValidIslandPosition(Vector2I center, Dictionary<Vector2I, ChunkData> chunks, HashSet<Vector2I> usedPositions)
    {
        // 距离已有岛屿至少 15 格
        foreach (var used in usedPositions)
        {
            if (HexOverworldTile.HexDistance(center.X, center.Y, used.X, used.Y) < 15)
                return false;
        }

        // 距离大陆至少 6 格（检查周围是否有非水域格子）
        var centerCube = HexOverworldTile.AxialToCube(center.X, center.Y);
        for (int ring = 1; ring <= 6; ring++)
        {
            var ringTiles = HexOverworldTile.CubeRing(centerCube, ring);
            foreach (var cube in ringTiles)
            {
                var axial = HexOverworldTile.CubeToAxial(cube);
                var chunkCoord = ChunkData.WorldToChunk(axial.X, axial.Y);
                if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
                var tile = chunk.GetTile(axial.X, axial.Y);
                if (tile != null && tile.Terrain != HexOverworldTile.TerrainType.DeepWater &&
                    tile.Terrain != HexOverworldTile.TerrainType.ShallowWater)
                    return false; // 太近大陆
            }
        }

        return true;
    }

    /// <summary>生成岛屿形状（从中心 BFS 扩展）</summary>
    private List<Vector2I> GenerateIslandShape(Vector2I center, int size, Random rng)
    {
        var island = new List<Vector2I> { center };
        var frontier = new List<Vector2I>();

        // 添加中心的邻居到前沿
        for (int d = 0; d < 6; d++)
            frontier.Add(HexOverworldTile.GetNeighbor(center.X, center.Y, d));

        var used = new HashSet<Vector2I> { center };

        while (island.Count < size && frontier.Count > 0)
        {
            int idx = rng.Next(frontier.Count);
            var next = frontier[idx];
            frontier.RemoveAt(idx);

            if (used.Contains(next)) continue;
            used.Add(next);
            island.Add(next);

            // 添加新邻居到前沿（概率递减，让形状不规则）
            for (int d = 0; d < 6; d++)
            {
                var nb = HexOverworldTile.GetNeighbor(next.X, next.Y, d);
                if (!used.Contains(nb) && rng.NextDouble() < 0.6)
                    frontier.Add(nb);
            }
        }

        return island;
    }

    /// <summary>随机选择岛屿地形</summary>
    private static HexOverworldTile.TerrainType GetIslandTerrain(Random rng)
    {
        return rng.Next(4) switch
        {
            0 => HexOverworldTile.TerrainType.Sand,
            1 => HexOverworldTile.TerrainType.Plains,
            2 => HexOverworldTile.TerrainType.Forest,
            3 => HexOverworldTile.TerrainType.Rocky,
            _ => HexOverworldTile.TerrainType.Sand,
        };
    }

    // ========================================
    // Stage 7.2: 海岛 POI + 渡船航线
    // ========================================

    /// <summary>
    /// 在每个海岛上放置特殊 POI。
    /// 岛屿 POI 类型丰富：海盗巢穴、沉船遗迹、隐士祭坛、走私港口、海兽巢穴、
    /// 远古灯塔、流放者营地等。每个岛屿根据加权随机获得独特主题。
    /// </summary>
    private int PlaceIslandPOIs(Dictionary<Vector2I, ChunkData> chunks, List<OverworldPOI> pois, int seed)
    {
        if (_islandCenters.Count == 0) return 0;

        var rng = new Random(seed ^ 0x49504F49); // "IPOI"
        int placed = 0;

        // 确保至少有 1 个港口岛（用于渡船网络）
        bool hasPort = false;

        for (int i = 0; i < _islandCenters.Count; i++)
        {
            var center = _islandCenters[i];

            // 验证岛屿中心 tile 存在且可通行
            var chunkCoord = ChunkData.WorldToChunk(center.X, center.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(center.X, center.Y);
            if (tile == null || !tile.IsPassable) continue;

            var poi = new OverworldPOI();
            poi.Position = HexOverworldTile.AxialToPixel(center.X, center.Y);
            poi.OwningFaction = "";

            // 第一个岛屿强制港口（保证渡船网络可达）
            // 之后每 3 个岛屿放 1 个港口，其余为特殊探索点
            if (!hasPort || (i > 0 && i % 3 == 0))
            {
                BuildIslandPort(poi, rng);
                hasPort = true;
            }
            else
            {
                BuildIslandSpecialPOI(poi, rng);
            }

            pois.Add(poi);
            placed++;
        }

        return placed;
    }

    /// <summary>生成海岛港口 POI</summary>
    private static void BuildIslandPort(OverworldPOI poi, Random rng)
    {
        string[] portNames = ["走私者港湾", "自由港", "海风码头", "潮汐锚地", "漂流者避风港"];
        poi.PoiName = portNames[rng.Next(portNames.Length)];
        poi.PoiTypeEnum = OverworldPOI.POIType.Port;
        poi.HasTavern = true;
        poi.HasShop = true;
        poi.FerryCost = 30 + rng.Next(40);
        poi.GarrisonMax = 8;
        poi.GarrisonCurrent = 8;
        poi.Prosperity = 25 + rng.Next(35);
    }

    /// <summary>
    /// 生成海岛特殊 POI — 加权随机从多种主题中选择。
    /// 每种主题有独特的名称、类型、属性和探索奖励暗示。
    /// </summary>
    private static void BuildIslandSpecialPOI(OverworldPOI poi, Random rng)
    {
        // 加权随机：海盗(25%) / 沉船遗迹(20%) / 隐士祭坛(15%) / 海兽巢穴(15%) /
        //           远古灯塔(10%) / 流放者营地(10%) / 宝藏岛(5%)
        int roll = rng.Next(100);

        if (roll < 25)
        {
            // 海盗巢穴 — 高威胁，有战利品
            string[] names = ["黑帆海盗窝", "血骷髅洞穴", "暴风海寇巢", "深渊劫掠者"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.PirateCove;
            poi.LairLevel = 2 + rng.Next(3);
            poi.ThreatLevel = 0.6f + (float)rng.NextDouble() * 0.3f;
        }
        else if (roll < 45)
        {
            // 沉船遗迹 — 中等威胁，探索型
            string[] names = ["沉没的商船", "幽灵帆船残骸", "远古战舰遗骨", "深海宝藏船"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.Ruins;
            poi.LairLevel = 1 + rng.Next(3);
            poi.ThreatLevel = 0.4f + (float)rng.NextDouble() * 0.3f;
        }
        else if (roll < 60)
        {
            // 隐士祭坛 — 无威胁，提供祝福/治疗
            string[] names = ["海神祭坛", "隐士灯塔", "潮汐圣所", "珊瑚神殿"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Shrine;
            poi.Prosperity = 20;
            poi.GarrisonMax = 0;
            poi.GarrisonCurrent = 0;
        }
        else if (roll < 75)
        {
            // 海兽巢穴 — 高威胁 boss 战
            string[] names = ["海蛇巢穴", "巨蟹洞窟", "深渊利维坦巢", "海妖礁石"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.DragonLair; // 复用龙巢模板（海兽 boss）
            poi.LairLevel = 3 + rng.Next(3);
            poi.ThreatLevel = 0.8f + (float)rng.NextDouble() * 0.2f;
        }
        else if (roll < 85)
        {
            // 远古灯塔 — 探索/谜题型，中等威胁
            string[] names = ["远古灯塔", "星辰观测台", "风暴信标塔", "迷雾引路灯"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.Ruins;
            poi.LairLevel = 2 + rng.Next(2);
            poi.ThreatLevel = 0.3f + (float)rng.NextDouble() * 0.3f;
        }
        else if (roll < 95)
        {
            // 流放者营地 — 可交互的中立聚落
            string[] names = ["流放者营地", "海难幸存者", "逃亡者避难所", "无法之地"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Tavern; // 复用旅店类型（可休息/招募）
            poi.HasTavern = true;
            poi.GarrisonMax = 5;
            poi.GarrisonCurrent = 5;
            poi.Prosperity = 15 + rng.Next(20);
        }
        else
        {
            // 宝藏岛 — 稀有，高奖励无守卫（已被清空或陷阱型）
            string[] names = ["藏宝洞穴", "海盗埋骨地", "黄金沙滩", "失落的宝库"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.AncientTomb;
            poi.LairLevel = 1 + rng.Next(2);
            poi.ThreatLevel = 0.2f + (float)rng.NextDouble() * 0.2f;
        }
    }

    /// <summary>
    /// 连接渡船航线 — 每个港口连接最近的 1-2 个其他港口。
    /// 大陆港口连接海岛港口，海岛港口互相连接。
    /// </summary>
    private static void ConnectFerryRoutes(List<OverworldPOI> pois)
    {
        var ports = pois.Where(p => p.PoiTypeEnum == OverworldPOI.POIType.Port).ToList();
        if (ports.Count < 2) return;

        foreach (var port in ports)
        {
            // 找最近的 2 个其他港口
            var others = ports
                .Where(p => p != port)
                .OrderBy(p => port.Position.DistanceTo(p.Position))
                .Take(2)
                .ToList();

            foreach (var other in others)
            {
                if (!port.FerryDestinations.Contains(other.PoiName))
                    port.FerryDestinations.Add(other.PoiName);

                // 双向连接
                if (!other.FerryDestinations.Contains(port.PoiName))
                    other.FerryDestinations.Add(port.PoiName);
            }
        }

        GD.Print($"[WorldCreator] 渡船航线: {ports.Count} 个港口已互联");
    }

    // ========================================
    // Stage 7: POI 放置
    // ========================================

    private List<OverworldPOI> PlacePOIs(
        Dictionary<Vector2I, ChunkData> chunks,
        Dictionary<string, NationTerritory> territories,
        List<NationConfig> nations,
        List<BiomeZone> zones,
        int seed)
    {
        var pois = new List<OverworldPOI>();
        var rng = new Random(seed ^ 0x504F49); // "POI"
        var usedPositions = new HashSet<Vector2I>();

        // 为每个有领土的国家放置 POI
        foreach (var nation in nations)
        {
            if (!territories.TryGetValue(nation.Id, out var territory)) continue;

            int poiCount = Math.Max(1, (int)(territory.TotalTiles * nation.PoiDensityPer1000Tiles / 1000.0f));

            // 放置首都（在核心区域中心附近）
            var capital = PlaceCapital(nation, territory, chunks, rng, usedPositions);
            if (capital != null) pois.Add(capital);

            // 放置其他 POI
            for (int i = 0; i < poiCount - 1; i++)
            {
                var poi = PlaceNationPOI(nation, territory, chunks, rng, usedPositions, i);
                if (poi != null) pois.Add(poi);
            }
        }

        // 放置野外 POI（在未分配区域）
        PlaceWildPOIs(chunks, zones, territories, pois, rng, usedPositions);

        return pois;
    }

    private OverworldPOI? PlaceCapital(
        NationConfig nation,
        NationTerritory territory,
        Dictionary<Vector2I, ChunkData> chunks,
        Random rng,
        HashSet<Vector2I> usedPositions)
    {
        var pos = FindValidPosition(territory.CoreZone.Centroid, territory.AllTiles, chunks, rng, usedPositions, 20);
        if (pos == null) return null;

        usedPositions.Add(pos.Value);
        var poi = new OverworldPOI();
        poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City);
        poi.PoiTypeEnum = OverworldPOI.POIType.Town;
        poi.Position = HexOverworldTile.AxialToPixel(pos.Value.X, pos.Value.Y);
        poi.OwningFaction = nation.Id;
        poi.HasTavern = true;
        poi.HasShop = true;
        poi.HasBlacksmith = true;
        poi.HasQuestBoard = true;
        poi.GarrisonMax = 50;
        poi.GarrisonCurrent = 50;
        poi.Prosperity = 80;
        return poi;
    }

    private OverworldPOI? PlaceNationPOI(
        NationConfig nation,
        NationTerritory territory,
        Dictionary<Vector2I, ChunkData> chunks,
        Random rng,
        HashSet<Vector2I> usedPositions,
        int index)
    {
        // 在领土内随机找一个有效位置
        var tileList = territory.AllTiles.ToList();
        var center = tileList[rng.Next(tileList.Count)];
        var pos = FindValidPosition(center, territory.AllTiles, chunks, rng, usedPositions, 20);
        if (pos == null) return null;

        usedPositions.Add(pos.Value);

        // 根据 index 和地形决定 POI 类型分布:
        // 首都已单独放置，这里从 index 0 开始
        // 0: 城镇, 1: 城堡, 2-3: 村庄, 4+: 混合小型设施
        // 特殊：沿海位置强制生成港口
        var poi = new OverworldPOI();
        poi.Position = HexOverworldTile.AxialToPixel(pos.Value.X, pos.Value.Y);
        poi.OwningFaction = nation.Id;
        poi.HasQuestBoard = true;

        // 沿海位置 → 强制生成港口（每个国家最多 1 个大陆港口）
        if (IsCoastalTile(pos.Value, chunks) && !_portsPlaced.Contains(nation.Id))
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City) + "港";
            poi.PoiTypeEnum = OverworldPOI.POIType.Port;
            poi.HasTavern = true;
            poi.HasShop = true;
            poi.FerryCost = 40 + rng.Next(30);
            poi.GarrisonMax = 20;
            poi.GarrisonCurrent = 20;
            poi.Prosperity = 50 + rng.Next(30);
            _portsPlaced.Add(nation.Id);
            return poi;
        }

        if (index < 2)
        {
            poi.GarrisonMax = 20;
            poi.GarrisonCurrent = 15;
        }
        else if (index < 1)
        {
            // 城镇
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City);
            poi.PoiTypeEnum = OverworldPOI.POIType.Town;
            poi.HasTavern = true;
            poi.HasShop = true;
            poi.HasBlacksmith = true;
            poi.GarrisonMax = 30;
            poi.GarrisonCurrent = 30;
            poi.Prosperity = 60 + rng.Next(20);
        }
        else if (index < 2)
        {
            // 城堡
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Fortress);
            poi.PoiTypeEnum = OverworldPOI.POIType.Castle;
            poi.HasBarracks = true;
            poi.HasBlacksmith = true;
            poi.GarrisonMax = 50 + rng.Next(30);
            poi.GarrisonCurrent = poi.GarrisonMax;
            poi.CastleDefenseLevel = 1 + rng.Next(2);
            poi.Prosperity = 40 + rng.Next(20);
        }
        else if (index < 5)
        {
            // 村庄
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village);
            poi.PoiTypeEnum = OverworldPOI.POIType.Village;
            poi.HasTavern = rng.Next(2) == 0;
            poi.GarrisonMax = 10;
            poi.GarrisonCurrent = 10;
            poi.Prosperity = 30 + rng.Next(30);
        }
        else
        {
            // 混合小型设施
            int subType = (index - 7) % 5;
            switch (subType)
            {
                case 0: // 旅店
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Tavern);
                    poi.PoiTypeEnum = OverworldPOI.POIType.Tavern;
                    poi.HasTavern = true;
                    poi.HasShop = true;
                    poi.GarrisonMax = 5;
                    poi.GarrisonCurrent = 5;
                    poi.Prosperity = 30 + rng.Next(20);
                    break;
                case 1: // 前哨站
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Fortress);
                    poi.PoiTypeEnum = OverworldPOI.POIType.Outpost;
                    poi.HasBarracks = true;
                    poi.GarrisonMax = 15;
                    poi.GarrisonCurrent = 15;
                    poi.Prosperity = 20;
                    break;
                case 2: // 矿场
                    poi.PoiName = $"{POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village)}矿场";
                    poi.PoiTypeEnum = OverworldPOI.POIType.Mine;
                    poi.GarrisonMax = 8;
                    poi.GarrisonCurrent = 8;
                    poi.Prosperity = 40 + rng.Next(20);
                    break;
                case 3: // 农庄
                    poi.PoiName = $"{POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village)}农庄";
                    poi.PoiTypeEnum = OverworldPOI.POIType.Farm;
                    poi.GarrisonMax = 5;
                    poi.GarrisonCurrent = 5;
                    poi.Prosperity = 35 + rng.Next(15);
                    break;
                case 4: // 药师所
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Monastery);
                    poi.PoiTypeEnum = OverworldPOI.POIType.Shrine;
                    poi.GarrisonMax = 3;
                    poi.GarrisonCurrent = 3;
                    poi.Prosperity = 25;
                    break;
            }
        }

        return poi;
    }

    private void PlaceWildPOIs(
        Dictionary<Vector2I, ChunkData> chunks,
        List<BiomeZone> zones,
        Dictionary<string, NationTerritory> territories,
        List<OverworldPOI> pois,
        Random rng,
        HashSet<Vector2I> usedPositions)
    {
        var assignedTiles = new HashSet<Vector2I>();
        foreach (var t in territories.Values)
            assignedTiles.UnionWith(t.AllTiles);

        // 在未分配的大生态区放置野外 POI
        var wildZones = zones.Where(z => !z.IsAssigned && z.TileCount >= 100).ToList();

        foreach (var zone in wildZones)
        {
            // 根据生态类型决定放什么
            string poiType = zone.DominantBiome switch
            {
                BiomeType.Mountain => "dragon_lair",
                BiomeType.Swamp => "ancient_tomb",
                BiomeType.Forest => "bandit_camp",
                BiomeType.Tundra => "ruins",
                _ => "resource_node",
            };

            var pos = FindValidPosition(zone.Centroid, zone.TileCoords, chunks, rng, usedPositions, 15);
            if (pos == null) continue;

            usedPositions.Add(pos.Value);
            var poi = new OverworldPOI();
            poi.PoiName = poiType switch
            {
                "dragon_lair" => "龙巢",
                "ancient_tomb" => "古墓",
                "bandit_camp" => "土匪营地",
                "ruins" => "远古废墟",
                _ => "资源点",
            };
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.Position = HexOverworldTile.AxialToPixel(pos.Value.X, pos.Value.Y);
            poi.OwningFaction = "";
            pois.Add(poi);
        }
    }

    /// <summary>检查一个格子是否沿海（邻居有水域）</summary>
    private static bool IsCoastalTile(Vector2I coord, Dictionary<Vector2I, ChunkData> chunks)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, dir);
            var chunkCoord = ChunkData.WorldToChunk(nb.X, nb.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(nb.X, nb.Y);
            if (tile == null) continue;
            if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
                tile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                tile.Terrain == HexOverworldTile.TerrainType.Sand)
                return true;
        }
        return false;
    }

    /// <summary>检查国家领土内是否已有港口</summary>
    private bool HasPortInNation(NationTerritory territory, Dictionary<Vector2I, ChunkData> chunks, HashSet<Vector2I> usedPositions)
    {
        // 简单检查：如果已放置的POI中有Port类型就返回true
        // 这里用一个简单的标记（通过usedPositions无法判断类型，所以用字段跟踪）
        return _portsPlaced.Contains(territory.NationId);
    }

    private readonly HashSet<string> _portsPlaced = new();

    /// <summary>
    /// 在指定区域内找一个有效的 POI 放置位置
    /// 要求：可通行、不在水上、距离已有 POI 足够远
    /// </summary>
    private Vector2I? FindValidPosition(
        Vector2I center,
        HashSet<Vector2I> validTiles,
        Dictionary<Vector2I, ChunkData> chunks,
        Random rng,
        HashSet<Vector2I> usedPositions,
        int minDistance)
    {
        // 从中心开始螺旋搜索
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int offsetQ = rng.Next(-30, 31);
            int offsetR = rng.Next(-30, 31);
            var candidate = new Vector2I(center.X + offsetQ, center.Y + offsetR);

            if (!validTiles.Contains(candidate)) continue;

            // 检查 tile 是否可通行且不在水上
            var chunkCoord = ChunkData.WorldToChunk(candidate.X, candidate.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(candidate.X, candidate.Y);
            if (tile == null || !tile.IsPassable) continue;
            // 排除水域地形 — POI 不应放在水上
            if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
                tile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                tile.Terrain == HexOverworldTile.TerrainType.River ||
                tile.Terrain == HexOverworldTile.TerrainType.Ice) continue;

            // 检查距离已有 POI
            bool tooClose = false;
            foreach (var used in usedPositions)
            {
                if (candidate.DistanceTo(used) < minDistance) { tooClose = true; break; }
            }
            if (tooClose) continue;

            return candidate;
        }

        return null;
    }

    // ========================================
    // Stage 7.5: 聚落间道路连接
    // ========================================

    /// <summary>
    /// 在城镇/村庄/城堡之间用 A* 寻路连接道路。
    /// 用 Prim MST 确保连通性，然后对每条边做 tile 级 A* 寻路，
    /// 将路径上的瓦片标记为 IsRoad=true, MoveCost=0.5。
    /// </summary>
    private void ConnectSettlementRoads(
        Dictionary<Vector2I, ChunkData> chunks,
        List<OverworldPOI> pois,
        int seed)
    {
        // 筛选可连接的聚落（城镇/村庄/城堡/前哨站/旅店/港口 — 有人居住的地方）
        var settlements = pois
            .Where(p => p.PoiTypeEnum == OverworldPOI.POIType.Town
                     || p.PoiTypeEnum == OverworldPOI.POIType.Village
                     || p.PoiTypeEnum == OverworldPOI.POIType.Castle
                     || p.PoiTypeEnum == OverworldPOI.POIType.Outpost
                     || p.PoiTypeEnum == OverworldPOI.POIType.Tavern
                     || p.PoiTypeEnum == OverworldPOI.POIType.Port)
            .ToList();

        if (settlements.Count < 2) return;

        // 只连接最近邻居 — 每个 POI 最多连接 2-3 个最近的
        var edges = BuildNearestNeighborRoads(settlements, seed);

        // 构建全局瓦片查找表
        var allTiles = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var chunk in chunks.Values)
            foreach (var kvp in chunk.Tiles)
                allTiles[kvp.Key] = kvp.Value;

        int roadsStamped = 0;
        foreach (var (from, to) in edges)
        {
            var fromAxial = HexOverworldTile.PixelToAxial(from.Position.X, from.Position.Y);
            var toAxial = HexOverworldTile.PixelToAxial(to.Position.X, to.Position.Y);

            var path = RoadAStar(fromAxial, toAxial, allTiles);
            if (path.Count >= 2)
            {
                StampRoadPath(path, chunks);
                roadsStamped++;
            }
        }

        GD.Print($"[WorldCreator] 聚落道路: {roadsStamped}/{edges.Count} 条连接 {settlements.Count} 个聚落");
    }

    /// <summary>
    /// 最近邻居道路网络 — 纯 MST，不添加额外边。
    /// MST 本身就是连接所有节点的最短总距离树，不会产生三角形。
    /// </summary>
    private static List<(OverworldPOI, OverworldPOI)> BuildNearestNeighborRoads(List<OverworldPOI> settlements, int seed)
    {
        var edges = new List<(OverworldPOI, OverworldPOI)>();
        if (settlements.Count < 2) return edges;

        // 纯 Prim MST — 保证连通性且无三角形（树结构不可能有环）
        var inTree = new HashSet<int> { 0 };
        var candidates = new HashSet<int>();
        for (int i = 1; i < settlements.Count; i++) candidates.Add(i);

        while (candidates.Count > 0)
        {
            float bestDist = float.MaxValue;
            int bestFrom = -1, bestTo = -1;

            foreach (int from in inTree)
            {
                foreach (int to in candidates)
                {
                    float d = settlements[from].Position.DistanceTo(settlements[to].Position);
                    if (d < bestDist) { bestDist = d; bestFrom = from; bestTo = to; }
                }
            }

            if (bestTo < 0) break;
            edges.Add((settlements[bestFrom], settlements[bestTo]));
            inTree.Add(bestTo);
            candidates.Remove(bestTo);
        }

        return edges;
    }

    /// <summary>
    /// 道路专用 A* — 在全局瓦片上寻路，偏好平坦地形。
    /// 代价：平原/草地=1, 森林=3, 丘陵=4, 沼泽=6, 已有道路=0.3
    /// 优化: 使用 PriorityQueue 替代 List 线性扫描 (O(log n) vs O(n) 出队)
    /// </summary>
    private static List<Vector2I> RoadAStar(Vector2I start, Vector2I end, Dictionary<Vector2I, HexOverworldTile> allTiles)
    {
        if (start == end) return new List<Vector2I> { start };

        // 如果起点或终点不在瓦片表中，直接返回空
        if (!allTiles.ContainsKey(start) || !allTiles.ContainsKey(end))
            return new List<Vector2I>();

        var openQueue = new PriorityQueue<Vector2I, float>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0 };
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var closed = new HashSet<Vector2I>();

        openQueue.Enqueue(start, HeuristicDist(start, end));

        int maxIter = 50000;
        int iter = 0;

        while (openQueue.Count > 0 && iter < maxIter)
        {
            iter++;

            var current = openQueue.Dequeue();

            if (closed.Contains(current)) continue;
            closed.Add(current);

            if (current == end)
            {
                var path = new List<Vector2I>();
                var node = end;
                while (node != start)
                {
                    path.Add(node);
                    node = cameFrom[node];
                }
                path.Add(start);
                path.Reverse();
                return path;
            }

            float currentG = gScore[current];

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (closed.Contains(neighbor)) continue;
                if (!allTiles.TryGetValue(neighbor, out var nTile)) continue;
                if (!nTile.IsPassable) continue;
                // 道路不应穿越任何水域
                if (nTile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
                    nTile.Terrain == HexOverworldTile.TerrainType.DeepWater) continue;

                float moveCost = GetRoadBuildCost(nTile);
                float tentativeG = currentG + moveCost;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    gScore[neighbor] = tentativeG;
                    cameFrom[neighbor] = current;
                    float f = tentativeG + HeuristicDist(neighbor, end);
                    openQueue.Enqueue(neighbor, f);
                }
            }
        }

        return new List<Vector2I>(); // 无法连通或超时
    }

    /// <summary>
    /// 道路建设代价 — 纯粹基于地形修路难度，不考虑已有道路。
    /// 道路生成是按顺序进行的，如果给已有道路折扣，后生成的道路会被先生成的吸引，
    /// 导致路径绕弯去蹭已有道路，形成三角形/8字形。
    /// 因此修路 A* 只看"这块地形修路有多难"，已有道路视为普通平原。
    /// </summary>
    private static float GetRoadBuildCost(HexOverworldTile tile)
    {
        // 已有道路 = 平原代价（不给折扣，避免绕路吸引）
        if (tile.IsRoad) return 1.0f;
        return tile.Terrain switch
        {
            HexOverworldTile.TerrainType.Road => 1.0f,
            HexOverworldTile.TerrainType.Plains => 1.0f,
            HexOverworldTile.TerrainType.Grassland => 1.0f,
            HexOverworldTile.TerrainType.Savanna => 1.2f,
            HexOverworldTile.TerrainType.Wasteland => 1.5f,
            HexOverworldTile.TerrainType.Sand => 2.0f,
            HexOverworldTile.TerrainType.Taiga => 2.5f,
            HexOverworldTile.TerrainType.Snow => 3.0f,
            HexOverworldTile.TerrainType.Forest => 4.0f,
            HexOverworldTile.TerrainType.Hills => 4.0f,
            HexOverworldTile.TerrainType.Rocky => 5.0f,
            HexOverworldTile.TerrainType.DenseForest => 6.0f,
            HexOverworldTile.TerrainType.Jungle => 7.0f,
            HexOverworldTile.TerrainType.Swamp or HexOverworldTile.TerrainType.Bog => 8.0f,
            HexOverworldTile.TerrainType.ShallowWater => 15.0f,
            HexOverworldTile.TerrainType.Ice => 10.0f,
            _ => 3.0f,
        };
    }

    /// <summary>六边形距离启发式</summary>
    private static float HeuristicDist(Vector2I a, Vector2I b)
    {
        int dq = Math.Abs(a.X - b.X);
        int dr = Math.Abs(a.Y - b.Y);
        int ds = Math.Abs((-a.X - a.Y) - (-b.X - b.Y));
        return (dq + dr + ds) / 2.0f;
    }

    /// <summary>将路径标记为道路瓦片</summary>
    private static void StampRoadPath(List<Vector2I> path, Dictionary<Vector2I, ChunkData> chunks)
    {
        for (int i = 0; i < path.Count; i++)
        {
            var coord = path[i];
            var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;

            var tile = chunk.GetTile(coord.X, coord.Y);
            if (tile == null) continue;

            tile.IsRoad = true;
            tile.MoveCost = 0.2f;

            // 方向位掩码
            if (i > 0)
            {
                int dirFrom = GetRoadDirection(path[i - 1], coord);
                if (dirFrom >= 0) tile.RoadDirections = SetBit(tile.RoadDirections, dirFrom);
            }
            if (i < path.Count - 1)
            {
                int dirTo = GetRoadDirection(coord, path[i + 1]);
                if (dirTo >= 0) tile.RoadDirections = SetBit(tile.RoadDirections, dirTo);
            }
        }
    }

    /// <summary>计算两个相邻六边形之间的方向 (0-5)</summary>
    private static int GetRoadDirection(Vector2I from, Vector2I to)
    {
        int dq = to.X - from.X;
        int dr = to.Y - from.Y;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(from.X, from.Y, d);
            if (nb.X == to.X && nb.Y == to.Y) return d;
        }
        return -1;
    }

    /// <summary>设置位掩码中的指定位</summary>
    private static int SetBit(int mask, int bit) => mask | (1 << bit);

    // ========================================
    // Stage 8: 遭遇密度预计算
    // ========================================

    private void PrecomputeEncounterDensity(
        Dictionary<Vector2I, ChunkData> chunks,
        Dictionary<string, NationTerritory> territories,
        List<BiomeZone> zones)
    {
        // 为每个 chunk 计算遭遇密度（基于距离国家中心 + 生态区危险度）
        var nationCenters = new Dictionary<string, Vector2I>();
        foreach (var (id, territory) in territories)
            nationCenters[id] = territory.CoreZone.Centroid;

        foreach (var (coord, chunk) in chunks)
        {
            var chunkCenter = ChunkData.ChunkToWorld(coord.X, coord.Y);
            chunkCenter += new Vector2I(ChunkData.ChunkSize / 2, ChunkData.ChunkSize / 2);

            // 距离最近国家中心越远，危险度越高
            float minDist = float.MaxValue;
            foreach (var center in nationCenters.Values)
            {
                float d = chunkCenter.DistanceTo(center);
                if (d < minDist) minDist = d;
            }

            // 归一化距离（假设世界对角线为最大距离）
            float maxDist = Mathf.Sqrt(chunks.Count) * ChunkData.ChunkSize;
            float distFactor = Mathf.Clamp(minDist / maxDist, 0.0f, 1.0f);

            // 基础危险度 = 距离因子 × 0.8（远离文明越危险）
            // 后续可以叠加生态区自身的危险度
            // 这里只做预计算标记，实际遭遇生成在运行时由 EncounterSpawner 处理
        }
    }
}
