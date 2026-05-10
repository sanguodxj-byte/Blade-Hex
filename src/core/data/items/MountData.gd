# MountData.gd
# 坐骑数据资源 — 骑乘系统用
# 对应策划案 06-装备与物品 → 坐骑系统
extends Resource
class_name MountData

# ============================================================================
# 数据字段
# ============================================================================

## 坐骑名称
@export var mount_name: String = "驮马"

## 坐骑唯一ID
@export var mount_id: String = ""

## 速度加成（额外移动格数）
@export var speed_bonus: int = 1

## 坐骑独立HP
@export var max_hp: int = 15

## 负重能力（影响携带量）
@export var carry_capacity: int = 1  # 1=中, 2=高

## 冲锋伤害加成（百分比，0.25 = +25%）
@export var charge_damage_bonus: float = 0.25

## 特殊特性
@export var special_traits: Array[String] = []
# 可能的值: "immune_fear"（免疫恐惧）, "forest_walk"（可穿越森林）,
#           "stealth_no_break"（潜行不中断）, "extra_damage_1d4"（攻击附带1d4）

## 可进入森林
@export var can_forest: bool = true

## 可进入密林
@export var can_dense_forest: bool = false

## 允许的骑射武器ID列表（空=不可骑射）
@export var allowed_mounted_weapons: Array[String] = []
# 示例: ["shortbow", "hand_crossbow"]

## 价格
@export var price: int = 20

## 坐骑描述
@export_multiline var description: String = ""

# ============================================================================
# 预定义坐骑工厂
# 对应策划案 06-装备与物品 → 坐骑类型
# ============================================================================

static func get_all_mounts() -> Array[MountData]:
	var mounts: Array[MountData] = []
	mounts.append(_create_pack_horse())
	mounts.append(_create_war_horse())
	mounts.append(_create_elite_war_horse())
	mounts.append(_create_elf_stag())
	mounts.append(_create_dwarf_war_bear())
	mounts.append(_create_wolf())
	return mounts

static func get_mount_by_id(id: String) -> MountData:
	for m in get_all_mounts():
		if m.mount_id == id:
			return m
	return _create_pack_horse()

static func _create_pack_horse() -> MountData:
	var m = MountData.new()
	m.mount_id = "pack_horse"
	m.mount_name = "驮马"
	m.speed_bonus = 1
	m.max_hp = 15
	m.carry_capacity = 2  # 高
	m.charge_damage_bonus = 0.0  # 无战斗加成
	m.can_forest = true
	m.can_dense_forest = false
	m.price = 20
	m.description = "无战斗加成，纯运输用途。"
	return m

static func _create_war_horse() -> MountData:
	var m = MountData.new()
	m.mount_id = "war_horse"
	m.mount_name = "军马"
	m.speed_bonus = 2
	m.max_hp = 20
	m.carry_capacity = 1
	m.charge_damage_bonus = 0.25
	m.can_forest = true
	m.can_dense_forest = false
	m.allowed_mounted_weapons = ["shortbow", "hand_crossbow"]
	m.price = 80
	m.description = "冲锋伤害+25%，可骑射。"
	return m

static func _create_elite_war_horse() -> MountData:
	var m = MountData.new()
	m.mount_id = "elite_war_horse"
	m.mount_name = "战马"
	m.speed_bonus = 3
	m.max_hp = 25
	m.carry_capacity = 1
	m.charge_damage_bonus = 0.50
	m.special_traits = ["immune_fear"]
	m.can_forest = true
	m.can_dense_forest = false
	m.allowed_mounted_weapons = ["shortbow", "hand_crossbow"]
	m.price = 200
	m.description = "冲锋伤害+50%，免疫恐惧，可骑射。"
	return m

static func _create_elf_stag() -> MountData:
	var m = MountData.new()
	m.mount_id = "elf_stag"
	m.mount_name = "精灵角鹿"
	m.speed_bonus = 2
	m.max_hp = 18
	m.carry_capacity = 0  # 低
	m.charge_damage_bonus = 0.25
	m.special_traits = ["forest_walk", "stealth_no_break"]
	m.can_forest = true
	m.can_dense_forest = true  # 可穿越密林
	m.allowed_mounted_weapons = ["shortbow", "hand_crossbow"]
	m.price = 150
	m.description = "可穿越森林（不减速），潜行不中断，可骑射。"
	return m

static func _create_dwarf_war_bear() -> MountData:
	var m = MountData.new()
	m.mount_id = "dwarf_war_bear"
	m.mount_name = "矮人战熊"
	m.speed_bonus = 1
	m.max_hp = 30
	m.carry_capacity = 2
	m.charge_damage_bonus = 0.25
	m.special_traits = ["immune_fear", "extra_damage_1d4"]
	m.can_forest = true
	m.can_dense_forest = false
	m.price = 250
	m.description = "攻击时附带1d4额外伤害，免疫恐惧。"
	return m

static func _create_wolf() -> MountData:
	var m = MountData.new()
	m.mount_id = "wolf"
	m.mount_name = "狼"
	m.speed_bonus = 2
	m.max_hp = 12
	m.carry_capacity = 0
	m.charge_damage_bonus = 0.25
	m.special_traits = ["flanking_bonus"]
	m.can_forest = true
	m.can_dense_forest = false
	m.allowed_mounted_weapons = ["shortbow", "hand_crossbow"]
	m.price = 60
	m.description = "包夹时额外+1命中，可骑射。"
	return m
