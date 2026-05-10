# EquipmentManager.gd
# 装备管理器 — 动态单位类型判定、双持/双手规则、盾牌、坐骑管理
# 对应策划案 06-装备与物品.md
class_name EquipmentManager

# ============================================================================
# 动态单位类型判定
# ============================================================================

## 单位类型枚举
enum UnitType {
	MELEE,        # 近战单位
	RANGED,       # 远程单位
	CAVALRY,      # 骑兵
	MAGE,         # 法术单位
	DUAL_WIELD,   # 双持单位
	SHIELD_FIGHTER, # 盾战士
	MIXED,        # 混合型（近战+远程）
}

## 根据当前装备动态判定单位类型
static func get_unit_type(unit: Unit) -> int:
	if not unit.data:
		return UnitType.MELEE

	var weapon = unit.get_main_hand()
	var is_mounted = unit.data.is_mounted
	var has_shield = _has_shield(unit)
	var has_spell_nodes = unit.data.known_spells.size() > 0

	# 有坐骑 = 骑兵（优先判定）
	if is_mounted:
		return UnitType.CAVALRY

	# 有法术触媒 + 有法术 = 法术单位
	if weapon and weapon.is_catalyst and has_spell_nodes:
		return UnitType.MAGE

	# 远程武器
	if weapon and weapon.is_ranged:
		return UnitType.RANGED

	# 双持
	if _is_dual_wielding(unit):
		return UnitType.DUAL_WIELD

	# 盾战士
	if has_shield:
		return UnitType.SHIELD_FIGHTER

	return UnitType.MELEE

## 获取单位类型显示名
static func get_unit_type_name(unit: Unit) -> String:
	match get_unit_type(unit):
		UnitType.MELEE: return "近战"
		UnitType.RANGED: return "远程"
		UnitType.CAVALRY: return "骑兵"
		UnitType.MAGE: return "法师"
		UnitType.DUAL_WIELD: return "双持"
		UnitType.SHIELD_FIGHTER: return "盾战士"
		UnitType.MIXED: return "混合"
		_: return "近战"


# ============================================================================
# 装备判定
# ============================================================================

## 是否装备了双手武器
static func is_two_handed_equipped(unit: Unit) -> bool:
	var weapon = unit.get_main_hand()
	return weapon != null and weapon.is_two_handed

## 是否装备了盾牌
static func _has_shield(unit: Unit) -> bool:
	var off_hand = unit.get_off_hand()
	if off_hand is ArmorData and off_hand.armor_type == ArmorData.ArmorType.SHIELD:
		return true
	if unit.data.armor and unit.data.armor.armor_type == ArmorData.ArmorType.SHIELD:
		return true
	return false

## 是否双持
static func _is_dual_wielding(unit: Unit) -> bool:
	var main = unit.get_main_hand()
	var off = unit.get_off_hand()
	if main and off is WeaponData:
		return main.is_dual_wieldable and not main.is_two_handed
	return false

## 是否可以装备指定物品到指定槽位
static func can_equip(unit: Unit, item: ItemData, slot: String) -> bool:
	# 力量需求检查
	if item is WeaponData:
		if item.str_required > 0 and unit.data.str < item.str_required:
			return false
	if item is ArmorData:
		if item.str_required > 0 and unit.data.str < item.str_required:
			return false
	# 双手武器冲突检查
	if item is WeaponData and item.is_two_handed:
		if slot == "main_hand":
			# 双手武器不能同时带副武器
			pass  # 由equip_item处理
	return true


# ============================================================================
# 武器切换
# ============================================================================

## 切换武器组
static func switch_weapon_set(unit: Unit):
	unit.using_primary_weapon = !unit.using_primary_weapon


# ============================================================================
# 坐骑管理
# ============================================================================

## 上马
static func mount_unit(unit: Unit, mount: MountData):
	if not unit.data:
		return
	unit.data.mount = mount
	unit.data.is_mounted = true
	unit.data.mount_current_hp = mount.max_hp

## 下马
static func dismount_unit(unit: Unit):
	if not unit.data:
		return
	unit.data.is_mounted = false
	# 坐骑保留在装备槽，只是不骑乘

## 坐骑受伤
static func mount_take_damage(unit: Unit, damage: int) -> bool:
	## 返回true=坐骑死亡
	if not unit.data.is_mounted or not unit.data.mount:
		return false
	unit.data.mount_current_hp -= damage
	if unit.data.mount_current_hp <= 0:
		# 坐骑死亡：骑手落地，受1d6伤害
		unit.data.mount_current_hp = 0
		unit.data.is_mounted = false
		var fall_damage = RPGRuleEngine.roll_dice(1, 6)
		unit.take_damage(fall_damage)
		return true
	return false


# ============================================================================
# 装备属性计算
# ============================================================================

## 获取攻击范围
static func get_attack_range(unit: Unit) -> int:
	var weapon = unit.get_main_hand()
	if not weapon:
		return 1
	if weapon.is_reach:
		return 2
	return weapon.range_cells

## 获取武器特性字典
static func get_weapon_traits(unit: Unit) -> Dictionary:
	var weapon = unit.get_main_hand()
	if not weapon:
		return {}
	var traits: Dictionary = {}
	if weapon.is_two_handed: traits["two_handed"] = true
	if weapon.is_finesse: traits["finesse"] = true
	if weapon.is_ranged: traits["ranged"] = true
	if weapon.is_throwing: traits["throwing"] = true
	if weapon.needs_reload: traits["reload"] = true
	if weapon.is_blunt: traits["blunt"] = true
	if weapon.is_armor_piercing: traits["armor_piercing"] = true
	if weapon.is_reach: traits["reach"] = true
	if weapon.is_anti_cavalry: traits["anti_cavalry"] = true
	if weapon.is_sweep: traits["sweep"] = true
	if weapon.is_catalyst: traits["catalyst"] = true
	if weapon.is_dual_wieldable: traits["dual_wieldable"] = true
	return traits

## 从装备计算AC
static func get_ac_from_equipment(unit: Unit) -> int:
	if not unit.data:
		return 10

	var ac = unit.data.base_ac
	var dex_mod = RPGRuleEngine.get_stat_modifier(unit.data.dex)

	# 防具加成
	if unit.data.armor:
		var armor = unit.data.armor
		if armor.base_ac_override >= 0:
			# 固定AC（重甲）
			ac = armor.base_ac_override
		else:
			# 基础AC + DEX（轻/中甲）
			ac = 10 + armor.ac_bonus
			# 中甲限制DEX加成
			dex_mod = mini(dex_mod, armor.max_dex_bonus)

	# 盾牌加成
	var off_hand = unit.get_off_hand()
	if off_hand is ArmorData and off_hand.armor_type == ArmorData.ArmorType.SHIELD:
		ac += off_hand.ac_bonus

	return ac + dex_mod
