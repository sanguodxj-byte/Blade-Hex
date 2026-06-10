import json
from collections import Counter, defaultdict
from pathlib import Path

import generate_skill_tree_star_chart as gen


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_REFERENCE = ROOT / "tools" / "skill_tiles (9).json"
OUT_DIR = ROOT / "BladeHexCore" / "src" / "SkillTree"

EXPECTED_BY_BAND = {
    ("inner", "active"): 2,
    ("inner", "passive"): 2,
    ("inner", "keystone"): 1,
    ("middle", "active"): 2,
    ("middle", "passive"): 4,
    ("middle", "keystone"): 2,
    ("outer", "active"): 2,
    ("outer", "passive"): 2,
    ("outer", "giant"): 1,
}
TARGET_PIP_PER_REGION = 107
SECTOR_TILE_STATS = {
    "str": ("melee_damage_percent", 0.02),
    "dex": ("ranged_damage_percent", 0.02),
    "con": ("max_hp", 5),
    "int": ("mana_max", 3),
    "wis": ("critical_rate", 0.01),
    "cha": ("ally_bonus", 1),
}
SMALL_EXTRA_STATS = [
    ("max_hp", 5),
    ("mana_max", 3),
    ("critical_rate", 0.01),
]


def main() -> None:
    reference_path = DEFAULT_REFERENCE
    layout, content = build_from_reference(reference_path)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / "skill_tree_layout.json").write_text(
        json.dumps(layout, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    (OUT_DIR / "skill_tree_content.json").write_text(
        json.dumps(content, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"imported {reference_path}")
    print(f"wrote {len(layout['nodes'])} layout nodes to {OUT_DIR}")


def build_from_reference(reference_path: Path) -> tuple[dict, dict]:
    reference = json.loads(reference_path.read_text(encoding="utf-8"))
    if reference.get("hexRadius") != gen.HEX_RADIUS:
        raise RuntimeError(f"reference hexRadius {reference.get('hexRadius')} != {gen.HEX_RADIUS}")
    if not isinstance(reference.get("components"), list):
        raise RuntimeError("reference JSON must contain exported components")

    assigned = gen.partition_tiles_by_region()
    components_by_region = load_components_by_region(reference["components"], assigned)

    layout_nodes = [
        {
            "id": "start",
            "region": "start",
            "type": "start",
            "depth": 0,
            "tiles": [gen.to_triplet(t) for t in gen.start_tiles()],
        }
    ]
    content_nodes = [
        {
            "id": "start",
            "name": "启程",
            "description": "所有角色的技能星盘起点。",
            "statBonuses": {},
        }
    ]

    for region in gen.REGION_SEQUENCE:
        region_nodes = build_region_from_reference(region, assigned[region], components_by_region[region])
        for depth, (node_id, node_type, tiles) in enumerate(region_nodes, 1):
            layout_nodes.append(
                {
                    "id": node_id,
                    "region": region,
                    "type": node_type,
                    "depth": depth,
                    "tiles": [gen.to_triplet(t) for t in tiles],
                }
            )
            content_nodes.append(make_content(region, node_id, node_type, len(tiles), node_band(tiles)))

    layout = {"version": 1, "hexRadius": gen.HEX_RADIUS, "nodes": layout_nodes}
    content = {
        "version": 1,
        "randomPools": {
            region: [
                {"stat": stat, "min": mn, "max": mx, "weight": weight}
                for stat, mn, mx, weight in entries
            ]
            for region, entries in gen.RANDOM_POOLS.items()
        },
        "nodes": content_nodes,
    }
    validate_complete_layout(layout)
    return layout, content


def make_content(region: str, node_id: str, node_type: str, tile_count: int, band: str) -> dict:
    if node_type in {"small", "pip"}:
        stat_bonuses = make_attribute_content(region, node_id, tile_count)
        return {
            "id": node_id,
            "name": "属性星纹" if node_type == "small" else "微光星点",
            "description": "每片瓦片提供所属扇区六维属性。二瓦片节点额外获得一条生命、魔力或暴击词条。",
            "statBonuses": stat_bonuses,
            "figureTemplate": "attribute_pair_2" if node_type == "small" else "pip_1",
            "seed": gen.stable_seed(node_id),
        }

    content = gen.make_content(region, node_id, node_type)
    if region == "int" and node_type == "big" and content.get("isActiveSkill"):
        tier = {"inner": 1, "middle": 2, "outer": 3}[band]
        suffix = node_id.split("_")[-1]
        label = "甲" if suffix in {"a01", "a03", "a05"} else "乙"
        content["name"] = f"法术研习·{tier} 环({label})"
        content["effect"] = f"spell_slot_{tier}"
        content["description"] = f"点亮后从 {tier} 环法术中选择一个学派研习。"
    elif region == "int" and node_type == "giant":
        content["effect"] = "int_giant_apex"
        content["isActiveSkill"] = True
    return content


def node_band(tiles: list[tuple[int, int]]) -> str:
    return component_band([gen.tile_ring(tile) for tile in tiles])


def make_attribute_content(region: str, node_id: str, tile_count: int) -> dict:
    if tile_count != 2:
        return {}

    extra_stat, extra_value = SMALL_EXTRA_STATS[gen.stable_seed(node_id) % len(SMALL_EXTRA_STATS)]
    return {extra_stat: extra_value}


def multiply_stat(value: int | float, count: int) -> int | float:
    result = value * count
    return normalize_number(result)


def add_stat(left: int | float, right: int | float) -> int | float:
    return normalize_number(left + right)


def normalize_number(value: int | float) -> int | float:
    if isinstance(value, float) and abs(value - round(value)) < 0.0001:
        return int(round(value))
    return value


def load_components_by_region(components: list[dict], assigned: dict[str, set[tuple[int, int]]]) -> dict[str, list[dict]]:
    result: dict[str, list[dict]] = {region: [] for region in gen.REGION_SEQUENCE}
    seen_tiles: dict[tuple[int, int], str] = {}

    for component in components:
        component_type = component.get("type")
        if component_type not in {"active", "passive", "keystone", "giant"}:
            raise RuntimeError(f"unsupported reference component type: {component_type}")

        tiles = [gen.enc(q, r, t) for q, r, t in component["tiles"]]
        expected_size = {"active": 4, "passive": 3, "keystone": 6, "giant": 12}[component_type]
        if len(tiles) != expected_size:
            raise RuntimeError(f"{component['id']} size {len(tiles)} != {expected_size}")
        if len(set(tiles)) != len(tiles):
            raise RuntimeError(f"{component['id']} has duplicate tiles")
        if not gen.is_connected(tiles):
            raise RuntimeError(f"{component['id']} is disconnected")

        owning_regions = {region for region, region_tiles in assigned.items() if set(tiles) <= region_tiles}
        if len(owning_regions) != 1:
            raise RuntimeError(f"{component['id']} does not fit exactly one sector: {owning_regions}")
        region = next(iter(owning_regions))

        for tile in tiles:
            if tile in seen_tiles:
                raise RuntimeError(f"{component['id']} overlaps {seen_tiles[tile]} at {tile}")
            seen_tiles[tile] = component["id"]

        rings = [gen.tile_ring(tile) for tile in tiles]
        band = component_band(rings)
        result[region].append(
            {
                "id": component["id"],
                "type": component_type,
                "tiles": tiles,
                "band": band,
                "min_ring": min(rings),
                "max_ring": max(rings),
            }
        )

    for region, region_components in result.items():
        counts = Counter((c["band"], c["type"]) for c in region_components)
        for key, expected in EXPECTED_BY_BAND.items():
            actual = counts.get(key, 0)
            if actual != expected:
                raise RuntimeError(f"{region} {key} count {actual} != {expected}")

    return result


def component_band(rings: list[int]) -> str:
    if max(rings) <= 8:
        return "inner"
    if min(rings) >= 16:
        return "outer"
    return "middle"


def build_region_from_reference(
    region: str,
    available_tiles: set[tuple[int, int]],
    components: list[dict],
) -> list[tuple[str, str, list[tuple[int, int]]]]:
    model = gen.make_sector_model(region, available_tiles)
    result: list[tuple[str, str, list[tuple[int, int]]]] = []

    def take(band: str, component_type: str, node_ids: list[str]) -> None:
        selected = [c for c in components if c["band"] == band and c["type"] == component_type]
        selected.sort(key=lambda c: component_sort_key(c, model))
        if len(selected) != len(node_ids):
            raise RuntimeError(f"{region} {band}/{component_type} count {len(selected)} != {len(node_ids)}")
        layout_type = "big" if component_type in {"active", "passive"} else component_type
        for node_id, component in zip(node_ids, selected):
            result.append((f"{region}_{node_id}", layout_type, component["tiles"]))

    take("inner", "keystone", ["ks01"])
    take("inner", "active", ["a01", "a02"])
    take("inner", "passive", ["p01", "p02"])
    take("middle", "keystone", ["ks02", "ks03"])
    take("middle", "active", ["a03", "a04"])
    take("middle", "passive", ["p03", "p04", "p05", "p06"])
    take("outer", "giant", ["g01"])
    take("outer", "active", ["a05", "a06"])
    take("outer", "passive", ["p07", "p08"])

    used = {tile for _, _, tiles in result for tile in tiles}
    if len(used) != sum(len(tiles) for _, _, tiles in result):
        raise RuntimeError(f"{region} reference components overlap")
    remaining = set(available_tiles) - used

    pairs, unmatched = split_small_and_pip_tiles(remaining, TARGET_PIP_PER_REGION)

    small_index = 1
    for left, right in sorted(pairs, key=lambda pair: component_sort_key({"tiles": list(pair)}, model)):
        result.append((f"{region}_s{small_index:03d}", "small", [left, right]))
        small_index += 1

    pip_index = 1
    for tile in sorted(unmatched, key=lambda t: gen.tile_ring(t)):
        result.append((f"{region}_pip{pip_index:02d}", "pip", [tile]))
        pip_index += 1

    consumed = sum(len(tiles) for _, _, tiles in result)
    if consumed != len(available_tiles):
        raise RuntimeError(f"{region} consumed {consumed}/{len(available_tiles)} tiles")
    return result


def component_sort_key(component: dict, model: dict) -> tuple[float, float, float, int, int]:
    tiles = component["tiles"]
    rows_cols = [model["row_col"][tile] for tile in tiles]
    avg_ring = sum(ring for ring, _ in rows_cols) / len(rows_cols)
    avg_col = sum(col for _, col in rows_cols) / len(rows_cols)
    avg_dist = sum(gen.tile_distance(tile) for tile in tiles) / len(tiles)
    first = min(tiles)
    return avg_ring, avg_col, avg_dist, first[0], first[1]


def validate_complete_layout(layout: dict) -> None:
    gen.validate(layout)
    total_tiles = sum(len(node["tiles"]) for node in layout["nodes"])
    expected_total = gen.HEX_RADIUS * gen.HEX_RADIUS * 6
    if total_tiles != expected_total:
        raise RuntimeError(f"layout tile total {total_tiles} != {expected_total}")

    by_region: dict[str, int] = defaultdict(int)
    quota: dict[tuple[str, str], int] = defaultdict(int)
    for node in layout["nodes"]:
        region = node["region"]
        by_region[region] += len(node["tiles"])
        quota[(region, node["type"])] += 1

    for region in gen.REGION_SEQUENCE:
        if by_region[region] != gen.HEX_RADIUS * gen.HEX_RADIUS - 1:
            raise RuntimeError(f"{region} tile count {by_region[region]} != {gen.HEX_RADIUS * gen.HEX_RADIUS - 1}")
        if quota[(region, "giant")] != 1:
            raise RuntimeError(f"{region} giant quota failed")
        if quota[(region, "keystone")] != 3:
            raise RuntimeError(f"{region} keystone quota failed")
        if abs(quota[(region, "small")] - quota[(region, "pip")]) > 1:
            raise RuntimeError(f"{region} small/pip counts are not close: small={quota[(region, 'small')]}, pip={quota[(region, 'pip')]}")
        small_tiles = quota[(region, "small")] * 2
        pip_tiles = quota[(region, "pip")]
        if small_tiles + pip_tiles != gen.HEX_RADIUS * gen.HEX_RADIUS - 1 - 78:
            raise RuntimeError(f"{region} small/pip tile finish failed")

    if by_region["start"] != 6:
        raise RuntimeError(f"start tile count {by_region['start']} != 6")


def split_small_and_pip_tiles(
    remaining: set[tuple[int, int]],
    target_pips: int,
) -> tuple[list[tuple[tuple[int, int], tuple[int, int]]], list[tuple[int, int]]]:
    pairs, unmatched = gen.maximum_tile_matching(remaining)
    if len(unmatched) > target_pips:
        raise RuntimeError(f"natural unmatched {len(unmatched)} > target_pips {target_pips}")
    if (target_pips - len(unmatched)) % 2 != 0:
        raise RuntimeError(f"cannot split pairs to target_pips={target_pips} with unmatched={len(unmatched)}")

    split_count = (target_pips - len(unmatched)) // 2
    pairs_sorted = sorted(
        pairs,
        key=lambda pair: (
            pair_center_ring(pair),
            pair[0][0] + pair[1][0],
            pair[0][1] + pair[1][1],
        ),
    )
    split_pairs = pairs_sorted[:split_count]
    kept_pairs = pairs_sorted[split_count:]
    pip_tiles = list(unmatched)
    for left, right in split_pairs:
        pip_tiles.extend([left, right])
    if len(pip_tiles) != target_pips:
        raise RuntimeError(f"selected {len(pip_tiles)} pips, expected {target_pips}")
    return kept_pairs, sorted(pip_tiles, key=lambda tile: (gen.tile_ring(tile), tile[0], tile[1]))


def pair_center_ring(pair: tuple[tuple[int, int], tuple[int, int]]) -> float:
    return (gen.tile_ring(pair[0]) + gen.tile_ring(pair[1])) * 0.5


if __name__ == "__main__":
    main()
