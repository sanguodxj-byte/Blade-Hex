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

    public const float ElevationFreq = 0.025f;   // 大陆基础高程（降低：更大块的陆地）
    public const float RidgeFreq = 0.04f;        // 山脊噪声（降低：更连续的山脉）
    public const float MoistureFreq = 0.018f;    // 湿度基础噪声（大幅降低：更宽广的湿度带）
    public const float TemperatureFreq = 0.01f;  // 温度扰动（降低：温度带更平缓）

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
        _noiseElev.FractalOctaves = 4;          // 减少 octaves：去掉最高频细节
        _noiseElev.FractalLacunarity = 2.0f;
        _noiseElev.FractalGain = 0.4f;          // 降低 gain：高频 octave 贡献更小

        // 山脊噪声（Ridged Multi — 产生线性山脉走向）
        _noiseRidge = new FastNoiseLite();
        _noiseRidge.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseRidge.Seed = WorldSeed + 500;
        _noiseRidge.Frequency = RidgeFreq;
        _noiseRidge.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
        _noiseRidge.FractalOctaves = 3;         // 减少：山脉更平滑
        _noiseRidge.FractalLacunarity = 2.0f;
        _noiseRidge.FractalGain = 0.45f;

        // 湿度基础噪声（用于局部变化，最终受海洋距离调制）
        _noiseMoist = new FastNoiseLite();
        _noiseMoist.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseMoist.Seed = WorldSeed + 1000;
        _noiseMoist.Frequency = MoistureFreq;
        _noiseMoist.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseMoist.FractalOctaves = 2;         // 大幅减少：湿度变化极平缓
        _noiseMoist.FractalLacunarity = 2.0f;
        _noiseMoist.FractalGain = 0.4f;

        // 温度扰动噪声
        _noiseTemp = new FastNoiseLite();
        _noiseTemp.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseTemp.Seed = WorldSeed + 2000;
        _noiseTemp.Frequency = TemperatureFreq;
        _noiseTemp.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseTemp.FractalOctaves = 2;          // 减少：温度带更宽

        // 大陆形状扭曲（Domain Warp — 让海岸线不规则）
        _noiseWarp = new FastNoiseLite();
        _noiseWarp.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseWarp.Seed = WorldSeed + 3000;
        _noiseWarp.Frequency = 0.015f;          // 降低：更大尺度的海岸线扭曲
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

        // 合成高程：基础 75% + 山脊 25%（降低山脊权重，减少碎片化山地）
        float elev = baseElev * 0.75f + ridge * 0.25f;

        // 边缘衰减 — 大陆轮廓（用 domain warp 扭曲坐标，产生不规则海岸线）
        float warpScale = Mathf.Max(WorldWidth, WorldHeight) * 0.15f; // 扭曲幅度增大到 15%
        float warpSeedOffset = (WorldSeed % 1000) * 0.1f; // 种子影响 warp 采样位置
        float warpX = _noiseWarp!.GetNoise2D(worldQ * 0.35f + warpSeedOffset, worldR * 0.35f) * warpScale;
        float warpY = _noiseWarp.GetNoise2D(worldQ * 0.35f + 200 + warpSeedOffset, worldR * 0.35f + 200) * warpScale;
        float edgeFalloff = CalcEdgeFalloff(
            worldQ + (int)warpX, worldR + (int)warpY,
            WorldWidth, WorldHeight);

        // ========================================
        // 内海/湖泊生成 — 改进版
        // 使用独立的低频噪声，产生大块连续的内海而非碎片化水坑
        // ========================================
        float lakeSeedOffset = ((WorldSeed >> 12) & 0xFFF) * 0.05f;
        float lakeNoise = _noiseWarp.GetNoise2D(worldQ * 0.15f + 500 + lakeSeedOffset, worldR * 0.15f + 500 + lakeSeedOffset);
        lakeNoise = (lakeNoise + 1.0f) * 0.5f;
        // 种子控制内海数量：有些种子产生更多内海，有些几乎没有
        float lakeThreshold = 0.80f + ((WorldSeed >> 5) & 0x7) / 70.0f; // 0.80~0.90
        float carveStrength = Mathf.Clamp((lakeNoise - lakeThreshold) * 5.0f, 0.0f, 1.0f);
        bool isLakeArea = edgeFalloff > 0.65f && carveStrength > 0.0f;

        if (isLakeArea)
        {
            // 内海区域：大幅降低高程，确保形成连续水体
            // 使用平滑的凹陷曲线，中心最深，边缘渐浅
            float depthCurve = carveStrength * carveStrength; // 二次曲线，中心更深
            elev = Mathf.Max(elev * edgeFalloff - depthCurve * 0.35f, 0.0f);
        }
        else
        {
            // 正常区域：衰减 + 内陆保底（提高保底值，防止内陆出现零散水坑）
            float inlandFloor = edgeFalloff > 0.45f ? 0.34f : 0.0f;
            elev = Mathf.Max(elev * edgeFalloff, inlandFloor * edgeFalloff);
        }
        elev = Mathf.Clamp(elev, 0.0f, 1.0f);

        // ========================================
        // 2. 湿度 = 海洋距离 + 噪声变化 - 雨影效应
        // ========================================
        float moistNoise = SampleSmoothed(_noiseMoist!, worldQ, worldR);

        // 海洋距离因子：用 edgeFalloff 近似（边缘=近海=湿，中心=内陆=干）
        float oceanProximity = 1.0f - Mathf.Clamp(edgeFalloff, 0.0f, 1.0f);
        // 沿海湿度加成（距海 0-30% 范围内线性增加）
        float coastalMoisture = Mathf.Clamp(oceanProximity * 2.5f, 0.0f, 0.35f);

        // 内海/湖泊附近也增加湿度（模拟湖泊蒸发效应）
        if (isLakeArea || carveStrength > 0.3f)
            coastalMoisture = Mathf.Max(coastalMoisture, 0.2f);

        // 雨影效应：山脉背后更干燥
        float rainShadow = Mathf.Clamp(elev - 0.6f, 0.0f, 0.25f);

        float moist = Mathf.Clamp(moistNoise * 0.55f + coastalMoisture - rainShadow, 0.0f, 1.0f);

        // ========================================
        // 3. 温度 = 纬度 + 海洋调节 + 噪声 - 海拔惩罚
        // ========================================
        float tempRaw = SampleSmoothed(_noiseTemp!, worldQ, worldR);

        // 纬度基准（r=0 为北方冷，r=WorldHeight 为南方热）
        float latitudeTemp = (float)worldR / WorldHeight;

        // 海洋调节：沿海温度更温和（向 0.5 靠拢）
        float oceanModeration = oceanProximity * 0.15f;
        float moderatedTemp = Mathf.Lerp(latitudeTemp, 0.5f, oceanModeration);

        // 噪声扰动（降低扰动幅度，让纬度梯度更明显）
        float tempNoise = (tempRaw - 0.5f) * 0.2f;

        // 海拔降温（降低系数，避免温带高地被错误归为寒冷）
        float altitudePenalty = Mathf.Clamp(elev - 0.55f, 0.0f, 0.4f) * 0.4f;

        float temp = Mathf.Clamp(moderatedTemp + tempNoise - altitudePenalty, 0.0f, 1.0f);

        // ========================================
        // 4. 生物群落决策
        // ========================================
        var terrain = BiomeRules.Decide(elev, moist, temp);

        return HexOverworldTile.Create(worldQ, worldR, terrain, elev, moist, temp);
    }

    /// <summary>
    /// 空间低通滤波 — 2 环邻居加权平均。
    /// 中心权重 0.2，1 环(6 邻居)各 0.08，2 环(12 邻居)各 0.02。
    /// 总权重: 0.2 + 6×0.08 + 12×0.02 = 0.2 + 0.48 + 0.24 = 0.92（归一化到 1.0）
    /// 大范围平滑确保地形高度连续，消除碎片化。
    /// </summary>
    private static float SampleSmoothed(FastNoiseLite noise, int q, int r)
    {
        float center = (noise.GetNoise2D(q, r) + 1.0f) * 0.5f;

        // 1 环邻居
        float ring1Sum = 0.0f;
        for (int i = 0; i < 6; i++)
        {
            int nq = q + HexNeighborOffsets[i][0];
            int nr = r + HexNeighborOffsets[i][1];
            ring1Sum += (noise.GetNoise2D(nq, nr) + 1.0f) * 0.5f;
        }

        // 2 环邻居（12 个位置）
        float ring2Sum = 0.0f;
        int ring2Count = 0;
        for (int i = 0; i < 6; i++)
        {
            // 直线延伸 2 格
            int nq = q + HexNeighborOffsets[i][0] * 2;
            int nr = r + HexNeighborOffsets[i][1] * 2;
            ring2Sum += (noise.GetNoise2D(nq, nr) + 1.0f) * 0.5f;
            ring2Count++;

            // 对角位置
            int next = (i + 1) % 6;
            int dq = q + HexNeighborOffsets[i][0] + HexNeighborOffsets[next][0];
            int dr = r + HexNeighborOffsets[i][1] + HexNeighborOffsets[next][1];
            ring2Sum += (noise.GetNoise2D(dq, dr) + 1.0f) * 0.5f;
            ring2Count++;
        }

        // 加权平均：中心 20%，1 环 52%，2 环 28%
        const float wCenter = 0.20f;
        const float wRing1 = 0.52f;
        const float wRing2 = 0.28f;

        return center * wCenter + ring1Sum * (wRing1 / 6.0f) + ring2Sum * (wRing2 / ring2Count);
    }

    /// <summary>
    /// 边缘衰减函数 — 多重噪声调制的大陆轮廓。
    /// 不再是固定椭圆，而是用种子驱动的噪声产生不规则大陆形状：
    /// - 基础形状：椭圆 + 种子控制的宽高比和偏移
    /// - 噪声调制：低频噪声扭曲边缘，产生半岛、海湾、峡谷
    /// - 多大陆支持：种子决定是单大陆还是群岛/双大陆
    /// </summary>
    private float CalcEdgeFalloff(int q, int r, int width, int height)
    {
        // 归一化坐标到 [-1, 1]
        float nx = (float)q / width * 2.0f - 1.0f;
        float ny = (float)r / height * 2.0f - 1.0f;

        // === 种子驱动的大陆形状参数 ===
        // 从种子中提取多个独立的随机参数
        int s = WorldSeed;
        float aspectRatio = 0.7f + ((s & 0xFF) / 255.0f) * 0.6f;          // 宽高比 0.7~1.3
        float offsetX = (((s >> 8) & 0xFF) / 255.0f - 0.5f) * 0.25f;      // 中心偏移 X [-0.125, 0.125]
        float offsetY = (((s >> 16) & 0xFF) / 255.0f - 0.5f) * 0.2f;      // 中心偏移 Y [-0.1, 0.1]
        float rotation = (((s >> 24) & 0xFF) / 255.0f) * Mathf.Pi * 0.3f; // 旋转角 0~54°
        float fadeStart = 0.45f + (((s >> 4) & 0xF) / 15.0f) * 0.2f;      // 衰减起始 0.45~0.65

        // 应用偏移
        float cx = nx - offsetX;
        float cy = ny - offsetY;

        // 应用旋转
        float cosR = Mathf.Cos(rotation);
        float sinR = Mathf.Sin(rotation);
        float rx = cx * cosR - cy * sinR;
        float ry = cx * sinR + cy * cosR;

        // 椭圆距离（宽高比控制）
        float dist = rx * rx / (aspectRatio * aspectRatio) + ry * ry;

        // === 噪声调制边缘 — 产生不规则海岸线 ===
        // 用极坐标角度采样噪声，让边缘凹凸不平
        float angle = Mathf.Atan2(ny, nx);
        float edgeNoise = _noiseWarp!.GetNoise2D(
            Mathf.Cos(angle) * 50.0f + WorldSeed * 0.01f,
            Mathf.Sin(angle) * 50.0f) * 0.25f;

        // 大尺度形状噪声（让大陆不是完美椭圆）
        float shapeNoise = _noiseElev!.GetNoise2D(
            q * 0.008f + 1000,
            r * 0.008f + 1000);
        shapeNoise = (shapeNoise + 1.0f) * 0.5f;
        // 形状噪声让某些方向的边缘更近/更远（产生半岛和海湾）
        float shapeModifier = (shapeNoise - 0.5f) * 0.35f;

        // 最终距离 = 基础椭圆 + 边缘噪声 + 形状调制
        float modifiedDist = dist + edgeNoise + shapeModifier;

        return 1.0f - Mathf.SmoothStep(fadeStart, fadeStart + 0.55f, modifiedDist);
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
