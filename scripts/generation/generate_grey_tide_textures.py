from __future__ import annotations

import json
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "src" / "assets" / "tiles" / "grey_tide"
TEXTURES = OUT / "textures"
MASKS = OUT / "masks"
SEED = 34034


def clamp(v: float, lo: float = 0.0, hi: float = 1.0) -> float:
    return max(lo, min(hi, v))


def smoothstep(edge0: float, edge1: float, x: float) -> float:
    if edge0 == edge1:
        return 1.0 if x >= edge1 else 0.0
    t = clamp((x - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


def value_noise(size: int, cells: int, seed: int) -> list[list[float]]:
    rng = random.Random(seed)
    grid = [[rng.random() for _ in range(cells)] for _ in range(cells)]
    result = [[0.0 for _ in range(size)] for _ in range(size)]
    for y in range(size):
        gy = y / size * cells
        y0 = int(math.floor(gy)) % cells
        y1 = (y0 + 1) % cells
        fy = gy - math.floor(gy)
        fy = fy * fy * (3.0 - 2.0 * fy)
        for x in range(size):
            gx = x / size * cells
            x0 = int(math.floor(gx)) % cells
            x1 = (x0 + 1) % cells
            fx = gx - math.floor(gx)
            fx = fx * fx * (3.0 - 2.0 * fx)
            a = grid[y0][x0]
            b = grid[y0][x1]
            c = grid[y1][x0]
            d = grid[y1][x1]
            result[y][x] = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
    return result


def fbm(size: int, seed: int, octaves: tuple[tuple[int, float], ...]) -> list[list[float]]:
    layers = [(value_noise(size, cells, seed + i * 101), weight) for i, (cells, weight) in enumerate(octaves)]
    total_weight = sum(weight for _, weight in layers)
    out = [[0.0 for _ in range(size)] for _ in range(size)]
    for y in range(size):
        for x in range(size):
            v = sum(layer[y][x] * weight for layer, weight in layers) / total_weight
            out[y][x] = clamp(v)
    return out


def save_luma(path: Path, data: list[list[float]]) -> None:
    size = len(data)
    img = Image.new("L", (size, size))
    px = img.load()
    for y in range(size):
        for x in range(size):
            px[x, y] = int(clamp(data[y][x]) * 255)
    img.save(path)


def save_rgba(path: Path, pixels: list[list[tuple[int, int, int, int]]]) -> None:
    h = len(pixels)
    w = len(pixels[0])
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        for x in range(w):
            px[x, y] = pixels[y][x]
    img.save(path)


def generate_tileable_textures() -> list[dict[str, str]]:
    manifests: list[dict[str, str]] = []
    size = 1024

    large = fbm(size, SEED, ((5, 0.55), (11, 0.30), (23, 0.15)))
    save_luma(TEXTURES / "ash_noise_large.png", large)
    manifests.append({"file": "textures/ash_noise_large.png", "role": "Tileable low-frequency broken ash edge distortion."})

    fine = fbm(size, SEED + 1, ((32, 0.45), (67, 0.35), (137, 0.20)))
    save_luma(TEXTURES / "ash_noise_fine.png", fine)
    manifests.append({"file": "textures/ash_noise_fine.png", "role": "Tileable fine soot grain for charred parchment."})

    crack = fbm(size, SEED + 2, ((10, 0.35), (22, 0.35), (48, 0.30)))
    crack_img = Image.new("L", (size, size), 0)
    px = crack_img.load()
    for y in range(size):
        for x in range(size):
            v = crack[y][x]
            # Thin bright fissures with soft neighboring soot.
            fissure = smoothstep(0.66, 0.77, v) * (1.0 - smoothstep(0.88, 1.0, v))
            px[x, y] = int(fissure * 255)
    crack_img = crack_img.filter(ImageFilter.GaussianBlur(0.35))
    crack_img.save(TEXTURES / "char_cracks.png")
    manifests.append({"file": "textures/char_cracks.png", "role": "Tileable crack/fissure mask mixed into burned regions."})

    deposit_pixels: list[list[tuple[int, int, int, int]]] = []
    for y in range(size):
        row: list[tuple[int, int, int, int]] = []
        for x in range(size):
            soot = fine[y][x] * 0.65 + large[y][x] * 0.35
            r = int(82 + soot * 80)
            g = int(80 + soot * 76)
            b = int(72 + soot * 66)
            a = int(130 + soot * 95)
            row.append((r, g, b, a))
        deposit_pixels.append(row)
    save_rgba(TEXTURES / "ash_deposit_rgba.png", deposit_pixels)
    manifests.append({"file": "textures/ash_deposit_rgba.png", "role": "Semi-transparent ash deposit color texture for overlays or decals."})

    ramp = Image.new("RGBA", (256, 16))
    rpx = ramp.load()
    for x in range(256):
        t = x / 255
        # Black char -> ember orange -> pale ash.
        if t < 0.42:
            k = t / 0.42
            col = (
                int(18 + k * 94),
                int(16 + k * 33),
                int(14 + k * 4),
                255,
            )
        elif t < 0.60:
            k = (t - 0.42) / 0.18
            col = (
                int(112 + k * 143),
                int(49 + k * 64),
                int(18 + k * 14),
                255,
            )
        else:
            k = (t - 0.60) / 0.40
            col = (
                int(255 - k * 104),
                int(113 + k * 31),
                int(32 + k * 70),
                255,
            )
        for y in range(16):
            rpx[x, y] = col
    ramp.save(TEXTURES / "burn_edge_ramp.png")
    manifests.append({"file": "textures/burn_edge_ramp.png", "role": "1D burn gradient: charred core, ember rim, pale ash."})

    void_pixels: list[list[tuple[int, int, int, int]]] = []
    flow = fbm(size, SEED + 3, ((7, 0.45), (17, 0.35), (41, 0.20)))
    for y in range(size):
        row = []
        for x in range(size):
            swirl = math.sin((x * 0.018) + (flow[y][x] * 8.0)) * math.cos((y * 0.015) - (flow[y][x] * 5.0))
            v = clamp(0.5 + swirl * 0.25 + flow[y][x] * 0.25)
            row.append((int(92 + v * 98), int(92 + v * 98), int(86 + v * 108), 255))
        void_pixels.append(row)
    save_rgba(TEXTURES / "void_flow_greywhite.png", void_pixels)
    manifests.append({"file": "textures/void_flow_greywhite.png", "role": "Tileable grey-white void flow for AbyssGate interiors."})

    return manifests


def edge_mask(size: int, depth: float, jitter_strength: float, seed: int) -> Image.Image:
    n = fbm(size, seed, ((6, 0.55), (13, 0.30), (29, 0.15)))
    img = Image.new("L", (size, size), 0)
    px = img.load()
    for y in range(size):
        ny = y / (size - 1)
        for x in range(size):
            nx = x / (size - 1)
            dist_edge = min(nx, ny, 1.0 - nx, 1.0 - ny)
            front = depth + (n[y][x] - 0.5) * jitter_strength
            v = 1.0 - smoothstep(front * 0.70, front, dist_edge)
            px[x, y] = int(clamp(v) * 255)
    return img.filter(ImageFilter.GaussianBlur(0.8))


def abyss_gate_mask(size: int, center: tuple[float, float], radius: float, seed: int) -> Image.Image:
    n = fbm(size, seed, ((8, 0.50), (21, 0.30), (55, 0.20)))
    img = Image.new("L", (size, size), 0)
    px = img.load()
    cx, cy = center
    for y in range(size):
        ny = y / (size - 1)
        for x in range(size):
            nx = x / (size - 1)
            d = math.hypot(nx - cx, ny - cy)
            wobble = (n[y][x] - 0.5) * 0.09
            v = 1.0 - smoothstep(radius + wobble, radius + wobble + 0.055, d)
            px[x, y] = int(clamp(v) * 255)
    return img.filter(ImageFilter.GaussianBlur(0.6))


def generate_masks() -> list[dict[str, str]]:
    manifests: list[dict[str, str]] = []
    size = 768

    phases = [
        ("phase1_omen_edge_mask.png", 0.040, 0.030, "Stage 1 omen: faint outer scorch marks."),
        ("phase2_burning_front_mask.png", 0.145, 0.070, "Stage 2: edge-to-center burning front."),
        ("phase3_deep_invasion_mask.png", 0.270, 0.105, "Stage 3: deep irreversible burned area."),
    ]
    for i, (name, depth, jitter, role) in enumerate(phases):
        edge_mask(size, depth, jitter, SEED + 50 + i).save(MASKS / name)
        manifests.append({"file": f"masks/{name}", "role": role})

    gate = abyss_gate_mask(size, (0.22, 0.54), 0.055, SEED + 80)
    gate.save(MASKS / "abyss_gate_circular_mask.png")
    manifests.append({"file": "masks/abyss_gate_circular_mask.png", "role": "Circular burned-through AbyssGate mask, suitable for B channel/state masks."})

    burned = edge_mask(size, 0.27, 0.10, SEED + 90)
    burning = edge_mask(size, 0.205, 0.08, SEED + 91)
    threatened = edge_mask(size, 0.34, 0.12, SEED + 92)

    state = Image.new("RGBA", (size, size), (0, 0, 0, 255))
    spx = state.load()
    bpx = burned.load()
    fpx = burning.load()
    tpx = threatened.load()
    gpx = gate.load()
    for y in range(size):
        for x in range(size):
            burned_v = bpx[x, y]
            burning_v = max(0, fpx[x, y] - burned_v)
            threatened_v = max(0, tpx[x, y] - max(fpx[x, y], burned_v))
            gate_v = gpx[x, y]
            spx[x, y] = (
                max(burned_v, int(threatened_v * 0.42)),
                burning_v,
                gate_v,
                255,
            )
    state.save(MASKS / "invasion_state_channels_demo.png")
    manifests.append({"file": "masks/invasion_state_channels_demo.png", "role": "Debug RGBA state mask: R=ash/burned/threatened, G=burning front, B=AbyssGate."})

    return manifests


def write_manifest(items: list[dict[str, str]]) -> None:
    data = {
        "source_design": "docs/34-灰潮纪元终局.md",
        "seed": SEED,
        "usage": {
            "shader": "res://src/assets/shaders/overworld_parchment_2d.gdshader",
            "runtime_binder": "BladeHexFrontend/src/View/Map/MapAshController.cs",
            "dynamic_mask": "MapAshController maintains the live ash_data texture; files under masks/ are stage examples and debug sources.",
        },
        "assets": items,
    }
    (OUT / "grey_tide_manifest.json").write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def main() -> None:
    TEXTURES.mkdir(parents=True, exist_ok=True)
    MASKS.mkdir(parents=True, exist_ok=True)
    items = []
    items.extend(generate_tileable_textures())
    items.extend(generate_masks())
    write_manifest(items)
    print(f"generated {len(items)} grey tide assets under {OUT}")


if __name__ == "__main__":
    main()
