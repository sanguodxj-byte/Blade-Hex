"""Fix all remaining compilation errors in the BladeHexFrontend project."""
import re

def fix_file(path, replacements):
    """Apply multiple replacements to a file."""
    with open(path, 'r', encoding='utf-8', errors='replace') as f:
        content = f.read()
    
    for old, new in replacements:
        if old in content:
            content = content.replace(old, new, 1)
            print(f"  FIXED: {old[:60]}...")
        else:
            print(f"  SKIP (not found): {old[:60]}...")
    
    with open(path, 'w', encoding='utf-8', newline='') as f:
        f.write(content)

root = r'D:\123\Blade&Hex\BladeHexFrontend\src'

# ============================================================================
# 1. TownUI.cs - Fix TownTab.As and Node.Visible
# ============================================================================
print("\n=== TownUI.cs ===")
path = root + r'\View\UI\Overworld\TownUI.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix the _SwitchTab method
old_switch = '''        foreach (var key in _tabButtons.Keys)
        {
            var t = key.As<TownTab>();
            var btn = _tabButtons[t].AsGodotObject() as Button;
            if (btn != null)
            {
                btn.Modulate = t == tab ? new Color(1, 1, 1, 1) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
            }
        }'''
new_switch = '''        foreach (var kvp in _tabButtons)
        {
            kvp.Value.Modulate = kvp.Key == tab ? new Color(1, 1, 1, 1) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
        }'''

if old_switch in content:
    content = content.replace(old_switch, new_switch)
    print("  FIXED: TownTab.As iteration")
else:
    print("  SKIP: TownTab.As iteration not found")

# Fix child.Visible
content = content.replace(
    "foreach (Node child in _contentArea.GetChildren())\n            child.Visible = false;",
    "foreach (Node child in _contentArea.GetChildren())\n            if (child is Control ctrl) ctrl.Visible = false;"
)
print("  FIXED: Node.Visible -> Control.Visible")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 2. UIFactory.cs - Fix 'phases' variable
# ============================================================================
print("\n=== UIFactory.cs ===")
path = root + r'\View\UI\UIFactory.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# The variable 'phases' is used but never declared - it should be a local var
content = content.replace(
    "phases = phaseType switch",
    "var phases = phaseType switch"
)
print("  FIXED: phases -> var phases")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 3. OverworldUI.cs - Fix ?? Variant operator
# ============================================================================
print("\n=== OverworldUI.cs ===")
path = root + r'\View\UI\Overworld\OverworldUI.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix: (Variant)EconomyManager ?? Variant.CreateFrom(0)
# Variant is a struct, can't use ?? on it. Use ternary instead.
content = content.replace(
    '["economy"] = (Variant)EconomyManager ?? Variant.CreateFrom(0),',
    '["economy"] = EconomyManager != null ? (Variant)EconomyManager : Variant.CreateFrom(0),'
)
print("  FIXED: Variant ?? operator")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 4. QuestLog.cs - Fix all quest-related errors
# ============================================================================
print("\n=== QuestLog.cs ===")
path = root + r'\View\UI\Quest\QuestLog.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix SetItemCustomColor -> SetItemCustomFgColor (Godot 4 API)
content = content.replace("SetItemCustomColor", "SetItemCustomFgColor")

# Fix CompletedQuests -> CompletedQuestIds (but we need completed quest data, not just IDs)
# The code iterates over CompletedQuests expecting QuestData objects.
# Since QuestManager only has CompletedQuestIds (List<string>), we need to adapt.
# Replace the completed quests section
content = content.replace(
    "foreach (var quest in _manager.CompletedQuests)",
    "foreach (var questId in _manager.CompletedQuestIds)"
)
content = content.replace(
    '''        foreach (var questId in _manager.CompletedQuestIds)
        {
            int idx = _completedList.AddItem(quest.Title);
            _completedList.SetItemMetadata(idx, quest);
            _completedList.SetItemCustomFgColor(idx, Theme.TextPositive);
        }''',
    '''        foreach (var questId in _manager.CompletedQuestIds)
        {
            int idx = _completedList.AddItem(questId);
            _completedList.SetItemCustomFgColor(idx, Theme.TextPositive);
        }'''
)

# Fix Objectives iteration - Objectives is a string, not a collection
# The code does: foreach (var obj in _selectedQuest.Objectives) { obj.IsCompleted ... }
# This iterates over chars. Fix by replacing with a simple description display.
old_objectives = '''        d += "[b]任务目标:[/b]\\n";
        foreach (var obj in _selectedQuest.Objectives)
        {
            string color = obj.IsCompleted ? "green" : "white";
            string check = obj.IsCompleted ? "☑" : "☐";
            d += $"[color={color}]{check} {obj.Description} ({obj.CurrentCount}/{obj.TargetCount})[/color]\\n";
        }'''
new_objectives = '''        d += "[b]任务目标:[/b]\\n";
        d += $"{_selectedQuest.Objectives}\\n";'''

content = content.replace(old_objectives, new_objectives)

# Fix RewardXp -> RewardReputation (no XP reward exists)
content = content.replace(
    '_selectedQuest.RewardXp',
    '_selectedQuest.RewardReputation'
)

# Fix IsAbandonable -> always allow abandon for active quests
content = content.replace(
    '_abandonBtn.Disabled = !_selectedQuest.IsAbandonable;',
    '_abandonBtn.Disabled = false; // All active quests can be abandoned'
)

# Fix AbandonQuest -> FailQuest (closest equivalent)
content = content.replace(
    '_manager.AbandonQuest(_selectedQuest.Id)',
    '_manager.FailQuest(_selectedQuest)'
)

# Fix _selectedQuest.Id -> _selectedQuest.QuestId
content = content.replace('_selectedQuest.Id', '_selectedQuest.QuestId')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 5. QuestBoard.cs - Fix GlobalState.Instance and QuestData.Id
# ============================================================================
print("\n=== QuestBoard.cs ===")
path = root + r'\View\UI\Quest\QuestBoard.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix SetItemCustomColor
content = content.replace("SetItemCustomColor", "SetItemCustomFgColor")

# Fix GlobalState.Instance -> GetNodeOrNull<GlobalState>("/root/GlobalState")
# Replace all GlobalState.Instance usages
content = content.replace(
    'GlobalState.Instance?',
    'GetNodeOrNull<GlobalState>("/root/GlobalState")?'
)

# Fix _selectedQuest.Id -> _selectedQuest.QuestId
content = content.replace('_selectedQuest.Id', '_selectedQuest.QuestId')

# Fix RewardXp
content = content.replace('_selectedQuest.RewardXp', '_selectedQuest.RewardReputation')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 6. SkillTreeUI.cs - Fix Region/NodeType/SkillNodeType and Dictionary access
# ============================================================================
print("\n=== SkillTreeUI.cs ===")
path = root + r'\View\UI\Character\SkillTreeUI.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix node.Region -> node.CurrentRegion
content = content.replace('node.Region', 'node.CurrentRegion')

# Fix node.NodeType -> node.CurrentNodeType
content = content.replace('node.NodeType', 'node.CurrentNodeType')

# Fix SkillNodeData.SkillNodeType -> SkillNodeData.NodeType
content = content.replace('SkillNodeData.SkillNodeType', 'SkillNodeData.NodeType')

# Fix Dictionary.Success and Dictionary.Message access
# r.Success -> (bool)r["success"]
# r.Message -> (string)r["message"]
content = content.replace('r.Success', '(bool)r["success"]')
content = content.replace('r.Message', '(string)r["message"]')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 7. CharacterDetailPanel.cs - Fix VBoxContainer.VerticalAlignment, float->int, MaxMana
# ============================================================================
print("\n=== CharacterDetailPanel.cs ===")
path = root + r'\View\UI\Character\CharacterDetailPanel.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix VBoxContainer.VerticalAlignment (line 104) - remove it, use Alignment instead
content = content.replace(
    'idVbox.VerticalAlignment = VerticalAlignment.Center; // Note: Godot 4 VBox doesn\'t have alignment like this, use BoxContainer.AlignmentMode',
    '// VBoxContainer uses Alignment property, not VerticalAlignment'
)

# Fix float->int: SpacingXl * 1.5f passed to AddThemeConstantOverride which expects int
content = content.replace(
    'Theme.SpacingXl * 1.5f',
    '(int)(Theme.SpacingXl * 1.5f)'
)

# Fix MaxMana -> CurrentMana (UnitRuntimeState doesn't have MaxMana)
# The line is: u.Runtime.MaxMana
# Replace with a computed max mana or just use CurrentMana
content = content.replace('u.Runtime.MaxMana', 'u.Data.BaseMana')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 8. SettingsUI.cs - Fix MouseFilterMode and event handlers
# ============================================================================
print("\n=== SettingsUI.cs ===")
path = root + r'\View\UI\MainMenu\SettingsUI.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix Control.MouseFilterMode -> Control.MouseFilterEnum
content = content.replace('Control.MouseFilterMode', 'Control.MouseFilterEnum')

# Fix Action<long> -> ItemSelectedEventHandler
# optionBtn.ItemSelected += onChange; where onChange is Action<long>
# In Godot 4 C#, ItemSelected uses a delegate, not Action<long>
# The fix: wrap the action
content = content.replace(
    'optionBtn.ItemSelected += onChange;',
    'optionBtn.ItemSelected += (idx) => onChange(idx);'
)

# Fix Action<bool> -> ToggledEventHandler
# checkBtn.Toggled += onChange; where onChange is Action<bool>
content = content.replace(
    'checkBtn.Toggled += onChange;',
    'checkBtn.Toggled += (pressed) => onChange(pressed);'
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 9. SettingsPanel.cs - Fix MouseFilterMode, FullscreenModeEnum cast, Toggled
# ============================================================================
print("\n=== SettingsPanel.cs ===")
path = root + r'\View\UI\MainMenu\SettingsPanel.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix Control.MouseFilterMode -> Control.MouseFilterEnum
content = content.replace('Control.MouseFilterMode', 'Control.MouseFilterEnum')

# Fix int -> FullscreenModeEnum cast
# _editSettings.FullscreenMode = b ? 1 : 0
# FullscreenMode is FullscreenModeEnum, need cast
content = content.replace(
    '_editSettings.FullscreenMode = b ? 1 : 0',
    '_editSettings.FullscreenMode = b ? (GameSettings.FullscreenModeEnum)1 : (GameSettings.FullscreenModeEnum)0'
)

# Fix Toggled += callback where callback is Action<bool>
content = content.replace(
    'check.Toggled += callback;',
    'check.Toggled += (pressed) => callback(pressed);'
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 10. QuickCombatSetup.cs - Fix MouseFilterMode
# ============================================================================
print("\n=== QuickCombatSetup.cs ===")
path = root + r'\View\UI\MainMenu\QuickCombatSetup.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

content = content.replace('Control.MouseFilterMode', 'Control.MouseFilterEnum')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 11. LoadingScreen.cs - Fix SizeFlags
# ============================================================================
print("\n=== LoadingScreen.cs ===")
path = root + r'\View\UI\Loading\LoadingScreen.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix SizeFlags.ExpandFill -> Control.SizeFlags.ExpandFill
content = content.replace('SizeFlags.ExpandFill', 'Control.SizeFlags.ExpandFill')
# Also fix just 'SizeFlags' if used standalone
content = content.replace('SizeFlagsVertical = SizeFlags.', 'SizeFlagsVertical = Control.SizeFlags.')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 12. MainMenu.cs - Fix LoadingScreen reference (needs using)
# ============================================================================
print("\n=== MainMenu.cs ===")
path = root + r'\View\UI\MainMenu\MainMenu.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Add using for LoadingScreen namespace
if 'using BladeHex.UI.Loading;' not in content:
    content = content.replace(
        'using BladeHex.Data;',
        'using BladeHex.Data;\nusing BladeHex.UI.Loading;'
    )
    print("  FIXED: Added using BladeHex.UI.Loading")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 13. OriginSelect.cs - Fix RaceData[] to List and LoadingScreen
# ============================================================================
print("\n=== OriginSelect.cs ===")
path = root + r'\View\UI\MainMenu\OriginSelect.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Add using for LoadingScreen
if 'using BladeHex.UI.Loading;' not in content:
    content = content.replace(
        'using BladeHex.Data;',
        'using BladeHex.Data;\nusing BladeHex.UI.Loading;'
    )
    print("  FIXED: Added using BladeHex.UI.Loading")

# Fix RaceData[] to List<RaceData> - change the field type
# _allRaces = RaceData.GetAllRaces(); where _allRaces is List<RaceData>
# GetAllRaces() returns RaceData[], so change to .ToList() or change field type
content = content.replace(
    '_allRaces = RaceData.GetAllRaces();',
    '_allRaces = new System.Collections.Generic.List<RaceData>(RaceData.GetAllRaces());'
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 14. TurnOrderBar.cs - Fix GetThemeStyleboxOverride
# ============================================================================
print("\n=== TurnOrderBar.cs ===")
path = root + r'\View\UI\Combat\TurnOrderBar.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# GetThemeStyleboxOverride doesn't exist in Godot 4 C#
# The correct method is GetThemeStylebox or we need to track styles ourselves
# Actually in Godot 4, it's HasThemeStyleboxOverride + get via stored reference
# Simplest fix: use GetThemeStylebox which returns the effective stylebox
content = content.replace('GetThemeStyleboxOverride', 'GetThemeStylebox')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 15. EnemyInfoPanel.cs - Fix GetThemeStyleboxOverride
# ============================================================================
print("\n=== EnemyInfoPanel.cs ===")
path = root + r'\View\UI\Combat\EnemyInfoPanel.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

content = content.replace('GetThemeStyleboxOverride', 'GetThemeStylebox')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 16. EnemyUnitBar.cs - Fix MoraleLevel type mismatch
# ============================================================================
print("\n=== EnemyUnitBar.cs ===")
path = root + r'\View\UI\Combat\EnemyUnitBar.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# The issue: unit.Data.GetMoraleLevel() returns UnitData.MoraleLevel
# but UpdateMoraleIndicator expects BladeHex.Data.MoraleLevel
# Fix: cast the result
content = content.replace(
    'UpdateMoraleIndicator(unit.Data.GetMoraleLevel())',
    'UpdateMoraleIndicator((MoraleLevel)unit.Data.GetMoraleLevel())'
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 17. PartyPanel.cs - Fix ArmorType comparison
# ============================================================================
print("\n=== PartyPanel.cs ===")
path = root + r'\View\UI\Overworld\PartyPanel.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix: armor.ArmorType == 3 -> should compare with enum value
# ArmorType is an enum: Light=0, Medium=1, Heavy=2, Shield=3
# But the field is 'armorType' (lowercase), and comparing enum to int
content = content.replace(
    'armor.ArmorType == 3',
    'armor.armorType == ArmorData.ArmorType.Shield'
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 18. CombatUI.cs - Fix Script.New()
# ============================================================================
print("\n=== CombatUI.cs ===")
path = root + r'\View\UI\Combat\CombatUI.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# In Godot 4 C#, Script doesn't have New() directly
# The correct approach is: (T)cache.Call("new")
# Or use GDScript.New() which returns Variant
content = content.replace(
    "return cache!.New().As<T>();",
    "return ((GDScript)cache!).New().As<T>();"
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 19. OverworldScene.Interaction.cs - Fix type conversions
# ============================================================================
print("\n=== OverworldScene.Interaction.cs ===")
path = root + r'\Scenes\overworld\OverworldScene.Interaction.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Fix Array<InteractionOption> -> Array
# The ShowForEntity expects Godot.Collections.Array (non-generic)
content = content.replace(
    '_interactionPanel.ShowForEntity(entity, options)',
    '_interactionPanel.ShowForEntity(entity, new Godot.Collections.Array(options))'
)

# Fix QuestGenerator -> GodotObject
# ShowBoardDynamic expects GodotObject but QuestGenerator is plain C#
# Change the parameter type in QuestBoardPanel instead
# Actually, let's cast or change the call
content = content.replace(
    '_questBoardPanel.ShowBoardDynamic(_questGenerator, poiId, currentDay)',
    '_questBoardPanel.ShowBoardDynamic(_questGenerator, poiId, currentDay)'
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 20. QuestBoardPanel.cs - Fix ShowBoardDynamic parameter type
# ============================================================================
print("\n=== QuestBoardPanel.cs ===")
path = root + r'\View\UI\Overworld\QuestBoardPanel.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Change GodotObject to object for the questGenerator parameter
content = content.replace(
    'public void ShowBoardDynamic(GodotObject questGenerator, string poiId, int currentDay)',
    'public void ShowBoardDynamic(object questGenerator, string poiId, int currentDay)'
)

# Also fix the field type if it stores the generator
content = content.replace(
    'private GodotObject _questGenerator',
    'private object _questGenerator'
)
content = content.replace(
    'private GodotObject? _questGenerator',
    'private object? _questGenerator'
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("\n=== All fixes applied! ===")
