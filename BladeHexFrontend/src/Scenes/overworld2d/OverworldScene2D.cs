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
using BladeHex.Diagnostics;
using BladeHex.View.Strategic;

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
	private BladeHex.View.UI.Overworld.BattlefieldTooltip? _battlefieldTooltip;
	private OverworldEntity? _lastHoveredEntity;
	private string _lastHoveredEntityStateText = "";
	private string _lastHoveredEntityIntentText = "";
	private string _lastHoveredBattlefieldKey = "";

	// 时间
	public float GameTimeScale = 0.5f;
	public bool IsTimePaused = false;

	/// <summary>统一大地图时钟 — 集中计算时间推进逻辑（替代散落的 shouldTimePass）</summary>
	private readonly CampaignClock _campaignClock = new();

	// ========================================
	// 架构优化新增模块（管线分层）
	// ========================================

	/// <summary>View Projection — 从 Core 层投影只读视图数据</summary>
	private OverworldViewProjection? _viewProjection;

	/// <summary>输入命令路由 — 把左键点击拆成语义命令</summary>
	private readonly OverworldCommandRouter _commandRouter = new();

	/// <summary>交互冷却集中管理 — 替代散落的 _playerEntityInteractionCooldownUntilSec</summary>
	private readonly InteractionCooldown _interactionCooldown = new();

	/// <summary>战场注册表只读快照 — 供 View 层查询当前活跃战场</summary>
	private readonly BattlefieldRegistry _battlefieldRegistry = new();

	/// <summary>战场视觉层 — 战场 marker、hover、click 加入入口</summary>
	private OverworldBattlefieldLayer2D? _battlefieldLayer;

	/// <summary>围城视觉层 — 围城 marker、hover、click 加入入口</summary>
	private OverworldSiegeLayer2D? _siegeLayer;

	// ========================================
	// 生命周期
	// ========================================

	public override async void _Ready()
	{
		DiagnosticLog.Event("OverworldScene2D", "ready_start");
		GD.Randomize();

		var gs = BladeHex.Data.Globals.StateOrNull;
		int seed = (gs != null && gs.WorldGen.Seed > 0) ? gs.WorldGen.Seed : (int)GD.Randi();
		var readyReport = new DiagnosticPipelineReport(
			"overworld_scene_ready",
			$"overworld_ready_{System.DateTime.Now:yyyyMMdd_HHmmss}_{System.Environment.ProcessId}_{seed}",
			new Dictionary<string, object?>
			{
				["seed"] = seed,
				["loading_save"] = gs?.Save.IsLoadingSave ?? false,
				["quick_game"] = gs?.WorldGen.IsQuickGame ?? false,
				["world_size"] = gs?.WorldGen.Size ?? -1,
			});
		DiagnosticLog.Event("OverworldScene2D", "seed_selected", new Dictionary<string, object?>
		{
			["seed"] = seed,
			["loading_save"] = gs?.Save.IsLoadingSave ?? false,
			["quick_game"] = gs?.WorldGen.IsQuickGame ?? false,
			["world_size"] = gs?.WorldGen.Size ?? -1,
		});

		// 经济系统
		DiagnosticLog.Event("OverworldScene2D", "init_economy_start");
		if (!RunReadyStep(readyReport, "init_economy", () => InitEconomy()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "init_economy_end");

		// 世界生成（最重的操作，分帧前先让出一帧，让加载屏幕渲染）
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		DiagnosticLog.Event("OverworldScene2D", "init_world_start", new Dictionary<string, object?> { ["seed"] = seed });
		if (!RunReadyStep(readyReport, "init_world_generation", () => InitWorldGeneration(seed)))
			return;
		DiagnosticLog.Event("OverworldScene2D", "init_world_end", new Dictionary<string, object?>
		{
			["tiles"] = _grid?.TileCount() ?? -1,
			["pois"] = WorldPois?.Count ?? -1,
		});

		// 2D 渲染
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		DiagnosticLog.Event("OverworldScene2D", "renderer_start");
		_renderer = new HexOverworldRenderer2D();
		AddChild(_renderer);
		if (!RunReadyStep(readyReport, "renderer_initialize", () => _renderer.Initialize()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "renderer_end");

		DiagnosticLog.Event("OverworldScene2D", "ash_start");
		_ashController = new MapAshController { Name = "MapAshController" };
		AddChild(_ashController);
		if (!RunReadyStep(readyReport, "ash_initialize", () => _ashController.Initialize(_renderer)))
			return;
		DiagnosticLog.Event("OverworldScene2D", "ash_end");

		// 场景物体渲染器（树木/岩石/山脉）
		DiagnosticLog.Event("OverworldScene2D", "prop_renderer_start");
		_propRenderer = new OverworldPropRenderer2D();
		AddChild(_propRenderer);
		// 实机用 ChunkManager 初始化（内含全局地形数据），避免空网格导致山脉 BFS 失效
		if (!RunReadyStep(readyReport, "prop_renderer_initialize", () =>
		{
			if (_chunkManager != null)
				_propRenderer.InitializeFromChunks(seed, _chunkManager);
			else
				_propRenderer.Initialize(seed, _grid!);
		}))
			return;
		DiagnosticLog.Event("OverworldScene2D", "prop_renderer_end");

		// 墨色地形覆盖层
		DiagnosticLog.Event("OverworldScene2D", "decal_renderer_start");
		_decalRenderer = new OverworldDecalRenderer2D();
		AddChild(_decalRenderer);
		if (!RunReadyStep(readyReport, "decal_renderer_initialize", () => _decalRenderer.Initialize(seed)))
			return;
		DiagnosticLog.Event("OverworldScene2D", "decal_renderer_end");

		// 相机
		DiagnosticLog.Event("OverworldScene2D", "camera_start");
		_camera = new OverworldCamera2D();
		_camera.BaseZoom = 1.0f;
		_camera.ProcessPriority = 20;
		if (!RunReadyStep(readyReport, "camera_add", () => AddChild(_camera)))
			return;
		DiagnosticLog.Event("OverworldScene2D", "camera_end");

		// 导航系统（NavigationServer2D）
		DiagnosticLog.Event("OverworldScene2D", "navigation_start");
		if (!RunReadyStep(readyReport, "init_navigation", () => InitNavigation()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "navigation_end");

		// 玩家
		DiagnosticLog.Event("OverworldScene2D", "player_start");
		if (!RunReadyStep(readyReport, "init_player", () => InitPlayer(gs)))
			return;
		DiagnosticLog.Event("OverworldScene2D", "player_end");

		// 迷雾
		DiagnosticLog.Event("OverworldScene2D", "fog_start");
		if (!RunReadyStep(readyReport, "init_fog", () => InitFog()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "fog_end");

		// 相机边界
		DiagnosticLog.Event("OverworldScene2D", "camera_bounds_start");
		if (!RunReadyStep(readyReport, "init_camera_bounds", () => InitCameraBounds()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "camera_bounds_end");

		// 初始加载：地面/shader 全图缓存，装饰层按当前可加载范围流式加载。
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		DiagnosticLog.Event("OverworldScene2D", "initial_chunk_load_start");
		if (!RunReadyStep(readyReport, "initial_chunk_load", () => InitialChunkLoad()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "initial_chunk_load_end", new Dictionary<string, object?>
		{
			["props"] = _propRenderer?.PropCount ?? -1,
		});

		// 实体
		DiagnosticLog.Event("OverworldScene2D", "entities_start");
		if (!RunReadyStep(readyReport, "init_entities", () => InitEntities()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "entities_end", new Dictionary<string, object?>
		{
			["entities"] = EntityMgr?.Entities.Count ?? -1,
		});

		// POI 渲染
		DiagnosticLog.Event("OverworldScene2D", "poi_render_start");
		if (!RunReadyStep(readyReport, "render_world_pois", () => RenderWorldPOIs()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "poi_render_end");

		// 交互
		DiagnosticLog.Event("OverworldScene2D", "interaction_start");
		if (!RunReadyStep(readyReport, "init_interaction", () => InitInteraction()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "interaction_end");

		// 道路
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		DiagnosticLog.Event("OverworldScene2D", "roads_rivers_start");
		if (!RunReadyStep(readyReport, "render_roads_and_rivers", () => RenderRoadsAndRivers()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "roads_rivers_end");

		// 小地图
		DiagnosticLog.Event("OverworldScene2D", "minimap_start");
		if (!RunReadyStep(readyReport, "init_minimap", () => InitMinimap()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "minimap_end");

		// 领地覆盖层
		DiagnosticLog.Event("OverworldScene2D", "territory_overlay_start");
		if (!RunReadyStep(readyReport, "init_territory_overlay", () => InitTerritoryOverlay()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "territory_overlay_end");

		// 音频
		DiagnosticLog.Event("OverworldScene2D", "audio_start");
		if (!RunReadyStep(readyReport, "init_audio", () => InitAudio()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "audio_end");

		// 天气系统
		DiagnosticLog.Event("OverworldScene2D", "weather_start");
		if (!RunReadyStep(readyReport, "init_weather", () => InitWeatherSystem()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "weather_end");

		// 昼夜系统
		DiagnosticLog.Event("OverworldScene2D", "daynight_start");
		if (!RunReadyStep(readyReport, "setup_daynight", () => SetupDayNightCycle()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "daynight_end");

		// UI
		DiagnosticLog.Event("OverworldScene2D", "ui_start");
		if (!RunReadyStep(readyReport, "init_ui", () =>
		{
			InitUI();
			InitToast();
			InitRegionNameOverlay();
		}))
			return;
		DiagnosticLog.Event("OverworldScene2D", "ui_end");

		// 调试控制台
		DiagnosticLog.Event("OverworldScene2D", "debug_console_start");
		if (!RunReadyStep(readyReport, "setup_debug_console", () => SetupDebugConsole()))
			return;
		DiagnosticLog.Event("OverworldScene2D", "debug_console_end");

		// Focus the camera on the player.
		if (!RunReadyStep(readyReport, "force_camera_to_player", () => ForceCameraToPlayer()))
			return;

		GD.Print($"[OverworldScene2D] 就绪: tiles={_grid.TileCount()}, POI={WorldPois.Count}, seed={seed}");
		DiagnosticLog.Event("OverworldScene2D", "ready_complete", new Dictionary<string, object?>
		{
			["tiles"] = _grid?.TileCount() ?? -1,
			["pois"] = WorldPois?.Count ?? -1,
			["seed"] = seed,
			["log"] = DiagnosticLog.CurrentLogPath,
		});
		_initialized = true;
		readyReport.Complete();
		readyReport.FinishAndWrite();
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

	private bool RunReadyStep(DiagnosticPipelineReport report, string name, System.Action action)
	{
		var step = report.BeginStep(name, CaptureReadySnapshot());
		try
		{
			action();
			report.EndStep(step, CaptureReadySnapshot());
			return true;
		}
		catch (System.Exception ex)
		{
			report.FailStep(step, ex, CaptureReadySnapshot());
			report.Fail(ex);
			report.FinishAndWrite();
			DiagnosticLog.Exception($"OverworldScene2D ready step failed: {name}", ex);
			throw;
		}
	}

	private Dictionary<string, object?> CaptureReadySnapshot()
	{
		return new Dictionary<string, object?>
		{
			["children"] = GetChildCount(),
			["grid_tiles"] = _grid?.TileCount() ?? -1,
			["world_pois"] = WorldPois?.Count ?? -1,
			["special_characters"] = _worldSpecialCharacters?.Count ?? -1,
			["chunk_manager"] = _chunkManager != null,
			["known_chunks"] = _chunkManager?.AllKnownChunks.Count ?? -1,
			["active_chunks"] = _chunkManager?.ActiveChunks.Count ?? -1,
			["generated_chunk_coords"] = _chunkManager?.GeneratedChunkCoords.Count ?? -1,
			["entities"] = EntityMgr?.Entities.Count ?? -1,
			["props"] = _propRenderer?.PropCount ?? -1,
			["player_x"] = _playerPixelPos.X,
			["player_y"] = _playerPixelPos.Y,
			["renderer"] = _renderer != null,
			["prop_renderer"] = _propRenderer != null,
			["decal_renderer"] = _decalRenderer != null,
			["camera"] = _camera != null,
			["ui"] = _overworldUi != null,
		};
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

		// 时间推进：统一由 CampaignClock 计算
		_campaignClock.IsPaused = IsTimePaused;
		_campaignClock.PlayerMoving = _playerMoving;
		_campaignClock.PlayerWaiting = IsWaiting;
		_campaignClock.AISimulationActive = HasActiveEntityMotion();
		_campaignClock.BaseGameTimeScale = GameTimeScale;
		var clockResult = _campaignClock.Tick(dt);

		if (clockResult.ShouldAdvanceHours && EconomyMgr != null)
		{
			OverworldDiagnostics.LogClockStateChange(clockResult.Reason, timePassing: true, clockResult.DeltaHours);

			EconomyMgr.AdvanceTime(clockResult.DeltaHours);
			// 旅行食物消耗只跟玩家移动/等待相关，AI 活跃但玩家静止时不消耗
			if (clockResult.PlayerTravelDeltaHours > 0f)
				EconomyMgr.ConsumeFoodByTravel(clockResult.PlayerTravelDeltaHours);
			// 推进交战计时（分层渐进式战斗更新）
			EntityMgr?.TickGameHour(clockResult.DeltaHours);
		}
		else if (!clockResult.ShouldAdvanceHours)
		{
			OverworldDiagnostics.LogClockStateChange(clockResult.Reason, timePassing: false, 0f);
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
				OverworldDiagnostics.LogClockPause(IsTimePaused, "space_key");
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

		// 左键点击 -> CommandRouter 语义命令分发（唯一入口）
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			var pixelTarget = GetGlobalMousePosition();

			var routedCmd = _commandRouter.Resolve(
				pixelTarget,
				_battlefieldLayer,
				_siegeLayer,
				EntityMgr?.Entities ?? new List<OverworldEntity>(),
				WorldPois,
				GetCurrentPlayerFaction());

			GD.Print($"[CommandRouter] 路由结果: {routedCmd.Type} at {routedCmd.WorldPosition}");

			switch (routedCmd)
			{
				case JoinFieldBattleCommand joinCmd:
				{
					Vector2 joinTarget = joinCmd.Opportunity.HasWorldPosition ? joinCmd.Opportunity.WorldPosition : joinCmd.WorldPosition;
					float playerDist = _playerPixelPos.DistanceTo(joinTarget);
					SetDirectedBattleJoin(joinCmd.Opportunity, joinTarget);
					if (playerDist <= BATTLEFIELD_DIST)
						TryResolveDirectedInteraction(forceBattleJoin: true);
					else
					{
						_toast?.Show("正在靠近战场。");
						ResumeTravelForDirectedInteraction();
						StartPathfinding(joinTarget);
					}
					GetViewport().SetInputAsHandled();
					return;
				}

				case JoinSiegeCommand siegeCmd:
				{
					Vector2 joinTarget = siegeCmd.Opportunity.HasWorldPosition ? siegeCmd.Opportunity.WorldPosition : siegeCmd.WorldPosition;
					float playerDist = _playerPixelPos.DistanceTo(joinTarget);
					SetDirectedBattleJoin(siegeCmd.Opportunity, joinTarget);
					if (playerDist <= BATTLEFIELD_DIST)
						TryResolveDirectedInteraction(forceBattleJoin: true);
					else
					{
						_toast?.Show("正在靠近围城地点。");
						ResumeTravelForDirectedInteraction();
						StartPathfinding(joinTarget);
					}
					GetViewport().SetInputAsHandled();
					return;
				}

				case InspectEntityCommand inspectCmd:
				{
					var entity = inspectCmd.Entity;

					// 点击战斗中的实体 → 优先显示战场加入面板
					if (entity.CurrentAIState == OverworldEntity.AIState.Engaged && EntityMgr != null)
					{
						float playerDist = _playerPixelPos.DistanceTo(entity.Position);
						var joinOpportunity = WarBattleJoinService.Query(
							entity.Position,
							EntityMgr.Entities,
							WorldPois,
							GetCurrentPlayerFaction(),
							EntityMgr.Armies,
							joinRadius: 500.0f,
							engine: EntityMgr.WorldEngine,
							battlefieldRegistry: _battlefieldRegistry,
							battleResolver: EntityMgr.Simulation.BattleResolver,
							currentGameHour: EntityMgr.SimCtx.GameHour);

						if (joinOpportunity != null)
						{
							Vector2 joinTarget = joinOpportunity.HasWorldPosition ? joinOpportunity.WorldPosition : entity.Position;
							playerDist = _playerPixelPos.DistanceTo(joinTarget);
							SetDirectedBattleJoin(joinOpportunity, joinTarget);
							if (playerDist <= BATTLEFIELD_DIST)
								TryResolveDirectedInteraction(forceBattleJoin: true);
							else
							{
								_toast?.Show("正在靠近战场。");
								ResumeTravelForDirectedInteraction();
								StartPathfinding(joinTarget);
							}
							GetViewport().SetInputAsHandled();
							return;
						}
					}

					// 点击非战斗实体 → 检查距离并触发交互
					float dist = _playerPixelPos.DistanceTo(entity.Position);

					if (dist > INTERACT_DIST)
					{
						SetDirectedEntityInteraction(entity);
						_toast?.Show($"正在靠近 {entity.EntityName}。");
						ResumeTravelForDirectedInteraction();
						StartPathfinding(entity.Position);
						GetViewport().SetInputAsHandled();
						return;
					}

					ClearDirectedInteraction();
					// 距离足够，触发交互或战斗
					if (!IsHostileToCurrentPlayer(entity))
					{
						// 友方/中立：触发友方交互
						TriggerFriendlyInteraction(entity);
					}
					else
					{
						// 敌对：触发战斗（跳过追逃判定）
						TriggerHostileEncounter(entity);
					}

					GetViewport().SetInputAsHandled();
					return;
				}

			case MoveToPoiCommand poiCmd:
				{
					SetDirectedPoiInteraction(poiCmd.Poi, poiCmd.WorldPosition);
					GD.Print($"[CommandRouter] 前往 POI: {poiCmd.Poi.PoiName}");
					break;
				}
			}

			// MoveToCommand / 默认：解除暂停/等待并寻路
			if (IsTimePaused)
			{
				IsTimePaused = false;
			}
			if (IsWaiting)
			{
				IsWaiting = false;
			}

			// 若 CommandRouter 没命中的 POI，手动查一遍（兼容旧路径）
			if (routedCmd is not MoveToPoiCommand)
			{
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
					clickedPoi = FindPOIAtPosition(pixelTarget);

				if (clickedPoi != null)
				{
					SetDirectedPoiInteraction(clickedPoi, pixelTarget);
					GD.Print($"[Input] 点击并设定目标 POI: {clickedPoi.PoiName}");
				}
				else
				{
					ClearDirectedInteraction();
					GD.Print("[Input] 点击空地，清空目标 POI");
				}
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
		PlayerParty.ProcessPriority = -20;
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

		_battlefieldTooltip = new BladeHex.View.UI.Overworld.BattlefieldTooltip { Name = "BattlefieldTooltip" };
		_overworldUi.AddChild(_battlefieldTooltip);

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
			if (_battlefieldTooltip != null && (_battlefieldTooltip.IsShowing || !string.IsNullOrEmpty(_lastHoveredBattlefieldKey)))
			{
				_battlefieldTooltip.HidePanel();
				_lastHoveredBattlefieldKey = "";
			}
			return;
		}

		var mouseGlobal = GetGlobalMousePosition();

		// ── 从 _battlefieldLayer 查询战场 hover（取代旧 FindFieldBattleAtPosition）──
		string? hitBfKey = _battlefieldLayer?.HitTest(mouseGlobal);
		var hoverBattle = hitBfKey != null ? _battlefieldLayer?.HoveredBattlefield : null;
		if (hoverBattle.HasValue)
		{
			if (_poiTooltip != null && _poiTooltip.IsShowing)
			{
				_poiTooltip.HidePanel();
				_lastHoveredPoi = null;
			}
			if (_entityTooltip.IsShowing || _lastHoveredEntity != null)
			{
				_entityTooltip.HidePanel();
				_lastHoveredEntity = null;
				_lastHoveredEntityStateText = "";
				_lastHoveredEntityIntentText = "";
			}

			var screenPos = GetViewport().GetMousePosition();
			var battle = hoverBattle.Value;
			if (_battlefieldTooltip != null)
			{
				if (_lastHoveredBattlefieldKey != battle.Key)
				{
					_battlefieldTooltip.ShowForBattlefield(
						battle.Attacker,
						battle.Defender,
						screenPos,
						battle.AttackerRelation,
						battle.DefenderRelation,
						_worldNations,
						attackerCount: battle.AttackerNames.Length,
						defenderCount: battle.DefenderNames.Length,
						attackerTotalPower: battle.AttackerTotalPower,
						defenderTotalPower: battle.DefenderTotalPower);
					_lastHoveredBattlefieldKey = battle.Key;
				}
				else
				{
					_battlefieldTooltip.ShowAt(screenPos);
				}
			}
			return;
		}

		if (_battlefieldTooltip != null && (_battlefieldTooltip.IsShowing || !string.IsNullOrEmpty(_lastHoveredBattlefieldKey)))
		{
			_battlefieldTooltip.HidePanel();
			_lastHoveredBattlefieldKey = "";
		}

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
					EntityMgr.SimCtx.ZocManager,
					EntityMgr.SimCtx.WeatherSpeedFactor);
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
