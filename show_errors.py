import re
content = open(r'D:\123\Blade&Hex\build_out2.txt', 'r', encoding='utf-8', errors='replace').read()
errors = [l.strip() for l in content.split('\n') if 'error CS' in l]
print(f'Count: {len(errors)}')
for e in errors:
    print(e[:250])
    print()
