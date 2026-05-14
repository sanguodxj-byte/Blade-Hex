// ChunkGenerator.cs
// 确定性单 Chunk 地形生成器 — 从全局噪声生成一个 16×16 的 chunk
// 复用 HexOverworldGenerator 的噪声参数和生物群落规则
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 单 Chunk 确定性地形生成器
/// 每个 chunk 基于全局坐标独立生成，无跨 chunk 依赖
/// </summary>
[GlobalClass]
public partial class ChunkGenerator : RefCounted
{
    // ========================================
    // 噪声参数
    // ========================================

    public const float ElevationFreq = 0.035f;   // 大陆基础高程
    public const float RidgeFreq = 0.05f;        // 山脊噪声（线性山脉）
    public const float MoistureFreq = 0.03f;     // 湿度基础噪声
    public const float TemperatureFreq = 0.015f;  // 温度扰动

    // ========================================
    // 运行时噪声实例（确定性）
    // ========================================

    private FastNoiseLite? _noiseElev;
    private FastNoiseLite? _noiseRidge;   // 山脊噪声（用于线性山脉）
    private FastNoiseLite? _noiseMoist;
    private FastNoiseLite? _noiseTemp;
    private FastNoiseLite? _noiseWarp;    // 大陆形状扭曲

    /// <summary>世界种子</summary>
    public int WorldSeed { get; private set; } = 0;

    /// <summary>世界宽度（轴向格数，用于温度/边缘衰减计算）</summary>
    public int WorldWidth { get; private set; } = 1024;

    /// <summary>世界高度（轴向格数，用于温度/边缘衰减计算）</summary>
    public int WorldHeight { get; private set; } = 768;

    // ========================================
    // 区域定义 — 从 RegionRegistry (SSOT) 读取
    // ========================================

    private RegionDef[] _regions = [];

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 用世界种子初始化生成器（整个游戏生命周期调用一次）
    /// </summary>
    public void Initialize(int worldSeed, int worldWidth = 1024, int worldHeight = 768)
    {
        WorldSeed = worldSeed;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        InitNoise();
        DefineRegions();
    }

    private void InitNoise()
    {
        // 大陆基础高程（低频 FBM — 大块陆地/海洋）
        _noiseElev = new FastNoiseLite();
        _noiseElev.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseElev.Seed = WorldSeed;
        _noiseElev.Frequency = ElevationFreq;
        _noiseElev.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseElev.FractalOctaves = 5;
        _noiseElev.FractalLacunarity = 2.0f;
        _noiseElev.FractalGain = 0.45f;

        // 山脊噪声（Ridged Multi — 产生线性山脉走向）
        _noiseRidge = new FastNoiseLite();
        _noiseRidge.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseRidge.Seed = WorldSeed + 500;
        _noiseRidge.Frequency = RidgeFreq;
        _noiseRidge.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
        _noiseRidge.FractalOctaves = 4;
        _noiseRidge.FractalLacunarity = 2.2f;
        _noiseRidge.FractalGain = 0.5f;

        // 湿度基础噪声（用于局部变化，最终受海洋距离调制）
        _noiseMoist = new FastNoiseLite();
        _noiseMoist.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseMoist.Seed = WorldSeed + 1000;
        _noiseMoist.Frequency = MoistureFreq;
        _noiseMoist.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseMoist.FractalOctaves = 3;
        _noiseMoist.FractalLacunarity = 2.0f;
        _noiseMoist.FractalGain = 0.5f;

        // 温度扰动噪声
        _noiseTemp = new FastNoiseLite();
        _noiseTemp.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseTemp.Seed = WorldSeed + 2000;
        _noiseTemp.Frequency = TemperatureFreq;
        _noiseTemp.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseTemp.FractalOctaves = 3;

        // 大陆形状扭曲（Domain Warp — 让海岸线不规则）
        _noiseWarp = new FastNoiseLite();
        _noiseWarp.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseWarp.Seed = WorldSeed + 3000;
        _noiseWarp.Frequency = 0.02f;
        _noiseWarp.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseWarp.FractalOctaves = 3;
    }

    private void DefineRegions()
    {
        // 从 RegionRegistry (SSOT) 读取，不再维护本地副本
        _regions = RegionRegistry.Regions;
    }

    // ========================================
    // 主入口 — 生成单个 Chunk
    // ========================================

    /// <summary>
    /// 生成指定坐标的 chunk（确定性，基于 WorldSeed + chunkCoord）
    /// r 坐标补偿 axial 倾斜，使地图在像素空间中呈矩形。
    /// </summary>
    public ChunkData Generate(int chunkQ, int chunkR)
    {
        var chunk = new ChunkData();
        chunk.ChunkCoord = new Vector2I(chunkQ, chunkR);

        var origin = ChunkData.ChunkToWorld(chunkQ, chunkR);

        // 为每个 tile 计算全局坐标并生成地形
        for (int oq = 0; oq < ChunkData.ChunkSize; oq++)
        {
            for (int or_ = 0; or_ < ChunkData.ChunkSize; or_++)
            {
                int worldQ = origin.X + oq;
                int worldR = origin.Y + or_;

                // 补偿 axial 倾斜：将 r 偏移 -q/2，使像素空间呈矩形
                // 噪声采样和边缘衰减使用补偿后的"视觉行"坐标
                var tile = GenerateTile(worldQ, worldR);
                chunk.Tiles[tile.Coord] = tile;
            }
        }

        // 分配区域名称
        AssignRegionName(chunk);

        // 标记已生成
        chunk.IsGenerated = true;

        return chunk;
    }

    // ========================================
    // 单个瓦片生成
    // ========================================

    /// <summary>六边形 axial 邻居偏移（静态，避免 GC）</summary>
    private static readonly int[][] HexNeighborOffsets =
    [
        [1, 0], [0, 1], [-1, 1],
        [-1, 0], [0, -1], [1, -1]
    ];

    private HexOverworldTile GenerateTile(int worldQ, int worldR)
    {
        // ========================================
        // 1. 高程 = 大陆基础 + 山脊叠加
        // ========================================
        float baseElev = SampleSmoothed(_noiseElev!, worldQ, worldR);

        // 山脊噪声：Ridged Multi 产生 [-1,1]，取绝对值反转得到山脊
        float ridgeRaw = _noiseRidge!.GetNoise2D(worldQ, worldR);
        // Ridged noise 已经是 [0,1] 范围（FractalTypeEnum.Ridged 自动处理）
        float ridge = (ridgeRaw + 1.0f) * 0.5f;
        ridge = Mathf.Pow(ridge, 2.0f); // 锐化山脊（让山脉更窄更高）

        // 合成高程：基础 70% + 山脊 30%
        float elev = baseElev * 0.7f + ridge * 0.3f;

        // 边缘衰减 — 大陆轮廓（用 domain warp 扭曲坐标，产生不规则海岸线）
        float warpScale = Mathf.Max(WorldWidth, WorldHeight) * 0.12f; // 扭曲幅度 = 世界尺寸的 12%
        float warpX = _noiseWarp!.GetNoise2D(worldQ * 0.5f, worldR * 0.5f) * warpScale;
        float warpY = _noiseWarp.GetNoise2D(worldQ * 0.5f + 200, worldR * 0.5f + 200) * warpScale;
        float edgeFalloff = CalcEdgeFalloff(
            worldQ + (int)warpX, worldR + (int)warpY,
            WorldWidth, WorldHeight);

        // 内海/湖泊生成：用低频噪声在大陆深处挖出凹陷
        // 只在大陆核心（edgeFalloff > 0.7）生效，确保不会切断大陆
        float seaCarve = _noiseWarp.GetNoise2D(worldQ * 0.25f + 500, worldR * 0.25f + 500);
        seaCarve = (seaCarve + 1.0f) * 0.5f;
        // 噪声值 > 0.78 的区域挖凹（约 22% 的核心区域有机会形成内海）
        float carveStrength = Mathf.Clamp((seaCarve - 0.78f) * 4.5f, 0.0f, 1.0f);
        bool isCarvedArea = edgeFalloff > 0.7f && carveStrength > 0.0f;

        if (isCarvedArea)
        {
            // 内海区域：降低高程（最多降 0.3，形成浅水/深水）
            elev = elev * edgeFalloff - carveStrength * 0.3f;
        }
        else
        {
            // 正常区域：衰减 + 内陆保底
            float inlandFloor = edgeFalloff > 0.5f ? 0.32f : 0.0f;
            elev = Mathf.Max(elev * edgeFalloff, inlandFloor * edgeFalloff);
        }
        elev = Mathf.Clamp(elev, 0.0f, 1.0f);

        // ========================================
        // 2. 湿度 = 海洋距离 + 噪声变化 - 雨影效应
        // ========================================
        float moistNoise = SampleSmoothed(_noiseMoist!, worldQ, worldR);

        // 海洋距离因子：高程越低（越接近海平面）越湿
        // 用 edgeFalloff 近似海洋距离（边缘=近海=湿，中心=内陆=干）
        float oceanProximity = 1.0f - Mathf.Clamp(edgeFalloff, 0.0f, 1.0f);
        // 沿海湿度加成（距海 0-30% 范围内线性增加）
        float coastalMoisture = Mathf.Clamp(oceanProximity * 3.0f, 0.0f, 0.4f);

        // 雨影效应：山脉背后（高程高的区域下风侧）更干燥
        // 简化：高程越高，对下游湿度的抑制越强
        float rainShadow = Mathf.Clamp(elev - 0.6f, 0.0f, 0.3f);

        float moist = Mathf.Clamp(moistNoise * 0.6f + coastalMoisture - rainShadow, 0.0f, 1.0f);

        // ========================================
        // 3. 温度 = 纬度 + 海洋调节 + 噪声 - 海拔惩罚
        // ========================================
        float tempRaw = SampleSmoothed(_noiseTemp!, worldQ, worldR);

        // 纬度基准（r=0 为北方冷，r=WorldHeight 为南方热）
        float latitudeTemp = (float)worldR / WorldHeight;

        // 海洋调节：沿海温度更温和（向 0.5 靠拢）
        float oceanModeration = oceanProximity * 0.15f;
        float moderatedTemp = Mathf.Lerp(latitudeTemp, 0.5f, oceanModeration);

        // 噪声扰动
        float tempNoise = (tempRaw - 0.5f) * 0.3f;

        // 海拔降温
        float altitudePenalty = Mathf.Clamp(elev - 0.5f, 0.0f, 0.5f) * 0.6f;

        float temp = Mathf.Clamp(moderatedTemp + tempNoise - altitudePenalty, 0.0f, 1.0f);

        // ========================================
        // 4. 生物群落决策
        // ========================================
        var terrain = BiomeRules.Decide(elev, moist, temp);

        return HexOverworldTile.Create(worldQ, worldR, terrain, elev, moist, temp);
    }

    /// <summary>
    /// 空间低通滤波 — 中心权重 0.3 + 6 邻居各 0.117（≈0.7/6）。
    /// 邻居影响占 70%，确保地形高度连续，消除单瓦片噪点。
    /// </summary>
    private static float SampleSmoothed(FastNoiseLite noise, int q, int r)
    {
        float center = (noise.GetNoise2D(q, r) + 1.0f) * 0.5f;

        float neighborSum = 0.0f;
        for (int i = 0; i < 6; i++)
        {
            int nq = q + HexNeighborOffsets[i][0];
            int nr = r + HexNeighborOffsets[i][1];
            neighborSum += (noise.GetNoise2D(nq, nr) + 1.0f) * 0.5f;
        }

        // 中心 30%，邻居 70%（强平滑，确保连续性）
        return center * 0.3f + neighborSum * (0.7f / 6.0f);
    }

    /// <summary>
    /// 边缘衰减函数 — 用椭圆距离场产生大陆轮廓。
    /// 衰减起始点随机化（0.55-0.70），确保大陆足够大不会被切碎。
    /// </summary>
    private float CalcEdgeFalloff(int q, int r, int width, int height)
    {
        // 像素布局已是矩形（even-q offset），直接用 q/r 归一化
        float nx = (float)q / width * 2.0f - 1.0f;
        float ny = (float)r / height * 2.0f - 1.0f;

        float dist = nx * nx + ny * ny;

        float seedVariation = ((WorldSeed & 0xFF) / 255.0f) * 0.15f;
        float fadeStart = 0.55f + seedVariation;

        return 1.0f - Mathf.SmoothStep(fadeStart, 1.1f, dist);
    }

    // ========================================
    // 区域名称分配
    // ========================================

    private void AssignRegionName(ChunkData chunk)
    {
        // 用 chunk 中心的全局轴向坐标做区域判定
        var origin = ChunkData.ChunkToWorld(chunk.ChunkCoord.X, chunk.ChunkCoord.Y);
        int centerQ = origin.X + ChunkData.ChunkSize / 2;
        int centerR = origin.Y + ChunkData.ChunkSize / 2;

        // 归一化到 [0, 1] 范围（基于实际世界尺寸）
        float nq = (float)centerQ / (float)WorldWidth;
        float nr = (float)centerR / (float)WorldHeight;

        string bestRegion = "";
        float bestScore = -1.0f;
        var preferredSet = new HashSet<HexOverworldTile.TerrainType>();

        foreach (var region in _regions)
        {
            float dq = (nq - region.CenterQ) / Mathf.Max(region.RadiusQ, 0.01f);
            float dr = (nr - region.CenterR) / Mathf.Max(region.RadiusR, 0.01f);
            float distSq = dq * dq + dr * dr;
            float score = Mathf.Exp(-distSq * 2.0f);

            // 检查 chunk 中心 tile 的地形是否匹配区域偏好
            var centerTile = chunk.GetTile(centerQ, centerR);
            if (centerTile != null)
            {
                preferredSet.Clear();
                foreach (var pt in region.PreferredTerrains) preferredSet.Add(pt);
                if (preferredSet.Contains(centerTile.Terrain)) score *= 1.5f;
            }

            if (score > bestScore) { bestScore = score; bestRegion = region.Name; }
        }

        if (bestScore > 0.3f)
            chunk.RegionName = bestRegion;
    }

    // ========================================
    // 区域查询
    // ========================================

    /// <summary>获取区域定义列表</summary>
    public RegionDef[] GetRegions() => _regions;

    /// <summary>获取指定 chunk 的区域危险等级</summary>
    public float GetDangerLevel(int chunkQ, int chunkR)
    {
        var origin = ChunkData.ChunkToWorld(chunkQ, chunkR);
        float nq = (float)(origin.X + ChunkData.ChunkSize / 2) / (float)WorldWidth;
        float nr = (float)(origin.Y + ChunkData.ChunkSize / 2) / (float)WorldHeight;

        float bestScore = -1.0f;
        float danger = 0.0f;

        foreach (var region in _regions)
        {
            float dq = (nq - region.CenterQ) / Mathf.Max(region.RadiusQ, 0.01f);
            float dr = (nr - region.CenterR) / Mathf.Max(region.RadiusR, 0.01f);
            float distSq = dq * dq + dr * dr;
            float score = Mathf.Exp(-distSq * 2.0f);
            if (score > bestScore) { bestScore = score; danger = region.DangerLevel; }
        }

        return danger;
    }
}
