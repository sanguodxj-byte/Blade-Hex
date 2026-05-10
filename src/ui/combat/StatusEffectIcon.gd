# StatusEffectIcon.gd
# 状态效果图标系统 - 管理战斗中的状态效果显示
# 负面状态和正面状态的图标、颜色、持续时间可视化
extends Node
class_name StatusEffectIcon

## 状态效果定义
## 不使用骰子术语，用直观描述
const STATUS_DEFS := {
	# === 负面状态 ===
	"poison": {
		"icon": "☠",
		"text": "中毒",
		"color": Color(0.4, 0.85, 0.2),
		"desc": "每回合受到伤害",
		"category": "negative",
	},
	"burning": {
		"icon": "🔥",
		"text": "燃烧",
		"color": Color(0.95, 0.5, 0.1),
		"desc": "每回合受到火焰伤害，可蔓延",
		"category": "negative",
	},
	"frozen": {
		"icon": "❄",
		"text": "冰冻",
		"color": Color(0.3, 0.7, 0.95),
		"desc": "无法行动",
		"category": "negative",
	},
	"fear": {
		"icon": "😱",
		"text": "恐惧",
		"color": Color(0.7, 0.3, 0.8),
		"desc": "强制远离恐惧源",
		"category": "negative",
	},
	"silence": {
		"icon": "🤐",
		"text": "沉默",
		"color": Color(0.6, 0.6, 0.6),
		"desc": "无法施放法术",
		"category": "negative",
	},
	"blind": {
		"icon": "🕶",
		"text": "致盲",
		"color": Color(0.5, 0.5, 0.5),
		"desc": "近战困难，远程失效",
		"category": "negative",
	},
	"stun": {
		"icon": "💫",
		"text": "眩晕",
		"color": Color(0.95, 0.85, 0.2),
		"desc": "只能移动或攻击(二选一)",
		"category": "negative",
	},
	"bleed": {
		"icon": "🩸",
		"text": "流血",
		"color": Color(0.85, 0.1, 0.1),
		"desc": "每回合受到伤害，可叠加",
		"category": "negative",
	},
	"slow": {
		"icon": "🐌",
		"text": "减速",
		"color": Color(0.4, 0.6, 0.8),
		"desc": "移动速度降低",
		"category": "negative",
	},
	"root": {
		"icon": "🔗",
		"text": "缚足",
		"color": Color(0.7, 0.5, 0.3),
		"desc": "无法移动",
		"category": "negative",
	},
	# === 正面状态 ===
	"bless": {
		"icon": "✨",
		"text": "祝福",
		"color": Color(0.95, 0.9, 0.4),
		"desc": "攻击和豁免增强",
		"category": "positive",
	},
	"shield": {
		"icon": "🛡",
		"text": "护盾",
		"color": Color(0.3, 0.6, 0.95),
		"desc": "护甲大幅提升",
		"category": "positive",
	},
	"haste": {
		"icon": "⚡",
		"text": "加速",
		"color": Color(0.9, 0.85, 0.2),
		"desc": "移动力提升，额外行动",
		"category": "positive",
	},
	"regen": {
		"icon": "💚",
		"text": "再生",
		"color": Color(0.2, 0.85, 0.3),
		"desc": "每回合恢复生命",
		"category": "positive",
	},
	"invisible": {
		"icon": "👁",
		"text": "隐身",
		"color": Color(0.6, 0.6, 0.9),
		"desc": "不可被直接瞄准",
		"category": "positive",
	},
	"temp_hp": {
		"icon": "💎",
		"text": "临时HP",
		"color": Color(0.5, 0.8, 0.9),
		"desc": "额外生命值层",
		"category": "positive",
	},
}


## 创建2D状态效果图标列表（用于UI面板）
static func create_status_bar(parent: Control, effects: Array[String], horizontal: bool = true) -> HBoxContainer:
	var container := HBoxContainer.new()
	container.add_theme_constant_override("separation", 3)
	if not horizontal:
		# 垂直排列时可用 VBoxContainer 替代
		pass
	
	for effect_key in effects:
		if not STATUS_DEFS.has(effect_key):
			continue
		
		var def: Dictionary = STATUS_DEFS[effect_key]
		var icon_container := PanelContainer.new()
		var style := StyleBoxFlat.new()
		
		if def.category == "negative":
			style.bg_color = Color(0.2, 0.08, 0.08, 0.7)
			style.border_color = Color(def.color, 0.6)
		else:
			style.bg_color = Color(0.08, 0.12, 0.08, 0.7)
			style.border_color = Color(def.color, 0.6)
		
		style.set_border_width_all(1)
		style.set_corner_radius_all(3)
		style.set_content_margin_all(3)
		icon_container.add_theme_stylebox_override("panel", style)
		
		var label := Label.new()
		label.text = "%s %s" % [def.icon, def.text]
		label.add_theme_font_size_override("font_size", 10)
		label.add_theme_color_override("font_color", def.color)
		icon_container.add_child(label)
		
		# Tooltip
		icon_container.tooltip_text = def.desc
		container.add_child(icon_container)
	
	parent.add_child(container)
	return container


## 获取状态效果的3D显示文本（用于 Label3D）
static func get_3d_display(effect_key: String) -> Dictionary:
	if not STATUS_DEFS.has(effect_key):
		return {"text": "?", "color": Color.GRAY}
	
	var def: Dictionary = STATUS_DEFS[effect_key]
	return {
		"text": def.icon + def.text,
		"color": def.color,
		"desc": def.desc,
		"category": def.category,
	}


## 获取所有负面状态key列表
static func get_negative_effects() -> Array[String]:
	var result: Array[String] = []
	for key in STATUS_DEFS:
		if STATUS_DEFS[key].category == "negative":
			result.append(key)
	return result


## 获取所有正面状态key列表
static func get_positive_effects() -> Array[String]:
	var result: Array[String] = []
	for key in STATUS_DEFS:
		if STATUS_DEFS[key].category == "positive":
			result.append(key)
	return result
