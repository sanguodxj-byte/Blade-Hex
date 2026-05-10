# EquipmentGenerator.gd
# 装备生成器 — 根据稀有度、物品等级和词缀动态生成装备
# 对应策划案 06-装备与物品.md → 装备词缀系统 + 稀有度系统
class_name EquipmentGenerator

# ============================================================================
# 稀有度权重配置（影响随机抽取时的概率）
# ============================================================================

## 默认稀有度权重表（普通→传说）
const RARITY_WEIGHTS: Dictionary = {
	ItemData.Rarity.COMMON: 60.0,
	ItemData.Rarity.UNCOMMON: 25.0,
	ItemData.Rarity.RARE: 10.0,
	ItemData.Rarity.EPIC: 4.0,
	ItemData.Rarity.LEGENDARY: 1.0,
}

## 按区域/难度调整的稀有度权重
const RARITY_WEIGHTS_BY_DIFFICULTY: Dictionary = {
	"easy": { ItemData.Rarity.COMMON: 70.0, ItemData.Rarity.UNCOMMON: 20.0, ItemData.Rarity.RARE: 8.0, ItemData.Rarity.EPIC: 2.0, ItemData.Rarity.LEGENDARY: 0.0 },
	"normal": { ItemData.Rarity.COMMON: 55.0, ItemData.Rarity.UNCOMMON: 28.0, ItemData.Rarity.RARE: 12.0, ItemData.Rarity.EPIC: 4.0, ItemData.Rarity.LEGENDARY: 1.0 },
	"hard": { ItemData.Rarity.COMMON: 40.0, ItemData.Rarity.UNCOMMON: 30.0, ItemData.Rarity.RARE: 18.0, ItemData.Rarity.EPIC: 9.0, ItemData.Rarity.LEGENDARY: 3.0 },
	"nightmare": { ItemData.Rarity.COMMON: 25.0, ItemData.Rarity.UNCOMMON: 30.0, ItemData.Rarity.RARE: 25.0, ItemData.Rarity.EPIC: 14.0, ItemData.Rarity.LEGENDARY: 6.0 },
}


# ============================================================================
# 装备生成
# ============================================================================

## 根据基础模板和目标稀有度生成装备
## base_item: 基础物品模板（如长剑、皮甲等）
## target_rarity: 目标稀有度（-1=随机，按权重抽取）
## item_level: 物品等级（影响词缀范围）
## difficulty: 难度标签（影响稀有度权重）
static func generate_equipment(base_item: ItemData, target_rarity: int = -1, item_level: int = 1, difficulty: String = "normal") -> ItemData:
	# 深拷贝基础物品
	var item = _deep_copy_item(base_item)
	item.item_level = item_level

	# 确定稀有度
	if target_rarity == -1:
		target_rarity = _roll_rarity(difficulty)
	item.rarity = target_rarity

	# 根据稀有度附加词缀
	_apply_random_affixes(item, item_level)

	return item


## 随机生成一件武器
## weapon_pool: 可选武器ID列表（空=所有原型武器）
## target_rarity: 目标稀有度
## item_level: 物品等级
## difficulty: 难度标签
static func generate_random_weapon(weapon_pool: Array[String] = [], target_rarity: int = -1, item_level: int = 1, difficulty: String = "normal") -> WeaponData:
	var all_weapons = PrototypeData.get_weapons()
	var candidates: Array[WeaponData] = []

	if weapon_pool.is_empty():
		for key in all_weapons:
			candidates.append(all_weapons[key])
	else:
		for key in weapon_pool:
			if all_weapons.has(key):
				candidates.append(all_weapons[key])

	if candidates.is_empty():
		candidates.append(all_weapons["longsword"])

	var base = candidates[randi() % candidates.size()]
	var result = generate_equipment(base, target_rarity, item_level, difficulty)
	return result as WeaponData


## 随机生成一件防具
static func generate_random_armor(armor_pool: Array[String] = [], target_rarity: int = -1, item_level: int = 1, difficulty: String = "normal") -> ArmorData:
	var all_armors = PrototypeData.get_armors()
	var candidates: Array[ArmorData] = []

	if armor_pool.is_empty():
		for key in all_armors:
			var a = all_armors[key]
			if a.armor_type != ArmorData.ArmorType.SHIELD:
				candidates.append(a)
	else:
		for key in armor_pool:
			if all_armors.has(key):
				candidates.append(all_armors[key])

	if candidates.is_empty():
		candidates.append(all_armors["leather"])

	var base = candidates[randi() % candidates.size()]
	var result = generate_equipment(base, target_rarity, item_level, difficulty)
	return result as ArmorData


## 随机生成一面盾牌
static func generate_random_shield(target_rarity: int = -1, item_level: int = 1, difficulty: String = "normal") -> ArmorData:
	var all_armors = PrototypeData.get_armors()
	var candidates: Array[ArmorData] = []
	for key in all_armors:
		var a = all_armors[key]
		if a.armor_type == ArmorData.ArmorType.SHIELD:
			candidates.append(a)

	if candidates.is_empty():
		var fallback = ArmorData.new()
		fallback.item_name = "木盾"
		fallback.armor_type = ArmorData.ArmorType.SHIELD
		fallback.ac_bonus = 1
		candidates.append(fallback)

	var base = candidates[randi() % candidates.size()]
	var result = generate_equipment(base, target_rarity, item_level, difficulty)
	return result as ArmorData


## 随机生成一件饰品
static func generate_random_accessory(target_rarity: int = -1, item_level: int = 1, difficulty: String = "normal") -> AccessoryData:
	var all_accessories = AccessoryData.get_all_accessories()
	var base = all_accessories[randi() % all_accessories.size()]
	var result = generate_equipment(base, target_rarity, item_level, difficulty)
	return result as AccessoryData


## 随机生成任意类型装备
static func generate_random_any(item_level: int = 1, difficulty: String = "normal") -> ItemData:
	var roll = randf()
	if roll < 0.45:
		return generate_random_weapon([], -1, item_level, difficulty)
	elif roll < 0.75:
		return generate_random_armor([], -1, item_level, difficulty)
	elif roll < 0.85:
		return generate_random_shield(-1, item_level, difficulty)
	else:
		return generate_random_accessory(-1, item_level, difficulty)


# ============================================================================
# 稀有度抽取
# ============================================================================

## 根据难度权重随机抽取稀有度
static func _roll_rarity(difficulty: String = "normal") -> int:
	var weights = RARITY_WEIGHTS_BY_DIFFICULTY.get(difficulty, RARITY_WEIGHTS)
	var total = 0.0
	for w in weights.values():
		total += w

	var roll = randf() * total
	var cumulative = 0.0
	var rarities = [ItemData.Rarity.COMMON, ItemData.Rarity.UNCOMMON, ItemData.Rarity.RARE, ItemData.Rarity.EPIC, ItemData.Rarity.LEGENDARY]

	for r in rarities:
		cumulative += weights.get(r, 0.0)
		if roll <= cumulative:
			return r

	return ItemData.Rarity.COMMON


## 根据CR确定物品等级范围
static func get_item_level_from_cr(cr: float) -> int:
	if cr <= 0.25: return randi_range(1, 2)
	elif cr <= 0.5: return randi_range(1, 3)
	elif cr <= 1.0: return randi_range(2, 4)
	elif cr <= 2.0: return randi_range(3, 6)
	elif cr <= 5.0: return randi_range(5, 10)
	elif cr <= 10.0: return randi_range(8, 15)
	else: return randi_range(12, 20)


## 根据CR确定难度标签
static func get_difficulty_from_cr(cr: float) -> String:
	if cr <= 0.5: return "easy"
	elif cr <= 2.0: return "normal"
	elif cr <= 5.0: return "hard"
	else: return "nightmare"


# ============================================================================
# 词缀附加
# ============================================================================

## 根据稀有度给物品附加随机词缀
static func _apply_random_affixes(item: ItemData, item_level: int) -> void:
	var max_affixes = item.get_max_affix_count()
	if max_affixes == 0:
		return

	# 确定词缀目标类型
	var affix_target = EquipmentAffix.AffixTarget.ANY
	if item is WeaponData:
		affix_target = EquipmentAffix.AffixTarget.WEAPON
	elif item is ArmorData:
		var armor = item as ArmorData
		if armor.armor_type == ArmorData.ArmorType.SHIELD:
			affix_target = EquipmentAffix.AffixTarget.SHIELD
		else:
			affix_target = EquipmentAffix.AffixTarget.ARMOR
	elif item is AccessoryData:
		affix_target = EquipmentAffix.AffixTarget.ACCESSORY

	# 获取可用词缀池
	var available = EquipmentAffix.get_affixes_for_target(affix_target, item_level, item.rarity)
	if available.is_empty():
		return

	# 分离前缀和后缀
	var prefixes: Array[EquipmentAffix] = []
	var suffixes: Array[EquipmentAffix] = []
	for a in available:
		if a.is_prefix:
			prefixes.append(a)
		else:
			suffixes.append(a)

	# 先选一个前缀（如果有）
	if not prefixes.is_empty() and max_affixes > 0:
		var chosen = _weighted_random_affix(prefixes)
		if chosen:
			item.add_affix(chosen)
			max_affixes -= 1

	# 再选后缀填满剩余词缀位
	while max_affixes > 0 and not suffixes.is_empty():
		var chosen = _weighted_random_affix(suffixes)
		if chosen:
			item.add_affix(chosen)
			# 避免重复，移除已选项
			suffixes.erase(chosen)
			max_affixes -= 1
		else:
			break


# ============================================================================
# 工具方法
# ============================================================================

## 加权随机选择词缀
static func _weighted_random_affix(pool: Array[EquipmentAffix]) -> EquipmentAffix:
	if pool.is_empty():
		return null

	var total = 0.0
	for a in pool:
		total += a.weight

	var roll = randf() * total
	var cumulative = 0.0
	for a in pool:
		cumulative += a.weight
		if roll <= cumulative:
			return a

	return pool[pool.size() - 1]


## 深拷贝物品
static func _deep_copy_item(item: ItemData) -> ItemData:
	if item is WeaponData:
		return _copy_weapon(item as WeaponData)
	elif item is ArmorData:
		return _copy_armor(item as ArmorData)
	elif item is AccessoryData:
		return _copy_accessory(item as AccessoryData)
	elif item is ConsumableData:
		return _copy_consumable(item as ConsumableData)
	else:
		var copy = ItemData.new()
		copy.item_id = item.item_id
		copy.item_name = item.item_name
		copy.description = item.description
		copy.icon = item.icon
		copy.weight = item.weight
		copy.price = item.price
		return copy


static func _copy_weapon(src: WeaponData) -> WeaponData:
	var w = WeaponData.new()
	# ItemData 基础字段
	w.item_id = src.item_id
	w.item_name = src.item_name
	w.description = src.description
	w.icon = src.icon
	w.weight = src.weight
	w.price = src.price
	# WeaponData 字段
	w.damage_dice_count = src.damage_dice_count
	w.damage_dice_sides = src.damage_dice_sides
	w.damage_type = src.damage_type
	w.category = src.category
	w.is_two_handed = src.is_two_handed
	w.is_finesse = src.is_finesse
	w.is_ranged = src.is_ranged
	w.range_cells = src.range_cells
	w.is_throwing = src.is_throwing
	w.throw_range = src.throw_range
	w.needs_reload = src.needs_reload
	w.is_blunt = src.is_blunt
	w.is_armor_piercing = src.is_armor_piercing
	w.is_reach = src.is_reach
	w.is_anti_cavalry = src.is_anti_cavalry
	w.is_sweep = src.is_sweep
	w.str_required = src.str_required
	w.is_catalyst = src.is_catalyst
	w.spell_dc_bonus = src.spell_dc_bonus
	w.is_dual_wieldable = src.is_dual_wieldable
	return w


static func _copy_armor(src: ArmorData) -> ArmorData:
	var a = ArmorData.new()
	a.item_id = src.item_id
	a.item_name = src.item_name
	a.description = src.description
	a.icon = src.icon
	a.weight = src.weight
	a.price = src.price
	a.armor_type = src.armor_type
	a.ac_bonus = src.ac_bonus
	a.max_dex_bonus = src.max_dex_bonus
	a.movement_penalty = src.movement_penalty
	a.str_required = src.str_required
	a.stealth_disadvantage = src.stealth_disadvantage
	a.is_destroyable = src.is_destroyable
	a.base_ac_override = src.base_ac_override
	return a


static func _copy_accessory(src: AccessoryData) -> AccessoryData:
	var a = AccessoryData.new()
	a.item_id = src.item_id
	a.item_name = src.item_name
	a.description = src.description
	a.icon = src.icon
	a.weight = src.weight
	a.price = src.price
	a.accessory_type = src.accessory_type
	a.str_bonus = src.str_bonus
	a.dex_bonus = src.dex_bonus
	a.con_bonus = src.con_bonus
	a.int_bonus = src.int_bonus
	a.wis_bonus = src.wis_bonus
	a.cha_bonus = src.cha_bonus
	a.hp_bonus = src.hp_bonus
	a.ac_bonus = src.ac_bonus
	a.move_bonus = src.move_bonus
	a.initiative_bonus = src.initiative_bonus
	a.resistance = src.resistance
	a.immunity = src.immunity
	a.special_effect = src.special_effect
	a.special_value = src.special_value
	return a


static func _copy_consumable(src: ConsumableData) -> ConsumableData:
	var c = ConsumableData.new()
	c.item_id = src.item_id
	c.item_name = src.item_name
	c.description = src.description
	c.icon = src.icon
	c.weight = src.weight
	c.price = src.price
	c.consumable_type = src.consumable_type
	c.heal_dice_count = src.heal_dice_count
	c.heal_dice_sides = src.heal_dice_sides
	c.heal_bonus = src.heal_bonus
	c.removes_status = src.removes_status.duplicate()
	c.damage_dice_count = src.damage_dice_count
	c.damage_dice_sides = src.damage_dice_sides
	c.damage_type = src.damage_type
	c.aoe_radius = src.aoe_radius
	c.throw_range = src.throw_range
	c.linked_spell = src.linked_spell
	c.use_action = src.use_action
	c.usable_outside_combat = src.usable_outside_combat
	c.applied_status = src.applied_status
	c.applied_status_duration = src.applied_status_duration
	return c