# HitPreviewTooltip.gd
# 命中率预览浮窗 - 悬停敌方时显示命中率%、预计伤害范围、优势/劣势原因
# 核心设计原则：不暴露骰子术语(d20)，只显示概率和直观信息
extends PanelContainer
class_name HitPreviewTooltip

var hit_label: Label
var dmg_label: Label
var advantage_label: RichTextLabel
var details_label: RichTextLabel

## 显示额外信息的区域（掩体/高程等）
var context_label: RichTextLabel

const BG_COLOR := Color(0.06, 0.05, 0.09, 0.95)
const BORDER_COLOR := Color(0.5, 0.4, 0.2, 0.8)
const HIT_COLOR := Color(0.3, 0.85, 0.3)
const MISS_COLOR := Color(0.85, 0.3, 0.3)
const ADVANTAGE_COLOR := Color(0.3, 0.85, 0.9)
const DISADVANTAGE_COLOR := Color(0.9, 0.5, 0.2)
const NEUTRAL_COLOR := Color(0.7, 0.7, 0.7)

func _ready():
	_setup_tooltip()
	visible = false

func _setup_tooltip():
	# 面板样式
	var style := StyleBoxFlat.new()
	style.bg_color = BG_COLOR
	style.set_border_width_all(2)
	style.border_color = BORDER_COLOR
	style.set_corner_radius_all(4)
	style.set_content_margin_all(8)
	add_theme_stylebox_override("panel", style)
	
	# 确保始终在最上层
	z_index = 100
	
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 3)
	add_child(vbox)
	
	# 命中率
	hit_label = Label.new()
	hit_label.add_theme_font_size_override("font_size", 15)
	vbox.add_child(hit_label)
	
	# 预计伤害
	dmg_label = Label.new()
	dmg_label.add_theme_font_size_override("font_size", 13)
	dmg_label.add_theme_color_override("font_color", Color(0.9, 0.75, 0.5))
	vbox.add_child(dmg_label)
	
	# 分隔线
	var sep := HSeparator.new()
	sep.add_theme_stylebox_override("separator", _make_line_style(Color(0.4, 0.35, 0.2, 0.5)))
	vbox.add_child(sep)
	
	# 优势/劣势原因
	advantage_label = RichTextLabel.new()
	advantage_label.bbcode_enabled = true
	advantage_label.custom_minimum_size = Vector2(180, 0)
	advantage_label.fit_content = true
	advantage_label.scroll_active = false
	vbox.add_child(advantage_label)
	
	# 详细信息（掩体、高程、武器等）
	details_label = RichTextLabel.new()
	details_label.bbcode_enabled = true
	details_label.custom_minimum_size = Vector2(180, 0)
	details_label.fit_content = true
	details_label.scroll_active = false
	vbox.add_child(details_label)
	
	# 鼠标穿透（避免阻挡其他交互）
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func _make_line_style(color: Color) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = color
	s.set_content_margin_all(1)
	return s


## 显示预览信息
## attacker: 攻击方单位 attacker_weapon: 当前武器 target: 防御方
## cover_type: 掩体等级 (0=无, 1=半掩体, 2=全掩体)
## elevation_diff: 高程差 (正=攻击者在高处)
## has_flanking: 是否包夹
## has_sneak: 是否伏击
func show_preview(attacker: Unit, target: Unit, cover_type: int = 0, elevation_diff: int = 0, has_flanking: bool = false, has_sneak: bool = false):
	if not attacker or not target or not attacker.data or not target.data:
		return
	
	visible = true
	
	var weapon: WeaponData = attacker.get_main_hand()
	var atk_bonus: int = attacker.get_attack_bonus()
	var target_ac: int = target.get_ac()
	
	# === 收集优势/劣势因素 ===
	var advantages: Array[String] = []
	var disadvantages: Array[String] = []
	
	# 高程优势
	if elevation_diff > 0:
		advantages.append("占据高地")
		atk_bonus += 1  # 高地加成
	elif elevation_diff < 0:
		disadvantages.append("仰攻不利")
	
	# 包夹优势
	if has_flanking:
		advantages.append("包夹攻击")
	
	# 伏击优势
	if has_sneak:
		advantages.append("伏击!")
	
	# 掩体劣势（仅远程）
	if cover_type > 0 and weapon and weapon.is_ranged:
		if cover_type == 1:
			disadvantages.append("半掩体阻挡")
			target_ac += 2
		elif cover_type == 2:
			disadvantages.append("全掩体阻挡")
			# 全掩体不可被远程攻击
			hit_label.text = "不可攻击"
			hit_label.add_theme_color_override("font_color", Color(0.5, 0.5, 0.5))
			dmg_label.text = "目标完全隐蔽"
			advantage_label.text = ""
			details_label.text = "[color=gray]全掩体单位不可被远程攻击[/color]"
			return
	
	# 目标低HP状态（轻伤/重伤惩罚）
	if target.current_hp > 0:
		var hp_ratio := float(target.current_hp) / float(max(target.get_max_hp(), 1))
		if hp_ratio < 0.25:
			advantages.append("目标重伤")
		elif hp_ratio < 0.5:
			advantages.append("目标轻伤")
	
	# 士气影响（攻击方士气低 → 失误率增加）
	if attacker.data.is_enemy and attacker.data.morale <= -40:
		disadvantages.append("士气崩溃")
	
	# === 计算命中率 ===
	# 使用 CombatResolver 统一计算（含擦伤、优势劣势）
	var hit_chance := CombatResolver.get_hit_chance_preview(attacker, target, null) * 100.0
	hit_chance = clampf(hit_chance, 5.0, 95.0)
	
	# 优势/劣势修正（约 ±25%）
	if advantages.size() > disadvantages.size():
		hit_chance = minf(hit_chance + 25.0, 95.0)
	elif disadvantages.size() > advantages.size():
		hit_chance = maxf(hit_chance - 25.0, 5.0)
	
	# === 计算预计伤害范围 ===
	# 基础武器骰 + STR修正 + 等级Nd20
	var str_mod := attacker.get_stat_modifier(attacker.data.str)
	var level_extra := 0
	if attacker.data:
		level_extra = RPGRuleEngine.get_damage_dice_count(attacker.data.level) - 1
	
	var min_dmg: int = 1
	var max_dmg: int = 1
	if weapon:
		min_dmg = weapon.damage_dice_count + str_mod
		max_dmg = weapon.damage_dice_count * weapon.damage_dice_sides + str_mod
		# 等级Nd20: 最少每骰1点，最多每骰20点
		min_dmg += level_extra * 1
		max_dmg += level_extra * 20
		min_dmg = max(1, min_dmg)
		max_dmg = max(min_dmg, max_dmg)
	else:
		# 徒手：1d20 + 等级Nd20 + STR
		min_dmg = 1 + level_extra * 1 + str_mod
		max_dmg = 20 + level_extra * 20 + str_mod
		min_dmg = max(1, min_dmg)
		max_dmg = max(min_dmg, max_dmg)
	
	# 包夹伤害加成
	if has_flanking:
		max_dmg = int(max_dmg * 1.25)
	
	# === 更新UI显示 ===
	
	# 命中率颜色
	var hit_color: Color
	if hit_chance >= 75:
		hit_color = HIT_COLOR
	elif hit_chance >= 50:
		hit_color = Color(0.7, 0.8, 0.3)
	elif hit_chance >= 25:
		hit_color = Color(0.85, 0.65, 0.2)
	else:
		hit_color = MISS_COLOR
	
	hit_label.text = "命中率: %d%%" % roundi(hit_chance)
	hit_label.add_theme_color_override("font_color", hit_color)
	
	dmg_label.text = "预计伤害: %d - %d" % [min_dmg, max_dmg]
	
	# 优势/劣势文本
	var adv_text := ""
	for a in advantages:
		adv_text += "[color=%s]▲ %s[/color]\n" % [ADVANTAGE_COLOR.to_html(false), a]
	for d in disadvantages:
		adv_text += "[color=%s]▼ %s[/color]\n" % [DISADVANTAGE_COLOR.to_html(false), d]
	advantage_label.text = adv_text.strip_edges()
	
	# 详细信息
	var detail_text := ""
	var weapon_name := weapon.item_name if weapon else "徒手"
	var weapon_range := weapon.range_cells if weapon else 1
	var is_ranged := weapon.is_ranged if weapon else false
	
	detail_text += "[color=gray]武器: %s (%s)[/color]\n" % [weapon_name, "远程" if is_ranged else "近战"]
	detail_text += "[color=gray]射程: %d格[/color]\n" % weapon_range
	detail_text += "[color=gray]防御等级: %s[/color]" % _get_defense_rating(target_ac)
	
	# 敌方特殊属性提醒
	if target.data.is_enemy:
		if target.data.immunities.size() > 0:
			detail_text += "\n[color=#ff6666]免疫: %s[/color]" % ", ".join(target.data.immunities)
		if target.data.resistances.size() > 0:
			detail_text += "\n[color=#ccaa44]抗性: %s[/color]" % ", ".join(target.data.resistances)
	
	details_label.text = detail_text


## 隐藏预览
func hide_preview():
	visible = false


## 跟随鼠标位置
func follow_mouse(global_pos: Vector2):
	position = global_pos + Vector2(15, 15)
	
	# 边界修正（防止超出屏幕）
	await get_tree().process_frame
	var viewport_size := get_viewport().get_visible_rect().size
	if position.x + size.x > viewport_size.x:
		position.x = global_pos.x - size.x - 10
	if position.y + size.y > viewport_size.y:
		position.y = global_pos.y - size.y - 10


## 将内部AC值转换为玩家感知的防御等级描述
func _get_defense_rating(ac: int) -> String:
	if ac <= 8:
		return "极弱"
	elif ac <= 10:
		return "较弱"
	elif ac <= 12:
		return "普通"
	elif ac <= 14:
		return "坚固"
	elif ac <= 16:
		return "精良"
	elif ac <= 18:
		return "极其坚固"
	else:
		return "铜墙铁壁"
