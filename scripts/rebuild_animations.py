import os
import json

base_dir = r"d:\123\Blade&Hex\assets\animations"

def ensure_dir(path):
    if not os.path.exists(path):
        os.makedirs(path)

# 定义默认的骨骼动作姿态
def create_bone_pose(rot=0.0, px=0.0, py=0.0, srot=0.0, sx=1.0, sy=1.0, easing="Linear"):
    return {
        "rotation_z": float(rot),
        "position_x": float(px),
        "position_y": float(py),
        "sprite_rotation": float(srot),
        "scale_x": float(sx),
        "scale_y": float(sy),
        "easing": str(easing)
    }

# 辅助函数：快速为关键帧填充部分未定义骨骼的默认姿态
def complete_bones(bones, defaults=None):
    all_bones = ["Torso", "Head", "ArmL", "ForearmL", "ArmR", "ForearmR", "Weapon"]
    result = {}
    for b in all_bones:
        if b in bones:
            result[b] = bones[b]
        elif defaults and b in defaults:
            result[b] = defaults[b]
        else:
            result[b] = create_bone_pose()
    return result

# ═══════════════════════════════════════════
# 1. Common 通用动画 (5个)
# ═══════════════════════════════════════════

def build_common_animations():
    common_path = os.path.join(base_dir, "common")
    ensure_dir(common_path)

    # 1.1 idle (待机呼吸)
    idle = {
        "name": "idle",
        "weapon_category": "common",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=0, py=0, sx=1.0, sy=1.0),
                    "Head": create_bone_pose(rot=0.0),
                    "ArmR": create_bone_pose(rot=5.0),
                    "ForearmR": create_bone_pose(rot=5.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=0, py=-4.0, sx=1.02, sy=0.98), # 向上呼吸拉伸
                    "Head": create_bone_pose(rot=2.0),
                    "ArmR": create_bone_pose(rot=2.0),
                    "ForearmR": create_bone_pose(rot=10.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=0, py=0, sx=1.0, sy=1.0),
                    "Head": create_bone_pose(rot=0.0),
                    "ArmR": create_bone_pose(rot=5.0),
                    "ForearmR": create_bone_pose(rot=5.0)
                })
            }
        ]
    }
    with open(os.path.join(common_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 1.2 move (踱步跑步)
    move = {
        "name": "move",
        "weapon_category": "common",
        "duration": 0.8,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=0, sx=1.0, sy=1.0),
                    "Head": create_bone_pose(rot=0.0),
                    "ArmR": create_bone_pose(rot=0.0),
                    "ArmL": create_bone_pose(rot=0.0)
                })
            },
            {
                "time": 0.2, # 腾空起跳
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-12.0, sx=0.94, sy=1.06), # 纵向拉伸
                    "Head": create_bone_pose(rot=-3.0),
                    "ArmR": create_bone_pose(rot=-25.0), # 摆臂后收
                    "ArmL": create_bone_pose(rot=25.0)   # 摆臂前抬
                })
            },
            {
                "time": 0.4, # 落地碰撞
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=4.0, sx=1.08, sy=0.92),  # 横向扁平压缩
                    "Head": create_bone_pose(rot=4.0),
                    "ArmR": create_bone_pose(rot=5.0),
                    "ArmL": create_bone_pose(rot=-5.0)
                })
            },
            {
                "time": 0.6, # 第二次腾空
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-12.0, sx=0.94, sy=1.06),
                    "Head": create_bone_pose(rot=-3.0),
                    "ArmR": create_bone_pose(rot=25.0),  # 反摆臂前抬
                    "ArmL": create_bone_pose(rot=-25.0)  # 反摆臂后收
                })
            },
            {
                "time": 0.8,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=0, sx=1.0, sy=1.0),
                    "Head": create_bone_pose(rot=0.0),
                    "ArmR": create_bone_pose(rot=0.0),
                    "ArmL": create_bone_pose(rot=0.0)
                })
            }
        ]
    }
    with open(os.path.join(common_path, "move.json"), "w", encoding="utf-8") as f:
        json.dump(move, f, indent=2, ensure_ascii=False)

    # 1.3 hit (受击)
    hit = {
        "name": "hit",
        "weapon_category": "common",
        "duration": 0.4,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "Head": create_bone_pose(),
                    "ArmR": create_bone_pose()
                })
            },
            {
                "time": 0.1, # 剧烈后仰与物理挤压
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=-20.0, px=-25.0, py=0.0, sx=1.25, sy=0.75, easing="BackOut"),
                    "Head": create_bone_pose(rot=-30.0),
                    "ArmR": create_bone_pose(rot=-45.0),
                    "Weapon": create_bone_pose(rot=-40.0)
                })
            },
            {
                "time": 0.4, # 弹性恢复
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=0.0, px=0.0, py=0.0, sx=1.0, sy=1.0),
                    "Head": create_bone_pose(rot=0.0),
                    "ArmR": create_bone_pose(rot=0.0),
                    "Weapon": create_bone_pose(rot=0.0)
                })
            }
        ]
    }
    with open(os.path.join(common_path, "hit.json"), "w", encoding="utf-8") as f:
        json.dump(hit, f, indent=2, ensure_ascii=False)

    # 1.4 die (死亡拍砸)
    die = {
        "name": "die",
        "weapon_category": "common",
        "duration": 0.8,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose()
                })
            },
            {
                "time": 0.2, # 击飞腾空
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=-35.0, px=-15.0, py=-35.0, sx=1.0, sy=1.0),
                    "Head": create_bone_pose(rot=-20.0),
                    "ArmR": create_bone_pose(rot=-40.0)
                })
            },
            {
                "time": 0.5, # 重摔砸地
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=90.0, px=-30.0, py=48.0, sx=1.2, sy=0.8, easing="Bounce"), # 物理弹跳余震
                    "Head": create_bone_pose(rot=15.0),
                    "ArmR": create_bone_pose(rot=70.0),
                    "Weapon": create_bone_pose(rot=60.0)
                })
            },
            {
                "time": 0.8, # 死亡颤抖定格
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=90.0, px=-30.0, py=48.0, sx=1.15, sy=0.85),
                    "Head": create_bone_pose(rot=20.0),
                    "ArmR": create_bone_pose(rot=75.0),
                    "Weapon": create_bone_pose(rot=65.0)
                })
            }
        ]
    }
    with open(os.path.join(common_path, "die.json"), "w", encoding="utf-8") as f:
        json.dump(die, f, indent=2, ensure_ascii=False)

    # 1.5 block (戒备格挡)
    block = {
        "name": "block",
        "weapon_category": "common",
        "duration": 0.6,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "Head": create_bone_pose(),
                    "ArmL": create_bone_pose(),
                    "ForearmL": create_bone_pose(),
                    "ArmR": create_bone_pose(),
                    "ForearmR": create_bone_pose()
                })
            },
            {
                "time": 0.3, # 防守蹲伏，举盾格挡
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=6.0, sx=1.04, sy=0.96), # 重心沉稳
                    "Head": create_bone_pose(rot=-3.0),
                    "ArmL": create_bone_pose(rot=45.0),     # 举盾
                    "ForearmL": create_bone_pose(rot=30.0),
                    "ArmR": create_bone_pose(rot=25.0),     # 武器防守倾斜
                    "ForearmR": create_bone_pose(rot=15.0)
                })
            },
            {
                "time": 0.6,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "Head": create_bone_pose(),
                    "ArmL": create_bone_pose(),
                    "ForearmL": create_bone_pose(),
                    "ArmR": create_bone_pose(),
                    "ForearmR": create_bone_pose()
                })
            }
        ]
    }
    with open(os.path.join(common_path, "block.json"), "w", encoding="utf-8") as f:
        json.dump(block, f, indent=2, ensure_ascii=False)

    print("Successfully built 5 common animations.")

# ═══════════════════════════════════════════
# 2. Slash 砍伤武器 (3个)
# ═══════════════════════════════════════════

def build_slash_animations():
    slash_path = os.path.join(base_dir, "slash")
    ensure_dir(slash_path)

    # 2.1 idle
    idle = {
        "name": "idle",
        "weapon_category": "slash",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=0),
                    "ArmR": create_bone_pose(rot=-5.0),
                    "Weapon": create_bone_pose(rot=-15.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-4.0, sx=1.02, sy=0.98),
                    "ArmR": create_bone_pose(rot=-3.0),
                    "Weapon": create_bone_pose(rot=-12.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=0),
                    "ArmR": create_bone_pose(rot=-5.0),
                    "Weapon": create_bone_pose(rot=-15.0)
                })
            }
        ]
    }
    with open(os.path.join(slash_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 2.2 attack_melee (经典的挥砍招式)
    attack = {
        "name": "attack_melee",
        "weapon_category": "slash",
        "duration": 0.6,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(),
                    "Weapon": create_bone_pose()
                })
            },
            {
                "time": 0.15, # 脑后拉刀蓄力
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=-10.0, py=4.0),
                    "ArmR": create_bone_pose(rot=-90.0),
                    "ForearmR": create_bone_pose(rot=-35.0),
                    "Weapon": create_bone_pose(rot=-60.0)
                })
            },
            {
                "time": 0.35, # 极张力暴力大劈斩！
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=25.0, px=24.0, py=8.0, sx=1.1, sy=0.9, easing="BackOut"),
                    "ArmR": create_bone_pose(rot=65.0),
                    "ForearmR": create_bone_pose(rot=20.0),
                    "Weapon": create_bone_pose(rot=45.0)
                })
            },
            {
                "time": 0.6, # 收招平复
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(),
                    "Weapon": create_bone_pose()
                })
            }
        ]
    }
    with open(os.path.join(slash_path, "attack_melee.json"), "w", encoding="utf-8") as f:
        json.dump(attack, f, indent=2, ensure_ascii=False)

    # 2.3 hit
    hit = {
        "name": "hit",
        "weapon_category": "slash",
        "duration": 0.4,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({})
            },
            {
                "time": 0.1,
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=-20.0, px=-25.0, sx=1.25, sy=0.75, easing="BackOut"),
                    "Head": create_bone_pose(rot=-30.0),
                    "ArmR": create_bone_pose(rot=-45.0),
                    "Weapon": create_bone_pose(rot=-40.0)
                })
            },
            {
                "time": 0.4,
                "bones": complete_bones({})
            }
        ]
    }
    with open(os.path.join(slash_path, "hit.json"), "w", encoding="utf-8") as f:
        json.dump(hit, f, indent=2, ensure_ascii=False)

    print("Successfully built 3 slash animations.")

# ═══════════════════════════════════════════
# 3. Thrust 刺伤武器 (2个)
# ═══════════════════════════════════════════

def build_thrust_animations():
    thrust_path = os.path.join(base_dir, "thrust")
    ensure_dir(thrust_path)

    # 3.1 idle
    idle = {
        "name": "idle",
        "weapon_category": "thrust",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-25.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-4.0, sx=1.02, sy=0.98),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=-20.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-25.0)
                })
            }
        ]
    }
    with open(os.path.join(thrust_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 3.2 attack_melee (前突刺击)
    attack = {
        "name": "attack_melee",
        "weapon_category": "thrust",
        "duration": 0.5,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-25.0)
                })
            },
            {
                "time": 0.12, # 双手拉回长枪蓄势
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=-5.0, px=-8.0),
                    "ArmR": create_bone_pose(rot=-45.0),
                    "ForearmR": create_bone_pose(rot=-80.0),
                    "Weapon": create_bone_pose(rot=-15.0)
                })
            },
            {
                "time": 0.26, # 暴烈挺刺，极限长枪直前伸！
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=35.0, sx=1.18, sy=0.85, easing="BackOut"),
                    "ArmR": create_bone_pose(rot=-5.0),
                    "ForearmR": create_bone_pose(rot=0.0),
                    "Weapon": create_bone_pose(rot=25.0)
                })
            },
            {
                "time": 0.5, # 收招
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-25.0)
                })
            }
        ]
    }
    with open(os.path.join(thrust_path, "attack_melee.json"), "w", encoding="utf-8") as f:
        json.dump(attack, f, indent=2, ensure_ascii=False)

    print("Successfully built 2 thrust animations.")

# ═══════════════════════════════════════════
# 4. Crush 钝伤武器 (2个)
# ═══════════════════════════════════════════

def build_crush_animations():
    crush_path = os.path.join(base_dir, "crush")
    ensure_dir(crush_path)

    # 4.1 idle
    idle = {
        "name": "idle",
        "weapon_category": "crush",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=15.0),
                    "Weapon": create_bone_pose(rot=20.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-4.0, sx=1.02, sy=0.98),
                    "ArmR": create_bone_pose(rot=10.0),
                    "Weapon": create_bone_pose(rot=22.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=15.0),
                    "Weapon": create_bone_pose(rot=20.0)
                })
            }
        ]
    }
    with open(os.path.join(crush_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 4.2 attack_melee (重锤劈地砸裂)
    attack = {
        "name": "attack_melee",
        "weapon_category": "crush",
        "duration": 0.7,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=15.0),
                    "Weapon": create_bone_pose(rot=20.0)
                })
            },
            {
                "time": 0.2, # 极度高抬大锤仰身蓄力
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=-20.0, px=-12.0, py=-6.0),
                    "ArmR": create_bone_pose(rot=-110.0),
                    "Weapon": create_bone_pose(rot=-75.0)
                })
            },
            {
                "time": 0.45, # 力拔千钧砸向地面！
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=30.0, px=20.0, py=18.0, sx=1.25, sy=0.75, easing="Bounce"), # 碰撞震颤
                    "ArmR": create_bone_pose(rot=-20.0),
                    "ForearmR": create_bone_pose(rot=15.0),
                    "Weapon": create_bone_pose(rot=75.0)
                })
            },
            {
                "time": 0.7, # 扛锤起立
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=15.0),
                    "Weapon": create_bone_pose(rot=20.0)
                })
            }
        ]
    }
    with open(os.path.join(crush_path, "attack_melee.json"), "w", encoding="utf-8") as f:
        json.dump(attack, f, indent=2, ensure_ascii=False)

    print("Successfully built 2 crush animations.")

# ═══════════════════════════════════════════
# 5. Bow 弓类武器 (2个)
# ═══════════════════════════════════════════

def build_bow_animations():
    bow_path = os.path.join(base_dir, "bow")
    ensure_dir(bow_path)

    # 5.1 idle
    idle = {
        "name": "idle",
        "weapon_category": "bow",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=5.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-4.0, sx=1.02, sy=0.98),
                    "ArmR": create_bone_pose(rot=-8.0),
                    "Weapon": create_bone_pose(rot=2.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=5.0)
                })
            }
        ]
    }
    with open(os.path.join(bow_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 5.2 attack_ranged (拉弓射击)
    attack = {
        "name": "attack_ranged",
        "weapon_category": "bow",
        "duration": 0.7,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=5.0, sx=1.0, sy=1.0)
                })
            },
            {
                "time": 0.3, # 抠弦用力拉满弓月
                "bones": complete_bones({
                    "Torso": create_bone_pose(rot=-8.0, py=3.0),
                    "ArmR": create_bone_pose(rot=-45.0),
                    "ForearmR": create_bone_pose(rot=-70.0),
                    "Weapon": create_bone_pose(rot=0.0, sx=0.85, sy=0.85) # 物理挤压变形表现拉满
                })
            },
            {
                "time": 0.4, # 松指一箭封喉！激弹反响！
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=-5.0, easing="BackOut"),
                    "ArmR": create_bone_pose(rot=-70.0),     # 右手松弦反弹
                    "ForearmR": create_bone_pose(rot=-30.0),
                    "Weapon": create_bone_pose(rot=15.0, sx=1.1, sy=1.1)  # 弦弹回，弓胎纵伸缩抖动
                })
            },
            {
                "time": 0.7, # 恢复
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=5.0, sx=1.0, sy=1.0)
                })
            }
        ]
    }
    with open(os.path.join(bow_path, "attack_ranged.json"), "w", encoding="utf-8") as f:
        json.dump(attack, f, indent=2, ensure_ascii=False)

    print("Successfully built 2 bow animations.")

# ═══════════════════════════════════════════
# 6. Crossbow 弩类武器 (2个)
# ═══════════════════════════════════════════

def build_crossbow_animations():
    crossbow_path = os.path.join(base_dir, "crossbow")
    ensure_dir(crossbow_path)

    # 6.1 idle
    idle = {
        "name": "idle",
        "weapon_category": "crossbow",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=10.0),
                    "Weapon": create_bone_pose(rot=0.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-4.0, sx=1.02, sy=0.98),
                    "ArmR": create_bone_pose(rot=12.0),
                    "Weapon": create_bone_pose(rot=-2.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=10.0),
                    "Weapon": create_bone_pose(rot=0.0)
                })
            }
        ]
    }
    with open(os.path.join(crossbow_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 6.2 attack_ranged (弩箭平射与弹性后坐力震颤)
    attack = {
        "name": "attack_ranged",
        "weapon_category": "crossbow",
        "duration": 0.6,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=10.0),
                    "Weapon": create_bone_pose(rot=0.0)
                })
            },
            {
                "time": 0.18, # 压弩抠火上弦
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=2.0),
                    "ArmR": create_bone_pose(rot=20.0),
                    "ForearmR": create_bone_pose(rot=-30.0),
                    "Weapon": create_bone_pose(rot=-10.0)
                })
            },
            {
                "time": 0.28, # 发射！暴烈后坐力！
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=-15.0, py=-8.0, sx=0.9, sy=1.1, easing="BackOut"), # 重震抛退
                    "ArmR": create_bone_pose(rot=-45.0),     # 右手暴甩震飞
                    "ForearmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-30.0)   # 弩口暴力向上扬飞
                })
            },
            {
                "time": 0.6, # 平复
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=10.0),
                    "Weapon": create_bone_pose(rot=0.0)
                })
            }
        ]
    }
    with open(os.path.join(crossbow_path, "attack_ranged.json"), "w", encoding="utf-8") as f:
        json.dump(attack, f, indent=2, ensure_ascii=False)

    print("Successfully built 2 crossbow animations.")

# ═══════════════════════════════════════════
# 7. Throw 投掷武器 (2个)
# ═══════════════════════════════════════════

def build_throw_animations():
    throw_path = os.path.join(base_dir, "throw")
    ensure_dir(throw_path)

    # 7.1 idle
    idle = {
        "name": "idle",
        "weapon_category": "throw",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-20.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-4.0, sx=1.02, sy=0.98),
                    "ArmR": create_bone_pose(rot=-12.0),
                    "Weapon": create_bone_pose(rot=-15.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-20.0)
                })
            }
        ]
    }
    with open(os.path.join(throw_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 7.2 attack_ranged (旋臂投飞刀)
    attack = {
        "name": "attack_ranged",
        "weapon_category": "throw",
        "duration": 0.5,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-20.0)
                })
            },
            {
                "time": 0.15, # 旋臂向脑后蓄力
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=-10.0),
                    "ArmR": create_bone_pose(rot=-90.0),
                    "Weapon": create_bone_pose(rot=-65.0)
                })
            },
            {
                "time": 0.28, # 全力劈臂甩出，手指张开！
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=22.0, py=6.0, easing="BackOut"), # 前冲
                    "ArmR": create_bone_pose(rot=75.0),
                    "ForearmR": create_bone_pose(rot=25.0),
                    "Weapon": create_bone_pose(rot=45.0)
                })
            },
            {
                "time": 0.5, # 收回
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "Weapon": create_bone_pose(rot=-20.0)
                })
            }
        ]
    }
    with open(os.path.join(throw_path, "attack_ranged.json"), "w", encoding="utf-8") as f:
        json.dump(attack, f, indent=2, ensure_ascii=False)

    print("Successfully built 2 throw animations.")

# ═══════════════════════════════════════════
# 8. Catalyst 法术媒介 (2个)
# ═══════════════════════════════════════════

def build_catalyst_animations():
    catalyst_path = os.path.join(base_dir, "catalyst")
    ensure_dir(catalyst_path)

    # 8.1 idle
    idle = {
        "name": "idle",
        "weapon_category": "catalyst",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=-15.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-4.0, sx=1.02, sy=0.98),
                    "ArmR": create_bone_pose(rot=-8.0),
                    "Weapon": create_bone_pose(rot=-12.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=-15.0)
                })
            }
        ]
    }
    with open(os.path.join(catalyst_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 8.2 cast (魔能冲天飞空爆发)
    cast = {
        "name": "cast",
        "weapon_category": "catalyst",
        "duration": 0.8,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=-15.0)
                })
            },
            {
                "time": 0.25, # 凝聚吸纳魔能
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=6.0, sx=1.08, sy=0.92), # 聚魔下沉压缩
                    "ArmR": create_bone_pose(rot=15.0),
                    "Weapon": create_bone_pose(rot=10.0, sx=1.15, sy=0.85) # 法杖高频魔能微动
                })
            },
            {
                "time": 0.48, # 魔能指天暴发！飞天狂甩！
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-22.0, sx=0.82, sy=1.35, easing="BackOut"), # 飞天拉伸
                    "ArmR": create_bone_pose(rot=-120.0),     # 高举法杖指天
                    "Weapon": create_bone_pose(rot=-90.0, sx=0.8, sy=1.35)   # 法杖纵向冲天拉伸
                })
            },
            {
                "time": 0.8, # 缓缓收势落地
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=-10.0),
                    "Weapon": create_bone_pose(rot=-15.0, sx=1.0, sy=1.0)
                })
            }
        ]
    }
    with open(os.path.join(catalyst_path, "cast.json"), "w", encoding="utf-8") as f:
        json.dump(cast, f, indent=2, ensure_ascii=False)

    print("Successfully built 2 catalyst animations.")

# ═══════════════════════════════════════════
# 9. Unarmed 徒手肉搏 (2个)
# ═══════════════════════════════════════════

def build_unarmed_animations():
    unarmed_path = os.path.join(base_dir, "unarmed")
    ensure_dir(unarmed_path)

    # 9.1 idle
    idle = {
        "name": "idle",
        "weapon_category": "unarmed",
        "duration": 2.0,
        "loop": True,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=15.0),
                    "ForearmR": create_bone_pose(rot=30.0)
                })
            },
            {
                "time": 1.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(py=-4.0, sx=1.02, sy=0.98),
                    "ArmR": create_bone_pose(rot=10.0),
                    "ForearmR": create_bone_pose(rot=25.0)
                })
            },
            {
                "time": 2.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=15.0),
                    "ForearmR": create_bone_pose(rot=30.0)
                })
            }
        ]
    }
    with open(os.path.join(unarmed_path, "idle.json"), "w", encoding="utf-8") as f:
        json.dump(idle, f, indent=2, ensure_ascii=False)

    # 9.2 attack_melee (右勾重拳)
    attack = {
        "name": "attack_melee",
        "weapon_category": "unarmed",
        "duration": 0.4,
        "loop": False,
        "keyframes": [
            {
                "time": 0.0,
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=15.0),
                    "ForearmR": create_bone_pose(rot=30.0)
                })
            },
            {
                "time": 0.08, # 拳头微后拉蓄力
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=-4.0),
                    "ArmR": create_bone_pose(rot=-15.0),
                    "ForearmR": create_bone_pose(rot=45.0)
                })
            },
            {
                "time": 0.18, # 重直拳轰出！极具力道！
                "bones": complete_bones({
                    "Torso": create_bone_pose(px=26.0, sx=1.15, sy=0.85, easing="BackOut"), # 拧腰前探
                    "ArmR": create_bone_pose(rot=60.0),
                    "ForearmR": create_bone_pose(rot=0.0) # 完全打直
                })
            },
            {
                "time": 0.4, # 收招
                "bones": complete_bones({
                    "Torso": create_bone_pose(),
                    "ArmR": create_bone_pose(rot=15.0),
                    "ForearmR": create_bone_pose(rot=30.0)
                })
            }
        ]
    }
    with open(os.path.join(unarmed_path, "attack_melee.json"), "w", encoding="utf-8") as f:
        json.dump(attack, f, indent=2, ensure_ascii=False)

    print("Successfully built 2 unarmed animations.")

# ═══════════════════════════════════════════
# 主运行程序入口
# ═══════════════════════════════════════════

if __name__ == "__main__":
    print("Rebuilding all 22 animations under the new premium TRS + Easing framework...")
    build_common_animations()
    build_slash_animations()
    build_thrust_animations()
    build_crush_animations()
    build_bow_animations()
    build_crossbow_animations()
    build_throw_animations()
    build_catalyst_animations()
    build_unarmed_animations()
    print("All 22 animations rebuilt and saved successfully!")
