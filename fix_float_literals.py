"""Fix double literals that should be float (add f suffix) in audio method calls."""
import os
import re

root = r'D:\123\Blade&Hex\BladeHexFrontend\src'

# Pattern: a number like 2.0 or -6.0 that's NOT followed by f and IS inside a method call
# We'll target specific method calls that take float params
float_methods = ['PlaySfxName', 'PlaySfxNameRandomPitch', 'PlayScenarioBgm', 'PlayAttackHitSfx']

total_fixes = 0

for dp, dn, fns in os.walk(root):
    for f in fns:
        if not f.endswith('.cs'):
            continue
        path = os.path.join(dp, f)
        with open(path, 'r', encoding='utf-8', errors='replace') as fh:
            content = fh.read()
        
        original = content
        
        # Fix double literals in audio calls
        # Pattern: method_name(args containing X.Y not followed by f)
        for method in float_methods:
            # Find all calls to this method and fix float literals in them
            pattern = re.compile(
                rf'(\.{method}\([^)]*?)(-?\d+\.\d+)(?!f)([^)]*?\))'
            )
            while pattern.search(content):
                content = pattern.sub(r'\g<1>\g<2>f\3', content)
                total_fixes += 1
        
        if content != original:
            with open(path, 'w', encoding='utf-8', newline='') as fh:
                fh.write(content)
            print(f"  Fixed: {os.path.relpath(path, root)}")

print(f"\nTotal float literal fixes: {total_fixes}")
