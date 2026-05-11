import re

with open('src/ui/combat/CombatUI.gd', 'r', encoding='utf-8') as f:
    content = f.read()

replacement = """
	# SkillTreeUI
	skill_tree_ui = SkillTreeUI.new()
	skill_tree_ui.z_index = 60
	skill_tree_ui.visible = false
	add_child(skill_tree_ui)
	
	# 11. 轮盘菜单
	radial_menu = RadialMenu.new()
	radial_menu.z_index = 100
	radial_menu.visible = false
	radial_menu.action_selected.connect(func(act): action_selected.emit(act))
	add_child(radial_menu)
"""

content = re.sub(r'\t# SkillTreeUI.*?(?=\n\t# 10\. ESC 暂停菜单)', replacement, content, flags=re.DOTALL)

append_logic = """
# ============================================================================
# 轮盘菜单
# ============================================================================

var radial_menu: RadialMenu

func open_radial_menu(screen_pos: Vector2, unit: Unit, spell_manager, target_unit: Unit = null):
	var options = {}
	
	# 基本选项
	options["防御"] = "defend"
	
	# 检查法术
	if unit.data and not unit.data.known_spells.is_empty():
		options["法术"] = "spell"
		
	# 检查物品
	if unit.data and not unit.data.consumables.is_empty():
		options["物品"] = "item"
		
	# 结束回合 (可选放在轮盘或右下角，用户要求在右下角有按钮，但也可以放轮盘)
	options["取消"] = "none"
	
	radial_menu.setup(options)
	radial_menu.show_menu(screen_pos)
"""

if "func open_radial_menu" not in content:
    content += append_logic

with open('src/ui/combat/CombatUI.gd', 'w', encoding='utf-8') as f:
    f.write(content)
print('Added Radial Menu logic to CombatUI.gd')
