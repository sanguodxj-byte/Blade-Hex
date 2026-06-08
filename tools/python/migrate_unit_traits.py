#!/usr/bin/env python3
"""
T08: Unit Flavor Trait Migration Script
Scans UnitTemplateDB.cs and converts Chinese trait phrases to standardized IDs.
Output: _trait_migration_report.txt
"""

import re
import os
from pathlib import Path

# Chinese trait phrase → standardized ID mapping
TRAIT_MAPPING = {
    # 亡灵类
    "亡灵坚韧": "undead_resilience",
    "不知疲倦": "tireless",
    "腐烂之躯：近战攻击者中毒": "rotten_body",
    "腐臭爪击": "fetid_claws",
    "麻痹之咬：命中后DC12强韧麻痹1回合": "paralyzing_bite",
    # 飞行类
    "飞行：无视地形": "flying",
    "飞行": "flying",
    "隐身：1次/战斗": "stealth",
    # 群猎类
    "群猎：相邻每有1个友方狼，攻击+1": "pack_tactics",
    "群猎：相邻每有1个友方狼，攻击+2": "pack_tactics",
    "群猎": "pack_tactics",
    # 嗅觉类
    "嗅觉追踪": "scent_tracking",
    # 毒类
    "毒尾穿刺：命中附带中毒DC13": "poison_sting",
    "钳制：命中后目标缚足": "grapple",
    # 远程类
    "远程骚扰": "ranged_harassment",
    "游击战": "guerrilla_warfare",
    # 领袖类
    "领袖气场": "leadership_aura",
    "战吼：友方攻击+2持续1回合": "battle_cry",
    # 狂暴类
    "鲁莽攻击：攻击优势但被攻击也有优势": "reckless_attack",
    "狂暴：HP<25%时攻击+2，AC-2": "berserk",
    # 蛛类
    "蛛网行走：无视蛛网地形惩罚": "web_walk",
    "吐丝：射程4格，目标缚足2回合": "web_shot",
    # 分裂类
    "分裂：HP<50%时分裂为2个小史莱姆": "split",
    "分裂": "split",
    # 腐蚀类
    "腐蚀：近战攻击者武器受损": "corrosive",
    "灼热：近战攻击者受1d4火焰": "burning_touch",
    "液态体": "amorphous",
    # 攻击类
    "多重攻击：熊掌+啃咬": "multi_attack",
    "扑击：冲锋命中后目标倒地": "pounce",
    # 魅惑类
    "魅惑之歌：DC13WIS否则向其移动": "charm_song",
    # 吐息类
    "火焰吐息": "fire_breath",
    "冰霜吐息": "frost_breath",
    "毒雾吐息": "poison_breath",
    # 恐惧类
    "恐惧光环": "fear_aura",
    "恐惧凝视": "fear_gaze",
    # 传奇类
    "传奇坚韧": "legendary_resilience",
    "传奇动作": "legendary_actions",
    "巢穴动作": "lair_actions",
}


def scan_templates(file_path):
    """Scan UnitTemplateDB.cs for trait arrays."""
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Pattern: { "traits", new string[] { "trait1", "trait2" } }
    pattern = r'\{ "traits", new string\[\] \{([^}]+)\} \}'
    matches = re.findall(pattern, content)

    results = []
    for match in matches:
        # Extract individual traits
        traits = re.findall(r'"([^"]+)"', match)
        results.append(traits)

    return results


def migrate_traits(traits):
    """Convert Chinese traits to IDs."""
    migrated = []
    warnings = []

    for trait in traits:
        if trait in TRAIT_MAPPING:
            migrated.append(TRAIT_MAPPING[trait])
        else:
            # Try partial match
            matched = False
            for cn, en in TRAIT_MAPPING.items():
                if cn in trait or trait in cn:
                    migrated.append(en)
                    matched = True
                    break
            if not matched:
                migrated.append(trait)  # Keep original
                warnings.append(f"  [WARN] Unmapped trait: {trait}")

    return migrated, warnings


def main():
    # Find UnitTemplateDB.cs
    base_dir = Path(__file__).parent.parent.parent
    template_file = base_dir / "BladeHexCore" / "src" / "Data" / "UnitTemplateDB.cs"

    if not template_file.exists():
        print(f"Error: {template_file} not found")
        return

    print(f"Scanning {template_file}...")
    all_traits = scan_templates(template_file)

    print(f"Found {len(all_traits)} trait arrays")

    # Process each template
    report = []
    report.append("=== T08: Unit Flavor Trait Migration Report ===")
    report.append(f"Source: {template_file}")
    report.append(f"Templates found: {len(all_traits)}")
    report.append("")

    total_traits = 0
    migrated_traits = 0
    warnings = []

    for i, traits in enumerate(all_traits):
        migrated, warns = migrate_traits(traits)
        total_traits += len(traits)
        migrated_traits += len(
            [m for m in migrated if m != traits[migrated.index(m)] if m in migrated]
        )
        warnings.extend(warns)

        report.append(f"Template {i + 1}:")
        report.append(f"  Original: {traits}")
        report.append(f"  Migrated: {migrated}")
        if warns:
            report.extend(warns)
        report.append("")

    # Summary
    report.append("=== Summary ===")
    report.append(f"Total traits: {total_traits}")
    report.append(f"Warnings: {len(warnings)}")
    report.append("")

    if warnings:
        report.append("=== Warnings ===")
        report.extend(warnings)
        report.append("")

    # Write report
    report_path = base_dir / "_trait_migration_report.txt"
    with open(report_path, "w", encoding="utf-8") as f:
        f.write("\n".join(report))

    print(f"Report written to {report_path}")
    print(f"Total traits: {total_traits}")
    print(f"Warnings: {len(warnings)}")


if __name__ == "__main__":
    main()
