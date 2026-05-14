"""Fix GDScript-style Call() in QuickCombatScene.cs to use direct C# method calls."""
import re

path = r'D:\123\Blade&Hex\BladeHexFrontend\src\Scenes\combat\QuickCombatScene.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Map of snake_case Call methods to PascalCase direct calls on _combatUi
replacements = {
    '_combatUi.Call("register_enemy", ': '_combatUi.RegisterEnemy(',
    '_combatUi.Call("set_turn_text", ': '_combatUi.SetTurnText(',
    '_combatUi.Call("set_action_bar_visible", ': '_combatUi.SetActionBarVisible(',
    '_combatUi.Call("update_unit_info", ': '_combatUi.UpdateUnitInfo(',
    '_combatUi.Call("log_message", ': '_combatUi.LogMessage(',
    '_combatUi.Call("open_spell_panel", ': '_combatUi.OpenSpellPanel(',
}

for old, new in replacements.items():
    count = content.count(old)
    if count > 0:
        content = content.replace(old, new)
        print(f"  Replaced {count}x: {old.strip()} -> {new.strip()}")

# Fix the closing parens - Call("method", args) has one extra paren level
# Actually the replacement just changes the prefix, the args and closing ) stay the same

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("Done!")
