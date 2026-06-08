from __future__ import annotations

import heapq
import json
import math
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "BladeHexCore" / "src" / "SkillTree"
HEX_RADIUS = 20


def enc(q: int, r: int, t: int) -> tuple[int, int]:
    return (q * 2 + t, r)


def dec(tile: tuple[int, int]) -> tuple[int, int, int]:
    x, y = tile
    t = ((x % 2) + 2) % 2
    q = (x - t) // 2
    return q, y, t


def neighbors(tile: tuple[int, int]) -> list[tuple[int, int]]:
    q, r, t = dec(tile)
    if t == 0:
        return [enc(q, r, 1), enc(q - 1, r, 1), enc(q, r - 1, 1)]
    return [enc(q, r, 0), enc(q + 1, r, 0), enc(q, r + 1, 0)]


def vertex_inside(q: int, r: int, radius: int) -> bool:
    s = -q - r
    return abs(q) <= radius and abs(r) <= radius and abs(s) <= radius


def tile_inside(tile: tuple[int, int], radius: int) -> bool:
    q, r, t = dec(tile)
    vertices = [(q, r), (q + 1, r), (q, r + 1)] if t == 0 else [(q + 1, r), (q, r + 1), (q + 1, r + 1)]
    return all(vertex_inside(vq, vr, radius) for vq, vr in vertices)


def vertex_to_pixel(q: int, r: int) -> tuple[float, float]:
    raw_x = math.sqrt(3.0) * q + math.sqrt(3.0) / 2.0 * r
    raw_y = 1.5 * r
    return raw_y, -raw_x


def centroid(tile: tuple[int, int]) -> tuple[float, float]:
    q, r, t = dec(tile)
    vertices = [(q, r), (q + 1, r), (q, r + 1)] if t == 0 else [(q + 1, r), (q, r + 1), (q + 1, r + 1)]
    xs = ys = 0.0
    for vq, vr in vertices:
        x, y = vertex_to_pixel(vq, vr)
        xs += x
        ys += y
    return xs / 3.0, ys / 3.0


def to_triplet(tile: tuple[int, int]) -> list[int]:
    q, r, t = dec(tile)
    return [q, r, t]


def start_tiles() -> list[tuple[int, int]]:
    q, r, _ = dec(enc(0, 0, 0))
    return [
        enc(q, r, 0),
        enc(q - 1, r, 0),
        enc(q, r - 1, 0),
        enc(q - 1, r, 1),
        enc(q, r - 1, 1),
        enc(q - 1, r - 1, 1),
    ]


ALL_TILES = [
    enc(q, r, t)
    for q in range(-HEX_RADIUS - 1, HEX_RADIUS + 1)
    for r in range(-HEX_RADIUS - 1, HEX_RADIUS + 1)
    for t in (0, 1)
    if tile_inside(enc(q, r, t), HEX_RADIUS)
]

REGION_ANGLES = {
    "str": math.radians(30),
    "dex": math.radians(90),
    "con": math.radians(150),
    "int": math.radians(-150),
    "wis": math.radians(-90),
    "cha": math.radians(-30),
}

REGION_ORDER = ["int", "wis", "cha", "str", "dex", "con"]
REGION_SEQUENCE = ["str", "dex", "con", "int", "wis", "cha"]

GIANTS = {
    "str": ("ᚦᚢᚱᛋ", "Thurs·蛮神：当回合内每击杀 1 个敌人立即恢复全部 AP。"),
    "dex": ("ᚱᚨᛁᚦ", "Raið·疾驰：远程攻击命中即返还本次行动，最多连续 5 次。"),
    "con": ("ᛁᛋᚨ", "Isa·凝滞：3 回合内 HP 最低锁定为 1，每场战斗 1 次。"),
    "int": ("ᚨᚾᛋ", "Ans·启示：点亮即获得 1 个 4 环法术。"),
    "wis": ("ᛇᚹᚨᛉ", "Eihwaz·绝杀：当回合内所有攻击必定命中且必定暴击。"),
    "cha": ("ᛋᛁᚷᚱ", "Sigr·凯旋：解除全场友军减益，并令窗口内友军总攻。"),
}

KEYSTONES = {
    "str": [
        ("str_ks01", "狂暴之力", "近战伤害 +50%。", "AC -3，不能用盾。", "berserk_power"),
        ("str_ks02", "铁血技法", "近战攻击必定命中。", "永不暴击；远程攻击劣势。", "resolute_technique"),
        ("str_ks03", "嗜血誓约", "近战命中按伤害 25% 吸血，溢出转临时 HP。", "无法被他人治疗；每回合开始流失最大 HP。", "blood_oath"),
    ],
    "dex": [
        ("dex_ks01", "幽灵步伐", "受到远程攻击时获得永久掩护。", "HP 上限 -20%。", "ghost_footwork"),
        ("dex_ks02", "杂技身法", "受到攻击 30% 概率完全免伤。", "AC -4，不能穿中/重甲。", "acrobatics"),
        ("dex_ks03", "近迫精射", "近距离远程伤害 +50%。", "射程 -2，远距离伤害衰减。", "point_blank"),
    ],
    "con": [
        ("con_ks01", "不朽之躯", "HP <0 时每场 1 次尝试恢复 1HP。", "移动速度 -2。", "undying_body"),
        ("con_ks02", "守誓壁垒", "装备盾牌时近战伤害额外 -25%。", "不能装备双手武器；远程伤害 -50%。", "shield_bastion"),
        ("con_ks03", "玄铁之躯", "免疫位移、击退、眩晕、恐惧，AC +3。", "不能闪避，速度 -2，不能主动后撤。", "iron_body"),
    ],
    "int": [
        ("int_ks01", "绝对专注", "法术 DC +4。", "法术研习锁定同一体系。", "absolute_focus"),
        ("int_ks02", "血祭施法", "法术改用 HP 施放，无视法力。", "法力上限归 0。", "blood_magic"),
        ("int_ks03", "玄一之心", "免疫一切持续伤害与减益。", "HP 上限 -50%。", "chaos_inoculation"),
    ],
    "wis": [
        ("wis_ks01", "刺客本能", "暴击倍率 +0.5x，暴击率 +5%。", "不能装备重甲，不能用盾。", "assassin_instinct"),
        ("wis_ks02", "元素失衡", "最近 2 回合内暴击过则所有伤害 +40%。", "暴击不再加倍伤害。", "elemental_overload"),
        ("wis_ks03", "痛觉专注", "HP <35% 时所有伤害 +30%、暴击率 +15%。", "HP 上限 -25%。", "pain_attunement"),
    ],
    "cha": [
        ("cha_ks01", "君临天下", "范围内友军全豁免 +2、不会恐慌。", "自身 HP -20%。", "royal_presence"),
        ("cha_ks02", "不可知论", "免疫心灵/恐惧效果，指挥光环范围翻倍。", "无法获得增益类 Buff。", "agnostic_command"),
        ("cha_ks03", "殉道誓约", "半径 3 友军伤害 50% 转移到你身上。", "转移伤害不可被减免；倒下则失效。", "martyr_oath"),
    ],
}

ACTIVES = {
    "str": [("连击", "double_attack"), ("旋风斩", "whirlwind"), ("狂战姿态", "berserk_stance"), ("战吼震慑", "battle_cry"), ("无畏冲锋", "fearless_charge"), ("血腥漩涡", "blood_vortex")],
    "dex": [("精准射击", "precise_shot"), ("连珠箭", "rapid_shot"), ("致盲箭", "blind_arrow"), ("闪避翻滚", "evasive_roll"), ("游猎姿态", "hunter_stance"), ("流星箭雨", "arrow_rain")],
    "con": [("盾击", "shield_bash"), ("盾牌猛击", "shield_slam"), ("守护链接", "guardian_link"), ("守御姿态", "guard_stance"), ("岿然不动", "unyielding_stand"), ("大地践踏", "earth_stomp")],
    "int": [("法术研习·1 环(甲)", "spell_slot_1"), ("法术研习·1 环(乙)", "spell_slot_1"), ("法术研习·2 环(甲)", "spell_slot_2"), ("法术研习·2 环(乙)", "spell_slot_2"), ("法术研习·3 环(甲)", "spell_slot_3"), ("法术研习·3 环(乙)", "spell_slot_3")],
    "wis": [("弱点穿刺", "weakpoint_pierce"), ("影刃涂毒", "poison_blade"), ("七窍追命", "seven_apertures"), ("疾影突袭", "shadow_lunge"), ("影遁", "shadow_hide"), ("致命标记", "deadly_mark")],
    "cha": [("战术调度", "tactical_reposition"), ("集结号令", "gathering_order"), ("威压", "command_intimidate"), ("英雄号召", "battle_banner"), ("鼓舞士气", "morale_boost"), ("战阵激励", "warband_inspiration")],
}

PASSIVES = {
    "str": ["裂帛", "碎骨", "巨握", "持刃者", "坚骨", "乘胜", "背水", "怒锋"],
    "dex": ["长弓手", "弩炮手", "掷矛手", "影袭者", "鹰瞵", "疾影", "致命专注", "锐眼"],
    "con": ["厚盾", "重铠", "磐体", "铁皮", "坚毅", "止血", "困兽", "不退"],
    "int": ["法杖客", "法球师", "魔杖手", "蓄能", "通流", "锐识", "灌注", "凝神"],
    "wis": ["短匕客", "剜心", "洞隙", "凝杀", "灵蕴", "锋寒", "缢绳", "嗜血"],
    "cha": ["号令", "旌旗", "威仪", "慑势", "财路", "鼓噪", "临阵", "锋芒"],
}

RANDOM_POOLS = {
    "str": [("melee_damage", 1, 2, 30), ("max_hp", 3, 6, 25), ("melee_hit", 1, 1, 20), ("ac", 1, 1, 15), ("critical_rate", 0.02, 0.03, 10)],
    "dex": [("ranged_damage", 1, 2, 28), ("ranged_hit", 1, 1, 22), ("initiative", 2, 3, 20), ("critical_rate", 0.02, 0.03, 18), ("speed", 1, 1, 12)],
    "con": [("max_hp", 5, 10, 34), ("ac", 1, 1, 28), ("all_save", 1, 1, 28), ("heal_amount", 1, 1, 10)],
    "int": [("mana_max", 3, 6, 35), ("spell_damage", 1, 1, 25), ("spell_hit", 1, 1, 22), ("ac", 1, 1, 8), ("all_save", 1, 1, 10)],
    "wis": [("critical_rate", 0.02, 0.05, 44), ("mana_max", 3, 5, 28), ("mana_regen", 1, 1, 20), ("all_save", 1, 1, 8)],
    "cha": [("ally_bonus", 1, 1, 50), ("initiative", 1, 2, 30), ("max_hp", 3, 5, 20)],
}


def score(tile: tuple[int, int], region: str, layer: int) -> float:
    x, y = centroid(tile)
    angle = REGION_ANGLES[region]
    tx, ty = math.cos(angle), math.sin(angle)
    radial = x * tx + y * ty
    lateral = abs(x * -ty + y * tx)
    dist = math.hypot(x, y)
    return -radial * 10.0 + lateral * 1.5 + abs(dist - layer * 1.6) * 0.2


def tile_distance(tile: tuple[int, int]) -> float:
    x, y = centroid(tile)
    return x * x + y * y


def tile_angle(tile: tuple[int, int]) -> float:
    x, y = centroid(tile)
    return math.atan2(y, x)


def fixed_region_slots(region: str) -> list[tuple[str, str, int]]:
    return [
        (f"{region}_a01", "big", 4),
        (f"{region}_p01", "big", 4),
        (f"{region}_a02", "big", 4),
        (f"{region}_p02", "big", 4),
        (f"{region}_ks01", "keystone", 6),
        (f"{region}_a03", "big", 4),
        (f"{region}_p03", "big", 4),
        (f"{region}_a04", "big", 4),
        (f"{region}_p04", "big", 4),
        (f"{region}_ks02", "keystone", 6),
        (f"{region}_a05", "big", 4),
        (f"{region}_p05", "big", 4),
        (f"{region}_ks03", "keystone", 6),
        (f"{region}_g01", "giant", 12),
        (f"{region}_a06", "big", 4),
        (f"{region}_p06", "big", 4),
        (f"{region}_p07", "big", 4),
        (f"{region}_p08", "big", 4),
    ]


def partition_tiles_by_region() -> dict[str, set[tuple[int, int]]]:
    reserved = set(start_tiles())
    tiles = [tile for tile in ALL_TILES if tile not in reserved]
    tiles.sort(key=lambda tile: (tile_angle(tile), tile_distance(tile)))
    chunk_size = len(tiles) // len(REGION_ORDER)
    if chunk_size * len(REGION_ORDER) != len(tiles):
        raise RuntimeError("skill star chart tile count is not divisible by region count")

    return {
        region: set(tiles[index * chunk_size:(index + 1) * chunk_size])
        for index, region in enumerate(REGION_ORDER)
    }


def allocate_connected_group(
    remaining: set[tuple[int, int]],
    placed: set[tuple[int, int]],
    size: int,
    prefer_outer: bool = False,
) -> list[tuple[int, int]]:
    seed_key = (lambda tile: (-tile_distance(tile), tile_angle(tile), tile[0], tile[1])) if prefer_outer else (
        lambda tile: (tile_distance(tile), tile_angle(tile), tile[0], tile[1]))
    frontier_key = (lambda tile: (-tile_distance(tile), tile_angle(tile), tile)) if prefer_outer else (
        lambda tile: (tile_distance(tile), tile_angle(tile), tile))

    seeds = [tile for tile in remaining if any(neighbor in placed for neighbor in neighbors(tile))]
    seeds.sort(key=seed_key)
    for seed in seeds:
        group = [seed]
        group_set = {seed}
        frontier: list[tuple[float, float, tuple[int, int]]] = []
        for neighbor in neighbors(seed):
            if neighbor in remaining and neighbor not in group_set:
                heapq.heappush(frontier, frontier_key(neighbor))

        while len(group) < size and frontier:
            _, _, tile = heapq.heappop(frontier)
            if tile not in remaining or tile in group_set:
                continue
            if not any(neighbor in group_set for neighbor in neighbors(tile)):
                continue
            group.append(tile)
            group_set.add(tile)
            for neighbor in neighbors(tile):
                if neighbor in remaining and neighbor not in group_set:
                    heapq.heappush(frontier, frontier_key(neighbor))

        if len(group) == size and is_connected(group):
            return group

    raise RuntimeError(f"failed to allocate connected group size={size}, remaining={len(remaining)}")


def maximum_tile_matching(tiles: set[tuple[int, int]]) -> tuple[list[tuple[tuple[int, int], tuple[int, int]]], list[tuple[int, int]]]:
    left = [tile for tile in tiles if dec(tile)[2] == 0]
    adjacency = {tile: [neighbor for neighbor in neighbors(tile) if neighbor in tiles] for tile in left}
    pair_left: dict[tuple[int, int], tuple[int, int]] = {}
    pair_right: dict[tuple[int, int], tuple[int, int]] = {}
    infinity = 10**9

    def bfs() -> tuple[bool, dict[tuple[int, int], int]]:
        queue: deque[tuple[int, int]] = deque()
        distance: dict[tuple[int, int], int] = {}
        found_free_right = False
        for tile in left:
            if tile not in pair_left:
                distance[tile] = 0
                queue.append(tile)
            else:
                distance[tile] = infinity

        while queue:
            tile = queue.popleft()
            for right in adjacency[tile]:
                paired_left = pair_right.get(right)
                if paired_left is None:
                    found_free_right = True
                elif distance.get(paired_left, infinity) == infinity:
                    distance[paired_left] = distance[tile] + 1
                    queue.append(paired_left)

        return found_free_right, distance

    def dfs(tile: tuple[int, int], distance: dict[tuple[int, int], int]) -> bool:
        for right in adjacency[tile]:
            paired_left = pair_right.get(right)
            if paired_left is None or (
                distance.get(paired_left, infinity) == distance[tile] + 1 and dfs(paired_left, distance)
            ):
                pair_left[tile] = right
                pair_right[right] = tile
                return True
        distance[tile] = infinity
        return False

    while True:
        found, distance = bfs()
        if not found:
            break
        for tile in left:
            if tile not in pair_left:
                dfs(tile, distance)

    pairs = [(left_tile, right_tile) for left_tile, right_tile in pair_left.items()]
    matched = {tile for pair in pairs for tile in pair}
    unmatched = [tile for tile in tiles if tile not in matched]
    return pairs, unmatched


def build_region_layout(region: str, available_tiles: set[tuple[int, int]]) -> list[tuple[str, str, list[tuple[int, int]]]]:
    remaining = set(available_tiles)
    placed = set(start_tiles())
    result: list[tuple[str, str, list[tuple[int, int]]]] = []

    for node_id, node_type, size in fixed_region_slots(region):
        tiles = allocate_connected_group(remaining, placed, size, prefer_outer=node_type == "giant")
        result.append((node_id, node_type, tiles))
        tile_set = set(tiles)
        remaining -= tile_set
        placed |= tile_set

    pairs, unmatched = maximum_tile_matching(remaining)
    while len(unmatched) < 3 and pairs:
        left, right = pairs.pop()
        unmatched.extend([left, right])

    if not (1 <= len(unmatched) <= 9):
        raise RuntimeError(f"{region} pip count {len(unmatched)} outside design range")

    small_index = 1
    for left, right in pairs:
        result.append((f"{region}_s{small_index:03d}", "small", [left, right]))
        small_index += 1

    pip_index = 1
    for tile in sorted(unmatched):
        result.append((f"{region}_pip{pip_index:02d}", "pip", [tile]))
        pip_index += 1

    if sum(len(tiles) for _, _, tiles in result) != len(available_tiles):
        raise RuntimeError(f"{region} did not consume all assigned tiles")

    return result


def pick_bridge_nodes(
    assigned: dict[str, set[tuple[int, int]]],
    region_nodes: dict[str, list[tuple[str, str, list[tuple[int, int]]]]],
) -> dict[str, tuple[str, str]]:
    bridge_specs = [
        ("trans_sd01", "str", "dex"),
        ("trans_dc01", "dex", "con"),
        ("trans_ci01", "con", "int"),
        ("trans_iw01", "int", "wis"),
        ("trans_wc01", "wis", "cha"),
        ("trans_cs01", "cha", "str"),
    ]
    selected: dict[str, tuple[str, str]] = {}
    used_node_ids: set[str] = set()

    for bridge_id, left, right in bridge_specs:
        candidates: list[tuple[float, str]] = []
        right_tiles = assigned[right]
        for node_id, node_type, tiles in region_nodes[left]:
            if node_type != "small" or node_id in used_node_ids:
                continue
            if any(neighbor in right_tiles for tile in tiles for neighbor in neighbors(tile)):
                candidates.append((-tile_distance(tiles[0]), node_id))

        if not candidates:
            raise RuntimeError(f"failed to find bridge candidate for {left}-{right}")

        candidates.sort()
        selected[candidates[0][1]] = (bridge_id, right)
        used_node_ids.add(candidates[0][1])

    return selected


def build() -> tuple[dict, dict]:
    layout_nodes = [
        {
            "id": "start",
            "region": "start",
            "type": "start",
            "depth": 0,
            "tiles": [to_triplet(t) for t in start_tiles()],
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

    assigned = partition_tiles_by_region()
    region_nodes = {
        region: build_region_layout(region, assigned[region])
        for region in REGION_SEQUENCE
    }
    bridge_replacements = pick_bridge_nodes(assigned, region_nodes)

    for region in REGION_SEQUENCE:
        for depth, (node_id, node_type, tiles) in enumerate(region_nodes[region], 1):
            if node_id in bridge_replacements:
                bridge_id, right = bridge_replacements[node_id]
                layout_nodes.append(
                    {
                        "id": bridge_id,
                        "region": "transition",
                        "type": "small",
                        "depth": depth,
                        "isBridge": True,
                        "tiles": [to_triplet(t) for t in tiles],
                    }
                )
                content_nodes.append(
                    {
                        "id": bridge_id,
                        "name": "过渡星纹",
                        "description": f"{region.upper()} 与 {right.upper()} 之间的横跨桥点。",
                        "contentMode": "random_attribute",
                        "statBonuses": {"all_save": 1},
                    }
                )
                continue

            layout_nodes.append(
                {
                    "id": node_id,
                    "region": region,
                    "type": node_type,
                    "depth": depth,
                    "tiles": [to_triplet(t) for t in tiles],
                }
            )
            content_nodes.append(make_content(region, node_id, node_type))

    for region, tiles in assigned.items():
        consumed = sum(
            len(node_tiles)
            for _, _, node_tiles in region_nodes[region]
        )
        if consumed != len(tiles):
            raise RuntimeError(f"{region} consumed {consumed}/{len(tiles)} assigned tiles")

    layout = {"version": 1, "hexRadius": HEX_RADIUS, "nodes": layout_nodes}
    content = {
        "version": 1,
        "randomPools": {
            region: [
                {"stat": stat, "min": mn, "max": mx, "weight": weight}
                for stat, mn, mx, weight in entries
            ]
            for region, entries in RANDOM_POOLS.items()
        },
        "nodes": content_nodes,
    }
    validate(layout)
    return layout, content

def make_content(region: str, node_id: str, node_type: str) -> dict:
    if node_type in {"small", "pip"}:
        return {
            "id": node_id,
            "name": "属性星纹" if node_type == "small" else "微光星点",
            "description": "按角色种子从本扇区风格池生成纯属性。",
            "contentMode": "random_attribute",
            "seed": stable_seed(node_id),
        }

    if node_type == "giant":
        name, desc = GIANTS[region]
        return {
            "id": node_id,
            "name": name,
            "description": desc,
            "effect": "spell_slot_4" if region == "int" else f"{region}_giant_apex",
            "isActiveSkill": region != "int",
            "figureName": f"{name}巨型命座",
        }

    if node_type == "keystone":
        idx = int(node_id[-2:]) - 1
        _, name, benefit, cost, effect = KEYSTONES[region][idx]
        return {
            "id": node_id,
            "name": name,
            "description": benefit,
            "effect": effect,
            "keystoneCost": cost,
            "costBonuses": keystone_cost_bonus(region, idx),
        }

    suffix = node_id.split("_")[-1]
    idx = int(suffix[1:]) - 1
    if suffix.startswith("a"):
        name, effect = ACTIVES[region][idx]
        return {
            "id": node_id,
            "name": name,
            "description": f"主动：{name}。具体规则见技能星盘节点内容设计。",
            "effect": effect,
            "isActiveSkill": True,
        }

    name = PASSIVES[region][idx]
    return {
        "id": node_id,
        "name": name,
        "description": f"被动：{name}。具体规则见技能星盘节点内容设计。",
        "effect": f"{node_id}_passive",
        "isActiveSkill": False,
        "statBonuses": passive_bonus(region, idx),
    }


def passive_bonus(region: str, idx: int) -> dict:
    table = {
        "str": [{"melee_damage": 1}, {"melee_damage": 1}, {"melee_damage": 1}, {"melee_damage": 1}, {"max_hp": 5}, {"melee_damage": 1}, {"melee_damage": 1}, {"melee_damage": 1}],
        "dex": [{"ranged_damage": 1}, {"ranged_damage": 1}, {"ranged_damage": 1}, {"ranged_damage": 1}, {"critical_rate": 0.05}, {"speed": 1}, {"ranged_damage": 1}, {"ranged_damage": 1}],
        "con": [{"ac": 1}, {"max_hp": 5}, {"max_hp": 5}, {"ac": 1}, {"all_save": 1}, {"heal_amount": 1}, {"ac": 1}, {"ac": 1}],
        "int": [{"spell_damage": 1}, {"spell_damage": 1}, {"spell_damage": 1}, {"mana_max": 5}, {"mana_regen": 1}, {"spell_hit": 1}, {"spell_damage": 1}, {"spell_damage": 1}],
        "wis": [{"critical_rate": 0.05}, {"critical_rate": 0.05}, {"critical_rate": 0.05}, {"critical_rate": 0.05}, {"mana_max": 5}, {"critical_rate": 0.03}, {"critical_rate": 0.05}, {"critical_rate": 0.05}],
        "cha": [{"ally_bonus": 1}, {"ally_bonus": 1}, {"ally_bonus": 1}, {"ally_bonus": 1}, {"initiative": 1}, {"initiative": 2}, {"max_hp": 5}, {"ally_bonus": 1}],
    }
    return table[region][idx]


def keystone_cost_bonus(region: str, idx: int) -> dict:
    costs = {
        "str": [{"ac": -3}, {"critical_rate": -1.0}, {"heal_amount": -99}],
        "dex": [{"max_hp": -10}, {"ac": -4}, {"range_bonus": -2}],
        "con": [{"speed": -2}, {"ranged_damage": -2}, {"speed": -2}],
        "int": [{"spell_hit": 1}, {"mana_max": -99}, {"max_hp": -20}],
        "wis": [{"ac": -1}, {"critical_rate": -0.05}, {"max_hp": -10}],
        "cha": [{"max_hp": -10}, {"ally_bonus": -1}, {"ac": -2}],
    }
    return costs[region][idx]


def stable_seed(text: str) -> int:
    value = 17
    for ch in text:
        value = (value * 31 + ord(ch)) & 0x7FFFFFFF
    return value


def validate(layout: dict) -> None:
    owner: dict[tuple[int, int], str] = {}
    node_tiles: dict[str, list[tuple[int, int]]] = {}
    for node in layout["nodes"]:
        tiles = [enc(q, r, t) for q, r, t in node["tiles"]]
        node_tiles[node["id"]] = tiles
        if not is_connected(tiles):
            raise RuntimeError(f"{node['id']} is internally disconnected")
        for tile in tiles:
            if tile in owner:
                raise RuntimeError(f"{node['id']} overlaps {owner[tile]} at {tile}")
            owner[tile] = node["id"]

    adjacent = {node_id: set() for node_id in node_tiles}
    for node_id, tiles in node_tiles.items():
        for tile in tiles:
            for nb in neighbors(tile):
                other = owner.get(nb)
                if other and other != node_id:
                    adjacent[node_id].add(other)
                    adjacent[other].add(node_id)

    seen = {"start"}
    queue = deque(["start"])
    while queue:
        current = queue.popleft()
        for other in adjacent[current]:
            if other not in seen:
                seen.add(other)
                queue.append(other)
    if len(seen) != len(node_tiles):
        raise RuntimeError(f"start reaches {len(seen)}/{len(node_tiles)} nodes")


def is_connected(tiles: list[tuple[int, int]]) -> bool:
    if len(tiles) <= 1:
        return True
    tile_set = set(tiles)
    seen = {tiles[0]}
    queue = deque([tiles[0]])
    while queue:
        current = queue.popleft()
        for nb in neighbors(current):
            if nb in tile_set and nb not in seen:
                seen.add(nb)
                queue.append(nb)
    return len(seen) == len(tiles)


def main() -> None:
    layout, content = build()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / "skill_tree_layout.json").write_text(json.dumps(layout, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (OUT_DIR / "skill_tree_content.json").write_text(json.dumps(content, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {len(layout['nodes'])} layout nodes to {OUT_DIR}")


if __name__ == "__main__":
    main()
