"""
批量修复 GDScript 警告的脚本
处理的警告类型：
1. UNUSED_PARAMETER — 未使用的函数参数加下划线前缀
2. SHADOWED_VARIABLE — 函数参数遮蔽类成员变量，加下划线前缀
3. UNUSED_VARIABLE — 未使用的局部变量加下划线前缀
4. CONFUSABLE_LOCAL_DECLARATION — 同名局部变量重命名
"""

import re
import os
import sys

SRC_ROOT = os.path.join(os.path.dirname(__file__), "src")

# 已知的 Godot 内置类型，用于判断参数是否需要类型注解
KNOWN_TYPES = {
    "int",
    "float",
    "bool",
    "String",
    "StringName",
    "Vector2",
    "Vector2i",
    "Vector3",
    "Vector3i",
    "Color",
    "Array",
    "Dictionary",
    "Callable",
    "Signal",
    "RID",
    "Resource",
    "Node",
    "Node2D",
    "Node3D",
    "Control",
    "Object",
    "RefCounted",
    "PackedByteArray",
    "PackedInt32Array",
    "PackedFloat32Array",
    "PackedStringArray",
    "PackedVector2Array",
    "PackedVector3Array",
    "PackedColorArray",
    "Variant",
    "Unit",
    "HexGrid",
    "HexCell",
    "WeaponData",
    "ConsumableData",
    "ArmorData",
    "UnitData",
    "SpellData",
    "AIData",
    "AIAction",
    "StatusEffect",
    "TerrainData",
    "DifficultyConfig",
    "PanelContainer",
    "VBoxContainer",
    "HBoxContainer",
    "Label",
    "RichTextLabel",
    "ProgressBar",
    "ColorRect",
    "CenterContainer",
    "HSeparator",
    "StyleBoxFlat",
    "StyleBoxEmpty",
    "Timer",
    "Tween",
    "Thread",
    "CanvasLayer",
    "Control",
    "MarginContainer",
    "UITheme",
    "RPGRuleEngine",
    "HexUtils",
    "AISpatialAnalyzer",
    "Texture2D",
    "CompressedTexture2D",
    "AudioStream",
    "InputEventMouseButton",
    "InputEventMouseMotion",
    "InputEventKey",
    "InputEvent",
    "Sprite2D",
    "AnimatedSprite2D",
    "Camera2D",
    "TileMapLayer",
    "Area2D",
    "CollisionShape2D",
    "Shape2D",
    "Button",
    "TextureButton",
    "SpinBox",
    "Slider",
    "ScrollBar",
    "ItemList",
    "Tree",
    "TabContainer",
    "Panel",
    "ResourceLoader",
    "Engine",
    "OS",
    "Input",
    "SceneTree",
    "Performance",
    "Time",
    "JSON",
    "DirAccess",
    "FileAccess",
    "RegEx",
    "Marshalls",
    "ColorParser",
    "ShaderMaterial",
    "Shader",
    "GradientTexture1D",
}


def find_all_gd_files(root):
    """递归查找所有 .gd 文件"""
    gd_files = []
    for dirpath, dirnames, filenames in os.walk(root):
        for f in filenames:
            if f.endswith(".gd"):
                gd_files.append(os.path.join(dirpath, f))
    return gd_files


def extract_class_vars(content):
    """提取类级别的成员变量名（非函数内的局部变量）"""
    var_names = set()
    # 匹配类级别的 var 声明（不在函数内的）
    # 简单策略：在缩进为 0 或 1 tab 的 var 声明
    for line in content.split("\n"):
        stripped = line.strip()
        # 跳过函数内的（缩进的）
        if line.startswith("\t") or line.startswith("  "):
            continue
        # 匹配 var 声明
        m = re.match(r"var\s+(\w+)", stripped)
        if m:
            var_names.add(m.group(1))
    return var_names


def fix_unused_and_shadowed_params(content, class_vars):
    """修复函数参数的未使用警告和遮蔽警告

    策略：
    1. 找到所有函数声明
    2. 检查参数是否在函数体中被使用
    3. 未使用的参数加下划线
    4. 遮蔽类成员的参数也加下划线
    """
    lines = content.split("\n")
    result_lines = list(lines)

    # 找到所有函数声明的行号和参数
    func_pattern = re.compile(r"^(static\s+)?func\s+(\w+)\s*\((.*?)\)")

    i = 0
    while i < len(lines):
        line = lines[i]
        m = func_pattern.match(line.lstrip())
        if not m:
            i += 1
            continue

        is_static = m.group(1) is not None
        func_name = m.group(2)
        params_str = m.group(3).strip()

        if not params_str:
            i += 1
            continue

        # 解析参数
        params = parse_params(params_str)
        if not params:
            i += 1
            continue

        # 收集函数体
        func_start = i
        func_body_lines = []
        base_indent = get_indent_level(lines[i])
        j = i + 1
        while j < len(lines):
            if lines[j].strip() == "":
                j += 1
                continue
            indent = get_indent_level(lines[j])
            if indent <= base_indent and lines[j].strip() != "":
                break
            func_body_lines.append(lines[j])
            j += 1

        func_body = "\n".join(func_body_lines)

        # 检查每个参数
        new_params = list(params)
        changed = False
        for pi, (pname, ptype, pdefault) in enumerate(params):
            # 已经有下划线前缀的跳过
            if pname.startswith("_"):
                continue

            # 检查是否遮蔽类成员
            is_shadowed = pname in class_vars

            # 检查是否在函数体中使用（排除参数声明本身）
            # 用 word boundary 匹配
            usage_pattern = re.compile(r"\b" + re.escape(pname) + r"\b")
            usages = usage_pattern.findall(func_body)

            is_used = len(usages) > 0

            if not is_used or is_shadowed:
                new_name = "_" + pname
                new_params[pi] = (new_name, ptype, pdefault)
                changed = True

        if changed:
            # 重建参数字符串
            new_params_str = rebuild_params(new_params)
            # 重建函数声明行
            indent = lines[i][: len(lines[i]) - len(lines[i].lstrip())]
            prefix = "static " if is_static else ""
            new_line = f"{indent}{prefix}func {func_name}({new_params_str}):"
            result_lines[i] = new_line

        i = j

    return "\n".join(result_lines)


def parse_params(params_str):
    """解析函数参数列表"""
    if not params_str:
        return []

    params = []
    # 简单分割（不处理嵌套括号内的逗号）
    depth = 0
    current = ""
    for ch in params_str:
        if ch in "([{":
            depth += 1
            current += ch
        elif ch in ")]}":
            depth -= 1
            current += ch
        elif ch == "," and depth == 0:
            params.append(current.strip())
            current = ""
        else:
            current += ch
    if current.strip():
        params.append(current.strip())

    result = []
    for p in params:
        # 格式: name: Type = default 或 name: Type 或 name = default 或 name
        m = re.match(r"(\w+)\s*:\s*(\w+(?:\.\w+)?)\s*(?:=\s*(.+))?", p)
        if m:
            result.append((m.group(1), m.group(2), m.group(3)))
            continue
        m = re.match(r"(\w+)\s*(?:=\s*(.+))?", p)
        if m:
            result.append((m.group(1), None, m.group(2)))
            continue

    return result


def rebuild_params(params):
    """重建参数字符串"""
    parts = []
    for name, ptype, pdefault in params:
        s = name
        if ptype:
            s += f": {ptype}"
        if pdefault:
            s += f" = {pdefault}"
        parts.append(s)
    return ", ".join(parts)


def get_indent_level(line):
    """获取行的缩进级别（空格或 tab 数量）"""
    count = 0
    for ch in line:
        if ch == "\t":
            count += 1
        elif ch == " ":
            count += 1
        else:
            break
    return count


def fix_unused_local_vars(content):
    """修复未使用的局部变量：在赋值后未读取的 var 声明加下划线"""
    lines = content.split("\n")
    result_lines = list(lines)

    var_pattern = re.compile(r"^(\s*)var\s+(\w+)\s*=")

    for i, line in enumerate(lines):
        m = var_pattern.match(line)
        if not m:
            continue

        indent = m.group(1)
        var_name = m.group(2)

        if var_name.startswith("_"):
            continue

        # 检查后续行是否使用了这个变量
        used = False
        usage_pattern = re.compile(r"\b" + re.escape(var_name) + r"\b")
        for j in range(i + 1, min(i + 50, len(lines))):
            next_line = lines[j]
            # 如果到了同级或更少缩进的非空行，停止
            if next_line.strip() and get_indent_level(next_line) <= get_indent_level(
                line
            ):
                break
            # 排除变量声明行本身
            if usage_pattern.search(next_line):
                # 排除 var 重新声明
                if not re.match(r"\s*var\s+" + re.escape(var_name), next_line):
                    used = True
                    break

        if not used:
            new_line = line.replace(f"var {var_name}", f"var _{var_name}", 1)
            # 同时更新后续使用处（在同一个作用域内）
            result_lines[i] = new_line
            # 更新后续引用
            for j in range(i + 1, min(i + 50, len(lines))):
                if lines[j].strip() and get_indent_level(lines[j]) <= get_indent_level(
                    line
                ):
                    break
                result_lines[j] = result_lines[j].replace(var_name, f"_{var_name}")

    return "\n".join(result_lines)


def process_file(filepath):
    """处理单个文件"""
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    original = content

    # 提取类成员变量
    class_vars = extract_class_vars(content)

    # 修复未使用参数和遮蔽参数
    content = fix_unused_and_shadowed_params(content, class_vars)

    # 修复未使用局部变量
    content = fix_unused_local_vars(content)

    if content != original:
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)
        return True
    return False


def main():
    gd_files = find_all_gd_files(SRC_ROOT)
    print(f"Found {len(gd_files)} GDScript files")

    fixed_count = 0
    for filepath in gd_files:
        try:
            if process_file(filepath):
                rel = os.path.relpath(filepath, os.path.dirname(__file__))
                print(f"  Fixed: {rel}")
                fixed_count += 1
        except Exception as e:
            print(f"  Error processing {filepath}: {e}")

    print(f"\nFixed {fixed_count} files")


if __name__ == "__main__":
    main()
