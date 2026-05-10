# CharacterSkillTree.gd
# 角色技能盘运行时类 — 管理单个角色的技能盘状态
# 负责：点亮节点、跳跃、属性汇总、技能列表
# 所有每角色状态保存在此对象中，不修改共享 SkillTreeData
extends RefCounted
class_name CharacterSkillTree

## 预加载依赖
const SkillNodeData = preload("res://src/core/skill_tree/SkillNodeData.gd")
const SkillTreeData = preload("res://src/core/skill_tree/SkillTreeData.gd")
const SkillTreeCoord = preload("res://src/core/skill_tree/SkillTreeCoord.gd")
const ClassTitleResolver = preload("res://src/core/skill_tree/ClassTitleResolver.gd")
const NodeFiller = preload("res://src/core/skill_tree/NodeFiller.gd")

# ============================================================================
# 信号
# ============================================================================

signal node_activated(node_id: String)
@warning_ignore("unused_signal")
signal node_deactivated(node_id: String) # 预留：未来支持节点取消点亮
signal skill_point_changed(new_count: int)
signal jump_used(jumps_remaining: int)

# ============================================================================
# 核心数据
# ============================================================================

## 技能盘图数据引用（全局共享，只读）
var tree_data: SkillTreeData

## 已点亮的节点ID列表（有序）
var activated_nodes: Array[String] = []

## 已点亮节点ID的快速查找表（O(1)），与 activated_nodes 同步维护
var activated_set: Dictionary = {}

## 可用（可点亮）节点ID集合，由 NodeFiller 刷新
var available_set: Dictionary = {}

## 可用技能点数
var available_skill_points: int = 0

## 跳跃次数
var total_jumps: int = 0
var used_jumps: int = 0

## 角色等级
var character_level: int = 1

## 节点激活时的属性累计缓存
var accumulated_stats: Dictionary = {}
var accumulated_costs: Dictionary = {}

# ============================================================================
# 初始化
# ============================================================================

func _init(ptree_data: SkillTreeData, p_level: int = 1):
	tree_data = ptree_data
	character_level = p_level
	_activate_start_node()


func _activate_start_node():
	var start = tree_data.get_start_node()
	if start:
		activated_nodes.append(start.node_id)
		activated_set[start.node_id] = true
		_apply_node_stats(start)
		NodeFiller.refresh_available(tree_data.nodes, activated_set, character_level, available_set)


# ============================================================================
# 技能点管理
# ============================================================================

func add_skill_point(amount: int = 1):
	available_skill_points += amount
	skill_point_changed.emit(available_skill_points)


func consume_skill_point() -> bool:
	if available_skill_points <= 0:
		return false
	available_skill_points -= 1
	skill_point_changed.emit(available_skill_points)
	return true


func register_jump():
	total_jumps += 1


func use_jump() -> bool:
	if used_jumps >= total_jumps:
		return false
	used_jumps += 1
	jump_used.emit(total_jumps - used_jumps)
	return true


func get_remaining_jumps() -> int:
	return total_jumps - used_jumps


# ============================================================================
# 状态查询（替代直接读取 SkillNodeData.state）
# ============================================================================

## 节点是否已激活
func is_activated(node_id: String) -> bool:
	return activated_set.has(node_id)

## 节点是否可用（可点亮）
func is_available(node_id: String) -> bool:
	return available_set.has(node_id)

# ============================================================================
# 点亮节点
# ============================================================================

## 公共验证逻辑（try_activate_node 和 try_jump_activate 共用）
func _validate_activation(node_id: String) -> Dictionary:
	if not tree_data.nodes.has(node_id):
		return {"valid": false, "message": "节点不存在"}

	var node: SkillNodeData = tree_data.nodes[node_id]

	if activated_set.has(node_id):
		return {"valid": false, "message": "节点已点亮"}

	if node.required_level > character_level:
		return {"valid": false, "message": "需要角色等级 %d" % node.required_level}

	for prereq in node.prerequisites:
		if not activated_set.has(prereq):
			var _prereq_node = tree_data.nodes.get(prereq)
			var prereq_name = _prereq_node.node_name if _prereq_node else prereq
			return {"valid": false, "message": "需要先点亮前置节点: %s" % prereq_name}

	return {"valid": true, "node": node}


## 尝试点亮一个节点
func try_activate_node(node_id: String) -> Dictionary:
	var v = _validate_activation(node_id)
	if not v.valid:
		return {"success": false, "message": v.message}

	var node: SkillNodeData = v.node

	if not node.is_adjacent_to_activated(activated_set):
		return {"success": false, "message": "该节点与已点亮区域不相邻，请先连接路径或使用跳跃"}

	if available_skill_points <= 0:
		return {"success": false, "message": "没有可用技能点"}

	_do_activate_node(node)
	return {"success": true, "message": "点亮 %s" % node.node_name}


## 跳跃点亮（无视相邻限制）
func try_jump_activate(node_id: String) -> Dictionary:
	var v = _validate_activation(node_id)
	if not v.valid:
		return {"success": false, "message": v.message}

	if get_remaining_jumps() <= 0:
		return {"success": false, "message": "没有可用跳跃次数"}

	if available_skill_points <= 0:
		return {"success": false, "message": "没有可用技能点"}

	use_jump()
	_do_activate_node(v.node)
	return {"success": true, "message": "跳跃点亮 %s" % v.node.node_name}


## 内部激活 — 更新本角色状态，刷新可用集合
func _do_activate_node(node: SkillNodeData):
	consume_skill_point()
	activated_nodes.append(node.node_id)
	activated_set[node.node_id] = true
	_apply_node_stats(node)
	node_activated.emit(node.node_id)
	NodeFiller.refresh_available(tree_data.nodes, activated_set, character_level, available_set)


# ============================================================================
# 属性汇总
# ============================================================================

func _apply_node_stats(node: SkillNodeData):
	for key in node.stat_bonuses:
		var val = node.stat_bonuses[key]
		if accumulated_stats.has(key):
			accumulated_stats[key] += val
		else:
			accumulated_stats[key] = val

	if node.node_type == SkillNodeData.NodeType.KEYSTONE:
		for key in node.cost_bonuses:
			var val = node.cost_bonuses[key]
			if accumulated_costs.has(key):
				accumulated_costs[key] += val
			else:
				accumulated_costs[key] = val


func get_hp_bonus() -> int:
	return int(accumulated_stats.get("max_hp", 0))

func get_ac_bonus() -> int:
	return int(accumulated_stats.get("ac", 0))

func get_melee_hit_bonus() -> int:
	return int(accumulated_stats.get("melee_hit", 0))

func get_melee_damage_bonus() -> int:
	return int(accumulated_stats.get("melee_damage", 0))

func get_ranged_hit_bonus() -> int:
	return int(accumulated_stats.get("ranged_hit", 0))

func get_ranged_damage_bonus() -> int:
	return int(accumulated_stats.get("ranged_damage", 0))

func get_critical_rate_bonus() -> float:
	return float(accumulated_stats.get("critical_rate", 0.0))

func get_speed_bonus() -> int:
	return int(accumulated_stats.get("speed", 0))

func get_mana_max_bonus() -> int:
	return int(accumulated_stats.get("mana_max", 0))

func get_initiative_bonus() -> int:
	return int(accumulated_stats.get("initiative", 0))

func get_all_save_bonus() -> int:
	return int(accumulated_stats.get("all_save", 0))

func get_range_bonus() -> int:
	return int(accumulated_stats.get("range_bonus", 0))

func get_morale_bonus() -> int:
	return int(accumulated_stats.get("morale", 0))

func get_cha_check_bonus() -> int:
	return int(accumulated_stats.get("cha_check", 0))

func get_wis_check_bonus() -> int:
	return int(accumulated_stats.get("wis_check", 0))

func get_spell_hit_bonus() -> int:
	return int(accumulated_stats.get("spell_hit", 0))

func get_spell_damage_bonus() -> int:
	return int(accumulated_stats.get("spell_damage", 0))

func get_heal_bonus() -> int:
	return int(accumulated_stats.get("heal_amount", 0))

func get_ally_bonus() -> int:
	return int(accumulated_stats.get("ally_bonus", 0))

func get_all_accumulated_stats() -> Dictionary:
	return accumulated_stats.duplicate()

func get_all_accumulated_costs() -> Dictionary:
	return accumulated_costs.duplicate()


# ============================================================================
# 技能查询
# ============================================================================

func get_active_skills() -> Array[SkillNodeData]:
	var result: Array[SkillNodeData] = []
	for node_id in activated_nodes:
		var node: SkillNodeData = tree_data.nodes[node_id]
		if node.is_active_skill:
			result.append(node)
	return result


func get_passive_skills() -> Array[SkillNodeData]:
	var result: Array[SkillNodeData] = []
	for node_id in activated_nodes:
		var node: SkillNodeData = tree_data.nodes[node_id]
		if not node.is_active_skill and node.node_type != SkillNodeData.NodeType.SMALL and node.node_type != SkillNodeData.NodeType.START:
			result.append(node)
	return result


func has_skill_effect(effect_name: String) -> bool:
	for node_id in activated_nodes:
		var node: SkillNodeData = tree_data.nodes[node_id]
		if node.skill_effect == effect_name:
			return true
	return false


func get_active_skill_effects() -> Array[String]:
	var result: Array[String] = []
	for node_id in activated_nodes:
		var node: SkillNodeData = tree_data.nodes[node_id]
		if node.skill_effect != "":
			result.append(node.skill_effect)
	return result


func is_node_activated(node_id: String) -> bool:
	return activated_set.has(node_id)

func get_activated_count() -> int:
	return activated_nodes.size()


# ============================================================================
# 可用节点查询
# ============================================================================

func get_available_nodes() -> Array[SkillNodeData]:
	var result: Array[SkillNodeData] = []
	for node_id in available_set:
		var node: SkillNodeData = tree_data.nodes.get(node_id)
		if node:
			result.append(node)
	return result


func get_jumpable_nodes() -> Array[SkillNodeData]:
	if get_remaining_jumps() <= 0:
		return []
	var result: Array[SkillNodeData] = []
	for node in tree_data.nodes.values():
		if activated_set.has(node.node_id):
			continue
		if node.node_type == SkillNodeData.NodeType.START:
			continue
		if node.required_level > character_level:
			continue
		var pre_req_met = true
		for prereq in node.prerequisites:
			if not activated_set.has(prereq):
				pre_req_met = false
				break
		if pre_req_met:
			result.append(node)
	return result


# ============================================================================
# 职业称号
# ============================================================================

func get_class_title(character_attrs: Dictionary = {}) -> Dictionary:
	return ClassTitleResolver.resolve(self, character_attrs)

func get_class_title_name(character_attrs: Dictionary = {}) -> String:
	return ClassTitleResolver.get_title(self, character_attrs)

func get_region_stats() -> Dictionary:
	return ClassTitleResolver.get_region_stats(self)


# ============================================================================
# 序列化
# ============================================================================

func serialize() -> Dictionary:
	return {
		"activated_nodes": activated_nodes.duplicate(),
		"available_skill_points": available_skill_points,
		"total_jumps": total_jumps,
		"used_jumps": used_jumps,
		"character_level": character_level,
	}


func deserialize(data: Dictionary, ptree_data: SkillTreeData):
	tree_data = ptree_data
	activated_nodes = data.get("activated_nodes", [])
	available_skill_points = data.get("available_skill_points", 0)
	total_jumps = data.get("total_jumps", 0)
	used_jumps = data.get("used_jumps", 0)
	character_level = data.get("character_level", 1)

	# 重建每角色状态
	activated_set.clear()
	accumulated_stats.clear()
	accumulated_costs.clear()
	for node_id in activated_nodes:
		if tree_data.nodes.has(node_id):
			activated_set[node_id] = true
			_apply_node_stats(tree_data.nodes[node_id])

	# 刷新可用节点集合
	NodeFiller.refresh_available(tree_data.nodes, activated_set, character_level, available_set)


# ============================================================================
# AI 自动加点
# ============================================================================

func ai_allocate_points(ai_accuracy: float, primary_attr: String, secondary_attr: String):
	var region_map = {
		"str": SkillNodeData.Region.STR,
		"dex": SkillNodeData.Region.DEX,
		"con": SkillNodeData.Region.CON,
		"int": SkillNodeData.Region.INT,
		"wis": SkillNodeData.Region.WIS,
		"cha": SkillNodeData.Region.CHA,
	}
	var primary_region = region_map.get(primary_attr, SkillNodeData.Region.STR)
	var secondary_region = region_map.get(secondary_attr, SkillNodeData.Region.DEX)

	while available_skill_points > 0:
		var available = get_available_nodes()
		if available.is_empty():
			var jumpable = get_jumpable_nodes()
			if jumpable.is_empty():
				break
			available = jumpable

		available.sort_custom(_ai_sort_descending.bind(primary_region, secondary_region, ai_accuracy))

		var selected: SkillNodeData
		if randf() < ai_accuracy:
			selected = available[0]
		else:
			var pool_start = mini(1, available.size() - 1)
			selected = available[randi_range(pool_start, available.size() - 1)]

		if selected.is_adjacent_to_activated(activated_set):
			try_activate_node(selected.node_id)
		else:
			try_jump_activate(selected.node_id)


func _ai_sort_descending(a: SkillNodeData, b: SkillNodeData, primary_region, secondary_region, _accuracy: float):
	var score_a = _ai_node_score(a, primary_region, secondary_region)
	var score_b = _ai_node_score(b, primary_region, secondary_region)
	return score_a > score_b


func _ai_node_score(node: SkillNodeData, primary_region, secondary_region) -> float:
	var score = 0.0
	if node.region == primary_region:
		score += 100.0
	elif node.region == secondary_region:
		score += 50.0
	elif node.region == SkillNodeData.Region.TRANSITION:
		score += 20.0

	if node.node_type == SkillNodeData.NodeType.KEYSTONE:
		score += 30.0
	elif node.node_type == SkillNodeData.NodeType.BIG:
		score += 20.0

	return score
