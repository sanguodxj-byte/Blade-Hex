# CharacterRenderBus.gd
# 角色渲染树总线 — 纯注册簿 + 信号广播层
# 职责: 管理 Unit→RenderNode 映射、选中管理、信号转发
# 不持有渲染节点（渲染节点是 Unit 的子节点，自动跟随位置）
class_name CharacterRenderBus
extends Node


## =========================================
# 全局视觉常量
## =========================================

const DEFAULT_PIXEL_SIZE: float = 1.0
const PLACEHOLDER_TEXTURE_SIZE := Vector2(80, 120)
const PLAYER_COLOR := Color(0.2, 0.5, 1.0)
const ENEMY_COLOR := Color(1.0, 0.2, 0.2)
const DEFAULT_TEX_HEIGHT: float = 120.0


## =========================================
# 对外信号 — 统一用 Unit 做参数（UI 不需要知道 RenderNode）
## =========================================

signal unit_selected(unit: Unit)
signal unit_deselected(unit: Unit)
signal unit_hp_changed(unit: Unit, current_hp: int, max_hp: int)
signal unit_died(unit: Unit)
signal unit_animation_played(unit: Unit, anim_name: String)
signal unit_status_effects_changed(unit: Unit, effects: Array)
signal unit_equipment_changed(unit: Unit, slot: int)
signal turn_changed(active_unit: Unit)


## =========================================
# 注册簿 — key = Unit instance ID, value = CharacterRenderNode
## =========================================

var _registry: Dictionary = {}
var _selected_unit: Unit = null


## =========================================
# 注册 / 注销
## =========================================

## 注册 Unit 和它的 RenderNode 到总线
func register(unit: Unit, render_node: CharacterRenderNode) -> bool:
	if unit == null or render_node == null:
		push_warning("CharacterRenderBus.register: 参数为空")
		return false

	var uid: int = unit.get_instance_id()
	if _registry.has(uid):
		unregister(unit)

	_registry[uid] = render_node

	# 转发 RenderNode 的信号
	render_node.hp_updated.connect(_on_hp_updated.bind(unit))
	render_node.died.connect(_on_died.bind(unit))
	render_node.equipment_slot_changed.connect(_on_equip_changed.bind(unit))

	return true


## 注销
func unregister(unit: Unit) -> void:
	if unit == null:
		return
	var uid: int = unit.get_instance_id()
	if not _registry.has(uid):
		return

	if _selected_unit == unit:
		deselect_all()

	var render_node: CharacterRenderNode = _registry[uid]
	if is_instance_valid(render_node):
		if render_node.hp_updated.is_connected(_on_hp_updated):
			render_node.hp_updated.disconnect(_on_hp_updated)
		if render_node.died.is_connected(_on_died):
			render_node.died.disconnect(_on_died)
		if render_node.equipment_slot_changed.is_connected(_on_equip_changed):
			render_node.equipment_slot_changed.disconnect(_on_equip_changed)

	_registry.erase(uid)


## 查询
func get_render_node(unit: Unit) -> CharacterRenderNode:
	if unit == null:
		return null
	return _registry.get(unit.get_instance_id())


func get_all_render_nodes() -> Array[CharacterRenderNode]:
	var result: Array[CharacterRenderNode] = []
	for node in _registry.values():
		if is_instance_valid(node):
			result.append(node)
	return result


func get_count() -> int:
	return _registry.size()


## 场景切换时清除
func clear_all() -> void:
	deselect_all()
	for uid in _registry.keys():
		var node: CharacterRenderNode = _registry[uid]
		if is_instance_valid(node):
			if node.hp_updated.is_connected(_on_hp_updated):
				node.hp_updated.disconnect(_on_hp_updated)
			if node.died.is_connected(_on_died):
				node.died.disconnect(_on_died)
			if node.equipment_slot_changed.is_connected(_on_equip_changed):
				node.equipment_slot_changed.disconnect(_on_equip_changed)
	_registry.clear()


## =========================================
# 选中管理
## =========================================

func select(unit: Unit) -> void:
	deselect_all()
	var node := get_render_node(unit)
	if node:
		_selected_unit = unit
		node.set_selected(true)
		unit_selected.emit(unit)


func deselect_all() -> void:
	if _selected_unit and is_instance_valid(_selected_unit):
		var node := get_render_node(_selected_unit)
		if node:
			node.set_selected(false)
		var old := _selected_unit
		_selected_unit = null
		unit_deselected.emit(old)


func get_selected_unit() -> Unit:
	return _selected_unit


## =========================================
# 批量操作
## =========================================

func refresh_all_hp() -> void:
	for node in _registry.values():
		if is_instance_valid(node) and node.unit_ref:
			node.update_hp(node.unit_ref.current_hp, node.unit_ref.get_max_hp())


func refresh_all_status() -> void:
	for node in _registry.values():
		if is_instance_valid(node):
			node.update_status_effects(node.unit_ref.data.active_status_effects if node.unit_ref and node.unit_ref.data else [])


func play_anim_all(anim_name: String) -> void:
	for node in _registry.values():
		if is_instance_valid(node):
			node.play_animation(anim_name)


## =========================================
# 通知接口 — 供 CombatManager / Unit 调用
## =========================================

## 通知某单位受击
func notify_hit(unit: Unit) -> void:
	var node := get_render_node(unit)
	if node:
		node.play_hit()


## 通知某单位死亡
func notify_death(unit: Unit) -> void:
	var node := get_render_node(unit)
	if node:
		node.play_death()


## 通知某单位播放技能动画
func notify_animation(unit: Unit, anim_name: String) -> void:
	var node := get_render_node(unit)
	if node:
		node.play_animation(anim_name)
		unit_animation_played.emit(unit, anim_name)


## 通知回合切换
func notify_turn_changed(active_unit: Unit) -> void:
	for node in _registry.values():
		if is_instance_valid(node):
			node.set_active_turn(false)
	if active_unit:
		var node := get_render_node(active_unit)
		if node:
			node.set_active_turn(true)
	turn_changed.emit(active_unit)


## 通知装备全量刷新
func notify_equipment_refresh(unit: Unit) -> void:
	var node := get_render_node(unit)
	if node:
		node.refresh_all_equipment()


## 通知状态效果变化
func notify_status_effects(unit: Unit, effects: Array) -> void:
	var node := get_render_node(unit)
	if node:
		node.update_status_effects(effects)
		unit_status_effects_changed.emit(unit, effects)


## =========================================
# RenderNode 信号转发
## =========================================

func _on_hp_updated(current: int, maximum: int, unit: Unit) -> void:
	unit_hp_changed.emit(unit, current, maximum)


func _on_died(unit: Unit) -> void:
	unit_died.emit(unit)


func _on_equip_changed(slot: int, unit: Unit) -> void:
	unit_equipment_changed.emit(unit, slot)
