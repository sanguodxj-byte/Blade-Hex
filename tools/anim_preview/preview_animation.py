"""骨骼动画 JSON → 关键帧静态预览图。

把 assets/animations/**/*.json 里的关键帧渲染成一张横向拼图，方便交给视觉 AI
做姿态描述和迭代修订，无需启动 Godot。

仅渲染上半身骨骼（Torso/Head/ArmL/ForearmL/ArmR/ForearmR/Weapon），完全对照
docs/32-上半身骨骼动画系统设计.md 中的骨骼参数。

用法：
    python preview_animation.py <animation_json_path> [-o out.png] [--fps N]

示例：
    python tools/anim_preview/preview_animation.py assets/animations/slash/attack_melee.json
    python tools/anim_preview/preview_animation.py assets/animations/slash/attack_melee.json --fps 12

依赖：仅 Pillow（pip install pillow）
"""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


# 与 docs/32-上半身骨骼动画系统设计.md 一致的骨骼参数（像素）
BASE_PEDESTAL_TOP = 40
TORSO_HEIGHT = 48
SHOULDER_WIDTH = 24
SHOULDER_Y = 40
UPPER_ARM_LEN = 20
FOREARM_LEN = 18
HEAD_OFFSET = 52  # 头部中心相对躯干底部 Y
WEAPON_LEN = 28   # 武器视觉长度（doc 未指定，仅用于预览）

# 画布
CELL_W = 220
CELL_H = 280
GROUND_Y = CELL_H - 40        # 底座地面
ORIGIN_X = CELL_W // 2        # 角色中心列
ORIGIN_Y = GROUND_Y - BASE_PEDESTAL_TOP  # SkeletonRoot 在画布中的 Y

# 颜色
BG_COLOR = (32, 34, 40)
GRID_COLOR = (50, 54, 62)
PEDESTAL_COLOR = (90, 70, 50)
BONE_COLORS = {
    "Torso":    (220, 220, 220),
    "Head":     (240, 200, 160),
    "ArmL":     (140, 170, 240),
    "ForearmL": (100, 140, 220),
    "ArmR":     (240, 160, 140),
    "ForearmR": (220, 120, 100),
    "Weapon":   (200, 200, 100),
}
JOINT_COLOR = (255, 255, 255)
LABEL_COLOR = (200, 210, 220)


@dataclass
class BonePose:
    rotation_z: float = 0.0
    position_x: float = 0.0
    position_y: float = 0.0
    scale_x: float = 1.0
    scale_y: float = 1.0


def parse_pose(d: dict) -> BonePose:
    return BonePose(
        rotation_z=float(d.get("rotation_z", 0.0)),
        position_x=float(d.get("position_x", 0.0)),
        position_y=float(d.get("position_y", 0.0)),
        scale_x=float(d.get("scale_x", 1.0)),
        scale_y=float(d.get("scale_y", 1.0)),
    )


def empty_pose() -> BonePose:
    return BonePose()


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def lerp_pose(a: BonePose, b: BonePose, t: float) -> BonePose:
    return BonePose(
        rotation_z=lerp(a.rotation_z, b.rotation_z, t),
        position_x=lerp(a.position_x, b.position_x, t),
        position_y=lerp(a.position_y, b.position_y, t),
        scale_x=lerp(a.scale_x, b.scale_x, t),
        scale_y=lerp(a.scale_y, b.scale_y, t),
    )


def sample_pose(keyframes: list[dict], bone: str, t: float) -> BonePose:
    """在 keyframes 中按时间线性插值得到指定骨骼的姿态。"""
    if not keyframes:
        return empty_pose()

    # 找到 t 所在的两个相邻帧
    times = [kf["time"] for kf in keyframes]
    if t <= times[0]:
        return parse_pose(keyframes[0]["bones"].get(bone, {}))
    if t >= times[-1]:
        return parse_pose(keyframes[-1]["bones"].get(bone, {}))

    for i in range(len(times) - 1):
        if times[i] <= t <= times[i + 1]:
            t0, t1 = times[i], times[i + 1]
            span = t1 - t0
            local_t = 0.0 if span == 0 else (t - t0) / span
            a = parse_pose(keyframes[i]["bones"].get(bone, {}))
            b = parse_pose(keyframes[i + 1]["bones"].get(bone, {}))
            return lerp_pose(a, b, local_t)
    return empty_pose()


def rotate(x: float, y: float, deg: float) -> tuple[float, float]:
    """绕原点旋转，注意游戏内 +rotation_z 为逆时针，画布 y 向下，所以这里翻号。"""
    r = math.radians(-deg)
    c, s = math.cos(r), math.sin(r)
    return x * c - y * s, x * s + y * c


def draw_bone_segment(
    draw: ImageDraw.ImageDraw,
    start: tuple[float, float],
    end: tuple[float, float],
    color: tuple[int, int, int],
    width: int = 6,
) -> None:
    draw.line([start, end], fill=color, width=width)
    draw.ellipse(
        [start[0] - 4, start[1] - 4, start[0] + 4, start[1] + 4],
        fill=JOINT_COLOR,
    )


def draw_pedestal(draw: ImageDraw.ImageDraw, cx: int) -> None:
    w, h = 60, 14
    x0 = cx - w // 2
    y0 = GROUND_Y - h // 2
    draw.rectangle([x0, y0, x0 + w, y0 + h], fill=PEDESTAL_COLOR, outline=(0, 0, 0))


def draw_grid(draw: ImageDraw.ImageDraw) -> None:
    # 地平线
    draw.line([(0, GROUND_Y), (CELL_W, GROUND_Y)], fill=GRID_COLOR, width=1)
    # 中心垂直线
    draw.line([(ORIGIN_X, 0), (ORIGIN_X, CELL_H)], fill=GRID_COLOR, width=1)


def render_frame(
    keyframes: list[dict],
    t: float,
    label: str,
    font: ImageFont.ImageFont,
) -> Image.Image:
    img = Image.new("RGB", (CELL_W, CELL_H), BG_COLOR)
    draw = ImageDraw.Draw(img)
    draw_grid(draw)
    draw_pedestal(draw, ORIGIN_X)

    # 取每根骨骼当前姿态（仅使用 rotation_z 和 Torso 的 position）
    p = {b: sample_pose(keyframes, b, t) for b in BONE_COLORS}

    # 计算每根骨头在画布上的关节坐标。约定：
    # - 画布 +x 朝右，+y 朝下
    # - 游戏数据 +rotation_z 为逆时针（已在 rotate 中处理）
    # - 游戏数据 +position_y 为 "向上"（含义为画布里 -y），所以这里翻号
    torso_root_x = ORIGIN_X + p["Torso"].position_x
    torso_root_y = ORIGIN_Y - p["Torso"].position_y

    # 躯干本体：画一根从 root 向上 TORSO_HEIGHT 的"芯轴"
    torso_top_local = (0, -TORSO_HEIGHT)
    rx, ry = rotate(*torso_top_local, p["Torso"].rotation_z)
    torso_top = (torso_root_x + rx, torso_root_y + ry)
    draw_bone_segment(
        draw,
        (torso_root_x, torso_root_y),
        torso_top,
        BONE_COLORS["Torso"],
        width=10,
    )

    # 工具：把"躯干局部坐标 (lx, ly)"换算到画布坐标
    def torso_local_to_canvas(lx: float, ly: float) -> tuple[float, float]:
        rxx, ryy = rotate(lx, -ly, p["Torso"].rotation_z)
        return torso_root_x + rxx, torso_root_y + ryy

    # 头部
    head_root = torso_local_to_canvas(0, HEAD_OFFSET)
    # 头部相对躯干又有自己的 rotation_z
    head_dir_local = (0, -16)  # 画一段表示头朝向
    hx, hy = rotate(*head_dir_local, p["Torso"].rotation_z + p["Head"].rotation_z)
    head_tip = (head_root[0] + hx, head_root[1] + hy)
    draw_bone_segment(draw, head_root, head_tip, BONE_COLORS["Head"], width=14)
    # 头部圆
    draw.ellipse(
        [head_root[0] - 9, head_root[1] - 9, head_root[0] + 9, head_root[1] + 9],
        fill=BONE_COLORS["Head"],
        outline=(0, 0, 0),
    )

    # 左右肩 → 上臂 → 前臂
    for side in ("L", "R"):
        sw = -SHOULDER_WIDTH if side == "L" else SHOULDER_WIDTH
        shoulder = torso_local_to_canvas(sw, SHOULDER_Y)

        arm_rot = p["Torso"].rotation_z + p[f"Arm{side}"].rotation_z
        # 上臂默认指向"向下"（局部 +y），画布 y 朝下，因此局部 (0, UPPER_ARM_LEN)
        ax, ay = rotate(0, UPPER_ARM_LEN, arm_rot)
        elbow = (shoulder[0] + ax, shoulder[1] + ay)
        draw_bone_segment(draw, shoulder, elbow, BONE_COLORS[f"Arm{side}"], width=7)

        forearm_rot = arm_rot + p[f"Forearm{side}"].rotation_z
        fx, fy = rotate(0, FOREARM_LEN, forearm_rot)
        wrist = (elbow[0] + fx, elbow[1] + fy)
        draw_bone_segment(
            draw,
            elbow,
            wrist,
            BONE_COLORS[f"Forearm{side}"],
            width=6,
        )

        # 武器只挂在右手
        if side == "R":
            weapon_rot = forearm_rot + p["Weapon"].rotation_z
            wx, wy = rotate(0, WEAPON_LEN, weapon_rot)
            weapon_tip = (wrist[0] + wx, wrist[1] + wy)
            draw_bone_segment(
                draw,
                wrist,
                weapon_tip,
                BONE_COLORS["Weapon"],
                width=4,
            )

    # 标签
    draw.text((6, 4), label, fill=LABEL_COLOR, font=font)
    return img


def load_font() -> ImageFont.ImageFont:
    # 尽量用系统字体，找不到就退回默认位图字体
    candidates = [
        r"C:\Windows\Fonts\consola.ttf",
        r"C:\Windows\Fonts\arial.ttf",
        "/System/Library/Fonts/SFNSMono.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
    ]
    for p in candidates:
        if Path(p).exists():
            try:
                return ImageFont.truetype(p, 14)
            except Exception:
                continue
    return ImageFont.load_default()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("animation", type=Path, help="动画 JSON 路径")
    parser.add_argument("-o", "--output", type=Path, default=None, help="输出 PNG")
    parser.add_argument(
        "--fps",
        type=int,
        default=0,
        help="若 >0，按 fps 等距采样而不是只渲染关键帧",
    )
    parser.add_argument(
        "--scale",
        type=float,
        default=1.0,
        help="输出整体放大倍数（默认 1.0）",
    )
    args = parser.parse_args()

    data = json.loads(args.animation.read_text(encoding="utf-8"))
    keyframes: list[dict] = data["keyframes"]
    duration: float = float(data.get("duration", keyframes[-1]["time"]))

    # 决定渲染哪些时间点
    if args.fps > 0:
        n = max(2, int(round(duration * args.fps)) + 1)
        times = [duration * i / (n - 1) for i in range(n)]
    else:
        times = [kf["time"] for kf in keyframes]

    font = load_font()
    frames = [
        render_frame(
            keyframes,
            t,
            f"t={t:.2f}s ({i + 1}/{len(times)})",
            font,
        )
        for i, t in enumerate(times)
    ]

    sheet = Image.new("RGB", (CELL_W * len(frames), CELL_H + 24), BG_COLOR)
    for i, f in enumerate(frames):
        sheet.paste(f, (i * CELL_W, 24))

    # 顶部标题
    draw = ImageDraw.Draw(sheet)
    title = (
        f"{data.get('name', args.animation.stem)} | "
        f"category={data.get('weapon_category', '?')} | "
        f"duration={duration:.2f}s | "
        f"loop={data.get('loop', False)}"
    )
    draw.text((6, 4), title, fill=(255, 255, 255), font=font)

    if args.scale != 1.0:
        new_size = (int(sheet.width * args.scale), int(sheet.height * args.scale))
        sheet = sheet.resize(new_size, Image.LANCZOS)

    out = args.output or args.animation.with_suffix(".preview.png")
    sheet.save(out)
    print(f"saved: {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
