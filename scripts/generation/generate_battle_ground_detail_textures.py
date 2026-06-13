from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "BladeHexFrontend" / "src" / "assets" / "tiles" / "battle_ground" / "overlays"
OUT_DIR = ROOT / "BladeHexFrontend" / "src" / "assets" / "tiles" / "battle_ground" / "detail"

SIZE = 1024
EDGE_BLEND = 96


def fit_square(image: Image.Image, size: int = SIZE) -> Image.Image:
    image = image.convert("RGBA")
    w, h = image.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    image = image.crop((left, top, left + side, top + side))
    if image.size != (size, size):
        image = image.resize((size, size), Image.Resampling.LANCZOS)
    return image


def soften_tile_edges(image: Image.Image, blend: int = EDGE_BLEND) -> Image.Image:
    image = image.convert("RGBA")
    w, h = image.size
    x_shift = ImageChops.offset(image, w // 2, 0)
    y_shift = ImageChops.offset(image, 0, h // 2)

    mask_x = Image.new("L", (w, h), 0)
    mask_y = Image.new("L", (w, h), 0)
    px_x = mask_x.load()
    px_y = mask_y.load()

    for x in range(w):
        dx = min(x, w - 1 - x)
        mx = int(max(0.0, 1.0 - dx / max(1, blend)) * 255)
        for y in range(h):
            px_x[x, y] = mx

    for y in range(h):
        dy = min(y, h - 1 - y)
        my = int(max(0.0, 1.0 - dy / max(1, blend)) * 255)
        for x in range(w):
            px_y[x, y] = my

    image = Image.composite(x_shift, image, mask_x.filter(ImageFilter.GaussianBlur(12)))
    image = Image.composite(y_shift, image, mask_y.filter(ImageFilter.GaussianBlur(12)))
    return image


def high_pass_detail(source: Image.Image, blur_radius: float, contrast: float, brightness: float) -> Image.Image:
    base = fit_square(source)
    blurred = base.filter(ImageFilter.GaussianBlur(blur_radius))
    detail = ImageChops.subtract(base, blurred, scale=1.0, offset=128)
    detail = ImageOps.grayscale(detail)
    detail = ImageEnhance.Contrast(detail).enhance(contrast)
    detail = ImageEnhance.Brightness(detail).enhance(brightness)
    detail = soften_tile_edges(detail)
    detail = ImageEnhance.Sharpness(detail).enhance(1.1)
    return detail.convert("RGBA")


def edge_delta(image: Image.Image) -> float:
    image = image.convert("RGBA")
    w, h = image.size
    px = image.load()
    total = 0.0
    samples = 0
    for y in range(h):
        left = px[0, y]
        right = px[w - 1, y]
        total += sum(abs(left[i] - right[i]) for i in range(3)) / 3.0
        samples += 1
    for x in range(w):
        top = px[x, 0]
        bottom = px[x, h - 1]
        total += sum(abs(top[i] - bottom[i]) for i in range(3)) / 3.0
        samples += 1
    return total / max(1, samples)


def save_texture(path: Path, image: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def main() -> None:
    sources = [
        {
            "name": "moss_grass",
            "source": SOURCE_DIR / "moss_grass_1254.png",
            "output": OUT_DIR / "moss_grass_detail.png",
            "blur_radius": 18.0,
            "contrast": 1.35,
            "brightness": 1.05,
            "role": "Fine grass and moss grain for plains and forest overlays.",
        },
        {
            "name": "stony_mud",
            "source": SOURCE_DIR / "stony_mud_1254.png",
            "output": OUT_DIR / "stony_mud_detail.png",
            "blur_radius": 20.0,
            "contrast": 1.25,
            "brightness": 1.00,
            "role": "Rough earth grain for dirt, wasteland, hills and sand fallback.",
        },
        {
            "name": "murky_water",
            "source": SOURCE_DIR / "murky_water_1254.png",
            "output": OUT_DIR / "murky_water_detail.png",
            "blur_radius": 16.0,
            "contrast": 1.20,
            "brightness": 0.96,
            "role": "Subtle ripple grain for water surfaces and pools.",
        },
    ]

    manifest = []
    for spec in sources:
        if not spec["source"].exists():
            raise FileNotFoundError(f"Missing source texture: {spec['source']}")

        image = Image.open(spec["source"])
        detail = high_pass_detail(
            image,
            blur_radius=spec["blur_radius"],
            contrast=spec["contrast"],
            brightness=spec["brightness"],
        )
        save_texture(spec["output"], detail)
        manifest.append(
            {
                "name": spec["name"],
                "source": str(spec["source"].relative_to(ROOT)),
                "output": str(spec["output"].relative_to(ROOT)),
                "edge_delta": round(edge_delta(detail), 3),
                "size": list(detail.size),
                "role": spec["role"],
            }
        )
        print(f"{spec['output']}  edge_delta={manifest[-1]['edge_delta']}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / "battle_ground_detail_manifest.json").write_text(
        json.dumps(
            {
                "size": SIZE,
                "edge_blend": EDGE_BLEND,
                "textures": manifest,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
