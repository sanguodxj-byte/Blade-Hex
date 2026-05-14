// RiverRoadGenerator.cs
// 全局河流路径生成器 — 在轻量级高程网格上生成跨 chunk 的河流路径
// 道路生成已移除：由 WorldCreator.ConnectSettlementRoads（纯 MST）直接在 tiles 上生成
// 算法复用自 HexOverworldGenerator，但操作 float[,] 而非 HexOverworldGrid
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 全局河流骨架数据（道路已移除，由 WorldCreator.ConnectSettlementRoads 纯 MST 生成）
/// </summary>
public class RiverRoadSkeleton
{
    /// <summary>河流路径列表: 每条河流是一个全局轴向坐标数组</summary>
    public List<Vector2I[]> RiverPaths { get; set; } = new();

    /// <summary>[已废弃] 道路路径 — 保留字段兼容旧存档反序列化，运行时不再使用</summary>
    [Obsolete("道路由 ConnectSettlementRoads 纯 MST 生成，不再存储在骨架中")]
    public List<Vector2I[]> RoadPaths { get; set; } = new();

    // ========================================
    // 序列化
    // ========================================

    /// <summary>序列化骨架数据为 Godot Dictionary（仅河流）</summary>
    public Godot.Collections.Dictionary Serialize()
    {
        var riversData = new Godot.Collections.Array();
        foreach (var river in RiverPaths)
        {
            var arr = new Godot.Collections.Array();
            foreach (var coord in river) arr.Add(coord);
            riversData.Add(arr);
        }

        return new Godot.Collections.Dictionary
        {
            ["rivers"] = riversData,
        };
    }

    /// <summary>从 Godot Dictionary 反序列化骨架数据（兼容旧存档含 roads 字段）</summary>
    public static RiverRoadSkeleton Deserialize(Godot.Collections.Dictionary data)
    {
        var skeleton = new RiverRoadSkeleton();

        if (data.ContainsKey("rivers") && data["rivers"].Obj is Godot.Collections.Array rivers)
        {
            foreach (var riverEntry in rivers)
            {
                var arr = (Godot.Collections.Array)riverEntry;
                var path = new Vector2I[arr.Count];
                for (int i = 0; i < arr.Count; i++) path[i] = (Vector2I)arr[i];
                skeleton.RiverPaths.Add(path);
            }
        }

        // 旧存档可能含 roads 字段 — 忽略，不再加载
        return skeleton;
    }
}

/// <summary>
/// 轻量级地形查询 — 从 float[,] 高程网格推导地形类型
/// 与 HexOverworldGenerator.BiomeDecision / ChunkGenerator.BiomeDecision 一致
/// </summary>
public struct LiteTerrainInfo
{
    public HexOverworldTile.TerrainType Terrain;
    public float Elevation;
    public float Moisture;
    public float Temperature;

    public bool IsPassable => Terrain switch
    {
        HexOverworldTile.TerrainType.DeepWater => false,
        HexOverworldTile.TerrainType.Mountain => false,
        HexOverworldTile.TerrainType.MountainSnow => false,
        HexOverworldTile.TerrainType.River => false,
        _ => true,
    };

    public float MoveCost => Terrain switch
    {
        HexOverworldTile.TerrainType.DeepWater => 99.0f,
        HexOverworldTile.TerrainType.ShallowWater => 3.0f,
        HexOverworldTile.TerrainType.Sand => 1.5f,
        HexOverworldTile.TerrainType.Plains => 1.0f,
        HexOverworldTile.TerrainType.Grassland => 1.0f,
        HexOverworldTile.TerrainType.Forest => 1.5f,
        HexOverworldTile.TerrainType.DenseForest => 2.5f,
        HexOverworldTile.TerrainType.Hills => 2.0f,
        HexOverworldTile.TerrainType.Mountain => 99.0f,
        HexOverworldTile.TerrainType.MountainSnow => 99.0f,
        HexOverworldTile.TerrainType.Snow => 2.0f,
        HexOverworldTile.TerrainType.Swamp => 2.5f,
        HexOverworldTile.TerrainType.Savanna => 1.0f,
        HexOverworldTile.TerrainType.River => 99.0f,
        _ => 1.0f,
    };
}

/// <summary>
/// 全局河流路径生成器
/// 在世界初始化时生成跨 chunk 的河流骨架路径
/// 不依赖 HexOverworldGrid 或 HexOverworldTile 对象，操作轻量级 float[,] 网格
/// 道路生成已移除 — 由 WorldCreator.ConnectSettlementRoads（纯 MST）负责
/// </summary>
[GlobalClass]
public partial class RiverRoadGenerator : RefCounted
{
    // ========================================
    // 生成配置常量 (与 HexOverworldGenerator 一致)
    // ========================================

    public const float SeaLevel = 0.30f;
    public const float ShallowLevel = 0.35f;
    public const float BeachLevel = 0.38f;
    public const float MountainLevel = 0.78f;

    public const int RiverCountMin = 3;
    public const int RiverCountMax = 6;
    public const int RiverMinLength = 15;

    // ========================================
    // 轻量级网格数据
    // ========================================

    /// <summary>全局高程网格 (轴向坐标直接索引)</summary>
    private float[,]? _elevation;

    /// <summary>全局湿度网格</summary>
    private float[,]? _moisture;

    /// <summary>全局温度网格</summary>
    private float[,]? _temperature;

    /// <summary>网格宽高 (轴向坐标范围)</summary>
    private int _gridWidth;
    private int _gridHeight;

    // ========================================
    // 噪声实例 (用于生成轻量级网格)
    // ========================================

    private FastNoiseLite? _noiseElev;
    private FastNoiseLite? _noiseMoist;
    private FastNoiseLite? _noiseTemp;

    // ========================================
    // 区域定义 (与 HexOverworldGenerator 一致)
    // ========================================

    private readonly List<RegionDef> _regions = new();

    // ========================================
    // 主入口
    // ========================================

    /// <summary>
    /// 初始化并生成全局河流骨架（道路已移除，由 ConnectSettlementRoads 生成）
    /// </summary>
    /// <param name="worldSeed">世界种子</param>
    /// <param name="worldWidth">世界宽度 (轴向格数, 如 1024)</param>
    /// <param name="worldHeight">世界高度 (轴向格数, 如 768)</param>
    /// <returns>河流骨架数据</returns>
    public RiverRoadSkeleton Generate(int worldSeed, int worldWidth, int worldHeight)
    {
        _gridWidth = worldWidth;
        _gridHeight = worldHeight;

        InitNoise(worldSeed);
        DefineRegions();
        BuildLightweightGrids();

        var skeleton = new RiverRoadSkeleton();

        GenerateRivers(skeleton);

        GD.Print($"[RiverRoadGenerator] 河流骨架生成完成: {skeleton.RiverPaths.Count} 条河流");

        return skeleton;
    }

    // ========================================
    // 噪声初始化
    // ========================================

    private void InitNoise(int seed)
    {
        _noiseElev = new FastNoiseLite();
        _noiseElev.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseElev.Seed = seed;
        _noiseElev.Frequency = HexOverworldGenerator.ElevationFreq;
        _noiseElev.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseElev.FractalOctaves = 6;
        _noiseElev.FractalLacunarity = 2.0f;
        _noiseElev.FractalGain = 0.5f;

        _noiseMoist = new FastNoiseLite();
        _noiseMoist.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseMoist.Seed = seed + 1000;
        _noiseMoist.Frequency = HexOverworldGenerator.MoistureFreq;
        _noiseMoist.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseMoist.FractalOctaves = 4;
        _noiseMoist.FractalLacunarity = 2.0f;
        _noiseMoist.FractalGain = 0.5f;

        _noiseTemp = new FastNoiseLite();
        _noiseTemp.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseTemp.Seed = seed + 2000;
        _noiseTemp.Frequency = HexOverworldGenerator.TemperatureFreq;
        _noiseTemp.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseTemp.FractalOctaves = 3;
    }

    // ========================================
    // 轻量级网格构建
    // ========================================

    private void BuildLightweightGrids()
    {
        _elevation = new float[_gridWidth, _gridHeight];
        _moisture = new float[_gridWidth, _gridHeight];
        _temperature = new float[_gridWidth, _gridHeight];

        for (int q = 0; q < _gridWidth; q++)
        {
            for (int r = 0; r < _gridHeight; r++)
            {
                // 高程 + 边缘衰减（制造大陆轮廓）
                float rawElev = _noiseElev!.GetNoise2D(q, r);
                float elev = (rawElev + 1.0f) * 0.5f;
                float edgeFalloff = CalcEdgeFalloff(q, r, _gridWidth, _gridHeight);
                elev *= edgeFalloff;
                _elevation[q, r] = Mathf.Clamp(elev, 0.0f, 1.0f);

                float rawMoist = _noiseMoist!.GetNoise2D(q, r);
                _moisture[q, r] = Mathf.Clamp((rawMoist + 1.0f) * 0.5f, 0.0f, 1.0f);

                // 温度: 纬度归一化到世界高度 + 高海拔降温
                float tempBase = (float)r / (float)_gridHeight;
                float tempNoise = _noiseTemp!.GetNoise2D(q, r) * 0.2f;
                float altitudePenalty = Mathf.Clamp(elev - 0.5f, 0.0f, 0.5f) * 0.6f;
                _temperature[q, r] = Mathf.Clamp(tempBase + tempNoise - altitudePenalty, 0.0f, 1.0f);
            }
        }
    }

    /// <summary>
    /// 边缘衰减函数 — 地图边缘 15% 范围内高程线性衰减到 0
    /// </summary>
    private static float CalcEdgeFalloff(int q, int r, int width, int height)
    {
        float nx = (float)q / width;
        float ny = (float)r / height;

        float dx = Mathf.Min(nx, 1.0f - nx);
        float dy = Mathf.Min(ny, 1.0f - ny);
        float edgeDist = Mathf.Min(dx, dy);

        return edgeDist < 0.15f ? edgeDist / 0.15f : 1.0f;
    }

    // ========================================
    // 轻量级地形查询
    // ========================================

    /// <summary>获取指定坐标的轻量级地形信息</summary>
    public LiteTerrainInfo GetTerrainAt(int q, int r)
    {
        if (_elevation == null || q < 0 || q >= _gridWidth || r < 0 || r >= _gridHeight)
            return new LiteTerrainInfo { Terrain = HexOverworldTile.TerrainType.DeepWater };

        float elev = _elevation[q, r];
        float moist = _moisture![q, r];
        float temp = _temperature![q, r];

        return new LiteTerrainInfo
        {
            Elevation = elev,
            Moisture = moist,
            Temperature = temp,
            Terrain = BiomeDecision(elev, moist, temp),
        };
    }

    /// <summary>获取指定坐标的高程 (无边界检查)</summary>
    public float GetElevation(int q, int r)
    {
        if (_elevation == null || q < 0 || q >= _gridWidth || r < 0 || r >= _gridHeight)
            return 0.0f;
        return _elevation[q, r];
    }

    private static HexOverworldTile.TerrainType BiomeDecision(float e, float m, float t)
    {
        if (e < SeaLevel) return HexOverworldTile.TerrainType.DeepWater;
        if (e < ShallowLevel) return HexOverworldTile.TerrainType.ShallowWater;
        if (e < BeachLevel) return t < 0.15f ? HexOverworldTile.TerrainType.Ice : HexOverworldTile.TerrainType.Sand;

        if (e > MountainLevel)
            return (e > 0.88f || t < 0.25f) ? HexOverworldTile.TerrainType.MountainSnow : HexOverworldTile.TerrainType.Mountain;

        bool freezing = t < 0.15f;
        bool cold = t >= 0.15f && t < 0.35f;
        bool temperate = t >= 0.35f && t < 0.70f;
        bool hot = t >= 0.70f;

        bool arid = m < 0.25f;
        bool dry = m >= 0.25f && m < 0.50f;
        bool wet = m >= 0.50f && m < 0.75f;

        var baseTerrain = HexOverworldTile.TerrainType.Plains;

        if (freezing)
            baseTerrain = arid ? HexOverworldTile.TerrainType.Ice : HexOverworldTile.TerrainType.Snow;
        else if (cold)
            baseTerrain = arid ? HexOverworldTile.TerrainType.Rocky : (dry || wet) ? HexOverworldTile.TerrainType.Taiga : HexOverworldTile.TerrainType.Bog;
        else if (temperate)
            baseTerrain = arid ? HexOverworldTile.TerrainType.Wasteland : dry ? HexOverworldTile.TerrainType.Plains : wet ? HexOverworldTile.TerrainType.Forest : HexOverworldTile.TerrainType.DenseForest;
        else if (hot)
            baseTerrain = arid ? HexOverworldTile.TerrainType.Sand : dry ? HexOverworldTile.TerrainType.Savanna : wet ? HexOverworldTile.TerrainType.Jungle : HexOverworldTile.TerrainType.Swamp;

        if (e > 0.65f && e <= MountainLevel)
        {
            if (baseTerrain == HexOverworldTile.TerrainType.Snow || baseTerrain == HexOverworldTile.TerrainType.Ice)
                return HexOverworldTile.TerrainType.Snow;
            return HexOverworldTile.TerrainType.Hills;
        }

        return baseTerrain;
    }

    // ========================================
    // 区域定义 (与 HexOverworldGenerator 一致)
    // ========================================

    private void DefineRegions()
    {
        _regions.Clear();

        _regions.Add(new RegionDef
        {
            Name = "霜冠山脉", CenterQ = 0.5f, CenterR = 0.1f, RadiusQ = 0.4f, RadiusR = 0.12f,
            DangerLevel = 0.7f, PreferredTerrains = [HexOverworldTile.TerrainType.Mountain, HexOverworldTile.TerrainType.Snow, HexOverworldTile.TerrainType.Hills]
        });
        _regions.Add(new RegionDef
        {
            Name = "银叶森林", CenterQ = 0.15f, CenterR = 0.45f, RadiusQ = 0.12f, RadiusR = 0.25f,
            DangerLevel = 0.3f, PreferredTerrains = [HexOverworldTile.TerrainType.Forest, HexOverworldTile.TerrainType.DenseForest]
        });
        _regions.Add(new RegionDef
        {
            Name = "中央平原", CenterQ = 0.5f, CenterR = 0.5f, RadiusQ = 0.35f, RadiusR = 0.2f,
            DangerLevel = 0.1f, PreferredTerrains = [HexOverworldTile.TerrainType.Plains, HexOverworldTile.TerrainType.Grassland]
        });
        _regions.Add(new RegionDef
        {
            Name = "焦土荒原", CenterQ = 0.75f, CenterR = 0.85f, RadiusQ = 0.2f, RadiusR = 0.12f,
            DangerLevel = 0.8f, PreferredTerrains = [HexOverworldTile.TerrainType.Sand, HexOverworldTile.TerrainType.Savanna]
        });
        _regions.Add(new RegionDef
        {
            Name = "蛮荒沼泽", CenterQ = 0.2f, CenterR = 0.85f, RadiusQ = 0.18f, RadiusR = 0.12f,
            DangerLevel = 0.5f, PreferredTerrains = [HexOverworldTile.TerrainType.Swamp]
        });
        _regions.Add(new RegionDef
        {
            Name = "丘陵草原", CenterQ = 0.85f, CenterR = 0.5f, RadiusQ = 0.12f, RadiusR = 0.2f,
            DangerLevel = 0.4f, PreferredTerrains = [HexOverworldTile.TerrainType.Savanna, HexOverworldTile.TerrainType.Hills]
        });
    }

    // ========================================
    // 河流生成 — 下坡寻路 + 渐进加宽
    // ========================================

    private void GenerateRivers(RiverRoadSkeleton skeleton)
    {
        GD.Seed((ulong)(WorldSeed != 0 ? WorldSeed : 42));
        int riverCount = (int)GD.RandRange(RiverCountMin, RiverCountMax);

        // 收集高海拔源头（山脚/丘陵，不是山顶）
        var highTiles = new List<Vector2I>();
        // 收集海岸线终点（ShallowWater 格子本身）
        var coastTiles = new List<Vector2I>();

        for (int q = 0; q < _gridWidth; q++)
        {
            for (int r = 0; r < _gridHeight; r++)
            {
                var info = GetTerrainAt(q, r);

                // 源头：高程 0.55-0.75 的可通行格（山脚而非山顶，更自然）
                if (info.Elevation > 0.55f && info.Elevation < 0.75f && info.IsPassable)
                    highTiles.Add(new Vector2I(q, r));

                // 终点：浅水格子（海岸线本身）
                if (info.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                    coastTiles.Add(new Vector2I(q, r));
            }
        }

        if (highTiles.Count == 0 || coastTiles.Count == 0) return;

        int riversPlaced = 0;
        var usedSources = new HashSet<Vector2I>();
        var allRiverTiles = new HashSet<Vector2I>(); // 防止河流交叉
        var rng = new Random(WorldSeed != 0 ? WorldSeed : 42);
        ShuffleList(highTiles, rng);

        for (int attempt = 0; attempt < riverCount * 8 && riversPlaced < riverCount; attempt++)
        {
            if (attempt >= highTiles.Count) break;
            var source = highTiles[attempt];
            if (usedSources.Contains(source)) continue;

            // 找最近的海岸终点
            var target = FindNearestCoast(source, coastTiles);
            if (target == source) continue;

            // 源头和终点距离太近的跳过
            if (HexDistance(source, target) < RiverMinLength) continue;

            var path = FindDownhillPath(source, target, allRiverTiles);
            if (path.Length < RiverMinLength) continue;

            // 验证路径确实到达了水边（最后几格高程应该很低）
            var lastInfo = GetTerrainAt(path[^1].X, path[^1].Y);
            if (lastInfo.Elevation > 0.4f) continue; // 没到低地，放弃

            skeleton.RiverPaths.Add(path);
            usedSources.Add(source);
            foreach (var coord in path) allRiverTiles.Add(coord);
            riversPlaced++;
        }

        GD.Print($"[RiverRoadGenerator] 生成 {riversPlaced} 条河流");
    }

    /// <summary>找到离源头最近的海岸格</summary>
    private Vector2I FindNearestCoast(Vector2I source, List<Vector2I> coastTiles)
    {
        Vector2I best = source;
        int bestDist = int.MaxValue;
        foreach (var coast in coastTiles)
        {
            int d = HexDistance(source, coast);
            if (d < bestDist) { bestDist = d; best = coast; }
        }
        return best;
    }

    /// <summary>
    /// 下坡优先寻路 — 河流从高处流向低处。
    /// 代价函数：强烈惩罚上坡，奖励下坡，禁止穿越水域和已有河流。
    /// </summary>
    private Vector2I[] FindDownhillPath(Vector2I start, Vector2I target, HashSet<Vector2I> existingRivers)
    {
        if (!IsInBounds(start.X, start.Y) || !IsInBounds(target.X, target.Y))
            return [];

        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0.0f };
        var closedSet = new HashSet<Vector2I>();

        var openQueue = new PriorityQueue<Vector2I, float>();
        openQueue.Enqueue(start, HexDistance(start, target));

        float startElev = GetElevation(start.X, start.Y);
        int maxIterations = Math.Min(_gridWidth * _gridHeight, 300000);
        int iteration = 0;

        while (openQueue.Count > 0 && iteration < maxIterations)
        {
            iteration++;

            var current = openQueue.Dequeue();
            if (closedSet.Contains(current)) continue;
            closedSet.Add(current);

            if (current == target)
                return ReconstructPath(cameFrom, current);

            float currentElev = GetElevation(current.X, current.Y);

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (!IsInBounds(neighbor.X, neighbor.Y) || closedSet.Contains(neighbor))
                    continue;

                var nInfo = GetTerrainAt(neighbor.X, neighbor.Y);

                // 禁止穿越深水（河流应该在到达浅水/海岸时停止）
                if (nInfo.Terrain == HexOverworldTile.TerrainType.DeepWater) continue;

                // 禁止穿越已有河流（防止交叉）
                if (existingRivers.Contains(neighbor)) continue;

                // 禁止穿越山脉
                if (nInfo.Terrain == HexOverworldTile.TerrainType.Mountain ||
                    nInfo.Terrain == HexOverworldTile.TerrainType.MountainSnow) continue;

                float neighborElev = nInfo.Elevation;
                float elevDiff = neighborElev - currentElev; // 正=上坡, 负=下坡

                // 代价计算：下坡便宜，上坡极贵
                float cost;
                if (elevDiff <= 0)
                {
                    // 下坡：代价 = 1 + 微小的高程值（偏好低地）
                    cost = 1.0f + neighborElev * 2.0f;
                }
                else
                {
                    // 上坡：代价极高（河流不应该上坡）
                    cost = 1.0f + elevDiff * 50.0f;
                }

                // 到达浅水（终点附近）时代价很低，吸引路径
                if (nInfo.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                    cost = 0.5f;

                float tentativeG = gScore.GetValueOrDefault(current, 999999.0f) + cost;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, 999999.0f))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    float fScore = tentativeG + HexDistance(neighbor, target) * 0.5f;
                    openQueue.Enqueue(neighbor, fScore);
                }
            }
        }

        return [];
    }

    // ========================================
    // 辅助方法
    // ========================================

    private bool IsInBounds(int q, int r)
    {
        return q >= 0 && q < _gridWidth && r >= 0 && r < _gridHeight;
    }

    private static int HexDistance(Vector2I a, Vector2I b)
    {
        return HexOverworldTile.HexDistance(a.X, a.Y, b.X, b.Y);
    }

    private static Vector2I[] ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
    {
        var path = new List<Vector2I> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path.ToArray();
    }

    private static void ShuffleList<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>世界种子 (用于确定性随机)</summary>
    public int WorldSeed { get; set; } = 0;

    /// <summary>获取区域定义列表</summary>
    public RegionDef[] GetRegions() => _regions.ToArray();
}
