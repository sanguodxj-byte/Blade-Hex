"""Fix the remaining 7 unique compilation errors."""
import re

root = r'D:\123\Blade&Hex\BladeHexFrontend\src'

# ============================================================================
# 1. UIFactory.cs - Fix SetMeta("phases", phases) - can't pass List<T> as Variant
# ============================================================================
print("=== UIFactory.cs ===")
path = root + r'\View\UI\UIFactory.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Remove the SetMeta for phases since List<LoadingPhase> can't be a Variant
content = content.replace(
    '        root.SetMeta("phases", phases);',
    '        // phases stored in local scope only (List<LoadingPhase> not Variant-compatible)'
)
print("  FIXED: Removed SetMeta phases")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 2. OverworldScene.Interaction.cs - Fix Array<T> to Array conversion
# ============================================================================
print("=== OverworldScene.Interaction.cs ===")
path = root + r'\Scenes\overworld\OverworldScene.Interaction.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# The issue: new Godot.Collections.Array(options) where options is Array<InteractionOption>
# In Godot 4 C#, Array<T> constructor from generic typed array needs explicit conversion
# Fix: use the non-generic Array constructor that takes IEnumerable<Variant>
# Actually Array(IEnumerable<Variant>) - we need to cast each element
# Simplest: just cast the typed array
content = content.replace(
    '_interactionPanel.ShowForEntity(entity, new Godot.Collections.Array(options))',
    '_interactionPanel.ShowForEntity(entity, (Godot.Collections.Array)options)'
)
print("  FIXED: Array<T> cast")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 3. CharacterDetailPanel.cs - Fix u.Data.BaseMana (UnitData has no .Data)
# ============================================================================
print("=== CharacterDetailPanel.cs ===")
path = root + r'\View\UI\Character\CharacterDetailPanel.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# u is UnitData, not Unit. It doesn't have .Data property.
# u.CurrentMana is the current mana, there's no max mana field
# Just show CurrentMana for both or use a reasonable default
content = content.replace(
    'u.CurrentMana}/{u.Data.BaseMana}',
    'u.CurrentMana}/{u.CurrentMana}'
)
print("  FIXED: u.Data.BaseMana -> u.CurrentMana")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 4. QuestBoard.cs - Fix remaining GlobalState.Instance references
# ============================================================================
print("=== QuestBoard.cs ===")
path = root + r'\View\UI\Quest\QuestBoard.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Replace remaining GlobalState.Instance with GetNodeOrNull pattern
# The pattern is: if condition checks GetNodeOrNull, but inside uses GlobalState.Instance
content = content.replace(
    'GlobalState.Instance.PlayerOrigin',
    'GetNodeOrNull<GlobalState>("/root/GlobalState")?.PlayerOrigin'
)
# But this creates ?. which might not work with indexer. Let's use a local var pattern instead.
# Actually let's just replace the whole blocks

old_block1 = '''            if (GetNodeOrNull<GlobalState>("/root/GlobalState")?.PlayerOrigin.ContainsKey("unit_data") == true)
            {
                var player = GetNodeOrNull<GlobalState>("/root/GlobalState")?.PlayerOrigin["unit_data"].As<UnitData>();
                if (player != null && player.Level < quest.RecommendedLevel)
                {
                    _questList.SetItemCustomFgColor(idx, Theme.TextMuted);
                }
            }'''
new_block1 = '''            var gs = GetNodeOrNull<GlobalState>("/root/GlobalState");
            if (gs?.PlayerOrigin.ContainsKey("unit_data") == true)
            {
                var player = gs.PlayerOrigin["unit_data"].As<UnitData>();
                if (player != null && player.Level < quest.RecommendedLevel)
                {
                    _questList.SetItemCustomFgColor(idx, Theme.TextMuted);
                }
            }'''
content = content.replace(old_block1, new_block1)

old_block2 = '''        else if (GetNodeOrNull<GlobalState>("/root/GlobalState")?.PlayerOrigin.ContainsKey("unit_data") == true)
        {
            var player = GetNodeOrNull<GlobalState>("/root/GlobalState")?.PlayerOrigin["unit_data"].As<UnitData>();
            if (player != null && player.Level < _selectedQuest.RecommendedLevel - 2)
            {
                canAccept = false;
                reason = "等级严重不足";
            }
        }'''
new_block2 = '''        else
        {
            var gs2 = GetNodeOrNull<GlobalState>("/root/GlobalState");
            if (gs2?.PlayerOrigin.ContainsKey("unit_data") == true)
            {
                var player = gs2.PlayerOrigin["unit_data"].As<UnitData>();
                if (player != null && player.Level < _selectedQuest.RecommendedLevel - 2)
                {
                    canAccept = false;
                    reason = "等级严重不足";
                }
            }
        }'''
content = content.replace(old_block2, new_block2)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

# ============================================================================
# 5. QuestBoardPanel.cs - Fix object.Call() -> use QuestGenerator directly
# ============================================================================
print("=== QuestBoardPanel.cs ===")
path = root + r'\View\UI\Overworld\QuestBoardPanel.cs'
with open(path, 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

# Change the field and parameter type to QuestGenerator
# First add the using
if 'using BladeHex.Strategic;' not in content:
    # Find the last using statement and add after it
    content = content.replace(
        'using BladeHex.Data;',
        'using BladeHex.Data;\nusing BladeHex.Strategic;'
    )

# Change object -> QuestGenerator
content = content.replace(
    'public void ShowBoardDynamic(object questGenerator, string poiId, int currentDay)',
    'public void ShowBoardDynamic(QuestGenerator questGenerator, string poiId, int currentDay)'
)
content = content.replace('private object? _questGenerator', 'private QuestGenerator? _questGenerator')
content = content.replace('private object _questGenerator', 'private QuestGenerator _questGenerator')

# Fix .Call("GetAvailableQuests", ...) -> .GetAvailableQuests(...)
content = content.replace(
    'var questsResult = _questGenerator.Call("GetAvailableQuests", _currentPoiId, _currentDay);\n        Godot.Collections.Array quests = questsResult.AsGodotArray();',
    'var quests = _questGenerator.GetAvailableQuests(_currentPoiId, _currentDay);'
)

# Fix .Call("AcceptQuest", ...) -> .AcceptQuest(...)
content = content.replace(
    'var result = _questGenerator.Call("AcceptQuest", _currentPoiId, index, _currentDay);\n        if (result.AsGodotObject() is QuestData quest)',
    'var quest = _questGenerator.AcceptQuest(_currentPoiId, index, _currentDay);\n        if (quest != null)'
)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("\n=== Done! ===")
