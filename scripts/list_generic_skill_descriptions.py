import json
from pathlib import Path

p = Path('Blade&Hex/BladeHexCore/src/SkillTree/skill_tree_content.json')
data = json.loads(p.read_text(encoding='utf-8'))
generic = [n for n in data['nodes'] if '具体规则见技能星盘节点内容设计' in n.get('description', '')]
print('generic_count', len(generic))
for n in generic:
    print(f"{n.get('id')}\t{n.get('name')}\t{n.get('effect')}\tactive={n.get('isActiveSkill')}")
