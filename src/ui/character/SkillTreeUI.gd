# SkillTreeUI.gd
# 技能盘可视化UI - 六边形网格布局, 150+节点, 支持缩放平移
# 坐标转换委托给 SkillTreeCoord (axial → pixel)
# 状态查询通过 CharacterSkillTree（不修改共享 SkillTreeData）
extends PanelContainer
class_name SkillTreeUI

# ============================================================================
# 信号
# ============================================================================
signal node_clicked(node_id: String)
signal close_requested()

# ============================================================================
# 常量
# ============================================================================
const HEX_SIZE := 55.0
const REGION_ANGLES := {
	SkillNodeData.Region.STR: 0.0, SkillNodeData.Region.CHA: 60.0,
	SkillNodeData.Region.WIS: 120.0, SkillNodeData.Region.INT: 180.0,
	SkillNodeData.Region.CON: 240.0, SkillNodeData.Region.DEX: 300.0,
}
const REGION_NAMES := {
	SkillNodeData.Region.STR: "力量", SkillNodeData.Region.DEX: "敏捷",
	SkillNodeData.Region.CON: "体质", SkillNodeData.Region.INT: "智力",
	SkillNodeData.Region.WIS: "感知", SkillNodeData.Region.CHA: "魅力",
	SkillNodeData.Region.NONE: "中心", SkillNodeData.Region.TRANSITION: "过渡",
}


## 坐标转换组件（C# 类运行时加载）
var _SkillTreeCoord = load("res://src/core/skill_tree/SkillTreeCoord.cs")
var _coord = null

# ============================================================================
# 内部
# ============================================================================
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()
var _draw_container: Control
var _node_buttons: Dictionary = {}
var _node_positions: Dictionary = {}
var _character_tree: CharacterSkillTree = null
var _tree_data: SkillTreeData = null
var _center := Vector2(600, 500)
var _zoom := 1.0
var _pan_offset := Vector2.ZERO
var _is_panning := false
var _pan_start := Vector2.ZERO
var _info_panel: PanelContainer
var _info_title: Label
var _info_desc: RichTextLabel
var _info_activate_btn: Button
var _info_jump_btn: Button
var _selected_node_id: String = ""
var _stat_labels: Dictionary = {}

func _ready():
	_factory = UIFactory.new()
	_setup()
	visible = false

func _setup():
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_primary, _theme.border_magic, 2, _theme.radius_lg, 0))
	var root_margin = _factory.create_margin(20, 20, 15, 15)
	add_child(root_margin)
	var main_vbox := VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	root_margin.add_child(main_vbox)
	# === Header ===
	var header := HBoxContainer.new()
	header.add_theme_constant_override("separation", _theme.spacing_md)
	main_vbox.add_child(header)
	var title = _factory.create_title_label("技 能 盘", _theme.font_size_xxl)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)
	var sp_lbl = _factory.create_body_label("技能点: 0", _theme.text_accent)
	header.add_child(sp_lbl)
	_stat_labels["skill_points"] = sp_lbl
	var jmp_lbl = _factory.create_body_label("跳跃: 0", _theme.text_magic)
	header.add_child(jmp_lbl)
	_stat_labels["jumps"] = jmp_lbl
	var zoom_lbl = _factory.create_body_label("[滚轮缩放/右键拖拽]", _theme.text_muted)
	header.add_child(zoom_lbl)
	var close_btn = _factory.create_button("返回 (ESC)", Vector2(120, 36))
	close_btn.pressed.connect(func(): visible = false; close_requested.emit())
	header.add_child(close_btn)
	main_vbox.add_child(_factory.create_separator_h())
	# === Body ===
	var body := HBoxContainer.new()
	body.add_theme_constant_override("separation", _theme.spacing_lg)
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_vbox.add_child(body)
	# --- Left: hex grid ---
	var draw_panel := PanelContainer.new()
	draw_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	draw_panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	draw_panel.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_tertiary, _theme.border_default, 1, _theme.radius_md, 4))
	body.add_child(draw_panel)
	_draw_container = Control.new()
	_draw_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_draw_container.draw.connect(_on_draw)
	_draw_container.gui_input.connect(_on_draw_input)
	draw_panel.add_child(_draw_container)
	# --- Right: info panel ---
	_info_panel = _factory.create_panel(Vector2(260, 0), _theme.bg_secondary, _theme.border_magic)
	_info_panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body.add_child(_info_panel)
	var info_margin = _factory.create_margin(12, 12, 10, 10)
	_info_panel.add_child(info_margin)
	var info_vbox := VBoxContainer.new()
	info_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	info_margin.add_child(info_vbox)
	_info_title = _factory.create_title_label("选择节点", _theme.font_size_lg)
	info_vbox.add_child(_info_title)
	info_vbox.add_child(_factory.create_separator_h(_theme.border_magic))
	_info_desc = _factory.create_rich_text(Vector2(220, 0))
	_info_desc.size_flags_vertical = Control.SIZE_EXPAND_FILL
	info_vbox.add_child(_info_desc)
	_info_activate_btn = _factory.create_button("点亮节点", Vector2(0, 40))
	_info_activate_btn.disabled = true
	_info_activate_btn.pressed.connect(_on_activate_pressed)
	info_vbox.add_child(_info_activate_btn)
	_info_jump_btn = _factory.create_button("跳跃点亮", Vector2(0, 40))
	_info_jump_btn.disabled = true
	_info_jump_btn.pressed.connect(_on_jump_pressed)
	info_vbox.add_child(_info_jump_btn)

# ============================================================================
# Hex 坐标转换 — 委托给 SkillTreeCoord
# ============================================================================

func _ensure_coord():
	if not _coord:
		_coord = _SkillTreeCoord.new()
		_coord.hex_size = HEX_SIZE

func _node_to_pixel(node: SkillNodeData) -> Vector2:
	_ensure_coord()
	var px = _coord.hex_to_pixel(node.grid_position.x, node.grid_position.y)
	return _center + px * _zoom + _pan_offset

# ============================================================================
# 缩放平移
# ============================================================================

func _on_draw_input(event: InputEvent):
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			_zoom = minf(_zoom * 1.1, 2.0)
			_rebuild_positions()
			_draw_container.queue_redraw()
			accept_event()
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			_zoom = maxf(_zoom * 0.9, 0.3)
			_rebuild_positions()
			_draw_container.queue_redraw()
			accept_event()
		elif event.button_index == MOUSE_BUTTON_RIGHT:
			_is_panning = event.pressed
			_pan_start = event.position
			accept_event()
	elif event is InputEventMouseMotion and _is_panning:
		_pan_offset += event.position - _pan_start
		_pan_start = event.position
		_rebuild_positions()
		_draw_container.queue_redraw()

# ============================================================================
# 公开接口
# ============================================================================

func open_skill_tree(character_tree: CharacterSkillTree, tree_data: SkillTreeData):
	_character_tree = character_tree
	_tree_data = tree_data
	_zoom = 1.0
	_pan_offset = Vector2.ZERO
	_coord = null  # 重置坐标组件
	visible = true
	_build_or_update_nodes()
	_update_stats()

func close_skill_tree():
	visible = false
	_character_tree = null
	_tree_data = null

# ============================================================================
# 节点构建 — 首次创建，后续只更新样式和位置
# ============================================================================

func _build_or_update_nodes():
	if not _tree_data: return
	_center = _draw_container.size / 2.0
	if _center == Vector2.ZERO: _center = Vector2(500, 400)

	var existing_ids = {}
	for key in _node_buttons:
		existing_ids[key] = true

	# Create new buttons for nodes not yet built
	for node_id in _tree_data.nodes:
		var node: SkillNodeData = _tree_data.nodes[node_id]
		var pos = _node_to_pixel(node)
		_node_positions[node_id] = pos
		if not existing_ids.has(node_id):
			_create_node_button(node_id, node, pos)

	# Update all button styles and positions
	_update_all_button_states()
	_draw_container.queue_redraw()

func _rebuild_positions():
	if not _tree_data: return
	for node_id in _tree_data.nodes:
		var node: SkillNodeData = _tree_data.nodes[node_id]
		var pos = _node_to_pixel(node)
		_node_positions[node_id] = pos
	_update_button_positions()

func _create_node_button(node_id: String, node: SkillNodeData, pos: Vector2):
	var btn := Button.new()
	btn.position = pos - Vector2(HEX_SIZE * 0.5, HEX_SIZE * 0.3) * _zoom
	btn.size = Vector2(HEX_SIZE, HEX_SIZE * 0.6) * _zoom
	btn.text = node.node_name.left(4)
	btn.tooltip_text = "%s\n%s" % [node.node_name, node.description]
	_style_btn(btn, node, false, false)
	btn.pressed.connect(_on_node_clicked.bind(node_id))
	_draw_container.add_child(btn)
	_node_buttons[node_id] = btn

func _update_all_button_states():
	for node_id in _node_buttons:
		if not _tree_data.nodes.has(node_id): continue
		var btn: Button = _node_buttons[node_id]
		if not is_instance_valid(btn): continue
		var node: SkillNodeData = _tree_data.nodes[node_id]
		var act = _character_tree.is_activated(node_id) if _character_tree else false
		var avail = _character_tree.is_available(node_id) if _character_tree else false
		_style_btn(btn, node, act, avail)

func _update_button_positions():
	for node_id in _node_buttons:
		var btn: Button = _node_buttons[node_id]
		if not is_instance_valid(btn): continue
		if not _node_positions.has(node_id): continue
		var pos = _node_positions[node_id]
		btn.position = pos - Vector2(HEX_SIZE * 0.5, HEX_SIZE * 0.3) * _zoom
		btn.size = Vector2(HEX_SIZE, HEX_SIZE * 0.6) * _zoom

func _style_btn(btn: Button, node: SkillNodeData, activated: bool, available: bool):
	var rc = _theme.get_region_color(node.region as int)
	if activated:
		var s = _theme.make_button_style(
			Color(rc.r * 0.3, rc.g * 0.3, rc.b * 0.3), rc,
			Color(rc.r * 0.2, rc.g * 0.2, rc.b * 0.2),
			Color(rc.r * 0.15, rc.g * 0.15, rc.b * 0.15), _theme.radius_md)
		_theme.apply_button_theme(btn, s)
		btn.add_theme_color_override("font_color", rc)
	elif available:
		var s = _theme.make_button_style(
			Color(0.2, 0.2, 0.25), _theme.border_highlight,
			Color(0.25, 0.25, 0.3), Color(0.15, 0.15, 0.18), _theme.radius_md)
		_theme.apply_button_theme(btn, s)
		btn.add_theme_color_override("font_color", _theme.text_primary)
	else:
		var s = _theme.make_button_style(
			Color(0.1, 0.1, 0.12, 0.7), Color(0.2, 0.2, 0.22, 0.5),
			Color(0.12, 0.12, 0.14, 0.7), Color(0.08, 0.08, 0.1, 0.5), _theme.radius_md)
		_theme.apply_button_theme(btn, s)
		btn.add_theme_color_override("font_color", _theme.text_muted)
	if node.node_type == SkillNodeData.NodeType.BIG or node.node_type == SkillNodeData.NodeType.KEYSTONE:
		btn.custom_minimum_size = Vector2(HEX_SIZE * 1.1, HEX_SIZE * 0.7) * _zoom

# ============================================================================
# 绘制
# ============================================================================

func _on_draw():
	if not _tree_data: return
	# Draw hex cells
	for node_id in _tree_data.nodes:
		if not _node_positions.has(node_id): continue
		var pos = _node_positions[node_id]
		var node: SkillNodeData = _tree_data.nodes[node_id]
		var act = _character_tree.is_activated(node_id) if _character_tree else false
		var avail = _character_tree.is_available(node_id) if _character_tree else false
		var rc = _theme.get_region_color(node.region as int)
		var color: Color
		if act:
			color = Color(rc.r, rc.g, rc.b, 0.25)
		elif avail:
			color = Color(0.25, 0.25, 0.3, 0.2)
		else:
			color = Color(0.08, 0.08, 0.1, 0.15)
		_draw_hex(pos, HEX_SIZE * 0.45 * _zoom, color)
	# Draw connections
	for node_id in _tree_data.nodes:
		var node: SkillNodeData = _tree_data.nodes[node_id]
		if not _node_positions.has(node_id): continue
		var from = _node_positions[node_id]
		for nid in node.neighbors:
			if not _node_positions.has(nid): continue
			if nid < node_id: continue  # avoid double draw
			var to = _node_positions[nid]
			var fa = _character_tree.is_activated(node_id) if _character_tree else false
			var ta = _character_tree.is_activated(nid) if _character_tree else false
			var both = fa and ta
			var c = _theme.border_highlight if both else _theme.border_default
			var w = 2.0 if both else 1.0
			_draw_container.draw_line(from, to, c, w)
	# Draw region labels
	_ensure_coord()
	var label_dirs := {
		SkillNodeData.Region.STR: Vector2i(10, 0),
		SkillNodeData.Region.DEX: Vector2i(0, 10),
		SkillNodeData.Region.CON: Vector2i(-10, 10),
		SkillNodeData.Region.INT: Vector2i(-10, 0),
		SkillNodeData.Region.WIS: Vector2i(0, -10),
		SkillNodeData.Region.CHA: Vector2i(10, -10),
	}
	for rv in REGION_NAMES:
		if not label_dirs.has(rv):
			continue
		var lp = _center + _coord.hex_to_pixel(label_dirs[rv].x, label_dirs[rv].y) * _zoom + _pan_offset
		var rn = REGION_NAMES.get(rv, "")
		var col = _theme.get_region_color(rv as int)
		_draw_container.draw_string(ThemeDB.fallback_font, lp - Vector2(20, 0), rn,
			HORIZONTAL_ALIGNMENT_CENTER, -1, 16, col)

func _draw_hex(center: Vector2, size: float, color: Color):
	var pts = []
	for i in range(6):
		var a = PI / 3.0 * i
		pts.append(center + Vector2(cos(a), sin(a)) * size)
	_draw_container.draw_colored_polygon(pts, color)

# ============================================================================
# 事件处理
# ============================================================================

func _on_node_clicked(node_id: String):
	_selected_node_id = node_id
	_update_info_panel()

func _on_activate_pressed():
	if _character_tree and _selected_node_id != "":
		var r = _character_tree.try_activate_node(_selected_node_id)
		if r.success: _refresh(r.message)
		else: _info_desc.text = "[color=red]%s[/color]" % r.message

func _on_jump_pressed():
	if _character_tree and _selected_node_id != "":
		var r = _character_tree.try_jump_activate(_selected_node_id)
		if r.success: _refresh(r.message)
		else: _info_desc.text = "[color=red]%s[/color]" % r.message

func _refresh(msg: String):
	_update_all_button_states()
	_draw_container.queue_redraw()
	_update_stats()
	_update_info_panel()
	_info_desc.text += "\n[color=green]%s[/color]" % msg

func _update_info_panel():
	if not _tree_data or not _selected_node_id:
		_info_title.text = "选择节点"
		_info_desc.text = ""
		_info_activate_btn.disabled = true
		_info_jump_btn.disabled = true
		return
	var node: SkillNodeData = _tree_data.nodes.get(_selected_node_id, null)
	if not node: return
	_info_title.text = node.node_name
	_info_title.add_theme_color_override("font_color", _theme.get_region_color(node.region as int))
	var tn := "小节点(属性)"
	if node.node_type == SkillNodeData.NodeType.BIG: tn = "大节点(技能)"
	elif node.node_type == SkillNodeData.NodeType.KEYSTONE: tn = "Keystone(代价)"
	elif node.node_type == SkillNodeData.NodeType.START: tn = "启程"
	var d := ""
	d += "[color=gray]区域:[/color] %s\n" % REGION_NAMES.get(node.region, "中心")
	d += "[color=gray]类型:[/color] %s\n" % tn
	if node.required_level > 0:
		d += "[color=gray]需要等级:[/color] %d\n" % node.required_level
	d += "[color=gray]效果:[/color] %s\n" % node.get_effect_text()
	if node.keystone_cost != "":
		d += "[color=red]代价:[/color] %s\n" % node.keystone_cost
	if node.neighbors.size() > 0:
		var nn: Array[String] = []
		for nid in node.neighbors:
			if _tree_data.nodes.has(nid):
				var n2: SkillNodeData = _tree_data.nodes[nid]
				var st = "*" if _character_tree and _character_tree.is_activated(nid) else "o"
				nn.append("%s %s" % [st, n2.node_name])
		d += "\n[color=gray]相邻:[/color] %s" % "、".join(nn)
	var activated = _character_tree and _character_tree.is_activated(_selected_node_id)
	if activated:
		d += "\n\n[color=green]已点亮[/color]"
	_info_desc.text = d
	var can_normal = not activated and _character_tree and _character_tree.is_available(_selected_node_id) and _character_tree.available_skill_points > 0
	var can_jump = not activated and _character_tree and _character_tree.get_remaining_jumps() > 0 and _character_tree.available_skill_points > 0
	if can_jump and _tree_data and _tree_data.nodes.has(_selected_node_id):
		var sn: SkillNodeData = _tree_data.nodes[_selected_node_id]
		if sn.required_level > _character_tree.character_level: can_jump = false
		for p in sn.prerequisites:
			if not _character_tree.is_activated(p): can_jump = false
	_info_activate_btn.disabled = not can_normal
	_info_jump_btn.disabled = not can_jump

func _update_stats():
	if not _character_tree: return
	if _stat_labels.has("skill_points"):
		_stat_labels["skill_points"].text = "技能点: %d" % _character_tree.available_skill_points
	if _stat_labels.has("jumps"):
		_stat_labels["jumps"].text = "跳跃: %d/%d" % [_character_tree.get_remaining_jumps(), _character_tree.total_jumps]

func _unhandled_input(event):
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		if visible:
			visible = false
			close_requested.emit()
			get_viewport().set_input_as_handled()
