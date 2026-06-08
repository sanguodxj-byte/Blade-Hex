// OverworldScene2D.cs
// 2D 大地图场景 — 从 OverworldScene3D 迁移
// Node2D 基类，地面、玩家/POI 在同一个 2D 世界中
// 包含：地图生成、玩家移动、经济系统、OverworldUI
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.Scenes.Overworld;
using BladeHex.Scenes.Overworld.Components;

namespace BladeHex.Scenes.Overworld2d;

/// <summary>
/// 2D 大地图场景 — 替代 OverworldScene3D (Node3D)
/// 实现 IOverworldContext，供 UI 层访问
/// </summary>
[GlobalClass]
public partial class OverworldScene2D : Node2D, IOverworldContext
{
	// ========================================
	// IOverworldContext 实现
	// ========================================
	public OverworldParty PlayerParty { get; set; } = null!;
	public UnitData PlayerUnitData { get; set; } = null!;
	public int PlayerRaceId { get; set; } = 0;
	public ReputationTracker ReputationTracker => _reputationTracker!;
	public bool IsWaiting { get; set; } = false;

	/// <summary>当前游戏天数（窄化查询，替代暴露完整 EconomyManager）</summary>
	public int CurrentDay => EconomyMgr?.DaysPassed ?? 1;

	/// <summary>增加玩家金币（窄化命令，替代暴露完整 EconomyManager）</summary>>
	public void AddGold(int amount) => EconomyMgr?.AddGold(amount);

	// ========================================
	// 内部管理器，不通过 IOverworldContext 暴露
	// ========================================
	public EconomyManager EconomyMgr { get; set; } = null!;
	public OverworldEntityManager EntityMgr { get; set; } = null!;

	// ========================================
	// 核心组件
	// ========================================
	private ReputationTracker? _reputationTracker;
	private OverworldCamera2D _camera = null!;
	private HexOverworldRenderer2D _renderer = null!;
	private OverworldPropRenderer2D? _propRenderer;
	private OverworldDecalRenderer2D? _decalRenderer;
	private MapAshController? _ashController;
	private OverworldMapAccess _mapAccess = null!;
	private HexOverworldGrid _grid = null!;
	private HexOverworldGenerator? _gen;
	private HexOverworldAStar _astar = null!;
	private EncounterSpawner _encounterSpawner = new();
	private CanvasLayer? _uiLayer;
	private BladeHex.View.UI.Overworld.OverworldUI? _overworldUi;

	// Player position in overworld pixels.
	private Vector2 _playerPixelPos;
	private bool _playerMoving = false;
	private const float PlayerMoveSpeed = 800.0f;

	// Current interaction target and hover tooltip state.
	private OverworldPOI? _targetPoi;
	private BladeHex.View.UI.Overworld.POITooltip? _poiTooltip;
	private OverworldPOI? _lastHoveredPoi;
	private BladeHex.View.UI.Overworld.EntityTooltip? _entityTooltip;
	private OverworldEntity? _lastHoveredEntity;
	private string _lastHoveredEntityStateText = "";
	private string _lastHoveredEntityIntentText = "";

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

		// 世界生成（最重的操作，分帧前先让出一帧，让加载屏幕渲染）
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		InitWorldGeneration(seed);

		// 2D 渲染
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		_renderer = new HexOverworldRenderer2D();
		AddChild(_renderer);
		_renderer.Initialize();

		_ashController = new MapAshController { Name = "MapAshController" };
		AddChild(_ashController);
		_ashController.Initialize(_renderer);

		// 场景物体渲染器（树木/岩石/山脉）
		_propRenderer = new OverworldPropRenderer2D();
		AddChild(_propRenderer);
		// 实机用 ChunkManager 初始化（内含全局地形数据），避免空网格导致山脉 BFS 失效
		if (_chunkManager != null)
			_propRenderer.InitializeFromChunks(seed, _chunkManager);
		else
			_propRenderer.Initialize(seed, _grid);

		// 墨色地形覆盖层
		_decalRenderer = new OverworldDecalRenderer2D();
		AddChild(_decalRenderer);
		_decalRenderer.Initialize(seed);

		// 相机
		_camera = new OverworldCamera2D();
		_camera.BaseZoom = 1.0f;
		AddChild(_camera);

		// 导航系统（NavigationServer2D）
		InitNavigation();

		// 玩家
		InitPlayer(gs);

		// 迷雾
		InitFog();

		// 相机边界
		InitCameraBounds();

		// 初始加载：地面/shader 全图缓存，装饰层按当前可加载范围流式加载。
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		InitialChunkLoad();

		// 实体
		InitEntities();

		// POI 渲染
		RenderWorldPOIs();

		// 交互
		InitInteraction();

		// 道路
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		RenderRoadsAndRivers();

		// 小地图
		InitMinimap();

		// 领地覆盖层
		InitTerritoryOverlay();

		// 音频
		InitAudio();

		// 天气系统
		InitWeatherSystem();

		// 昼夜系统
		SetupDayNightCycle();

		// UI
		InitUI();
		InitToast();
		InitRegionNameOverlay();

		// 调试控制台
		SetupDebugConsole();

		// Focus the camera on the player.
		ForceCameraToPlayer();

		GD.Print($"[OverworldScene2D] 就绪: tiles={_grid.TileCount()}, POI={WorldPois.Count}, seed={seed}");
		_initialized = true;
		BladeHex.UI.Loading.LoadingScreen.NotifySceneReady();

		// 新存档立即弹出教程确认
		if (gs != null && !gs.Save.IsLoadingSave)
		{
			CallDeferred(nameof(ShowTutorialPromptDeferred));
		}

		// Run terrain analysis in debug builds.
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

	private void ShowTutorialPromptDeferred()
	{
		var tutMgr = BladeHex.UI.Tutorial.TutorialManager.Instance;
		if (tutMgr == null)
		{
			GD.PushWarning("[OverworldScene2D] TutorialManager 未注册为 Autoload");
			return;
		}

		// 如果教程已被禁用且 welcome 章节已完成，则跳过提示
		if (!tutMgr.IsEnabled && tutMgr.IsCompleted("welcome")) return;

		// Show the tutorial prompt on first new game.
		if (!tutMgr.IsCompleted("welcome"))
		{
			tutMgr.ResetProgress();
			tutMgr.ShowNewGamePrompt();
		}
	}

	private bool _initialized = false;

	public override void _Process(double delta)
	{
		if (!_initialized) return;
		if (_encounterActive) return; // 战斗中暂停大地图所有逻辑
		float dt = (float)delta;

		// NavMesh 分批生成
		UpdateNavMeshGeneration();

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
			// 推进交战计时（分层渐进式战斗更新）
			EntityMgr?.TickGameHour(deltaHours);
		}

		UpdateDayNightCycle();
		UpdateNightLighting();

		// 天气系统
		UpdateWeather(dt);

		// 场景物体 LOD
		_propRenderer?.UpdateLOD(_camera.Zoom.X);
		_decalRenderer?.UpdateLOD(_camera.Zoom.X);

		// 小地图
		UpdateMinimap();

		// 区域名称显示（Wartales 风格）
		UpdateRegionNameOverlay();

		// 音频
		UpdateAudio(dt);

		// UI 数据刷新
		UpdateUIInfo();

		// 更新鼠标悬浮的移动实体和 POI 信息显示
		UpdateEntityTooltip();
		UpdatePOITooltip();
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

			// WASD 手动平移 -> 取消摄像头跟随（仅在相机未被阻断时）
			if (!(_camera?.ExternalControl ?? false) &&
				(key.Keycode == Key.W || key.Keycode == Key.A ||
				 key.Keycode == Key.S || key.Keycode == Key.D))
			{
				_cameraFollowing = false;
			}
		}

		// 中键拖拽 -> 取消摄像头跟随（仅在相机未被阻断时）
		if (!(_camera?.ExternalControl ?? false) &&
			@event is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Middle)
		{
			_cameraFollowing = false;
		}

		// Ignore map clicks while an interaction panel owns input.
		if (_poiEntered || (_camera?.ExternalControl ?? false)) return;

		// 右键 -> 查看实体/POI 信息（暂停时也可用）
		if (@event is InputEventMouseButton rmb && rmb.Pressed && rmb.ButtonIndex == MouseButton.Right)
		{
			var pixelPos = GetGlobalMousePosition();
			ShowInfoTooltip(pixelPos);
			GetViewport().SetInputAsHandled();
			return;
		}

		// 左键点击 -> 寻路
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			// Left click movement resumes paused overworld time.
			if (IsTimePaused)
			{
				IsTimePaused = false;
			}

			// Left click movement cancels waiting.
			if (IsWaiting)
			{
				IsWaiting = false;
			}

			var pixelTarget = GetGlobalMousePosition();

			// 检测是否点中了具名领主 / 英雄实体
			if (EntityMgr != null)
			{
				foreach (var entity in EntityMgr.Entities)
				{
					if (!entity.IsAlive) continue;
					if (entity.IsNamedCharacter && !string.IsNullOrEmpty(entity.HeroId) && pixelTarget.DistanceTo(entity.Position) < 180.0f)
					{
						var hero = EntityMgr.Heroes.Get(entity.HeroId);
						if (hero != null)
						{
							BladeHex.View.UI.Overworld.HeroDetailPanel.ShowDetail(hero, EntityMgr, this);
							GetViewport().SetInputAsHandled();
							return;
						}
					}
				}
			}

			// 检测点击的是否是 POI 占用格，设定目标 POI
			var clickedAxial = HexOverworldTile.PixelToAxial(pixelTarget.X, pixelTarget.Y);
			HexOverworldTile? clickedTile = _mapAccess.GetActiveTile(clickedAxial.X, clickedAxial.Y);

			OverworldPOI? clickedPoi = null;
			if (clickedTile != null && !string.IsNullOrEmpty(clickedTile.PoiId))
			{
				foreach (var poi in WorldPois)
				{
					if (poi.PoiName == clickedTile.PoiId)
					{
						clickedPoi = poi;
						break;
					}
				}
			}

			if (clickedPoi == null)
			{
				clickedPoi = FindPOIAtPosition(pixelTarget);
			}

			if (clickedPoi != null)
			{
				_targetPoi = clickedPoi;
				GD.Print($"[Input] 点击并设定目标 POI: {clickedPoi.PoiName}");
			}
			else
			{
				_targetPoi = null;
				GD.Print("[Input] 点击空地，清空目标 POI");
			}

			StartPathfinding(pixelTarget);
		}
	}

	// ========================================
	// 初始化：委托给 partial classes
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
				GD.Print($"[OverworldScene2D] 读档还原经济数据完成，saveId={gs.Save.CurrentSaveId}");
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
				PlayerUnitData = new UnitData { UnitName = "Adventurer", Level = 1 };
				PlayerUnitData.Race = RaceData.GetRaceById(RaceData.Race.Human);
			}
			if (PlayerUnitData.Race != null)
				PlayerRaceId = (int)PlayerUnitData.Race.raceId;
			// 确保 HP 已初始化
			if (PlayerUnitData.BaseMaxHp <= 0)
				PlayerUnitData.BaseMaxHp = 10 + (PlayerUnitData.Con - 10) / 2 * 2;
		}
		else
		{
			PlayerUnitData = new UnitData();
			PlayerUnitData.UnitName = "Adventurer";
			PlayerUnitData.Race = RaceData.GetRaceById(RaceData.Race.Human);
			PlayerUnitData.Str = 14; PlayerUnitData.Dex = 12; PlayerUnitData.Con = 13;
			PlayerUnitData.Intel = 10; PlayerUnitData.Wis = 10; PlayerUnitData.Cha = 10;
			PlayerUnitData.BaseMaxHp = 20; PlayerUnitData.Level = 1;
		}

		// 确保玩家至少有初始装备（布衣 + 鞋 + 武器）
		CharacterGenerator.EquipStartingGear(PlayerUnitData);

		// 创建 OverworldParty（逻辑层，Node2D）
		PlayerParty = new OverworldParty();
		PlayerParty.SetHexNavigation(_grid, _astar);
		PlayerParty.SetMapAccess(_mapAccess);
		AddChild(PlayerParty);

		// Chunk 模式：注入 ChunkAStar
		ChunkAStar? chunkAstar = null;
		if (_chunkManager != null)
		{
			chunkAstar = new ChunkAStar();
			PlayerParty.SetChunkNavigation(_chunkManager, chunkAstar);
		}
		PlayerParty.SetNavigationAccess(new OverworldNavigationAccess(_mapAccess, _chunkManager, chunkAstar, _astar));

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
			// Quick game starts with two companions.
			for (int i = 0; i < 2; i++)
			{
				var companion = CharacterGenerator.GenerateCharacter(
					PlayerUnitData.Race, level: 1, seedVal: -1);
				PartyRoster.SetCurrentHp(companion, companion.BaseMaxHp);
				roster.Add(companion);
			}
		}

		// Add origin-selected starting items to the inventory.
		if (gs != null && !gs.WorldGen.IsQuickGame && gs.OriginContext.Data.ContainsKey("items"))
		{
			var itemsVar = gs.OriginContext.Data["items"];
			string[]? itemNames = null;
			string[]? itemIds = null;
			string[]? itemTypes = null;
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
						"accessory" => LootEntry.LootType.Material,
						_ => LootEntry.LootType.Material,
					};

					var entry = new LootEntry(name, lootType, 1, 5, "出身物品");
					entry.ItemDataId = id;
					PlayerParty.Inventory.Add(entry);
				}
				GD.Print($"[OverworldScene2D] Added origin items: {itemNames.Length}");
			}
		}

		// 移动速度组件
		var speedComp = new MovementSpeedComponent();
		speedComp.BaseSpeed = PlayerParty.BaseMoveSpeed;
		speedComp.MapAccess = _mapAccess;
		speedComp.ChunkManagerRef = _chunkManager;
		speedComp.HexGridRef = _grid;
		speedComp.EconomyManagerRef = EconomyMgr;
		speedComp.UnitDataRef = PlayerUnitData;
		PlayerParty.SpeedComponent = speedComp;

		GD.Print($"[OverworldScene2D] 玩家: {PlayerUnitData.UnitName}, 队伍: {roster}");

		// 注入 ActiveRoster 到 EconomyManager
		EconomyMgr.ActiveRoster = roster;
		GD.Print($"[OverworldScene2D] EconomyManager.ActiveRoster 已注入，队伍人数: {roster.Count}");
	}

	/// <summary>根据出身选择创建初始伙伴单位（通过模板系统）</summary>
	private static UnitData? _CreateCompanionUnit(string companionType, UnitData player)
	{
		switch (companionType)
		{
			case "忠犬相随":
			{
				var tpl = BladeHex.Data.UnitTemplateDB.CompanionLoyalHound();
				tpl["level"] = player.Level;
				var unit = BladeHex.Data.UnitTemplateDB.InstantiateTemplate(tpl);
				if (unit != null) unit.IsEnemy = false;
				return unit;
			}
			case "幼熊追随":
			{
				var tpl = BladeHex.Data.UnitTemplateDB.CompanionYoungBear();
				tpl["level"] = player.Level;
				var unit = BladeHex.Data.UnitTemplateDB.InstantiateTemplate(tpl);
				if (unit != null) unit.IsEnemy = false;
				return unit;
			}
			case "神秘少女":
			{
				var unit = CharacterGenerator.GenerateCharacter(player.Race, level: player.Level, seedVal: -1);
				unit.UnitName = "神秘少女";
				return unit;
			}
			case "史莱姆伙伴":
			{
				var tpl = BladeHex.Data.UnitTemplateDB.CompanionSlime();
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
		_overworldUi.EntityMgr = EntityMgr;
		_overworldUi.PanelDismissed += OnPanelDismissed;
		AddChild(_overworldUi);

		// 实例化 POITooltip 并加到 _overworldUi 中
		_poiTooltip = new BladeHex.View.UI.Overworld.POITooltip { Name = "POITooltip" };
		_overworldUi.AddChild(_poiTooltip);

		// 实例化 EntityTooltip 并加到 _overworldUi 中
		_entityTooltip = new BladeHex.View.UI.Overworld.EntityTooltip { Name = "EntityTooltip" };
		_overworldUi.AddChild(_entityTooltip);

		if (EntityMgr?.WorldEngine != null)
		{
			EntityMgr.WorldEngine.NewsAdded += OnNewsAdded;
		}

		GD.Print("[OverworldScene2D] OverworldUI loaded");
	}

	private void OnNewsAdded(BladeHex.Strategic.WorldEvents.NewsEntry news)
	{
		if (news.Type == "poi_captured" || news.Type == "war_declared" || news.Type == "peace")
		{
			_toast?.Show($"[World] {news.Description}", new Color(1.0f, 0.85f, 0.4f));
		}
	}

	private void ForceCameraToPlayer()
	{
		_camera?.FocusOnImmediate(_playerPixelPos);
	}

	// ========================================
	// 生态区名称显示（Wartales 风格）
	// ========================================

	/// <summary>初始化区域名称覆盖层</summary>
	private void InitRegionNameOverlay()
	{
		if (_namedBiomeZones == null || _namedBiomeZones.Count == 0)
		{
			GD.Print("[OverworldScene2D] Skipped region name overlay: no named zones");
			return;
		}

		_regionNameOverlay = new BladeHex.View.UI.Overworld.RegionNameOverlay();
		_regionNameOverlay.Name = "RegionNameOverlay";
		AddChild(_regionNameOverlay);

		GD.Print($"[OverworldScene2D] Region name overlay initialized ({_namedBiomeZones.Count} zones)");
	}

	/// <summary>每帧更新区域名称显示</summary>
	private void UpdateRegionNameOverlay()
	{
		if (_regionNameOverlay == null || _biomeZoneNamer == null) return;

		// 查找玩家当前所在的生态区（使用 O(1) 快速查找）
		var playerAxial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
		var currentZone = _biomeZoneNamer.FindZoneAtCoordFast(playerAxial);

		if (currentZone != null)
		{
			// 根据语言设置选择显示名称
			bool isZH = BladeHex.Data.NameGenerator.GetCurrentLanguage() == "zh";
			string displayName = isZH ? currentZone.NameCN : currentZone.Name;
			_regionNameOverlay.ShowRegion(displayName, currentZone.SizeClass);
		}
		else
		{
			_regionNameOverlay.HideRegion();
		}
	}

	/// <summary>每帧更新鼠标悬浮 POI 详情显示</summary>
	private void UpdatePOITooltip()
	{
		if (_poiTooltip == null) return;

		// 如果已经进入 POI 交互、战斗中，或者相机被阻断，则隐藏 Tooltip
		if (_poiEntered || _encounterActive || (_camera?.ExternalControl ?? false))
		{
			if (_poiTooltip.IsShowing || _lastHoveredPoi != null)
			{
				_poiTooltip.HidePanel();
				_lastHoveredPoi = null;
			}
			return;
		}

		// Entity tooltip takes priority over POI tooltip.
		if (_entityTooltip != null && _entityTooltip.IsShowing)
		{
			if (_poiTooltip.IsShowing || _lastHoveredPoi != null)
			{
				_poiTooltip.HidePanel();
				_lastHoveredPoi = null;
			}
			return;
		}

		var mouseGlobal = GetGlobalMousePosition();
		var mouseAxial = HexOverworldTile.PixelToAxial(mouseGlobal.X, mouseGlobal.Y);

		HexOverworldTile? tile = _mapAccess.GetActiveTile(mouseAxial.X, mouseAxial.Y);

		OverworldPOI? hoverPoi = null;

		if (tile != null && !string.IsNullOrEmpty(tile.PoiId))
		{
			foreach (var poi in WorldPois)
			{
				if (poi.PoiName == tile.PoiId)
				{
					hoverPoi = poi;
					break;
				}
			}
		}

		// 距离检测回退
		if (hoverPoi == null)
		{
			hoverPoi = FindPOIAtPosition(mouseGlobal);
		}

		if (hoverPoi != null)
		{
			var screenPos = GetViewport().GetMousePosition();
			// 脏检查优化：只有当前悬浮的 POI 改变时才重绘 UI 文本
			if (hoverPoi != _lastHoveredPoi)
			{
				_poiTooltip.ShowForPOI(hoverPoi, screenPos, _worldNations);
				_lastHoveredPoi = hoverPoi;
			}
			else
			{
				// 如果是同一个 POI，仅更新悬浮框的屏幕位置，避免重绘 UI 文本
				_poiTooltip.ShowAt(screenPos);
			}
		}
		else
		{
			if (_poiTooltip.IsShowing || _lastHoveredPoi != null)
			{
				_poiTooltip.HidePanel();
				_lastHoveredPoi = null;
			}
		}
	}

	/// <summary>每帧更新鼠标悬浮移动实体详情显示</summary>
	private void UpdateEntityTooltip()
	{
		if (_entityTooltip == null) return;

		// 如果已经进入 POI 交互、战斗中，或者相机被阻断，则隐藏 Tooltip
		if (_poiEntered || _encounterActive || (_camera?.ExternalControl ?? false))
		{
			if (_entityTooltip.IsShowing || _lastHoveredEntity != null)
			{
				_entityTooltip.HidePanel();
				_lastHoveredEntity = null;
			}
			return;
		}

		var mouseGlobal = GetGlobalMousePosition();
		OverworldEntity? hoverEntity = FindEntityAtPosition(mouseGlobal);

		if (hoverEntity != null)
		{
			// Hide the POI tooltip when an entity tooltip is active.
			if (_poiTooltip != null && _poiTooltip.IsShowing)
			{
				_poiTooltip.HidePanel();
				_lastHoveredPoi = null;
			}

			var screenPos = GetViewport().GetMousePosition();
			// 状态/意图变化时也刷新文本，否则追逃目标变化会只移动 tooltip 不更新内容
			string stateText = hoverEntity.GetStateText();
			string intentText = hoverEntity.LastIntentSummary;
			if (hoverEntity != _lastHoveredEntity || stateText != _lastHoveredEntityStateText || intentText != _lastHoveredEntityIntentText)
			{
				// 使用 EntitySpeedCalculator 计算有效移速分解
				var breakdown = EntitySpeedCalculator.GetBreakdown(
					hoverEntity, hoverEntity.Position,
					EntityMgr.SimCtx.TerrainQuery,
					EntityMgr.SimCtx.ZocManager);
				_entityTooltip.ShowForEntity(hoverEntity, screenPos, _worldNations, breakdown);
				_lastHoveredEntity = hoverEntity;
				_lastHoveredEntityStateText = stateText;
				_lastHoveredEntityIntentText = intentText;
			}
			else
			{
				// 如果是同一个实体，仅更新悬浮框的屏幕位置，避免重绘 UI 文本
				_entityTooltip.ShowAt(screenPos);
			}
		}
		else
		{
			if (_entityTooltip.IsShowing || _lastHoveredEntity != null)
			{
				_entityTooltip.HidePanel();
				_lastHoveredEntity = null;
				_lastHoveredEntityStateText = "";
				_lastHoveredEntityIntentText = "";
			}
		}
	}
}
