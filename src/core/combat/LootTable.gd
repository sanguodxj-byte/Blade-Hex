# LootTable.gd
# 战利品表系统 — 根据敌人CR和类型生成掉落物品
# 对应策划案 06-装备与物品.md + 08-敌方与AI.md
class_name LootTable

# ============================================================================
# 战利品类型枚举
# ============================================================================

enum LootType {
	WEAPON,
	ARMOR,
	SHIELD,
	ACCESSORY,
	CONSUMABLE,
	GOLD,
}

# ============================================================================
# 战利品表配置
# ============================================================================

## 基础掉落概率（按CR调整）
const BASE_DROP_CHANCE: Dictionary = {
	"weapon": 0.15,
	"armor": 0.12,
	"shield": 0.08,
	"accessory": 0.10,
	"consumable": 0.30,
	"gold": 0.80,
}

## CR倍率（CR越高，掉落概率越高）
const CR_MULTIPLIER: Dictionary = {
	0.125: 0.5,
	0.25: 0.7,
	0.5: 0.9,
	1.0: 1.0,
	2.0: 1.2,
	3.0: 1.4,
	5.0: 1.6,
	8.0: 1.8,
	10.0: 2.0,
	13.0: 2.5,
	15.0: 3.0,
	20.0: 4.0,
}

## 敌人类型掉落偏好
const ENEMY_TYPE_LOOT_BIAS: Dictionary = {
	UnitData.EnemyType.HUMANOID: {
		"weapon": 1.5,
		"armor": 1.3,
		"shield": 1.2,
		"accessory": 0.8,
		"consumable": 1.0,
		"gold": 1.2,
	},
	UnitData.EnemyType.BEAST: {
		"weapon": 0.0,
		"armor": 0.0,
		"shield": 0.0,
		"accessory": 0.3,
		"consumable": 0.5,
		"gold": 0.2,
	},
	UnitData.EnemyType.UNDEAD: {
		"weapon": 0.8,
		"armor": 0.7,
		"shield": 0.5,
		"accessory": 1.2,
		"consumable": 0.3,
		"gold": 0.5,
	},
	UnitData.EnemyType.DEMON: {
		"weapon": 1.0,
		"armor": 0.8,
		"shield": 0.6,
		"accessory": 1.5,
		"consumable": 0.8,
		"gold": 1.0,
	},
	UnitData.EnemyType.GIANT: {
		"weapon": 1.2,
		"armor": 1.0,
		"shield": 0.8,
		"accessory": 0.5,
		"consumable": 0.7,
		"gold": 1.5,
	},
	UnitData.EnemyType.CONSTRUCT: {
		"weapon": 0.3,
		"armor": 0.5,
		"shield": 0.4,
		"accessory": 1.8,
		"consumable": 0.0,
		"gold": 0.3,
	},
	UnitData.EnemyType.DRAGON: {
		"weapon": 1.5,
		"armor": 1.5,
		"shield": 1.0,
		"accessory": 2.0,
		"consumable": 1.0,
		"gold": 3.0,
	},
	UnitData.EnemyType.LEGENDARY: {
		"weapon": 2.0,
		"armor": 2.0,
		"shield": 1.5,
		"accessory": 2.5,
		"consumable": 1.5,
		"gold": 5.0,
	},
}

# ============================================================================
# 战利品生成
# ============================================================================

## 生成敌人死亡掉落
## enemy_data: 敌人UnitData
## 返回: Array[ItemData] 掉落物品列表
static func generate_loot(enemy_data: UnitData) -> Array[ItemData]:
	var loot: Array[ItemData] = []
	
	if not enemy_data or not enemy_data.is_enemy:
		return loot
	
	var cr = enemy_data.threat_level
	var enemy_type = enemy_data.enemy_type
	
	# 获取CR倍率
	var cr_mult = _get_cr_multiplier(cr)
	
	# 获取敌人类型偏好
	var type_bias = ENEMY_TYPE_LOOT_BIAS.get(enemy_type, {})
	
	# 掉落金币
	if randf() < BASE_DROP_CHANCE["gold"] * cr_mult * type_bias.get("gold", 1.0):
		var gold_amount = _roll_gold_amount(cr)
		# 金币作为特殊物品（可以用ConsumableData或单独的GoldData）
		# 这里简化处理，返回描述
		var gold_item = ItemData.new()
		gold_item.item_name = "金币"
		gold_item.description = "%d 金币" % gold_amount
		gold_item.price = gold_amount
		loot.append(gold_item)
	
	# 掉落装备
	if randf() < BASE_DROP_CHANCE["weapon"] * cr_mult * type_bias.get("weapon", 1.0):
		var weapon = _generate_weapon_loot(cr, enemy_type)
		if weapon:
			loot.append(weapon)
	
	if randf() < BASE_DROP_CHANCE["armor"] * cr_mult * type_bias.get("armor", 1.0):
		var armor = _generate_armor_loot(cr, enemy_type)
		if armor:
			loot.append(armor)
	
	if randf() < BASE_DROP_CHANCE["shield"] * cr_mult * type_bias.get("shield", 1.0):
		var shield = _generate_shield_loot(cr, enemy_type)
		if shield:
			loot.append(shield)
	
	if randf() < BASE_DROP_CHANCE["accessory"] * cr_mult * type_bias.get("accessory", 1.0):
		var accessory = _generate_accessory_loot(cr, enemy_type)
		if accessory:
			loot.append(accessory)
	
	# 掉落消耗品
	if randf() < BASE_DROP_CHANCE["consumable"] * cr_mult * type_bias.get("consumable", 1.0):
		var consumable = _generate_consumable_loot(cr, enemy_type)
		if consumable:
			loot.append(consumable)
	
	# 唯一掉落（传说级敌人）
	if enemy_data.unique_drop_id != "":
		var unique = _get_unique_drop(enemy_data.unique_drop_id)
		if unique:
			loot.append(unique)
	
	return loot


## 获取CR倍率
static func _get_cr_multiplier(cr: float) -> float:
	# 找到最接近的CR档位
	var closest_cr = 1.0
	var min_diff = abs(cr - 1.0)
	for key in CR_MULTIPLIER.keys():
		var diff = abs(cr - key)
		if diff < min_diff:
			min_diff = diff
			closest_cr = key
	return CR_MULTIPLIER.get(closest_cr, 1.0)


## 掷金币数量
static func _roll_gold_amount(cr: float) -> int:
	if cr <= 0.25:
		return randi_range(1, 10)
	elif cr <= 0.5:
		return randi_range(5, 20)
	elif cr <= 1.0:
		return randi_range(10, 50)
	elif cr <= 2.0:
		return randi_range(20, 100)
	elif cr <= 5.0:
		return randi_range(50, 200)
	elif cr <= 10.0:
		return randi_range(100, 500)
	elif cr <= 15.0:
		return randi_range(200, 1000)
	else:
		return randi_range(500, 2000)


## 生成武器掉落
static func _generate_weapon_loot(cr: float, _enemy_type: int) -> WeaponData:
	var item_level = EquipmentGenerator.get_item_level_from_cr(cr)
	var difficulty = EquipmentGenerator.get_difficulty_from_cr(cr)
	return EquipmentGenerator.generate_random_weapon([], -1, item_level, difficulty)


## 生成防具掉落
static func _generate_armor_loot(cr: float, _enemy_type: int) -> ArmorData:
	var item_level = EquipmentGenerator.get_item_level_from_cr(cr)
	var difficulty = EquipmentGenerator.get_difficulty_from_cr(cr)
	return EquipmentGenerator.generate_random_armor([], -1, item_level, difficulty)


## 生成盾牌掉落
static func _generate_shield_loot(cr: float, _enemy_type: int) -> ArmorData:
	var item_level = EquipmentGenerator.get_item_level_from_cr(cr)
	var difficulty = EquipmentGenerator.get_difficulty_from_cr(cr)
	return EquipmentGenerator.generate_random_shield(-1, item_level, difficulty)


## 生成饰品掉落
static func _generate_accessory_loot(cr: float, _enemy_type: int) -> AccessoryData:
	var item_level = EquipmentGenerator.get_item_level_from_cr(cr)
	var difficulty = EquipmentGenerator.get_difficulty_from_cr(cr)
	return EquipmentGenerator.generate_random_accessory(-1, item_level, difficulty)


## 生成消耗品掉落
static func _generate_consumable_loot(cr: float, _enemy_type: int) -> ConsumableData:
	var consumables = PrototypeData.get_consumables()
	var keys = consumables.keys()
	if keys.is_empty():
		return null
	
	# 根据CR调整掉落类型
	var roll = randf()
	if cr >= 5.0 and roll < 0.3:
		# 高CR敌人更可能掉落强效药水
		if consumables.has("strong_healing_potion"):
			return EquipmentGenerator._copy_consumable(consumables["strong_healing_potion"])
	
	# 随机掉落
	var key = keys[randi() % keys.size()]
	return EquipmentGenerator._copy_consumable(consumables[key])


## 获取唯一掉落物品
static func _get_unique_drop(unique_id: String) -> ItemData:
	# 这里可以扩展为从配置文件或数据库读取唯一物品
	# 暂时返回null，由具体实现扩展
	match unique_id:
		"dragon_scale_armor":
			var armor = ArmorData.new()
			armor.item_id = "dragon_scale_armor"
			armor.item_name = "龙鳞甲"
			armor.armor_type = ArmorData.ArmorType.HEAVY
			armor.ac_bonus = 8
			armor.base_ac_override = 18
			armor.rarity = ItemData.Rarity.LEGENDARY
			armor.is_unique = true
			armor.description = "由真龙鳞片锻造的传说护甲，提供极强的防护。"
			return armor
		"demon_slayer_sword":
			var weapon = WeaponData.new()
			weapon.item_id = "demon_slayer_sword"
			weapon.item_name = "屠魔剑"
			weapon.damage_dice_count = 1
			weapon.damage_dice_sides = 8
			weapon.damage_type = WeaponData.DamageType.SLASH
			weapon.rarity = ItemData.Rarity.LEGENDARY
			weapon.is_unique = true
			weapon.description = "对魔物造成额外2d6伤害的传说之剑。"
			return weapon
		_:
			return null


# ============================================================================
# 战利品表查询
# ============================================================================

## 根据区域/难度获取可能的掉落池
static func get_loot_pool_for_area(_area_name: String, min_cr: float, max_cr: float):
	var pool: Array[ItemData] = []
	
	# 根据区域特性生成掉落池
	# 这里可以扩展为从配置文件读取
	var avg_cr = (min_cr + max_cr) / 2.0
	var item_level = EquipmentGenerator.get_item_level_from_cr(avg_cr)
	var difficulty = EquipmentGenerator.get_difficulty_from_cr(avg_cr)
	
	# 生成一些示例物品
	for i in range(10):
		pool.append(EquipmentGenerator.generate_random_any(item_level, difficulty))
	
	return pool


## 从战利品池中随机抽取
static func roll_from_pool(pool: Array[ItemData], count: int = 1) -> Array[ItemData]:
	var result: Array[ItemData] = []
	if pool.is_empty():
		return result
	
	for i in range(count):
		var item = pool[randi() % pool.size()]
		result.append(item)
	
	return result
