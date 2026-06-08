import os

search_dir = r"d:\123\Blade&Hex"
found = []

for root, dirs, files in os.walk(search_dir):
    # 排除 .git, .godot, .claude 等无关系统文件夹
    if any(p in root for p in [".git", ".godot", ".claude", "generated_weapons_backup"]):
        continue
    for f in files:
        if f.endswith(".json"):
            filepath = os.path.join(root, f)
            try:
                with open(filepath, "r", encoding="utf-8") as file:
                    content = file.read()
                    if "offset_x" in content or "offset_y" in content:
                        found.append((filepath, os.path.getsize(filepath)))
            except:
                pass

print(f"Total offset JSON files found: {len(found)}")
for path, size in found:
    print(f"  - {path} (Size: {size} bytes)")
