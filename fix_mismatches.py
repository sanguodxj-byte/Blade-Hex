"""
修复 fix_warnings.py 的误改
问题：脚本把使用中的局部变量也加了下划线，导致 _xxx 定义但 xxx 被使用（名字不匹配）
策略：找到 var _xxx 但后续使用 xxx 的情况，把下划线去掉
"""

import re
import os

SRC_ROOT = os.path.join(os.path.dirname(__file__), "src")


def find_all_gd_files(root):
    gd_files = []
    for dirpath, dirnames, filenames in os.walk(root):
        for f in filenames:
            if f.endswith(".gd"):
                gd_files.append(os.path.join(dirpath, f))
    return gd_files


def fix_mismatched_vars(content):
    """修复 var _xxx = ... 但后续使用 xxx 的情况"""
    lines = content.split("\n")
    changed = False

    for i in range(len(lines)):
        line = lines[i]
        # 匹配 var _name = ...
        m = re.match(r"^(\s*)var\s+(_\w+)\s*=\s*(.+)$", line)
        if not m:
            continue

        indent = m.group(1)
        var_name_with_underscore = m.group(2)
        var_name_without = var_name_with_underscore[1:]  # 去掉下划线

        # 检查后续代码是否使用了不带下划线的名字
        base_indent = len(indent)
        used_without = False
        used_with = False

        pattern_without = re.compile(r"\b" + re.escape(var_name_without) + r"\b")
        pattern_with = re.compile(r"\b" + re.escape(var_name_with_underscore) + r"\b")

        for j in range(i + 1, min(i + 80, len(lines))):
            next_line = lines[j]
            # 同级或更少缩进的非空行 → 作用域结束
            if (
                next_line.strip()
                and len(get_leading_whitespace(next_line)) <= base_indent
            ):
                break
            # 排除变量自身的声明
            if pattern_without.search(next_line):
                used_without = True
            if pattern_with.search(next_line):
                used_with = True

        if used_without and not used_with:
            # 变量名被错误加了下划线，后续都用不带下划线的名字
            # 把 var _name 改回 var name
            lines[i] = lines[i].replace(
                f"var {var_name_with_underscore}", f"var {var_name_without}", 1
            )
            changed = True

    return "\n".join(lines) if changed else content


def fix_mismatched_params(content):
    """修复函数参数 _name 但函数体内使用 name 的情况"""
    lines = content.split("\n")
    result = content

    func_pattern = re.compile(r"^(\s*(?:static\s+)?)func\s+(\w+)\s*\((.*?)\)")

    i = 0
    changed = False
    while i < len(lines):
        m = func_pattern.match(lines[i])
        if not m:
            i += 1
            continue

        prefix = m.group(1)
        func_name = m.group(2)
        params_str = m.group(3).strip()

        if not params_str:
            i += 1
            continue

        # 解析参数
        params = []
        for p in split_params(params_str):
            pm = re.match(r"(_?\w+)\s*:\s*(\w+(?:\.\w+)?)\s*(?:=\s*(.+))?", p)
            if pm:
                params.append((pm.group(1), pm.group(2), pm.group(3)))
            else:
                pm2 = re.match(r"(_?\w+)\s*(?:=\s*(.+))?", p)
                if pm2:
                    params.append((pm2.group(1), None, pm2.group(2)))

        # 收集函数体
        func_body_start = i + 1
        func_body = []
        base_indent = len(get_leading_whitespace(lines[i]))
        j = i + 1
        while j < len(lines):
            if (
                lines[j].strip()
                and len(get_leading_whitespace(lines[j])) <= base_indent
            ):
                break
            func_body.append(lines[j])
            j += 1

        func_body_str = "\n".join(func_body)

        # 检查每个下划线参数
        new_params = list(params)
        any_changed = False
        for pi, (pname, ptype, pdefault) in enumerate(params):
            if not pname.startswith("_"):
                continue
            base_name = pname[1:]

            # 检查函数体中是否使用了不带下划线的名字
            pattern = re.compile(r"\b" + re.escape(base_name) + r"\b")
            if pattern.search(func_body_str):
                # 函数体使用不带下划线的名字，说明参数不该加下划线
                new_params[pi] = (base_name, ptype, pdefault)
                any_changed = True

        if any_changed:
            new_params_str = ", ".join(
                f"{n}: {t}" + (f" = {d}" if d else "")
                if t
                else f"{n}" + (f" = {d}" if d else "")
                for n, t, d in new_params
            )
            lines[i] = f"{prefix}func {func_name}({new_params_str}):"
            changed = True

        i = j

    return "\n".join(lines) if changed else result


def split_params(params_str):
    if not params_str:
        return []
    depth = 0
    current = ""
    result = []
    for ch in params_str:
        if ch in "([{":
            depth += 1
            current += ch
        elif ch in ")]}":
            depth -= 1
            current += ch
        elif ch == "," and depth == 0:
            result.append(current.strip())
            current = ""
        else:
            current += ch
    if current.strip():
        result.append(current.strip())
    return result


def get_leading_whitespace(s):
    return s[: len(s) - len(s.lstrip())]


def process_file(filepath):
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    original = content
    content = fix_mismatched_vars(content)
    content = fix_mismatched_params(content)

    if content != original:
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)
        return True
    return False


def main():
    gd_files = find_all_gd_files(SRC_ROOT)
    print(f"Checking {len(gd_files)} files for mismatched names...")

    fixed = 0
    for filepath in gd_files:
        try:
            if process_file(filepath):
                rel = os.path.relpath(filepath, os.path.dirname(__file__))
                print(f"  Fixed: {rel}")
                fixed += 1
        except Exception as e:
            print(f"  Error: {filepath}: {e}")

    print(f"\nFixed {fixed} files")


if __name__ == "__main__":
    main()
