// OverworldScene3D.cs
// 3D 大地图场景 — Phase 1 迁移版本
// Node3D 基类，地面/玩家/POI 在同一个 3D 世界中
// 包含：地图生成、玩家移动、经济系统、OverworldUI
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.Scenes.Overworld.Components;

namespace BladeHex.Scenes.Overworld;

/// <summary>
/// 3D 大地图场景 — 替代 OverworldScene (Node2D)
/// 实现 IOverworldContext 供 UI 层访问
/// </summary>
[GlobalClass]
public partial class OverworldScene3D : Node3D, IOverworldContext
{
    // ========================================
    // IOverworldContext 实现
    // ========================================
    public OverworldParty PlayerParty { get; set; } = null!;
    public UnitData PlayerUnitData { get; set; } = null!;
    public int PlayerRaceId { get; set; } = 0;
    public EconomyManager EconomyMgr { get; set; } = null!;
    public OverworldEntityManager EntityMgr { get; set; } = null!;
    public bool IsWaiting { get; set; } = false;

    // ========================================
    // 核心组件
    // ========================================
    private OverworldCamera3D _camera = null!;
    private HexOverworldRenderer3D _renderer = null!;
    private OverworldPropRenderer? _propRenderer;
    private OverworldLightSystem? _lightSystem;
    private HexOverworldGrid _grid = null!;
    private HexOverworldGenerator? _gen;
    private HexOverworldAStar _astar = null!;
    private CanvasLayer? _uiLayer;
    private BladeHex.View.UI.Overworld.OverworldUI? _overworldUi;

    // 玩家 3D 表示
    private MeshInstance3D? _playerMesh;
    private Vector2 _playerPixelPos;
    private bool _playerMoving = false;
    private const float PlayerMoveSpeed = 800.0f;

    // 时间
    public float GameTimeScale = 0.5f;
    public bool IsTimePaused = false;

    // ========================================
    // 生命周期
    // ========================================

    public override async void _Ready()
    {
        GD.Randomize();

        var gs = BladeHex.Data.Globals.StateOrNull;
        int seed = (gs != null && gs.WorldGen.Seed > 0) ? gs.WorldGen.Seed : (int)GD.Randi();

        // 经济系统
        InitEconomy();

        // 世界生成（最重的操作，分帧前先让出一帧让加载屏幕渲染）
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        InitWorldGeneration(seed);

        // 3D 渲染
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _renderer = new HexOverworldRenderer3D();
        AddChild(_renderer);
        _renderer.Initialize();

        // 场景物体渲染器（树木/岩石/山脉）
        _propRenderer = new OverworldPropRenderer();
        AddChild(_propRenderer);
        _propRenderer.Initialize(seed);

        // 光圈系统（昼夜灯火）
        _lightSystem = new OverworldLightSystem();
        AddChild(_lightSystem);
        _lightSystem.Initialize();

        // 相机
        _camera = new OverworldCamera3D();
        _camera.BaseOrthoSize = 8.0f;
        _camera.Distance = 50.0f;
        AddChild(_camera);

        // 光照
        SetupLighting();

        // 玩家
        InitPlayer(gs);

        // 迷雾
        InitFog();

        // 相机边界
        InitCameraBounds();

        // 初始 chunk 加载 + 渲染（较重）
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        InitialChunkLoad();

        // 渲染已揭示领土（较重）
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderAllRevealedTiles();

        // 实体
        InitEntities();

        // 导航系统（NavigationServer3D）
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        InitNavigation();
        BuildInitialNavMesh();

        // POI 渲染
        RenderWorldPOIs();

        // POI 光圈（夜间灯火）
        _lightSystem?.CreatePOILights(WorldPois);
        _lightSystem?.CreatePlayerLight(_playerPixelPos);

        // 交互
        InitInteraction();

        // 道路
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderRoadsAndRivers();

        // 小地图
        InitMinimap();

        // 领土覆盖层
        InitTerritoryOverlay();

        // 音频
        InitAudio();

        // 天气系统
        InitWeatherSystem();

        // UI
        InitUI();
        InitToast();

        // 调试控制台
        SetupDebugConsole();

        // 聚焦相机到玩家
        ForceCameraToPlayer();

        GD.Print($"[OverworldScene3D] 就绪: tiles={_grid.TileCount()}, POI={WorldPois.Count}, seed={seed}");
        _initialized = true;
        BladeHex.UI.Loading.LoadingScreen.NotifySceneReady();

        // 开发期：自动运行地形分析（输出到控制台）
        #if DEBUG
        CallDeferred(nameof(RunTerrainAnalysisDeferred));
        #endif
    }

    private void RunTerrainAnalysisDeferred()
    {
        var gs = BladeHex.Data.Globals.StateOrNull;
        int testSeed = gs?.WorldGen.Seed ?? 12345;
        string result = BladeHex.Tests.TerrainGenerationTest.RunAnalysis(testSeed, 10, 8);
        GD.Print(result);
    }

    private bool _initialized = false;

    public override void _Process(double delta)
    {
        if (!_initialized) return;
        if (_encounterActive) return; // 战斗中暂停大地图所有逻辑
        float dt = (float)delta;

        // 玩家移动
        UpdatePlayerMovement(dt);

        // Chunk 流式加载
        UpdateChunkLoading();

        // 迷雾更新
        UpdateFog();

        // 实体更新 + 遭遇检测
        UpdateEntities(dt);

        // POI 进入检测
        CheckPOIEnter();

        // 时间推进（移动或等待时）
        bool shouldTimePass = (_playerMoving || IsWaiting) && !IsTimePaused;
        if (shouldTimePass && EconomyMgr != null)
        {
            float deltaHours = dt * GameTimeScale;
            if (IsWaiting && !_playerMoving) deltaHours *= 8.0f;

            EconomyMgr.AdvanceTime(deltaHours);
            // 动态口粮行军消耗（每人每天 0.5 单位，按小时平摊）
            EconomyMgr.ConsumeFoodByTravel(deltaHours);
        }

        // 昼夜循环（必须在天气之前，天气会叠加修正）
        UpdateDayNightCycle();

        // 地形 hillshade — 将太阳方向同步到地形 shader
        UpdateTerrainHillshade();

        // 光圈系统昼夜更新
        if (_lightSystem != null && EconomyMgr != null)
        {
            float visionPx = 15.0f * HexOverworldTile.HexSize * 1.732f * WeatherVisionFactor;
            _lightSystem.UpdateDayNight(EconomyMgr.CurrentHour, visionPx);
            _lightSystem.UpdatePlayerLightPosition(_playerPixelPos);
        }

        // 天气系统
        UpdateWeather(dt);

        // 场景物体 LOD
        _propRenderer?.UpdateLOD(_camera.Size);

        // 小地图
        UpdateMinimap();

        // 云层和领土覆盖层同步相机位置
        if (_cloudLayer != null && _camera != null)
        {
            float zoom = _camera.Size > 0 ? 8.0f / _camera.Size : 1.0f;
            // 屏幕中心 → 世界坐标 → 像素坐标（相机焦点的准确位置）
            var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var screenCenter = vpSize * 0.5f;
            var camPixelPos = CoordConverter.ScreenToPixel(_camera, screenCenter) ?? _playerPixelPos;
            _cloudLayer.UpdateCamera(camPixelPos, zoom);
            _territoryOverlay?.UpdateCamera(camPixelPos, zoom);
        }

        // 音频
        UpdateAudio(dt);

        // UI 数据刷新
        UpdateUIInfo();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_encounterActive) return; // 战斗中不处理大地图输入

        // 快捷键（始终可用）
        HandleHotkeys(@event);

        // Space 暂停（始终可用）
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Space)
            {
                IsTimePaused = !IsTimePaused;
                GetViewport().SetInputAsHandled();
                return;
            }

            // WASD 手动平移 → 取消摄像头跟随
            if (key.Keycode == Key.W || key.Keycode == Key.A ||
                key.Keycode == Key.S || key.Keycode == Key.D)
            {
                _cameraFollowing = false;
            }
        }

        // 中键拖拽 → 取消摄像头跟随
        if (@event is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Middle)
        {
            _cameraFollowing = false;
        }

        // 交互面板打开时不处理地图点击
        if (_poiEntered) return;

        // 右键 → 查看实体/POI 信息（暂停时也可用）
        if (@event is InputEventMouseButton rmb && rmb.Pressed && rmb.ButtonIndex == MouseButton.Right)
        {
            var worldPos = CoordConverter.ScreenToWorld3D(_camera, rmb.Position);
            if (worldPos != null)
            {
                var pixelPos = CoordConverter.World3DToPixel(worldPos.Value);
                ShowInfoTooltip(pixelPos);
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        // 暂停时不处理左键寻路（但上面的右键查看和面板快捷键仍可用）
        if (IsTimePaused) return;

        // 左键点击 → 寻路
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var worldPos = CoordConverter.ScreenToWorld3D(_camera, mb.Position);
            if (worldPos != null)
            {
                var pixelTarget = CoordConverter.World3DToPixel(worldPos.Value);
                StartPathfinding(pixelTarget);
            }
        }
    }

    // ========================================
    // 初始化
    // ========================================

    private void InitEconomy()
    {
        EconomyMgr = new EconomyManager();
        EconomyMgr.Name = "EconomyManager";
        GetTree().Root.CallDeferred("add_child", EconomyMgr);

        // 若为读档模式，在 EconomyManager 注入场景树后立即还原经济数据
        var gs = BladeHex.Data.Globals.StateOrNull;
        if (gs != null && gs.Save.IsLoadingSave && !string.IsNullOrEmpty(gs.Save.CurrentSaveId))
        {
            var saveData = new BladeHex.Data.SaveManager().LoadGame(gs.Save.CurrentSaveId);
            if (saveData?.Economy != null)
            {
                BladeHex.Data.SaveManager.RestoreEconomy(EconomyMgr, saveData.Economy);
                GD.Print($"[OverworldScene3D] 读档还原经济数据完成，saveId={gs.Save.CurrentSaveId}");
            }
        }
    }

    private void InitPlayer(GlobalState? gs)
    {
        // 生成角色数据
        PlayerRaceId = 0; // 默认人类
        if (gs != null && gs.WorldGen.IsQuickGame)
        {
            var race = RaceData.GetRaceById((RaceData.Race)PlayerRaceId);
            PlayerUnitData = CharacterGenerator.GenerateCharacter(race, level: 1, seedVal: -1);
        }
        else if (gs != null && gs.OriginContext.Data.ContainsKey("unit_data"))
        {
            // 从出身选择界面获取完整角色数据
            PlayerUnitData = gs.OriginContext.Data["unit_data"].As<UnitData>();
            if (PlayerUnitData == null)
            {
                PlayerUnitData = new UnitData { UnitName = "冒险者", Level = 1 };
                PlayerUnitData.Race = RaceData.GetRaceById(RaceData.Race.Human);
            }
            if (PlayerUnitData.Race != null)
                PlayerRaceId = (int)PlayerUnitData.Race.raceId;
            // 确保HP已初始化
            if (PlayerUnitData.BaseMaxHp <= 0)
                PlayerUnitData.BaseMaxHp = 10 + (PlayerUnitData.Con - 10) / 2 * 2;
        }
        else
        {
            PlayerUnitData = new UnitData();
            PlayerUnitData.UnitName = "冒险者";
            PlayerUnitData.Race = RaceData.GetRaceById(RaceData.Race.Human);
            PlayerUnitData.Str = 14; PlayerUnitData.Dex = 12; PlayerUnitData.Con = 13;
            PlayerUnitData.Intel = 10; PlayerUnitData.Wis = 10; PlayerUnitData.Cha = 10;
            PlayerUnitData.BaseMaxHp = 20; PlayerUnitData.Level = 1;
        }

        // 确保玩家至少有初始装备（布衣+鞋+武器）
        CharacterGenerator.EquipStartingGear(PlayerUnitData);

        // 创建 OverworldParty（逻辑层，Node2D）
        // 加入场景树确保 _Process 和信号正常工作
        PlayerParty = new OverworldParty();
        PlayerParty.SetHexNavigation(_grid, _astar);
        PlayerParty.Visible = false; // 不渲染 2D 视觉（3D 标记代替）
        AddChild(PlayerParty);

        // Chunk 模式：注入 ChunkAStar
        if (_chunkManager != null)
        {
            var chunkAstar = new ChunkAStar();
            PlayerParty.SetChunkNavigation(_chunkManager, chunkAstar);
        }

        // 起始位置
        _playerPixelPos = GetPlayerStartPosition();
        PlayerParty.Position = _playerPixelPos;

        // 队伍名册
        var roster = PlayerParty.Roster;
        roster.SetLeader(PlayerUnitData);

        // 根据出身选择添加初始伙伴
        if (gs != null && !gs.WorldGen.IsQuickGame && gs.OriginContext.Data.ContainsKey("companion"))
        {
            string companionType = gs.OriginContext.Data["companion"].AsString();
            UnitData? companionUnit = _CreateCompanionUnit(companionType, PlayerUnitData);
            if (companionUnit != null)
            {
                PartyRoster.SetCurrentHp(companionUnit, companionUnit.BaseMaxHp);
                roster.Add(companionUnit);
            }
        }
        else
        {
            // 快速游戏加 2 个队友
            for (int i = 0; i < 2; i++)
            {
                var companion = CharacterGenerator.GenerateCharacter(
                    PlayerUnitData.Race, level: 1, seedVal: -1);
                PartyRoster.SetCurrentHp(companion, companion.BaseMaxHp);
                roster.Add(companion);
            }
        }

        // 出身选择的初始物品加入背包
        if (gs != null && !gs.WorldGen.IsQuickGame && gs.OriginContext.Data.ContainsKey("items"))
        {
            var itemsVar = gs.OriginContext.Data["items"];
            string[] itemNames = null;
            string[] itemIds = null;
            string[] itemTypes = null;
            try { itemNames = itemsVar.AsStringArray(); } catch { }
            if (itemNames == null)
            {
                try
                {
                    var arr = itemsVar.AsGodotArray();
                    itemNames = new string[arr.Count];
                    for (int i = 0; i < arr.Count; i++)
                        itemNames[i] = arr[i].AsString();
                }
                catch { }
            }
            // 读取物品 ID 和类型（新字段）
            if (gs.OriginContext.Data.ContainsKey("itemIds"))
            {
                try { itemIds = gs.OriginContext.Data["itemIds"].AsStringArray(); } catch { }
                if (itemIds == null)
                {
                    try
                    {
                        var arr = gs.OriginContext.Data["itemIds"].AsGodotArray();
                        itemIds = new string[arr.Count];
                        for (int i = 0; i < arr.Count; i++)
                            itemIds[i] = arr[i].AsString();
                    }
                    catch { }
                }
            }
            if (gs.OriginContext.Data.ContainsKey("itemTypes"))
            {
                try { itemTypes = gs.OriginContext.Data["itemTypes"].AsStringArray(); } catch { }
                if (itemTypes == null)
                {
                    try
                    {
                        var arr = gs.OriginContext.Data["itemTypes"].AsGodotArray();
                        itemTypes = new string[arr.Count];
                        for (int i = 0; i < arr.Count; i++)
                            itemTypes[i] = arr[i].AsString();
                    }
                    catch { }
                }
            }
            if (itemNames != null)
            {
                for (int i = 0; i < itemNames.Length; i++)
                {
                    string name = itemNames[i];
                    if (string.IsNullOrEmpty(name)) continue;

                    string id = (itemIds != null && i < itemIds.Length) ? itemIds[i] : "";
                    string type = (itemTypes != null && i < itemTypes.Length) ? itemTypes[i] : "material";

                    var lootType = type switch
                    {
                        "weapon" => LootEntry.LootType.Weapon,
                        "armor" => LootEntry.LootType.Armor,
                        "consumable" => LootEntry.LootType.Consumable,
                        "accessory" => LootEntry.LootType.Material, // 饰品暂用 Material 类型
                        _ => LootEntry.LootType.Material,
                    };

                    var entry = new LootEntry(name, lootType, 1, 5, "出身物品");
                    entry.ItemDataId = id; // 关联实际物品数据库 ID
                    PlayerParty.Inventory.Add(entry);
                }
                GD.Print($"[OverworldScene3D] 出身物品: {itemNames.Length} 件加入背包");
            }
        }

        // 移动速度组件 — 汇总地形/季节/昼夜/负重/坐骑/技能/天气/ZoC 修正
        var speedComp = new MovementSpeedComponent();
        speedComp.BaseSpeed = PlayerParty.BaseMoveSpeed;
        speedComp.ChunkManagerRef = _chunkManager;
        speedComp.HexGridRef = _grid;
        speedComp.EconomyManagerRef = EconomyMgr;
        speedComp.UnitDataRef = PlayerUnitData;
        PlayerParty.SpeedComponent = speedComp;

        // 3D 玩家标记
        var worldPos = CoordConverter.PixelToWorld3D(_playerPixelPos);
        float groundY = GetGroundElevationAt(_playerPixelPos);
        _playerMesh = new MeshInstance3D();
        _playerMesh.Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f };
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.9f, 0.15f, 0.15f);
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _playerMesh.MaterialOverride = mat;
        _playerMesh.Position = worldPos + new Vector3(0, groundY + 0.4f, 0);
        _playerMesh.Name = "PlayerMarker";
        AddChild(_playerMesh);

        GD.Print($"[OverworldScene3D] 玩家: {PlayerUnitData.UnitName}, 队伍: {roster}");

        // 注入 ActiveRoster 到 EconomyManager，使动态军饷与口粮结算能感知队伍状态
        EconomyMgr.ActiveRoster = roster;
        GD.Print($"[OverworldScene3D] EconomyManager.ActiveRoster 已注入，队伍人数: {roster.Count}");
    }

    /// <summary>根据出身选择创建初始伙伴单位（通过模板系统）</summary>
    private static UnitData? _CreateCompanionUnit(string companionType, UnitData player)
    {
        switch (companionType)
        {
            case "忠犬相随":
            {
                var tpl = BladeHex.Data.UnitTemplateDB.CompanionLoyalHound();
                tpl["level"] = player.Level; // 等级与玩家一致
                var unit = BladeHex.Data.UnitTemplateDB.InstantiateTemplate(tpl);
                if (unit != null) unit.IsEnemy = false;
                return unit;
            }
            case "幼熊追随":
            {
                var tpl = BladeHex.Data.UnitTemplateDB.CompanionYoungBear();
                tpl["level"] = player.Level; // 等级与玩家一致
                var unit = BladeHex.Data.UnitTemplateDB.InstantiateTemplate(tpl);
                if (unit != null) unit.IsEnemy = false;
                return unit;
            }
            case "神秘少女":
            {
                // 同种族女性角色，等级与玩家一致
                var unit = CharacterGenerator.GenerateCharacter(player.Race, level: player.Level, seedVal: -1);
                unit.UnitName = "神秘少女";
                return unit;
            }
            case "史莱姆伙伴":
            {
                var tpl = BladeHex.Data.UnitTemplateDB.CompanionSlime();
                // 史莱姆固定15级，不跟随玩家
                var unit = BladeHex.Data.UnitTemplateDB.InstantiateTemplate(tpl);
                if (unit != null) unit.IsEnemy = false;
                return unit;
            }
            default:
                return null;
        }
    }

    private void InitUI()
    {
        _overworldUi = new BladeHex.View.UI.Overworld.OverworldUI();
        _overworldUi.EconomyManager = EconomyMgr;
        _overworldUi.PanelDismissed += () => CleanupInteraction();
        AddChild(_overworldUi); // OverworldUI 继承 CanvasLayer，直接加

        GD.Print("[OverworldScene3D] OverworldUI 已加载");
    }

    private void SetupLighting()
    {
        _sunLight = new DirectionalLight3D();
        _sunLight.RotationDegrees = new Vector3(-50, -30, 0);
        _sunLight.LightEnergy = 0.9f;
        _sunLight.ShadowEnabled = false;
        AddChild(_sunLight);

        var env = new WorldEnvironment();
        var envRes = new Godot.Environment();
        envRes.BackgroundMode = Godot.Environment.BGMode.Color;
        envRes.BackgroundColor = new Color(0.10f, 0.13f, 0.18f);
        envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        envRes.AmbientLightColor = new Color(0.65f, 0.65f, 0.70f);
        envRes.AmbientLightEnergy = 0.5f;
        env.Environment = envRes;
        _worldEnv = envRes;
        AddChild(env);

        SetupDayNightCycle();
    }

    /// <summary>
    /// 每帧更新地形 shader 的太阳方向 uniform，驱动 hillshade 明暗随时间变化。
    /// 从 DayNightController 的太阳 rotation 推导方向向量。
    /// </summary>
    private void UpdateTerrainHillshade()
    {
        if (_sunLight == null || _renderer == null) return;

        // 从 DirectionalLight3D 的旋转推导太阳方向（光线从太阳射向地面的反方向）
        // DirectionalLight3D 的 -Z 轴是光线方向，取反得到"从地面指向太阳"
        var sunForward = -_sunLight.GlobalTransform.Basis.Z;
        _renderer.UpdateSunDirection(sunForward);
    }

    // POI 渲染已移至 OverworldScene3D.POI.cs
    // 玩家移动已移至 OverworldScene3D.Navigation.cs

    // ========================================
    // 相机
    // ========================================

    private void ForceCameraToPlayer()
    {
        var playerWorld = CoordConverter.PixelToWorld3D(_playerPixelPos);
        _camera.FocusOn(playerWorld);
        float rad = Mathf.DegToRad(_camera.PitchAngle);
        _camera.Position = new Vector3(
            playerWorld.X,
            _camera.Distance * Mathf.Sin(rad),
            playerWorld.Z + _camera.Distance * Mathf.Cos(rad)
        );
        _camera.RotationDegrees = new Vector3(-_camera.PitchAngle, 0, 0);
    }
}
