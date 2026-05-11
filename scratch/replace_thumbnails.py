import re

with open('src/ui/combat/CombatUI.gd', 'r', encoding='utf-8') as f:
    content = f.read()

replacement = """
func _find_ally_list() -> HBoxContainer:
	return find_child("AllyList", true, false) as HBoxContainer

func _find_enemy_list() -> HBoxContainer:
	return find_child("EnemyList", true, false) as HBoxContainer

# ============================================================================
# 缩略图生成器 (盟友/敌人通用)
# ============================================================================

func _create_thumbnail_entry(unit: Unit, is_enemy: bool) -> PanelContainer:
	var entry := PanelContainer.new()
	entry.set_meta("unit_ref", unit)
	entry.custom_minimum_size = Vector2(60, 70)
	
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.1, 0.1, 0.8)
	style.set_border_width_all(2)
	style.border_color = _theme.border_negative if is_enemy else _theme.border_friendly
	style.set_corner_radius_all(8)
	entry.add_theme_stylebox_override("panel", style)
	
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 2)
	vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	entry.add_child(vbox)
	
	# 头像占位 (可用 TextureRect 替换)
	var avatar := ColorRect.new()
	avatar.custom_minimum_size = Vector2(40, 40)
	avatar.color = Color(0.4, 0.2, 0.2) if is_enemy else Color(0.2, 0.4, 0.6)
	avatar.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	vbox.add_child(avatar)
	
	var name_lbl = Label.new()
	name_lbl.text = unit.data.unit_name.substr(0, 3) # 缩略名
	name_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_lbl.add_theme_font_size_override("font_size", _theme.font_size_xs)
	vbox.add_child(name_lbl)
	
	# 小 HP 条
	var max_hp = unit.get_max_hp()
	var hp_bar := ProgressBar.new()
	hp_bar.custom_minimum_size = Vector2(50, 6)
	hp_bar.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	hp_bar.show_percentage = false
	hp_bar.min_value = 0
	hp_bar.max_value = max_hp
	hp_bar.value = unit.current_hp
	_theme.apply_bar_theme(hp_bar, _theme.get_hp_color(float(unit.current_hp) / float(max(max_hp, 1))), _theme.hp_bar_bg)
	vbox.add_child(hp_bar)
	
	# 点击切换选中 (仅限盟友或用来查看敌人)
	entry.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT:
			if not is_enemy:
				unit_selected_in_list.emit(unit)
	)
	if not is_enemy:
		entry.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	
	return entry

func _update_thumbnail_entry(entry: PanelContainer, unit: Unit):
	if not is_instance_valid(unit) or not unit.data:
		return
	var vbox = entry.get_child(0) if entry.get_child_count() > 0 else null
	if not vbox or vbox.get_child_count() < 3:
		return
	var hp_bar: ProgressBar = vbox.get_child(2)
	var max_hp = unit.get_max_hp()
	hp_bar.max_value = max_hp
	hp_bar.value = unit.current_hp
	_theme.apply_bar_theme(hp_bar, _theme.get_hp_color(float(unit.current_hp) / float(max(max_hp, 1))), _theme.hp_bar_bg)

# ============================================================================
# 公开接口 — 友方
# ============================================================================

func register_ally(unit: Unit):
	var ally_list_node = _find_ally_list()
	if not ally_list_node: return
	for child in ally_list_node.get_children():
		if child.has_meta("unit_ref") and child.get_meta("unit_ref") == unit:
			return
	var entry = _create_thumbnail_entry(unit, false)
	ally_list_node.add_child(entry)

func remove_ally(unit: Unit):
	if not unit: return
	var ally_list_node = _find_ally_list()
	if not ally_list_node: return
	for child in ally_list_node.get_children():
		if child.has_meta("unit_ref") and child.get_meta("unit_ref") == unit:
			ally_list_node.remove_child(child)
			child.queue_free()
			return

func refresh_ally_list():
	var ally_list_node = _find_ally_list()
	if not ally_list_node: return
	for child in ally_list_node.get_children():
		var unit_ref = child.get_meta("unit_ref", null)
		if is_instance_valid(unit_ref):
			_update_thumbnail_entry(child, unit_ref)

# ============================================================================
# 公开接口 — 敌方
# ============================================================================

func register_enemy(unit: Unit):
	enemy_info_panel.add_enemy(unit)
	var enemy_list_node = _find_enemy_list()
	if not enemy_list_node: return
	for child in enemy_list_node.get_children():
		if child.has_meta("unit_ref") and child.get_meta("unit_ref") == unit:
			return
	var entry = _create_thumbnail_entry(unit, true)
	enemy_list_node.add_child(entry)

func remove_enemy(unit: Unit):
	enemy_info_panel.remove_enemy(unit)
	if not unit: return
	var enemy_list_node = _find_enemy_list()
	if not enemy_list_node: return
	for child in enemy_list_node.get_children():
		if child.has_meta("unit_ref") and child.get_meta("unit_ref") == unit:
			enemy_list_node.remove_child(child)
			child.queue_free()
			return

func update_enemy_info(unit: Unit):
	enemy_info_panel.update_enemy(unit)
	var enemy_list_node = _find_enemy_list()
	if not enemy_list_node: return
	for child in enemy_list_node.get_children():
		if child.has_meta("unit_ref") and child.get_meta("unit_ref") == unit:
			_update_thumbnail_entry(child, unit)

func highlight_enemy(unit: Unit, highlighted: bool):
	enemy_info_panel.highlight_enemy(unit, highlighted)
	# 可选：在敌方缩略图上加高亮特效
"""

pattern = re.compile(r'func _find_ally_list\(\) -> VBoxContainer:.*?func highlight_enemy\(unit: Unit, highlighted: bool\):\n\tenemy_info_panel\.highlight_enemy\(unit, highlighted\)', re.DOTALL)
new_content = pattern.sub(replacement, content)

with open('src/ui/combat/CombatUI.gd', 'w', encoding='utf-8') as f:
    f.write(new_content)
print('Replaced thumbnail list logic in CombatUI.gd')
