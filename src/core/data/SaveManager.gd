# SaveManager.gd
# 处理游戏的序列化与持久化存储
extends Node

const SAVE_PATH = "user://sword_and_hex_save.dat"

## 预加载依赖
const _FogOfWarClass = preload("res://src/core/strategic/FogOfWar.gd")

## 检查是否存在有效存档
func has_save() -> bool:
	return FileAccess.file_exists(SAVE_PATH)

## 执行保存逻辑
func save_game(context: Dictionary):
	# context 结构:
	# {
	#   "economy": EconomyManager node,
	#   "player_party": OverworldParty node,
	#   "player_unit": UnitData resource,
	#   "fog_of_war": FogOfWar (可选，战争迷雾数据),
	#   "player_race_id": int (可选，玩家种族ID)
	# }
	
	var econ = context["economy"]
	var party = context["player_party"]
	var unit = context["player_unit"]
	
	var data = {
		"version": "0.2.1",
		"timestamp": Time.get_datetime_dict_from_system(),
		"economy": {
			"gold": econ.gold,
			"food": econ.food,
			"days": econ.days_passed
		},
		"world": {
			"player_pos_x": party.position.x,
			"player_pos_y": party.position.y
		},
		"character": {
			"name": unit.unit_name,
			"str": unit.str,
			"dex": unit.dex,
			"con": unit.con,
			"intel": unit.intel,
			"wis": unit.wis,
			"cha": unit.cha,
			"base_hp": unit.base_max_hp,
			"xp": unit.xp,
			"level": unit.level,
			"race_id": context.get("player_race_id", 0),
		}
	}
	
	# 战争迷雾数据（RLE 压缩）
	var fog = context.get("fog_of_war", null)
	if fog and fog is _FogOfWarClass:
		data["fog_of_war"] = fog.serialize()
	
	# 保存背包物品 (目前保存 item_name 占位，之后应保存资源路径或 ID)
	var inv_items = []
	for item in econ.player_inventory:
		inv_items.append({
			"name": item.item_name,
			"type": "weapon" if item is WeaponData else "armor"
			# 实际项目中这里需要更复杂的序列化
		})
	data["inventory"] = inv_items
	
	var file = FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if file:
		file.store_var(data)
		file.close()
		print("游戏已成功保存到: ", ProjectSettings.globalize_path(SAVE_PATH))
		return true
	return false

## 执行读取逻辑
func load_game_data() -> Dictionary:
	if not has_save():
		return {}
		
	var file = FileAccess.open(SAVE_PATH, FileAccess.READ)
	if file:
		var data = file.get_var()
		file.close()
		return data
	return {}

## 删除存档
func delete_save():
	if has_save():
		DirAccess.remove_absolute(SAVE_PATH)
