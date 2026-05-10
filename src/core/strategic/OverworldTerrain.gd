# OverworldTerrain.gd
# 大地图地形类型枚举 — 用于将大地图噪声值映射为地形类型
# 并提供大地图地形 → 战斗地图模板的映射关系
# 增强版：支持多色调色板、高度着色、区域过渡色
class_name OverworldTerrain

## 大地图地形类型
## 对应当前 OverworldMap 噪声生成的地形区间
enum Type {
	PLAINS,    # 平原 (noise: -0.1 ~ 0.3)
	FOREST,    # 森林 (noise: 0.3 ~ 0.5)
	MOUNTAIN,  # 山地 (noise > 0.5)
	SWAMP,     # 沼泽 (noise: -0.3 ~ -0.1, 低频区)
	WATER,     # 水域 (noise < -0.3)
	ROAD,      # 道路 (特殊)
	DESERT,    # 沙漠 (noise: -0.1 ~ 0.3, 高频区)
}

## ========================================
## 增强色板系统：每种地形提供 base/highlight/shadow 三色
## ========================================

class TerrainPalette:
	var base: Color = Color.WHITE
	var highlight: Color = Color.WHITE
	var shadow: Color = Color.WHITE
	var detail_tint: Color = Color(0.05, 0.05, 0.05)
	var detail_spread: float = 0.06
	
	func _init(b: Color, h: Color, s: Color, t: Color = Color(0.05, 0.05, 0.05), ds: float = 0.06):
		base = b
		highlight = h
		shadow = s
		detail_tint = t
		detail_spread = ds


## 获取地形增强色板
static func get_terrain_palette(terrain: Type) -> TerrainPalette:
	match terrain:
		Type.PLAINS:
			return TerrainPalette.new(
				Color(0.72, 0.68, 0.48),   # 暖黄绿基底
				Color(0.85, 0.80, 0.58),   # 阳光高光
				Color(0.55, 0.52, 0.35),   # 暗部
				Color(0.10, 0.08, 0.05),   # 暖色偏移
				0.06
			)
		Type.FOREST:
			return TerrainPalette.new(
				Color(0.22, 0.45, 0.18),   # 深绿基底
				Color(0.35, 0.62, 0.28),   # 翠绿高光
				Color(0.12, 0.30, 0.08),   # 暗绿
				Color(0.05, 0.12, 0.03),   # 绿色偏移
				0.10
			)
		Type.MOUNTAIN:
			return TerrainPalette.new(
				Color(0.50, 0.48, 0.52),   # 冷灰基底
				Color(0.92, 0.95, 0.98),   # 雪白高光
				Color(0.30, 0.28, 0.35),   # 深灰蓝
				Color(0.08, 0.06, 0.12),   # 冷色偏移
				0.08
			)
		Type.SWAMP:
			return TerrainPalette.new(
				Color(0.38, 0.48, 0.28),   # 暗黄绿基底
				Color(0.50, 0.60, 0.35),   # 浅绿
				Color(0.20, 0.30, 0.15),   # 积水暗色
				Color(0.06, 0.08, 0.04),   # 暗绿偏移
				0.08
			)
		Type.WATER:
			return TerrainPalette.new(
				Color(0.30, 0.45, 0.70),   # 深蓝基底
				Color(0.45, 0.60, 0.85),   # 浅蓝高光
				Color(0.18, 0.30, 0.55),   # 深水
				Color(0.04, 0.06, 0.10),   # 蓝色偏移
				0.05
			)
		Type.ROAD:
			return TerrainPalette.new(
				Color(0.75, 0.65, 0.45),   # 棕色基底
				Color(0.85, 0.75, 0.55),   # 浅棕
				Color(0.60, 0.50, 0.35),   # 深棕
				Color(0.08, 0.06, 0.04),   # 暖色偏移
				0.04
			)
		Type.DESERT:
			return TerrainPalette.new(
				Color(0.78, 0.58, 0.38),   # 赤橙基底
				Color(0.92, 0.75, 0.50),   # 亮沙
				Color(0.55, 0.38, 0.22),   # 暗沙
				Color(0.12, 0.06, 0.03),   # 暖橙偏移
				0.10
			)
		_:
			return TerrainPalette.new(
				Color(0.72, 0.68, 0.48),
				Color(0.85, 0.80, 0.58),
				Color(0.55, 0.52, 0.35)
			)


## 将大地图噪声值转换为地形类型
## 对应 OverworldMap 中 noise.get_noise_2d() 的返回值范围
static func from_noise(noise_value: float) -> Type:
	if noise_value < -0.3:
		return Type.WATER
	elif noise_value < -0.1:
		return Type.SWAMP
	elif noise_value < 0.3:
		return Type.PLAINS
	elif noise_value < 0.5:
		return Type.FOREST
	else:
		return Type.MOUNTAIN


## 将地形类型转换为中文名称
static func get_name(terrain: Type) -> String:
	match terrain:
		Type.PLAINS:   return "平原"
		Type.FOREST:   return "森林"
		Type.MOUNTAIN: return "山地"
		Type.SWAMP:    return "沼泽"
		Type.WATER:    return "水域"
		Type.ROAD:     return "道路"
		Type.DESERT:   return "沙漠"
		_:             return "未知"


## 将大地图地形映射到战斗地图模板名称
## 对应 BattleMapGenerator 中的内置模板列表
static func get_battle_template_name(terrain: Type) -> String:
	match terrain:
		Type.PLAINS:   return "plain_field"
		Type.FOREST:   return "forest_ambush"
		Type.MOUNTAIN: return "mountain_pass"
		Type.SWAMP:    return "swamp_battle"
		Type.WATER:    return "coastal_ambush"
		Type.ROAD:     return "plain_field"
		Type.DESERT:   return "desert_skirmish"
		_:             return "plain_field"


## 获取大地图地形对应的战场移动速度倍率
## 对应策划案 04-战略层系统 → 地图地形
static func get_move_speed_multiplier(terrain: Type) -> float:
	match terrain:
		Type.PLAINS:   return 1.0
		Type.FOREST:   return 0.7
		Type.MOUNTAIN: return 0.5
		Type.SWAMP:    return 0.5
		Type.WATER:    return 0.3
		Type.ROAD:     return 1.5
		Type.DESERT:   return 0.8
		_:             return 1.0


## 获取地形对应的遭遇概率倍率（越高越容易遭遇敌人）
static func get_encounter_rate_multiplier(terrain: Type) -> float:
	match terrain:
		Type.PLAINS:   return 1.0
		Type.FOREST:   return 1.5
		Type.MOUNTAIN: return 1.2
		Type.SWAMP:    return 1.5
		Type.WATER:    return 0.5
		Type.ROAD:     return 0.7
		Type.DESERT:   return 1.0
		_:             return 1.0


## 获取地形显示颜色（用于大地图渲染）
static func get_terrain_color(terrain: Type) -> Color:
	var palette := get_terrain_palette(terrain)
	return palette.base
