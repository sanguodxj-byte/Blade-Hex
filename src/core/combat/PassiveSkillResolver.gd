# PassiveSkillResolver.gd
# 被动技能查询接口 — 从 SkillEffectExecutor 中提取
# 提供所有被动技能的加成查询，供 CombatResolver / Unit / CombatManager 调用
class_name PassiveSkillResolver


# ============================================================================
# 被动伤害加成
# ============================================================================

## 暴击伤害倍率（默认x2，critical_x3 → x3）
static func get_crit_multiplier(unit: Unit) -> int:
	if unit.has_skill_effect("critical_x3"):
		return 3
	return 2


## 近战伤害加成（被动）
## weapon_mastery: STR修正 * 1.5（替换原有的STR修正部分）
static func get_passive_melee_damage_bonus(unit: Unit) -> int:
	var bonus = 0
	if unit.has_skill_effect("weapon_mastery"):
		var str_mod = RPGRuleEngine.get_stat_modifier(unit.data.str)
		if str_mod > 0:
			bonus += int(str_mod * 0.5)
	return bonus


## 近战伤害总倍率修正（berserk_power = 1.5x, last_stand = 1.5x）
static func get_passive_melee_damage_multiplier(unit: Unit) -> float:
	if unit.has_skill_effect("berserk_power"):
		return 1.5
	if unit.has_skill_effect("last_stand"):
		if unit.data and unit.current_hp > 0:
			var hp_pct = float(unit.current_hp) / float(unit.get_max_hp())
			if hp_pct < 0.25:
				return 1.5
	return 1.0


## 物理伤害减免（iron_wall: -3, diamond_body: -3）
static func get_passive_damage_reduction(unit: Unit, damage_type: String = "physical") -> int:
	var reduction = 0
	if damage_type == "physical" and unit.has_skill_effect("iron_wall"):
		reduction += 3
	if unit.has_skill_effect("diamond_body"):
		reduction += 3
	return reduction


# ============================================================================
# 被动AC加成
# ============================================================================

## 被动AC加成（hold_ground / dodge_master / last_stand）
static func get_passive_ac_bonus(unit: Unit) -> int:
	var bonus = 0
	if unit.has_skill_effect("hold_ground"):
		if not unit.has_moved:
			bonus += 2
	if unit.has_skill_effect("dodge_master"):
		var dex_mod = RPGRuleEngine.get_stat_modifier(unit.data.dex)
		if dex_mod > 0:
			bonus += dex_mod
	if unit.has_skill_effect("last_stand"):
		if unit.data and unit.current_hp > 0:
			var hp_pct = float(unit.current_hp) / float(unit.get_max_hp())
			if hp_pct < 0.25:
				bonus += 5
	return bonus


## 远程AC加成（ghost_step掩护效果）
static func get_passive_ranged_ac_bonus(unit: Unit) -> int:
	if unit.has_skill_effect("ghost_step"):
		return 2
	return 0


# ============================================================================
# 被动命中加成
# ============================================================================

## 被动近战命中加成
static func get_passive_melee_hit_bonus(unit: Unit) -> int:
	var bonus = 0
	if unit.has_skill_effect("melee_hit_plus_1"):
		bonus += 1
	return bonus


## 被动远程命中加成
static func get_passive_ranged_hit_bonus(unit: Unit) -> int:
	var bonus = 0
	if unit.has_skill_effect("ranged_hit_plus_1"):
		bonus += 1
	return bonus


# ============================================================================
# 法术相关被动
# ============================================================================

## 法术DC加成
static func get_passive_spell_dc_bonus(unit: Unit) -> int:
	var bonus = 0
	if unit.has_skill_effect("spell_mastery"):
		bonus += 2
	if unit.has_skill_effect("absolute_focus"):
		bonus += 4
	return bonus


## 法术穿透（目标抗性减值）
static func get_passive_spell_penetration(unit: Unit) -> int:
	if unit.has_skill_effect("spell_penetration"):
		return 2
	return 0


## 范围法术额外范围
static func get_passive_aoe_range_bonus(unit: Unit) -> int:
	if unit.has_skill_effect("range_expand"):
		return 1
	return 0


# ============================================================================
# 特殊被动
# ============================================================================

## 偷袭额外伤害骰数
static func get_sneak_attack_dice(unit: Unit, has_advantage: bool) -> int:
	if not has_advantage:
		return 0
	var dice = 0
	if unit.has_skill_effect("sneak_attack"):
		dice += 2  # +2d6
	if unit.has_skill_effect("deadly_blow"):
		dice += 3  # +3d6
	return dice


## 偷袭伤害面数
static func get_sneak_attack_sides() -> int:
	return 6


## 是否拥有自动反击
static func has_auto_counter(unit: Unit) -> bool:
	return unit.has_skill_effect("counter_attack")


## 是否拥有死亡豁免（iron_will）
static func has_death_save(unit: Unit) -> bool:
	return unit.has_skill_effect("iron_will")


## 执行死亡豁免（DC15强韧）
static func roll_death_save(unit: Unit) -> bool:
	if not unit.data:
		return false
	var con_score = unit.data.con
	var prof = RPGRuleEngine.get_proficiency_bonus(unit.data.level)
	var result = RPGRuleEngine.make_save(con_score, prof, false, 15)
	return result["success"]


## 是否拥有快速施法
static func has_quick_cast(unit: Unit) -> bool:
	return unit.has_skill_effect("quick_cast")


## 是否拥有穿透射击
static func has_piercing_shot(unit: Unit) -> bool:
	return unit.has_skill_effect("piercing_shot")


# ============================================================================
# 奥术共鸣
# ============================================================================

## 奥术共鸣层数的伤害加成（0.0 / 0.2 / 0.4）
static func get_arcane_resonance_bonus(unit: Unit) -> float:
	if not unit.has_skill_effect("arcane_resonance"):
		return 0.0
	var stacks = int(unit.data.get("_arcane_resonance_stacks") or 0)
	return mini(stacks, 2) * 0.2


## 增加奥术共鸣层数（施法后调用）
static func increment_arcane_resonance(unit: Unit):
	if unit.has_skill_effect("arcane_resonance"):
		var current = int(unit.data.get("_arcane_resonance_stacks") or 0)
		unit.data["_arcane_resonance_stacks"] = mini(current + 1, 2)


## 重置奥术共鸣（回合开始时可选重置）
static func reset_arcane_resonance(unit: Unit):
	if unit.data:
		unit.data["_arcane_resonance_stacks"] = 0


# ============================================================================
# 治疗被动
# ============================================================================

## 治疗额外加成（nature_affinity: +1d4）
static func get_passive_heal_bonus(unit: Unit) -> int:
	if unit.has_skill_effect("nature_affinity"):
		return RPGRuleEngine.roll_dice(1, 4)
	return 0


# ============================================================================
# 远程被动
# ============================================================================

## 远程伤害被动加成
static func get_passive_ranged_damage_bonus(unit: Unit, has_high_ground: bool = false) -> int:
	var bonus = 0
	if unit.has_skill_effect("sniper") and has_high_ground:
		bonus += 1
	return bonus


## 远程射程加成
static func get_passive_range_bonus(unit: Unit) -> int:
	if unit.has_skill_effect("sniper"):
		return 2
	return 0


# ============================================================================
# 光环与誓言
# ============================================================================

## 统帅光环加成
static func get_command_aura_bonus(unit: Unit) -> Dictionary:
	if unit.has_skill_effect("command_aura"):
		return {"attack_bonus": 1, "ac_bonus": 1}
	return {}


## 复仇誓言伤害加成
static func get_vow_of_vengeance_bonus(unit: Unit, target: Unit) -> float:
	if not unit.has_skill_effect("vow_of_vengeance"):
		return 1.0
	var marked = unit.data.get("_vengeance_target_id") if unit.data.get("_vengeance_target_id") != null else -1
	if marked == -1:
		return 1.0
	if unit.data and target.data and target.data.get_instance_id() == marked:
		return 1.25
	return 1.0


## 设置复仇目标
static func set_vengeance_target(unit: Unit, target: Unit):
	if unit.data:
		unit.data["_vengeance_target_id"] = target.data.get_instance_id() if target.data else -1


## 复仇目标死亡时恢复全队10%HP
static func on_vengeance_target_killed(avenger: Unit, all_allies: Array[Unit]):
	if not avenger.has_skill_effect("vow_of_vengeance"):
		return
	for ally in all_allies:
		if is_instance_valid(ally) and ally.current_hp > 0:
			var heal = maxi(1, int(ally.get_max_hp() * 0.1))
			ally.current_hp = mini(ally.current_hp + heal, ally.get_max_hp())


## 君临天下加成（范围内友军全豁免+2，免疫恐慌）
static func get_royal_presence_save_bonus(unit: Unit) -> int:
	if unit.has_skill_effect("royal_presence"):
		return 2
	return 0


## 生命之泉每回合治疗量
static func get_life_spring_heal(unit: Unit) -> int:
	if unit.has_skill_effect("life_spring"):
		return RPGRuleEngine.roll_dice(1, 6)
	return 0


## 灵魂守护：友军死亡时触发恢复
static func trigger_soul_guardian(guardian: Unit, _dying_ally: Unit):
	if not guardian.has_skill_effect("soul_guardian"):
		return 0
	if guardian.data.get("_soul_guardian_used") == true:
		return 0
	guardian.data["_soul_guardian_used"] = true
	var wis_mod = RPGRuleEngine.get_stat_modifier(guardian.data.wis)
	var heal = RPGRuleEngine.roll_dice(1, 10) + wis_mod
	return maxi(1, heal)


# ============================================================================
# Keystone 惩罚
# ============================================================================

## Keystone AC惩罚
static func get_keystone_ac_penalty(unit: Unit) -> int:
	if unit.has_skill_effect("berserk_power"):
		return 3
	return 0


## Keystone HP上限修正比例
static func get_keystone_hp_modifier(unit: Unit) -> float:
	var mod = 1.0
	if unit.has_skill_effect("ghost_step"):
		mod *= 0.8
	if unit.has_skill_effect("royal_presence"):
		mod *= 0.8
	return mod


## Keystone 速度惩罚
static func get_keystone_speed_penalty(unit: Unit) -> int:
	var penalty = 0
	if unit.has_skill_effect("diamond_body"):
		penalty += 2
	if unit.has_skill_effect("life_spring"):
		penalty += 1
	return penalty


# ============================================================================
# 非战斗被动（经济/商店）
# ============================================================================

## 商店折扣（diplomacy）
static func get_shop_discount(unit: Unit) -> float:
	if unit.has_skill_effect("diplomacy"):
		return 0.8  # 八折
	return 1.0


## 招募折扣（diplomacy）
static func get_recruit_discount(unit: Unit) -> float:
	if unit.has_skill_effect("diplomacy"):
		return 0.85  # 八五折
	return 1.0


## 额外金币倍率（merchant_empire）
static func get_gold_bonus_multiplier(unit: Unit) -> float:
	if unit.has_skill_effect("merchant_empire"):
		return 1.0
	return 1.0


## 稀有物品概率加成（merchant_empire）
static func get_rare_item_chance_bonus(unit: Unit) -> float:
	if unit.has_skill_effect("merchant_empire"):
		return 0.15
	return 0.0
