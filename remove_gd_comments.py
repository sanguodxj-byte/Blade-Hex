"""Remove all GDScript-related comments from C# files to avoid confusion."""
import os
import re

root = r'D:\123\Blade&Hex\BladeHexFrontend\src'
root2 = r'D:\123\Blade&Hex\BladeHexCore\src'

# Patterns to remove (entire lines that are just GD migration comments)
line_patterns = [
    # "// 迁移自 GDScript XXX.gd" or "// 迁移自 XXX.gd"
    re.compile(r'^\s*//.*迁移自.*\.gd.*$'),
    # "// — 从 XXX.gd 迁移"
    re.compile(r'^\s*//.*从.*\.gd.*迁移.*$'),
    # "// Translated from GDScript XXX.gd"
    re.compile(r'^\s*//.*[Tt]ranslated from.*\.gd.*$'),
    # "// 对应 XXX.gd lines NNN-NNN"
    re.compile(r'^\s*//.*\.gd\s+lines?\s+\d+.*$'),
    # "// GD: xxx (lines NNN-NNN)"
    re.compile(r'^\s*//\s*GD:.*lines?\s+\d+.*$'),
    # "// 实现所有初始化方法，从 GDScript XXX.gd 翻译"
    re.compile(r'^\s*//.*从 GDScript.*\.gd.*翻译.*$'),
]

# Inline patterns to clean (remove the GD reference part but keep the rest)
inline_patterns = [
    # "// 快速战斗场景 (C# 版本) — 从 QuickCombatScene.gd 迁移" → "// 快速战斗场景"
    (re.compile(r'(//.*?)(?:\s*[—\-]\s*从\s+\S+\.gd\s*迁移)'), r'\1'),
    # "// XXX (C# 版本) — 从 XXX.gd 迁移" → "// XXX"
    (re.compile(r'(//.*?)\s*\(C#\s*版本\)\s*[—\-]\s*从.*\.gd.*$'), r'\1'),
    # Remove " — 从 XXX.gd 迁移" suffix
    (re.compile(r'(//[^/\n]*?)\s*[—\-]+\s*从\s+\S+\.gd\s*迁移\s*$'), r'\1'),
]

# Comments that reference .gd in explanatory context (replace .gd with description)
# "// NOTE: 以下 [Signal] 保留仅为 GDScript 兼容(CombatUI.gd 等仍通过 connect 订阅)"
note_pattern = re.compile(r'^\s*//\s*NOTE:.*GDScript.*兼容.*\.gd.*$')
# "// 待 rendering-layer-refactor Phase 2 完成场景迁 C# 后,删除这三个 Signal,"
phase2_pattern = re.compile(r'^\s*//\s*待.*迁\s*C#.*后.*删除.*$')
# "// 所有订阅方统一改走 EventBus"
eventbus_pattern = re.compile(r'^\s*//\s*所有订阅方统一改走.*$')

total_files = 0
total_lines_removed = 0

for root_dir in [root, root2]:
    for dp, dn, fns in os.walk(root_dir):
        for f in fns:
            if not f.endswith('.cs'):
                continue
            path = os.path.join(dp, f)
            with open(path, 'r', encoding='utf-8', errors='replace') as fh:
                lines = fh.readlines()

            new_lines = []
            changed = False
            for line in lines:
                stripped = line.rstrip('\n').rstrip('\r')
                
                # Check if entire line should be removed
                remove = False
                for pat in line_patterns:
                    if pat.match(stripped):
                        remove = True
                        break
                
                if not remove and note_pattern.match(stripped):
                    remove = True
                if not remove and phase2_pattern.match(stripped):
                    remove = True
                if not remove and eventbus_pattern.match(stripped):
                    remove = True
                
                if remove:
                    changed = True
                    total_lines_removed += 1
                    continue
                
                # Check inline patterns
                new_line = line
                for pat, repl in inline_patterns:
                    new_line = pat.sub(repl, new_line)
                
                if new_line != line:
                    changed = True
                
                new_lines.append(new_line)

            if changed:
                # Remove consecutive blank lines (more than 2)
                final_lines = []
                blank_count = 0
                for line in new_lines:
                    if line.strip() == '':
                        blank_count += 1
                        if blank_count <= 2:
                            final_lines.append(line)
                    else:
                        blank_count = 0
                        final_lines.append(line)
                
                with open(path, 'w', encoding='utf-8', newline='') as fh:
                    fh.writelines(final_lines)
                total_files += 1

print(f"Modified {total_files} files, removed {total_lines_removed} lines")
