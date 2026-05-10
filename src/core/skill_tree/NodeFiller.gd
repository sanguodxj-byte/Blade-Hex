# NodeFiller.gd
# 技能盘自动维护组件 — 刷新全图可用状态到每角色集合
# 不修改共享 SkillTreeData 中的任何节点状态
extends RefCounted
class_name NodeFiller

const SkillNodeData = preload("res://src/core/skill_tree/SkillNodeData.gd")

## 全量刷新：遍历所有节点，将满足条件的节点ID写入 available_set
## activated_set: Dictionary[node_id] = true（O(1)查找）
## available_set: Dictionary[node_id] = true（输出参数，由本函数清空并重建）
static func refresh_available(nodes: Dictionary, activated_set: Dictionary, character_level: int, available_set: Dictionary) -> void:
	available_set.clear()
	for node_id in nodes:
		if activated_set.has(node_id):
			continue
		var node: SkillNodeData = nodes[node_id]
		if _check_available(node, activated_set, character_level):
			available_set[node_id] = true

## 检查单个节点是否可解锁
static func _check_available(node: SkillNodeData, activated_set: Dictionary, character_level: int) -> bool:
	# 规则1：必须有已解锁的邻居
	var has_unlocked_neighbor := false
	for neighbor_id in node.neighbors:
		if activated_set.has(neighbor_id):
			has_unlocked_neighbor = true
			break
	if not has_unlocked_neighbor:
		return false

	# 规则2：前置节点必须全部解锁
	for prereq in node.prerequisites:
		if not activated_set.has(prereq):
			return false

	# 规则3：角色等级达标
	if node.required_level > character_level:
		return false

	return true
