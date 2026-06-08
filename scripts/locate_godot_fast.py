import os
import sys

def fast_search():
    # 针对 Godot 在系统中极其常用的安装路径进行扫描
    search_dirs = [
        r"d:\123",
        r"d:\bin",
        r"d:\Tools",
        r"c:\Tools",
        r"c:\Program Files",
        r"d:\Program Files",
        r"c:\Program Files (x86)",
        r"d:\Program Files (x86)",
        r"c:\Users\Administrator",
        r"d:\Antigravity\tools"
    ]
    
    # 同时也浅度遍历 D 盘和 C 盘下的所有非系统文件夹
    for drive in ["d:\\", "c:\\"]:
        try:
            for entry in os.listdir(drive):
                full_p = os.path.join(drive, entry)
                if os.path.isdir(full_p) and not any(p in entry.lower() for p in ["windows", "systemvolumeinformation", "recycler", "$recycle.bin", "appdata"]):
                    search_dirs.append(full_p)
        except:
            pass

    print(f"Scanning {len(search_dirs)} unique directories for godot*.exe...")
    
    found = []
    for d in search_dirs:
        if not os.path.exists(d):
            continue
        try:
            # 浅层遍历目录本身（不递归或只递归一层）
            for f in os.listdir(d):
                full_f = os.path.join(d, f)
                if os.path.isfile(full_f) and "godot" in f.lower() and f.endswith(".exe"):
                    found.append(full_f)
                elif os.path.isdir(full_f):
                    # 递归一层
                    try:
                        for sub_f in os.listdir(full_f):
                            full_sub = os.path.join(full_f, sub_f)
                            if os.path.isfile(full_sub) and "godot" in sub_f.lower() and sub_f.endswith(".exe"):
                                found.append(full_sub)
                    except:
                        pass
        except:
            pass
            
    print(f"Search complete. Found {len(found)} godot executables:")
    for f in found:
        print(f"  -> {f}")

if __name__ == "__main__":
    fast_search()
