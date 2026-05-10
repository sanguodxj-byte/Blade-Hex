# WorldGenerator.gd
# 世界生成器 —— 在大地图上程序化生成完整的生态系统
# 根据地理区域放置城镇、村庄、城堡、外族聚落、龙巢、墓穴，并生成初始AI实体
class_name WorldGenerator
extends RefCounted

## 生成结果
var pois: Array[OverworldPOI] = []
var entities: Array[OverworldEntity] = []

## 地图参数（与 OverworldMap 对齐 — 4×4 瓦片拼接）
var map_width: int = 6144
var map_height: int = 4096
var noise: FastNoiseLite

## 地理区域定义（对应01-世界观.md五大区域）
## 区域通过噪声值和空间位置共同判定
## 增强版：增加区域密度参数
class Region:
	var name: String = ""
	var noise_range: Vector2 = Vector2(-1, 1)  # (min, max)噪声范围
	var x_range: Vector2 = Vector2(0, 1)        # (min, max) x位置比例
	var y_range: Vector2 = Vector2(0, 1)        # (min, max) y位置比例
	var terrain_preference: OverworldTerrain.Type = OverworldTerrain.Type.PLAINS
	var danger_level: float = 0.0  # 0=安全, 1=极危险
	var poi_density: float = 1.0   # POI生成密度倍率（影响该区域的POI数量）

## 区域注册表
var regions: Array[Region] = []

func _init():
	_setup_regions()

func _setup_regions():
	# 中央平原：地图中部，噪声中等 — 安全区
	var plains = Region.new()
	plains.name = "中央平原"
	plains.noise_range = Vector2(-0.15, 0.25)
	plains.x_range = Vector2(0.1, 0.9)
	plains.y_range = Vector2(0.25, 0.75)
	plains.terrain_preference = OverworldTerrain.Type.PLAINS
	plains.danger_level = 0.1
	plains.poi_density = 1.2
	regions.append(plains)
	
	# 霜冠山脉：地图顶部，噪声高 — 高海拔危险区
	var mountains = Region.new()
	mountains.name = "霜冠山脉"
	mountains.noise_range = Vector2(0.25, 1.0)
	mountains.x_range = Vector2(0.1, 0.9)
	mountains.y_range = Vector2(0.0, 0.2)
	mountains.terrain_preference = OverworldTerrain.Type.MOUNTAIN
	mountains.danger_level = 0.7
	mountains.poi_density = 0.6
	regions.append(mountains)
	
	# 银叶森林：地图左侧，噪声偏高 — 精灵领地
	var forest = Region.new()
	forest.name = "银叶森林"
	forest.noise_range = Vector2(0.15, 0.5)
	forest.x_range = Vector2(0.0, 0.25)
	forest.y_range = Vector2(0.2, 0.8)
	forest.terrain_preference = OverworldTerrain.Type.FOREST
	forest.danger_level = 0.3
	forest.poi_density = 0.9
	regions.append(forest)
	
	# 焦土荒原：地图底部右侧，噪声低但干燥 — 高危险区
	var wasteland = Region.new()
	wasteland.name = "焦土荒原"
	wasteland.noise_range = Vector2(-0.15, 0.3)
	wasteland.x_range = Vector2(0.5, 1.0)
	wasteland.y_range = Vector2(0.75, 1.0)
	wasteland.terrain_preference = OverworldTerrain.Type.DESERT
	wasteland.danger_level = 0.8
	wasteland.poi_density = 0.7
	regions.append(wasteland)
	
	# 蛮荒沼泽：地图底部左侧，噪声低 — 中危险区
	var swamp = Region.new()
	swamp.name = "蛮荒沼泽"
	swamp.noise_range = Vector2(-0.3, -0.05)
	swamp.x_range = Vector2(0.0, 0.4)
	swamp.y_range = Vector2(0.75, 1.0)
	swamp.terrain_preference = OverworldTerrain.Type.SWAMP
	swamp.danger_level = 0.5
	swamp.poi_density = 0.8
	regions.append(swamp)
	
	# 丘陵草原：地图东部，半兽人领地 — 中高危险
	var grassland = Region.new()
	grassland.name = "丘陵草原"
	grassland.noise_range = Vector2(-0.1, 0.3)
	grassland.x_range = Vector2(0.7, 1.0)
	grassland.y_range = Vector2(0.25, 0.7)
	grassland.terrain_preference = OverworldTerrain.Type.PLAINS
	grassland.danger_level = 0.4
	grassland.poi_density = 0.8
	regions.append(grassland)

## 判定某个像素坐标属于哪个区域
func get_region_at(px: float, py: float, noise_val: float) -> Region:
	var nx := px / float(map_width)   # 0~1 归一化
	var ny := py / float(map_height)
	
	var best_region: Region = regions[0]  # 默认中央平原
	var best_score := -1.0
	
	for region in regions:
		# 检查是否在区域范围内
		if nx >= region.x_range.x and nx <= region.x_range.y and \
		   ny >= region.y_range.x and ny <= region.y_range.y and \
		   noise_val >= region.noise_range.x and noise_val <= region.noise_range.y:
			# 计算匹配度得分（越接近区域中心越高）
			var cx = (region.x_range.x + region.x_range.y) / 2.0
			var cy = (region.y_range.x + region.y_range.y) / 2.0
			var dist = Vector2(nx - cx, ny - cy).length()
			var score = 1.0 - dist
			if score > best_score:
				best_score = score
				best_region = region
	
	return best_region

## 检查位置是否适合放置POI（不在水中，不在太靠近其他POI的位置）
func is_valid_poi_position(px: float, py: float, min_distance: float = 120.0) -> bool:
	# 不在水域（噪声<-0.3）
	if noise and noise.get_noise_2d(px, py) < -0.25:
		return false
	# 不在地图边缘
	if px < 80 or py < 80 or px > map_width - 80 or py > map_height - 80:
		return false
	# 不太靠近已有POI
	for poi in pois:
		if poi.position.distance_to(Vector2(px, py)) < min_distance:
			return false
	return true

## 找到区域内的一个有效位置
func find_position_in_region(region: Region, min_distance: float = 120.0) -> Vector2:
	for attempt in range(50):
		var px = region.x_range.x * map_width + randf() * (region.x_range.y - region.x_range.x) * map_width
		var py = region.y_range.x * map_height + randf() * (region.y_range.y - region.y_range.x) * map_height
		if is_valid_poi_position(px, py, min_distance):
			return Vector2(px, py)
	# 降级：放宽距离要求
	for attempt in range(30):
		var px = region.x_range.x * map_width + randf() * (region.x_range.y - region.x_range.x) * map_width
		var py = region.y_range.x * map_height + randf() * (region.y_range.y - region.y_range.x) * map_height
		if is_valid_poi_position(px, py, 60.0):
			return Vector2(px, py)
	return Vector2(map_width / 2.0, map_height / 2.0)

## ========================================
## 从 JSON 数据构建 POI 对象（地图编辑器导出）
## ========================================

## 将地图编辑器导出的 JSON POI 数组转换为 OverworldPOI 对象数组
func build_pois_from_data(data_array: Array) -> Array[OverworldPOI]:
	var result: Array[OverworldPOI] = []
	for entry in data_array:
		var poi = OverworldPOI.new()
		poi.poi_name = entry.get("poi_name", "未命名")
		
		# 类型映射
		var type_str = entry.get("poi_type", "VILLAGE")
		poi.poi_type = _poi_type_from_string(type_str)
		
		# 位置
		var pos = entry.get("position", [0, 0])
		poi.position = Vector2(pos[0], pos[1])
		
		# 通用属性
		poi.owning_faction = entry.get("owning_faction", "neutral")
		poi.prosperity = int(entry.get("prosperity", 50))
		
		# 外族聚落子类型
		if poi.poi_type == OverworldPOI.POIType.SETTLEMENT:
			poi.settlement_race = _settlement_race_from_string(entry.get("settlement_race", "GOBLIN"))
			poi.threat_level = float(entry.get("threat_level", 0.5))
			poi.raid_interval_days = int(entry.get("raid_interval_days", 7))
			poi.max_raiding_parties = int(entry.get("max_raiding_parties", 2))
		
		# 巢穴子类型
		if poi.poi_type == OverworldPOI.POIType.LAIR:
			poi.lair_type = _lair_type_from_string(entry.get("lair_type", "ANCIENT_TOMB"))
			poi.lair_level = int(entry.get("lair_level", 1))
		
		# 城镇/村庄设施
		if poi.poi_type in [OverworldPOI.POIType.TOWN, OverworldPOI.POIType.VILLAGE]:
			poi.has_tavern = entry.get("has_tavern", poi.poi_type == OverworldPOI.POIType.TOWN)
			poi.has_shop = entry.get("has_shop", false)
			poi.has_blacksmith = entry.get("has_blacksmith", false)
			poi.has_quest_board = entry.get("has_quest_board", true)
			poi.has_barracks = entry.get("has_barracks", false)
		
		# 城堡防御
		if poi.poi_type == OverworldPOI.POIType.CASTLE:
			poi.castle_defense_level = int(entry.get("castle_defense_level", 2))
			poi.garrison_max = int(entry.get("garrison_max", 50))
			poi.garrison_current = int(entry.get("garrison_current", 20))
		
		result.append(poi)
		pois.append(poi)
	
	print("[WorldGenerator] 从 JSON 构建 %d 个 POI" % result.size())
	return result


## 仅为已有 POI 生成初始 AI 实体（不生成新 POI）
func generate_entities(existingpois: Array) -> Array[OverworldEntity]:
	pois = existingpois
	entities.clear()
	_generate_initial_entities()
	return entities


## ========================================
## JSON → 枚举 映射辅助
## ========================================

func _poi_type_from_string(s: String) -> int:
	match s:
		"TOWN": return OverworldPOI.POIType.TOWN
		"VILLAGE": return OverworldPOI.POIType.VILLAGE
		"CASTLE": return OverworldPOI.POIType.CASTLE
		"SETTLEMENT": return OverworldPOI.POIType.SETTLEMENT
		"LAIR": return OverworldPOI.POIType.LAIR
		_: return OverworldPOI.POIType.VILLAGE

func _settlement_race_from_string(s: String) -> int:
	match s:
		"GOBLIN": return OverworldPOI.SettlementRace.GOBLIN
		"KOBOLD": return OverworldPOI.SettlementRace.KOBOLD
		"MINOTAUR": return OverworldPOI.SettlementRace.MINOTAUR
		"SHADOW_CULT": return OverworldPOI.SettlementRace.SHADOW_CULT
		_: return OverworldPOI.SettlementRace.GOBLIN

func _lair_type_from_string(s: String) -> int:
	match s:
		"DRAGON_LAIR": return OverworldPOI.LairType.DRAGON_LAIR
		"ANCIENT_TOMB": return OverworldPOI.LairType.ANCIENT_TOMB
		"RUINS": return OverworldPOI.LairType.RUINS
		"GOLEM_FORGE": return OverworldPOI.LairType.GOLEM_FORGE
		_: return OverworldPOI.LairType.ANCIENT_TOMB

## ========================================
## 主入口：生成完整世界（后备方案，无 JSON 数据时使用）
## ========================================
func generate(mapnoise: FastNoiseLite) -> Dictionary:
	noise = mapnoise
	pois.clear()
	entities.clear()
	
	# 第1步：生成城镇（3-4个，集中在中央平原）
	_generate_towns()
	
	# 第2步：生成村庄（8-12个，分散在各安全区域）
	_generate_villages()
	
	# 第3步：生成城堡（1-2个，关键位置）
	_generate_castles()
	
	# 第4步：生成外族聚落（危险区域）
	_generate_settlements()
	
	# 第5步：生成龙巢和墓穴
	_generate_lairs()
	
	# 第6步：生成精灵定居点
	_generate_elf_settlements()
	
	# 第7步：生成矮人城邦
	_generate_dwarf_cities()
	
	# 第8步：生成初始AI实体
	_generate_initial_entities()
	
	return {"pois": pois, "entities": entities}

## 生成城镇
func _generate_towns():
	var town_names = ["艾尔德镇", "铁锤堡", "晨曦城", "河湾镇"]
	var plains = _get_region_by_name("中央平原")
	var count = mini(town_names.size(), 3 + randi() % 2)
	
	for i in range(count):
		var poi = OverworldPOI.new()
		poi.poi_name = town_names[i]
		poi.poi_type = OverworldPOI.POIType.TOWN
		poi.position = find_position_in_region(plains, 250.0)
		poi.has_tavern = true
		poi.has_shop = true
		poi.has_blacksmith = true
		poi.has_quest_board = true
		poi.prosperity = 60 + randi() % 30
		poi.owning_faction = "kingdom"
		pois.append(poi)

## 生成村庄
func _generate_villages():
	var village_names = [
		"绿溪村", "石桥村", "松林村", "麦田村", "山脚村",
		"湖畔村", "林间村", "河岸村", "果园区", "猎人谷",
		"矮丘村", "渡口村"
	]
	village_names.shuffle()
	
	# 村庄主要在中央平原和银叶森林边缘
	var safe_regions = [
		_get_region_by_name("中央平原"),
		_get_region_by_name("银叶森林"),
	]
	# 基础数量受区域密度调节
	var base_count = 8 + randi() % 5  # 8-12个
	var density_factor := 0.0
	for r in safe_regions:
		density_factor += r.poi_density
	density_factor /= float(safe_regions.size())
	var count := int(base_count * density_factor)
	count = maxi(count, 3)  # 至少3个村庄
	
	for i in range(mini(count, village_names.size())):
		var region = safe_regions[i % safe_regions.size()]
		var poi = OverworldPOI.new()
		poi.poi_name = village_names[i]
		poi.poi_type = OverworldPOI.POIType.VILLAGE
		poi.position = find_position_in_region(region, 150.0)
		poi.has_shop = randf() < 0.5
		poi.has_quest_board = true
		poi.prosperity = 30 + randi() % 40
		poi.owning_faction = "kingdom"
		pois.append(poi)

## 生成城堡
func _generate_castles():
	var castle_names = ["鹰巢堡", "磐石要塞"]
	var plains = _get_region_by_name("中央平原")
	
	for i in range(mini(2, castle_names.size())):
		var poi = OverworldPOI.new()
		poi.poi_name = castle_names[i]
		poi.poi_type = OverworldPOI.POIType.CASTLE
		poi.position = find_position_in_region(plains, 300.0)
		poi.castle_defense_level = 2  # 石堡
		poi.garrison_max = 150
		poi.garrison_current = 80 + randi() % 50
		poi.has_barracks = true
		poi.has_blacksmith = true
		poi.prosperity = 70
		poi.owning_faction = "kingdom"
		pois.append(poi)

## 生成外族聚落
func _generate_settlements():
	# 哥布林营地：森林边缘和沼泽
	_generate_settlements_for_race(
		OverworldPOI.SettlementRace.GOBLIN,
		["哥布林暗洞", "黑牙营地", "烂泥窟", "毒刺巢穴", "绿皮窟"],
		[_get_region_by_name("银叶森林"), _get_region_by_name("蛮荒沼泽"), _get_region_by_name("中央平原")],
		3 + randi() % 3  # 3-5
	)
	
	# 狗头人矿坑：山地和沼泽
	_generate_settlements_for_race(
		OverworldPOI.SettlementRace.KOBOLD,
		["深掘矿坑", "暗铜矿洞", "龙牙坑道"],
		[_get_region_by_name("霜冠山脉"), _get_region_by_name("蛮荒沼泽")],
		2 + randi() % 2  # 2-3
	)
	
	# 牛头人石堡：焦土荒原
	_generate_settlements_for_race(
		OverworldPOI.SettlementRace.MINOTAUR,
		["裂角堡", "血蹄石堡"],
		[_get_region_by_name("焦土荒原")],
		1 + randi() % 2  # 1-2
	)
	
	# 暗影教团据点：偏远地带
	_generate_settlements_for_race(
		OverworldPOI.SettlementRace.SHADOW_CULT,
		["暗影神殿", "虚无祭坛"],
		[_get_region_by_name("焦土荒原"), _get_region_by_name("蛮荒沼泽")],
		1
	)

func _generate_settlements_for_race(race: int, names: Array, region_pool: Array, count: int):
	# 根据目标区域的 POI 密度调节数量
	var density_factor := 0.0
	for r in region_pool:
		density_factor += r.poi_density
	density_factor /= float(maxi(region_pool.size(), 1))
	var adjusted_count := int(count * density_factor)
	adjusted_count = maxi(adjusted_count, 1)  # 至少生成1个
	
	names.shuffle()
	for i in range(mini(adjusted_count, names.size())):
		var region = region_pool[i % region_pool.size()]
		var poi = OverworldPOI.new()
		poi.poi_name = names[i]
		poi.poi_type = OverworldPOI.POIType.SETTLEMENT
		poi.settlement_race = race
		poi.position = find_position_in_region(region, 200.0)
		poi.threat_level = 0.3 + randf() * 0.7
		poi.raid_interval_days = 5 + randi() % 7
		poi.max_raiding_parties = 1 + randi() % 3
		poi.owning_faction = "hostile"
		pois.append(poi)

## 生成龙巢和墓穴
func _generate_lairs():
	# 龙巢：霜冠山脉
	var mountains = _get_region_by_name("霜冠山脉")
	for i in range(1 + randi() % 2):  # 1-2个龙巢
		var poi = OverworldPOI.new()
		poi.poi_name = ["霜翼巢穴", "赤焰龙窟", "冰晶龙巢"][i]
		poi.poi_type = OverworldPOI.POIType.LAIR
		poi.lair_type = OverworldPOI.LairType.DRAGON_LAIR
		poi.position = find_position_in_region(mountains, 300.0)
		poi.lair_level = 3 + randi() % 3  # 3-5级
		poi.owning_faction = "hostile"
		pois.append(poi)
	
	# 墓穴/遗迹：分散各区域
	var lair_configs = [
		{"name": "亡者墓穴", "type": OverworldPOI.LairType.ANCIENT_TOMB, "regions": ["蛮荒沼泽", "中央平原"]},
		{"name": "矮人遗迹", "type": OverworldPOI.LairType.RUINS, "regions": ["霜冠山脉"]},
		{"name": "石化圣所", "type": OverworldPOI.LairType.GOLEM_FORGE, "regions": ["焦土荒原"]},
		{"name": "荒坟岗", "type": OverworldPOI.LairType.ANCIENT_TOMB, "regions": ["中央平原", "银叶森林"]},
		{"name": "封印之间", "type": OverworldPOI.LairType.RUINS, "regions": ["银叶森林"]},
	]
	
	for config in lair_configs:
		if randf() < 0.7:  # 70%概率生成每个
			var region = _get_region_by_name(config["regions"][randi() % config["regions"].size()])
			var poi = OverworldPOI.new()
			poi.poi_name = config["name"]
			poi.poi_type = OverworldPOI.POIType.LAIR
			poi.lair_type = config["type"]
			poi.position = find_position_in_region(region, 180.0)
			poi.lair_level = 1 + randi() % 3
			poi.owning_faction = "neutral"
			pois.append(poi)

## 生成精灵定居点
func _generate_elf_settlements():
	var forest = _get_region_by_name("银叶森林")
	
	# 世界树庭：精灵王庭，森林深处
	var world_tree = OverworldPOI.new()
	world_tree.poi_name = "世界树庭"
	world_tree.poi_type = OverworldPOI.POIType.TOWN
	world_tree.position = find_position_in_region(forest, 300.0)
	world_tree.has_tavern = false
	world_tree.has_shop = true
	world_tree.has_blacksmith = false
	world_tree.has_quest_board = true
	world_tree.prosperity = 80
	world_tree.owning_faction = "elves"
	pois.append(world_tree)
	
	# 月影哨站：森林东缘，对外贸易站
	var outpost = OverworldPOI.new()
	outpost.poi_name = "月影哨站"
	outpost.poi_type = OverworldPOI.POIType.TOWN
	outpost.position = find_position_in_region(forest, 200.0)
	outpost.has_tavern = true
	outpost.has_shop = true
	outpost.has_blacksmith = false
	outpost.has_quest_board = true
	outpost.prosperity = 65
	outpost.owning_faction = "elves"
	pois.append(outpost)

## 生成矮人城邦
func _generate_dwarf_cities():
	var mountains = _get_region_by_name("霜冠山脉")
	
	# 铁炉堡：矮人首邑
	var ironforge = OverworldPOI.new()
	ironforge.poi_name = "铁炉堡"
	ironforge.poi_type = OverworldPOI.POIType.TOWN
	ironforge.position = find_position_in_region(mountains, 300.0)
	ironforge.has_tavern = true
	ironforge.has_shop = true
	ironforge.has_blacksmith = true
	ironforge.has_quest_board = true
	ironforge.prosperity = 75
	ironforge.owning_faction = "dwarves"
	pois.append(ironforge)
	
	# 霜塔堡：矮人地表贸易站
	var frosttower = OverworldPOI.new()
	frosttower.poi_name = "霜塔堡"
	frosttower.poi_type = OverworldPOI.POIType.TOWN
	frosttower.position = find_position_in_region(mountains, 250.0)
	frosttower.has_tavern = true
	frosttower.has_shop = true
	frosttower.has_blacksmith = true
	frosttower.has_quest_board = false
	frosttower.prosperity = 60
	frosttower.owning_faction = "dwarves"
	pois.append(frosttower)

## 生成初始AI实体
func _generate_initial_entities():
	# 冒险者队伍（2-3支，在安全区域巡游）
	var adv_names = ["铜剑团", "灰烬小队", "银叶猎团"]
	for i in range(2 + randi() % 2):
		var region = _get_region_by_name("中央平原")
		var pos = find_position_in_region(region, 100.0)
		var entity = OverworldEntity.new()
		entity.entity_name = adv_names[i] if i < adv_names.size() else "无名冒险者"
		entity.entity_type = OverworldEntity.EntityType.ADVENTURER
		entity.position = pos
		entity.home_position = pos
		entity.party_size = 2 + randi() % 5
		entity.party_level = 1 + randi() % 3
		entity.combat_power = entity.party_size * entity.party_level * 2.0
		entity.move_speed = 180.0
		entity.patrol_radius = 400.0
		entity.vision_range = 350.0
		entity.is_hostile_to_player = false
		entity.faction = "adventurers"
		entity.adventurer_type = ["novice", "veteran", "elite"][mini(i, 2)]
		entity.gold_carried = 30 + randi() % 100
		entities.append(entity)
	
	# 初始掠夺队（从聚落产生）
	for poi in pois:
		if poi.poi_type == OverworldPOI.POIType.SETTLEMENT and poi.should_spawn_raid_party():
			var entity = _create_raiding_party(poi)
			if entity:
				entities.append(entity)
				poi.on_raid_party_spawned()
	
	# 商队（2-3支，连接城镇）
	var towns = pois.filter(func(p): return p.poi_type == OverworldPOI.POIType.TOWN)
	if towns.size() >= 2:
		for i in range(mini(2 + randi() % 2, towns.size() - 1)):
			var entity = OverworldEntity.new()
			entity.entity_name = "商队%d" % (i + 1)
			entity.entity_type = OverworldEntity.EntityType.CARAVAN
			entity.position = towns[i].position
			entity.home_position = towns[i].position
			entity.origin_town = towns[i]
			entity.destination_town = towns[(i + 1) % towns.size()]
			entity.target_position = entity.destination_town.position
			entity.move_speed = 120.0
			entity.combat_power = 5.0
			entity.party_size = 3
			entity.party_level = 1
			entity.vision_range = 200.0
			entity.is_hostile_to_player = false
			entity.faction = "merchants"
			entity.trade_goods = 50 + randi() % 150
			entity.ai_state = OverworldEntity.AIState.MOVING_TO_TARGET
			entities.append(entity)
	
	# 史诗怪物（龙在龙巢附近）
	var dragon_lairs = pois.filter(func(p): return p.poi_type == OverworldPOI.POIType.LAIR and p.lair_type == OverworldPOI.LairType.DRAGON_LAIR)
	for lair in dragon_lairs:
		var entity = OverworldEntity.new()
		entity.entity_name = "霜冠巨龙" if lairs_front(lair) else "远古赤龙"
		entity.entity_type = OverworldEntity.EntityType.EPIC_MONSTER
		entity.monster_type = "dragon"
		entity.position = lair.position + Vector2(randf_range(-100, 100), randf_range(-100, 100))
		entity.home_position = lair.position
		entity.territory_center = lair.position
		entity.territory_radius = 400.0 + randf() * 200.0
		entity.combat_power = 30.0 + lair.lair_level * 5.0
		entity.party_level = lair.lair_level
		entity.move_speed = 250.0
		entity.patrol_radius = entity.territory_radius
		entity.vision_range = 500.0
		entity.is_hostile_to_player = true
		entity.is_aggressive = false
		entity.faction = "hostile"
		entities.append(entity)
	
	# 精灵游侠队长（1支，在银叶森林巡逻）
	var elf_towns = pois.filter(func(p): return p.owning_faction == "elves")
	if elf_towns.size() > 0:
		var elf_base = elf_towns[0]
		var elf_lord = OverworldEntity.new()
		elf_lord.entity_name = "月影游侠队长"
		elf_lord.entity_type = OverworldEntity.EntityType.LORD_ARMY
		elf_lord.position = elf_base.position + Vector2(randf_range(-50, 50), randf_range(-50, 50))
		elf_lord.home_position = elf_base.position
		elf_lord.guarded_poi = elf_base
		elf_lord.garrison_size = 20
		elf_lord.party_level = 3
		elf_lord.combat_power = 35.0
		elf_lord.move_speed = 220.0
		elf_lord.patrol_radius = 500.0
		elf_lord.vision_range = 450.0
		elf_lord.is_hostile_to_player = false
		elf_lord.faction = "elves"
		entities.append(elf_lord)
	
	# 矮人要塞守卫（1支，在霜冠山脉巡逻）
	var dwarf_towns = pois.filter(func(p): return p.owning_faction == "dwarves")
	if dwarf_towns.size() > 0:
		var dwarf_base = dwarf_towns[0]
		var dwarf_lord = OverworldEntity.new()
		dwarf_lord.entity_name = "铁炉堡卫队长"
		dwarf_lord.entity_type = OverworldEntity.EntityType.LORD_ARMY
		dwarf_lord.position = dwarf_base.position + Vector2(randf_range(-50, 50), randf_range(-50, 50))
		dwarf_lord.home_position = dwarf_base.position
		dwarf_lord.guarded_poi = dwarf_base
		dwarf_lord.garrison_size = 40
		dwarf_lord.party_level = 3
		dwarf_lord.combat_power = 50.0
		dwarf_lord.move_speed = 140.0
		dwarf_lord.patrol_radius = 300.0
		dwarf_lord.vision_range = 300.0
		dwarf_lord.is_hostile_to_player = false
		dwarf_lord.faction = "dwarves"
		entities.append(dwarf_lord)
	
	# 半兽人战团首领（1-2支，在丘陵草原巡逻）
	var grassland = _get_region_by_name("丘陵草原")
	for i in range(1 + randi() % 2):
		var pos = find_position_in_region(grassland, 200.0)
		var orc_warboss = OverworldEntity.new()
		orc_warboss.entity_name = "赤铜部落首领" if i == 0 else "灰狼酋长"
		orc_warboss.entity_type = OverworldEntity.EntityType.LORD_ARMY
		orc_warboss.position = pos
		orc_warboss.home_position = pos
		orc_warboss.garrison_size = 25 + randi() % 15
		orc_warboss.party_level = 2 + randi() % 2
		orc_warboss.combat_power = float(orc_warboss.garrison_size) * orc_warboss.party_level * 1.8
		orc_warboss.move_speed = 200.0
		orc_warboss.patrol_radius = 400.0
		orc_warboss.vision_range = 350.0
		orc_warboss.is_hostile_to_player = true
		orc_warboss.faction = "hostile"
		orc_warboss.lord_personality = OverworldPOI.LordPersonality.AGGRESSIVE
		entities.append(orc_warboss)

func lairs_front(lair: OverworldPOI) -> bool:
	return lair.position.y < map_height / 2.0

## 创建掠夺队
func _create_raiding_party(source: OverworldPOI) -> OverworldEntity:
	# 找到最近的村庄作为目标
	var villages = pois.filter(func(p): return p.poi_type == OverworldPOI.POIType.VILLAGE)
	if villages.is_empty():
		return null
	
	var closest_village: OverworldPOI = villages[0]
	var closest_dist := source.position.distance_to(villages[0].position)
	for v in villages:
		var d = source.position.distance_to(v.position)
		if d < closest_dist:
			closest_dist = d
			closest_village = v
	
	var entity = OverworldEntity.new()
	entity.entity_name = source.get_settlement_race_name() + "掠夺队"
	entity.entity_type = OverworldEntity.EntityType.RAIDING_PARTY
	entity.position = source.position + Vector2(randf_range(-30, 30), randf_range(-30, 30))
	entity.home_position = source.position
	entity.target_position = closest_village.position
	entity.source_settlement = source
	entity.party_size = 4 + randi() % 8
	entity.party_level = 1 + int(source.threat_level * 2)
	entity.combat_power = entity.party_size * entity.party_level * 1.5
	entity.move_speed = 160.0 + randf() * 80.0
	entity.vision_range = 300.0
	entity.is_hostile_to_player = true
	entity.faction = "hostile"
	entity.ai_state = OverworldEntity.AIState.MOVING_TO_TARGET
	entity.loot_carried = 0
	return entity

## 辅助：通过名称获取区域
func _get_region_by_name(name: String) -> Region:
	for region in regions:
		if region.name == name:
			return region
	return regions[0]  # fallback到中央平原
