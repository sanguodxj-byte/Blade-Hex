import os

def find_file(filename, search_path):
    result = []
    for root, dirs, files in os.walk(search_path):
        if filename.lower() in [f.lower() for f in files]:
            result.append(os.path.join(root, filename))
    return result

# 搜索可能装 Godot 的几个常用路径
drives = ["c:\\", "d:\\"]
for drive in drives:
    print(f"Searching for godot.exe in {drive}...")
    try:
        # 查找一些浅层目录，比如 Program Files, Tools 等，不要搜索太深以防超时
        for entry in os.listdir(drive):
            full_p = os.path.join(drive, entry)
            if os.path.isdir(full_p) and not any(p in entry.lower() for p in ["windows", "systemvolumeinformation", "recycler", "$recycle.bin"]):
                res = find_file("godot.exe", full_p)
                if res:
                    print(f"FOUND: {res}")
                res_console = find_file("godot_console.exe", full_p)
                if res_console:
                    print(f"FOUND CONSOLE: {res_console}")
    except Exception as e:
        print(f"Error reading {drive}: {e}")
