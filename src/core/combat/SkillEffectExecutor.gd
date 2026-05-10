# SkillEffectExecutor.gd
# 技能特效执行引擎 — 静态工具类
# 负责主动技能的执行逻辑
# 注册表数据委托给 SkillRegistry，被动查询委托给 PassiveSkillResolver
# 对应策划案 技能盘系统.md 中定义的全部 ~63 个技能效果
class_name SkillEffectExecutor


# ============================================================================
# 枚举（保留向后兼容，同时委托给 SkillRegistry）
# ============================================================================

enum SkillCategory {
	MELEE_ACTIVE,
	RANGED_ACTIVE,
	MAGIC_ACTIVE,
	HEAL_ACTIVE,
	SUPPORT_ACTIVE,
	PASSIVE,
	KEYSTONE,
	OUT_OF_COMBAT,
}

enum TargetType {
	SELF,
	SINGLE_ENEMY,
	SINGLE_ALLY,
	ALL_ADJACENT,
	AOE_SMALL,
	AOE_CONE,
	RANGED_SINGLE,
	RANGED_AOE,
	ALL_ALLIES,
}


# ============================================================================
# 注册表查询 — 委托给 SkillRegistry（向后兼容接口）
# ============================================================================

## 获取技能配置（委托给 SkillRegistry）
static func get_skill_config(skill_effect: String) -> Dictionary:
	return SkillRegistry.get_skill_config(skill_effect)


## 判断技能是否为主动技能（委托给 SkillRegistry）
static func is_active_skill(skill_effect: String) -> bool:
	return SkillRegistry.is_active_skill(skill_effect)


## 判断技能是否为被动技能（委托给 SkillRegistry）
static func is_passive_skill(skill_effect: String) -> bool:
	return SkillRegistry.is_passive_skill(skill_effect)


## 获取所有主动技能列表（委托给 SkillRegistry）
static func get_all_active_skill_ids() -> Array[String]:
	return SkillRegistry.get_all_active_skill_ids()


# ============================================================================
# 主动技能执行 — 主入口
# ============================================================================

## 执行主动技能
## attacker: 施放者
## skill_effect: 技能效果标识字符串
## target_cell: 目标格子坐标
## grid: HexGrid（用于空间查询，可传 null）
## all_units: 所有战斗单位
## player_units / enemy_units: 区分敌我
## 返回: { "success": bool, "results": Array, "action_cost": String, "vfx_type": String, "status_effects": Array }
static func execute_active_skill(
	attacker: Unit,
	skill_effect: String,
	target_cell: Vector2i,
	grid,  # HexGrid or null
	_all_units: Array[Unit],
	player_units: Array[Unit],
	enemy_units: Array[Unit]
) -> Dictionary:
	var cfg = get_skill_config(skill_effect)
	if cfg.is_empty():
		return {"success": false, "results": [], "action_cost": "", "vfx_type": "", "status_effects": [], "reason": "未知技能"}

	if not is_active_skill(skill_effect):
		return {"success": false, "results": [], "action_cost": "", "vfx_type": "", "status_effects": [], "reason": "非主动技能"}

	# 查找技能节点获取更多信息
	var _skill_node = _find_skill_node(attacker, skill_effect)

	# 获取敌我列表
	var allies = player_units if not attacker.data.is_enemy else enemy_units
	var enemies = enemy_units if not attacker.data.is_enemy else player_units

	# 分派到具体技能执行方法
	var result: Dictionary = {"success": true, "results": [], "action_cost": cfg.get("action_cost", "major"), "vfx_type": cfg.get("vfx", ""), "status_effects": []}

	match skill_effect:
		# STR 主动
		"double_attack":
			_exec_double_attack(attacker, target_cell, grid, enemies, result)
		"whirlwind":
			_exec_whirlwind(attacker, grid, enemies, result)
		"battle_cry":
			_exec_battle_cry(attacker, grid, allies, enemies, result)
		"blood_vortex":
			_exec_blood_vortex(attacker, grid, enemies, result)

		# DEX 主动
		"aimed_shot":
			_exec_aimed_shot(attacker, target_cell, grid, enemies, result)
		"double_shot":
			_exec_double_shot(attacker, target_cell, grid, enemies, result)
		"scatter_shot":
			_exec_scatter_shot(attacker, target_cell, grid, enemies, result)
		"stealth":
			_exec_stealth(attacker, result)
		"shadow_clone":
			_exec_shadow_clone(attacker, result)
		"trick_arrow":
			_exec_trick_arrow(attacker, target_cell, grid, enemies, result)
		"poison_blade":
			_exec_poison_blade(attacker, target_cell, grid, enemies, result)

		# CON 主动
		"shield_bash":
			_exec_shield_bash(attacker, target_cell, grid, enemies, result)
		"taunt":
			_exec_taunt(attacker, grid, enemies, result)
		"unyielding_bulwark":
			_exec_unyielding_bulwark(attacker, result)
		"field_medic":
			_exec_field_medic(attacker, target_cell, grid, allies, result)

		# INT 主动
		"mana_shield":
			_exec_mana_shield(attacker, result)
		"time_warp":
			_exec_time_warp(attacker, result)

		# WIS 主动
		"basic_heal":
			_exec_basic_heal(attacker, target_cell, grid, allies, result)
		"blessing":
			_exec_blessing(attacker, target_cell, grid, allies, result)
		"holy_shield":
			_exec_holy_shield(attacker, target_cell, grid, allies, result)
		"mass_heal":
			_exec_mass_heal(attacker, target_cell, grid, allies, result)
		"dispel":
			_exec_dispel(attacker, target_cell, grid, allies, result)
		"holy_judgment":
			_exec_holy_judgment(attacker, target_cell, grid, enemies, result)
		"natures_wrath":
			_exec_natures_wrath(attacker, target_cell, grid, enemies, result)

		# CHA 主动
		"war_cry":
			_exec_war_cry(attacker, grid, allies, result)
		"inspire":
			_exec_inspire(attacker, allies, result)
		"rally":
			_exec_rally(attacker, target_cell, grid, allies, result)
		"intimidate":
			_exec_intimidate(attacker, target_cell, grid, enemies, result)
		"heroic_call":
			_exec_heroic_call(attacker, allies, result)

		_:
			result["success"] = false
			result["reason"] = "技能 %s 尚未实现" % skill_effect

	return result


## 查找角色的技能节点数据
static func _find_skill_node(unit: Unit, skill_effect: String) -> SkillNodeData:
	if not unit.skill_tree:
		return null
	for node_id in unit.skill_tree.activated_nodes:
		var node: SkillNodeData = unit.skill_tree.tree_data.nodes.get(node_id)
		if node and node.skill_effect == skill_effect:
			return node
	return null


## 获取目标格子上的单位
static func _get_unit_at_cell(cell: Vector2i, all_units: Array[Unit]) -> Unit:
	for u in all_units:
		if is_instance_valid(u) and u.current_hp > 0 and u.grid_pos == cell:
			return u
	return null


## 获取攻击者周围的所有敌方单位
static func _get_adjacent_enemies(attacker: Unit, enemies: Array[Unit]) -> Array[Unit]:
	var result: Array[Unit] = []
	for e in enemies:
		if not is_instance_valid(e) or e.current_hp <= 0:
			continue
		var dist = HexUtils.distance(attacker.grid_pos.x, attacker.grid_pos.y, e.grid_pos.x, e.grid_pos.y)
		if dist <= 1:
			result.append(e)
	return result


## 获取范围内的友军（不含自身）
static func _get_allies_in_range(attacker: Unit, center: Vector2i, range_val: int, allies: Array[Unit], include_self: bool = false) -> Array[Unit]:
	var result: Array[Unit] = []
	for a in allies:
		if not is_instance_valid(a) or a.current_hp <= 0:
			continue
		if not include_self and a == attacker:
			continue
		var dist = HexUtils.distance(center.x, center.y, a.grid_pos.x, a.grid_pos.y)
		if dist <= range_val:
			result.append(a)
	return result


## 获取WIS修正值
static func _get_wis_mod(unit: Unit) -> int:
	if not unit.data: return 0
	return RPGRuleEngine.get_stat_modifier(unit.data.wis)


## 获取CON修正值
static func _get_con_mod(unit: Unit) -> int:
	if not unit.data: return 0
	return RPGRuleEngine.get_stat_modifier(unit.data.con)


## 获取STR修正值
static func _get_str_mod(unit: Unit) -> int:
	if not unit.data: return 0
	return RPGRuleEngine.get_stat_modifier(unit.data.str)


# ============================================================================
# STR 主动技能实现
# ============================================================================

## 连击：攻击2次，第二次-3命中
static func _exec_double_attack(attacker: Unit, target_cell: Vector2i, grid, enemies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, enemies)
	if not target:
		result["success"] = false
		result["reason"] = "目标格无敌人"
		return
	# 第一次攻击（正常）
	var r1 = CombatResolver.resolve_attack(attacker, target, grid)
	r1["attack_index"] = 0
	result["results"].append(r1)
	# 第二次攻击（-3命中）
	var r2 = CombatResolver.resolve_attack(attacker, target, grid)
	if r2["hit"]:
		# 重新计算：total_attack - 3 重新判定（含擦伤）
		r2["total_attack"] -= 3
		var is_crit = r2["roll"] == 20
		var is_fumble = r2["roll"] == 1
		var is_hit = is_crit or (not is_fumble and r2["total_attack"] >= r2["target_ac"])
		# 擦伤
		if not is_hit and not is_fumble:
			var miss_by = r2["target_ac"] - r2["total_attack"]
			if miss_by <= 2:
				is_hit = true
				r2["graze"] = true
				r2["damage"] = maxi(1, r2["damage"] / 2)
		r2["hit"] = is_hit
		if not r2["hit"]:
			r2["damage"] = 0
	r2["attack_index"] = 1
	r2["hit_penalty"] = -3
	result["results"].append(r2)
	attacker.play_anim("attack")


## 旋风斩：攻击周围所有敌人
static func _exec_whirlwind(attacker: Unit, grid, enemies: Array[Unit], result: Dictionary):
	var targets = _get_adjacent_enemies(attacker, enemies)
	if targets.is_empty():
		result["success"] = false
		result["reason"] = "周围没有敌人"
		return
	for t in targets:
		var r = CombatResolver.resolve_attack(attacker, t, grid)
		r["skill"] = "whirlwind"
		result["results"].append(r)
	attacker.play_anim("attack")


## 战斗怒吼：震慑周围敌人攻击-2，友军士气+3
static func _exec_battle_cry(attacker: Unit, _grid, allies: Array[Unit], enemies: Array[Unit], result: Dictionary):
	# 对周围敌人施加攻击-2的debuff
	var adj_enemies = _get_adjacent_enemies(attacker, enemies)
	for e in adj_enemies:
		result["status_effects"].append({
			"target": e,
			"effect_id": "intimidated",
			"duration": 2,  # 下回合
			"stat_modifiers": {"attack_penalty": -2},
		})
		result["results"].append({"target": e, "effect": "intimidated", "attack_penalty": -2})
	# 对周围友军施加士气+3
	var adj_allies = _get_allies_in_range(attacker, attacker.grid_pos, 1, allies, true)
	for a in adj_allies:
		result["results"].append({"target": a, "morale_bonus": 3})
	attacker.play_anim("attack")


## 血腥漩涡：横扫周围所有敌人，每命中1个恢复1d6 HP
static func _exec_blood_vortex(attacker: Unit, grid, enemies: Array[Unit], result: Dictionary):
	var targets = _get_adjacent_enemies(attacker, enemies)
	if targets.is_empty():
		result["success"] = false
		result["reason"] = "周围没有敌人"
		return
	var hits = 0
	for t in targets:
		var r = CombatResolver.resolve_attack(attacker, t, grid)
		r["skill"] = "blood_vortex"
		result["results"].append(r)
		if r["hit"]:
			hits += 1
	# 每命中1个恢复1d6
	if hits > 0:
		var heal = RPGRuleEngine.roll_dice(hits, 6)
		attacker.current_hp = mini(attacker.current_hp + heal, attacker.get_max_hp())
		result["results"].append({"self_heal": true, "amount": heal, "hits": hits})
	attacker.play_anim("attack")


# ============================================================================
# DEX 主动技能实现
# ============================================================================

## 精准射击：优势+伤害x2
static func _exec_aimed_shot(attacker: Unit, target_cell: Vector2i, _grid, enemies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, enemies)
	if not target:
		result["success"] = false
		result["reason"] = "目标格无敌人"
		return
	# 使用优势掷骰
	var roll_info = RPGRuleEngine.roll_with_advantage()
	var roll = roll_info["result"]
	var attack_bonus = attacker.get_attack_bonus()
	var total = roll + attack_bonus
	var target_ac = target.get_ac()
	var is_crit = (roll == 20)
	var is_hit = is_crit or (total >= target_ac)

	var damage = 0
	if is_hit:
		var dmg_info = attacker.roll_damage()
		damage = dmg_info["total"]
		damage *= 2  # 伤害翻倍
		if is_crit:
			damage *= 2  # 暴击再翻倍（共4倍）
		target.take_damage(damage)

	result["results"].append({
		"target": target, "hit": is_hit, "critical": is_crit,
		"roll": roll, "total_attack": total, "target_ac": target_ac,
		"damage": damage, "advantage": true, "skill": "aimed_shot",
	})
	attacker.play_anim("attack")


## 双重射击：射击2个目标各-2命中
static func _exec_double_shot(attacker: Unit, target_cell: Vector2i, grid, enemies: Array[Unit], result: Dictionary):
	# 第一个目标：target_cell
	var t1 = _get_unit_at_cell(target_cell, enemies)
	if t1:
		var r1 = CombatResolver.resolve_attack(attacker, t1, grid)
		_recheck_hit_with_penalty(r1, 2)
		r1["hit_penalty"] = -2
		r1["target_index"] = 0
		result["results"].append(r1)

	# 第二个目标：target_cell的邻近随机敌人
	var second_targets: Array[Unit] = []
	for e in enemies:
		if not is_instance_valid(e) or e.current_hp <= 0 or e == t1:
			continue
		var dist = HexUtils.distance(target_cell.x, target_cell.y, e.grid_pos.x, e.grid_pos.y)
		if dist <= 2:
			second_targets.append(e)
	if second_targets.size() > 0:
		var t2 = second_targets[randi() % second_targets.size()]
		var r2 = CombatResolver.resolve_attack(attacker, t2, grid)
		_recheck_hit_with_penalty(r2, 2)
		r2["hit_penalty"] = -2
		r2["target_index"] = 1
		result["results"].append(r2)

	attacker.play_anim("attack")


## 散射：锥形范围射击（简化为前方3格+中间延伸）
static func _exec_scatter_shot(attacker: Unit, _target_cell: Vector2i, grid, enemies: Array[Unit], result: Dictionary):
	var hit_enemies: Array[Unit] = []
	for e in enemies:
		if not is_instance_valid(e) or e.current_hp <= 0:
			continue
		var dist = HexUtils.distance(attacker.grid_pos.x, attacker.grid_pos.y, e.grid_pos.x, e.grid_pos.y)
		if dist <= 3:
			# 简化锥形：距离3以内且在攻击方向上的敌人
			hit_enemies.append(e)
	for e in hit_enemies:
		var r = CombatResolver.resolve_attack(attacker, e, grid)
		r["skill"] = "scatter_shot"
		# 散射伤害减半
		if r["hit"] and r["damage"] > 0:
			r["damage"] = maxi(1, r["damage"] / 2)
			e.take_damage(r["damage"] - r["damage"])  # 已在resolve_attack中扣除，这里调整记录
		result["results"].append(r)
	attacker.play_anim("attack")


## 隐匿：进入潜行状态
static func _exec_stealth(attacker: Unit, result: Dictionary):
	result["status_effects"].append({
		"target": attacker,
		"effect_id": "invisibility",
		"duration": -1,  # 持续直到攻击
	})
	result["results"].append({"target": attacker, "effect": "stealth", "stealth": true})
	attacker.play_anim("default")


## 影分身：位移+残影，下次攻击自动闪避1次
static func _exec_shadow_clone(attacker: Unit, result: Dictionary):
	result["status_effects"].append({
		"target": attacker,
		"effect_id": "phantom",
		"duration": 3,  # 最多3回合
	})
	result["results"].append({"target": attacker, "effect": "shadow_clone", "dodge_next": true})
	attacker.play_anim("default")


## 元素箭：1d10+随机debuff
static func _exec_trick_arrow(attacker: Unit, target_cell: Vector2i, _grid, enemies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, enemies)
	if not target:
		result["success"] = false
		result["reason"] = "目标格无敌人"
		return
	var damage = RPGRuleEngine.roll_dice(1, 10)
	damage += RPGRuleEngine.get_stat_modifier(attacker.data.str)
	damage = maxi(1, damage)
	target.take_damage(damage)
	# 随机debuff
	var debuffs = ["blind", "root", "stun"]
	var chosen = debuffs[randi() % debuffs.size()]
	result["status_effects"].append({
		"target": target,
		"effect_id": chosen,
		"duration": 1,
	})
	result["results"].append({
		"target": target, "hit": true, "damage": damage,
		"random_debuff": chosen, "skill": "trick_arrow",
	})
	attacker.play_anim("attack")


## 毒刃：攻击附带中毒
static func _exec_poison_blade(attacker: Unit, target_cell: Vector2i, grid, enemies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, enemies)
	if not target:
		result["success"] = false
		result["reason"] = "目标格无敌人"
		return
	# 先正常攻击
	var r = CombatResolver.resolve_attack(attacker, target, grid)
	r["skill"] = "poison_blade"
	result["results"].append(r)
	# 无论是否命中都尝试上毒（命中才有毒）
	if r["hit"]:
		result["status_effects"].append({
			"target": target,
			"effect_id": "poison",
			"duration": 3,  # 3回合
		})
	attacker.play_anim("attack")


# ============================================================================
# CON 主动技能实现
# ============================================================================

## 盾击：攻击+推开目标1格
static func _exec_shield_bash(attacker: Unit, target_cell: Vector2i, grid, enemies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, enemies)
	if not target:
		result["success"] = false
		result["reason"] = "目标格无敌人"
		return
	var r = CombatResolver.resolve_attack(attacker, target, grid)
	r["skill"] = "shield_bash"
	result["results"].append(r)
	if r["hit"]:
		var push_dir = _get_push_direction(attacker.grid_pos, target.grid_pos)
		var new_pos = HexUtils.get_neighbor(target.grid_pos.x, target.grid_pos.y, push_dir)
		r["pushed_to"] = new_pos
		r["pushed"] = true
		target.grid_pos = new_pos
	attacker.play_anim("attack")


## 嘲讽：强制周围敌人攻击自己
static func _exec_taunt(attacker: Unit, _grid, enemies: Array[Unit], result: Dictionary):
	var adj_enemies = _get_adjacent_enemies(attacker, enemies)
	for e in adj_enemies:
		result["status_effects"].append({
			"target": e,
			"effect_id": "taunted",
			"duration": 2,
			"stat_modifiers": {"forced_target": attacker},
		})
		result["results"].append({"target": e, "effect": "taunted", "forced_target": attacker})
	attacker.play_anim("attack")


## 不屈壁垒：受伤减半+临时HP
static func _exec_unyielding_bulwark(attacker: Unit, result: Dictionary):
	var wis_mod = _get_wis_mod(attacker)
	var temp_hp = maxi(1, wis_mod * 5)
	result["status_effects"].append({
		"target": attacker,
		"effect_id": "temp_hp",
		"duration": 2,
		"stat_modifiers": {"temp_hp": temp_hp, "damage_reduction_pct": 0.5},
	})
	result["results"].append({"target": attacker, "effect": "bulwark", "temp_hp": temp_hp})
	attacker.play_anim("default")


## 战地医疗：恢复友军2d8+CON修正HP，解除流血/中毒
static func _exec_field_medic(attacker: Unit, target_cell: Vector2i, _grid, allies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, allies)
	if not target:
		if attacker.grid_pos == target_cell:
			target = attacker
	if not target:
		result["success"] = false
		result["reason"] = "目标格无友军"
		return
	var con_mod = _get_con_mod(attacker)
	var heal = RPGRuleEngine.roll_dice(2, 8) + con_mod
	heal = maxi(1, heal)
	target.current_hp = mini(target.current_hp + heal, target.get_max_hp())
	result["results"].append({"target": target, "healed": true, "amount": heal})
	result["status_effects"].append({
		"target": target,
		"effect_id": "remove_bleed_poison",
		"duration": 0,
		"special": "remove_effects",
		"remove_ids": ["bleed", "poison"],
	})
	attacker.play_anim("default")


# ============================================================================
# INT 主动技能实现
# ============================================================================

## 魔力护盾：消耗5魔力获得护盾
static func _exec_mana_shield(attacker: Unit, result: Dictionary):
	if not attacker.data or attacker.data.current_mana < 5:
		result["success"] = false
		result["reason"] = "魔力不足（需要5）"
		return
	attacker.data.current_mana -= 5
	var shield_amount = 50
	result["status_effects"].append({
		"target": attacker,
		"effect_id": "temp_hp",
		"duration": 3,
		"stat_modifiers": {"temp_hp": shield_amount},
	})
	result["results"].append({"target": attacker, "effect": "mana_shield", "shield": shield_amount, "mana_spent": 5})
	attacker.play_anim("default")


## 时间扭曲：消耗10魔力获得额外次要行动(1/战斗)
static func _exec_time_warp(attacker: Unit, result: Dictionary):
	if not attacker.data or attacker.data.current_mana < 10:
		result["success"] = false
		result["reason"] = "魔力不足（需要10）"
		return
	if attacker.data.get("_time_warp_used") == true:
		result["success"] = false
		result["reason"] = "本场战斗已使用过时间扭曲"
		return
	attacker.data.current_mana -= 10
	attacker.data["_time_warp_used"] = true
	result["results"].append({"target": attacker, "effect": "time_warp", "extra_minor_action": true, "mana_spent": 10})
	attacker.play_anim("default")


# ============================================================================
# WIS 主动技能实现
# ============================================================================

## 基础治疗：恢复友军1d8+WIS修正HP
static func _exec_basic_heal(attacker: Unit, target_cell: Vector2i, _grid, allies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, allies)
	if not target:
		if attacker.grid_pos == target_cell:
			target = attacker
	if not target:
		result["success"] = false
		result["reason"] = "目标格无友军"
		return
	var wis_mod = _get_wis_mod(attacker)
	var heal = RPGRuleEngine.roll_dice(1, 8) + wis_mod
	heal += get_passive_heal_bonus(attacker)
	heal = maxi(1, heal)
	target.current_hp = mini(target.current_hp + heal, target.get_max_hp())
	result["results"].append({"target": target, "healed": true, "amount": heal})
	attacker.play_anim("default")


## 祈福：友军+1d4攻击/豁免3回合
static func _exec_blessing(attacker: Unit, target_cell: Vector2i, _grid, allies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, allies)
	if not target:
		if attacker.grid_pos == target_cell:
			target = attacker
	if not target:
		result["success"] = false
		result["reason"] = "目标格无友军"
		return
	result["status_effects"].append({
		"target": target,
		"effect_id": "bless",
		"duration": 3,
		"stat_modifiers": {"attack_bonus_dice": "1d4", "save_bonus_dice": "1d4"},
	})
	result["results"].append({"target": target, "effect": "blessing", "duration": 3})
	attacker.play_anim("default")


## 神圣护盾：友军获得临时HP
static func _exec_holy_shield(attacker: Unit, target_cell: Vector2i, _grid, allies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, allies)
	if not target:
		if attacker.grid_pos == target_cell:
			target = attacker
	if not target:
		result["success"] = false
		result["reason"] = "目标格无友军"
		return
	var wis_mod = _get_wis_mod(attacker)
	var temp_hp = maxi(1, wis_mod * 3)
	result["status_effects"].append({
		"target": target,
		"effect_id": "temp_hp",
		"duration": 3,
		"stat_modifiers": {"temp_hp": temp_hp},
	})
	result["results"].append({"target": target, "effect": "holy_shield", "temp_hp": temp_hp})
	attacker.play_anim("default")


## 群体治疗：恢复范围内所有友军HP
static func _exec_mass_heal(attacker: Unit, target_cell: Vector2i, _grid, allies: Array[Unit], result: Dictionary):
	var targets = _get_allies_in_range(attacker, target_cell, 1, allies, true)
	var wis_mod = _get_wis_mod(attacker)
	for t in targets:
		var heal = RPGRuleEngine.roll_dice(2, 6) + wis_mod
		heal += get_passive_heal_bonus(attacker)
		heal = maxi(1, heal)
		t.current_hp = mini(t.current_hp + heal, t.get_max_hp())
		result["results"].append({"target": t, "healed": true, "amount": heal})
	attacker.play_anim("default")


## 驱散：解除范围内负面状态
static func _exec_dispel(attacker: Unit, target_cell: Vector2i, _grid, allies: Array[Unit], result: Dictionary):
	var targets = _get_allies_in_range(attacker, target_cell, 1, allies, true)
	for t in targets:
		result["status_effects"].append({
			"target": t,
			"effect_id": "remove_all_negative",
			"duration": 0,
			"special": "remove_all_negative",
		})
		result["results"].append({"target": t, "effect": "dispelled"})
	attacker.play_anim("default")


## 圣光审判：亡灵/恶魔3d8，其他1d8+恢复一半HP
static func _exec_holy_judgment(attacker: Unit, target_cell: Vector2i, _grid, enemies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, enemies)
	if not target:
		result["success"] = false
		result["reason"] = "目标格无敌人"
		return
	var creature_type = target.data.get("creature_type") if target.data else ""
	var is_undead = creature_type == "undead"
	var is_demon = creature_type == "demon"
	var damage: int
	if is_undead or is_demon:
		damage = RPGRuleEngine.roll_dice(3, 8)
	else:
		damage = RPGRuleEngine.roll_dice(1, 8)
	damage += _get_wis_mod(attacker)
	damage = maxi(1, damage)
	target.take_damage(damage)
	if not is_undead and not is_demon:
		var heal = maxi(1, int(damage / 2))
		attacker.current_hp = mini(attacker.current_hp + heal, attacker.get_max_hp())
		result["results"].append({"self_heal": true, "amount": heal})
	result["results"].append({"target": target, "hit": true, "damage": damage, "is_undead_or_demon": is_undead or is_demon})
	attacker.play_anim("attack")


## 自然之怒：绊索陷阱束缚敌人1回合+1d6穿刺
static func _exec_natures_wrath(attacker: Unit, target_cell: Vector2i, _grid, enemies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, enemies)
	if not target:
		result["success"] = false
		result["reason"] = "目标格无敌人"
		return
	var damage = RPGRuleEngine.roll_dice(1, 6) + _get_wis_mod(attacker)
	damage = maxi(1, damage)
	target.take_damage(damage)
	result["status_effects"].append({"target": target, "effect_id": "root", "duration": 1})
	result["results"].append({"target": target, "hit": true, "damage": damage, "rooted": true})
	attacker.play_anim("attack")


# ============================================================================
# CHA 主动技能实现
# ============================================================================

## 战吼：范围内友军下次攻击+2伤害
static func _exec_war_cry(attacker: Unit, _grid, allies: Array[Unit], result: Dictionary):
	var targets = _get_allies_in_range(attacker, attacker.grid_pos, 2, allies, true)
	for t in targets:
		result["status_effects"].append({
			"target": t,
			"effect_id": "war_cry_buff",
			"duration": 2,
			"stat_modifiers": {"damage_bonus": 2},
		})
		result["results"].append({"target": t, "effect": "war_cry", "damage_bonus": 2})
	attacker.play_anim("attack")


## 鼓舞士气：所有友军士气+2持续3回合
static func _exec_inspire(attacker: Unit, allies: Array[Unit], result: Dictionary):
	for a in allies:
		if not is_instance_valid(a) or a.current_hp <= 0:
			continue
		result["status_effects"].append({
			"target": a,
			"effect_id": "inspired",
			"duration": 3,
			"stat_modifiers": {"morale": 2},
		})
		result["results"].append({"target": a, "effect": "inspired", "morale": 2})
	attacker.play_anim("default")


## 号召力：恢复范围内友军士气至满
static func _exec_rally(attacker: Unit, target_cell: Vector2i, _grid, allies: Array[Unit], result: Dictionary):
	var targets = _get_allies_in_range(attacker, target_cell, 2, allies, true)
	for t in targets:
		result["results"].append({"target": t, "effect": "rally", "morale_restored": true})
	attacker.play_anim("default")


## 威压：敌人攻击-2(3回合)，WIS豁免
static func _exec_intimidate(attacker: Unit, target_cell: Vector2i, _grid, enemies: Array[Unit], result: Dictionary):
	var target = _get_unit_at_cell(target_cell, enemies)
	if not target:
		result["success"] = false
		result["reason"] = "目标格无敌人"
		return
	var cha_mod = RPGRuleEngine.get_stat_modifier(attacker.data.cha)
	var prof = RPGRuleEngine.get_proficiency_bonus(attacker.data.level)
	var dc = 8 + cha_mod + prof
	var wis_score = target.data.wis
	var target_prof = RPGRuleEngine.get_proficiency_bonus(target.data.level)
	var save_result = RPGRuleEngine.make_save(wis_score, target_prof, false, dc)
	if not save_result["success"]:
		result["status_effects"].append({
			"target": target,
			"effect_id": "intimidated",
			"duration": 3,
			"stat_modifiers": {"attack_penalty": -2},
		})
		result["results"].append({"target": target, "effect": "intimidated", "save_failed": true})
	else:
		result["results"].append({"target": target, "effect": "resisted", "save_success": true})
	attacker.play_anim("attack")


## 英雄号召：插战旗，友军攻击+2 AC+1持续3回合
static func _exec_heroic_call(attacker: Unit, allies: Array[Unit], result: Dictionary):
	var targets = _get_allies_in_range(attacker, attacker.grid_pos, 3, allies, true)
	for t in targets:
		result["status_effects"].append({
			"target": t,
			"effect_id": "heroic_call_buff",
			"duration": 3,
			"stat_modifiers": {"damage_bonus": 2, "ac_bonus": 1},
		})
		result["results"].append({"target": t, "effect": "heroic_call", "damage_bonus": 2, "ac_bonus": 1})
	attacker.play_anim("attack")


# ============================================================================
# 工具方法
# ============================================================================

## 计算推开方向（从攻击者指向目标的方向）
static func _get_push_direction(from: Vector2i, to: Vector2i) -> int:
	var diff = to - from
	var directions = [
		Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 1),
		Vector2i(-1, 0), Vector2i(0, -1), Vector2i(1, -1),
	]
	var best_dir = 0
	var best_dot = -9999.0
	for i in range(6):
		var d = Vector2(directions[i].x, directions[i].y).normalized()
		var t = Vector2(diff.x, diff.y).normalized()
		var dot = d.dot(t)
		if dot > best_dot:
			best_dot = dot
			best_dir = i
	return best_dir


# ============================================================================
# 被动技能查询接口 — 委托给 PassiveSkillResolver（向后兼容接口）
# 供 CombatResolver / CombatManager / Unit 调用
# ============================================================================

static func get_crit_multiplier(unit: Unit) -> int:
	return PassiveSkillResolver.get_crit_multiplier(unit)

static func get_passive_melee_damage_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_melee_damage_bonus(unit)

static func get_passive_melee_damage_multiplier(unit: Unit) -> float:
	return PassiveSkillResolver.get_passive_melee_damage_multiplier(unit)

static func get_passive_damage_reduction(unit: Unit, damage_type: String = "physical") -> int:
	return PassiveSkillResolver.get_passive_damage_reduction(unit, damage_type)

static func has_auto_counter(unit: Unit) -> bool:
	return PassiveSkillResolver.has_auto_counter(unit)

static func has_death_save(unit: Unit) -> bool:
	return PassiveSkillResolver.has_death_save(unit)

static func roll_death_save(unit: Unit) -> bool:
	return PassiveSkillResolver.roll_death_save(unit)

static func get_passive_ac_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_ac_bonus(unit)

static func get_passive_ranged_ac_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_ranged_ac_bonus(unit)

static func get_passive_melee_hit_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_melee_hit_bonus(unit)

static func get_passive_ranged_hit_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_ranged_hit_bonus(unit)

static func get_passive_spell_dc_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_spell_dc_bonus(unit)

static func get_passive_spell_penetration(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_spell_penetration(unit)

static func get_passive_aoe_range_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_aoe_range_bonus(unit)

static func has_quick_cast(unit: Unit) -> bool:
	return PassiveSkillResolver.has_quick_cast(unit)

static func get_arcane_resonance_bonus(unit: Unit) -> float:
	return PassiveSkillResolver.get_arcane_resonance_bonus(unit)

static func increment_arcane_resonance(unit: Unit):
	PassiveSkillResolver.increment_arcane_resonance(unit)

static func reset_arcane_resonance(unit: Unit):
	PassiveSkillResolver.reset_arcane_resonance(unit)

static func get_passive_heal_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_heal_bonus(unit)

static func get_passive_ranged_damage_bonus(unit: Unit, has_high_ground: bool = false) -> int:
	return PassiveSkillResolver.get_passive_ranged_damage_bonus(unit, has_high_ground)

static func get_passive_range_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_passive_range_bonus(unit)

static func has_piercing_shot(unit: Unit) -> bool:
	return PassiveSkillResolver.has_piercing_shot(unit)

static func get_sneak_attack_dice(unit: Unit, has_advantage: bool) -> int:
	return PassiveSkillResolver.get_sneak_attack_dice(unit, has_advantage)

static func get_sneak_attack_sides() -> int:
	return PassiveSkillResolver.get_sneak_attack_sides()

static func get_command_aura_bonus(unit: Unit) -> Dictionary:
	return PassiveSkillResolver.get_command_aura_bonus(unit)

static func get_vow_of_vengeance_bonus(unit: Unit, target: Unit) -> float:
	return PassiveSkillResolver.get_vow_of_vengeance_bonus(unit, target)

static func set_vengeance_target(unit: Unit, target: Unit):
	PassiveSkillResolver.set_vengeance_target(unit, target)

static func on_vengeance_target_killed(avenger: Unit, all_allies: Array[Unit]):
	PassiveSkillResolver.on_vengeance_target_killed(avenger, all_allies)

static func get_royal_presence_save_bonus(unit: Unit) -> int:
	return PassiveSkillResolver.get_royal_presence_save_bonus(unit)

static func get_life_spring_heal(unit: Unit) -> int:
	return PassiveSkillResolver.get_life_spring_heal(unit)

static func trigger_soul_guardian(guardian: Unit, dying_ally: Unit) -> int:
	return PassiveSkillResolver.trigger_soul_guardian(guardian, dying_ally)

static func get_keystone_ac_penalty(unit: Unit) -> int:
	return PassiveSkillResolver.get_keystone_ac_penalty(unit)

static func get_keystone_hp_modifier(unit: Unit) -> float:
	return PassiveSkillResolver.get_keystone_hp_modifier(unit)

static func get_keystone_speed_penalty(unit: Unit) -> int:
	return PassiveSkillResolver.get_keystone_speed_penalty(unit)

static func get_shop_discount(unit: Unit) -> float:
	return PassiveSkillResolver.get_shop_discount(unit)

static func get_recruit_discount(unit: Unit) -> float:
	return PassiveSkillResolver.get_recruit_discount(unit)

static func get_gold_bonus_multiplier(unit: Unit) -> float:
	return PassiveSkillResolver.get_gold_bonus_multiplier(unit)

static func get_rare_item_chance_bonus(unit: Unit) -> float:
	return PassiveSkillResolver.get_rare_item_chance_bonus(unit)


# ============================================================================
# 内部工具
# ============================================================================

## 带惩罚重新检定命中（含擦伤机制）
## 用于多段攻击/减命中的技能二次判定
static func _recheck_hit_with_penalty(result: Dictionary, penalty: int) -> void:
	result["total_attack"] -= penalty
	var is_crit = result["roll"] == 20
	var is_fumble = result["roll"] == 1
	var is_hit = is_crit or (not is_fumble and result["total_attack"] >= result["target_ac"])
	# 擦伤
	if not is_hit and not is_fumble:
		var miss_by = result["target_ac"] - result["total_attack"]
		if miss_by <= 2:
			is_hit = true
			result["graze"] = true
			result["damage"] = maxi(1, result["damage"] / 2)
	result["hit"] = is_hit
	if not is_hit:
		result["damage"] = 0
