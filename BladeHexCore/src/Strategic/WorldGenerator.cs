// WorldGenerator.cs
// 世界生成器（协调器）— 增量式世界初始化
// 职责：协调 WorldRegionRegistry / PoiFactory / EntitySpawner / RiverRoadStamper / ChunkManager / TriggerEngine
// 不再直接包含区域定义、POI 构建、实体生成、河流道路标记的实现逻辑
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 世界生成器 —— 增量式世界初始化协调器
/// 负责全局骨架（区域定义、POI 位置、河流/道路骨架）
/// 不再负责全地图瓦片生成（由 ChunkGenerator 按需生成）
/// </summary>
[GlobalClass]
public partial class WorldGenerator : RefCounted
{
    // ========================================
    // 子系统（拆分后的独立组件）
    // ========================================

    /// <summary>区域注册表 — SSOT 区域定义</summary>
    public WorldRegionRegistry Regions { get; private set; } = new();

    /// <summary>POI 工厂</summary>
    public PoiFactory PoiFactoryInstance { get; private set; } = new();

    /// <summary>实体生成器</summary>
    public EntitySpawner EntitySpawnerInstance { get; private set; } = new();

    /// <summary>河流/道路印章器</summary>
    public RiverRoadStamper Stamper { get; private set; } = new();

    // ========================================
    // Chunk 系统集成
    // ========================================

    /// <summary>Chunk 管理器</summary>
    public ChunkManager? ChunkMgr { get; set; }

    /// <summary>遭遇生成器</summary>
    public EncounterSpawner? Spawner { get; set; }

    /// <summary>Chunk 级别迷雾</summary>
    public ChunkFogOfWar? Fog { get; set; }

    // ========================================
    // 世界种子与网格
    // ========================================

    /// <summary>世界种子</summary>
    public int WorldSeed { get; private set; } = 0;

    /// <summary>保留对旧 HexGrid 的兼容引用（用于迁移过渡期）</summary>
    public HexOverworldGrid? HexGrid;
    public HexOverworldGenerator? HexGen;

    // ========================================
    // 河流/道路骨架
    // ========================================

    /// <summary>河流/道路路径生成器（操作轻量级高程网格）</summary>
    public RiverRoadGenerator? RiverRoadGen { get; set; }

    /// <summary>全局河流/道路骨架数据</summary>
    public RiverRoadSkeleton? Skeleton { get => Stamper.Skeleton; set => Stamper.Skeleton = value; }

    // ========================================
    // 世界网格尺寸（轴向坐标范围）
    // ========================================

    /// <summary>世界宽度（轴向格数，用于河流/道路生成）</summary>
    public int WorldTileWidth { get; set; } = 1024;

    /// <summary>世界高度（轴向格数，用于河流/道路生成）</summary>
    public int WorldTileHeight { get; set; } = 768;

    // ========================================
    // 触发引擎
    // ========================================

    /// <summary>触发引擎 — 条件触发框架</summary>
    public TriggerEngine? TriggerSystem { get; set; }

    /// <summary>触发历史（持久化）</summary>
    public TriggerHistory TriggerRecord { get; set; } = new();

    // ========================================
    // 公开数据（向后兼容）
    // ========================================

    public List<OverworldPOI> Pois = new();

    /// <summary>实体模板列表（不再作为运行时实体，仅作为遭遇数据源）</summary>
    public List<OverworldEntity> EntityTemplates = new();

    public int MapWidth { get => Regions.MapWidth; set => Regions.MapWidth = value; }
    public int MapHeight { get => Regions.MapHeight; set => Regions.MapHeight = value; }
    public FastNoiseLite? Noise { get => Regions.Noise; set => Regions.Noise = value; }

    // ========================================
    // 初始化
    // ========================================

    public WorldGenerator()
    {
        // WorldRegionRegistry 在构造时自动 SetupDefaultRegions
    }

    // ========================================
    // 增量式世界初始化（Chunk 模式）
    // ========================================

    /// <summary>
    /// 初始化增量式世界（Chunk 模式）
    /// 不生成全地图，只初始化全局骨架 + POI
    /// </summary>
    public void InitializeChunkWorld(int worldSeed, BladeHex.Data.RaceData.Race raceId)
    {
        WorldSeed = worldSeed;
        Stamper.WorldSeed = worldSeed;

        // 初始化 Chunk 系统
        ChunkMgr = new ChunkManager();
        ChunkMgr.Initialize(worldSeed, WorldTileWidth, WorldTileHeight);
        ChunkMgr.RiverRoadStamper = Stamper; // 直接引用 Stamper

        // 初始化遭遇生成器
        Spawner = new EncounterSpawner();

        // 初始化 Chunk 级别迷雾
        Fog = new ChunkFogOfWar();
        Fog.Initialize(raceId);
        Fog.ApplyRaceInitialReveal();

        // 初始化全局噪声（用于 POI 位置验证）
        Noise = new FastNoiseLite();
        Noise.Seed = worldSeed;
        Noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;

        // 初始化河流骨架（全局路径，跨 chunk；道路由 ConnectSettlementRoads 生成）
        RiverRoadGen = new RiverRoadGenerator();
        RiverRoadGen.WorldSeed = worldSeed;
        Skeleton = RiverRoadGen.Generate(worldSeed, WorldTileWidth, WorldTileHeight);

        // 初始化触发引擎
        TriggerSystem = new TriggerEngine();
        TriggerSystem.RegisterHandler(new SpatialTriggerHandler());
        TriggerSystem.RegisterHandler(new InteractionTriggerHandler());
        TriggerSystem.RegisterHandler(new TimeTriggerHandler() { Pois = Pois });
        TriggerSystem.RegisterHandler(new ChainTriggerHandler());
        TriggerSystem.RegisterHandler(new EnvironmentTriggerHandler());
        RegisterDefaultTriggers();

        GD.Print($"[WorldGenerator] Chunk 世界初始化完成: seed={worldSeed}, race={raceId}");
    }

    // ========================================
    // 默认触发条件注册
    // ========================================

    private void RegisterDefaultTriggers()
    {
        if (TriggerSystem == null) return;

        // --- 空间触发 ---
        TriggerSystem.RegisterCondition(new TriggerCondition
        {
            Id = "spatial_wild_monsters",
            Type = TriggerType.Spatial,
            Priority = 10.0f,
            Chance = 0.25f,
            RequiredTerrains = [Map.HexOverworldTile.TerrainType.Forest, Map.HexOverworldTile.TerrainType.DenseForest,
                Map.HexOverworldTile.TerrainType.Swamp, Map.HexOverworldTile.TerrainType.Hills],
            EncounterType = EncounterType.WildMonsters,
            Narrative = "你在荒野中遇到了敌对生物！",
        });

        TriggerSystem.RegisterCondition(new TriggerCondition
        {
            Id = "spatial_hostile_patrol",
            Type = TriggerType.Spatial,
            Priority = 8.0f,
            Chance = 0.15f,
            MinPlayerLevel = 3,
            EncounterType = EncounterType.HostilePatrol,
            Narrative = "一队敌对士兵拦住了你的去路！",
        });

        TriggerSystem.RegisterCondition(new TriggerCondition
        {
            Id = "spatial_resource_node",
            Type = TriggerType.Spatial,
            Priority = 5.0f,
            Chance = 0.20f,
            RequiredTerrains = [Map.HexOverworldTile.TerrainType.Forest, Map.HexOverworldTile.TerrainType.Hills,
                Map.HexOverworldTile.TerrainType.Grassland],
            EncounterType = EncounterType.ResourceNode,
            Narrative = "你发现了可采集的资源！",
        });

        TriggerSystem.RegisterCondition(new TriggerCondition
        {
            Id = "spatial_mystery",
            Type = TriggerType.Spatial,
            Priority = 3.0f,
            Chance = 0.05f,
            RequiredTerrains = [Map.HexOverworldTile.TerrainType.DenseForest, Map.HexOverworldTile.TerrainType.Swamp,
                Map.HexOverworldTile.TerrainType.Taiga],
            EncounterType = EncounterType.Mystery,
            Narrative = "你发现了不寻常的迹象...",
        });

        // --- 环境触发 ---
        TriggerSystem.RegisterCondition(new TriggerCondition
        {
            Id = "env_weather",
            Type = TriggerType.Environment,
            Priority = 15.0f,
            Chance = 0.30f,
            Narrative = "恶劣天气来袭！",
        });

        // --- 时间触发 ---
        TriggerSystem.RegisterCondition(new TriggerCondition
        {
            Id = "time_raid_spawn",
            Type = TriggerType.Time,
            Priority = 10.0f,
            Chance = 1.0f,
            MinDay = 7,
            CooldownDays = 7,
        });

        TriggerSystem.RegisterCondition(new TriggerCondition
        {
            Id = "time_poi_recovery",
            Type = TriggerType.Time,
            Priority = 5.0f,
            Chance = 1.0f,
            CooldownDays = 1,
        });
    }

    // ========================================
    // Chunk 更新
    // ========================================

    /// <summary>
    /// 更新玩家位置触发的 chunk 加载
    /// </summary>
    public List<ChunkData> UpdatePlayerPosition(int worldQ, int worldR, int playerLevel, int daysElapsed)
    {
        if (ChunkMgr == null || Spawner == null) return new List<ChunkData>();

        var newChunks = ChunkMgr.UpdateChunks(worldQ, worldR);

        foreach (var chunk in newChunks)
        {
            if (chunk.IsGenerated && chunk.RegionName != "")
            {
                float danger = ChunkMgr.Generator?.GetDangerLevel(chunk.ChunkCoord.X, chunk.ChunkCoord.Y) ?? 0.0f;
                int chunkSeed = WorldSeed ^ (chunk.ChunkCoord.X * 7919 + chunk.ChunkCoord.Y * 104729);
                Spawner.PopulateEncounterSlots(chunk, danger, playerLevel, daysElapsed, chunkSeed);
            }
        }

        if (Fog != null)
        {
            var activeCoords = new HashSet<Vector2I>();
            foreach (var kv in ChunkMgr.ActiveChunks)
                activeCoords.Add(kv.Key);
            Fog.UpdateVision(activeCoords);
        }

        return newChunks;
    }

    /// <summary>获取指定位置的活跃 chunk</summary>
    public ChunkData? GetChunkAt(int worldQ, int worldR)
    {
        return ChunkMgr?.GetChunkAt(worldQ, worldR);
    }

    /// <summary>触发指定位置的遭遇</summary>
    public EncounterData? TriggerEncounter(Vector2I worldCoord, int playerLevel, float dangerLevel)
    {
        var chunk = ChunkMgr?.GetChunkAt(worldCoord.X, worldCoord.Y);
        if (chunk == null) return null;

        var state = chunk.GetEncounterState(worldCoord.X, worldCoord.Y);
        if (state != EncounterSlotState.Available) return null;

        chunk.SetEncounterState(worldCoord.X, worldCoord.Y, EncounterSlotState.Triggered);

        var tile = chunk.GetTile(worldCoord.X, worldCoord.Y);
        if (tile == null) return null;

        return Spawner?.BuildEncounter(worldCoord, tile, playerLevel, dangerLevel);
    }

    // ========================================
    // 河流/道路 Chunk 查询（委托到 Stamper）
    // ========================================

    /// <summary>
    /// 获取指定 chunk 内的河流路径段
    /// </summary>
    public List<Vector2I[]> GetRiverPathsForChunk(int chunkQ, int chunkR)
    {
        return Stamper.GetRiverPathsForChunk(chunkQ, chunkR);
    }

    /// <summary>
    /// [已废弃] 获取指定 chunk 内的道路路径段 — 始终返回空列表。
    /// 道路由 ConnectSettlementRoads 直接生成在 tiles 上。
    /// </summary>
    [Obsolete("道路由 ConnectSettlementRoads 纯 MST 生成，不再存储在骨架中")]
    public List<Vector2I[]> GetRoadPathsForChunk(int chunkQ, int chunkR)
    {
        return new List<Vector2I[]>();
    }

    /// <summary>
    /// 将河流路径标记到 chunk 的瓦片上（仅河流）
    /// </summary>
    public void StampRiverRoadOnChunk(ChunkData chunk)
    {
        Stamper.StampOnChunk(chunk);
    }

    // ========================================
    // 地理查询（委托到 WorldRegionRegistry）
    // ========================================

    public Region GetRegionAt(float px, float py, float noiseVal)
    {
        return Regions.GetRegionAt(px, py, noiseVal);
    }

    public bool IsValidPoiPosition(float px, float py, float minDistance = 120.0f)
    {
        return Regions.IsValidPoiPosition(px, py, Pois, minDistance);
    }

    public Vector2 FindPositionInRegion(Region region, float minDistance = 120.0f)
    {
        return Regions.FindPositionInRegion(region, Pois, minDistance);
    }

    // ========================================
    // POI 构建（委托到 PoiFactory）
    // ========================================

    public List<OverworldPOI> BuildPoisFromData(Godot.Collections.Array dataArray)
    {
        var result = PoiFactoryInstance.BuildPoisFromData(dataArray);
        Pois.AddRange(result);
        return result;
    }

    // ========================================
    // 实体生成（委托到 EntitySpawner）
    // ========================================

    public List<OverworldEntity> GenerateEntityTemplates(List<OverworldPOI> existingPois, HexOverworldGrid? grid = null, HexOverworldGenerator? gen = null)
    {
        Pois = existingPois;
        EntityTemplates = EntitySpawnerInstance.GenerateEntityTemplates(existingPois, Regions, grid, gen);
        return EntityTemplates;
    }

    /// <summary>
    /// 创建掠夺队（委托到 EntitySpawner，带完整 POI 列表）
    /// </summary>
    public OverworldEntity? CreateRaidingParty(OverworldPOI source)
    {
        return EntitySpawnerInstance.CreateRaidingPartyWithTargets(source, Pois);
    }
}
