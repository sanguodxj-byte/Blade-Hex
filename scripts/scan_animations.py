import os

anim_dir = r"d:\123\Blade&Hex\assets\animations"
for root, dirs, files in os.walk(anim_dir):
    json_files = [f for f in files if f.endswith(".json")]
    if json_files:
        rel_path = os.path.relpath(root, anim_dir)
        print(f"Directory: {rel_path} | Count: {len(json_files)}")
        for f in sorted(json_files):
            full_p = os.path.join(root, f)
            size = os.path.getsize(full_p)
            print(f"  - {f:20} (Size: {size} bytes)")
