using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Scenes.Overworld2d.Components;
using BladeHex.Strategic;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.WorldEvents;
using BladeHex.View.Map;
using Godot;

namespace BladeHex.Tests.Strategic;

[GlobalClass]
public partial class MicroOverworldAIInteractionTest : Node2D
{
	private const int ScenarioSeed = 424242;
	private const int ScenarioChunkWidth = 4;
	private const int ScenarioChunkHeight = 4;

	private int _failures;
	private readonly List<string> _visualLog = new();

	private HexOverworldGrid? _grid;
	private List<OverworldPOI> _worldPois = new();
	private ChunkManager? _chunkManager;
	private bool _builtWithWorldCreator;
	private OverworldPOI? _castle;
	private OverworldPOI? _village;
	private OverworldPOI? _camp;
	private OverworldEntity? _player;
	private OverworldEntity? _fieldAlly;
	private OverworldEntity? _fieldEnemy;
	private OverworldEntity? _fieldJoiner;
	private OverworldEntity? _siegeMarshal;
	private OverworldEntity? _siegeJoiner;
	private OverworldEntity? _defenderJoiner;
	private Battlefield? _fieldBattle;
	private Vector2[] _fieldRoute = [];
	private bool _siegeCaptured;

	private ScenarioMarkerOverlay? _markerOverlay;

	public override void _Ready()
	{
		GD.Print("========================================");
		GD.Print("  MicroOverworldAIInteractionTest");
		GD.Print("========================================");

		try
		{
			RunScenario();
		}
		catch (Exception ex)
		{
			_failures++;
			GD.PrintErr($"[MicroOverworldAIInteractionTest] Exception: {ex}");
			_visualLog.Add($"EXCEPTION: {ex.Message}");
		}

		GD.Print("========================================");
		GD.Print(_failures == 0
			? "  MicroOverworldAIInteractionTest PASS"
			: $"  MicroOverworldAIInteractionTest FAIL: {_failures} checks failed");
		GD.Print("========================================");

		if (DisplayServer.GetName() == "headless")
		{
			GetTree().Quit(_failures == 0 ? 0 : 1);
			return;
		}

		BuildMainSceneVisuals();
		AddVisualPanel();
	}

	private void RunScenario()
	{
		var scenarioWorld = BuildScenarioWorld();
		_grid = scenarioWorld.Grid;
		_worldPois = scenarioWorld.Pois;
		_chunkManager = scenarioWorld.ChunkManager;
		_builtWithWorldCreator = scenarioWorld.BuiltWithWorldCreator;
		var astar = new HexOverworldAStar(_grid);

		_castle = SelectRequiredPoi(_worldPois, OverworldPOI.POIType.Castle, "攻守城测试需要 WorldCreator 生成城堡 POI");
		_village = SelectPoi(_worldPois, OverworldPOI.POIType.Village, _castle)
			?? _worldPois.First(p => p != _castle);
		_camp = SelectPoi(_worldPois, OverworldPOI.POIType.Settlement, _castle, _village)
			?? SelectPoi(_worldPois, OverworldPOI.POIType.Lair, _castle, _village)
			?? _worldPois.FirstOrDefault(p => p != _castle && p != _village)
			?? _village;

		_castle.OwningFaction = "player";
		_castle.GarrisonMax = Math.Max(_castle.GarrisonMax, 28);
		_castle.GarrisonCurrent = 28;

		var castleTile = TileForPoi(_grid, _castle);
		var villageTile = TileForPoi(_grid, _village);
		var campTile = TileForPoi(_grid, _camp);
		var playerTile = PickPassableTile(_grid, 0.22f, 0.78f, [castleTile.Coord, villageTile.Coord, campTile.Coord]);
		var fieldTile = PickPassableTile(_grid, 0.27f, 0.30f, [castleTile.Coord, villageTile.Coord, campTile.Coord, playerTile.Coord]);
		var joinerTile = PickPassableTile(_grid, 0.34f, 0.37f, [castleTile.Coord, villageTile.Coord, campTile.Coord, playerTile.Coord, fieldTile.Coord]);

		_player = MakeEntity("玩家队伍", OverworldEntity.EntityType.Adventurer, playerTile.PixelPos, "player", false, 180f, 8);
		_player.CurrentAIState = OverworldEntity.AIState.Idle;

		_fieldAlly = MakeEntity("边境巡逻队", OverworldEntity.EntityType.LordArmy, fieldTile.PixelPos, "player", false, 120f, 10);
		_fieldEnemy = MakeEntity("黑林劫掠队", OverworldEntity.EntityType.BanditParty, _fieldAlly.Position + new Vector2(75f, 0f), "bandit", true, 80f, 8);
		_fieldJoiner = MakeEntity("玩家友军增援", OverworldEntity.EntityType.LordArmy, joinerTile.PixelPos, "player", false, 95f, 8);

		_siegeMarshal = MakeEntity("赤旗攻城主将", OverworldEntity.EntityType.LordArmy, _castle.Position + new Vector2(260f, -60f), "invaders", true, 520f, 28);
		_siegeJoiner = MakeEntity("赤旗攻城援军", OverworldEntity.EntityType.LordArmy, _castle.Position + new Vector2(330f, 80f), "invaders", true, 440f, 24);
		_defenderJoiner = MakeEntity("灰桥守军援军", OverworldEntity.EntityType.LordArmy, _castle.Position + new Vector2(-420f, 120f), "player", false, 160f, 16);

		var entities = new List<OverworldEntity>
		{
			_player, _fieldAlly, _fieldEnemy, _fieldJoiner,
			_siegeMarshal, _siegeJoiner, _defenderJoiner,
		};

		LogMap();
		Check(_builtWithWorldCreator, "大地图由主场景 WorldCreator/WorldPipeline 构建");
		Check(_grid.TileCount() == ScenarioChunkWidth * ScenarioChunkHeight * ChunkData.TileCount, "微型大地图保留 chunk 构建尺寸");
		Check(_worldPois.Count > 0, "场景 POI 来自 WorldCreator 生成结果");
		Check(_player.Faction == "player", "场景包含玩家角色");
		Check(_castle.PoiTypeEnum == OverworldPOI.POIType.Castle, "场景包含可攻守的城堡 POI");
		Check(_grid.Tiles.Values.Select(t => t.Terrain).Distinct().Count() >= 2, "主世界生成地图包含至少两种地形");
		Check(_grid.GetPassableTiles().Length > 0, "主世界生成地图包含可通行地块");

		RunFieldBattleJoinTest(entities);
		RunSiegeJoinAndReinforcementTest(entities, astar);
		RunAIIntentStateTransitionTest(entities);
		RunWarBattleJoinQueryTest(entities);
	}

	private void RunFieldBattleJoinTest(List<OverworldEntity> entities)
	{
		var resolver = new BattleResolver();

		resolver.ProcessEntityInteractions([_fieldAlly!, _fieldEnemy!], currentGameHour: 6f);
		_fieldBattle = resolver.Battlefields.FirstOrDefault();
		Check(_fieldBattle != null && _fieldBattle.ParticipantCount == 2, "野外 AI 接触后创建 1v1 战场");

		_fieldRoute = [_fieldJoiner!.Position, _fieldEnemy!.Position];
		_fieldJoiner.Position = _fieldEnemy.Position + new Vector2(0f, 70f);
		resolver.ProcessEntityInteractions(entities, currentGameHour: 6.2f);
		_fieldBattle = resolver.Battlefields.FirstOrDefault();

		bool joinerEngaged = _fieldJoiner.CurrentAIState == OverworldEntity.AIState.Engaged
							 && _fieldJoiner.BattlefieldId == _fieldAlly!.BattlefieldId;
		bool threeParticipants = _fieldBattle != null && _fieldBattle.ParticipantCount == 3;
		bool joinedAllySide = _fieldBattle != null && _fieldBattle.AreSameSide(_fieldJoiner, _fieldAlly!);

		_visualLog.Add($"野战加入: participants={_fieldBattle?.ParticipantCount ?? 0}, joiner={_fieldJoiner.CurrentAIState}");
		GD.Print($"[ScenarioAI] field_join participants={_fieldBattle?.ParticipantCount ?? 0}, joinerState={_fieldJoiner.CurrentAIState}, battlefield={_fieldJoiner.BattlefieldId}");
		Check(joinerEngaged, "第三支 AI 队伍加入已发生的野外战斗");
		Check(threeParticipants, "战场参与者数量从 2 扩展为 3");
		Check(joinedAllySide, "加入战斗的友军进入玩家方阵营");
	}

	private void RunSiegeJoinAndReinforcementTest(List<OverworldEntity> entities, HexOverworldAStar astar)
	{
		var ctx = new OverworldSimulationContext
		{
			HexGrid = _grid,
			HexAStar = astar,
			PlayerPosition = _player!.Position,
			PlayerFaction = "player",
			CurrentDay = 9,
			GameHour = 18f,
			SpatialIndex = new EntitySpatialIndex(256),
		};
		ctx.Pois.Add(_castle!);
		ctx.Pois.Add(_village!);
		ctx.Pois.Add(_camp!);
		ctx.Entities.AddRange(entities);
		ctx.SpatialIndex!.Rebuild(ctx.Entities);

		_siegeMarshal!.CurrentAIState = OverworldEntity.AIState.Besieging;
		_siegeMarshal.SiegeTarget = _castle;
		_siegeMarshal.IsMarshal = true;
		_castle!.BeginSiege(_siegeMarshal);
		_castle.SiegeDays = 4;

		var army = ctx.Armies.Create(_siegeMarshal, _castle.PoiName, ctx.CurrentDay);
		army.State = ArmyState.Besieging;
		army.Members.Add(_siegeJoiner!);
		_siegeJoiner!.ArmyId = army.ArmyId;
		_siegeJoiner.CurrentAIState = OverworldEntity.AIState.Besieging;
		_siegeJoiner.SiegeTarget = _castle;

		float aggregateAttackPower = army.AggregateCombatPower;
		float defenderPowerBeforeSiege = _castle!.GetDefensePower();
		_visualLog.Add($"攻城加入: armyMembers={army.LivingMemberCount}, aggregatePower={aggregateAttackPower:F1}");
		GD.Print($"[ScenarioCombatPower] siege_attack members={army.LivingMemberCount}, aggregate={aggregateAttackPower:F1}, marshal={_siegeMarshal.CombatPower:F1}, joiner={_siegeJoiner.CombatPower:F1}, defender={defenderPowerBeforeSiege:F1}");
		Check(army.LivingMemberCount == 2, "攻城方援军加入同一攻城军团");
		Check(_siegeJoiner.SiegeTarget == _castle && _siegeJoiner.CurrentAIState == OverworldEntity.AIState.Besieging,
			"攻城方援军进入 Besieging 并绑定同一城堡");
		Check(aggregateAttackPower > _siegeMarshal.CombatPower, "攻城战力聚合包含加入的攻城援军");

		var siegeSignals = new VisualSiegeSignals(_visualLog);
		var siegeProcessor = new SiegeProcessor();
		siegeProcessor.SetNavigation(_grid!, astar);
		siegeProcessor.SetArmyRegistry(ctx.Armies);

		siegeProcessor.ProcessReinforcementChecks(ctx.Entities, ctx.Pois, siegeSignals, ctx.SpatialIndex);
		bool defenderJoined = _defenderJoiner!.CurrentAIState == OverworldEntity.AIState.Reinforcing
							  && _defenderJoiner.ReinforceTarget == _castle
							  && siegeSignals.ReinforcementCount > 0;
		Check(defenderJoined, "守城方附近领主响应围攻并加入回援");

		siegeProcessor.ProcessSieges(ctx.Entities, siegeSignals, ctx.CurrentDay, ctx.WorldEngine, ctx.PlayerPosition);
		_siegeCaptured = siegeSignals.PoiCapturedCount > 0 && _castle.OwningFaction == "invaders";
		_visualLog.Add($"攻城结算: resolved={siegeSignals.SiegeResolvedCount}, captured={_siegeCaptured}");
		GD.Print($"[ScenarioCombatPower] siege_resolve resolved={siegeSignals.SiegeResolvedCount}, captured={_siegeCaptured}, attackerWon={_castle.OwningFaction == "invaders"}");
		Check(siegeSignals.SiegeResolvedCount > 0, "攻城流程输出 SiegeResolved 日志");
		Check(_siegeCaptured, "攻城军团完成一次战力结算并夺取城堡");
	}

	private void RunAIIntentStateTransitionTest(List<OverworldEntity> entities)
	{
		// 准备：创建一对敌对实体，处于 Idle 状态，在彼此视野范围内
		var idleAttacker = MakeEntity("游骑斥候", OverworldEntity.EntityType.LordArmy,
			_player!.Position + new Vector2(200f, 0f), "player", false, 200f, 12);
		idleAttacker.CurrentAIState = OverworldEntity.AIState.Idle;

		var idleEnemy = MakeEntity("暗影伏兵", OverworldEntity.EntityType.BanditParty,
			_player.Position + new Vector2(350f, 0f), "bandit", true, 80f, 6);
		idleEnemy.CurrentAIState = OverworldEntity.AIState.Idle;

		entities.Add(idleAttacker);
		entities.Add(idleEnemy);

		var spatialIndex = new EntitySpatialIndex(256);
		foreach (var e in entities)
			spatialIndex.Add(e);

		// 测试1: ProcessHourlyIntent 应让敌对 Idle 实体进入 Chasing/Fleeing
		var processor = new DailyDecisionProcessor();
		processor.SetNavigation(_grid!, new HexOverworldAStar(_grid!));

		OverworldEntity.AIState attackerStateBefore = idleAttacker.CurrentAIState;
		OverworldEntity.AIState enemyStateBefore = idleEnemy.CurrentAIState;

		processor.ProcessHourlyIntent(entities, spatialIndex);

		bool attackerTransitioned = idleAttacker.CurrentAIState == OverworldEntity.AIState.Chasing
								   || idleAttacker.CurrentAIState == OverworldEntity.AIState.Fleeing
								   || idleAttacker.IsMoving;
		bool enemyTransitioned = idleEnemy.CurrentAIState == OverworldEntity.AIState.Chasing
								|| idleEnemy.CurrentAIState == OverworldEntity.AIState.Fleeing
								|| idleEnemy.IsMoving;

		GD.Print($"[ScenarioAI] intent_test attacker: {attackerStateBefore} -> {idleAttacker.CurrentAIState}, moving={idleAttacker.IsMoving}");
		GD.Print($"[ScenarioAI] intent_test enemy: {enemyStateBefore} -> {idleEnemy.CurrentAIState}, moving={idleEnemy.IsMoving}");
		_visualLog.Add($"AI意图: attacker={idleAttacker.CurrentAIState}, enemy={idleEnemy.CurrentAIState}");

		Check(attackerTransitioned, "Idle 敌对实体在视野内通过 HourlyIntent 自动进入追逃或移动状态");
		Check(enemyTransitioned, "Idle 弱势实体感知到强敌后进入逃跑或移动状态");

		// 测试2: ProcessFrameTactics 不应清掉已有有效路径
		if (idleAttacker.CurrentAIState == OverworldEntity.AIState.Chasing && idleAttacker.Path.Count > 0)
		{
			int pathCountBefore = idleAttacker.Path.Count;
			// 模拟寻路失败但保留路径的情况
			processor.ProcessFrameTactics(entities, spatialIndex);
			bool pathNotCleared = idleAttacker.Path.Count > 0 || idleAttacker.IsMoving;

			GD.Print($"[ScenarioAI] frame_tactics path preserved: before={pathCountBefore}, after={idleAttacker.Path.Count}, moving={idleAttacker.IsMoving}");
			Check(pathNotCleared, "Chasing 实体的路径在帧级战术刷新中不被无故清除");
		}
		else
		{
			// 如果实体没有进入 Chasing，跳过路径保留测试
			GD.Print("[ScenarioAI] frame_tactics path test: skipped (attacker not chasing)");
			_visualLog.Add("SKIP: frame_tactics path test (attacker not chasing)");
		}
	}

	private void RunWarBattleJoinQueryTest(List<OverworldEntity> entities)
	{
		// 测试1: 玩家靠近野战战场时应返回 JoinOpportunity
		// 此时 _fieldAlly 和 _fieldEnemy 应该已经在 Engaged 状态（由 RunFieldBattleJoinTest 设置）
		var playerNearBattle = _fieldAlly!.Position + new Vector2(100f, 0f);

		var joinOpp = WarBattleJoinService.Query(
			playerPos: playerNearBattle,
			entities: entities,
			pois: _worldPois,
			playerFaction: "player",
			joinRadius: 300f);

		bool fieldBattleFound = joinOpp != null && joinOpp.Type == WarBattleType.FieldBattle;
		GD.Print($"[ScenarioJoin] field_battle_query: found={joinOpp != null}, type={joinOpp?.Type}, dist={joinOpp?.Distance:F1}");
		_visualLog.Add($"野战加入查询: found={fieldBattleFound}, type={joinOpp?.Type}");
		Check(fieldBattleFound, "玩家靠近野战战场时 WarBattleJoinService 返回 FieldBattle 机会");

		// 测试2: 玩家远离任何战斗时不应返回 JoinOpportunity
		var playerFarAway = new Vector2(-99999f, -99999f);
		var noJoin = WarBattleJoinService.Query(
			playerPos: playerFarAway,
			entities: entities,
			pois: _worldPois,
			playerFaction: "player",
			joinRadius: 300f);

		bool noOpportunity = noJoin == null;
		GD.Print($"[ScenarioJoin] far_away_query: found={noJoin != null}");
		Check(noOpportunity, "玩家远离所有战斗时 WarBattleJoinService 不返回任何机会");

		// 测试3: 玩家靠近被围城的 POI 时应返回 Siege 机会
		// _castle 应该已经在 RunSiegeJoinAndReinforcementTest 中被围攻
		if (_castle!.IsUnderSiege)
		{
			var playerNearSiege = _castle.Position + new Vector2(150f, 0f);
			var siegeOpp = WarBattleJoinService.Query(
				playerPos: playerNearSiege,
				entities: entities,
				pois: _worldPois,
				playerFaction: "player",
				joinRadius: 300f);

			// 可能返回 Siege 或 ArmyJoin（取决于军团状态）
			bool siegeOrArmyFound = siegeOpp != null &&
				(siegeOpp.Type == WarBattleType.Siege || siegeOpp.Type == WarBattleType.ArmyJoin);
			GD.Print($"[ScenarioJoin] siege_query: found={siegeOpp != null}, type={siegeOpp?.Type}, poi={_castle.PoiName}");
			_visualLog.Add($"围城加入查询: found={siegeOrArmyFound}, type={siegeOpp?.Type}");
			Check(siegeOrArmyFound, "玩家靠近被围城 POI 时 WarBattleJoinService 返回 Siege 或 ArmyJoin 机会");
		}
		else
		{
			GD.Print("[ScenarioJoin] siege_query: skipped (castle not under siege)");
			_visualLog.Add("SKIP: siege join query (castle not under siege)");
		}
	}

	private void BuildMainSceneVisuals()
	{
		if (_grid == null)
			return;

		var renderer = new HexOverworldRenderer2D { Name = "MainSceneHexOverworldRenderer2D" };
		AddChild(renderer);
		renderer.Initialize();
		renderer.LoadFromGrid(_grid);

		var propRenderer = new OverworldPropRenderer2D { Name = "MainSceneOverworldPropRenderer2D" };
		AddChild(propRenderer);
		if (_chunkManager != null)
			propRenderer.InitializeFromChunks(ScenarioSeed, _chunkManager);
		else
			propRenderer.Initialize(ScenarioSeed, _grid);
		propRenderer.LoadPropsForTiles(_grid.Tiles.Values);
		propRenderer.UpdateLOD(1.0f);

		var poiRenderer = new POIRenderer2D { Name = "MainScenePOIRenderer2D" };
		AddChild(poiRenderer);
		poiRenderer.Initialize(_worldPois, null);
		poiRenderer.RenderAll();

		_markerOverlay = new ScenarioMarkerOverlay
		{
			Name = "ScenarioMarkerOverlay",
			ZIndex = 120,
			Data = new ScenarioMarkerData
			{
				Castle = _castle,
				Village = _village,
				Camp = _camp,
				Player = _player,
				FieldAlly = _fieldAlly,
				FieldEnemy = _fieldEnemy,
				FieldJoiner = _fieldJoiner,
				SiegeMarshal = _siegeMarshal,
				SiegeJoiner = _siegeJoiner,
				DefenderJoiner = _defenderJoiner,
				FieldRoute = _fieldRoute,
			},
		};
		AddChild(_markerOverlay);

		var camera = new Camera2D
		{
			Name = "ScenarioCamera2D",
			Position = _castle?.Position ?? _grid.GetCenterPixel(),
			Zoom = new Vector2(0.42f, 0.42f),
			Enabled = true,
		};
		AddChild(camera);
		camera.MakeCurrent();
	}

	private static ScenarioWorld BuildScenarioWorld()
	{
		var config = new WorldCreationConfig
		{
			WorldChunksW = ScenarioChunkWidth,
			WorldChunksH = ScenarioChunkHeight,
			Nations = NationConfig.GetDefaultNations().Take(3).ToList(),
			MinBiomeZoneSize = 12,
		};
		var creator = new WorldCreator();
		creator.OnProgress = (progress, message) =>
			GD.Print($"[ScenarioWorldCreator] {progress:P0} {message}");

		var worldData = creator.CreateWorld(ScenarioSeed, config);
		var chunkManager = new ChunkManager();
		chunkManager.Initialize(ScenarioSeed, config.WorldTileWidth, config.WorldTileHeight);
		chunkManager.LoadIntoMemory(worldData.Chunks);

		var grid = BuildGridFromChunks(worldData.Chunks, config.WorldTileWidth, config.WorldTileHeight);
		OverworldPOI.BindParentChildRelationships(worldData.Pois);
		GD.Print($"[ScenarioWorldCreator] chunks={worldData.Chunks.Count}, pois={worldData.Pois.Count}, specials={worldData.SpecialCharacters.Count}");
		return new ScenarioWorld(grid, worldData.Pois, chunkManager, true);
	}

	private static HexOverworldGrid BuildGridFromChunks(Dictionary<Vector2I, ChunkData> chunks, int width, int height)
	{
		var grid = new HexOverworldGrid
		{
			GridWidth = width,
			GridHeight = height,
			SeedValue = ScenarioSeed,
		};

		foreach (var chunk in chunks.Values)
		{
			foreach (var kvp in chunk.Tiles)
				grid.Tiles[kvp.Key] = kvp.Value;
		}

		CalculatePixelBounds(grid);
		return grid;
	}

	private static void CalculatePixelBounds(HexOverworldGrid grid)
	{
		if (grid.Tiles.Count == 0)
			return;

		float minX = float.MaxValue;
		float minY = float.MaxValue;
		float maxX = float.MinValue;
		float maxY = float.MinValue;

		foreach (var tile in grid.Tiles.Values)
		{
			minX = Mathf.Min(minX, tile.PixelPos.X);
			minY = Mathf.Min(minY, tile.PixelPos.Y);
			maxX = Mathf.Max(maxX, tile.PixelPos.X);
			maxY = Mathf.Max(maxY, tile.PixelPos.Y);
		}

		grid.MapPixelWidth = maxX - minX + HexOverworldTile.HexSize * 2.0f;
		grid.MapPixelHeight = maxY - minY + HexOverworldTile.HexSize * 2.0f;
	}

	private static OverworldPOI SelectRequiredPoi(List<OverworldPOI> pois, OverworldPOI.POIType type, string failureMessage)
	{
		var poi = SelectPoi(pois, type);
		if (poi == null)
			throw new InvalidOperationException($"{failureMessage}. Generated POIs: {string.Join(", ", pois.Select(p => $"{p.PoiName}:{p.PoiTypeEnum}"))}");
		return poi;
	}

	private static OverworldPOI? SelectPoi(List<OverworldPOI> pois, OverworldPOI.POIType type, params OverworldPOI?[] excluded)
	{
		var excludedSet = excluded.Where(p => p != null).ToHashSet();
		return pois
			.Where(p => p.PoiTypeEnum == type && !excludedSet.Contains(p))
			.OrderByDescending(p => p.Prosperity)
			.FirstOrDefault();
	}

	private static HexOverworldTile TileForPoi(HexOverworldGrid grid, OverworldPOI poi)
	{
		if (grid.Tiles.TryGetValue(poi.CenterHex, out var centerTile))
			return centerTile;
		return grid.FindPassableNearPixel(poi.Position.X, poi.Position.Y, 12)
			?? throw new InvalidOperationException($"POI '{poi.PoiName}' is not placed on a known scenario tile.");
	}

	private static HexOverworldTile PickPassableTile(HexOverworldGrid grid, float xRatio, float yRatio, IReadOnlyCollection<Vector2I>? reserved = null)
	{
		var target = new Vector2(grid.MapPixelWidth * xRatio, grid.MapPixelHeight * yRatio);
		var reservedSet = reserved == null ? new HashSet<Vector2I>() : new HashSet<Vector2I>(reserved);

		var tile = grid.Tiles.Values
			.Where(t => t.IsPassable && !reservedSet.Contains(t.Coord))
			.OrderBy(t => t.PixelPos.DistanceSquaredTo(target))
			.FirstOrDefault();

		if (tile == null)
			throw new InvalidOperationException("Scenario grid has no passable tile for test placement.");
		return tile;
	}

	private static OverworldPOI MakePoi(string name, OverworldPOI.POIType type, string faction, HexOverworldTile tile, int prosperity, int garrison)
	{
		var poi = new OverworldPOI
		{
			PoiName = name,
			PoiTypeEnum = type,
			Position = tile.PixelPos,
			CenterHex = tile.Coord,
			FootprintTemplateName = "solo",
			OwningFaction = faction,
			Prosperity = prosperity,
			GarrisonMax = Math.Max(garrison, 1),
			GarrisonCurrent = garrison,
		};
		poi.RebuildOccupiedHexes();
		tile.HasSettlement = true;
		tile.PoiId = name;
		tile.IsPoiCenter = true;
		return poi;
	}

	private static OverworldEntity MakeEntity(
		string name,
		OverworldEntity.EntityType type,
		Vector2 position,
		string faction,
		bool hostileToPlayer,
		float combatPower,
		int partySize)
	{
		return new OverworldEntity
		{
			EntityName = name,
			EntityTypeEnum = type,
			Position = position,
			HomePosition = position,
			TerritoryCenter = position,
			TerritoryRadius = 900f,
			MoveSpeed = 180f,
			PartySize = partySize,
			PartyLevel = 5,
			CombatPower = combatPower,
			GarrisonSize = partySize,
			Faction = faction,
			IsHostileToPlayer = hostileToPlayer,
			VisionRange = 900f,
			PatrolRadius = 360f,
			IsAlive = true,
			Lod = OverworldEntity.EntityLod.Active,
		};
	}

	private void LogMap()
	{
		var terrainSummary = _grid!.Tiles.Values
			.GroupBy(t => t.Terrain)
			.Select(g => $"{HexOverworldTile.TerrainToString(g.Key)}={g.Count()}")
			.OrderBy(s => s)
			.ToArray();

		GD.Print($"[ScenarioMap] seed={ScenarioSeed}, tiles={_grid.TileCount()}, terrains={string.Join(", ", terrainSummary)}");
		GD.Print($"[ScenarioMap] chunks={_chunkManager?.AllKnownChunks.Count ?? 0}, generatedPois={_worldPois.Count}, builtWithWorldCreator={_builtWithWorldCreator}");
		GD.Print($"[ScenarioMap] pois={string.Join(", ", _worldPois.Select(p => $"{p.PoiName}:{p.PoiTypeEnum}"))}");
		GD.Print($"[ScenarioMap] player={_player!.EntityName} pos={FormatVec(_player.Position)}");
		GD.Print($"[ScenarioMap] castle={_castle!.PoiName} garrison={_castle.GarrisonCurrent} faction={_castle.OwningFaction}");
		_visualLog.Add("主场景资源链路: WorldCreator/WorldPipeline -> ChunkData -> HexOverworldGrid");
		_visualLog.Add("渲染链路: HexOverworldRenderer2D + OverworldPropRenderer2D.InitializeFromChunks + POIRenderer2D");
		_visualLog.Add($"生成种子: {ScenarioSeed}");
		_visualLog.Add($"chunks: {_chunkManager?.AllKnownChunks.Count ?? 0}, POI: {_worldPois.Count}");
		_visualLog.Add($"地形: {string.Join(", ", terrainSummary)}");
	}

	private void Check(bool condition, string message)
	{
		if (condition)
		{
			GD.Print($"[PASS] {message}");
			_visualLog.Add($"PASS: {message}");
			return;
		}

		_failures++;
		GD.PrintErr($"[FAIL] {message}");
		_visualLog.Add($"FAIL: {message}");
	}

	private void AddVisualPanel()
	{
		var layer = new CanvasLayer { Name = "ScenarioLogCanvasLayer", Layer = 50 };
		AddChild(layer);

		var panel = new PanelContainer
		{
			Position = new Vector2(720f, 34f),
			CustomMinimumSize = new Vector2(520f, 650f),
		};

		var label = new Label
		{
			Text = BuildVisualText(),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(490f, 620f),
		};
		panel.AddChild(label);
		layer.AddChild(panel);
	}

	private string BuildVisualText()
	{
		var lines = new List<string>
		{
			"Overworld AI Integration Test",
			"",
			"使用主场景大地图资源:",
			"WorldCreator / WorldPipeline",
			"ChunkData / ChunkManager",
			"HexOverworldGrid",
			"HexOverworldRenderer2D",
			"OverworldPropRenderer2D",
			"POIRenderer2D",
			"",
			"图例:",
			"P 玩家队伍",
			"A/B 野外交战双方",
			"J 加入野战的友军",
			"M/S 攻城主将/攻城援军",
			"D 守城回援",
			"",
			"检查日志:",
		};
		lines.AddRange(_visualLog);
		lines.Add("");
		lines.Add(_failures == 0 ? "结果: PASS" : $"结果: FAIL ({_failures})");
		return string.Join("\n", lines);
	}

	private static string FormatVec(Vector2 value)
	{
		return $"({value.X:F1},{value.Y:F1})";
	}

	private sealed record ScenarioWorld(
		HexOverworldGrid Grid,
		List<OverworldPOI> Pois,
		ChunkManager ChunkManager,
		bool BuiltWithWorldCreator);

	private sealed class ScenarioMarkerData
	{
		public OverworldPOI? Castle;
		public OverworldPOI? Village;
		public OverworldPOI? Camp;
		public OverworldEntity? Player;
		public OverworldEntity? FieldAlly;
		public OverworldEntity? FieldEnemy;
		public OverworldEntity? FieldJoiner;
		public OverworldEntity? SiegeMarshal;
		public OverworldEntity? SiegeJoiner;
		public OverworldEntity? DefenderJoiner;
		public Vector2[] FieldRoute = [];
	}

	private sealed partial class ScenarioMarkerOverlay : Node2D
	{
		public ScenarioMarkerData Data { get; init; } = new();

		public override void _Draw()
		{
			DrawRoute(Data.FieldRoute, new Color(1.0f, 0.85f, 0.22f));
			DrawSiegeLines();
			DrawEntity(Data.Player, new Color(0.96f, 0.86f, 0.32f), "P");
			DrawEntity(Data.FieldAlly, new Color(0.25f, 0.58f, 0.95f), "A");
			DrawEntity(Data.FieldEnemy, new Color(0.92f, 0.28f, 0.22f), "B");
			DrawEntity(Data.FieldJoiner, new Color(0.45f, 0.78f, 1.0f), "J");
			DrawEntity(Data.SiegeMarshal, new Color(0.90f, 0.20f, 0.16f), "M");
			DrawEntity(Data.SiegeJoiner, new Color(0.95f, 0.36f, 0.18f), "S");
			DrawEntity(Data.DefenderJoiner, new Color(0.20f, 0.78f, 0.42f), "D");
		}

		private void DrawSiegeLines()
		{
			if (Data.Castle == null)
				return;

			foreach (var entity in new[] { Data.SiegeMarshal, Data.SiegeJoiner, Data.DefenderJoiner })
			{
				if (entity == null)
					continue;
				var color = entity.Faction == "player" ? new Color(0.35f, 0.9f, 0.45f) : new Color(0.95f, 0.30f, 0.16f);
				DrawLine(entity.Position, Data.Castle.Position, color, 7.0f, true);
			}
		}

		private void DrawRoute(Vector2[] route, Color color)
		{
			if (route.Length < 2)
				return;
			for (int i = 0; i < route.Length - 1; i++)
				DrawLine(route[i], route[i + 1], color, 6.0f, true);
		}

		private void DrawEntity(OverworldEntity? entity, Color color, string tag)
		{
			if (entity == null)
				return;

			var pos = entity.Position;
			DrawCircle(pos, 30f, new Color(0.02f, 0.02f, 0.02f), true);
			DrawCircle(pos, 22f, color, true);

			if (entity.CurrentAIState == OverworldEntity.AIState.Engaged)
				DrawCircle(pos, 38f, new Color(1.0f, 0.86f, 0.18f, 0.75f), false, 5.0f, true);
			if (entity.CurrentAIState == OverworldEntity.AIState.Besieging)
				DrawPolyline(Diamond(pos, 40f), new Color(1.0f, 0.55f, 0.22f), 5.0f, true);
			if (entity.CurrentAIState == OverworldEntity.AIState.Reinforcing)
				DrawCircle(pos, 42f, new Color(0.34f, 1.0f, 0.48f, 0.75f), false, 5.0f, true);
		}

		private static Vector2[] Diamond(Vector2 center, float radius)
		{
			return
			[
				center + new Vector2(0f, -radius),
				center + new Vector2(radius, 0f),
				center + new Vector2(0f, radius),
				center + new Vector2(-radius, 0f),
				center + new Vector2(0f, -radius),
			];
		}
	}

	private sealed class VisualSiegeSignals : ISiegeSignals
	{
		private readonly List<string> _log;
		public int SiegeResolvedCount { get; private set; }
		public int PoiCapturedCount { get; private set; }
		public int ReinforcementCount { get; private set; }

		public VisualSiegeSignals(List<string> log)
		{
			_log = log;
		}

		public void OnSiegeResolved(OverworldPOI target, bool attackerWon, OverworldEntity attacker)
		{
			SiegeResolvedCount++;
			GD.Print($"[Siege] resolved target={target.PoiName}, attacker={attacker.EntityName}, attackerWon={attackerWon}");
			_log.Add($"SiegeResolved: {target.PoiName}, attackerWon={attackerWon}");
		}

		public void OnPoiCaptured(OverworldPOI poi, string newFaction, OverworldEntity captor)
		{
			PoiCapturedCount++;
			GD.Print($"[Siege] captured poi={poi.PoiName}, faction={newFaction}, captor={captor.EntityName}");
			_log.Add($"PoiCaptured: {poi.PoiName} -> {newFaction}");
		}

		public void OnReinforcementArrived(OverworldPOI targetPoi, OverworldEntity reinforcer)
		{
			ReinforcementCount++;
			GD.Print($"[Siege] reinforcement poi={targetPoi.PoiName}, reinforcer={reinforcer.EntityName}");
			_log.Add($"Reinforcement: {reinforcer.EntityName} -> {targetPoi.PoiName}");
		}
	}
}
