# CombatResolver.gd
# 战斗结算器 — 集中处理攻击解析、所有战斗修正叠加（纯静态工具类）
class_name CombatResolver


# ============================================================================
# 注意：此类为纯静态工具类，不使用信号。
# 调用方负责根据返回的 result 字典触发 UI 更新和日志。
# ============================================================================

# ============================================================================
# 主攻击解析
# ============================================================================

## 完整攻击结算管道
static func resolve_attack(attacker: Unit, defender: Unit, grid: HexGrid = null, is_charge: bool = false, is_aoo: bool = false) -> Dictionary:
	var result: Dictionary = {
		"attacker": attacker,
		"defender": defender,
		"hit": false,
		"critical": false,
		"fumble": false,
		"graze": false,
		"damage": 0,
		"roll": 0,
		"attack_bonus": 0,
		"total_attack": 0,
		"target_ac": 0,
		"modifiers": {},
		"advantage": false,
		"disadvantage": false,
		"is_counter": false,
		"is_flanking": false,
		"flank_direction": "front",
		"is_charge": is_charge,
	}

	# ===== 1. 攻击加成 =====
	var attack_bonus = attacker.get_attack_bonus()
	result["attack_bonus"] = attack_bonus

	# ===== 2. 优劣势判定 =====
	var has_advantage = false
	var has_disadvantage = false

	# 高地优势
	if grid:
		var high_ground = LineOfSight.get_high_ground_bonus(attacker.grid_pos, defender.grid_pos, grid)
		if high_ground["advantage"]:
			has_advantage = true
			result["modifiers"]["high_ground"] = true
		if high_ground["disadvantage"]:
			has_disadvantage = true
			result["modifiers"]["low_ground"] = true

	# 冲锋优势
	if is_charge:
		has_advantage = true
		result["modifiers"]["charge"] = true

	# 士气效果
	var morale_effects = MoraleSystem.get_morale_effects(attacker)
	if morale_effects["fumble_rate"] > 0:
		has_disadvantage = true
		result["modifiers"]["low_morale"] = true

	# 掩体惩罚（远程攻击时）
	var weapon = attacker.get_main_hand()
	if weapon and weapon.is_ranged and grid:
		var cover = LineOfSight.get_cover_level(defender.grid_pos, attacker.grid_pos, grid)
		if cover >= 2:
			pass
		elif cover == 1:
			result["modifiers"]["half_cover"] = true

	# 渡河惩罚
	if grid and LineOfSight.has_river_crossing_penalty(attacker.grid_pos, defender.grid_pos, grid):
		has_disadvantage = true
		result["modifiers"]["river_crossing"] = true

	# 优势劣势互相抵消
	if has_advantage and has_disadvantage:
		has_advantage = false
		has_disadvantage = false

	result["advantage"] = has_advantage
	result["disadvantage"] = has_disadvantage

	# ===== 3. 掷攻击检定 =====
	var roll: int
	if has_advantage:
		roll = RPGRuleEngine.roll_with_advantage()["result"]
	elif has_disadvantage:
		roll = RPGRuleEngine.roll_with_disadvantage()["result"]
	else:
		roll = RPGRuleEngine.roll_d20()

	result["roll"] = roll

	# ===== 4. 目标AC =====
	var target_ac = defender.get_effective_ac(attacker, grid)

	if result["modifiers"].get("half_cover", false):
		target_ac += 2

	result["target_ac"] = target_ac

	var total_attack = roll + attack_bonus
	result["total_attack"] = total_attack
	
	# 命中率百分比（供UI显示，不暴露内部算法）
	var hit_pct = RPGRuleEngine.calculate_hit_chance(attack_bonus, target_ac, has_advantage, has_disadvantage)
	result["hit_chance_percent"] = roundi(hit_pct * 100.0)

	# ===== 5. 命中判定 =====
	# d20 单骰 vs AC
	# 暴击：d20 >= 攻击者暴击阈值（WIS降低需求）
	# 擦伤：未命中但差值≤2时，造成50%伤害
	var attacker_crit = attacker.get_crit_threshold()
	var is_crit = (roll >= attacker_crit)
	var is_fumble = (roll == 1)
	var is_hit = is_crit or (not is_fumble and total_attack >= target_ac)
	
	# 擦伤机制
	var is_graze = false
	if not is_hit and not is_fumble:
		var miss_by = target_ac - total_attack
		if miss_by <= 2:
			is_graze = true
			is_hit = true
			result["graze"] = true

	result["critical"] = is_crit

	if is_fumble:
		return result

	if not is_hit:
		return result

	# ===== 6. 伤害计算 =====
	var damage_info = attacker.roll_damage()
	var damage = damage_info["total"]
	
	# 擦伤伤害减半
	if is_graze:
		damage = maxi(1, damage / 2)

	# 暴击翻倍（受防御者WIS暴击伤害减免）
	if is_crit:
		var crit_mult = SkillEffectExecutor.get_crit_multiplier(attacker)
		damage *= crit_mult
		var crit_reduction = defender.get_crit_damage_taken_multiplier()
		damage = maxi(1, int(damage * crit_reduction))

	# 偷袭额外伤害
	var sneak_dice = SkillEffectExecutor.get_sneak_attack_dice(attacker, result.get("advantage", false))
	if sneak_dice > 0:
		damage += RPGRuleEngine.roll_dice(sneak_dice, SkillEffectExecutor.get_sneak_attack_sides())

	# 被动近战伤害加成
	if not attacker.get_main_hand() or not attacker.get_main_hand().is_ranged:
		damage += SkillEffectExecutor.get_passive_melee_damage_bonus(attacker)
		var melee_mult = SkillEffectExecutor.get_passive_melee_damage_multiplier(attacker)
		damage = int(damage * melee_mult)

	# 包夹加成
	if not is_aoo:
		var flank_bonus = FacingSystem.get_flanking_bonus(attacker.grid_pos, defender)
		damage = int(damage * flank_bonus["damage_multiplier"])
		if flank_bonus["damage_multiplier"] > 1.0:
			result["is_flanking"] = true
			result["flank_direction"] = "flank" if flank_bonus["damage_multiplier"] < 1.5 else "rear"

	# 冲锋加成
	if is_charge:
		var charge = FacingSystem.get_charge_bonus(attacker, true)
		damage = int(damage * charge["damage_multiplier"])

	# 骑乘加成
	if attacker.data.is_mounted:
		damage += 2

	# 确保最低伤害为1
	damage = maxi(1, damage)
	
	# ===== 6.5 被动伤害减免 =====
	var damage_reduction = SkillEffectExecutor.get_passive_damage_reduction(defender, "physical")
	damage = maxi(1, damage - damage_reduction)
	if damage_reduction > 0:
		result["damage_reduction"] = damage_reduction
	
	# ===== 6.6 装甲穿透结算 =====
	damage = _resolve_armor_penetration(damage, roll, attacker, defender, result)
	
	result["damage"] = damage

	# ===== 7. 应用伤害 =====
	if damage >= defender.current_hp and SkillEffectExecutor.has_death_save(defender):
		if SkillEffectExecutor.roll_death_save(defender):
			damage = maxi(0, defender.current_hp - 1)
			result["death_saved"] = true
	
	defender.take_damage(damage)

	return result


# ============================================================================
# 装甲穿透系统
# d20 自然骰 >= 目标DR阈值 → 穿透装甲
# 未穿透：伤害全打装甲耐久（钝伤始终穿透10%HP）
# 穿透：伤害按砍/刺/钝类型分配给HP和装甲
# ============================================================================

static func _resolve_armor_penetration(damage: int, roll: int, attacker: Unit, defender: Unit, result: Dictionary) -> int:
	var dr_threshold = defender.get_dr_threshold()
	if dr_threshold <= 0:
		return damage  # 无装甲或已损毁，直接打HP
	
	# 获取武器伤害类型
	var weapon = attacker.get_main_hand()
	var dmg_type = WeaponData.DamageType.SLASH  # 默认砍伤
	if weapon:
		dmg_type = weapon.damage_type
	
	# 穿透判定：d20自然骰 >= DR阈值
	var penetrated = (roll >= dr_threshold)
	
	# 钝伤始终穿透10%HP
	var is_crush = (dmg_type == WeaponData.DamageType.CRUSH)
	if is_crush and not penetrated:
		# 钝伤未穿透：10%HP + 90%DR
		var hp_damage = maxi(1, int(damage * 0.1))
		var dr_damage = maxi(1, int(damage * 0.9))
		defender.take_dr_damage(dr_damage)
		result["armor_penetrated"] = false
		result["armor_damage"] = dr_damage
		return hp_damage
	
	if not penetrated:
		# 未穿透：按类型分配伤害到DR
		var dr_ratio = 0.4  # 砍伤默认40%削甲
		if dmg_type == WeaponData.DamageType.PIERCE:
			dr_ratio = 0.1  # 刺伤几乎不削甲
		# 钝伤已在上面处理
		
		var dr_damage = maxi(1, int(damage * dr_ratio))
		defender.take_dr_damage(dr_damage)
		result["armor_penetrated"] = false
		result["armor_damage"] = dr_damage
		return 0  # HP不扣
	
	# ===== 穿透 =====
	result["armor_penetrated"] = true
	
	match dmg_type:
		WeaponData.DamageType.SLASH:
			# 砍伤穿透：70%HP + 30%DR
			var hp_dmg = maxi(1, int(damage * 0.7))
			var dr_dmg = maxi(1, int(damage * 0.3))
			defender.take_dr_damage(dr_dmg)
			result["armor_damage"] = dr_dmg
			return hp_dmg
		
		WeaponData.DamageType.PIERCE:
			# 刺伤穿透：100%HP，0%DR（完全无视装甲）
			result["armor_damage"] = 0
			return damage
		
		WeaponData.DamageType.CRUSH:
			# 钝伤穿透：30%HP + 70%DR，DR≤0时伤害×1.5
			var hp_dmg = maxi(1, int(damage * 0.3))
			var dr_dmg = maxi(1, int(damage * 0.7))
			defender.take_dr_damage(dr_dmg)
			result["armor_damage"] = dr_dmg
			if defender.is_armor_destroyed():
				hp_dmg = int(hp_dmg * 1.5)  # 装甲损毁后增伤
				result["crush_bonus"] = true
			return hp_dmg
		
		_:
			# 魔法等：无视装甲直接打HP
			return damage


# ============================================================================
# 借机攻击结算
# ============================================================================

static func resolve_attack_of_opportunity(attacker: Unit, mover: Unit) -> Dictionary:
	var result = resolve_attack(attacker, mover)
	if result["hit"]:
		result["damage"] = maxi(1, result["damage"] / 2)
		mover.take_damage(result["damage"] - result["damage"])
	attacker.data.aoo_used_this_turn = true
	return result


# ============================================================================
# 反击结算
# ============================================================================

static func resolve_counter_attack(defender: Unit, attacker_pos: Vector2i) -> Dictionary:
	var mult = FacingSystem.get_counter_attack_multiplier(defender, attacker_pos)
	if mult <= 0.0:
		return {"hit": false, "damage": 0}

	var weapon = defender.get_main_hand()
	var base_damage = 0
	if weapon:
		base_damage = weapon.damage_dice_count * (weapon.damage_dice_sides + 1) / 2
		var stat_mod = RPGRuleEngine.get_stat_modifier(defender.data.str)
		base_damage += stat_mod
	else:
		base_damage = 2

	var damage = maxi(1, int(base_damage * mult))
	defender.data.counter_used_this_turn = true

	return {"hit": true, "damage": damage, "multiplier": mult}


# ============================================================================
# 命中率预览（供UI用）
# ============================================================================

static func get_hit_chance_preview(attacker: Unit, defender: Unit, grid: HexGrid = null) -> float:
	var attack_bonus = attacker.get_attack_bonus()
	var target_ac = defender.get_effective_ac(attacker, grid)

	var has_advantage = false
	var has_disadvantage = false

	if grid:
		var hg = LineOfSight.get_high_ground_bonus(attacker.grid_pos, defender.grid_pos, grid)
		if hg["advantage"]: has_advantage = true
		if hg["disadvantage"]: has_disadvantage = true

	return RPGRuleEngine.calculate_hit_chance(attack_bonus, target_ac, has_advantage, has_disadvantage)

## 预览伤害范围
static func get_damage_preview(attacker: Unit) -> Dictionary:
	var weapon = attacker.get_main_hand()
	if not weapon:
		return {"min": 1, "max": 3, "avg": 2}

	var stat_mod = RPGRuleEngine.get_stat_modifier(attacker.data.str)
	var min_dmg = weapon.damage_dice_count + stat_mod
	var max_dmg = weapon.damage_dice_count * weapon.damage_dice_sides + stat_mod
	var avg_dmg = weapon.damage_dice_count * (weapon.damage_dice_sides + 1) / 2 + stat_mod

	return {"min": maxi(1, min_dmg), "max": max(1, max_dmg), "avg": max(1, avg_dmg)}
