"""Fix corrupted comment lines that eat the next line of code.

Pattern: // some comment text?        var something = ...
Should become:
// some comment text
        var something = ...
"""
import os, re

root = r'D:\123\Blade&Hex\BladeHexFrontend\src'
# Pattern: a // comment containing ? followed by 2+ spaces then actual code
pattern = re.compile(r'^(\s*//[^\n]*\?)\s{2,}(\S.*)$')

fixed_count = 0

for dp, dn, fns in os.walk(root):
    for f in fns:
        if not f.endswith('.cs'):
            continue
        path = os.path.join(dp, f)
        with open(path, 'r', encoding='utf-8', errors='replace') as fh:
            lines = fh.readlines()
        
        new_lines = []
        changed = False
        for line in lines:
            m = pattern.match(line)
            if m:
                comment_part = m.group(1)
                code_part = m.group(2)
                # Determine indentation for the code part from the original line
                # The code part should have the same indentation as the comment
                indent = re.match(r'^(\s*)', line).group(1)
                new_lines.append(comment_part.rstrip() + '\n')
                new_lines.append(indent + code_part + '\n')
                changed = True
                fixed_count += 1
                print(f"FIXED {path}:{len(new_lines)-1}: {comment_part.strip()} | {code_part.strip()}")
            else:
                new_lines.append(line)
        
        if changed:
            with open(path, 'w', encoding='utf-8', newline='') as fh:
                fh.writelines(new_lines)

print(f"\nTotal fixes: {fixed_count}")
