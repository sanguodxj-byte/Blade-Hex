# CharacterRenderNode.gd
# 角色渲染节点 — 封装单个角色的完整视觉表示
# 作为 Unit 的子节点，自动跟随位置
# 6层分部位渲染（头/头盔/身体/服装/手甲/武器），各有独立锚点
# 所有层统一使用 AnimatedSprite3D（单帧即静态，多帧即动画，无需销毁重建）
class_name CharacterRenderNode
extends Node3D


## =========================================
# 信号 — 供 Bus 中转或外部直接监听
## =========================================

signal hp_updated(current_hp: int, max_hp: int)
signal died
signal equipment_slot_changed(slot: int)


## =========================================
# 常量
## =========================================

const HP_BAR_WIDTH: float = 60.0
const HP_BAR_HEIGHT: float = 4.0
const HP_BAR_Y_GAP: float = 15.0
const HP_LABEL_PIXEL_SIZE: float = 3.0
const HP_LABEL_Y_GAP: float = 20.0
const SELECTION_RING_RADIUS: float = 40.0
const SELECTION_RING_HEIGHT: float = 5.0
const TURN_INDICATOR_Y_GAP: float = 10.0
const STATUS_ICON_SIZE: float = 16.0
const STATUS_ICON_SPACING: float = 20.0
const DEATH_FADE_DURATION: float = 1.0
const HIT_FLASH_DURATION: float = 0.5
const LAYER_NODE_PREFIX: String = "Layer_"


## =========================================
# 外部引用
## =========================================

var unit_ref: Unit = null


## =========================================
# 渲染层 — key = slot int, value = AnimatedSprite3D
## =========================================

var _layers: Dictionary = {}
var _body_root: Node3D = null

## HUD 元素
var _hp_label: Label3D = null
var _hp_bar_bg: MeshInstance3D = null
var _hp_bar_fg: MeshInstance3D = null
var _status_container: Node3D = null
var _selection_ring: MeshInstance3D = null
var _turn_indicator: MeshInstance3D = null

## 渲染状态
var _current_hp: int = 0
var _max_hp: int = 1
var _cached_body_height: float = 120.0
var _cached_pixel_size: float = 1.0
var _is_selected: bool = false
var _is_active_turn: bool = false
var _is_dead: bool = false


## =========================================
# 生命周期
## =========================================

func _ready() -> void:
	visible = false


## =========================================
# 初始化 — 由 Unit 在 _ready 中调用
## =========================================

func setup(unit: Unit) -> void:
	unit_ref = unit
	if not unit.data:
		push_warning("CharacterRenderNode.setup: Unit.data 为空")
		return

	_current_hp = unit.current_hp
	_max_hp = unit.get_max_hp()
	_is_dead = _current_hp <= 0

	_build_body_root()
	_build_all_layers()
	_load_equipment()
	_build_hud()

	visible = true


## =========================================
# 部位层构建
## =========================================

func _build_body_root() -> void:
	_body_root = Node3D.new()
	_body_root.name = "BodyRoot"
	add_child(_body_root)


func _build_all_layers() -> void:
	for cfg in EquipmentSlotConfig.get_all_sorted():
		_build_layer(cfg)
	_setup_body_content()


func _build_layer(cfg: EquipmentSlotConfig.SlotConfig) -> void:
	var sprite := AnimatedSprite3D.new()
	sprite.name = LAYER_NODE_PREFIX + EquipmentSlotConfig.get_slot_name(cfg.slot)
	sprite.pixel_size = cfg.pixel_size
	sprite.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	sprite.position = cfg.anchor_offset + Vector3(0, 0, cfg.sort_offset)
	sprite.visible = false
	_layers[cfg.slot] = sprite
	_body_root.add_child(sprite)


## 为 BODY 层加载基础内容（角色本体）
func _setup_body_content() -> void:
	var data: UnitData = unit_ref.data
	var sprite: AnimatedSprite3D = _layers[EquipmentSlotConfig.SLOT_BODY]

	if data.sprite_frames:
		sprite.sprite_frames = data.sprite_frames
		if sprite.sprite_frames.get_frame_count("default") > 0:
			var tex = sprite.sprite_frames.get_frame_texture("default", 0)
			if tex:
				_cached_body_height = tex.get_height()
		sprite.offset = Vector2(0, _cached_body_height / 2.0)
		sprite.play("default")
		sprite.visible = true
		_cached_pixel_size = sprite.pixel_size
		return

	# 没有帧动画时，创建单帧 SpriteFrames
	var frames := SpriteFrames.new()
	frames.add_animation("default")
	frames.set_animation_speed("default", 1.0)
	frames.set_animation_loop("default", true)

	if data.battle_sprite:
		frames.add_frame("default", data.battle_sprite)
		_cached_body_height = data.battle_sprite.get_height()
	else:
		# 占位符
		var tex := PlaceholderTexture2D.new()
		tex.size = Vector2(80, 120)
		frames.add_frame("default", tex)
		sprite.pixel_size = 1.5
		_cached_body_height = 120.0
		sprite.modulate = CharacterRenderBus.PLAYER_COLOR if not data.is_enemy else CharacterRenderBus.ENEMY_COLOR

	sprite.sprite_frames = frames
	sprite.offset = Vector2(0, _cached_body_height / 2.0)
	sprite.play("default")
	sprite.visible = true
	_cached_pixel_size = sprite.pixel_size


## =========================================
# 换装 API — 无销毁重建，直接替换 SpriteFrames / Texture
## =========================================

## 设置指定部位的外观纹理（复用已有 SpriteFrames，避免 GC 压力）
func set_slot_texture(slot: int, texture: Texture2D) -> void:
	if not _layers.has(slot):
		return
	var sprite: AnimatedSprite3D = _layers[slot]
	var frames: SpriteFrames = sprite.sprite_frames
	# 如果已有 SpriteFrames 且只有 default 动画，复用之
	if frames and frames.get_animation_names().size() == 1 and frames.has_animation("default"):
		frames.clear("default")
		frames.add_frame("default", texture)
	else:
		frames = SpriteFrames.new()
		frames.add_animation("default")
		frames.set_animation_speed("default", 1.0)
		frames.set_animation_loop("default", true)
		frames.add_frame("default", texture)
		sprite.sprite_frames = frames

	var cfg := EquipmentSlotConfig.get_config(slot)
	sprite.offset = Vector2(0, (texture.get_height() / 2.0) if texture else cfg.default_size.y / 2.0)
	sprite.play("default")
	sprite.visible = (texture != null)
	equipment_slot_changed.emit(slot)


## 设置指定部位的外观序列帧
func set_slot_frames(slot: int, frames: SpriteFrames) -> void:
	if not _layers.has(slot):
		return
	var sprite: AnimatedSprite3D = _layers[slot]
	sprite.sprite_frames = frames

	var cfg := EquipmentSlotConfig.get_config(slot)
	var tex_height := cfg.default_size.y
	if frames and frames.get_frame_count("default") > 0:
		var tex = frames.get_frame_texture("default", 0)
		if tex:
			tex_height = tex.get_height()
	sprite.offset = Vector2(0, tex_height / 2.0)
	sprite.play("default")
	sprite.visible = true
	equipment_slot_changed.emit(slot)


## 清除指定部位外观（BODY 层不可清除）
func clear_slot(slot: int) -> void:
	if not EquipmentSlotConfig.is_swappable(slot):
		return
	if not _layers.has(slot):
		return
	_layers[slot].visible = false


## 获取指定部位的渲染节点
func get_layer(slot: int) -> AnimatedSprite3D:
	return _layers.get(slot)


## =========================================
# 装备加载 — 单一映射，Bus 通过此接口驱动
## =========================================

## 从 UnitData 装备槽位加载所有外观
func _load_equipment() -> void:
	var data: UnitData = unit_ref.data
	# 遍历所有可换装部位，从 UnitData 对应字段取物品
	_apply_item_if_valid(data.helmet)
	_apply_item_if_valid(data.armor)
	_apply_item_if_valid(data.shield)
	var main_hand = data.primary_main_hand if unit_ref.using_primary_weapon else data.secondary_main_hand
	_apply_item_if_valid(main_hand)


func _apply_item_if_valid(item: ItemData) -> void:
	if not item:
		return
	if item.equip_sprite_frames:
		set_slot_frames(item.equip_slot_target, item.equip_sprite_frames)
	elif item.equip_texture:
		set_slot_texture(item.equip_slot_target, item.equip_texture)


## 全量刷新装备外观（重新从 UnitData 读取）
func refresh_all_equipment() -> void:
	if not unit_ref or not unit_ref.data:
		return
	# 先清除可换装层
	for slot in _layers:
		if EquipmentSlotConfig.is_swappable(slot):
			_layers[slot].visible = false
	_load_equipment()


## =========================================
# 动画 — 同步所有 AnimatedSprite3D 层
## =========================================

func play_animation(anim_name: String) -> void:
	if _is_dead:
		return
	for slot_key in _layers:
		var sprite: AnimatedSprite3D = _layers[slot_key]
		if not sprite.visible:
			continue
		if sprite.sprite_frames and sprite.sprite_frames.has_animation(anim_name):
			sprite.play(anim_name)
		elif sprite.sprite_frames and sprite.sprite_frames.has_animation("default"):
			sprite.play("default")


func play_hit() -> void:
	if _is_dead:
		return
	_flash_all(Color(1.5, 1.5, 1.5))
	play_animation("hit")
	_schedule(HIT_FLASH_DURATION, func(): if not _is_dead: play_animation("default"))


func play_death() -> void:
	_is_dead = true
	play_animation("die")
	_fade_all(DEATH_FADE_DURATION)
	for node in [_hp_bar_bg, _hp_bar_fg, _hp_label, _status_container]:
		if node:
			node.visible = false
	died.emit()


## =========================================
# HP 显示
## =========================================

func update_hp(current: int, maximum: int) -> void:
	_current_hp = current
	_max_hp = maxi(1, maximum)
	if _hp_label:
		_hp_label.text = "%d/%d" % [_current_hp, _max_hp]
	if _hp_bar_fg:
		var ratio := float(_current_hp) / float(_max_hp)
		var w := HP_BAR_WIDTH * ratio
		(_hp_bar_fg.mesh as QuadMesh).size = Vector2(maxf(0.1, w), HP_BAR_HEIGHT)
		_hp_bar_fg.position.x = -(HP_BAR_WIDTH - w) / 2.0
		var mat: StandardMaterial3D = _hp_bar_fg.material_override
		if mat:
			mat.albedo_color = (
				Color(0.2, 0.8, 0.2) if ratio > 0.6 else
				Color(0.9, 0.7, 0.1) if ratio > 0.3 else
				Color(0.9, 0.2, 0.1)
			)
	hp_updated.emit(_current_hp, _max_hp)


## =========================================
# 状态效果图标
## =========================================

func update_status_effects(effects: Array) -> void:
	if not _status_container:
		return
	for child in _status_container.get_children():
		child.queue_free()
	for i in range(effects.size()):
		var icon := _make_status_icon(effects[i], i)
		if icon:
			_status_container.add_child(icon)


## =========================================
# 选中 / 回合
## =========================================

func set_selected(on: bool) -> void:
	_is_selected = on
	if _selection_ring:
		_selection_ring.visible = on
		set_process(on)


func set_active_turn(on: bool) -> void:
	_is_active_turn = on
	if _turn_indicator:
		_turn_indicator.visible = on


## =========================================
# HUD 构建
## =========================================

func _build_hud() -> void:
	var top_y := _cached_body_height * _cached_pixel_size
	_build_hp_label(top_y)
	_build_hp_bar(top_y)
	_build_status_container(top_y)
	_build_selection_ring()
	_build_turn_indicator(top_y)
	update_hp(_current_hp, _max_hp)


func _build_hp_label(top_y: float) -> void:
	_hp_label = Label3D.new()
	_hp_label.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	_hp_label.pixel_size = HP_LABEL_PIXEL_SIZE
	_hp_label.font_size = 12
	_hp_label.position = Vector3(0, top_y + HP_LABEL_Y_GAP, 0)
	add_child(_hp_label)


func _build_hp_bar(top_y: float) -> void:
	var y := top_y + HP_BAR_Y_GAP
	_hp_bar_bg = _make_bar(HP_BAR_WIDTH, HP_BAR_HEIGHT, Vector3(0, y, 0), Color(0.2, 0.2, 0.2, 0.8))
	_hp_bar_fg = _make_bar(HP_BAR_WIDTH, HP_BAR_HEIGHT, Vector3(0, y, -0.1), Color(0.2, 0.8, 0.2))
	add_child(_hp_bar_bg)
	add_child(_hp_bar_fg)


func _build_status_container(top_y: float) -> void:
	_status_container = Node3D.new()
	_status_container.position = Vector3(0, top_y + HP_BAR_Y_GAP + HP_BAR_HEIGHT + 5.0, 0)
	add_child(_status_container)


func _build_selection_ring() -> void:
	_selection_ring = MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = SELECTION_RING_RADIUS
	cyl.bottom_radius = SELECTION_RING_RADIUS
	cyl.height = SELECTION_RING_HEIGHT
	_selection_ring.mesh = cyl
	_selection_ring.position = Vector3(0, SELECTION_RING_HEIGHT / 2.0, 0)
	var mat := StandardMaterial3D.new()
	mat.transparency = StandardMaterial3D.TRANSPARENCY_ALPHA
	mat.shading_mode = StandardMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = Color(1.0, 0.9, 0.2, 0.6)
	_selection_ring.material_override = mat
	_selection_ring.visible = false
	add_child(_selection_ring)


func _build_turn_indicator(top_y: float) -> void:
	_turn_indicator = MeshInstance3D.new()
	var quad := QuadMesh.new()
	quad.size = Vector2(12, 12)
	_turn_indicator.mesh = quad
	_turn_indicator.position = Vector3(0, top_y + HP_LABEL_Y_GAP + HP_LABEL_PIXEL_SIZE * 15.0 + TURN_INDICATOR_Y_GAP, 0)
	var mat := StandardMaterial3D.new()
	mat.shading_mode = StandardMaterial3D.SHADING_MODE_UNSHADED
	mat.billboard_mode = StandardMaterial3D.BILLBOARD_ENABLED
	mat.albedo_color = Color(0.2, 1.0, 0.4)
	_turn_indicator.material_override = mat
	_turn_indicator.visible = false
	add_child(_turn_indicator)


func _make_bar(w: float, h: float, pos: Vector3, color: Color) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var quad := QuadMesh.new()
	quad.size = Vector2(w, h)
	mi.mesh = quad
	mi.position = pos
	var mat := StandardMaterial3D.new()
	mat.transparency = StandardMaterial3D.TRANSPARENCY_ALPHA
	mat.shading_mode = StandardMaterial3D.SHADING_MODE_UNSHADED
	mat.billboard_mode = StandardMaterial3D.BILLBOARD_ENABLED
	mat.albedo_color = color
	mi.material_override = mat
	return mi


func _make_status_icon(effect, index: int) -> Sprite3D:
	var icon := Sprite3D.new()
	var tex := PlaceholderTexture2D.new()
	tex.size = Vector2(STATUS_ICON_SIZE, STATUS_ICON_SIZE)
	icon.texture = tex
	icon.pixel_size = 0.5
	icon.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	icon.offset = Vector2(0, STATUS_ICON_SIZE / 2.0)
	icon.position = Vector3((index - 2.0) * STATUS_ICON_SPACING, 0, 0)
	if effect is Dictionary:
		icon.modulate = _status_color(str(effect.get("id", "")))
	return icon


## =========================================
# 视觉效果 — 统一操作所有层
## =========================================

func _flash_all(color: Color) -> void:
	for slot in _layers:
		var s: AnimatedSprite3D = _layers[slot]
		if not s.visible: continue
		var orig := s.modulate
		s.modulate = color
		_schedule(0.1, func(): if is_instance_valid(s): s.modulate = orig)


func _fade_all(duration: float) -> void:
	for slot in _layers:
		var s: AnimatedSprite3D = _layers[slot]
		if not s.visible: continue
		var tw := create_tween()
		tw.tween_property(s, "modulate:a", 0.0, duration)
		tw.tween_callback(func(): if is_instance_valid(s): s.visible = false)


## =========================================
# _process — 仅选中时运行
## =========================================

func _process(delta: float) -> void:
	if not _is_selected or not _selection_ring:
		set_process(false)
		return
	var t := fmod(Time.get_ticks_msec() / 1000.0, 2.0)
	_selection_ring.scale = Vector3(1.0 + 0.1 * sin(t * TAU), 1.0, 1.0 + 0.1 * sin(t * TAU))
	if _turn_indicator and _is_active_turn:
		var base_y := _cached_body_height * _cached_pixel_size + HP_LABEL_Y_GAP + HP_LABEL_PIXEL_SIZE * 15.0 + TURN_INDICATOR_Y_GAP
		_turn_indicator.position.y = base_y + 3.0 * sin(t * PI * 3.0)


## =========================================
# 工具
## =========================================

func _schedule(delay: float, callback: Callable) -> void:
	var timer := get_tree().create_timer(delay)
	timer.timeout.connect(callback)


func _status_color(id: String) -> Color:
	match id:
		"burning":           return Color(1.0, 0.4, 0.1)
		"freeze", "frozen":  return Color(0.3, 0.6, 1.0)
		"poison", "poisoned": return Color(0.4, 0.8, 0.2)
		"entangled", "web":  return Color(0.6, 0.4, 0.2)
		"stun", "stunned":   return Color(0.9, 0.9, 0.2)
		"charmed":           return Color(1.0, 0.5, 0.8)
		"bleed", "bleeding": return Color(0.8, 0.1, 0.1)
		"shield", "magic_shield": return Color(0.3, 0.5, 1.0)
		"blessing":          return Color(1.0, 1.0, 0.7)
		_:                   return Color(0.7, 0.7, 0.7)
