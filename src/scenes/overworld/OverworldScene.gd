# OverworldScene.gd
# 战略层大地图入口场景 — 集成完整AI生态（围攻/回援/追击/逃跑/交互）
extends Node2D

var player_party: OverworldParty
var camera: Camera2D
var ui: OverworldUI
var economy_manager: Node

# 战争迷雾系统
var fog_of_war: FogOfWar
var fog_renderer: FogOfWarRenderer

# 玩家种族（用于分种族初始揭示和存档）
var player_race_id: int = RaceData.Race.HUMAN

# 交互系统
var interaction_manager: InteractionManager
var interaction_panel: InteractionPanel
var dialogue_panel: DialoguePanel
var trade_panel: TradePanel
var rest_panel: RestPanel
var town_panel: TownPanel
var arena_panel: ArenaPanel
var smithy_panel: SmithyPanel
var training_panel: TrainingPanel
var temple_panel: TemplePanel
var quest_board_panel: QuestBoardPanel
var recruit_panel: RecruitPanel

# 玩家角色数据
var player_unit_data: UnitData

# 世界生成
var world_generator: WorldGenerator
var entity_manager: OverworldEntityManager
var world_pois: Array[OverworldPOI] = []
var world_entities: Array[OverworldEntity] = []

# POI 可视化节点
var poi_visuals: Array[Node2D] = []

# ======== 六边形瓦片大地图系统 (战场兄弟风格) ========
var hex_grid: HexOverworldGrid
var hex_generator: HexOverworldGenerator
var hex_renderer: HexOverworldRenderer
var hex_astar: HexOverworldAStar

# 游戏时间
var food_tick_timer: float = 0.0
var day_tick_timer: float = 0.0
var day_counter: int = 1
var _poi_entered: bool = false
## 当前是否正在遭遇敌方实体（防止每帧重复触发）
var _encounter_active: bool = false
## 上次触发交互的原始实体（用于防止反复触发同一实体）
var _last_encountered_entity: OverworldEntity = null

## 任务目标点可视化节点 (QuestTargetVisual)
var quest_target_visuals: Array[QuestTargetVisual] = []
## 上次接近的任务目标点ID（防止重复触发）
var _last_approached_quest_id: String = ""

# 常量（基于 HEX_SIZE=156, 313px纹理）
const VISION_RANGE := 3000.0
const ENCOUNTER_DIST := 300.0
const POI_ENTER_DIST := 450.0
const QUEST_TARGET_APPROACH_DIST := 600.0  ## 玩家接近任务目标点的距离

var canvas_modulate: CanvasModulate
var time_gradient: Gradient
var game_time_scale: float = 2.0 
var is_time_paused: bool = false
var is_waiting: bool = false # 骑砍式等待模式

func _ready():
	# 播放大地图旅行背景音乐
	AudioManager.play_scenario_bgm(AudioManager.Scenario.OVERWORLD, "default", 2.0)
	
	_init_economy()
	_setup_camera()
	_setup_time_gradient()
	
	# ======== 六边形瓦片大地图初始化 ========
	_init_hex_overworld()
	
	# 昼夜系统
	canvas_modulate = CanvasModulate.new()
	add_child(canvas_modulate)
	
	# ======== 获取玩家种族 ========
	_determine_player_race()

	# ======== 世界加载 ========
	world_generator = WorldGenerator.new()
	# 直接在六边形网格上生成 POI（不再用噪声虚拟坐标空间）
	_generate_world_pois()
	# 初始 AI 实体
	var entity_data = world_generator.generate_entities(world_pois)
	world_entities = entity_data
	
	# ======== 战争迷雾系统 ========
	_init_fog_of_war()
	
	# 初始化 AI 实体管理器
	entity_manager = OverworldEntityManager.new()
	entity_manager.set_hex_navigation(hex_grid, hex_astar)
	entity_manager.load_world(world_pois, world_entities)
	# 连接所有世界事件信号
	entity_manager.village_attacked.connect(_on_village_attacked)
	entity_manager.siege_started.connect(_on_siege_started)
	entity_manager.siege_resolved.connect(_on_siege_resolved)
	entity_manager.reinforcement_arrived.connect(_on_reinforcement_arrived)
	entity_manager.ai_battle_occurred.connect(_on_ai_battle)
	entity_manager.poi_captured.connect(_on_poi_captured)
	entity_manager.entity_removed.connect(_on_entity_removed)
	add_child(entity_manager)
	
	# 渲染世界 POI
	_render_world_pois()
	
	# ======== 初始化玩家队伍 ========
	player_party = OverworldParty.new()
	player_party.set_hex_navigation(hex_grid, hex_astar)
	add_child(player_party)
	
	# ======== 初始化移动速度组件 ========
	_init_speed_component()
	
	# 放置玩家在第一个城镇附近（确保不在水域）
	var start_pos := _find_hex_start_position()
	if start_pos == Vector2.ZERO:
		var start_town = _find_nearest_poi_of_type(
			hex_grid.get_center_pixel(),
			OverworldPOI.POIType.TOWN
		)
		if start_town:
			var tile := hex_grid.find_passable_near_pixel(start_town.position.x + 50.0, start_town.position.y + 50.0, 15)
			if tile:
				start_pos = tile.pixel_pos
			else:
				start_pos = hex_grid.get_valid_start_pos()
		else:
			start_pos = hex_grid.get_valid_start_pos()
	player_party.place_at(start_pos.x, start_pos.y)
	
	camera.position = player_party.position
	
	# ======== UI ========
	ui = OverworldUI.new()
	ui.layer = 10
	ui.economy_manager = economy_manager
	add_child(ui)
	ui.menu_opened.connect(_on_ui_menu_opened)
	_setup_initial_player_data()
	# 角色数据初始化后，刷新速度组件引用
	if player_party.speed_component:
		player_party.speed_component.unit_data = player_unit_data
	_update_ui_info()

	# ======== 应用游戏设置 ========
	_apply_game_settings()

	# ======== 交互系统 ========
	_setup_interaction_system()

	# ======== 调试控制台 ========
	_create_debug_console()
	_create_tile_aligner()


# ========================================
# 六边形瓦片大地图系统
# ========================================

## 初始化六边形大地图 (生成+渲染)
func _init_hex_overworld() -> void:
	hex_generator = HexOverworldGenerator.new()
	hex_grid = hex_generator.generate()  # 使用默认 64×48
	hex_astar = HexOverworldAStar.new(hex_grid)
	
	# 渲染
	hex_renderer = HexOverworldRenderer.new()
	hex_renderer.setup(hex_grid)
	add_child(hex_renderer)
	# 六边形地图位于场景坐标空间, 偏移到让地图居中
	var renderer_offset := hex_renderer.get_pixel_offset()
	hex_renderer.position = Vector2(-renderer_offset.x * 0.5, -renderer_offset.y * 0.5)
	
	print("[OverworldScene] 六边形瓦片地图初始化完成: %d 瓦片, %.0f×%.0f px" % [
		hex_grid.tile_count(), hex_grid.map_pixel_width, hex_grid.map_pixel_height])


## 在六边形网格上直接生成 POI（不经过噪声地图虚拟坐标）
func _generate_world_pois() -> void:
	var regions := hex_generator.get_regions()
	
	# 第1步: 在各区域中心放置城镇 (3-4 个)
	var town_names := ["艾尔德镇", "铁锤堡", "晨曦城", "河湾镇"]
	var town_count := mini(town_names.size(), 3 + randi() % 2)
	for i in range(town_count):
		var region: HexOverworldGenerator.RegionDef = regions[i % regions.size()]
		var tile := hex_generator.find_settlement_position(region.name, 8)
		if not tile:
			continue
		tile.has_settlement = true
		tile.settlement_type = OverworldPOI.POIType.TOWN
		tile.poi_id = town_names[i]
		var poi := OverworldPOI.new()
		poi.poi_name = town_names[i]
		poi.poi_type = OverworldPOI.POIType.TOWN
		poi.position = tile.pixel_pos
		poi.has_tavern = true
		poi.has_shop = true
		poi.has_blacksmith = true
		poi.garrison_max = 30 + randi() % 20
		poi.garrison_current = poi.garrison_max
		world_pois.append(poi)
	
	# 第2步: 村庄 (8-12 个)
	var village_names := ["柳溪村", "石桥村", "绿叶村", "河畔村", "山脚村", "枫林村", "白杨村", "谷仓村", "松林村", "晨露村", "暮色村", "鹤鸣村"]
	var village_count := 8 + randi() % 5
	for i in range(village_count):
		var region: HexOverworldGenerator.RegionDef = regions[i % regions.size()]
		if region.danger_level > 0.5:
			continue
		var tile := hex_generator.find_settlement_position(region.name, 5)
		if not tile:
			continue
		tile.has_settlement = true
		tile.settlement_type = OverworldPOI.POIType.VILLAGE
		tile.poi_id = village_names[i % village_names.size()]
		var poi := OverworldPOI.new()
		poi.poi_name = village_names[i % village_names.size()]
		poi.poi_type = OverworldPOI.POIType.VILLAGE
		poi.position = tile.pixel_pos
		poi.garrison_max = 10 + randi() % 10
		poi.garrison_current = poi.garrison_max
		world_pois.append(poi)
	
	# 第3步: 城堡 (1-2 个)
	var castle_names := ["霜鹰堡", "龙脊要塞"]
	var castle_count := 1 + randi() % 2
	for i in range(castle_count):
		var region: HexOverworldGenerator.RegionDef = regions[0]
		for reg in regions:
			if reg.danger_level > 0.3 and reg.danger_level < 0.7:
				region = reg
				break
		var tile := hex_generator.find_settlement_position(region.name, 10)
		if not tile:
			continue
		tile.has_settlement = true
		tile.settlement_type = OverworldPOI.POIType.CASTLE
		tile.poi_id = castle_names[i % castle_names.size()]
		var poi := OverworldPOI.new()
		poi.poi_name = castle_names[i % castle_names.size()]
		poi.poi_type = OverworldPOI.POIType.CASTLE
		poi.position = tile.pixel_pos
		poi.garrison_max = 50 + randi() % 30
		poi.garrison_current = poi.garrison_max
		world_pois.append(poi)
	
	print("[OverworldScene] POI 生成完成: %d 个" % world_pois.size())


## 在六边形地图上找玩家起始位置
func _find_hex_start_position() -> Vector2:
	if not hex_grid:
		return Vector2.ZERO
	
	# 优先找城镇瓦片
	for tile in hex_grid.get_settlement_tiles():
		if tile.settlement_type == OverworldPOI.POIType.TOWN and tile.is_passable:
			return tile.pixel_pos
	
	# 回退: 地图中心附近可通行
	return hex_grid.get_valid_start_pos()


## 获取六边形地形在指定像素位置的移动速度倍率
func get_hex_terrain_speed_at(px: float, py: float) -> float:
	if not hex_grid:
		return 1.0
	var terrain_type := hex_grid.sample_terrain_at_pixel(px, py)
	return OverworldTerrain.get_move_speed_multiplier(terrain_type)


# ========================================
# 世界 POI 渲染
# ========================================

func _render_world_pois():
	for poi in world_pois:
		var visual: Node2D = null
		
		match poi.poi_type:
			OverworldPOI.POIType.TOWN:
				visual = _create_town_visual(poi, Color(0.2, 0.4, 0.8), 25.0)
			OverworldPOI.POIType.VILLAGE:
				var size = 15.0 + poi.prosperity * 0.1
				visual = _create_town_visual(poi, Color(0.3, 0.6, 0.3), size)
			OverworldPOI.POIType.CASTLE:
				visual = _create_town_visual(poi, Color(0.5, 0.4, 0.2), 28.0)
			OverworldPOI.POIType.SETTLEMENT:
				var color = _get_settlement_color(poi.settlement_race)
				visual = _create_marker_visual(poi, color, 18.0)
			OverworldPOI.POIType.LAIR:
				var color = _get_lair_color(poi.lair_type)
				visual = _create_marker_visual(poi, color, 20.0)
		
		if visual:
			add_child(visual)
			poi_visuals.append(visual)

func _create_town_visual(poi: OverworldPOI, color: Color, size: float) -> Node2D:
	var town = OverworldTown.new()
	town.town_name = poi.poi_name
	town.place_at(poi.position.x, poi.position.y)
	if town.visual_poly:
		town.visual_poly.color = color
		var points = PackedVector2Array()
		points.append(Vector2(-size, -size))
		points.append(Vector2(size, -size))
		points.append(Vector2(size, size))
		points.append(Vector2(-size, size))
		town.visual_poly.polygon = points
	return town

func _create_marker_visual(poi: OverworldPOI, color: Color, size: float) -> Node2D:
	var marker = OverworldEnemy.new()
	marker.place_at(poi.position.x, poi.position.y)
	if marker.visual_poly:
		marker.visual_poly.color = color
		marker.visual_poly.scale = Vector2(size / 10.0, size / 10.0)
	return marker

func _get_settlement_color(race: int) -> Color:
	match race:
		OverworldPOI.SettlementRace.GOBLIN: return Color(0.6, 0.4, 0.2)
		OverworldPOI.SettlementRace.KOBOLD: return Color(0.5, 0.4, 0.3)
		OverworldPOI.SettlementRace.MINOTAUR: return Color(0.7, 0.3, 0.2)
		OverworldPOI.SettlementRace.SHADOW_CULT: return Color(0.4, 0.2, 0.5)
		_: return Color(0.6, 0.3, 0.3)

func _get_lair_color(lair_type: int) -> Color:
	match lair_type:
		OverworldPOI.LairType.DRAGON_LAIR: return Color(0.8, 0.6, 0.1)
		OverworldPOI.LairType.ANCIENT_TOMB: return Color(0.4, 0.4, 0.5)
		OverworldPOI.LairType.RUINS: return Color(0.6, 0.5, 0.3)
		OverworldPOI.LairType.GOLEM_FORGE: return Color(0.5, 0.3, 0.2)
		_: return Color(0.5, 0.5, 0.5)


# ========================================
# 查找辅助
# ========================================

func _find_nearest_poi_of_type(pos: Vector2, type: int) -> OverworldPOI:
	var closest: OverworldPOI = null
	var closest_dist := 99999.0
	for poi in world_pois:
		if poi.poi_type == type:
			var d = pos.distance_to(poi.position)
			if d < closest_dist:
				closest_dist = d
				closest = poi
	return closest


## 查找点击位置附近的POI（任意类型）
func _find_nearest_poi_in_range(click_pos: Vector2, max_dist: float) -> OverworldPOI:
	var closest: OverworldPOI = null
	var closest_dist := max_dist
	for poi in world_pois:
		var d = click_pos.distance_to(poi.position)
		if d < closest_dist:
			closest_dist = d
			closest = poi
	return closest


## 查找点击位置附近的AI实体
func _find_nearest_entity_in_range(click_pos: Vector2, max_dist: float) -> OverworldEntity:
	var closest: OverworldEntity = null
	var closest_dist := max_dist
	for entity in world_entities:
		if not entity.is_alive: continue
		var d = click_pos.distance_to(entity.position)
		if d < closest_dist:
			closest_dist = d
			closest = entity
	return closest


## 查找点击位置附近的任务目标点
func _find_nearest_quest_target_in_range(click_pos: Vector2, max_dist: float) -> QuestTargetSite:
	if not quest_manager: return null
	var closest: QuestTargetSite = null
	var closest_dist := max_dist
	for site in quest_manager.get_all_target_sites():
		if site.is_cleared: continue
		var d := click_pos.distance_to(site.world_position)
		if d < closest_dist:
			closest_dist = d
			closest = site
	return closest


# ========================================
# 场景基础设施
# ========================================

func _setup_camera():
	camera = Camera2D.new()
	camera.zoom = Vector2(0.25, 0.25)
	camera.position_smoothing_enabled = true
	camera.position_smoothing_speed = 8.0
	add_child(camera)
	camera.make_current()


## 初始化移动速度组件
func _init_speed_component():
	var speed_comp = MovementSpeedComponent.new()
	speed_comp.hex_grid = hex_grid
	speed_comp.economy_manager = economy_manager
	speed_comp.unit_data = player_unit_data
	player_party.speed_component = speed_comp


## ========================================
# 战争迷雾系统
## ========================================

## 从 GlobalState 或存档确定玩家种族
func _determine_player_race() -> void:
	# 优先从存档恢复
	if GlobalState.is_loading_save and not GlobalState.loaded_data.is_empty():
		var saved_race = GlobalState.loaded_data.get("character", {}).get("race_id", -1)
		if saved_race >= 0:
			player_race_id = int(saved_race)
			return
	# 从出身选择获取
	if GlobalState.player_origin.has("race") and GlobalState.player_origin["race"] is RaceData:
		player_race_id = GlobalState.player_origin["race"].race_id
		return
	# 快速游戏：使用已随机生成的角色种族
	if GlobalState.player_origin.has("unit_data") and GlobalState.player_origin["unit_data"] is UnitData:
		var ud: UnitData = GlobalState.player_origin["unit_data"]
		if ud.race is RaceData:
			player_race_id = ud.race.race_id
			return
	# 默认人类
	player_race_id = RaceData.Race.HUMAN


## 初始化战争迷雾
func _init_fog_of_war() -> void:
	var map_w := int(hex_grid.map_pixel_width)
	var map_h := int(hex_grid.map_pixel_height)
	var cell_sz := int(HexOverworldTile.HEX_SIZE * 2.0)
	# 检查是否有存档中的迷雾数据
	if GlobalState.is_loading_save and not GlobalState.loaded_data.is_empty():
		var saved_fog = GlobalState.loaded_data.get("fog_of_war", {})
		if saved_fog.size() > 0:
			fog_of_war = FogOfWar.deserialize(saved_fog)
			fog_of_war.map_width_px = map_w
			fog_of_war.map_height_px = map_h
	# 无存档数据时新建
	if not fog_of_war:
		fog_of_war = FogOfWar.new()
		fog_of_war.initialize(
			map_w,
			map_h,
			cell_sz,
			player_race_id
		)

	# 创建渲染器
	fog_renderer = FogOfWarRenderer.new()
	add_child(fog_renderer)
	fog_renderer.setup(fog_of_war)

	# 把迷雾引用传给六边形渲染器，使其渲染 chunk 时能查询迷雾状态
	if hex_renderer:
		hex_renderer.fog = fog_of_war


## 揭示指定区域（购买地图、NPC情报等）
func reveal_map_area(center: Vector2, radius: float) -> void:
	if fog_of_war:
		fog_of_war.reveal_area(center, radius)
		fog_renderer.mark_dirty()


## 揭示指定区域名的整块区域
func reveal_map_region(region_name: String) -> void:
	if fog_of_war:
		fog_of_war.reveal_region_by_name(region_name)
		fog_renderer.mark_dirty()


## ========================================
# 游戏设置应用
## ========================================

## 从 GameSettings 加载并应用运行时参数
func _apply_game_settings() -> void:
	var settings := GameSettings.new()
	if settings.load_from_file():
		game_time_scale = settings.game_speed
		settings.apply_to_engine()


var quest_manager: QuestManager

func _init_economy():
	economy_manager = EconomyManager.new()
	economy_manager.name = "EconomyManager"
	get_tree().root.call_deferred("add_child", economy_manager)
	
	# 同步初始化任务管理器
	quest_manager = QuestManager.new()
	quest_manager.name = "QuestManager"
	add_child(quest_manager)
	
	# 连接任务目标点信号
	quest_manager.quest_target_spawned.connect(_on_quest_target_spawned)
	quest_manager.quest_target_cleared.connect(_on_quest_target_cleared)


# ========================================
# 交互系统
# ========================================

func _setup_interaction_system():
	interaction_manager = InteractionManager.new()
	interaction_manager.player_party = player_party
	interaction_manager.hex_grid = hex_grid
	add_child(interaction_manager)

	# 交互面板
	interaction_panel = InteractionPanel.new()
	add_child(interaction_panel)
	interaction_panel.option_selected.connect(_on_interaction_option_selected)
	interaction_panel.close_requested.connect(_on_interaction_closed)

	# 对话面板
	dialogue_panel = DialoguePanel.new()
	add_child(dialogue_panel)
	dialogue_panel.dialogue_finished.connect(_on_dialogue_finished)

	# 交易面板
	trade_panel = TradePanel.new()
	add_child(trade_panel)
	trade_panel.trade_finished.connect(_on_sub_panel_closed)

	# 休息面板
	rest_panel = RestPanel.new()
	add_child(rest_panel)
	rest_panel.rest_completed.connect(_on_sub_panel_closed)

	# 城镇面板
	town_panel = TownPanel.new()
	add_child(town_panel)
	town_panel.facility_selected.connect(_on_facility_selected)
	town_panel.leave_town.connect(_on_sub_panel_closed)

	# 竞技场面板
	arena_panel = ArenaPanel.new()
	add_child(arena_panel)
	arena_panel.arena_finished.connect(_on_sub_panel_closed)

	# 铁匠铺面板
	smithy_panel = SmithyPanel.new()
	add_child(smithy_panel)
	smithy_panel.smithy_finished.connect(_on_sub_panel_closed)

	# 训练场面板
	training_panel = TrainingPanel.new()
	add_child(training_panel)
	training_panel.training_finished.connect(_on_sub_panel_closed)

	# 神殿面板
	temple_panel = TemplePanel.new()
	add_child(temple_panel)
	temple_panel.temple_finished.connect(_on_sub_panel_closed)

	# 委托面板
	quest_board_panel = QuestBoardPanel.new()
	add_child(quest_board_panel)
	quest_board_panel.board_closed.connect(_on_sub_panel_closed)

	# 招募面板
	recruit_panel = RecruitPanel.new()
	add_child(recruit_panel)
	recruit_panel.recruit_finished.connect(_on_recruit_finished)

	# InteractionManager 信号
	interaction_manager.interaction_requested.connect(_on_interaction_requested)
	interaction_manager.combat_requested.connect(_on_combat_from_interaction)
	interaction_manager.dialogue_requested.connect(_on_dialogue_requested)
	interaction_manager.trade_requested.connect(_on_trade_requested)
	interaction_manager.rest_requested.connect(_on_rest_requested)
	interaction_manager.train_requested.connect(_on_train_requested)
	interaction_manager.heal_requested.connect(_on_heal_requested)
	interaction_manager.arena_requested.connect(_on_arena_requested)
	interaction_manager.quest_requested.connect(_on_quest_requested)
	interaction_manager.repair_requested.connect(_on_repair_requested)
	interaction_manager.interaction_completed.connect(_on_interaction_completed)


func _on_interaction_requested(entity, options: Array) -> void:
	is_time_paused = true
	interaction_panel.show_for_entity(entity, options)

func _on_interaction_option_selected(option: InteractionOption) -> void:
	interaction_panel.hide_panel()
	interaction_manager.execute_option(option)

func _on_interaction_closed() -> void:
	interaction_panel.hide_panel()
	interaction_manager.end_interaction()
	is_time_paused = false

func _on_interaction_completed(_result: String) -> void:
	is_time_paused = false

func _on_combat_from_interaction(battle_context: BattleContext) -> void:
	interaction_manager.end_interaction()
	is_time_paused = false
	_enter_combat_with_context(battle_context)

func _on_dialogue_requested(profile: NPCProfile) -> void:
	dialogue_panel.show_dialogue(profile)

func _on_dialogue_finished() -> void:
	interaction_manager.end_interaction()
	is_time_paused = false

func _on_trade_requested(source_name: String) -> void:
	var econ = economy_manager as EconomyManager if economy_manager is EconomyManager else null
	trade_panel.show_trade(source_name, econ)

func _on_rest_requested(_facility_type: int) -> void:
	var econ = economy_manager as EconomyManager if economy_manager is EconomyManager else null
	rest_panel.show_rest(econ)

func _on_train_requested() -> void:
	var econ = economy_manager as EconomyManager if economy_manager is EconomyManager else null
	training_panel.show_training(econ)

func _on_heal_requested() -> void:
	var econ = economy_manager as EconomyManager if economy_manager is EconomyManager else null
	temple_panel.show_temple(econ)

func _on_arena_requested() -> void:
	var econ = economy_manager as EconomyManager if economy_manager is EconomyManager else null
	arena_panel.show_arena(econ)

func _on_quest_requested() -> void:
	quest_board_panel.show_board()

func _on_repair_requested() -> void:
	var econ = economy_manager as EconomyManager if economy_manager is EconomyManager else null
	smithy_panel.show_smithy(econ)

func _on_recruit_finished(_hired: bool) -> void:
	_on_sub_panel_closed()

func _on_facility_selected(facility_type: int) -> void:
	town_panel.hide_panel()
	var econ = economy_manager as EconomyManager if economy_manager is EconomyManager else null
	match facility_type:
		TownFacility.FacilityType.MARKET:
			trade_panel.show_trade(_current_town_name(), econ)
		TownFacility.FacilityType.TAVERN:
			rest_panel.show_rest(econ)
		TownFacility.FacilityType.TEMPLE:
			temple_panel.show_temple(econ)
		TownFacility.FacilityType.ARENA:
			arena_panel.show_arena(econ)
		TownFacility.FacilityType.SMITHY:
			smithy_panel.show_smithy(econ)
		TownFacility.FacilityType.TRAINING:
			training_panel.show_training(econ)
		TownFacility.FacilityType.CASTLE:
			quest_board_panel.show_board()

func _current_town_name() -> String:
	if interaction_manager._current_entity is OverworldTown:
		return interaction_manager._current_entity.town_name
	return "商店"

func _on_sub_panel_closed(_dummy = null) -> void:
	# 如果城镇面板还在显示，不恢复
	if town_panel.is_panel_visible():
		return
	interaction_manager.end_interaction()
	is_time_paused = false

func _enter_combat_with_context(ctx: BattleContext) -> void:
	print("战斗上下文: %s" % ctx.get_description())
	player_party.is_moving = false
	_enter_combat_scene(ctx)


# ========================================
# 输入处理
# ========================================

func _unhandled_input(event):
	# 调试快捷键
	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_F1:
			_debug_visible = not _debug_visible
			if _debug_panel:
				_debug_panel.visible = _debug_visible
			return
		if event.keycode == KEY_F5:
			_refresh_debug_info()
			return
		if event.keycode == KEY_F6:
			_toggle_tile_aligner()
			_refresh_debug_info()  # 更新控制台显示状态
			return
		if event.keycode == KEY_F7:
			_output_aligner_data()
			return
	
	# 瓦片拼接工具输入
	_tile_aligner_input(event)
	if _tile_aligner_active:
		# 拼接模式下拦截点击，不移动玩家
		if event is InputEventMouseButton and event.pressed:
			return
	
	if not player_party: return
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_LEFT:
			var click_pos = get_global_mouse_position()
			
			# 优先检查是否点击了POI（城镇/村庄/城堡）
			var clicked_poi = _find_nearest_poi_in_range(click_pos, 600.0)
			if clicked_poi:
				var tile := hex_grid.find_passable_near_pixel(clicked_poi.position.x + 350.0, clicked_poi.position.y + 350.0, 15)
				player_party.move_to(tile.pixel_pos if tile else clicked_poi.position)
			else:
				# 检查是否点击了任务目标点
				var clicked_target = _find_nearest_quest_target_in_range(click_pos, 600.0)
				if clicked_target:
					var tile := hex_grid.find_passable_near_pixel(clicked_target.world_position.x + 230.0, clicked_target.world_position.y + 230.0, 15)
					player_party.move_to(tile.pixel_pos if tile else clicked_target.world_position)
				else:
					# 检查是否点击了敌方实体附近
					var clicked_entity = _find_nearest_entity_in_range(click_pos, 600.0)
					if clicked_entity:
						var tile := hex_grid.find_passable_near_pixel(clicked_entity.position.x + 230.0, clicked_entity.position.y + 230.0, 15)
						player_party.move_to(tile.pixel_pos if tile else clicked_entity.position)
					else:
						# 普通移动
						player_party.move_to(click_pos)
			
			# 移动时自动取消等待状态
			if is_waiting:
				is_waiting = false
				if is_instance_valid(ui):
					ui.update_top_info_status("")
					
		elif event.button_index == MOUSE_BUTTON_WHEEL_UP:
			camera.zoom *= 1.1
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			camera.zoom *= 0.9


# ========================================
# 每帧更新
# ========================================

func _setup_time_gradient():
	time_gradient = Gradient.new()
	# 昼夜渐变曲线：亮度单调递减（黄昏→午夜），色温自然过渡
	# 关键原则：黄昏后 RGB 三通道持续单调下降，不存在回升
	# ratio = hour / 24，如 0.75 = 18:00
	time_gradient.add_point(0.00, Color(0.20, 0.20, 0.35))  # 00:00 午夜（最暗）
	time_gradient.add_point(0.08, Color(0.20, 0.20, 0.35))  # 02:00 深夜
	time_gradient.add_point(0.18, Color(0.25, 0.24, 0.38))  # 04:20 黎明前
	time_gradient.add_point(0.22, Color(0.50, 0.38, 0.42))  # 05:20 破晓
	time_gradient.add_point(0.27, Color(0.80, 0.58, 0.45))  # 06:30 日出
	time_gradient.add_point(0.33, Color(0.95, 0.88, 0.78))  # 08:00 早晨
	time_gradient.add_point(0.42, Color(1.00, 1.00, 1.00))  # 10:00 白昼
	time_gradient.add_point(0.54, Color(1.00, 1.00, 0.98))  # 13:00 午后
	time_gradient.add_point(0.65, Color(0.98, 0.94, 0.86))  # 15:30 傍晚前
	time_gradient.add_point(0.71, Color(0.92, 0.74, 0.55))  # 17:00 金色黄昏
	time_gradient.add_point(0.75, Color(0.78, 0.50, 0.38))  # 18:00 日落橙红
	time_gradient.add_point(0.79, Color(0.55, 0.35, 0.32))  # 19:00 暮色（暖→冷过渡）
	time_gradient.add_point(0.83, Color(0.38, 0.28, 0.30))  # 20:00 入夜
	time_gradient.add_point(0.88, Color(0.28, 0.24, 0.32))  # 21:00 深夜前
	time_gradient.add_point(0.94, Color(0.22, 0.21, 0.34))  # 22:30 深夜
	time_gradient.add_point(1.00, Color(0.20, 0.20, 0.35))  # 24:00 午夜（闭环=00:00）

func has_variable(var_name: String) -> bool:
	return var_name in self

# ...

func _process(delta):
	if not player_party or not camera or not economy_manager: return

	# 更新玩家位置给AI管理器
	entity_manager.update_player_position(player_party.position)

	# 摄像机跟随玩家
	camera.position = player_party.position

	# 骑砍式时间流逝逻辑：只有在移动或点击了“等待”时，时间才推进
	var should_time_pass = (player_party.is_moving or is_waiting) and not is_time_paused

	if should_time_pass:
		var delta_hours = delta * game_time_scale
		# 如果是特意等待，时间流速加快
		if is_waiting and not player_party.is_moving:
			delta_hours *= 4.0 

		economy_manager.advance_time(delta_hours)

		# 食物消耗
		economy_manager.consume_food(delta_hours * 0.1)

	# AI 实体始终更新 (或者也跟随时间流转？通常 AI 应该只在时间流动时动)
	if should_time_pass:
		entity_manager.tick_movement(delta)

	_update_visual_cycle()
	_update_visibility()
	_update_ui_info()

	# 检查与敌方实体的遭遇（通过交互系统）
	var encountered = entity_manager.check_player_encounters(player_party.position)
	if encountered:
		if not _encounter_active or encountered != _last_encountered_entity:
			_encounter_active = true
			_last_encountered_entity = encountered
			var enemy = _create_enemy_from_entity(encountered)
			if enemy:
				interaction_manager.trigger_interaction(enemy)
			else:
				_trigger_combat_with_entity(encountered)
			return
	else:
		# 玩家已离开遭遇范围，重置标志
		_encounter_active = false
		_last_encountered_entity = null

	# 检查进入 POI（城镇/村庄）
	var entered_poi = entity_manager.check_player_poi_enter(player_party.position, player_party.is_moving)
	if entered_poi and not _poi_entered:
		_poi_entered = true
		player_party.is_moving = false
		var town = _create_town_from_poi(entered_poi)
		if town:
			interaction_manager.trigger_interaction(town)
		else:
			ui.menu_opened.emit("poi_" + entered_poi.poi_name)
	elif not entered_poi:
		_poi_entered = false

	# 检查任务目标点接近
	_check_quest_target_proximity()

	# WASD 摄像机
	var cam_speed = 1000.0 * delta / camera.zoom.x
	var move_vec = Vector2.ZERO
	if Input.is_key_pressed(KEY_W): move_vec.y -= 1
	if Input.is_key_pressed(KEY_S): move_vec.y += 1
	if Input.is_key_pressed(KEY_A): move_vec.x -= 1
	if Input.is_key_pressed(KEY_D): move_vec.x += 1
	if move_vec.length() > 0:
		camera.position += move_vec.normalized() * cam_speed

## 亮度下限 — 保证 CanvasModulate 的 RGB 分量不低于此值
const BRIGHTNESS_FLOOR := 0.22

func _update_visual_cycle():
	if not canvas_modulate or not economy_manager: return
	var time_ratio = economy_manager.current_hour / 24.0
	var base_color = time_gradient.sample(time_ratio)

	# 季节色调：微调偏移量，避免极端压暗
	var season_tint = Color.WHITE
	match economy_manager.get_season():
		EconomyManager.Season.SPRING: season_tint = Color(0.97, 1.04, 0.97)
		EconomyManager.Season.SUMMER: season_tint = Color(1.03, 1.03, 0.96)
		EconomyManager.Season.FALL:   season_tint = Color(1.06, 0.97, 0.88)
		EconomyManager.Season.WINTER: season_tint = Color(0.90, 0.97, 1.10)

	var final_color = base_color * season_tint

	# 亮度下限保护：防止任何通道低于 BRIGHTNESS_FLOOR
	final_color.r = maxf(final_color.r, BRIGHTNESS_FLOOR)
	final_color.g = maxf(final_color.g, BRIGHTNESS_FLOOR)
	final_color.b = maxf(final_color.b, BRIGHTNESS_FLOOR)

	canvas_modulate.color = final_color

func _update_visibility():
	if not fog_of_war or not player_party:
		return

	# 更新迷雾视野（核心逻辑：更新网格 + 降级旧视野）
	fog_of_war.update_vision(player_party.position)

	# 渲染迷雾纹理
	if fog_renderer:
		var viewport_size = get_viewport().get_visible_rect().size
		fog_renderer.update_render(camera.position, viewport_size, camera.zoom.x)

	# 根据迷雾状态控制 POI 可见性
	for visual in poi_visuals:
		if is_instance_valid(visual):
			visual.visible = fog_of_war.is_revealed(visual.position.x, visual.position.y)

	# 根据迷雾状态控制 AI 实体可见性（仅当前视野内可见）
	if entity_manager:
		for entity in world_entities:
			if is_instance_valid(entity) and entity.is_alive:
				if entity.has_method("set_visible"):
					entity.visible = fog_of_war.is_in_vision(entity.position.x, entity.position.y)

func _update_ui_info():
	if not ui or not economy_manager: return
	
	var clock_text = "%02d:%02d" % [
		int(economy_manager.current_hour), 
		int((economy_manager.current_hour - int(economy_manager.current_hour)) * 60)
	]
	
	ui.update_top_info(
		economy_manager.year,
		economy_manager.month,
		economy_manager.days_passed,
		economy_manager.get_season_name(),
		clock_text,
		economy_manager.gold,
		int(economy_manager.food),
		int(economy_manager.max_food)
	)

# ========================================
# 战斗触发
# ========================================

## 从 OverworldEntity 创建 OverworldEnemy（用于交互系统）
func _create_enemy_from_entity(entity: OverworldEntity) -> OverworldEnemy:
	var enemy = OverworldEnemy.new()
	enemy.display_name = entity.entity_name
	enemy.description_text = "战力: %.0f | 人数: %d | 等级: %d" % [entity.combat_power, entity.party_size, entity.party_level]
	enemy.place_at(entity.position.x, entity.position.y)
	# 让 OverworldEnemy 持有原始实体引用，方便交互后清理
	enemy.set_meta("original_entity", entity)
	
	# 根据实体类型创建对应的 NPC 档案
	if entity.entity_type == OverworldEntity.EntityType.ADVENTURER:
		var profile = NPCProfile.new()
		profile.npc_name = entity.entity_name
		profile.npc_type = NPCProfile.NPCType.ADVENTURER
		profile.attitude = NPCProfile.Attitude.FRIENDLY if not entity.is_hostile_to_player else NPCProfile.Attitude.HOSTILE
		profile.gold = entity.gold_carried
		profile.faction = entity.faction
		enemy.npc_profile = profile
	elif entity.entity_type == OverworldEntity.EntityType.RAIDING_PARTY:
		# 掠夺队是敌对人形
		var profile = NPCProfile.new()
		profile.npc_name = entity.entity_name
		profile.npc_type = NPCProfile.NPCType.HOSTILE_HUMANOID
		profile.attitude = NPCProfile.Attitude.HOSTILE
		profile.faction = entity.faction
		enemy.npc_profile = profile
	elif entity.entity_type == OverworldEntity.EntityType.LORD_ARMY:
		# 领主军队：根据敌对状态决定态度
		var profile = NPCProfile.new()
		profile.npc_name = entity.entity_name
		profile.npc_type = NPCProfile.NPCType.HOSTILE_HUMANOID if entity.is_hostile_to_player else NPCProfile.NPCType.ADVENTURER
		profile.attitude = NPCProfile.Attitude.HOSTILE if entity.is_hostile_to_player else NPCProfile.Attitude.NEUTRAL
		profile.faction = entity.faction
		enemy.npc_profile = profile
	elif entity.entity_type == OverworldEntity.EntityType.CARAVAN:
		# 商队
		var profile = NPCProfile.new()
		profile.npc_name = entity.entity_name
		profile.npc_type = NPCProfile.NPCType.MERCHANT
		profile.attitude = NPCProfile.Attitude.FRIENDLY
		profile.faction = entity.faction
		enemy.npc_profile = profile
	# 史诗怪物（龙等）不设 npc_profile，走非人形交互
	
	return enemy


## 从 OverworldPOI 创建 OverworldTown（用于交互系统）
func _create_town_from_poi(poi: OverworldPOI) -> OverworldTown:
	if poi.poi_type not in [OverworldPOI.POIType.TOWN, OverworldPOI.POIType.VILLAGE]:
		return null
	var town = OverworldTown.new()
	town.town_name = poi.poi_name
	town.prosperity = poi.prosperity
	town.faction = poi.owning_faction
	town.garrison = poi.garrison_current if poi.poi_type == OverworldPOI.POIType.CASTLE else int(poi.prosperity / 2.0)
	if poi.poi_type == OverworldPOI.POIType.VILLAGE:
		town.setup_village_facilities()
	else:
		town.setup_default_facilities()
	town.place_at(poi.position.x, poi.position.y)
	return town


func _trigger_combat_with_entity(entity: OverworldEntity):
	print("遭遇 %s: %s！切换至战术战场..." % [entity.get_type_name(), entity.entity_name])
	player_party.is_moving = false
	# 从玩家位置采样大地图坐标作为遭遇点
	var encounter_tile = hex_grid.find_passable_near_pixel(player_party.position.x, player_party.position.y, 5)
	var encounter_coord = encounter_tile.coord if encounter_tile else Vector2i.ZERO
	var terrain_type = hex_grid.sample_terrain_at_pixel(player_party.position.x, player_party.position.y)
	var ctx := BattleContext.create(terrain_type, BattleMapGenerator.BattleSize.MERCENARY, BattleContext.EngagementType.NORMAL)
	ctx.overworld_grid = hex_grid
	ctx.encounter_coord = encounter_coord
	ctx.poi_type = -1  # 野外遭遇
	entity_manager._remove_entity(entity)
	_enter_combat_scene(ctx)

func _enter_combat_scene(battle_context: BattleContext = null):
	hex_renderer.visible = false
	player_party.visible = false
	ui.visible = false
	camera.enabled = false
	for visual in poi_visuals:
		if is_instance_valid(visual): visual.visible = false
	
	var combat_scene = CombatScene.new()
	if battle_context != null:
		combat_scene.battle_context = battle_context
	combat_scene.combat_finished.connect(_on_combat_finished.bind(combat_scene))
	get_tree().root.add_child(combat_scene)


# ========================================
# 世界事件回调
# ========================================

func _on_combat_finished(victory: bool, combat_scene: Node):
	print("战斗结束！胜利: ", victory)
	combat_scene.queue_free()
	hex_renderer.visible = true
	player_party.visible = true
	ui.visible = true
	camera.enabled = true
	_update_ui_info()
	
	# 战斗结束，恢复大地图背景音乐
	AudioManager.play_bgm("res://src/assets/audio/bgm/overworld_travel.ogg", 2.0)

func _on_ui_menu_opened(menu_name: String):
	if menu_name in ["character", "inventory", "party"]:
		ui.party_panel.open_tab(menu_name, player_unit_data)
	else:
		print("打开菜单: ", menu_name)

func _setup_initial_player_data():
	player_unit_data = UnitData.new()
	player_unit_data.unit_name = "雇佣兵团长"
	player_unit_data.str = 16
	player_unit_data.dex = 14
	player_unit_data.con = 15
	player_unit_data.base_ac = 10
	
	# 给背包塞两件装备用于测试系统
	# 注意：EconomyManager 在 _init_economy 后可能需要一点时间初始化
	await get_tree().process_frame
	if is_instance_valid(economy_manager):
		var test_sword = WeaponData.new()
		test_sword.item_name = "钢剑"
		test_sword.damage_dice_sides = 8
		economy_manager.add_item(test_sword)
		
		var test_armor = ArmorData.new()
		test_armor.item_name = "锁子甲"
		test_armor.ac_bonus = 5
		test_armor.max_dex_bonus = 2
		economy_manager.add_item(test_armor)

func _on_village_attacked(village: OverworldPOI, attacker: OverworldEntity):
	print("[世界事件] %s 被 %s 袭击！繁荣度降至 %d" % [village.poi_name, attacker.entity_name, village.prosperity])

func _on_siege_started(target: OverworldPOI, attacker: OverworldEntity):
	print("[围攻开始] %s 开始围攻 %s" % [attacker.entity_name, target.poi_name])

func _on_siege_resolved(target: OverworldPOI, attacker_won: bool, attacker: OverworldEntity):
	if attacker_won:
		print("[围攻结果] %s 攻占了 %s！" % [attacker.entity_name, target.poi_name])
	else:
		print("[围攻结果] %s 守住了 %s 的围攻" % [target.poi_name, attacker.entity_name])
	_update_ui_info()

func _on_reinforcement_arrived(target: OverworldPOI, reinforcer: OverworldEntity):
	print("[回援] %s 赶到 %s 支援" % [reinforcer.entity_name, target.poi_name])

func _on_ai_battle(attacker: OverworldEntity, defender: OverworldEntity, attacker_won: bool):
	var winner = attacker.entity_name if attacker_won else defender.entity_name
	print("[AI战斗] %s vs %s → %s 获胜" % [attacker.entity_name, defender.entity_name, winner])

func _on_poi_captured(poi: OverworldPOI, new_faction: String, captor: OverworldEntity):
	print("[领土变更] %s 被 %s 攻占，归属: %s" % [poi.poi_name, captor.entity_name, new_faction])
	# TODO: 更新POI视觉颜色以反映新势力归属

func _on_entity_removed(_entity: OverworldEntity):
	# 实体被移除，可能需要更新UI
	pass


# ========================================
# 任务目标点系统
# ========================================

## 任务目标点生成回调 — 在大地图上渲染目标标记
func _on_quest_target_spawned(target_site: QuestTargetSite) -> void:
	var visual := QuestTargetVisual.new()
	visual.setup(target_site)
	add_child(visual)
	quest_target_visuals.append(visual)
	print("[OverworldScene] 渲染任务目标点: %s (%s)" % [target_site.site_name, target_site.get_site_type_name()])


## 任务目标点清理回调 — 移除目标标记
func _on_quest_target_cleared(quest_id: String) -> void:
	for i in range(quest_target_visuals.size() - 1, -1, -1):
		var visual: QuestTargetVisual = quest_target_visuals[i]
		if is_instance_valid(visual) and visual.target_site and visual.target_site.quest_id == quest_id:
			visual.mark_cleared()
			# 延迟淡出后释放节点
			var tween := create_tween()
			tween.tween_property(visual, "modulate:a", 0.0, 1.0)
			tween.tween_callback(visual.queue_free)
			quest_target_visuals.remove_at(i)
			break
	_last_approached_quest_id = ""


## 每帧检测玩家是否接近任务目标点
func _check_quest_target_proximity() -> void:
	if not player_party or not quest_manager: return

	var player_pos := player_party.position

	for visual in quest_target_visuals:
		if not is_instance_valid(visual): continue
		if not visual.target_site: continue
		if visual.target_site.is_cleared: continue

		var dist := player_pos.distance_to(visual.target_site.world_position)

		# 更新视野可见性
		visual.visible = fog_of_war.is_revealed(visual.target_site.world_position.x, visual.target_site.world_position.y)

		# 接近检测
		if dist < QUEST_TARGET_APPROACH_DIST:
			var qid: String = visual.target_site.quest_id
			if qid != _last_approached_quest_id:
				_last_approached_quest_id = qid
				_on_player_reached_quest_target(visual.target_site)
			return  # 每帧只处理一个目标点

	# 玩家离开所有目标点范围后重置
	if _last_approached_quest_id != "":
		# 检查是否真的离开了
		var last_site := quest_manager.get_target_site(_last_approached_quest_id)
		if last_site and player_pos.distance_to(last_site.world_position) >= QUEST_TARGET_APPROACH_DIST:
			_last_approached_quest_id = ""


## 玩家到达任务目标点 — 触发遭遇/战斗
func _on_player_reached_quest_target(site: QuestTargetSite) -> void:
	print("[任务] 到达目标点: %s (%s)" % [site.site_name, site.get_site_type_name()])
	player_party.is_moving = false

	# 获取遭遇配置
	var config := site.get_encounter_config()

	# 创建战斗上下文（从六边形地形采样）
	var terrain_type := hex_grid.sample_terrain_at_pixel(site.world_position.x, site.world_position.y)
	var ctx := BattleContext.create(terrain_type, BattleMapGenerator.BattleSize.MERCENARY, BattleContext.EngagementType.NORMAL)
	ctx.encounter_position = Vector2i(int(site.world_position.x), int(site.world_position.y))
	# 传入大地图数据，让 BattleMapGenerator 从大地图采样地形
	ctx.overworld_grid = hex_grid
	var encounter_tile = hex_grid.find_passable_near_pixel(site.world_position.x, site.world_position.y, 5)
	ctx.encounter_coord = encounter_tile.coord if encounter_tile else Vector2i.ZERO
	ctx.poi_type = -1  # 任务目标遭遇，无固定POI类型
	# 将任务遭遇数据存储在元数据中，供 CombatScene 使用
	ctx.set_meta("quest_id", site.quest_id)
	ctx.set_meta("enemy_ids", config.get("enemies", []))
	ctx.set_meta("cr_total", config.get("cr_total", 1.0))
	ctx.set_meta("battle_template", site.get_battle_template_name())
	ctx.set_meta("battle_modifiers", config.get("battle_modifiers", []))

	# 通过交互系统触发战斗
	_enter_combat_with_context(ctx)


## ========================================
## 调试控制台 — 可选中复制文本
## ========================================

var _debug_panel: PanelContainer
var _debug_label: RichTextLabel
var _debug_visible: bool = true

func _create_debug_console() -> void:
	# 容器 — 直接加到场景根，不用 ui 层
	_debug_panel = PanelContainer.new()
	_debug_panel.name = "DebugConsole"
	_debug_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	_debug_panel.offset_left = 8
	_debug_panel.offset_top = 60
	_debug_panel.offset_right = 620
	_debug_panel.offset_bottom = 680
	_debug_panel.z_index = 9999
	# 半透明黑色背景
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0, 0, 0, 0.85)
	bg.border_color = Color(1, 1, 0, 0.8)
	bg.set_border_width_all(2)
	_debug_panel.add_theme_stylebox_override("panel", bg)
	
	# RichTextLabel — 支持选择复制
	_debug_label = RichTextLabel.new()
	_debug_label.name = "DebugText"
	_debug_label.bbcode_enabled = true
	_debug_label.selection_enabled = true
	_debug_label.context_menu_enabled = true
	_debug_label.add_theme_font_size_override("normal_font_size", 14)
	_debug_label.add_theme_color_override("default_color", Color(0, 1, 0))
	_debug_label.scroll_following = true
	_debug_panel.add_child(_debug_label)
	
	# 用 CanvasLayer 确保在最顶层
	var debug_layer := CanvasLayer.new()
	debug_layer.name = "DebugLayer"
	debug_layer.layer = 100
	debug_layer.add_child(_debug_panel)
	add_child(debug_layer)
	
	# 首次填充
	_refresh_debug_info()

func _refresh_debug_info() -> void:
	if not _debug_label:
		return
	
	var lines: Array[String] = []
	lines.append("[color=yellow][b]=== 调试控制台 (F1=切换 F5=刷新) ===[/b][/color]")
	
	# ---- 地形统计 ----
	lines.append("\n[color=cyan][b]--- 地形 ---[/b][/color]")
	if hex_grid and not hex_grid.tiles.is_empty():
		var terrain_names := {
			0: "DEEP_WATER", 1: "SHALLOW_WATER", 2: "SAND", 3: "PLAINS",
			4: "GRASSLAND", 5: "FOREST", 6: "DENSE_FOREST", 7: "HILLS",
			8: "MOUNTAIN", 9: "SNOW", 10: "SWAMP", 11: "SAVANNA",
			12: "ROAD", 13: "RIVER"
		}
		var tc: Dictionary = {}
		for key in hex_grid.tiles:
			var t: HexOverworldTile = hex_grid.tiles[key]
			var tt: int = t.terrain
			if not tc.has(tt): tc[tt] = 0
			tc[tt] += 1
		lines.append("瓦片总数: %d" % hex_grid.tiles.size())
		for tt in tc:
			var n = terrain_names.get(tt, "UNKNOWN_%d" % tt)
			lines.append("  %s: %d" % [n, tc[tt]])
			# 显示调试颜色
			var c = _debug_terrain_color(tt)
			var hex_color = "#%02x%02x%02x" % [int(c.r*255), int(c.g*255), int(c.b*255)]
			lines.append("    [color=%s]■■■[/color] %s" % [hex_color, n])
	else:
		lines.append("[color=red]hex_grid 为空！[/color]")
	
	# ---- 渲染器 ----
	lines.append("\n[color=cyan][b]--- 渲染器 ---[/b][/color]")
	if hex_renderer:
		lines.append("初始化: %s" % str(hex_renderer._initialized))
		lines.append("纹理预加载: %d" % hex_renderer._preloaded.size())
		lines.append("活跃Chunks: %d" % hex_renderer._active_chunks.size())
		lines.append("地图尺寸: %.0f x %.0f px" % [hex_renderer.map_pixel_width, hex_renderer.map_pixel_height])
		lines.append("位置: %s" % str(hex_renderer.position))
		lines.append("grid: %s" % str(hex_renderer.grid != null))
		if hex_renderer._active_chunks.size() > 0:
			lines.append("Chunk列表: %s" % str(hex_renderer._active_chunks.keys().slice(0, 10)))
	else:
		lines.append("[color=red]hex_renderer 为空！[/color]")
	
	# ---- 迷雾 ----
	lines.append("\n[color=cyan][b]--- 迷雾 ---[/b][/color]")
	if fog_of_war:
		lines.append("网格: %d x %d (cell=%d)" % [fog_of_war.grid_w, fog_of_war.grid_h, fog_of_war.cell_size])
		lines.append("地图像素: %d x %d" % [fog_of_war.map_width_px, fog_of_war.map_height_px])
		lines.append("探索进度: %.1f%%" % (fog_of_war.get_exploration_progress() * 100.0))
		lines.append("视野范围: %.0f px" % fog_of_war.vision_range)
		# 统计迷雾状态
		var s0 := 0; var s1 := 0; var s2 := 0
		for gy in range(fog_of_war.grid_h):
			for gx in range(fog_of_war.grid_w):
				var s: int = fog_of_war.explored_grid[gy][gx]
				if s == 0: s0 += 1
				elif s == 1: s1 += 1
				else: s2 += 1
		lines.append("UNEXPLORED: %d" % s0)
		lines.append("REVEALED: %d" % s1)
		lines.append("IN_VISION: %d" % s2)
	else:
		lines.append("[color=red]fog_of_war 为空！[/color]")
	
	# ---- FogOfWarRenderer ----
	lines.append("\n[color=cyan][b]--- 迷雾渲染器 ---[/b][/color]")
	if fog_renderer:
		lines.append("fog: %s" % str(fog_renderer.fog != null))
		lines.append("image: %s" % str(fog_renderer.image != null))
		lines.append("texture: %s" % str(fog_renderer.texture != null))
		lines.append("sprite: %s" % str(fog_renderer._sprite != null))
		if fog_renderer._sprite:
			lines.append("sprite visible: %s" % str(fog_renderer._sprite.visible))
			lines.append("sprite z: %d" % fog_renderer._sprite.z_index)
			lines.append("sprite scale: %s" % str(fog_renderer._sprite.scale))
			lines.append("sprite mat: %s" % str(fog_renderer._sprite.material != null))
	else:
		lines.append("[color=red]fog_renderer 为空！[/color]")
	
	# ---- 玩家 ----
	lines.append("\n[color=cyan][b]--- 玩家 ---[/b][/color]")
	if player_party:
		lines.append("位置: %s" % str(player_party.position))
	
	_debug_label.text = "\n".join(lines)

func _debug_terrain_color(terrain_type: int) -> Color:
	match terrain_type:
		0: return Color(0.05, 0.1, 0.4)
		1: return Color(0.1, 0.3, 0.7)
		2: return Color(0.9, 0.85, 0.5)
		3: return Color(0.4, 0.75, 0.3)
		4: return Color(0.3, 0.7, 0.25)
		5: return Color(0.15, 0.5, 0.15)
		6: return Color(0.08, 0.35, 0.08)
		7: return Color(0.6, 0.55, 0.35)
		8: return Color(0.5, 0.45, 0.4)
		9: return Color(0.9, 0.93, 0.98)
		10: return Color(0.3, 0.4, 0.2)
		11: return Color(0.75, 0.7, 0.35)
		12: return Color(0.7, 0.6, 0.4)
		13: return Color(0.15, 0.35, 0.75)
		_: return Color(1.0, 0.0, 1.0)


## ========================================
## 瓦片拼接工具 — 手动微调1+6个瓦片位置
## ========================================
## 操作:
##   拖拽瓦片移动位置
##   F6 = 切换到拼接模式 / 退出拼接模式
##   F7 = 输出所有瓦片位置数据到控制台（可复制）
##   鼠标左键拖拽 = 移动选中的邻居瓦片
##   鼠标右键 = 选中下一个邻居瓦片
##   滚轮 = 微调选中瓦片 (1px) / Shift+滚轮 = 10px

var _tile_aligner_active: bool = false
var _tile_aligner_node: Node2D
var _tile_sprites: Array[Sprite2D] = []  # [0]=中心, [1-6]=邻居
var _tile_labels: Array[Label] = []
var _selected_neighbor: int = 1  # 当前选中的邻居(1-6)
var _dragging: bool = false
var _drag_offset: Vector2 = Vector2.ZERO
var _aligner_tex: Texture2D = null

func _create_tile_aligner() -> void:
	var layer := CanvasLayer.new()
	layer.name = "TileAlignerLayer"
	layer.layer = 9998
	add_child(layer)
	
	_tile_aligner_node = Node2D.new()
	_tile_aligner_node.name = "TileAligner"
	_tile_aligner_node.visible = false
	layer.add_child(_tile_aligner_node)

func _toggle_tile_aligner() -> void:
	_tile_aligner_active = not _tile_aligner_active
	if _tile_aligner_active:
		if not _tile_aligner_node:
			_create_tile_aligner()
		_build_tile_aligner()
		_tile_aligner_node.visible = true
		camera.zoom = Vector2(0.8, 0.8)
	else:
		_tile_aligner_node.visible = false
		camera.zoom = Vector2(0.5, 0.5)

func _build_tile_aligner() -> void:
	# 清理旧内容
	for child in _tile_aligner_node.get_children():
		child.queue_free()
	_tile_sprites.clear()
	_tile_labels.clear()
	
	# 直接加载纹理
	var tex := load("res://src/assets/tiles/hex_terrain/grassland_0.png") as Texture2D
	if not tex:
		push_error("[TileAligner] 纹理加载失败")
		return
	
	_aligner_tex = tex
	var tw := float(tex.get_width())
	var th := float(tex.get_height())
	
	# 6个邻居方向（axial偏移）+ 名称 + 排序用的y权重
	# 方向: (q,r), 名称
	var dirs: Array[Vector2i] = [
		Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 1),
		Vector2i(-1, 0), Vector2i(0, -1), Vector2i(1, -1)
	]
	var dir_names := ["E(+q)", "SE(+r)", "SW(-q+r)", "W(-q)", "NW(-r)", "NE(+q-r)"]
	
	# 默认间距 = 和渲染器一致的公式
	var half_w := tw / 2.0
	# 平顶六边形 axial_to_pixel:
	# x = half_w * 1.5 * q
	# y = half_w * (0.866 * q + 1.732 * r)
	var sqrt3_half := 0.866025
	
	# 中心位置 = 屏幕中心
	var vp := get_viewport().get_visible_rect().size
	var center_pos := Vector2(vp.x / 2.0, vp.y / 2.0)
	
	# 中心瓦片
	var s0 := Sprite2D.new()
	s0.texture = tex
	s0.centered = true
	s0.position = center_pos
	s0.modulate = Color(1, 1, 0.5)
	s0.z_index = 10
	_tile_aligner_node.add_child(s0)
	_tile_sprites.append(s0)
	
	var l0 := Label.new()
	l0.text = "★ 中心(q=0,r=0) ★\n纹理: %dx%d\n半宽: %.0f" % [int(tw), int(th), half_w]
	l0.position = center_pos + Vector2(-80, -th / 2.0 - 45)
	l0.add_theme_font_size_override("font_size", 15)
	l0.add_theme_color_override("font_color", Color(1, 1, 0))
	_tile_aligner_node.add_child(l0)
	_tile_labels.append(l0)
	
	# 6个邻居，按 y 坐标排序渲染（下方覆盖上方）
	var neighbors: Array = []
	for i in range(6):
		var d: Vector2i = dirs[i]
		var nx := center_pos.x + half_w * 1.5 * float(d.x)
		var ny := center_pos.y + half_w * (sqrt3_half * float(d.x) + sqrt3_half * 2.0 * float(d.y))
		neighbors.append({"idx": i, "pos": Vector2(nx, ny)})
	
	# 按 y 排序
	neighbors.sort_custom(func(a, b): return a["pos"].y < b["pos"].y)
	
	for ni in range(neighbors.size()):
		var info = neighbors[ni]
		var i: int = info["idx"]
		var pos: Vector2 = info["pos"]
		
		var s := Sprite2D.new()
		s.texture = tex
		s.centered = true
		s.position = pos
		s.z_index = 20 + ni  # y大的z高，覆盖上方
		s.modulate = Color(0.5, 1, 0.5) if i == _selected_neighbor - 1 else Color(1, 1, 1)
		_tile_aligner_node.add_child(s)
		_tile_sprites.append(s)
		
		var d: Vector2i = dirs[i]
		var l := Label.new()
		l.text = "%d: %s (q%+d,r%+d)" % [i + 1, dir_names[i], d.x, d.y]
		l.position = pos + Vector2(-50, -th / 2.0 - 25)
		l.add_theme_font_size_override("font_size", 13)
		l.add_theme_color_override("font_color", Color(0.5, 1, 0.5) if i == _selected_neighbor - 1 else Color(1, 1, 1))
		_tile_aligner_node.add_child(l)
		_tile_labels.append(l)
	
	# 操作提示
	var hint := Label.new()
	hint.text = "F6=退出 | 右键=选瓦片 | 拖拽=移动 | 滚轮=微调(Ctrl=纵向) | F7=输出数据"
	hint.position = Vector2(vp.x / 2.0 - 280, vp.y - 50)
	hint.add_theme_font_size_override("font_size", 16)
	hint.add_theme_color_override("font_color", Color(1, 0.8, 0))
	_tile_aligner_node.add_child(hint)

func _tile_aligner_input(event: InputEvent) -> void:
	if not _tile_aligner_active:
		return
	
	# F7 = 输出数据
	if event is InputEventKey and event.pressed and event.keycode == KEY_F7:
		_output_aligner_data()
		return
	
	# 右键 = 切换选中邻居
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_RIGHT:
		_selected_neighbor = (_selected_neighbor % 6) + 1
		_update_aligner_highlight()
		return
	
	# 左键拖拽（用屏幕坐标，因为瓦片在 CanvasLayer 上）
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			var mouse := get_viewport().get_mouse_position()
			var s := _tile_sprites[_selected_neighbor]
			if mouse.distance_to(s.position) < 200:
				_dragging = true
				_drag_offset = s.position - mouse
		else:
			_dragging = false
	
	if event is InputEventMouseMotion and _dragging:
		var mouse := get_viewport().get_mouse_position()
		var s := _tile_sprites[_selected_neighbor]
		s.position = mouse + _drag_offset
		# 更新标签位置
		_tile_labels[_selected_neighbor].position = s.position + Vector2(-30, -_aligner_tex.get_height() / 2.0 - 20)
	
	# 滚轮微调
	if event is InputEventMouseButton:
		var step := 1.0
		if Input.is_key_pressed(KEY_SHIFT):
			step = 10.0
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			var s := _tile_sprites[_selected_neighbor]
			if Input.is_key_pressed(KEY_CTRL):
				s.position.y -= step
			else:
				s.position.x += step
			_tile_labels[_selected_neighbor].position = s.position + Vector2(-30, -_aligner_tex.get_height() / 2.0 - 20)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			var s := _tile_sprites[_selected_neighbor]
			if Input.is_key_pressed(KEY_CTRL):
				s.position.y += step
			else:
				s.position.x -= step
			_tile_labels[_selected_neighbor].position = s.position + Vector2(-30, -_aligner_tex.get_height() / 2.0 - 20)

func _update_aligner_highlight() -> void:
	for i in range(_tile_sprites.size()):
		if i == 0:
			_tile_sprites[i].modulate = Color(1, 1, 0.5)
		elif i == _selected_neighbor:
			_tile_sprites[i].modulate = Color(0.5, 1, 0.5)
			_tile_labels[i].add_theme_color_override("font_color", Color(0.5, 1, 0.5))
		else:
			_tile_sprites[i].modulate = Color(1, 1, 1)
			_tile_labels[i].add_theme_color_override("font_color", Color(1, 1, 1))

func _output_aligner_data() -> void:
	if _tile_sprites.size() < 7:
		return
	
	var center := _tile_sprites[0].position
	
	var lines: Array[String] = []
	lines.append("[color=yellow][b]=== 瓦片拼接数据 (F7输出) ===[/b][/color]")
	lines.append("纹理尺寸: %d x %d" % [_aligner_tex.get_width(), _aligner_tex.get_height()])
	lines.append("")
	
	# 方向名
	var dir_names := ["右", "右下", "左下", "左", "左上", "右上"]
	var dir_axial := [
		Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 1),
		Vector2i(-1, 0), Vector2i(0, -1), Vector2i(1, -1)
	]
	
	lines.append("[color=cyan]相对偏移 (相对于中心):[/color]")
	for i in range(6):
		var rel := _tile_sprites[i + 1].position - center
		var ax: Vector2i = dir_axial[i]
		lines.append("  %s (q%+d,r%+d): dx=%.2f, dy=%.2f" % [
			dir_names[i], ax.x, ax.y, rel.x, rel.y
		])
	
	lines.append("")
	lines.append("[color=cyan]GDScript 代码 (可直接复制):[/color]")
	lines.append("[code]")
	lines.append("# 纹理实际尺寸")
	lines.append("const TEX_W := %d" % _aligner_tex.get_width())
	lines.append("const TEX_H := %d" % _aligner_tex.get_height())
	lines.append("const HALF_W := TEX_W / 2.0")
	lines.append("")
	lines.append("# 平顶六边形间距 (axial_to_pixel)")
	lines.append("static func tile_pixel_pos(q: int, r: int) -> Vector2:")
	
	# 分析数据推导公式
	# 取(1,0)和(0,1)的偏移
	var rel_10 := _tile_sprites[1].position - center  # 方向(1,0)
	var rel_01 := _tile_sprites[2].position - center  # 方向(0,1)
	
	lines.append("  # (1,0)偏移: (%.2f, %.2f)" % [rel_10.x, rel_10.y])
	lines.append("  # (0,1)偏移: (%.2f, %.2f)" % [rel_01.x, rel_01.y])
	lines.append("  var x := HALF_W * %.6f * float(q)" % (rel_10.x / (_aligner_tex.get_width() / 2.0)))
	lines.append("  var y := HALF_W * (%.6f * float(q) + %.6f * float(r))" % [
		rel_10.y / (_aligner_tex.get_width() / 2.0),
		rel_01.y / (_aligner_tex.get_width() / 2.0)
	])
	lines.append("  return Vector2(x, y)")
	lines.append("[/code]")
	
	# 写入调试控制台
	if _debug_label:
		_debug_label.text = "\n".join(lines)
