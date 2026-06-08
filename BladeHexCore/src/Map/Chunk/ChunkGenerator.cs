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

    public const float ElevationFreq = 0.025f;   // 大陆基础高程
    public const float RidgeFreq = 0.04f;        // 山脊噪声
    public const float MoistureFreq = 0.025f;    // 湿度（提高：更多大块湿度斑块而非长条）
    public const float TemperatureFreq = 0.018f; // 温度扰动（提高：温度带打散为斑块）
    public const float RegionalCharFreq = 0.012f; // 地区性格（新：整块区域的偏向）

    // ========================================
    // 运行时噪声实例（确定性）
    // ========================================

    private FastNoiseLite? _noiseElev;
    private FastNoiseLite? _noiseRidge;   // 山脊噪声（用于线性山脉）
    private FastNoiseLite? _noiseMoist;
    private FastNoiseLite? _noiseTemp;
    private FastNoiseLite? _noiseWarp;    // 大陆形状扭曲
    private FastNoiseLite? _noiseRegion;  // 地区性格（让大块区域偏向某种地形）
    private FastNoiseLite? _noiseNutrient; // 养分/土壤肥力噪声

    /// <summary>世界生成模板网格（按 (gridX, gridY) 索引，每个格子一个独立模板）</summary>
    public WorldTemplate[,] TemplateGrid { get; private set; } = new WorldTemplate[1, 1];

    /// <summary>模板网格宽度</summary>
    public int TemplateGridW { get; private set; } = 1;

    /// <summary>模板网格高度</summary>
    public int TemplateGridH { get; private set; } = 1;

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
    public void Initialize(int worldSeed, int worldWidth = 1024, int worldHeight = 768,
        int templateGridW = 1, int templateGridH = 1)
    {
        WorldSeed = worldSeed;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        TemplateGridW = Mathf.Max(1, templateGridW);
        TemplateGridH = Mathf.Max(1, templateGridH);
        TemplateGrid = new WorldTemplate[TemplateGridW, TemplateGridH];
        for (int y = 0; y < TemplateGridH; y++)
            for (int x = 0; x < TemplateGridW; x++)
            {
                int subSeed = worldSeed ^ (x * 73856093) ^ (y * 19349663);
                TemplateGrid[x, y] = WorldTemplateGenerator.PickFromSeed(subSeed);
            }

        Godot.GD.Print($"[ChunkGenerator] Template grid {TemplateGridW}x{TemplateGridH}:");
        for (int y = 0; y < TemplateGridH; y++)
        {
            var line = new System.Text.StringBuilder("  ");
            for (int x = 0; x < TemplateGridW; x++)
                line.Append($"[{WorldTemplateGenerator.GetDisplayName(TemplateGrid[x, y])}] ");
            Godot.GD.Print(line.ToString());
        }
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

        // 地区性格噪声（控制大块区域整体偏好 — 干旱区、湿润区、寒冷区等）
        _noiseRegion = new FastNoiseLite();
        _noiseRegion.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseRegion.Seed = WorldSeed + 4000;
        _noiseRegion.Frequency = RegionalCharFreq;
        _noiseRegion.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseRegion.FractalOctaves = 3;
        _noiseRegion.FractalLacunarity = 2.0f;
        _noiseRegion.FractalGain = 0.5f;

        // 养分/土壤肥力噪声（低频大块，让同一区域内出现肥沃/贫瘠的自然变化）
        _noiseNutrient = new FastNoiseLite();
        _noiseNutrient.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noiseNutrient.Seed = WorldSeed + 5000;
        _noiseNutrient.Frequency = 0.03f;  // 低频：大块肥沃/贫瘠区域
        _noiseNutrient.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noiseNutrient.FractalOctaves = 2;
        _noiseNutrient.FractalLacunarity = 2.0f;
        _noiseNutrient.FractalGain = 0.4f;
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
        // 0. 模板修正参数（从模板网格采样，4邻插值平滑过渡）
        // ========================================
        var tpl = SampleTemplateGrid(worldQ, worldR);

        // ========================================
        // 1. 高程 = 大陆基础 + 山脊叠加
        // ========================================
        float baseElev = SampleSmoothed(_noiseElev!, worldQ, worldR);

        // 山脊噪声：Ridged Multi 产生 [-1,1]
        float ridgeRaw = _noiseRidge!.GetNoise2D(worldQ, worldR);
        float ridge = (ridgeRaw + 1.0f) * 0.5f;
        ridge = Mathf.Pow(ridge, 1.4f);  // 降低指数（从 1.7 → 1.4），让山脉更胖更明显

        // 山脊掩膜
        float ridgeMask = _noiseRegion!.GetNoise2D(worldQ * 0.5f + 8000, worldR * 0.5f + 8000);
        ridgeMask = Mathf.Clamp((ridgeMask + 0.5f) * 1.8f, 0.0f, 1.5f); // 偏向高值，可超过1放大山脉

        // 应用模板山脊乘数
        float ridgeWeight = 0.40f * tpl.RidgeMul;  // 从 0.30 → 0.40，山脉更显著
        float elev = baseElev * (1.0f - ridgeWeight) + ridge * ridgeMask * ridgeWeight;

        // 应用模板高程偏移
        elev += tpl.ElevBias;

        // 边缘衰减
        float warpScale = Mathf.Max(WorldWidth, WorldHeight) * 0.15f;
        float warpSeedOffset = (WorldSeed % 1000) * 0.1f;
        float warpX = _noiseWarp!.GetNoise2D(worldQ * 0.35f + warpSeedOffset, worldR * 0.35f) * warpScale;
        float warpY = _noiseWarp.GetNoise2D(worldQ * 0.35f + 200 + warpSeedOffset, worldR * 0.35f + 200) * warpScale;
        float edgeFalloff = CalcEdgeFalloff(
            worldQ + (int)warpX, worldR + (int)warpY,
            WorldWidth, WorldHeight);

        // 应用模板边缘衰减乘数
        edgeFalloff *= tpl.FalloffMul;
        edgeFalloff = Mathf.Clamp(edgeFalloff, 0.0f, 1.0f);

        // ========================================
        // 内海/湖泊生成
        // ========================================
        float lakeSeedOffset = ((WorldSeed >> 12) & 0xFFF) * 0.05f;
        float lakeNoise = _noiseWarp.GetNoise2D(worldQ * 0.15f + 500 + lakeSeedOffset, worldR * 0.15f + 500 + lakeSeedOffset);
        lakeNoise = (lakeNoise + 1.0f) * 0.5f;
        float lakeThreshold = 0.80f + ((WorldSeed >> 5) & 0x7) / 70.0f;
        float carveStrength = Mathf.Clamp((lakeNoise - lakeThreshold) * 5.0f, 0.0f, 1.0f);
        bool isLakeArea = edgeFalloff > 0.65f && carveStrength > 0.0f;

        if (isLakeArea)
        {
            float depthCurve = carveStrength * carveStrength;
            elev = Mathf.Max(elev * edgeFalloff - depthCurve * 0.35f, 0.0f);
        }
        else
        {
            float inlandFloor = edgeFalloff > 0.45f ? 0.34f : 0.0f;
            elev = Mathf.Max(elev * edgeFalloff, inlandFloor * edgeFalloff);
        }
        elev = Mathf.Clamp(elev, 0.0f, 1.0f);

        // ========================================
        // 2. 湿度 — 加入地区性格大块偏移
        // ========================================
        float moistNoise = SampleSmoothed(_noiseMoist!, worldQ, worldR);
        float oceanProximity = 1.0f - Mathf.Clamp(edgeFalloff, 0.0f, 1.0f);
        float coastalMoisture = Mathf.Clamp(oceanProximity * 2.5f, 0.0f, 0.35f);
        if (isLakeArea || carveStrength > 0.3f)
            coastalMoisture = Mathf.Max(coastalMoisture, 0.2f);

        // 雨影
        float rainShadow = Mathf.Clamp(elev - 0.6f, 0.0f, 0.25f);

        // 地区性格：让某些大区域整体更干或更湿（产生沙漠带、雨林带等）
        // 同一份 region noise 同时驱动 moist 和 temp（强相关），让"干区"自动"热"
        // 这样 hot+dry 联合触发条件不再是两个独立噪声的随机交集，而是同一个区域整体偏差
        float regionMoist = _noiseRegion.GetNoise2D(worldQ + 1000, worldR + 1000); // [-1,1]
        // 把 region noise 的极端值放大：负值（干）变更干，正值（湿）变更湿
        float regionMoistBias = Mathf.Sign(regionMoist) * Mathf.Pow(Mathf.Abs(regionMoist), 0.7f) * 0.35f;

        float moist = Mathf.Clamp(
            moistNoise * 0.45f + coastalMoisture - rainShadow + regionMoistBias,
            0.0f, 1.0f);

        // ========================================
        // 3. 温度 — 减弱纬度主导，加入地区性格
        // ========================================
        float tempRaw = SampleSmoothed(_noiseTemp!, worldQ, worldR);

        // 纬度基准（温和梯度，不再霸占整个温度范围）
        float latitudeTemp = (float)worldR / WorldHeight;
        // 把纬度从 [0,1] 重映射到 [0.15, 0.85]，让两极更冷、赤道更热
        latitudeTemp = 0.15f + latitudeTemp * 0.70f;

        float oceanModeration = oceanProximity * 0.15f;
        float moderatedTemp = Mathf.Lerp(latitudeTemp, 0.5f, oceanModeration);

        // 温度噪声扰动（提高幅度：从 0.2 → 0.4，让温度有大块斑块而非纯横条）
        float tempNoise = (tempRaw - 0.5f) * 0.40f;

        // 地区性格也影响温度：用同一份 regionMoist noise（不是独立的）
        // 这样"干区"自动"热"，"湿区"自动"凉"——形成自然的"沙漠带 / 雨林带 / 冻土带"
        // regionMoist 负 → 干旱 → 加热（temp 加正偏置）
        // regionMoist 正 → 湿润 → 降温（temp 减偏置）
        float regionTempBias = -regionMoist * 0.25f;

        // 海拔降温
        float altitudePenalty = Mathf.Clamp(elev - 0.55f, 0.0f, 0.4f) * 0.4f;

        float temp = Mathf.Clamp(
            moderatedTemp + tempNoise + regionTempBias - altitudePenalty,
            0.0f, 1.0f);

        // ========================================
        // 4. 养分/土壤肥力
        // ========================================
        float nutrientBase = (_noiseNutrient!.GetNoise2D(worldQ, worldR) + 1.0f) * 0.5f; // [0,1]
        // 湿润地区养分高（冲积效应）
        float moistBoost = Mathf.Lerp(0.7f, 1.0f, moist);
        // 高海拔贫瘠（薄土层）
        float elevPenalty = elev > 0.65f ? Mathf.Lerp(1.0f, 0.5f, (elev - 0.65f) / 0.35f) : 1.0f;
        float nutrient = Mathf.Clamp(nutrientBase * moistBoost * elevPenalty, 0.0f, 1.0f);

        // ========================================
        // 5. 生物群落决策
        // ========================================
        var terrain = BiomeRules.Decide(elev, moist, temp);

        return HexOverworldTile.Create(worldQ, worldR, terrain, elev, moist, temp, nutrient);
    }

    /// <summary>
    /// 从模板网格采样修正参数。每个网格单元负责一块世界，相邻单元用4邻插值过渡。
    /// 这样既保证大区域有不同模板，又避免硬切痕迹。
    /// </summary>
    private TemplateParams SampleTemplateGrid(int worldQ, int worldR)
    {
        // 计算 tile 在网格中的浮点位置 [0, gridW), [0, gridH)
        float fx = (float)worldQ / WorldWidth * TemplateGridW;
        float fy = (float)worldR / WorldHeight * TemplateGridH;

        // 与 CalcGridEdgeFalloff 一致的 domain warp，让模板边界与海岸边界一起扭曲
        float warpAmpQ = 0.30f;
        float warpAmpR = 0.30f;
        if (_noiseWarp != null)
        {
            fx += _noiseWarp.GetNoise2D(worldQ * 0.025f, worldR * 0.025f) * warpAmpQ;
            fy += _noiseWarp.GetNoise2D(worldQ * 0.025f + 1000, worldR * 0.025f + 1000) * warpAmpR;
        }

        // 4邻整数索引（带 clamp）
        int ix0 = Mathf.Clamp((int)Mathf.Floor(fx), 0, TemplateGridW - 1);
        int iy0 = Mathf.Clamp((int)Mathf.Floor(fy), 0, TemplateGridH - 1);
        int ix1 = Mathf.Clamp(ix0 + 1, 0, TemplateGridW - 1);
        int iy1 = Mathf.Clamp(iy0 + 1, 0, TemplateGridH - 1);

        // 单元内归一化坐标 [-1, 1]
        float lx0 = (fx - ix0) * 2.0f - 1.0f;
        float ly0 = (fy - iy0) * 2.0f - 1.0f;
        float lx1 = (fx - ix1) * 2.0f - 1.0f;
        float ly1 = (fy - iy1) * 2.0f - 1.0f;

        // 在 4 个网格单元中分别采样
        var t00 = WorldTemplateGenerator.Sample(TemplateGrid[ix0, iy0], lx0, ly0, WorldSeed);
        var t10 = WorldTemplateGenerator.Sample(TemplateGrid[ix1, iy0], lx1, ly0, WorldSeed);
        var t01 = WorldTemplateGenerator.Sample(TemplateGrid[ix0, iy1], lx0, ly1, WorldSeed);
        var t11 = WorldTemplateGenerator.Sample(TemplateGrid[ix1, iy1], lx1, ly1, WorldSeed);

        // 单元内插值权重
        float wx = Mathf.Clamp(fx - ix0, 0.0f, 1.0f);
        float wy = Mathf.Clamp(fy - iy0, 0.0f, 1.0f);
        // smoothstep 让过渡更自然
        wx = wx * wx * (3.0f - 2.0f * wx);
        wy = wy * wy * (3.0f - 2.0f * wy);

        // 双线性插值 3 个参数
        float falloffMul = Mathf.Lerp(
            Mathf.Lerp(t00.FalloffMul, t10.FalloffMul, wx),
            Mathf.Lerp(t01.FalloffMul, t11.FalloffMul, wx),
            wy);
        float elevBias = Mathf.Lerp(
            Mathf.Lerp(t00.ElevBias, t10.ElevBias, wx),
            Mathf.Lerp(t01.ElevBias, t11.ElevBias, wx),
            wy);
        float ridgeMul = Mathf.Lerp(
            Mathf.Lerp(t00.RidgeMul, t10.RidgeMul, wx),
            Mathf.Lerp(t01.RidgeMul, t11.RidgeMul, wx),
            wy);

        return new TemplateParams { FalloffMul = falloffMul, ElevBias = elevBias, RidgeMul = ridgeMul };
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
        // ========================================
        // 多模板网格模式：每个单元有自己的局部大陆形状
        // ========================================
        if (TemplateGridW > 1 || TemplateGridH > 1)
        {
            return CalcGridEdgeFalloff(q, r, width, height);
        }

        // ========================================
        // 单模板（1x1）：原有的全局椭圆衰减
        // ========================================
        return CalcSingleEdgeFalloff(q, r, width, height);
    }

    /// <summary>原始单大陆椭圆衰减（小型世界用）</summary>
    private float CalcSingleEdgeFalloff(int q, int r, int width, int height)
    {
        float nx = (float)q / width * 2.0f - 1.0f;
        float ny = (float)r / height * 2.0f - 1.0f;

        int s = WorldSeed;
        float aspectRatio = 0.7f + ((s & 0xFF) / 255.0f) * 0.6f;
        float offsetX = (((s >> 8) & 0xFF) / 255.0f - 0.5f) * 0.25f;
        float offsetY = (((s >> 16) & 0xFF) / 255.0f - 0.5f) * 0.2f;
        float rotation = (((s >> 24) & 0xFF) / 255.0f) * Mathf.Pi * 0.3f;
        float fadeStart = 0.45f + (((s >> 4) & 0xF) / 15.0f) * 0.2f;

        float cx = nx - offsetX;
        float cy = ny - offsetY;

        float cosR = Mathf.Cos(rotation);
        float sinR = Mathf.Sin(rotation);
        float rx = cx * cosR - cy * sinR;
        float ry = cx * sinR + cy * cosR;

        float dist = rx * rx / (aspectRatio * aspectRatio) + ry * ry;

        float angle = Mathf.Atan2(ny, nx);
        float edgeNoise = _noiseWarp!.GetNoise2D(
            Mathf.Cos(angle) * 50.0f + WorldSeed * 0.01f,
            Mathf.Sin(angle) * 50.0f) * 0.25f;

        float shapeNoise = _noiseElev!.GetNoise2D(
            q * 0.008f + 1000,
            r * 0.008f + 1000);
        shapeNoise = (shapeNoise + 1.0f) * 0.5f;
        float shapeModifier = (shapeNoise - 0.5f) * 0.35f;

        float modifiedDist = dist + edgeNoise + shapeModifier;

        return 1.0f - Mathf.SmoothStep(fadeStart, fadeStart + 0.55f, modifiedDist);
    }

    /// <summary>
    /// 多模板网格衰减（v4 — 全局大陆派生 + 最低陆地保障）：
    /// 1. 不再每单元独立椭圆。
    /// 2. 用低频噪声 + 一层世界级椭圆生成全局连续大陆形状
    /// 3. 模板影响 fadeStart（让某些区域多陆少海）但形状由全局噪声主导
    /// 4. 这样陆地能跨单元连成大陆，避免颗粒化海岛
    /// 5. 中心区域有最低陆地保障，防止极端 seed 产生全海地图
    /// </summary>
    private float CalcGridEdgeFalloff(int q, int r, int width, int height)
    {
        // ========== 世界级椭圆（让边缘衰减成海，避免方形地图边）==========
        float wnx = (float)q / width * 2.0f - 1.0f;
        float wny = (float)r / height * 2.0f - 1.0f;
        // 椭圆形（宽更大，更像现实大陆）
        float wdist = Mathf.Sqrt(wnx * wnx * 0.7f + wny * wny);
        // 远离中心 → 衰减；中心衰减 = 0
        float worldEllipse = 1.0f - Mathf.SmoothStep(0.85f, 1.20f, wdist);

        // ========== 全局大陆噪声（低频，决定大陆形状）==========
        // 低频 = 大陆尺度，几个 chunk 一个起伏
        float continentNoise = _noiseElev != null
            ? _noiseElev.GetNoise2D(q * 0.008f - 5000, r * 0.008f - 5000)
            : 0;
        continentNoise = (continentNoise + 1.0f) * 0.5f;  // → [0, 1]

        // ========== 中心区域陆地保障 ==========
        // 世界中心 60% 范围内给予额外的大陆噪声提升，
        // 防止极端 seed 导致全海地图。越靠近中心提升越大。
        float centerDist = Mathf.Sqrt(wnx * wnx + wny * wny);
        float centerBoost = Mathf.Clamp(1.0f - centerDist / 0.60f, 0.0f, 1.0f);
        centerBoost *= centerBoost; // 平方衰减，中心强外围弱
        continentNoise += centerBoost * 0.15f; // 最多提升 0.15
        continentNoise = Mathf.Clamp(continentNoise, 0.0f, 1.0f);

        // ========== 模板权重（4 邻插值，决定 landBias）==========
        float fx = (float)q / width * TemplateGridW;
        float fy = (float)r / height * TemplateGridH;
        int ix0 = Mathf.Clamp((int)Mathf.Floor(fx), 0, TemplateGridW - 1);
        int iy0 = Mathf.Clamp((int)Mathf.Floor(fy), 0, TemplateGridH - 1);
        int ix1 = Mathf.Clamp(ix0 + 1, 0, TemplateGridW - 1);
        int iy1 = Mathf.Clamp(iy0 + 1, 0, TemplateGridH - 1);
        float wx = Mathf.Clamp(fx - ix0, 0f, 1f);
        float wy = Mathf.Clamp(fy - iy0, 0f, 1f);
        wx = wx * wx * (3f - 2f * wx);
        wy = wy * wy * (3f - 2f * wy);

        float bias00 = TemplateLandBias(TemplateGrid[ix0, iy0]);
        float bias10 = TemplateLandBias(TemplateGrid[ix1, iy0]);
        float bias01 = TemplateLandBias(TemplateGrid[ix0, iy1]);
        float bias11 = TemplateLandBias(TemplateGrid[ix1, iy1]);
        float landBias = Mathf.Lerp(
            Mathf.Lerp(bias00, bias10, wx),
            Mathf.Lerp(bias01, bias11, wx),
            wy);

        // ========== 海陆判定（连续值）==========
        // continentNoise 是 [0, 1]，threshold 决定海陆分界。
        // 关键：falloff 必须有"陆地高原"（值接近 1）和"海洋深处"（值接近 0），
        // 否则大量值落在中间被判为浅水/沙滩，造成"海平面过高"的视觉。
        float threshold = 0.25f - landBias; // landBias 正 → 阈值低 → 更多陆地（从 0.30 降到 0.25）

        // 远离 threshold 的两端快速饱和：
        //   continentNoise > threshold + 0.15 → falloff 1.0（深陆地）
        //   continentNoise < threshold - 0.15 → falloff 0.0（深海）
        //   过渡带 0.30 宽（threshold ± 0.15）→ 海岸自然且有足够陆地
        float falloffRaw = Mathf.SmoothStep(threshold - 0.15f, threshold + 0.15f, continentNoise);

        // 加一层中频细节噪声让海岸不再光滑（幅度小，避免打碎大陆）
        float detailNoise = _noiseWarp != null
            ? _noiseWarp.GetNoise2D(q * 0.04f, r * 0.04f) * 0.05f
            : 0;
        falloffRaw = Mathf.Clamp(falloffRaw + detailNoise, 0f, 1f);

        return falloffRaw * worldEllipse;
    }

    /// <summary>
    /// 模板对陆地占比的偏置：陆地多的模板 → 大正值，海洋多的模板 → 小负值
    /// 注意：负值不宜过大，否则与不利 seed 叠加会导致全海地图
    /// </summary>
    private static float TemplateLandBias(WorldTemplate template)
    {
        return template switch
        {
            WorldTemplate.Pangaea          =>  0.20f,  // 泛大陆：大量陆地
            WorldTemplate.Continental      =>  0.10f,  // 大陆：陆地为主
            WorldTemplate.HighlandFortress =>  0.10f,  // 高原：连贯山地
            WorldTemplate.TwinContinents   =>  0.05f,  // 双生大陆
            WorldTemplate.RingOfFire       =>  0.00f,  // 环火山带：均衡
            WorldTemplate.Mediterranean    =>  0.00f,  // 内陆之海：均衡（从 -0.05 提升）
            WorldTemplate.InlandSea        => -0.05f,  // 内陆海（从 -0.10 提升）
            WorldTemplate.BrokenIsles      => -0.08f,  // 破碎诸岛（从 -0.15 提升）
            WorldTemplate.Archipelago      => -0.08f,  // 群岛之海（从 -0.15 提升）
            WorldTemplate.Maelstrom        => -0.10f,  // 大漩涡（从 -0.20 提升）
            _                              =>  0.00f,
        };
    }

    /// <summary>
    /// 单个单元内部的椭圆衰减。
    /// 输入：单元内浮点偏移（dq=fx-ix, dr=fy-iy 都在 [0,1] 之间，越接近 0.5 越靠近单元中心）。
    /// 输出：falloff 值 [0..1]，单元中心 = 1（陆地），单元边缘 → 0。
    /// </summary>
    private float CellFalloff(float dq, float dr, int ix, int iy)
    {
        // 单元内归一化到 [-1, 1]
        float lnx = dq * 2.0f - 1.0f;
        float lny = dr * 2.0f - 1.0f;

        int cellSeed = WorldSeed ^ (ix * 73856093) ^ (iy * 19349663);
        float aspectRatio = 0.85f + ((cellSeed & 0xFF) / 255.0f) * 0.4f;
        float offsetX = (((cellSeed >> 8) & 0xFF) / 255.0f - 0.5f) * 0.20f;
        float offsetY = (((cellSeed >> 16) & 0xFF) / 255.0f - 0.5f) * 0.20f;
        float rotation = (((cellSeed >> 24) & 0xFF) / 255.0f) * Mathf.Pi * 0.5f;
        float fadeStart = 0.75f + (((cellSeed >> 4) & 0xF) / 15.0f) * 0.20f;

        float cx = lnx - offsetX;
        float cy = lny - offsetY;
        float cosR = Mathf.Cos(rotation);
        float sinR = Mathf.Sin(rotation);
        float rx = cx * cosR - cy * sinR;
        float ry = cx * sinR + cy * cosR;
        float dist = rx * rx / (aspectRatio * aspectRatio) + ry * ry;

        // 单元独立的边缘噪声（不再依赖 q/r 直接坐标，避免相邻单元相位冲突）
        float angle = Mathf.Atan2(lny, lnx);
        float edgeNoise = _noiseWarp!.GetNoise2D(
            Mathf.Cos(angle) * 50.0f + cellSeed * 0.01f,
            Mathf.Sin(angle) * 50.0f + ix * 17 + iy * 31) * 0.20f;

        float modifiedDist = dist + edgeNoise;
        return 1.0f - Mathf.SmoothStep(fadeStart, fadeStart + 0.40f, modifiedDist);
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
