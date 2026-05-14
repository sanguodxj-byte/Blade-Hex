import os

files_to_fix = [
    "src/ui/main_menu/OriginSelect.cs",
    "src/ui/overworld/TownPanel.cs",
    "src/ui/overworld/TradePanel.cs",
    "src/ui/overworld/DialoguePanel.cs",
    "src/ui/overworld/RecruitPanel.cs",
    "src/ui/main_menu/MainMenu.cs",
    "src/ui/main_menu/SettingsPanel.cs",
    "src/ui/loading/TipsDisplay.cs",
    "src/ui/loading/LoadingScreen.cs",
    "src/ui/overworld/InteractionPanel.cs",
    "src/core/unit/CharacterRenderNode.cs",
    "src/core/unit/CharacterRenderBus.cs",
    "src/ui/character/SkillTreeUI.cs"
]

usings_to_add = [
    "using BladeHex.Data;\n",
    "using BladeHex.Strategic;\n",
    "using BladeHex.Combat;\n",
    "using BladeHex.Core;\n",
    "using BladeHex.UI;\n"
]

for filepath in files_to_fix:
    if not os.path.exists(filepath):
        print(f"Skipping {filepath}, not found")
        continue

    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
        
    new_lines = []
    usings_seen = set()
    added_new_usings = False
    
    for line in lines:
        if "using BladeHex.Character;" in line:
            continue # Remove invalid namespace
            
        if line.startswith("using "):
            usings_seen.add(line.strip())
            new_lines.append(line)
        elif line.startswith("namespace ") or line.startswith("[GlobalClass]"):
            if not added_new_usings:
                for u in usings_to_add:
                    if u.strip() not in usings_seen:
                        new_lines.append(u)
                        usings_seen.add(u.strip())
                added_new_usings = True
            new_lines.append(line)
        else:
            new_lines.append(line)
            
    with open(filepath, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)
    print(f"Updated {filepath}")
