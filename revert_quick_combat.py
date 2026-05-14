"""Revert QuickCombatScene.cs - it uses GDScript CombatUI, so Call() is correct."""
import re

path = r'D:\123\Blade&Hex\BladeHexFrontend\src\Scenes\combat\QuickCombatScene.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Revert PascalCase direct calls back to Call("snake_case", ...)
replacements = {
    '_combatUi.RegisterEnemy(': '_combatUi.Call("register_enemy", ',
    '_combatUi.SetTurnText(': '_combatUi.Call("set_turn_text", ',
    '_combatUi.SetActionBarVisible(': '_combatUi.Call("set_action_bar_visible", ',
    '_combatUi.UpdateUnitInfo(': '_combatUi.Call("update_unit_info", ',
    '_combatUi.LogMessage(': '_combatUi.Call("log_message", ',
    '_combatUi.OpenSpellPanel(': '_combatUi.Call("open_spell_panel", ',
}

for old, new in replacements.items():
    count = content.count(old)
    if count > 0:
        content = content.replace(old, new)
        print(f"  Reverted {count}x: {old.strip()}")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("Done!")
