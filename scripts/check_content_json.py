import json
import pathlib
import re

paths = [pathlib.Path('Blade&Hex/BladeHexCore/src/SkillTree/skill_tree_content.json')]
paths.extend(pathlib.Path('Blade&Hex/BladeHexCore/src/Interaction/dialogues').glob('*.json'))

bad = re.compile(r'[�]|(?:鏂|锛|涓|浠|骞|妗|姝|烘|璇|绛|鍙|犳|鐨|鈥|銆|濮|閹|娑|绻|妞|湱|鐔|顏|劅|惄|顔|綆)')
summary = []
for path in paths:
    text = path.read_text(encoding='utf-8', errors='replace')
    try:
        data = json.loads(text)
        ok = True
        err = ''
    except Exception as exc:
        ok = False
        err = str(exc)
    hits = []
    for line_no, line in enumerate(text.splitlines(), 1):
        if bad.search(line):
            hits.append((line_no, line.strip()[:220]))
    summary.append((str(path), ok, err, hits))

for path, ok, err, hits in summary:
    print(f'{path} json_ok={ok} mojibake_hits={len(hits)}')
    if err:
        print('  JSON_ERROR', err)
    for line_no, sample in hits[:20]:
        print(f'  L{line_no}: {sample}')
