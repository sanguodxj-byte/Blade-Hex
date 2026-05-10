# StatusEffectDisplay.gd
# 状态效果显示 — 在底部面板显示单位当前所有活跃状态效果图标
# 对应策划案 03-战术战斗系统 → 七、战斗状态效果
extends HBoxContainer
class_name StatusEffectDisplay

# ============================================================================
# 常量
# ============================================================================

## 状态效果颜色映射
const EFFECT_COLORS = {
	"poison": Color(0.3, 0.8, 0.2),
	"burning": Color(1.0, 0.4, 0.1),
	"freeze": Color(0.3, 0.6, 1.0),
	"fear": Color(0.6, 0.2, 0.7),
	"silence": Color(0.5, 0.5, 0.5),
	"blind": Color(0.3, 0.3, 0.3),
	"stun": Color(1.0, 1.0, 0.2),
	"bleed": Color(0.8, 0.1, 0.1),
	"slow": Color(0.4, 0.6, 0.8),
	"root": Color(0.5, 0.3, 0.1),
	"wet": Color(0.3, 0.5, 0.9),
	"bless": Color(1.0, 0.9, 0.3),
	"shield": Color(0.3, 0.6, 1.0),
	"haste": Color(0.9, 0.9, 0.2),
	"regen": Color(0.2, 0.9, 0.3),
	"invisibility": Color(0.7, 0.7, 1.0),
	"phantom": Color(0.7, 0.5, 1.0),
	"temp_hp": Color(0.5, 0.8, 0.9),
}

## 状态效果显示名映射
const EFFECT_NAMES = {
	"poison": "毒", "burning": "火", "freeze": "冰", "fear": "惧",
	"silence": "默", "blind": "盲", "stun": "晕", "bleed": "血",
	"slow": "慢", "root": "缚", "wet": "湿",
	"bless": "祝", "shield": "盾", "haste": "速", "regen": "愈",
	"invisibility": "隐", "phantom": "幻", "temp_hp": "护",
}

# ============================================================================
# 内部
# ============================================================================

var _effect_labels: Array[Label] = []

func _ready():
	add_theme_constant_override("separation", 2)

# ============================================================================
# 更新显示
# ============================================================================

## 刷新状态效果显示
func update_effects(active_effects: Array[Dictionary]):
	# 清除旧的
	for child in get_children():
		child.queue_free()
	_effect_labels.clear()
	
	for effect in active_effects:
		var eid: String = effect.get("id", "")
		var duration: int = effect.get("duration", 0)
		var is_neg: bool = effect.get("is_negative", true)
		
		var lbl = Label.new()
		var name_str = EFFECT_NAMES.get(eid, eid.left(1))
		var color = EFFECT_COLORS.get(eid, Color.WHITE)
		
		lbl.text = "[%s%d]" % [name_str, duration]
		lbl.add_theme_font_size_override("font_size", 12)
		
		if is_neg:
			lbl.add_theme_color_override("font_color", color)
		else:
			lbl.add_theme_color_override("font_color", color)
		
		# tooltip
		var effect_name = effect.get("name", eid)
		lbl.tooltip_text = "%s (%d回合)" % [effect_name, duration]
		
		add_child(lbl)
		_effect_labels.append(lbl)
