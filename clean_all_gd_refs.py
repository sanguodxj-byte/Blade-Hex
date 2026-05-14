"""Comprehensive cleanup of all GDScript references in comments."""
import os
import re

roots = [
    r'D:\123\Blade&Hex\BladeHexFrontend\src',
    r'D:\123\Blade&Hex\BladeHexCore\src',
]

total_files = 0

for root in roots:
    for dp, _, fns in os.walk(root):
        for f in fns:
            if not f.endswith('.cs'):
                continue
            path = os.path.join(dp, f)
            with open(path, 'r', encoding='utf-8', errors='replace') as fh:
                lines = fh.readlines()
            
            new_lines = []
            changed = False
            
            for line in lines:
                orig = line
                
                # Remove lines that are purely GDScript reference comments
                stripped = line.strip()
                
                # "// 对应 GDScript _on_xxx (line NNN)" or "// 对应 GDScript lines NNN-NNN"
                if re.match(r'^//\s*对应\s+GDScript\s+', stripped):
                    changed = True
                    continue
                
                # "// NOTE: ... GDScript 兼容..."
                if re.match(r'^//\s*NOTE:.*GDScript', stripped):
                    changed = True
                    continue
                    
                # "// 待 ... 迁 C# 后 ... 删除"
                if re.match(r'^//\s*待.*迁\s*C#', stripped):
                    changed = True
                    continue
                
                # "// 所有订阅方统一改走 EventBus"
                if re.match(r'^//\s*所有订阅方统一改走', stripped):
                    changed = True
                    continue
                
                # Clean inline references
                # "GDScript OverworldUI — CanvasLayer 基类" → "OverworldUI — CanvasLayer 基类"
                line = re.sub(r'GDScript\s+(\w+)', r'\1', line)
                
                # "// 交互系统面板 (GDScript 类型 — 用 Node 引用)" → "// 交互系统面板"
                line = re.sub(r'\s*\(GDScript\s*类型[^)]*\)', '', line)
                
                # "// GDScript 互操作辅助" → "// 辅助方法"
                line = re.sub(r'GDScript\s*互操作辅助', '辅助方法（已废弃）', line)
                
                # "// 子面板引用 (GDScript class_name → base Godot type)" → "// 子面板引用"
                line = re.sub(r'\s*\(GDScript\s+class_name[^)]*\)', '', line)
                
                # "// GDScript 脚本缓存（静态，避免重复加载）" → remove entire line
                if re.match(r'\s*//\s*GDScript\s*脚本缓存', line.strip()):
                    changed = True
                    continue
                
                # "// GDScript 实例工厂" → "// 实例工厂"
                line = line.replace('GDScript 实例工厂', '实例工厂（已废弃）')
                
                # "// 公共 API — 由 CombatManager (C#) 及 GDScript 战斗场景调用"
                line = re.sub(r'\s*及\s*GDScript\s*战斗场景调用', '', line)
                
                # "/// 子面板使用 GDScript class_name 类型..." → remove these doc lines
                if 'GDScript class_name' in line and '///' in line:
                    changed = True
                    continue
                # "/// 方法调用通过 Call 转发。"
                if '方法调用通过 Call 转发' in line and '///' in line:
                    changed = True
                    continue
                
                # "// ... (Similar distance logic as GDScript)"
                line = re.sub(r'\s*\(Similar.*as GDScript\)', '', line)
                
                # "/// 难度配置，GDScript 可通过属性注入"
                line = line.replace('，GDScript 可通过属性注入', '')
                
                # "/// 无参初始化，GDScript 可调用"
                line = line.replace('，GDScript 可调用', '')
                
                # "/// ... 调用 AudioManager (GDScript autoload) 播放对应音效"
                line = line.replace(' (GDScript autoload)', '')
                
                # "// 设置相关（供 GDScript 调用）"
                line = re.sub(r'（供\s*GDScript\s*调用）', '', line)
                
                # "/// 注册一个分区（Callable 版本，兼容 GDScript 调用）"
                line = re.sub(r'（Callable\s*版本，兼容\s*GDScript\s*调用）', '', line)
                
                # "/// GDScript 访问视觉多边形用（用于改色/缩放）"
                line = line.replace('GDScript 访问', '访问')
                
                # "/// 放置城镇到大地图坐标（GDScript 调用）"
                line = re.sub(r'（GDScript\s*调用）', '', line)
                
                # "// 保留 Variant 签名供 Bus 从 GDScript 兼容调用"
                line = re.sub(r'供\s*Bus\s*从\s*GDScript\s*兼容调用', '供 Bus 调用', line)
                
                # "/// 移除实体（GDScript 兼容）"
                line = re.sub(r'（GDScript\s*兼容）', '', line)
                
                # "/// 移除已卸载 chunk 的遭遇标记（GDScript 兼容 stub）"
                line = re.sub(r'（GDScript\s*兼容\s*stub）', '', line)
                
                # "/// 当前活跃的遭遇标记像素位置（GDScript 兼容 stub）"
                line = re.sub(r'（GDScript\s*兼容\s*stub）', '', line)
                
                # "// 此方法保留为空实现，避免 GDScript 调用报错"
                line = line.replace('，避免 GDScript 调用报错', '')
                
                # "// 当 Chunk 遭遇被触发时发射（用 Vector2I 坐标，GDScript 侧通过 check_chunk_encounters 获取完整数据）"
                line = re.sub(r'，GDScript\s*侧通过[^）]*）', '）', line)
                
                # "// RefCounted wrapper — exposes XXX to GDScript"
                line = re.sub(r'\s*[—–-]+\s*exposes.*to GDScript', '', line)
                
                # "// Nested enum ... is passed as int from GDScript and cast inside"
                if 'from GDScript and cast inside' in line:
                    changed = True
                    continue
                
                # "// Separator (matching _factory.create_separator_h() position in GDScript)"
                line = re.sub(r'\s*\(matching.*in GDScript\)', '', line)
                
                # "// 假设初始有 25 点可分配 (参考 GDScript 逻辑)"
                line = re.sub(r'\s*\(参考\s*GDScript\s*逻辑\)', '', line)
                
                # "/// 注意：GDScript 原版是 Node（有信号），C# 版本改为纯静态工具类。"
                if 'GDScript 原版' in line:
                    changed = True
                    continue
                
                # "// 硬编码以消除 GDScript 依赖"  (might have corrupted chars)
                line = re.sub(r'GDScript\s*依赖', '外部依赖', line)
                
                # "/// 创建 GDScript 节点" → "/// 创建节点（已废弃）"
                line = line.replace('创建 GDScript 节点', '创建节点（已废弃）')
                line = line.replace('创建 GDScript 面板', '创建面板（已废弃）')
                
                # "GDScript '{className}' no longer exists"
                line = line.replace("GDScript '", "'")
                
                # "// 从 QuickCombatScene.gd 迁移" (already handled above but just in case)
                line = re.sub(r'从\s+\S+\.gd\s*迁移', '', line)
                
                # "// 实现所有初始化方法，从 GDScript OverworldScene.gd 翻译"
                line = re.sub(r'，从\s+GDScript\s+\S+\s*翻译', '', line)
                
                if line != orig:
                    changed = True
                
                new_lines.append(line)
            
            if changed:
                with open(path, 'w', encoding='utf-8', newline='') as fh:
                    fh.writelines(new_lines)
                total_files += 1

print(f"Modified {total_files} files")
