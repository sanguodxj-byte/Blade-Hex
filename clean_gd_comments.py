"""Remove/clean all .gd references from C# comments."""
import os
import re

roots = [
    r'D:\123\Blade&Hex\BladeHexFrontend\src',
    r'D:\123\Blade&Hex\BladeHexCore\src',
]

total_files = 0
total_changes = 0

for root in roots:
    for dp, _, fns in os.walk(root):
        for f in fns:
            if not f.endswith('.cs'):
                continue
            path = os.path.join(dp, f)
            with open(path, 'r', encoding='utf-8', errors='replace') as fh:
                content = fh.read()
            
            original = content
            
            # Pattern 1: Remove entire comment lines that are just migration notes
            # "// 迁移自 GDScript XXX.gd"
            content = re.sub(r'\n\s*//\s*迁移自\s*(GDScript\s*)?\S+\.gd\s*\n', '\n', content)
            
            # Pattern 2: "// XXX — 从 XXX.gd 迁移" on header comment lines
            # Remove the " — 从 XXX.gd 迁移" part
            content = re.sub(r'(//[^\n]*?)\s*[—–-]+\s*从\s+\S+\.gd\s*迁移', r'\1', content)
            
            # Pattern 3: "(C# 版本) — 从 XXX.gd 迁移"
            content = re.sub(r'\s*\(C#\s*版本\)\s*[—–-]+\s*从\s+\S+\.gd\s*迁移', '', content)
            
            # Pattern 4: "// 对应 XXX.gd lines NNN-NNN" or "// 对应 GDScript XXX.gd"
            content = re.sub(r'\n\s*//\s*对应\s+\S+\.gd\s+lines?\s+\d+[^\n]*\n', '\n', content)
            
            # Pattern 5: "// GD: _ready UI setup (lines 205-214)" style
            content = re.sub(r'\n\s*//\s*GD:\s+[^\n]*\(lines?\s+\d+[^\n]*\)\s*\n', '\n', content)
            
            # Pattern 6: "// 实现所有初始化方法，从 GDScript OverworldScene.gd 翻译"
            content = re.sub(r'(//[^\n]*?)，从\s+GDScript\s+\S+\.gd\s*翻译', r'\1', content)
            
            # Pattern 7: "// Translated from GDScript XXX.gd ..."
            content = re.sub(r'\n\s*//\s*[Tt]ranslated from\s+(GDScript\s+)?\S+\.gd[^\n]*\n', '\n', content)
            
            # Pattern 8: "// 快速战斗场景 (C# 版本) — 从 QuickCombatScene.gd 迁移"
            content = re.sub(r'(//[^\n]*?)\s*\(C#\s*版本\)', r'\1', content)
            
            # Pattern 9: "// NOTE: 以下 [Signal] 保留仅为 GDScript 兼容..."
            content = re.sub(r'\n\s*//\s*NOTE:.*GDScript\s*兼容[^\n]*\n', '\n', content)
            
            # Pattern 10: "// 待 rendering-layer-refactor ... 删除这三个 Signal"
            content = re.sub(r'\n\s*//\s*待.*迁\s*C#\s*后[^\n]*删除[^\n]*\n', '\n', content)
            
            # Pattern 11: "// 所有订阅方统一改走 EventBus"
            content = re.sub(r'\n\s*//\s*所有订阅方统一改走\s*EventBus[^\n]*\n', '\n', content)
            
            # Pattern 12: Inline "// 对应 GDScript XXX.gd 的XXX" → remove the .gd part
            content = re.sub(r'(//[^\n]*?)GDScript\s+\S+\.gd\s*的', r'\1', content)
            
            # Pattern 13: "从 QuickCombatScene.gd 迁移" in header
            content = re.sub(r'从\s+\S+\.gd\s*迁移', '', content)
            
            if content != original:
                with open(path, 'w', encoding='utf-8', newline='') as fh:
                    fh.write(content)
                total_files += 1
                total_changes += 1

print(f"Modified {total_files} files")
