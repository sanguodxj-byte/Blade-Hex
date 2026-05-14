import os

roots = [
    r'D:\123\Blade&Hex\BladeHexFrontend\src',
    r'D:\123\Blade&Hex\BladeHexCore\src',
]

for root in roots:
    for dp, _, fns in os.walk(root):
        for f in fns:
            if not f.endswith('.cs'):
                continue
            path = os.path.join(dp, f)
            with open(path, 'r', encoding='utf-8', errors='replace') as fh:
                for i, line in enumerate(fh, 1):
                    if '.gd' in line and 'gdshader' not in line:
                        rel = os.path.relpath(path, r'D:\123\Blade&Hex')
                        print(f"{rel}:{i}: {line.rstrip()[:120]}")
