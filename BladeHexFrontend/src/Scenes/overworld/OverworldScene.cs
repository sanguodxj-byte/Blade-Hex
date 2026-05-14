// OverworldScene.cs
// [T-601] OverworldScene — 战略层大地图场景 (C# 迁移版)
// 负责集成地图渲染、AI 生态、交互系统和时间流逝。
// 逻辑拆分到 partial 类: Init / Interaction / Process / Combat / Debug
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.Map;
using BladeHex.Data;
using BladeHex.View.Map;
using BladeHex.View.Environment;

namespace BladeHex.Scenes.Overworld;

[GlobalClass]
public partial class OverworldScene : Node2D, IOverworldContext
{
    // ========================================
    // 常量
    // ========================================
    public const float HEX_TILE_SIZE = 156.0f;
    public const float VISION_RANGE = 3000.0f;
    public const float ENCOUNTER_DIST = 80.0f;
    public const float POI_ENTER_DIST = 450.0f;
    public const float QUEST_TARGET_APPROACH_DIST = 600.0f;
    public const float BRIGHTNESS_FLOOR = 0.22f;

    // ========================================
    // 核心组件
    // ========================================
    public OverworldParty PlayerParty { get; set; } = null!;
    public Camera2D MainCamera { get; set; } = null!;
    public CanvasLayer UI { get; set; } = null!;       // OverworldUI — CanvasLayer 基类
    public EconomyManager EconomyMgr { get; set; } = null!;
    public BladeHex.Strategic.QuestManager QuestMgr { get; set; } = null!;
    public InteractionManager InteractionMgr { get; set; } = null!;
    public OverworldEntityManager EntityMgr { get; set; } = null!;

    // ========================================
    // 地图与渲染
    // ========================================
    public HexOverworldGrid HexGrid { get; set; } = null!;
    public HexOverworldGenerator HexGen { get; set; } = null!;
    public HexOverworldRenderer HexRenderer { get; set; } = null!;
    public HexOverworldRenderer3D? HexRenderer3D { get; set; }
    public HexOverworldAStar HexAStar { get; set; } = null!;
    public FogOfWar Fog { get; set; } = null!;
    public FogOfWarRenderer FogRenderer { get; set; } = null!;
    public WeatherManager WeatherMgr { get; set; } = null!;
    public EnvironmentEffectsLayer EnvironmentFx { get; set; } = null!;
    private WeatherParticles2D? _weatherParticles2D;
    public BladeHex.View.Map.RoadRenderer RoadRenderer { get; set; } = null!;
    public BladeHex.View.UI.Overworld.ToastNotification Toast { get; set; } = null!;
    private BladeHex.View.UI.Overworld.MinimapPanel? _minimap;

    /// <summary>3D 渲染子视口（嵌入式 3D 地面渲染）</summary>
    private SubViewportContainer? _3dViewportContainer;
    private SubViewport? _3dViewport;
    private OverworldCamera3D? _3dCamera;

    // ========================================
    // 状态数据
    // ========================================
    public List<OverworldPOI> WorldPois = new();
    public List<OverworldEntity> WorldEntities = new();
    public List<Node2D> PoiVisuals = new();

    /// <summary>永久可见的 POI 索引集合（出身种族领土 + 已探索到的）</summary>
    private HashSet<int> _discoveredPoiIndices = new();

    public int PlayerRaceId { get; set; } = 0;
    public UnitData PlayerUnitData { get; set; } = null!;

    public float GameTimeScale = 0.5f;
    public bool IsTimePaused = false;
    public bool IsWaiting { get; set; } = false;

    protected bool _poiEntered = false;
    protected int _poiLeaveCooldown = 0;
    protected bool _encounterActive = false;
    protected OverworldEntity? _lastEncounteredEntity;

    public CanvasModulate SceneCanvasModulate { get; set; } = null!;
    public Gradient TimeGradient { get; set; } = null!;
    public WorldGenerator WorldGen { get; set; } = null!;

    // ========================================
    // Chunk 模式字段
    // ========================================
    private ChunkManager? _chunkManager;
    private string? _chunkSaveId;
    private List<BiomeZone>? _worldZones;
    private Dictionary<string, NationTerritory>? _worldTerritories;
    private List<NationConfig>? _worldNations;
    private List<OverworldEntity>? _specialCharacters;

    /// <summary>Chunk 模式下世界中心的轴向坐标（用于玩家默认起始位置）</summary>
    private Vector2I _chunkWorldCenter = Vector2I.Zero;

    /// <summary>预计算地形代价网格</summary>
    private PathfindingCostGrid? _costGrid;

    /// <summary>敌对 POI 控制区管理器</summary>
    private ZoneOfControlManager? _zocManager;

    /// <summary>上一帧是否处于敌对 ZoC 内（用于进出检测）</summary>
    private bool _wasInHostileZoc = false;

    /// <summary>鼠标拖拽地图状态（中键/右键按下时）</summary>
    private bool _isDraggingCamera = false;
    private Vector2 _dragStartMouseScreen;
    private Vector2 _dragStartCameraPos;

    /// <summary>玩家寻路时镜头跟随玩家。手动平移/缩放/拖拽会取消跟随。</summary>
    private bool _isFollowingPlayer = false;

    private BladeHex.Audio.AudioManager? _audioManager;
    private BladeHex.Audio.EnvironmentAudioComponent? _envAudio;

    // ========================================
    // 交互系统面板 (类型 — 用 Node 引用)
    // ========================================
    protected BladeHex.View.UI.Overworld.InteractionPanel _interactionPanel = null!;
    protected BladeHex.View.UI.Overworld.DialoguePanel _dialoguePanel = null!;
    protected BladeHex.View.UI.Overworld.TradePanel _tradePanel = null!;
    protected BladeHex.View.UI.Overworld.RestPanel _restPanel = null!;
    protected BladeHex.View.UI.Overworld.TownPanel _townPanel = null!;
    protected BladeHex.View.UI.Overworld.ArenaPanel _arenaPanel = null!;
    protected BladeHex.View.UI.Overworld.SmithyPanel _smithyPanel = null!;
    protected BladeHex.View.UI.Overworld.TrainingPanel _trainingPanel = null!;
    protected BladeHex.View.UI.Overworld.TemplePanel _templePanel = null!;
    protected BladeHex.View.UI.Overworld.QuestBoardPanel _questBoardPanel = null!;
    protected BladeHex.View.UI.Overworld.RecruitPanel _recruitPanel = null!;

    // ========================================
    // 任务目标点
    // ========================================
    protected List<Node2D> _questTargetVisuals = new();
    protected string _lastApproachedQuestId = "";

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        // 启动时 randomize 全局 RNG，避免 GD.Randi() 在所有运行间返回相同值
        // 否则 gs.WorldSeed 未设置时会每次得到相同的"随机"种子
        GD.Randomize();

        _audioManager = GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager");
        if (_audioManager != null)
        {
            // 创建环境音频组件（动态切换天气/地形/时间BGM）
            _envAudio = new BladeHex.Audio.EnvironmentAudioComponent { Name = "EnvironmentAudio" };
            AddChild(_envAudio);
            _envAudio.SetScenario((int)BladeHex.Audio.AudioManager.Scenario.Overworld, 0.0f);
        }

        InitEconomy();
        SetupCamera();
        SetupTimeGradient();
        InitHexOverworld();

        SceneCanvasModulate = new CanvasModulate();
        AddChild(SceneCanvasModulate);

        DeterminePlayerRace();

        // Chunk 模式下 InitHexOverworld 已经生成了 POI 和实体
        if (_chunkManager == null)
        {
            // 旧路径：WorldGenerator 生成 POI
            WorldGen = new WorldGenerator();
            GenerateWorldPois();
        }

        InitFogOfWar();
        // Connect fog to renderer for explored tile caching
        HexRenderer?.SetFogOfWar(Fog);
        // Pre-render all revealed tiles (race territory + previously explored)
        // This runs during the loading screen's 6s animation
        PreRenderRevealedTiles();
        InitEntityManager();
        StoreSpecialCharactersIntoDormantPool();
        RenderWorldPois();
        InitDiscoveredPois();
        InitRoadNetwork();
        InitMapLabels();
        InitPathfindingGrid();
        InitZoneOfControl();
        InitPlayerParty();
        InitSpeedComponent();
        InitWeatherSystem();
        InitToastNotification();
        InitUI();
        InitMinimap();
        ApplyGameSettings();
        SetupInteractionSystem();
        SetupDebugConsole();
        SetupTileAligner();

        // 通知加载屏幕：场景初始化完成，可以淡出
        BladeHex.UI.Loading.LoadingScreen.NotifySceneReady();
    }

    private float _biomeCheckTimer = 0f;
    private BladeHex.Audio.EnvironmentAudioComponent.BiomeType _lastBiome = BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Plains;

    // ───── 种族BGM触发状态 ─────
    private string _currentNationId = "";              // 当前所在领土ID
    private float _raceBgmRandomTimer = 0f;            // 大地图随机触发计时
    private readonly System.Collections.Generic.HashSet<string> _visitedForeignRaces = new();  // 已触发过的外族领土

    public override void _Process(double delta)
    {
        if (PlayerParty == null || MainCamera == null || EconomyMgr == null) return;

        EntityMgr.UpdatePlayerPosition(PlayerParty.Position);
        // 同步玩家等级（影响遭遇敌方等级缩放）
        if (PlayerParty.Roster.Leader != null)
            EntityMgr.PlayerLevel = PlayerParty.Roster.Leader.Level;

        // 镜头跟随玩家：寻路点击后启用，玩家手动 WASD/拖拽时取消
        if (_isFollowingPlayer && PlayerParty.IsMoving)
        {
            MainCamera.PositionSmoothingEnabled = true;
            MainCamera.Position = PlayerParty.Position;
        }
        else if (_isFollowingPlayer && !PlayerParty.IsMoving)
        {
            // 寻路结束 → 自动停止跟随
            _isFollowingPlayer = false;
        }
        // 摄像机不再每帧跟随玩家。仅在左键点击寻路时回到玩家一次（HandleMapClick）。
        // 平时玩家可通过 WASD / 鼠标中键拖拽 自由查看地图。

        // 每秒检测一次玩家所在地形，更新BGM生态群系
        _biomeCheckTimer += (float)delta;
        if (_biomeCheckTimer >= 1.0f)
        {
            _biomeCheckTimer = 0f;
            UpdateBiomeAudio();
            UpdateRaceBgm();
        }

        // 大地图随机触发玩家种族BGM (每60秒检查一次，5%概率)
        _raceBgmRandomTimer += (float)delta;
        if (_raceBgmRandomTimer >= 60f)
        {
            _raceBgmRandomTimer = 0f;
            if (GD.Randf() < 0.05f) TryPlayPlayerRaceBgm();
        }

        // Chunk 模式：更新 chunk 加载/卸载
        if (_chunkManager != null)
        {
            UpdateChunkLoading();
            // 推进路径缓存时钟
            PlayerParty.ChunkAStar?.Tick();
        }

        // 更新遭遇实体视觉位置 + 检查触发
        UpdateEncounterVisuals();

        // 更新委托目标接近检测
        UpdateQuestTargetProximity();

        bool shouldTimePass = (IsPlayerMoving() || IsWaiting) && !IsTimePaused;

        if (shouldTimePass)
        {
            float deltaHours = (float)delta * GameTimeScale;
            if (IsWaiting && !IsPlayerMoving()) deltaHours *= 8.0f;

            EconomyMgr.AdvanceTime(deltaHours);
            EconomyMgr.ConsumeFood(deltaHours * 0.1f);
            EntityMgr.TickMovement((float)delta);

            // 天气循环 tick
            if (WeatherMgr != null)
            {
                int season = (int)EconomyMgr.GetSeason();
                UpdateWeatherTerrainContext();
                WeatherMgr.TickWeatherCycle(season, deltaHours);
            }
        }

        UpdateVisualCycle();
        UpdateVisibility();
        UpdateUIInfo();

        // 小地图同步
        if (_minimap != null && MainCamera != null)
            _minimap.UpdatePlayerAndCamera(PlayerParty.Position, MainCamera.Position, MainCamera.Zoom, GetViewport().GetVisibleRect().Size);

        CheckEncounters();
        CheckPoiEnter();
        CheckQuestTargetProximity();
        CheckZocTransition();
        HandleCameraWASD((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        HandleTileAlignerInput(@event);
        if (_tileAlignerActive)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed)
                return;
        }

        if (PlayerParty == null) return;

        // R 键：扎营休息
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.R && !IsTimePaused)
            {
                DoCampRest();
                GetViewport().SetInputAsHandled();
                return;
            }

            // 大地图快捷键
            if (UI is BladeHex.View.UI.Overworld.OverworldUI owUi)
            {
                string? action = keyEvent.Keycode switch
                {
                    Key.I => "army",          // I = Inventory/军队
                    Key.C => "character",      // C = Character/角色
                    Key.K => "skill_tree",     // K = sKill tree/技能盘
                    Key.J => "quests",         // J = Journal/任务
                    Key.T => "camp",           // T = Tent/营地
                    Key.F => "territory",      // F = Fief/领地
                    Key.H => "recenter",       // H = Home/镜头回到玩家
                    Key.Space => "pause_time", // Space = 暂停/恢复时间
                    _ => null,
                };

                if (action == "recenter")
                {
                    RecenterCameraOnPlayer();
                    _isFollowingPlayer = true;
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (action == "pause_time")
                {
                    IsTimePaused = !IsTimePaused;
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (action != null)
                {
                    owUi.HandleHotkey(action);
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
        }

        if (@event is InputEventMouseButton btn && btn.Pressed)
        {
            if (btn.ButtonIndex == MouseButton.Left)
            {
                HandleMapClick(GetGlobalMousePosition());
                if (IsWaiting)
                {
                    IsWaiting = false;
                }
            }
            else if (btn.ButtonIndex == MouseButton.Middle || btn.ButtonIndex == MouseButton.Right)
            {
                // 开始拖拽地图（中键/右键） — 取消跟随
                _isDraggingCamera = true;
                _dragStartMouseScreen = btn.Position;
                _dragStartCameraPos = MainCamera.Position;
                MainCamera.PositionSmoothingEnabled = false;
                _isFollowingPlayer = false;
            }
            else if (btn.ButtonIndex == MouseButton.WheelUp)
            {
                MainCamera.Zoom *= 1.1f;
                ClampOverworldCamera();
            }
            else if (btn.ButtonIndex == MouseButton.WheelDown)
            {
                MainCamera.Zoom *= 0.9f;
                ClampOverworldCamera();
            }
        }

        // 释放中键/右键 → 结束拖拽
        if (@event is InputEventMouseButton btnUp && !btnUp.Pressed
            && (btnUp.ButtonIndex == MouseButton.Middle || btnUp.ButtonIndex == MouseButton.Right))
        {
            _isDraggingCamera = false;
        }

        // 拖拽中 → 跟随鼠标偏移
        if (_isDraggingCamera && @event is InputEventMouseMotion motion)
        {
            // 鼠标在屏幕坐标里移动 1 像素，对应世界坐标 1/zoom 像素
            Vector2 deltaScreen = motion.Position - _dragStartMouseScreen;
            MainCamera.Position = _dragStartCameraPos - deltaScreen / MainCamera.Zoom.X;
            ClampOverworldCamera();
        }
    }

    /// <summary>限制大地图相机位置和缩放在合理范围内</summary>
    private void ClampOverworldCamera()
    {
        if (MainCamera == null) return;

        // 获取地图像素尺寸（优先 Fog 数据，因为 Chunk 模式下 HexGrid 可能未完整计算边界）
        float mapW = 0, mapH = 0;
        if (Fog != null && Fog.MapWidthPx > 0)
        {
            mapW = Fog.MapWidthPx;
            mapH = Fog.MapHeightPx;
        }
        else if (HexGrid != null && HexGrid.MapPixelWidth > 0)
        {
            mapW = HexGrid.MapPixelWidth;
            mapH = HexGrid.MapPixelHeight;
        }

        if (mapW <= 0 || mapH <= 0) return;

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var worldRect = new Rect2(0, 0, mapW, mapH);

        // MinZoom = 让整张地图刚好填满屏幕的 zoom 值
        // 公式：zoom = viewportSize / mapSize（取两轴中较大的那个 zoom 值，保证地图不超出屏幕）
        float fitZoomX = viewportSize.X / mapW;
        float fitZoomY = viewportSize.Y / mapH;
        float MinZoom = Mathf.Max(fitZoomX, fitZoomY);
        // 安全下限：即使地图极大也不允许 zoom 低于 0.05（避免渲染异常）
        MinZoom = Mathf.Max(MinZoom, 0.05f);

        const float MaxZoom = 2.0f;

        MainCamera.Zoom = new Vector2(
            Mathf.Clamp(MainCamera.Zoom.X, MinZoom, MaxZoom),
            Mathf.Clamp(MainCamera.Zoom.Y, MinZoom, MaxZoom));

        // 位置：限制视角不超出地图边界
        MainCamera.Position = BladeHex.View.Camera.CameraBoundsClamp.Clamp2D(
            MainCamera.Position, MainCamera.Zoom, worldRect, viewportSize);
    }

    /// <summary>扎营休息（R 键触发）</summary>
    private void DoCampRest()
    {
        if (PlayerParty?.Roster == null || EconomyMgr == null) return;
        if (PlayerParty.IsMoving) return; // 移动中不能扎营

        float food = EconomyMgr.Food;
        var result = CampSystem.Rest(PlayerParty.Roster, ref food, PlayerParty.Roster.Count);
        EconomyMgr.Food = food;

        if (result.Success)
        {
            // 推进时间
            EconomyMgr.AdvanceTime(result.HoursElapsed);
            GD.Print($"[Camp] {result.Message}");
        }
        else
        {
            GD.Print($"[Camp] 失败: {result.Message}");
        }
    }

    // ========================================
    // 互操作辅助
    // ========================================

    /// <summary>更新天气管理器的地形上下文（雪地/沙漠判定）</summary>
    private void UpdateWeatherTerrainContext()
    {
        if (WeatherMgr == null || PlayerParty == null) return;

        BladeHex.Map.HexOverworldTile? tile = null;
        if (_chunkManager != null)
        {
            var axial = BladeHex.Map.HexOverworldTile.PixelToAxial(PlayerParty.Position.X, PlayerParty.Position.Y);
            tile = _chunkManager.GetTile(axial.X, axial.Y);
        }
        else if (HexGrid != null)
        {
            tile = HexGrid.GetTileAtPixel(PlayerParty.Position.X, PlayerParty.Position.Y);
        }

        if (tile == null) return;

        var t = tile.Terrain;
        WeatherMgr.IsInSnowTerrain = t == BladeHex.Map.HexOverworldTile.TerrainType.Snow
            || t == BladeHex.Map.HexOverworldTile.TerrainType.Ice
            || t == BladeHex.Map.HexOverworldTile.TerrainType.MountainSnow
            || t == BladeHex.Map.HexOverworldTile.TerrainType.Taiga;

        WeatherMgr.IsInDesertTerrain = t == BladeHex.Map.HexOverworldTile.TerrainType.Sand
            || t == BladeHex.Map.HexOverworldTile.TerrainType.Wasteland
            || t == BladeHex.Map.HexOverworldTile.TerrainType.Savanna;
    }

    /// <summary>检查玩家队伍是否在移动</summary>
    private bool IsPlayerMoving()
    {
        return PlayerParty != null && PlayerParty.IsMoving;
    }

    /// <summary>
    /// 持久化世界数据到磁盘（玩家手动保存时由 UI 调用）。
    /// 保存 chunk 数据 + POI + 世界元数据。
    /// </summary>
    public void SaveWorldData()
    {
        if (string.IsNullOrEmpty(_chunkSaveId) || _chunkManager == null) return;

        // 保存 chunk 数据
        int saved = _chunkManager.SaveAllToDisk(_chunkSaveId);

        // 保存 POI
        ChunkPersistence.SavePois(_chunkSaveId, WorldPois);

        // 保存/更新世界元数据（标记为已保存）
        var meta = new Godot.Collections.Dictionary
        {
            ["seed"] = _chunkManager.Generator?.WorldSeed ?? 0,
            ["chunks_w"] = (_chunkManager.Generator?.WorldWidth ?? 256) / ChunkData.ChunkSize,
            ["chunks_h"] = (_chunkManager.Generator?.WorldHeight ?? 192) / ChunkData.ChunkSize,
            ["poi_count"] = WorldPois.Count,
            ["is_saved"] = true,
        };
        ChunkPersistence.SaveWorldMeta(_chunkSaveId, meta);

        GD.Print($"[OverworldScene] 世界数据已保存: {saved} chunks, {WorldPois.Count} POIs → {_chunkSaveId}");
    }

    /// <summary>让玩家队伍移动到目标位置</summary>
    private void MovePlayerTo(Vector2 target)
    {
        PlayerParty?.MoveTo(target);
    }

    /// <summary>让玩家队伍放置在位置</summary>
    private void PlacePlayerAt(float x, float y)
    {
        PlayerParty?.PlaceAt(x, y);
    }

    /// <summary>停止玩家移动</summary>
    private void StopPlayer()
    {
        if (PlayerParty != null)
            PlayerParty.IsMoving = false;
    }

    /// <summary>检测玩家所在地形并更新BGM生态群系</summary>
    private void UpdateBiomeAudio()
    {
        if (_envAudio == null || PlayerParty == null || HexGrid == null) return;

        var tile = HexGrid.GetTileAtPixel(PlayerParty.Position.X, PlayerParty.Position.Y);
        if (tile == null) return;

        // 映射地形类型到音频生态群系
        var biome = tile.Terrain switch
        {
            BladeHex.Map.HexOverworldTile.TerrainType.Forest => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Forest,
            BladeHex.Map.HexOverworldTile.TerrainType.DenseForest => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Forest,
            BladeHex.Map.HexOverworldTile.TerrainType.Jungle => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Forest,
            BladeHex.Map.HexOverworldTile.TerrainType.Taiga => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Forest,
            BladeHex.Map.HexOverworldTile.TerrainType.Hills => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Mountain,
            BladeHex.Map.HexOverworldTile.TerrainType.Mountain => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Mountain,
            BladeHex.Map.HexOverworldTile.TerrainType.MountainSnow => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Mountain,
            BladeHex.Map.HexOverworldTile.TerrainType.Rocky => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Mountain,
            BladeHex.Map.HexOverworldTile.TerrainType.Swamp => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Swamp,
            BladeHex.Map.HexOverworldTile.TerrainType.Bog => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Swamp,
            BladeHex.Map.HexOverworldTile.TerrainType.Sand => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Desert,
            BladeHex.Map.HexOverworldTile.TerrainType.Wasteland => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Desert,
            BladeHex.Map.HexOverworldTile.TerrainType.Savanna => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Desert,
            BladeHex.Map.HexOverworldTile.TerrainType.Snow => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Snowland,
            BladeHex.Map.HexOverworldTile.TerrainType.Ice => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Snowland,
            _ => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Plains,
        };

        if (biome != _lastBiome)
        {
            _envAudio.SetBiome(biome);
            _lastBiome = biome;
        }

        // 同时更新昼夜
        if (EconomyMgr != null)
        {
            float hour = EconomyMgr.CurrentHour;
            var newTime = hour >= 6 && hour < 19
                ? BladeHex.Audio.EnvironmentAudioComponent.TimeOfDay.Day
                : BladeHex.Audio.EnvironmentAudioComponent.TimeOfDay.Night;
            _envAudio.SetTimeOfDay(newTime);
        }
    }

    /// <summary>检测玩家所在领土，首次进入外族领土时触发该种族BGM</summary>
    private void UpdateRaceBgm()
    {
        if (PlayerParty == null || _worldTerritories == null || _worldNations == null) return;
        var audio = GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager");
        if (audio == null) return;

        // 找到玩家所在的tile坐标
        if (HexGrid == null) return;
        var tile = HexGrid.GetTileAtPixel(PlayerParty.Position.X, PlayerParty.Position.Y);
        if (tile == null) return;

        var pos = tile.Coord;
        string newNationId = "";
        foreach (var kvp in _worldTerritories)
        {
            if (kvp.Value.AllTiles.Contains(pos))
            {
                newNationId = kvp.Key;
                break;
            }
        }

        // 领土未变，跳过
        if (newNationId == _currentNationId) return;
        _currentNationId = newNationId;

        if (string.IsNullOrEmpty(newNationId)) return;

        // 找到这个国家的种族
        var nation = _worldNations.Find(n => n.Id == newNationId);
        if (nation == null) return;

        // 玩家自己的种族
        var playerRace = GetPlayerRaceId();
        var nationRaceId = MapNationRaceToBgmId(nation.Race);

        // 如果是外族领土且首次进入，触发该种族BGM
        if (nationRaceId != playerRace && !_visitedForeignRaces.Contains(nationRaceId))
        {
            _visitedForeignRaces.Add(nationRaceId);
            if (HasRaceBgm(nationRaceId))
            {
                audio.PlayRaceBgm(nationRaceId, 2.0f);
                GD.Print($"[OverworldScene] 首次进入 {nation.DisplayName} 领土，播放 {nationRaceId} 种族BGM");
            }
        }
    }

    /// <summary>大地图随机触发玩家自身种族BGM</summary>
    private void TryPlayPlayerRaceBgm()
    {
        var audio = GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager");
        if (audio == null) return;
        var raceId = GetPlayerRaceId();
        if (HasRaceBgm(raceId))
            audio.PlayRaceBgm(raceId, 3.0f);
    }

    private string GetPlayerRaceId()
    {
        if (PlayerParty?.Roster?.Leader?.Race == null) return "human";
        return PlayerParty.Roster.Leader.Race.raceId switch
        {
            BladeHex.Data.RaceData.Race.Human => "human",
            BladeHex.Data.RaceData.Race.Elf => "elf",
            BladeHex.Data.RaceData.Race.Dwarf => "dwarf",
            BladeHex.Data.RaceData.Race.HalfOrc => "halforc",
            BladeHex.Data.RaceData.Race.HalfElf => "halfelf",
            _ => "human",
        };
    }

    private static string MapNationRaceToBgmId(string nationRace) => nationRace switch
    {
        "human" => "human",
        "elf" => "elf",
        "dwarf" => "dwarf",
        "orc" => "halforc",      // orc国家 → 半兽人BGM
        "halfelf" => "halfelf",
        _ => "",                  // goblin/kobold/minotaur/shadow_cult 没有种族BGM
    };

    private static bool HasRaceBgm(string raceId)
    {
        return raceId == "human" || raceId == "elf" || raceId == "dwarf"
            || raceId == "halforc" || raceId == "halfelf";
    }

    /// <summary>创建 节点</summary>
    private static Node2D? CreateGDNode(string className)
    {
        GD.PushWarning($"CreateGDNode: '{className}' no longer exists. Returning null.");
        return null;
    }

    /// <summary>创建 面板 — 已废弃，面板已迁移到 C#</summary>
    private static Node? CreateGDPanel(string scriptPath)
    {
        GD.PushWarning($"CreateGDPanel: '{scriptPath}' no longer exists. Returning null.");
        return null;
    }
}
