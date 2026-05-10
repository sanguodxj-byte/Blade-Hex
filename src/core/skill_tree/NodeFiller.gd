# NodeFiller.gd
# 技能盘自动维护组件 — 解锁后自动刷新全图可用状态
# 核心逻辑：遍历所有 LOCKED 节点，检查是否有已解锁邻居 + 前置条件满足
extends RefCounted
class_name NodeFiller

const SkillNodeData = preload("res://src/core/skill_tree/SkillNodeData.gd")
const SkillTreeCoord = preload("res://src/core/skill_tree/SkillTreeCoord.gd")

## 全量刷新：遍历所有 LOCKED 节点，标记满足条件的为 AVAILABLE
## O(n) 复杂度，150+ 节点毫无压力
static func refresh_available(nodes: Dictionary, activated: Array[String], character_level: int) -> void:
	for node_id in nodes:
		var node: SkillNodeData = nodes[node_id]
		if node.state == SkillNodeData.State.LOCKED:
			if _check_available(node, nodes, activated, character_level):
				node.state = SkillNodeData.State.AVAILABLE

## 检查单个节点是否可解锁
static func _check_available(node: SkillNodeData, nodes: Dictionary, activated: Array[String], character_level: int) -> bool:
	# 规则1：必须有已解锁的邻居（图论邻接，非空间邻接）
	var has_unlocked_neighbor := false
	for neighbor_id in node.neighbors:
		if neighbor_id in activated:
			has_unlocked_neighbor = true
			break
	if not has_unlocked_neighbor:
		return false

	# 规则2：前置节点必须全部解锁
	for prereq in node.prerequisites:
		if prereq not in activated:
			return false

	# 规则3：角色等级达标
	if node.required_level > character_level:
		return false

	return true

## 解锁一个节点，成功后自动刷新全图
## 返回 { "success": bool, "message": String }
static func unlock(node_id: String, nodes: Dictionary, activated: Array[String], character_level: int, skill_points: int) -> Dictionary:
	if not nodes.has(node_id):
		return {"success": false, "message": "节点不存在"}

	var node: SkillNodeData = nodes[node_id]

	if node.state == SkillNodeData.State.UNLOCKED:
		return {"success": false, "message": "已经解锁"}

	if node.state != SkillNodeData.State.AVAILABLE:
		return {"success": false, "message": "不可解锁（需要相邻已解锁节点）"}

	if skill_points <= 0:
		return {"success": false, "message": "技能点不足"}

	# 执行解锁
	node.state = SkillNodeData.State.UNLOCKED
	node.is_activated = true
	activated.append(node_id)

	# 刷新全图
	refresh_available(nodes, activated, character_level)

	return {"success": true, "message": "解锁成功: %s" % node.node_name}

## 优化版：只刷新被解锁节点的邻居（适合 1000+ 节点扩展）
## 当前 150+ 节点用 refresh_available 即可，预留此接口
static func refresh_neighbors_of(unlocked_id: String, nodes: Dictionary, activated: Array[String], character_level: int) -> void:
	if not nodes.has(unlocked_id):
		return
	var unlocked: SkillNodeData = nodes[unlocked_id]
	for neighbor_id in unlocked.neighbors:
		if not nodes.has(neighbor_id):
			continue
		var nb: SkillNodeData = nodes[neighbor_id]
		if nb.state == SkillNodeData.State.LOCKED:
			if _check_available(nb, nodes, activated, character_level):
				nb.state = SkillNodeData.State.AVAILABLE
