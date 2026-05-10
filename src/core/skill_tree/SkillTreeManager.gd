# SkillTreeManager.gd
# 技能盘全局管理器 — 单例，三层分离架构
# 层1: SkillTreeData (图数据) 层2: NodeFiller (状态维护) 层3: CharacterSkillTree (角色实例)
# 挂载为 Autoload
extends Node

const SkillTreeData = preload("res://src/core/skill_tree/SkillTreeData.gd")
const SkillNodeData = preload("res://src/core/skill_tree/SkillNodeData.gd")
const SkillTreeCoord = preload("res://src/core/skill_tree/SkillTreeCoord.gd")
const NodeFiller = preload("res://src/core/skill_tree/NodeFiller.gd")
const CharacterSkillTree = preload("res://src/core/skill_tree/CharacterSkillTree.gd")

# ============================================================================
# 单例
# ============================================================================

static var _instance: SkillTreeManager

static func get_instance() -> SkillTreeManager:
	return _instance

# ============================================================================
# 数据
# ============================================================================

## 全局共享技能盘图数据（只读，所有角色共用）
var tree_data: SkillTreeData

## 坐标组件（用于 UI 渲染）
var coord: SkillTreeCoord

## 所有角色的技能盘（角色实例ID → CharacterSkillTree）
var character_trees: Dictionary = {}

# ============================================================================
# 初始化
# ============================================================================

func _ready():
	_instance = self
	_load_tree_data()

func _load_tree_data():
	tree_data = SkillTreeData.new()
	coord = SkillTreeCoord.new()
	print("[SkillTreeManager] 技能盘加载完成，节点总数: %d" % tree_data.get_node_count())

# ============================================================================
# 角色技能盘管理
# ============================================================================

## 为角色创建技能盘
func create_skill_tree(character_id: int, level: int = 1) -> CharacterSkillTree:
	var skill_tree = CharacterSkillTree.new(tree_data, level)
	character_trees[character_id] = skill_tree
	return skill_tree

## 获取角色的技能盘
func get_skill_tree(character_id: int) -> CharacterSkillTree:
	return character_trees.get(character_id, null)

## 移除角色的技能盘
func remove_skill_tree(character_id: int):
	character_trees.erase(character_id)

# ============================================================================
# 升级处理
# ============================================================================

func on_character_level_up(character_id: int, new_level: int):
	var tree = get_skill_tree(character_id)
	if not tree:
		return
	tree.character_level = new_level
	tree.add_skill_point(1)
	if new_level % 5 == 0:
		tree.register_jump()
		print("[SkillTreeManager] 角色 %d 升到 %d 级，获得1次跳跃机会" % [character_id, new_level])
	print("[SkillTreeManager] 角色 %d 升到 %d 级，获得1技能点" % [character_id, new_level])

func init_character_level(character_id: int, level: int):
	var tree = get_skill_tree(character_id)
	if not tree:
		return
	tree.available_skill_points = 0
	tree.used_jumps = 0
	tree.total_jumps = 0
	tree.character_level = level
	var points = level - 1
	tree.add_skill_point(points)
	var jumps = int(level / 5.0)
	for i in range(jumps):
		tree.register_jump()
	print("[SkillTreeManager] 初始化角色 %d Lv.%d：%d技能点, %d跳跃" % [character_id, level, points, jumps])

# ============================================================================
# 节点操作
# ============================================================================

## 点亮节点
func activate_node(character_id: int, node_id: String) -> Dictionary:
	var tree = get_skill_tree(character_id)
	if not tree:
		return {"success": false, "message": "角色不存在"}
	return tree.try_activate_node(node_id)

## 跳跃点亮节点
func jump_activate_node(character_id: int, node_id: String) -> Dictionary:
	var tree = get_skill_tree(character_id)
	if not tree:
		return {"success": false, "message": "角色不存在"}
	return tree.try_jump_activate(node_id)

# ============================================================================
# 序列化
# ============================================================================

func save_all() -> Dictionary:
	var data = {}
	for character_id in character_trees:
		data[str(character_id)] = character_trees[character_id].serialize()
	return data

func load_all(data: Dictionary):
	character_trees.clear()
	for key in data:
		var character_id = int(key)
		var tree = CharacterSkillTree.new(tree_data, 1)
		tree.deserialize(data[key], tree_data)
		character_trees[character_id] = tree

# ============================================================================
# 调试
# ============================================================================

func debug_print_tree(character_id: int):
	var tree = get_skill_tree(character_id)
	if not tree:
		print("[SkillTreeManager] 角色 %d 没有技能盘" % character_id)
		return
	print("========== 角色 %d 技能盘 ==========" % character_id)
	print("等级: %d | 可用技能点: %d | 剩余跳跃: %d/%d" % [
		tree.character_level, tree.available_skill_points,
		tree.get_remaining_jumps(), tree.total_jumps])
	print("已点亮节点 (%d):" % tree.activated_nodes.size())
	for node_id in tree.activated_nodes:
		if tree_data.nodes.has(node_id):
			var node = tree_data.nodes[node_id]
			var type_str = ["小", "大", "Keystone", "启程"][node.node_type]
			print("  [%s] %s - %s" % [type_str, node.node_name, node.get_effect_text()])
	print("累计属性: ", tree.accumulated_stats)
	print("代价属性: ", tree.accumulated_costs)
	print("======================================")
