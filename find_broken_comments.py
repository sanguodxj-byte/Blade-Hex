import os, re

root = r'D:\123\Blade&Hex\BladeHexFrontend\src'
pattern = re.compile(r'//.*\?\s{2,}\S')

for dp, dn, fns in os.walk(root):
    for f in fns:
        if not f.endswith('.cs'):
            continue
        path = os.path.join(dp, f)
        with open(path, 'r', encoding='utf-8', errors='replace') as fh:
            for i, line in enumerate(fh.readlines(), 1):
                if pattern.search(line):
                    print(f"{path}:{i}: {line.rstrip()[:120]}")
