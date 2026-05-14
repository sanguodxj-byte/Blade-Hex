content = open(r'D:\123\Blade&Hex\build_out9.txt', 'r', encoding='utf-8', errors='replace').read()
errors = [l.strip() for l in content.split('\n') if 'error' in l.lower() and 'CS' in l]
success = '已成功生成' in content
print(f'Build: {"OK" if success else "FAIL"}, Errors: {len(errors)//2}')
for e in errors[:20]:
    print(e[:200])
