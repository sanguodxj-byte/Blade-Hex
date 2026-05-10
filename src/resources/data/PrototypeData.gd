# PrototypeData.gd
# 原型数据注册器 — 在代码中生成所有原型阶段的武器/防具/法术/消耗品数据
# 对应策划案 06/07/08 的原型阶段规格
class_name PrototypeData


# ============================================================================
# 原型武器 (主武器4种 + 副武器3种)
# 对应策划案 06 → 原型阶段
# ============================================================================

static func get_weapons() -> Dictionary:
	var weapons: Dictionary = {}
	
	# 主武器
	var longsword = WeaponData.new()
	longsword.item_name = "长剑"
	longsword.damage_dice_count = 1
	longsword.damage_dice_sides = 8
	longsword.damage_type = WeaponData.DamageType.SLASH
	longsword.category = WeaponData.WeaponCategory.MARTIAL
	longsword.is_dual_wieldable = false
	longsword.range_cells = 1
	weapons["longsword"] = longsword

	var greatsword = WeaponData.new()
	greatsword.item_name = "巨剑"
	greatsword.damage_dice_count = 2
	greatsword.damage_dice_sides = 6
	greatsword.damage_type = WeaponData.DamageType.SLASH
	greatsword.category = WeaponData.WeaponCategory.MARTIAL
	greatsword.is_two_handed = true
	greatsword.is_sweep = true
	greatsword.range_cells = 1
	weapons["greatsword"] = greatsword

	var spear = WeaponData.new()
	spear.item_name = "长枪"
	spear.damage_dice_count = 1
	spear.damage_dice_sides = 10
	spear.damage_type = WeaponData.DamageType.PIERCE
	spear.category = WeaponData.WeaponCategory.MARTIAL
	spear.is_two_handed = true
	spear.is_reach = true
	spear.is_anti_cavalry = true
	spear.range_cells = 2
	weapons["spear"] = spear

	var staff = WeaponData.new()
	staff.item_name = "法杖"
	staff.damage_dice_count = 1
	staff.damage_dice_sides = 6
	staff.damage_type = WeaponData.DamageType.CRUSH
	staff.category = WeaponData.WeaponCategory.SIMPLE
	staff.is_catalyst = true
	staff.range_cells = 1
	weapons["staff"] = staff

	# 副武器
	var longbow = WeaponData.new()
	longbow.item_name = "长弓"
	longbow.damage_dice_count = 1
	longbow.damage_dice_sides = 8
	longbow.damage_type = WeaponData.DamageType.PIERCE
	longbow.category = WeaponData.WeaponCategory.MARTIAL
	longbow.is_ranged = true
	longbow.is_two_handed = true
	longbow.range_cells = 7
	weapons["longbow"] = longbow

	var crossbow = WeaponData.new()
	crossbow.item_name = "十字弩"
	crossbow.damage_dice_count = 1
	crossbow.damage_dice_sides = 10
	crossbow.damage_type = WeaponData.DamageType.PIERCE
	crossbow.category = WeaponData.WeaponCategory.MARTIAL
	crossbow.is_ranged = true
	crossbow.is_two_handed = true
	crossbow.needs_reload = true
	crossbow.is_armor_piercing = true
	crossbow.range_cells = 5
	weapons["crossbow"] = crossbow

	var dagger = WeaponData.new()
	dagger.item_name = "匕首"
	dagger.damage_dice_count = 1
	dagger.damage_dice_sides = 4
	dagger.damage_type = WeaponData.DamageType.PIERCE
	dagger.category = WeaponData.WeaponCategory.SIMPLE
	dagger.is_finesse = true
	dagger.is_dual_wieldable = true
	dagger.is_throwing = true
	dagger.throw_range = 3
	dagger.range_cells = 1
	weapons["dagger"] = dagger

	# 额外武器
	var greataxe = WeaponData.new()
	greataxe.item_name = "巨斧"
	greataxe.damage_dice_count = 1
	greataxe.damage_dice_sides = 12
	greataxe.damage_type = WeaponData.DamageType.SLASH
	greataxe.category = WeaponData.WeaponCategory.MARTIAL
	greataxe.is_two_handed = true
	greataxe.is_armor_piercing = true
	greataxe.range_cells = 1
	weapons["greataxe"] = greataxe

	var shortsword = WeaponData.new()
	shortsword.item_name = "短剑"
	shortsword.damage_dice_count = 1
	shortsword.damage_dice_sides = 6
	shortsword.damage_type = WeaponData.DamageType.PIERCE
	shortsword.category = WeaponData.WeaponCategory.SIMPLE
	shortsword.is_finesse = true
	shortsword.is_dual_wieldable = true
	shortsword.range_cells = 1
	weapons["shortsword"] = shortsword

	var warhammer = WeaponData.new()
	warhammer.item_name = "战锤"
	warhammer.damage_dice_count = 1
	warhammer.damage_dice_sides = 8
	warhammer.damage_type = WeaponData.DamageType.CRUSH
	warhammer.category = WeaponData.WeaponCategory.MARTIAL
	warhammer.is_blunt = true
	warhammer.range_cells = 1
	weapons["warhammer"] = warhammer

	var shortbow = WeaponData.new()
	shortbow.item_name = "短弓"
	shortbow.damage_dice_count = 1
	shortbow.damage_dice_sides = 6
	shortbow.damage_type = WeaponData.DamageType.PIERCE
	shortbow.category = WeaponData.WeaponCategory.SIMPLE
	shortbow.is_ranged = true
	shortbow.is_two_handed = true
	shortbow.range_cells = 4
	weapons["shortbow"] = shortbow

	return weapons


# ============================================================================
# 原型防具 (2种 + 盾牌1种)
# ============================================================================

static func get_armors() -> Dictionary:
	var armors: Dictionary = {}

	var leather = ArmorData.new()
	leather.item_name = "皮甲"
	leather.armor_type = ArmorData.ArmorType.LIGHT
	leather.ac_bonus = 1
	leather.max_dex_bonus = 99
	leather.base_ac_override = 11
	leather.dr_threshold = 6
	leather.max_dr = 60
	armors["leather"] = leather

	var chainmail = ArmorData.new()
	chainmail.item_name = "锁甲"
	chainmail.armor_type = ArmorData.ArmorType.MEDIUM
	chainmail.ac_bonus = 4
	chainmail.max_dex_bonus = 2
	chainmail.base_ac_override = 14
	chainmail.dr_threshold = 11
	chainmail.max_dr = 110
	armors["chainmail"] = chainmail

	var wooden_shield = ArmorData.new()
	wooden_shield.item_name = "木盾"
	wooden_shield.armor_type = ArmorData.ArmorType.SHIELD
	wooden_shield.ac_bonus = 1
	wooden_shield.is_destroyable = true
	wooden_shield.dr_threshold = 3
	wooden_shield.max_dr = 20
	armors["wooden_shield"] = wooden_shield

	var plate = ArmorData.new()
	plate.item_name = "板甲"
	plate.armor_type = ArmorData.ArmorType.HEAVY
	plate.ac_bonus = 0
	plate.max_dex_bonus = 0
	plate.base_ac_override = 17
	plate.str_required = 15
	plate.stealth_disadvantage = true
	plate.movement_penalty = 0
	plate.dr_threshold = 15
	plate.max_dr = 150
	armors["plate"] = plate

	var iron_shield = ArmorData.new()
	iron_shield.item_name = "铁盾"
	iron_shield.armor_type = ArmorData.ArmorType.SHIELD
	iron_shield.ac_bonus = 2
	armors["iron_shield"] = iron_shield

	return armors


# ============================================================================
# 原型法术 (2戏法 + 4个1环 + 2个2环 = 8个)
# 对应策划案 07 → 十、原型阶段
# ============================================================================

static func get_spells() -> Dictionary:
	var spells: Dictionary = {}

	# === 戏法 ===
	var force_orb = SpellData.new()
	force_orb.spell_id = "force_orb"
	force_orb.spell_name = "灵力矢"
	force_orb.description = "发射1发灵力矢，造成1d4+1力场伤害，自动命中"
	force_orb.spell_school = SpellData.SpellSchool.EVOCATION
	force_orb.tier = SpellData.SpellTier.CANTRIP
	force_orb.mana_cost = 0
	force_orb.cooldown_turns = 0
	force_orb.resolution_type = SpellData.ResolutionType.AUTO_HIT
	force_orb.shape = SpellData.SpellShape.SINGLE
	force_orb.range_cells = 6
	force_orb.damage_dice_count = 1
	force_orb.damage_dice_sides = 4
	force_orb.damage_type = "force"
	spells["force_orb"] = force_orb

	var fire_bolt = SpellData.new()
	fire_bolt.spell_id = "fire_bolt"
	fire_bolt.spell_name = "炎矢术"
	fire_bolt.description = "发射火焰箭矢，造成1d10火焰伤害"
	fire_bolt.spell_school = SpellData.SpellSchool.EVOCATION
	fire_bolt.tier = SpellData.SpellTier.CANTRIP
	fire_bolt.mana_cost = 0
	fire_bolt.cooldown_turns = 0
	fire_bolt.resolution_type = SpellData.ResolutionType.ATTACK_ROLL
	fire_bolt.shape = SpellData.SpellShape.SINGLE
	fire_bolt.range_cells = 8
	fire_bolt.damage_dice_count = 1
	fire_bolt.damage_dice_sides = 10
	fire_bolt.damage_type = "fire"
	spells["fire_bolt"] = fire_bolt

	# === 1环法术 ===
	var heal = SpellData.new()
	heal.spell_id = "heal"
	heal.spell_name = "治疗术"
	heal.description = "触碰目标恢复1d8+WIS修正HP"
	heal.spell_school = SpellData.SpellSchool.DIVINATION
	heal.tier = SpellData.SpellTier.TIER_1
	heal.mana_cost = 3
	heal.cooldown_turns = 1
	heal.resolution_type = SpellData.ResolutionType.AUTO_HIT
	heal.shape = SpellData.SpellShape.TOUCH
	heal.range_cells = 1
	heal.heal_dice_count = 1
	heal.heal_dice_sides = 8
	heal.heal_bonus = 0  # WIS修正运行时计算
	spells["heal"] = heal

	var burning_hand = SpellData.new()
	burning_hand.spell_id = "burning_hand"
	burning_hand.spell_name = "灼热之手"
	burning_hand.description = "锥形3格内3d6火焰伤害，DEX豁免半伤"
	burning_hand.spell_school = SpellData.SpellSchool.EVOCATION
	burning_hand.tier = SpellData.SpellTier.TIER_1
	burning_hand.mana_cost = 3
	burning_hand.cooldown_turns = 1
	burning_hand.resolution_type = SpellData.ResolutionType.SAVE
	burning_hand.save_type = SpellData.SaveType.DEX_SAVE
	burning_hand.shape = SpellData.SpellShape.CONE
	burning_hand.shape_size = 3
	burning_hand.range_cells = 0
	burning_hand.damage_dice_count = 3
	burning_hand.damage_dice_sides = 6
	burning_hand.damage_type = "fire"
	spells["burning_hand"] = burning_hand

	var magic_missile = SpellData.new()
	magic_missile.spell_id = "magic_missile"
	magic_missile.spell_name = "魔法飞弹"
	magic_missile.description = "发射3发飞弹，每发1d4+1力场伤害，自动命中"
	magic_missile.spell_school = SpellData.SpellSchool.EVOCATION
	magic_missile.tier = SpellData.SpellTier.TIER_1
	magic_missile.mana_cost = 3
	magic_missile.cooldown_turns = 1
	magic_missile.resolution_type = SpellData.ResolutionType.AUTO_HIT
	magic_missile.shape = SpellData.SpellShape.SINGLE
	magic_missile.range_cells = 10
	magic_missile.damage_dice_count = 3
	magic_missile.damage_dice_sides = 4
	magic_missile.damage_type = "force"
	spells["magic_missile"] = magic_missile

	var shield_spell = SpellData.new()
	shield_spell.spell_id = "shield"
	shield_spell.spell_name = "护盾术"
	shield_spell.description = "自身AC+5，持续1回合"
	shield_spell.spell_school = SpellData.SpellSchool.ABJURATION
	shield_spell.tier = SpellData.SpellTier.TIER_1
	shield_spell.mana_cost = 3
	shield_spell.cooldown_turns = 2
	shield_spell.resolution_type = SpellData.ResolutionType.AUTO_HIT
	shield_spell.shape = SpellData.SpellShape.SELF
	shield_spell.range_cells = 0
	shield_spell.applied_status_effect = "shield"
	shield_spell.status_duration = 1
	spells["shield"] = shield_spell

	# === 2环法术 ===
	var scorching_ray = SpellData.new()
	scorching_ray.spell_id = "scorching_ray"
	scorching_ray.spell_name = "灼炎射线"
	scorching_ray.description = "发射2道射线，每道2d6火焰伤害"
	scorching_ray.spell_school = SpellData.SpellSchool.EVOCATION
	scorching_ray.tier = SpellData.SpellTier.TIER_2
	scorching_ray.mana_cost = 5
	scorching_ray.cooldown_turns = 2
	scorching_ray.resolution_type = SpellData.ResolutionType.ATTACK_ROLL
	scorching_ray.shape = SpellData.SpellShape.RAY
	scorching_ray.shape_size = 8
	scorching_ray.range_cells = 8
	scorching_ray.damage_dice_count = 2
	scorching_ray.damage_dice_sides = 6
	scorching_ray.damage_type = "fire"
	spells["scorching_ray"] = scorching_ray

	var invisibility = SpellData.new()
	invisibility.spell_id = "invisibility"
	invisibility.spell_name = "隐身术"
	invisibility.description = "目标隐身，攻击后解除"
	invisibility.spell_school = SpellData.SpellSchool.ILLUSION
	invisibility.tier = SpellData.SpellTier.TIER_2
	invisibility.mana_cost = 5
	invisibility.cooldown_turns = 5
	invisibility.resolution_type = SpellData.ResolutionType.AUTO_HIT
	invisibility.shape = SpellData.SpellShape.TOUCH
	invisibility.range_cells = 1
	invisibility.applied_status_effect = "invisibility"
	invisibility.status_duration = 99
	spells["invisibility"] = invisibility

	return spells


# ============================================================================
# 原型消耗品 (治疗药水)
# ============================================================================

static func get_consumables() -> Dictionary:
	var items: Dictionary = {}

	var healing_potion = ConsumableData.new()
	healing_potion.item_name = "治疗药水"
	healing_potion.consumable_type = ConsumableData.ConsumableType.HEALING_POTION
	healing_potion.heal_dice_count = 2
	healing_potion.heal_dice_sides = 4
	healing_potion.heal_bonus = 2
	healing_potion.use_action = "main_action"
	items["healing_potion"] = healing_potion

	var strong_healing = ConsumableData.new()
	strong_healing.item_name = "强效治疗药水"
	strong_healing.consumable_type = ConsumableData.ConsumableType.STRONG_HEALING
	strong_healing.heal_dice_count = 4
	strong_healing.heal_dice_sides = 4
	strong_healing.heal_bonus = 4
	strong_healing.use_action = "main_action"
	items["strong_healing_potion"] = strong_healing

	var antidote = ConsumableData.new()
	antidote.item_name = "解毒剂"
	antidote.consumable_type = ConsumableData.ConsumableType.ANTIDOTE
	antidote.removes_status = ["poison"]
	antidote.use_action = "main_action"
	items["antidote"] = antidote

	var fire_oil = ConsumableData.new()
	fire_oil.item_name = "火油瓶"
	fire_oil.consumable_type = ConsumableData.ConsumableType.FIRE_OIL
	fire_oil.damage_dice_count = 1
	fire_oil.damage_dice_sides = 4
	fire_oil.damage_type = "fire"
	fire_oil.aoe_radius = 1
	fire_oil.throw_range = 4
	fire_oil.applied_status = "burning"
	fire_oil.applied_status_duration = 3
	fire_oil.use_action = "main_action"
	items["fire_oil"] = fire_oil

	var holy_water = ConsumableData.new()
	holy_water.item_name = "圣水"
	holy_water.consumable_type = ConsumableData.ConsumableType.HOLY_WATER
	holy_water.damage_dice_count = 2
	holy_water.damage_dice_sides = 6
	holy_water.damage_type = "radiant"
	holy_water.aoe_radius = 1
	holy_water.throw_range = 4
	holy_water.use_action = "main_action"
	items["holy_water"] = holy_water

	return items


# ============================================================================
# 原型敌人 (3种)
# 对应策划案 08 → 原型阶段敌人
# ============================================================================

## 原型敌人（3种基础敌人，120级等级体系）
## 属性由等级 + 权重自动计算，HP由 base_hp + CON修正×等级
## 对应策划案 08 → 原型阶段敌人
static func get_enemies() -> Dictionary:
	var enemies: Dictionary = {}

	# 骷髅战士 (Lv.3, CR 0) — 亡灵炮灰
	var skeleton_data = UnitTemplateDB.instantiate_template(UnitTemplateDB.grunt_skeleton_warrior())
	skeleton_data.primary_main_hand = _create_weapon("锈蚀短剑", 1, 6, WeaponData.DamageType.PIERCE, false, true)
	enemies["skeleton_warrior"] = skeleton_data

	# 哥布林射手 (Lv.2, CR 0) — 远程杂兵
	var goblin_data = UnitTemplateDB.instantiate_template(UnitTemplateDB.grunt_goblin_archer())
	goblin_data.primary_main_hand = _create_ranged_weapon("短弓", 1, 6, 4)
	enemies["goblin_archer"] = goblin_data

	# 兽人狂战 (Lv.9, CR 1) — 力量型人形
	var orc_data = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_orc_berserker())
	orc_data.primary_main_hand = _create_weapon("巨斧", 1, 12, WeaponData.DamageType.SLASH, true, false)
	orc_data.primary_main_hand.is_armor_piercing = true
	enemies["orc_berserker"] = orc_data

	return enemies

## 获取敌人模板库（大量非人类单位）
## 对应策划案 08 → 敌方与AI
# ============================================================================

## 获取扩展敌人模板库（120级等级体系）
## 所有属性由 UnitTemplateDB 等级制模板生成
## 对应策划案 08 → 敌方与AI
static func get_extended_enemies() -> Dictionary:
	var enemies: Dictionary = {}
	
	# ========== 杂兵 (Lv 1~5) ==========
	
	enemies["goblin_warrior"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.grunt_goblin_warrior())
	enemies["forest_wolf"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.grunt_forest_wolf())
	enemies["zombie"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.grunt_zombie())
	enemies["slime"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.grunt_slime())
	enemies["imp"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.grunt_imp())
	enemies["lava_slime"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.grunt_lava_slime())
	
	# ========== 熟练 (Lv 6~17) ==========
	
	enemies["goblin_chieftain"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_goblin_chieftain())
	enemies["giant_spider"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_giant_spider())
	enemies["ghoul"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_ghoul())
	enemies["black_bear"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_black_bear())
	enemies["giant_scorpion"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_giant_scorpion())
	enemies["dire_wolf"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_dire_wolf())
	enemies["harpy"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_harpy())
	enemies["hellhound"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_hellhound())
	enemies["skeleton_archer"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_skeleton_archer())
	enemies["griffin"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_griffin())
	enemies["troll"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.standard_troll())
	
	# ========== 精英 (Lv 18~36) ==========
	
	enemies["ogre"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_ogre())
	enemies["minotaur"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_minotaur())
	enemies["gargoyle"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_gargoyle())
	enemies["corrupted_treant"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_corrupted_treant())
	enemies["lamia"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_lamia())
	enemies["shadow_assassin"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_shadow_assassin())
	enemies["death_knight"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_death_knight())
	enemies["fire_elemental"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_fire_elemental())
	enemies["ice_elemental"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_ice_elemental())
	enemies["demon_guard"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_demon_guard())
	enemies["frost_witch"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_frost_witch())
	enemies["shadow_inquisitor"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_shadow_inquisitor())
	enemies["nightmare_beast"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_nightmare_beast())
	enemies["minotaur_chieftain"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.elite_minotaur_chieftain())
	
	# ========== 构造体 ==========
	
	enemies["wood_sentinel"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.construct_wooden_sentinel())
	enemies["stone_golem"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.construct_stone_golem())
	
	# ========== 龙族 (Lv 78~90) ==========
	
	enemies["young_red_dragon"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.legendary_young_red_dragon())
	enemies["adult_red_dragon"] = UnitTemplateDB.instantiate_template(UnitTemplateDB.legendary_adult_red_dragon())
	
	return enemies


# ============================================================================
# 辅助方法：快速创建武器/防具
# ============================================================================


static func _create_weapon(name: String, dice_count: int, dice_sides: int, dmg_type: int, two_handed: bool, finesse: bool) -> WeaponData:
	var weapon = WeaponData.new()
	weapon.item_name = name
	weapon.damage_dice_count = dice_count
	weapon.damage_dice_sides = dice_sides
	weapon.damage_type = dmg_type
	weapon.is_two_handed = two_handed
	weapon.is_finesse = finesse
	weapon.range_cells = 1
	return weapon


static func _create_ranged_weapon(name: String, dice_count: int, dice_sides: int, range: int) -> WeaponData:
	var weapon = WeaponData.new()
	weapon.item_name = name
	weapon.damage_dice_count = dice_count
	weapon.damage_dice_sides = dice_sides
	weapon.damage_type = WeaponData.DamageType.PIERCE
	weapon.is_ranged = true
	weapon.is_two_handed = true
	weapon.range_cells = range
	return weapon


static func _create_natural_weapon(name: String, dice_count: int, dice_sides: int, bonus: int, dmg_type: int) -> WeaponData:
	var weapon = WeaponData.new()
	weapon.item_name = name
	weapon.damage_dice_count = dice_count
	weapon.damage_dice_sides = dice_sides
	weapon.damage_type = dmg_type
	weapon.bonus_damage = bonus
	weapon.range_cells = 1
	return weapon


static func _create_shield(name: String, ac_bonus: int) -> ArmorData:
	var shield = ArmorData.new()
	shield.item_name = name
	shield.armor_type = ArmorData.ArmorType.SHIELD
	shield.ac_bonus = ac_bonus
	return shield
