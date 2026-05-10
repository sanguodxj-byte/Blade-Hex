"""
修复 fix_warnings.py 对函数参数的误改
精确匹配函数体作用域，恢复被错误加下划线的参数名
"""

import re, os

SRC_ROOT = os.path.join(os.path.dirname(__file__), "src")


def find_all_gd_files(root):
    gd_files = []
    for dirpath, dirnames, filenames in os.walk(root):
        for f in filenames:
            if f.endswith(".gd"):
                gd_files.append(os.path.join(dirpath, f))
    return gd_files


def get_indent(line):
    """返回前导空白长度"""
    return len(line) - len(line.lstrip())


def process_file(filepath):
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()
    # Normalize line endings
    content = content.replace("\r\n", "\n").replace("\r", "\n")
    lines = [l + "\n" for l in content.split("\n")]

    original = list(lines)
    changed = False

    func_pattern = re.compile(
        r"^(\s*(?:static\s+)?)func\s+(\w+)\s*\((.*?)\)\s*(?:->\s*\w+)?:\s*$"
    )

    i = 0
    while i < len(lines):
        m = func_pattern.match(lines[i].rstrip())
        if not m:
            i += 1
            continue

        prefix = m.group(1)
        func_name = m.group(2)
        params_str = m.group(3).strip()

        if not params_str:
            i += 1
            continue

        func_indent = get_indent(lines[i])

        # 精确收集函数体（缩进更深或空行）
        body_start = i + 1
        body_end = i + 1
        for j in range(i + 1, len(lines)):
            stripped = lines[j].rstrip()
            if stripped == "":
                body_end = j + 1
                continue
            if get_indent(lines[j]) <= func_indent:
                break
            body_end = j + 1

        body_text = "".join(lines[body_start:body_end])

        # 解析参数
        params = []
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

        # 检查并修复每个带下划线的参数
        new_params = list(params)
        any_fix = False

        for pi, p in enumerate(params):
            # 提取参数名
            pm = re.match(r"(_\w+)(\s*:\s*\w+(?:\.\w+)?(?:\s*=\s*.+)?)?$", p)
            if not pm:
                continue
            pname = pm.group(1)
            rest = pm.group(2) or ""
            base = pname[1:]

            # 在函数体中查找不带下划线的名字是否被使用
            # 排除注释行和字符串中的匹配
            pattern = re.compile(r"\b" + re.escape(base) + r"\b")
            for bline in lines[body_start:body_end]:
                bline_stripped = bline.split("#")[0]  # 去掉注释
                if pattern.search(bline_stripped):
                    # 找到了！这个参数不该有下划线
                    new_params[pi] = base + rest
                    any_fix = True
                    break

        if any_fix:
            # 重建参数行
            new_params_str = ", ".join(new_params)
            new_line = f"{prefix}func {func_name}({new_params_str}):\n"
            if lines[i].rstrip().endswith("-> Dictionary:"):
                ret_match = re.search(r"->\s*(\w+)\s*:", lines[i])
                if ret_match:
                    new_line = f"{prefix}func {func_name}({new_params_str}) -> {ret_match.group(1)}:\n"
            lines[i] = new_line
            changed = True

        i = body_end

    if changed:
        with open(filepath, "w", encoding="utf-8") as f:
            f.writelines(lines)
    return changed


def main():
    gd_files = find_all_gd_files(SRC_ROOT)
    print(f"Fixing {len(gd_files)} files...")

    fixed = 0
    for fp in gd_files:
        try:
            if process_file(fp):
                rel = os.path.relpath(fp, os.path.dirname(__file__))
                print(f"  Fixed: {rel}")
                fixed += 1
        except Exception as e:
            print(f"  Error: {fp}: {e}")

    print(f"\nFixed {fixed} files")


if __name__ == "__main__":
    main()
