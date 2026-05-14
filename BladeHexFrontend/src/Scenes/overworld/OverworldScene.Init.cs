// OverworldScene.Init.cs
// [T-601] OverworldScene — 初始化 partial 类
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.Map;
using BladeHex.Data;
using BladeHex.View.Map;
using BladeHex.View.Environment;

namespace BladeHex.Scenes.Overworld;

// ========================================
// 初始化方法 — 由 _Ready() 按序调用
// ========================================
public partial class OverworldScene
{
    // ========================================
    // 1. 经济系统 + 任务管理器
    // ========================================

    /// <summary>
    /// 创建经济管理器与任务管理器
    /// GD: _init_economy (lines 637-650)
    /// </summary>
    private void InitEconomy()
    {
        // 创建经济管理器 （添加到场景树根节点，跨场景持久化）
        EconomyMgr = new EconomyManager();
        EconomyMgr.Name = "EconomyManager";
        GetTree().Root.CallDeferred("add_child", EconomyMgr);

        // 创建任务管理器
        var questManager = new QuestManager();
        questManager.Name = "QuestManager";
        AddChild(questManager);
        QuestMgr = questManager;

        // 连接任务目标点信号
        questManager.QuestTargetSpawned += OnQuestTargetSpawned;
        questManager.QuestTargetCleared += OnQuestTargetCleared;
    }

    // ========================================
    // 2. 摄像机设置
    // ========================================

    /// <summary>
    /// 设置大地图摄像机
    /// GD: _setup_camera (lines 544-551)
    /// </summary>
    private void SetupCamera()
    {
        MainCamera = new Camera2D();
        MainCamera.Zoom = new Vector2(0.25f, 0.25f);
        MainCamera.PositionSmoothingEnabled = true;
        MainCamera.PositionSmoothingSpeed = 8.0f;
        AddChild(MainCamera);
        MainCamera.MakeCurrent();
    }

    // ========================================
    // 3. 昼夜渐变曲线
    // ========================================

    /// <summary>
    /// 设置 16 点昼夜渐变 Gradient
    /// GD: _setup_time_gradient (lines 882-902)
    /// </summary>
    private void SetupTimeGradient()
    {
        TimeGradient = new Gradient();

        // 直接设置所有点（覆盖默认的两个白色点）
        TimeGradient.Offsets = new float[]
        {
            0.00f,  // 00:00 午夜
            0.08f,  // 02:00 深夜
            0.18f,  // 04:20 黎明前
            0.22f,  // 05:20 破晓
            0.27f,  // 06:30 日出
            0.33f,  // 08:00 早晨
            0.42f,  // 10:00 白昼
            0.54f,  // 13:00 午后
            0.65f,  // 15:30 傍晚前
            0.71f,  // 17:00 金色黄昏
            0.75f,  // 18:00 日落橙红
            0.79f,  // 19:00 暮色
            0.83f,  // 20:00 入夜
            0.88f,  // 21:00 深夜前
            0.94f,  // 22:30 深夜
            1.00f,  // 24:00 午夜
        };
        TimeGradient.Colors = new Color[]
        {
            new(0.20f, 0.20f, 0.35f), // 00:00 午夜
            new(0.20f, 0.20f, 0.35f), // 02:00 深夜
            new(0.25f, 0.24f, 0.38f), // 04:20 黎明前
            new(0.50f, 0.38f, 0.42f), // 05:20 破晓
            new(0.80f, 0.58f, 0.45f), // 06:30 日出
            new(0.95f, 0.88f, 0.78f), // 08:00 早晨
            new(1.00f, 1.00f, 1.00f), // 10:00 白昼
            new(1.00f, 1.00f, 0.98f), // 13:00 午后
            new(0.98f, 0.94f, 0.86f), // 15:30 傍晚前
            new(0.92f, 0.74f, 0.55f), // 17:00 金色黄昏
            new(0.78f, 0.50f, 0.38f), // 18:00 日落橙红
            new(0.55f, 0.35f, 0.32f), // 19:00 暮色
            new(0.38f, 0.28f, 0.30f), // 20:00 入夜
            new(0.28f, 0.24f, 0.32f), // 21:00 深夜前
            new(0.22f, 0.21f, 0.34f), // 22:30 深夜
            new(0.20f, 0.20f, 0.35f), // 24:00 午夜
        };
    }

    // ========================================
    // 4. 六边形大地图初始化
    // ========================================

    /// <summary>
    /// 初始化六边形大地图系统
    /// 新路径：WorldCreator 全量生成 → ChunkManager 流式加载 → HexOverworldRenderer chunk 渲染
    /// 旧路径（fallback）：HexOverworldGenerator 全图一次性生成
    /// </summary>
    private void InitHexOverworld()
    {
        InitHexOverworldChunkMode();
    }

    /// <summary>
    /// 新路径：Chunk 流式模式
    /// WorldCreator 全量生成 → 序列化到磁盘 → ChunkManager 按需加载
    /// </summary>
    private void InitHexOverworldChunkMode()
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");
        int seed = gs.WorldSeed > 0 ? gs.WorldSeed : (int)GD.Randi();
        string saveId = gs.CurrentSaveId ?? $"world_{seed}";

        // 检查是否有已保存的世界（读档 or 已生成）
        if (ChunkPersistence.HasSave(saveId))
        {
            // 从磁盘加载已有世界
            LoadExistingChunkWorld(saveId, seed);
        }
        else
        {
            // 新游戏：全量生成世界
            CreateNewChunkWorld(saveId, seed);
        }

        GD.Print($"[OverworldScene] Chunk 模式初始化完成: seed={seed}, saveId={saveId}");
    }

    /// <summary>新游戏：全量生成世界并序列化</summary>
    private void CreateNewChunkWorld(string saveId, int seed)
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");
        var worldSize = (WorldCreationConfig.WorldSize)gs.WorldSize;
        var config = WorldCreationConfig.Create(worldSize, seed);

        var creator = new WorldCreator();
        creator.OnProgress = (progress, msg) =>
        {
            GD.Print($"[WorldCreator] {progress:P0} {msg}");
        };

        var worldData = creator.CreateWorld(seed, config);

        // 初始化运行时 ChunkManager（内存模式 — 不写磁盘，玩家保存时才持久化）
        _chunkSaveId = saveId;
        _chunkManager = new ChunkManager();
        _chunkManager.Initialize(seed, config.WorldTileWidth, config.WorldTileHeight);
        _chunkManager.LoadIntoMemory(worldData.Chunks);

        // 记录世界中心坐标（用于玩家默认起始位置）
        _chunkWorldCenter = new Vector2I(config.WorldTileWidth / 2, config.WorldTileHeight / 2);

        // 初始化渲染器
        HexRenderer = new BladeHex.View.Map.HexOverworldRenderer();
        HexRenderer.Initialize(_chunkManager);
        AddChild(HexRenderer);

        // 存储 POI 和世界数据
        WorldPois = worldData.Pois;
        _worldZones = worldData.Zones;
        _worldTerritories = worldData.Territories;
        _worldNations = worldData.Nations;
        _specialCharacters = worldData.SpecialCharacters;

        // 初始化 HexAStar（使用 ChunkManager 的活跃 chunk）
        HexAStar = new HexOverworldAStar();
        // HexAStar 需要 HexGrid 兼容 — 暂时创建空 grid，后续 Phase 9 替换为 ChunkAStar
        HexGrid = new HexOverworldGrid();
    }

    /// <summary>读档：从磁盘加载已有世界</summary>
    private void LoadExistingChunkWorld(string saveId, int seed)
    {
        var meta = ChunkPersistence.LoadWorldMeta(saveId);
        int chunksW = meta != null && meta.ContainsKey("chunks_w") ? (int)meta["chunks_w"] : 16;
        int chunksH = meta != null && meta.ContainsKey("chunks_h") ? (int)meta["chunks_h"] : 12;
        int tileW = chunksW * ChunkData.ChunkSize;
        int tileH = chunksH * ChunkData.ChunkSize;

        _chunkSaveId = saveId;
        _chunkManager = new ChunkManager();
        _chunkManager.Initialize(seed, tileW, tileH);
        _chunkManager.SaveId = saveId; // 设置存档 ID，让 LoadOrGenerateChunk 从磁盘读取
        _chunkWorldCenter = new Vector2I(tileW / 2, tileH / 2);

        // 初始化渲染器
        HexRenderer = new BladeHex.View.Map.HexOverworldRenderer();
        HexRenderer.Initialize(_chunkManager);
        AddChild(HexRenderer);

        // HexAStar 兼容
        HexAStar = new HexOverworldAStar();
        HexGrid = new HexOverworldGrid();

        // 从磁盘加载 POI
        var loadedPois = ChunkPersistence.LoadPois(saveId);
        WorldPois = loadedPois ?? new List<OverworldPOI>();
    }

    /// <summary>旧路径（fallback）：全图一次性生成</summary>
    private void InitHexOverworldLegacy()
    {
        if (HexGen == null)
        {
            HexGen = new HexOverworldGenerator();
        }

        HexGrid = HexGen.Generate();
        HexAStar = new HexOverworldAStar();
        HexAStar.Grid = HexGrid;

        GD.Print($"[OverworldScene] 六边形大地图初始化完成(旧路径): {HexGrid.GridWidth}x{HexGrid.GridHeight} 瓦片");
    }

    // ========================================
    // 5. 玩家种族确定
    // ========================================

    /// <summary>
    /// 从 GlobalState 或存档确定玩家种族
    /// - 读档：恢复保存的 race_id
    /// - 出身选择：使用 PlayerOrigin.race
    /// - 快速游戏：随机挑一个可玩种族
    /// - 默认：Human
    /// </summary>
    private void DeterminePlayerRace()
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");

        // 优先从存档恢复
        if (gs.IsLoadingSave && gs.LoadedData.Count > 0)
        {
            if (gs.LoadedData.TryGetValue("character", out var charDataObj) &&
                charDataObj.Obj is Godot.Collections.Dictionary charData &&
                charData.TryGetValue("race_id", out var savedRace))
            {
                int raceId = savedRace.AsInt32();
                if (raceId >= 0)
                {
                    PlayerRaceId = raceId;
                    return;
                }
            }
        }

        // 从出身选择获取
        if (gs.PlayerOrigin.TryGetValue("race", out var raceObj) &&
            raceObj.Obj is RaceData raceData)
        {
            PlayerRaceId = (int)raceData.raceId;
            return;
        }

        // 快速游戏：使用已随机生成的角色种族
        if (gs.PlayerOrigin.TryGetValue("unit_data", out var unitObj) &&
            unitObj.Obj is UnitData unitData &&
            unitData.Race is RaceData unitRace &&
            unitData.Race.raceId != RaceData.Race.Human)
        {
            // 已经有合理的 race（非默认 Human），使用它
            PlayerRaceId = (int)unitRace.raceId;
            return;
        }

        // 快速游戏且未指定种族 → 随机挑一个
        if (gs.IsQuickGame)
        {
            var allRaces = RaceData.GetAllRaces();
            var rolled = allRaces[(int)(GD.Randi() % (uint)allRaces.Length)];
            PlayerRaceId = (int)rolled.raceId;
            GD.Print($"[OverworldScene] 快速游戏随机种族: {rolled.RaceName}");
            return;
        }

        // 默认人类 (RaceData.Race.Race.Human = 0)
        PlayerRaceId = 0;
    }

    // ========================================
    // 6. 世界 POI 生成
    // ========================================

    /// <summary>
    /// 在六边形网格上生成 POI
    /// GD: _generate_world_pois (lines 263-376)
    /// </summary>
    private void GenerateWorldPois()
    {
        var regions = HexGen.GetRegions();
        if (regions.Length == 0) return;

        // 第1步: 在各区域中心放置城镇 (3-4 个)
        int townCount = 3 + (int)(GD.Randi() % 2);
        for (int i = 0; i < townCount; i++)
        {
            var region = regions[i % regions.Length];
            var tile = HexGen.FindSettlementPosition(region.Name, 8);
            if (tile == null) continue;

            tile.HasSettlement = true;
            tile.SettlementType = (int)OverworldPOI.POIType.Town;
            tile.PoiId = BladeHex.Data.POINameGenerator.GeneratePOIName(BladeHex.Data.POINameGenerator.POIType.City);

            var poi = new OverworldPOI();
            poi.PoiName = tile.PoiId;
            poi.PoiTypeEnum = OverworldPOI.POIType.Town;
            poi.Position = tile.PixelPos;
            poi.HasTavern = true;
            poi.HasShop = true;
            poi.HasBlacksmith = true;
            poi.GarrisonMax = 30 + (int)(GD.Randi() % 20);
            poi.GarrisonCurrent = poi.GarrisonMax;
            WorldPois.Add(poi);
        }

        // 第2步: 村庄 (8-12 个)
        string[] villageNames = ["柳溪", "石桥", "绿荫", "河畔", "山脚", "枫丹",
                                  "白杨", "谷仓", "松针", "晨露", "暮色", "鹤鸣"];
        int villageCount = 8 + (int)(GD.Randi() % 5);
        for (int i = 0; i < villageCount; i++)
        {
            var region = regions[i % regions.Length];
            if (region.DangerLevel > 0.5f) continue;

            var tile = HexGen.FindSettlementPosition(region.Name, 5);
            if (tile == null) continue;

            tile.HasSettlement = true;
            tile.SettlementType = (int)OverworldPOI.POIType.Village;
            tile.PoiId = villageNames[i % villageNames.Length];

            var poi = new OverworldPOI();
            poi.PoiName = villageNames[i % villageNames.Length];
            poi.PoiTypeEnum = OverworldPOI.POIType.Village;
            poi.Position = tile.PixelPos;
            poi.GarrisonMax = 10 + (int)(GD.Randi() % 10);
            poi.GarrisonCurrent = poi.GarrisonMax;
            WorldPois.Add(poi);
        }

        // 第3步: 城堡 (1-2 个)
        string[] castleNames = ["霜鹰堡", "龙脊关"];
        int castleCount = 1 + (int)(GD.Randi() % 2);
        for (int i = 0; i < castleCount; i++)
        {
            var region = regions[0];
            foreach (var reg in regions)
            {
                if (reg.DangerLevel > 0.3f && reg.DangerLevel < 0.7f)
                {
                    region = reg;
                    break;
                }
            }

            var tile = HexGen.FindSettlementPosition(region.Name, 10);
            if (tile == null) continue;

            tile.HasSettlement = true;
            tile.SettlementType = (int)OverworldPOI.POIType.Castle;
            tile.PoiId = castleNames[i % castleNames.Length];

            var poi = new OverworldPOI();
            poi.PoiName = castleNames[i % castleNames.Length];
            poi.PoiTypeEnum = OverworldPOI.POIType.Castle;
            poi.Position = tile.PixelPos;
            poi.GarrisonMax = 50 + (int)(GD.Randi() % 30);
            poi.GarrisonCurrent = poi.GarrisonMax;
            WorldPois.Add(poi);
        }

        // 第4步: 敌对聚落与巢穴 (6-10 个)
        int lairCount = 6 + (int)(GD.Randi() % 5);
        for (int i = 0; i < lairCount; i++)
        {
            var region = regions[(int)(GD.Randi() % (ulong)regions.Length)];
            var tile = HexGen.FindSettlementPosition(region.Name, 6);
            if (tile == null) continue;

            var poi = new OverworldPOI();
            poi.Position = tile.PixelPos;
            tile.HasSettlement = true;

            // 随机决定是 Settlement(出兵聚落) 还是 Lair(静态巢穴)
            if (GD.Randf() > 0.5f)
            {
                tile.SettlementType = (int)OverworldPOI.POIType.Settlement;
                poi.PoiTypeEnum = OverworldPOI.POIType.Settlement;
                poi.SettlementRaceValue = (OverworldPOI.SettlementRace)(int)(GD.Randi() % 7UL);

                poi.PoiName = poi.SettlementRaceValue switch
                {
                    OverworldPOI.SettlementRace.Goblin => "哥布林营地",
                    OverworldPOI.SettlementRace.Kobold => "狗头人矿坑",
                    OverworldPOI.SettlementRace.Minotaur => "牛头人石堡",
                    OverworldPOI.SettlementRace.ShadowCult => "暗影祭坛",
                    OverworldPOI.SettlementRace.Bandit => "山贼营地",
                    OverworldPOI.SettlementRace.Robber => "劫匪林地",
                    OverworldPOI.SettlementRace.Pirate => "海寇据点",
                    _ => "未知聚落",
                };
            }
            else
            {
                tile.SettlementType = (int)OverworldPOI.POIType.Lair;
                poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
                poi.LairTypeValue = (OverworldPOI.LairType)(int)(GD.Randi() % 8UL);

                poi.PoiName = poi.LairTypeValue switch
                {
                    OverworldPOI.LairType.DragonLair => "巨龙巢穴",
                    OverworldPOI.LairType.AncientTomb => "远古墓穴",
                    OverworldPOI.LairType.Ruins => "古代废墟",
                    OverworldPOI.LairType.GolemForge => "魔像工坊",
                    OverworldPOI.LairType.BanditCamp => "山贼窝点",
                    OverworldPOI.LairType.RobberHideout => "劫匪老巢",
                    OverworldPOI.LairType.PirateCove => "海寇洞穴",
                    OverworldPOI.LairType.RaiderOutpost => "劫掠队前哨",
                    _ => "未知巢穴",
                };
            }

            tile.PoiId = poi.PoiName;
            WorldPois.Add(poi);
        }

        GD.Print($"[OverworldScene] POI 生成完成: {WorldPois.Count} 个");
    }

    // ========================================
    // 7. 战争迷雾初始化
    // ========================================

    /// <summary>
    /// 初始化战争迷雾系统
    /// Chunk 模式下使用世界总像素尺寸初始化像素级迷雾
    /// </summary>
    private void InitFogOfWar()
    {
        int mapW, mapH, cellSz;

        if (_chunkManager != null)
        {
            // Chunk 模式：计算世界像素包围盒
            // 六边形网格的像素范围需要检查所有四个角
            int tileW = _chunkManager.Generator?.WorldWidth ?? 256;
            int tileH = _chunkManager.Generator?.WorldHeight ?? 192;

            // 计算四个角的像素坐标，取包围盒
            var p00 = HexOverworldTile.AxialToPixel(0, 0);
            var pW0 = HexOverworldTile.AxialToPixel(tileW, 0);
            var p0H = HexOverworldTile.AxialToPixel(0, tileH);
            var pWH = HexOverworldTile.AxialToPixel(tileW, tileH);

            float minX = Mathf.Min(Mathf.Min(p00.X, pW0.X), Mathf.Min(p0H.X, pWH.X));
            float maxX = Mathf.Max(Mathf.Max(p00.X, pW0.X), Mathf.Max(p0H.X, pWH.X));
            float minY = Mathf.Min(Mathf.Min(p00.Y, pW0.Y), Mathf.Min(p0H.Y, pWH.Y));
            float maxY = Mathf.Max(Mathf.Max(p00.Y, pW0.Y), Mathf.Max(p0H.Y, pWH.Y));

            mapW = (int)(maxX - minX) + 2000;
            mapH = (int)(maxY - minY) + 2000;
            cellSz = 128; // 平衡粒度：视野边界平滑 + 内存可控
        }
        else
        {
            mapW = (int)HexGrid.MapPixelWidth;
            mapH = (int)HexGrid.MapPixelHeight;
            cellSz = (int)(HEX_TILE_SIZE * 2.0f);
        }

        // 检查是否有存档中的迷雾数据
        var gs = GetNode<GlobalState>("/root/GlobalState");
        if (gs.IsLoadingSave && gs.LoadedData.Count > 0 &&
            gs.LoadedData.TryGetValue("fog_of_war", out var savedFogObj) &&
            savedFogObj.Obj is Godot.Collections.Dictionary savedFog && savedFog.Count > 0)
        {
            Fog = FogOfWar.Deserialize(savedFog);
        }

        // 无存档数据时新建
        if (Fog == null)
        {
            Fog = new FogOfWar();
            Fog.Initialize(mapW, mapH, cellSz);
            RevealHomeTerritory();
        }

        // 创建迷雾渲染器
        FogRenderer = new FogOfWarRenderer();
        FogRenderer.Initialize(Fog, mapW, mapH);
        FogRenderer.ZIndex = 50; // 迷雾覆盖在所有地图元素之上
        AddChild(FogRenderer);

        GD.Print($"[OverworldScene] 迷雾系统初始化: {Fog.GridW}x{Fog.GridH} grid, cell={cellSz}px");
    }

    /// <summary>
    /// 揭示玩家出身种族的母国领土。
    /// 优先使用 WorldCreator 生成的实际领土边界，无数据时回退到硬编码区域。
    /// </summary>
    private void RevealHomeTerritory()
    {
        if (Fog == null) return;

        var playerRace = (RaceData.Race)PlayerRaceId;

        // 有实际领土数据 → 精确揭示
        if (_worldTerritories != null && _worldNations != null)
        {
            var homeNation = RaceNationMapping.FindHomeNation(playerRace, _worldTerritories, _worldNations);
            if (homeNation != null && _worldTerritories.TryGetValue(homeNation.Id, out var territory))
            {
                Fog.RevealTerritory(territory.AllTiles);
                GD.Print($"[OverworldScene] 母国领土揭示: {homeNation.DisplayName}, {territory.TotalTiles} tiles");
                return;
            }
        }

        // 无领土数据 → 硬编码 fallback
        Fog.RevealRaceRegionFallback(playerRace);
        GD.Print($"[OverworldScene] 母国领土揭示: fallback 矩形区域 (race={playerRace})");
    }

    // ========================================
    // 7.5 预渲染全部世界瓦片
    // ========================================

    /// <summary>
    /// 在加载阶段预渲染全部世界 chunk。
    /// 所有瓦片都渲染到 MultiMesh 中，迷雾遮罩由 FogOfWarRenderer 覆盖层处理。
    ///
    /// 强制要求：未探索区域必须是纯黑完全不可见。
    /// 这里渲染全部瓦片是为了让迷雾 shader 有东西可以遮盖（否则是引擎背景色）。
    /// 迷雾 shader 保证 visibility=0 时输出完全不透明黑色。
    /// </summary>
    private void PreRenderRevealedTiles()
    {
        if (_chunkManager == null || HexRenderer == null) return;

        var startTime = Time.GetTicksMsec();
        var generator = _chunkManager.Generator;
        if (generator == null) return;

        // 计算世界总 chunk 数
        int chunksW = generator.WorldWidth / ChunkData.ChunkSize;
        int chunksH = generator.WorldHeight / ChunkData.ChunkSize;

        var chunksToRender = new List<ChunkData>();

        for (int cq = 0; cq < chunksW; cq++)
        {
            for (int cr = 0; cr < chunksH; cr++)
            {
                var coord = new Vector2I(cq, cr);

                // 跳过已在 ActiveChunks 中的（避免重复渲染）
                if (_chunkManager.ActiveChunks.ContainsKey(coord)) continue;

                // 优先从内存缓存取，否则生成
                ChunkData chunk;
                if (_chunkManager.TryGetFromCache(coord, out var cached))
                {
                    chunk = cached;
                }
                else
                {
                    chunk = generator.Generate(cq, cr);
                    if (_chunkManager.RiverRoadStamper != null)
                        _chunkManager.RiverRoadStamper.StampOnChunk(chunk);
                }

                chunksToRender.Add(chunk);
            }
        }

        // 送入渲染器
        if (chunksToRender.Count > 0)
            HexRenderer.OnChunksUpdated(chunksToRender);

        var elapsed = Time.GetTicksMsec() - startTime;
        GD.Print($"[OverworldScene] 预渲染完成: {chunksToRender.Count + _chunkManager.ActiveChunks.Count} chunks, 耗时 {elapsed}ms");
    }

    // ========================================
    // 8. AI 实体管理器初始化
    // ========================================

    /// <summary>
    /// 初始化 AI 实体管理器
    /// </summary>
    private void InitEntityManager()
    {
        // 生成世界实体
        if (_chunkManager == null && WorldGen != null)
        {
            // 旧路径：从 WorldGenerator 生成实体模板
            WorldEntities = WorldGen.GenerateEntityTemplates(WorldPois, HexGrid, HexGen);
        }
        // Chunk 模式下 WorldCreator 已经生成了 POI，实体由 TriggerEngine 驱动
        // 暂时生成空实体列表，后续由每日结算填充

        // 创建实体管理器
        EntityMgr = new OverworldEntityManager();

        if (_chunkManager != null)
        {
            // Chunk 模式：使用 ChunkAStar 进行寻路
            var chunkAstar = new ChunkAStar();
            if (_costGrid != null) chunkAstar.CostGrid = _costGrid;
            EntityMgr.SetChunkNavigation(_chunkManager, chunkAstar);
        }
        else if (HexGrid != null && HexAStar != null)
        {
            EntityMgr.SetHexNavigation(HexGrid, HexAStar);
        }

        // 加载世界数据（转换为 Godot.Array）
        var poiArray = new Godot.Collections.Array();
        foreach (var poi in WorldPois) poiArray.Add(poi);

        var entityArray = new Godot.Collections.Array();
        foreach (var ent in WorldEntities) entityArray.Add(ent);

        EntityMgr.LoadWorld(poiArray, entityArray);

        // 连接所有世界事件信号
        EntityMgr.VillageAttacked += OnVillageAttacked;
        EntityMgr.SiegeStarted += OnSiegeStarted;
        EntityMgr.SiegeResolved += OnSiegeResolved;
        EntityMgr.ReinforcementArrived += OnReinforcementArrived;
        EntityMgr.AiBattleOccurred += OnAiBattle;
        EntityMgr.PoiCaptured += OnPoiCaptured;
        EntityMgr.EntityRemoved += OnEntityRemoved;

        AddChild(EntityMgr);

        // 初始化遭遇视觉系统（监听 spawn/remove 信号 + 触发战斗）
        InitEncounterVisuals();

        // 初始化经济子系统（工资/食物每日结算）
        InitEconomySystems();

        // 初始化委托系统
        InitQuestSystem();
    }

    // ========================================
    // 8.5 特殊角色收容到不活跃池
    // ========================================

    /// <summary>
    /// 将世界生成阶段创建的特殊角色（领主/冒险者）收容到 DormantEntityPool。
    /// 运行时由 EncounterEntitySpawner 按需激活。
    /// </summary>
    private void StoreSpecialCharactersIntoDormantPool()
    {
        if (_specialCharacters == null || _specialCharacters.Count == 0 || EntityMgr == null) return;

        int stored = 0;
        foreach (var character in _specialCharacters)
        {
            // 领主直接加入活跃实体列表（它们绑定 POI，需要立即参与 AI 决策）
            if (character.EntityTypeEnum == OverworldEntity.EntityType.LordArmy)
            {
                EntityMgr.Entities.Add(character);
            }
            else
            {
                // 冒险者收容到不活跃池，遭遇时复用
                EntityMgr.StoreToDormantPool(character);
                stored++;
            }
        }

        GD.Print($"[OverworldScene] 特殊角色分配: {_specialCharacters.Count - stored} 领主激活, {stored} 冒险者入池");
        _specialCharacters = null; // 释放引用
    }

    // ========================================
    // 9. 渲染世界 POI 视觉节点
    // ========================================

    /// <summary>
    /// 为每个 POI 创建视觉节点 — 使用彩色正方形占位符，按规模区分大小。
    /// 城镇/城堡: 3×3 瓦片大小, 村庄: 2×2, 小型设施: 1×1
    /// </summary>
    private void RenderWorldPois()
    {
        foreach (var poi in WorldPois)
        {
            Node2D visual;

            // 根据 POI 类型确定颜色和大小（以瓦片为单位）
            var (color, tileScale) = GetPoiVisualParams(poi);
            float pixelSize = tileScale * HEX_TILE_SIZE;

            // 创建简单的彩色正方形占位符
            visual = CreatePoiPlaceholder(poi, color, pixelSize);
            visual.Position = poi.Position;
            visual.ZIndex = 5;
            AddChild(visual);
            PoiVisuals.Add(visual);
        }
    }

    /// <summary>获取 POI 的视觉参数（颜色 + 瓦片缩放）</summary>
    private static (Color color, float tileScale) GetPoiVisualParams(OverworldPOI poi)
    {
        return poi.PoiTypeEnum switch
        {
            OverworldPOI.POIType.Town => (new Color(0.2f, 0.4f, 0.85f), 3.0f),      // 蓝色 3×3
            OverworldPOI.POIType.Castle => (new Color(0.6f, 0.4f, 0.15f), 3.0f),     // 棕金 3×3
            OverworldPOI.POIType.Village => (new Color(0.3f, 0.65f, 0.3f), 2.0f),    // 绿色 2×2
            OverworldPOI.POIType.Outpost => (new Color(0.5f, 0.5f, 0.2f), 2.0f),     // 暗黄 2×2
            OverworldPOI.POIType.Port => (new Color(0.2f, 0.5f, 0.7f), 2.0f),        // 青蓝 2×2
            OverworldPOI.POIType.Tavern => (new Color(0.7f, 0.5f, 0.2f), 1.5f),      // 橙色 1.5×1.5
            OverworldPOI.POIType.Mine => (new Color(0.5f, 0.4f, 0.3f), 1.5f),        // 土色 1.5×1.5
            OverworldPOI.POIType.Farm => (new Color(0.6f, 0.7f, 0.2f), 1.5f),        // 黄绿 1.5×1.5
            OverworldPOI.POIType.Shrine => (new Color(0.7f, 0.6f, 0.9f), 1.5f),      // 紫色 1.5×1.5
            OverworldPOI.POIType.Settlement => (new Color(0.7f, 0.3f, 0.2f), 2.0f),  // 红色 2×2
            OverworldPOI.POIType.Lair => (new Color(0.5f, 0.2f, 0.2f), 1.5f),        // 暗红 1.5×1.5
            _ => (new Color(0.5f, 0.5f, 0.5f), 1.0f),
        };
    }

    /// <summary>
    /// 创建 POI 占位符视觉节点 — 彩色正方形 + 名称标签。
    /// 使用 Polygon2D 绘制正方形，确保在任何缩放级别都清晰可见。
    /// </summary>
    private static Node2D CreatePoiPlaceholder(OverworldPOI poi, Color color, float size)
    {
        var node = new Node2D();
        node.Name = $"POI_{poi.PoiName}";

        // 正方形主体
        var poly = new Polygon2D();
        float half = size * 0.5f;
        poly.Polygon = new Vector2[]
        {
            new(-half, -half),
            new(half, -half),
            new(half, half),
            new(-half, half),
        };
        poly.Color = color;
        node.AddChild(poly);

        // 边框（深色描边效果 — 用稍大的深色正方形在下层）
        var border = new Polygon2D();
        float borderHalf = half + 4.0f;
        border.Polygon = new Vector2[]
        {
            new(-borderHalf, -borderHalf),
            new(borderHalf, -borderHalf),
            new(borderHalf, borderHalf),
            new(-borderHalf, borderHalf),
        };
        border.Color = new Color(color.R * 0.3f, color.G * 0.3f, color.B * 0.3f);
        border.ZIndex = -1;
        node.AddChild(border);

        // 名称标签
        var label = new Label();
        label.Text = poi.PoiName;
        label.Position = new Vector2(-half, half + 6.0f);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeFontSizeOverride("font_size", 28);
        node.AddChild(label);

        return node;
    }

    // ========================================
    // 9.5 初始化已发现 POI（出身种族领土内的 POI 永久可见）
    // ========================================

    /// <summary>
    /// 标记出身种族领土内的所有 POI 为永久可见。
    /// 使用迷雾系统的已揭示状态判断：种族初始揭示区域内的 POI 自动发现。
    /// </summary>
    private void InitDiscoveredPois()
    {
        if (Fog == null) return;

        for (int i = 0; i < WorldPois.Count; i++)
        {
            var poi = WorldPois[i];
            // 种族领土内的 POI（迷雾已揭示）→ 永久可见
            if (Fog.IsRevealed(poi.Position.X, poi.Position.Y))
            {
                _discoveredPoiIndices.Add(i);
            }
        }

        GD.Print($"[OverworldScene] 已发现 POI: {_discoveredPoiIndices.Count}/{WorldPois.Count}");
    }

    /// <summary>
    /// 将指定索引的 POI 标记为永久可见（玩家探索到时调用）
    /// </summary>
    private void DiscoverPoi(int poiIndex)
    {
        _discoveredPoiIndices.Add(poiIndex);
    }

    /// <summary>
    /// 创建城镇/村庄/城堡视觉节点
    /// GD: _create_town_visual (lines 439-451)
    /// </summary>
    private static Node2D CreateTownVisual(OverworldPOI poi, Color color, float size)
    {
        var town = new OverworldTown();
        town.TownName = poi.PoiName;
        town.Position = poi.Position;

        if (poi.PoiTypeEnum == OverworldPOI.POIType.Village)
        {
            town.TownType = "village";
        }
        else if (poi.PoiTypeEnum == OverworldPOI.POIType.Castle)
        {
            town.TownType = "castle";
        }

        // OverworldTown 在 _Ready 中自动创建视觉多边形
        // 颜色由 TownType 决定: town(蓝) / village(绿)
        return town;
    }

    /// <summary>
    /// 创建聚落/巢穴标记视觉节点
    /// GD: _create_marker_visual (lines 453-459)
    /// </summary>
    private static Node2D CreateMarkerVisual(OverworldPOI poi, Color color, float size)
    {
        var marker = new OverworldEnemy();
        marker.DisplayName = poi.PoiName;
        marker.PlaceAt(poi.Position.X, poi.Position.Y);

        // OverworldEnemy 在 _Ready 中自动创建视觉多边形
        // 人形显示为黄色, 非人形显示为红色
        // 如需自定义颜色需扩展 OverworldEnemy 类
        return marker;
    }

    /// <summary>
    /// 获取外族聚落颜色
    /// GD: _get_settlement_color (lines 461-470)
    /// </summary>
    private static Color GetSettlementColor(OverworldPOI.SettlementRace race)
    {
        return race switch
        {
            OverworldPOI.SettlementRace.Goblin => new Color(0.6f, 0.4f, 0.2f),
            OverworldPOI.SettlementRace.Kobold => new Color(0.5f, 0.4f, 0.3f),
            OverworldPOI.SettlementRace.Minotaur => new Color(0.7f, 0.3f, 0.2f),
            OverworldPOI.SettlementRace.ShadowCult => new Color(0.4f, 0.2f, 0.5f),
            OverworldPOI.SettlementRace.Bandit => new Color(0.6f, 0.3f, 0.2f),
            OverworldPOI.SettlementRace.Robber => new Color(0.5f, 0.3f, 0.3f),
            OverworldPOI.SettlementRace.Pirate => new Color(0.2f, 0.4f, 0.6f),
            _ => new Color(0.6f, 0.3f, 0.3f),
        };
    }

    /// <summary>
    /// 获取巢穴颜色
    /// GD: _get_lair_color (lines 472-482)
    /// </summary>
    private static Color GetLairColor(OverworldPOI.LairType lairType)
    {
        return lairType switch
        {
            OverworldPOI.LairType.DragonLair => new Color(0.8f, 0.6f, 0.1f),
            OverworldPOI.LairType.AncientTomb => new Color(0.4f, 0.4f, 0.5f),
            OverworldPOI.LairType.Ruins => new Color(0.6f, 0.5f, 0.3f),
            OverworldPOI.LairType.GolemForge => new Color(0.5f, 0.3f, 0.2f),
            OverworldPOI.LairType.BanditCamp => new Color(0.6f, 0.3f, 0.2f),
            OverworldPOI.LairType.RobberHideout => new Color(0.5f, 0.3f, 0.3f),
            OverworldPOI.LairType.PirateCove => new Color(0.2f, 0.4f, 0.6f),
            OverworldPOI.LairType.RaiderOutpost => new Color(0.6f, 0.2f, 0.2f),
            _ => new Color(0.5f, 0.5f, 0.5f),
        };
    }

    // ========================================
    // 10. 玩家队伍初始化
    // ========================================

    /// <summary>
    /// 初始化玩家队伍（OverworldParty）
    /// 按 PlayerRaceId 找到对应的母国，将玩家放到该国首都附近
    /// </summary>
    private void InitPlayerParty()
    {
        // 创建 C# OverworldParty
        PlayerParty = new OverworldParty();
        PlayerParty.SetHexNavigation(HexGrid, HexAStar);

        // Chunk 模式：注入 ChunkAStar 寻路（优先于 HexAStar）
        if (_chunkManager != null)
        {
            var chunkAstar = new ChunkAStar();
            if (_costGrid != null) chunkAstar.CostGrid = _costGrid;
            PlayerParty.SetChunkNavigation(_chunkManager, chunkAstar);
        }

        AddChild(PlayerParty);
        PlayerParty.ZIndex = 10; // 玩家在所有地图元素之上

        Vector2 startPos;

        if (_chunkManager != null)
        {
            startPos = FindChunkModeStartPosition();
        }
        else
        {
            // 旧路径：HexGrid 中寻找
            startPos = FindHexStartPosition();
            if (startPos == Vector2.Zero)
            {
                var startTown = FindNearestPoiOfType(HexGrid.GetCenterPixel(), OverworldPOI.POIType.Town);
                if (startTown != null)
                {
                    var tile = HexGrid.FindPassableNearPixel(
                        startTown.Position.X + 50.0f,
                        startTown.Position.Y + 50.0f,
                        15);
                    if (tile != null)
                        startPos = tile.PixelPos;
                    else
                        startPos = HexGrid.GetValidStartPos();
                }
                else
                {
                    startPos = HexGrid.GetValidStartPos();
                }
            }
        }

        PlacePlayerAt(startPos.X, startPos.Y);

        // 摄像机跟随玩家
        MainCamera.Position = PlayerParty.Position;
    }

    /// <summary>
    /// Chunk 模式下基于玩家种族挑选出生点：
    /// 1. 有母国首都 POI → 首都附近 4 格内可通行格
    /// 2. 母国有领土但没首都 POI → 领土的几何中心附近
    /// 3. 没有母国 → 任何城镇附近
    /// 4. 全都没有 → 世界轴向中心
    /// </summary>
    private Vector2 FindChunkModeStartPosition()
    {
        // 默认回退：世界中心
        Vector2 fallback = HexOverworldTile.AxialToPixel(_chunkWorldCenter.X, _chunkWorldCenter.Y);

        if (_worldNations == null || _worldTerritories == null || _worldNations.Count == 0)
        {
            // 世界数据不完整（读档或错误），退化到最近城镇
            if (WorldPois != null && WorldPois.Count > 0)
            {
                var anyTown = FindNearestPoiOfType(fallback, OverworldPOI.POIType.Town);
                if (anyTown != null) return FindPassableNearCity(anyTown.Position, 4);
            }
            return fallback;
        }

        var playerRace = (RaceData.Race)PlayerRaceId;
        var homeNation = RaceNationMapping.FindHomeNation(playerRace, _worldTerritories, _worldNations);

        if (homeNation == null)
        {
            GD.Print($"[OverworldScene] 未找到种族 {playerRace} 的母国，出生在世界中心");
            return fallback;
        }

        GD.Print($"[OverworldScene] 玩家种族 {playerRace} → 母国 {homeNation.DisplayName} ({homeNation.Id})");

        // 1. 优先找母国首都（PoiName 含"首都"或 OwningFaction 匹配的 Town）
        if (WorldPois != null && WorldPois.Count > 0)
        {
            OverworldPOI? capital = null;
            OverworldPOI? anyTownOfNation = null;

            foreach (var poi in WorldPois)
            {
                if (poi.OwningFaction != homeNation.Id) continue;
                if (poi.PoiTypeEnum != OverworldPOI.POIType.Town) continue;

                anyTownOfNation ??= poi;
                if (poi.PoiName.Contains("首都"))
                {
                    capital = poi;
                    break;
                }
            }

            var chosen = capital ?? anyTownOfNation;
            if (chosen != null)
            {
                var spawnPos = FindPassableNearCity(chosen.Position, 4);
                GD.Print($"[OverworldScene] 出生点: {chosen.PoiName} 附近 @ {spawnPos}");
                return spawnPos;
            }
        }

        // 2. 没有 POI 时用领土中心
        if (_worldTerritories.TryGetValue(homeNation.Id, out var territory))
        {
            var centroid = territory.CoreZone.Centroid;
            var pos = HexOverworldTile.AxialToPixel(centroid.X, centroid.Y);
            var spawnPos = FindPassableNearCity(pos, 4);
            GD.Print($"[OverworldScene] 出生点（领土中心附近）: {centroid} → {spawnPos}");
            return spawnPos;
        }

        return fallback;
    }

    /// <summary>
    /// 在城市位置附近 maxRadius 格内寻找可通行格作为出生点。
    /// 优先选择 1~maxRadius 格距离的格子（不直接站在城市上），
    /// 如果找不到则回退到城市位置本身。
    /// </summary>
    private Vector2 FindPassableNearCity(Vector2 cityPixelPos, int maxRadius)
    {
        // Chunk 模式：通过 ChunkManager 查找
        if (_chunkManager != null)
        {
            var cityCoord = HexOverworldTile.PixelToAxial(cityPixelPos.X, cityPixelPos.Y);
            var cityCube = HexOverworldTile.AxialToCube(cityCoord.X, cityCoord.Y);

            // 从 ring 1 开始搜索（避免直接站在城市格上）
            for (int ring = 1; ring <= maxRadius; ring++)
            {
                var ringCoords = HexOverworldTile.CubeRing(cityCube, ring);
                // 随机打乱环上的格子，避免总是出生在同一方向
                ShuffleArray(ringCoords);
                foreach (var cube in ringCoords)
                {
                    var axial = HexOverworldTile.CubeToAxial(cube);
                    var tile = _chunkManager.GetTile(axial.X, axial.Y);
                    if (tile != null && tile.IsPassable)
                        return tile.PixelPos;
                }
            }

            // 所有环都没找到，回退到城市位置附近最近可通行格
            var fallbackTile = _chunkManager.GetTile(cityCoord.X, cityCoord.Y);
            if (fallbackTile != null && fallbackTile.IsPassable)
                return fallbackTile.PixelPos;
        }

        // 非 Chunk 模式：使用 HexGrid
        if (HexGrid != null)
        {
            var tile = HexGrid.FindPassableNearPixel(cityPixelPos.X, cityPixelPos.Y, maxRadius);
            if (tile != null) return tile.PixelPos;
        }

        return cityPixelPos;
    }

    /// <summary>Fisher-Yates 洗牌</summary>
    private static void ShuffleArray<T>(T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = (int)(GD.Randi() % (uint)(i + 1));
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    /// <summary>
    /// 在六边形地图上找玩家起始位置
    /// GD: _find_hex_start_position (lines 380-390)
    /// </summary>
    private Vector2 FindHexStartPosition()
    {
        if (HexGrid == null) return Vector2.Zero;

        // 优先找城镇瓦片
        foreach (var tile in HexGrid.GetSettlementTiles())
        {
            if (tile.SettlementType == (int)OverworldPOI.POIType.Town && tile.IsPassable)
                return tile.PixelPos;
        }

        // 回退: 地图中心附近可通行
        return HexGrid.GetValidStartPos();
    }

    /// <summary>
    /// 查找指定类型的最近 POI
    /// GD: _find_nearest_poi_of_type (lines 489-498)
    /// </summary>
    private OverworldPOI? FindNearestPoiOfType(Vector2 pos, OverworldPOI.POIType type)
    {
        OverworldPOI? closest = null;
        float closestDist = 99999.0f;
        foreach (var poi in WorldPois)
        {
            if (poi.PoiTypeEnum == type)
            {
                float d = pos.DistanceTo(poi.Position);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = poi;
                }
            }
        }
        return closest;
    }

    // ========================================
    // 11. 移动速度组件初始化
    // ========================================

    /// <summary>
    /// 初始化移动速度组件
    /// GD: _init_speed_component (lines 554-559)
    /// </summary>
    private void InitSpeedComponent()
    {
        var speedComp = new MovementSpeedComponent();
        speedComp.HexGridRef = HexGrid;
        speedComp.EconomyManagerRef = EconomyMgr;
        speedComp.UnitDataRef = PlayerUnitData;

        // Chunk 模式: 注入 ChunkManager 和 ZoC 管理器
        if (_chunkManager != null)
            speedComp.ChunkManagerRef = _chunkManager;
        if (_zocManager != null)
            speedComp.ZocManagerRef = _zocManager;

        // 玩家阵营（用于 ZoC 敌对判定）
        if (_worldNations != null && _worldNations.Count > 0 && _worldTerritories != null)
        {
            var homeNation = RaceNationMapping.FindHomeNation(
                (BladeHex.Data.RaceData.Race)PlayerRaceId, _worldTerritories, _worldNations);
            if (homeNation != null)
                speedComp.PlayerFaction = homeNation.Id;
        }

        if (PlayerParty != null)
            PlayerParty.SpeedComponent = speedComp;
    }

    // ========================================
    // 11.5 天气与环境特效系统初始化
    // ========================================

    /// <summary>
    /// 初始化天气管理器和环境特效渲染层
    /// </summary>
    private void InitWeatherSystem()
    {
        // 创建天气管理器
        WeatherMgr = new BladeHex.View.Environment.WeatherManager();
        WeatherMgr.Name = "WeatherManager";
        AddChild(WeatherMgr);

        // 天气变化时更新 UI（事件驱动，不每帧轮询）
        WeatherMgr.WeatherChanged += OnWeatherChanged;

        // 创建环境特效层（shader 地面特效）
        EnvironmentFx = new BladeHex.View.Environment.EnvironmentEffectsLayer();
        EnvironmentFx.Name = "EnvironmentEffectsLayer";
        EnvironmentFx.Initialize(WeatherMgr, MainCamera);
        AddChild(EnvironmentFx);

        // 创建 GPU 粒子天气系统（替代 shader 循环，兼容 Mobile 渲染器）
        _weatherParticles2D = new BladeHex.View.Environment.WeatherParticles2D();
        _weatherParticles2D.Name = "WeatherParticles2D";
        AddChild(_weatherParticles2D);

        GD.Print("[OverworldScene] 天气与环境特效系统初始化完成");
    }

    /// <summary>天气变化回调 — 更新顶部 UI 天气显示 + 粒子系统</summary>
    private void OnWeatherChanged(int oldWeather, int newWeather)
    {
        var weatherType = (BladeHex.View.Environment.WeatherType)newWeather;
        string weatherName = weatherType switch
        {
            BladeHex.View.Environment.WeatherType.Rain => "🌧 雨天",
            BladeHex.View.Environment.WeatherType.Snow => "🌨 雪天",
            BladeHex.View.Environment.WeatherType.Sandstorm => "🌪 沙尘暴",
            _ => "☀ 晴天",
        };

        if (UI is BladeHex.View.UI.Overworld.OverworldUI overworldUi)
            overworldUi.UpdateWeatherDisplay(weatherName);

        // 同步到环境音频组件（自动切换BGM和环境音）
        _envAudio?.SetWeather(MapWeatherToAudioWeather(weatherType));

        // 更新 GPU 粒子系统
        if (_weatherParticles2D != null)
        {
            if (weatherType == BladeHex.View.Environment.WeatherType.Clear)
                _weatherParticles2D.StopAll();
            else
                _weatherParticles2D.SetWeather(weatherType, WeatherMgr.GetEffectiveIntensity());
        }
    }

    /// <summary>映射 View.Environment.WeatherType 到 Audio.EnvironmentAudioComponent.WeatherType</summary>
    private static BladeHex.Audio.EnvironmentAudioComponent.WeatherType MapWeatherToAudioWeather(BladeHex.View.Environment.WeatherType wt)
    {
        return wt switch
        {
            BladeHex.View.Environment.WeatherType.Rain => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Rain,
            BladeHex.View.Environment.WeatherType.Snow => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Snow,
            BladeHex.View.Environment.WeatherType.Sandstorm => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Sandstorm,
            _ => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Clear,
        };
    }

    // ========================================
    // 11.6 消息提示初始化
    // ========================================

    private void InitToastNotification()
    {
        Toast = new BladeHex.View.UI.Overworld.ToastNotification();
        Toast.Name = "ToastNotification";
        AddChild(Toast);
    }

    // ========================================
    // 11.7 道路网络初始化
    // ========================================

    /// <summary>
    /// 初始化道路和河流渲染器
    /// </summary>
    private void InitRoadNetwork()
    {
        // 道路渲染
        RoadRenderer = new BladeHex.View.Map.RoadRenderer();
        RoadRenderer.Name = "RoadRenderer";
        RoadRenderer.Initialize(_chunkManager);
        AddChild(RoadRenderer);
        RoadRenderer.RebuildFromAllKnownTiles();

        // 如果没找到道路瓦片（旧存档），用直连贝塞尔曲线作为视觉回退
        if (RoadRenderer.RoadCount == 0 && WorldPois.Count >= 2)
        {
            GD.Print("[OverworldScene] 未找到道路瓦片数据，使用视觉回退生成道路");
            RoadRenderer.GenerateFallbackRoads(WorldPois);
        }

        // 河流不再使用贝塞尔曲线渲染 — 仅通过地形瓦片颜色表达
        // RiverRenderer 已移除

        GD.Print("[OverworldScene] 道路渲染初始化完成");
    }

    // ========================================
    // 11.1 多层级地图标签
    // ========================================

    /// <summary>
    /// 初始化地图标签层 — 根据缩放级别显示世界名/区域名/国家名/POI名
    /// </summary>
    private void InitMapLabels()
    {
        if (MainCamera == null) return;

        int tileW = _chunkManager?.Generator?.WorldWidth ?? 576;
        int tileH = _chunkManager?.Generator?.WorldHeight ?? 384;
        int seed = _chunkManager?.Generator?.WorldSeed ?? 0;
        var worldCenter = HexOverworldTile.AxialToPixel(tileW / 2, tileH / 2);

        var labelLayer = new BladeHex.View.Map.MapLabelLayer();
        labelLayer.Initialize(MainCamera, worldCenter, WorldPois, _worldTerritories, _worldNations, tileW, tileH, seed);
        AddChild(labelLayer);

        GD.Print("[OverworldScene] 地图标签层初始化完成");
    }

    // ========================================
    // 11.2 预计算寻路代价网格
    // ========================================

    /// <summary>
    /// 初始化 PathfindingCostGrid — 为所有活跃 chunk 预计算代价数组。
    /// 注入到 ChunkAStar 中加速寻路。
    /// </summary>
    private void InitPathfindingGrid()
    {
        if (_chunkManager == null) return;

        _costGrid = new PathfindingCostGrid();

        // 为当前所有活跃 chunk 预计算代价
        foreach (var chunk in _chunkManager.ActiveChunks.Values)
            _costGrid.OnChunkLoaded(chunk);

        // 注入到玩家的 ChunkAStar
        if (PlayerParty?.ChunkAStar != null)
            PlayerParty.ChunkAStar.CostGrid = _costGrid;

        GD.Print($"[OverworldScene] 寻路代价网格初始化: {_chunkManager.ActiveChunks.Count} chunks 已缓存");
    }

    // ========================================
    // 11.3 敌对 POI 控制区初始化
    // ========================================

    /// <summary>
    /// 初始化 ZoneOfControlManager — 预计算所有敌对 POI 的控制区。
    /// 将 ZoC 代价写入 PathfindingCostGrid。
    /// </summary>
    private void InitZoneOfControl()
    {
        if (WorldPois.Count == 0) return;

        _zocManager = new ZoneOfControlManager();
        _zocManager.Initialize(WorldPois);

        // 将敌对 ZoC 写入代价网格
        if (_costGrid != null && _worldNations != null && _worldTerritories != null)
        {
            // 确定玩家阵营
            string playerFaction = "";
            var homeNation = RaceNationMapping.FindHomeNation(
                (BladeHex.Data.RaceData.Race)PlayerRaceId, _worldTerritories, _worldNations);
            if (homeNation != null) playerFaction = homeNation.Id;

            var hostileZocTiles = _zocManager.GetAllHostileZocTiles(playerFaction);
            if (hostileZocTiles.Count > 0)
            {
                _costGrid.UpdateZocRegion(hostileZocTiles, ZoneOfControlManager.ZocPathfindingMultiplier);
                GD.Print($"[OverworldScene] ZoC 代价层: {hostileZocTiles.Count} tiles 受敌对控制区影响");
            }
        }
    }

    // ========================================
    // 12. UI 初始化
    // ========================================

    /// <summary>
    /// 根据玩家来源（出身选择 / 快速游戏 / 读档）构建初始 PlayerUnitData
    /// - 出身选择：使用 PlayerOrigin["unit_data"] 里的完整数据
    /// - 快速游戏：按 PlayerRaceId 用 CharacterGenerator 随机生成属性+特质
    /// - 读档：PlayerUnitData 由存档流程恢复（此处兜底用）
    /// </summary>
    private UnitData BuildInitialPlayerUnit()
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");

        // 1. 从出身选择拿到的完整 unit_data（OriginSelect 已构建好属性和特质）
        if (gs.PlayerOrigin.TryGetValue("unit_data", out var unitObj) &&
            unitObj.Obj is UnitData originUnit)
        {
            // 判定是否为"真正的完整角色"：race 已填且 Str/Dex/Con 不全为默认
            bool isRealCharacter = originUnit.Race != null &&
                (originUnit.Str > 0 && originUnit.Dex > 0 && originUnit.Con > 0);
            if (isRealCharacter && !gs.IsQuickGame)
            {
                GD.Print($"[OverworldScene] 使用出身选择的角色: {originUnit.UnitName}");
                return originUnit;
            }
        }

        // 2. 快速游戏：用 CharacterGenerator 随机生成
        if (gs.IsQuickGame)
        {
            var race = RaceData.GetRaceById((RaceData.Race)PlayerRaceId);
            var rolled = CharacterGenerator.GenerateCharacter(race, level: 1, seedVal: -1);
            GD.Print($"[OverworldScene] 快速游戏生成角色: {rolled.UnitName} " +
                     $"({rolled.Race?.RaceName} Lv{rolled.Level} " +
                     $"Str{rolled.Str} Dex{rolled.Dex} Con{rolled.Con} " +
                     $"Int{rolled.Intel} Wis{rolled.Wis} Cha{rolled.Cha}) " +
                     $"traits={rolled.CharacterTraits.Count}");
            return rolled;
        }

        // 3. 兜底：最低限度人类战士
        var fallback = new UnitData();
        fallback.UnitName = "冒险者";
        fallback.Race = RaceData.GetRaceById(RaceData.Race.Human);
        fallback.Str = 14;
        fallback.Dex = 12;
        fallback.Con = 13;
        fallback.Intel = 10;
        fallback.Wis = 10;
        fallback.Cha = 10;
        fallback.BaseAc = 10;
        fallback.BaseMaxHp = 20;
        fallback.BaseMoveRange = 4;
        fallback.Level = 1;
        return fallback;
    }

    /// <summary>
    /// 初始化队伍名册：队长 + 起始队员
    /// - 出身选择：队长 + 1 个同种族新兵
    /// - 快速游戏：队长 + 2 个随机队员
    /// </summary>
    private void InitPartyRoster()
    {
        if (PlayerParty == null || PlayerUnitData == null) return;

        var roster = PlayerParty.Roster;
        roster.SetLeader(PlayerUnitData);

        var gs = GetNode<GlobalState>("/root/GlobalState");
        int startingCompanions = gs.IsQuickGame ? 2 : 1;

        var playerRace = PlayerUnitData.Race;

        for (int i = 0; i < startingCompanions; i++)
        {
            // 生成同种族或随机种族的 1 级队员
            RaceData? companionRace = playerRace;
            if (gs.IsQuickGame && GD.Randf() > 0.5f)
            {
                var allRaces = RaceData.GetAllRaces();
                companionRace = allRaces[(int)(GD.Randi() % (uint)allRaces.Length)];
            }

            var companion = CharacterGenerator.GenerateCharacter(companionRace, level: 1, seedVal: -1);
            PartyRoster.SetCurrentHp(companion, companion.BaseMaxHp);
            roster.Add(companion);
        }

        GD.Print($"[OverworldScene] 队伍初始化: {roster}");

        // 通知大地图玩家视觉同步领袖外观（统一渲染组件）
        PlayerParty.SyncVisualFromRoster();
    }

    /// <summary>
    /// 初始化大地图 UI
    /// GD: _ready UI setup (lines 205-214)
    /// </summary>
    private void InitUI()
    {
        // 使用 C# OverworldUI 类（已从 迁移）
        var overworldUi = new BladeHex.View.UI.Overworld.OverworldUI();
        overworldUi.EconomyManager = EconomyMgr;
        UI = overworldUi;
        AddChild(UI);

        // 连接菜单打开信号
        overworldUi.MenuOpened += OnUiMenuOpened;

        // 初始化玩家角色数据
        PlayerUnitData = BuildInitialPlayerUnit();

        // 初始化队伍名册
        InitPartyRoster();

        // 刷新速度组件引用（角色数据初始化后）
        if (PlayerParty?.SpeedComponent != null)
        {
            PlayerParty.SpeedComponent.UnitDataRef = PlayerUnitData;
        }

        // 更新 UI 信息
        UpdateUIInfo();
    }

    /// <summary>初始化右上角小地图</summary>
    private void InitMinimap()
    {
        float mapW = Fog?.MapWidthPx ?? 0;
        float mapH = Fog?.MapHeightPx ?? 0;
        if (mapW <= 0 || mapH <= 0) return;

        var minimapLayer = new CanvasLayer { Layer = 60, Name = "MinimapLayer" };
        AddChild(minimapLayer);

        _minimap = new BladeHex.View.UI.Overworld.MinimapPanel();
        _minimap.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _minimap.OffsetLeft = -348;
        _minimap.OffsetTop = 60;
        _minimap.OffsetRight = -12;
        _minimap.OffsetBottom = 310;
        minimapLayer.AddChild(_minimap);

        _minimap.Initialize(Fog!, _chunkManager, WorldPois, mapW, mapH);

        // 点击小地图 → 玩家寻路到该位置 + 镜头跟随
        _minimap.MinimapClicked += (worldPos) =>
        {
            MovePlayerTo(worldPos);
            RecenterCameraOnPlayer();
            _isFollowingPlayer = true;
        };

        // 点击小地图 POI → 玩家寻路到该 POI + 镜头跟随
        _minimap.MinimapPoiClicked += (worldPos) =>
        {
            MovePlayerTo(worldPos);
            RecenterCameraOnPlayer();
            _isFollowingPlayer = true;
        };
    }

    // ========================================
    // 13. 游戏设置应用
    // ========================================

    /// <summary>
    /// 从 GameSettings 加载并应用运行时参数
    /// GD: _apply_game_settings (lines 628-632)
    /// </summary>
    private void ApplyGameSettings()
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");
        var settings = gs.GetSettings();
        GameTimeScale = settings.GameSpeed;
        settings.ApplyToEngine();
    }

    /// <summary>
    /// UI菜单打开时的处理
    /// GD: 对应 ui.menu_opened 信号
    /// </summary>
    private void OnUiMenuOpened(string menuName)
    {
        GD.Print($"[OverworldScene] UI菜单打开: {menuName}");
    }
}
