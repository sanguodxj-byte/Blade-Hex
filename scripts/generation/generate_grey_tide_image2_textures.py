from __future__ import annotations

import argparse
import base64
import io
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path

import requests
from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "src" / "assets" / "tiles" / "grey_tide" / "image2"
RAW = OUT / "raw"
PROCESSED = OUT / "processed"

API_URL = os.environ.get("BH_IMAGE_API_URL", "http://127.0.0.1:17200/v1/images/generations")
API_KEY = os.environ.get("BH_IMAGE_API_KEY", "sk-klein-q-bdA7G5SelTQFtfvrvHl5ybZnzPvAyoX2RGzbVq")
MODEL = os.environ.get("BH_IMAGE_MODEL", "gpt-image-2")


@dataclass(frozen=True)
class PromptSpec:
    key: str
    out: str
    prompt: str
    role: str
    derive_mask: bool = False
    tileable: bool = True


COMMON_NEGATIVE = (
    "No text, no letters, no numbers, no logo, no UI, no objects, no creatures, "
    "no characters, no frame, no hard vignette, no central illustration unless requested."
)


PROMPTS: tuple[PromptSpec, ...] = (
    PromptSpec(
        key="grey_parchment_deposit",
        out="image2_grey_parchment_deposit.png",
        role="Color detail texture for grey tide dust deposits over the parchment overworld.",
        prompt=(
            "Seamless tileable 1024x1024 game material texture for an antique parchment world map surface "
            "covered by grey mineral powder deposits. Weathered paper fibers, pale grey dust, soft smoky staining, "
            "subtle dry grain, muted bone grey with faint warm parchment undertones. Top-down orthographic texture, "
            "even lighting, no directional shadow. Designed for a shader detail map. "
            f"{COMMON_NEGATIVE}"
        ),
    ),
    PromptSpec(
        key="fine_powder_grain",
        out="image2_fine_powder_grain.png",
        role="High-frequency grain texture to replace or layer with procedural ash_noise_fine.",
        prompt=(
            "Seamless tileable 1024x1024 monochrome grey powder grain texture. Fine dry particles, paper fiber noise, "
            "subtle cloudy variation, mostly mid grey values with small pale flecks. Top-down flat material scan style, "
            "not photographic dirt, not fabric, not stone. Useful as a high-frequency shader noise layer. "
            f"{COMMON_NEGATIVE}"
        ),
    ),
    PromptSpec(
        key="dark_fissure_mask_source",
        out="image2_dark_fissure_mask_source.png",
        role="Source image for deriving char_cracks-style mask from dark line work.",
        derive_mask=True,
        prompt=(
            "Seamless tileable 1024x1024 source texture of thin dark irregular branching fissure lines across old parchment. "
            "Dark grey ink-like separations, varied line thickness, mostly neutral grey paper background, high contrast but "
            "not pure black and white. Lines continue naturally through the image edges for tiling. Intended to be converted "
            "into a shader mask for surface line details. "
            f"{COMMON_NEGATIVE}"
        ),
    ),
    PromptSpec(
        key="greywhite_flow",
        out="image2_greywhite_flow.png",
        role="Grey-white flowing texture for terminal-era special marker interiors.",
        prompt=(
            "Seamless tileable 1024x1024 abstract grey-white flowing texture for a fantasy world map shader. Pale mineral "
            "currents, cloudy grey-white streams, faint opalescent swirls, soft motion implied by brush texture. Keep the "
            "palette grey, white, bone, and a little muted charcoal; avoid saturated blue and purple. "
            f"{COMMON_NEGATIVE}"
        ),
    ),
    PromptSpec(
        key="circular_marker_source",
        out="image2_circular_marker_source.png",
        role="Centered circular source art for deriving AbyssGate/terminal marker masks.",
        derive_mask=True,
        tileable=False,
        prompt=(
            "Top-down 1024x1024 game texture source for a centered circular grey terminal-era map marker on antique parchment. "
            "Ragged dark circular rim, curled dry paper edge, pale grey-white cloudy center, powder scattered around the ring, "
            "painterly hand-crafted texture matching parchment map art. Opaque square image with clear circular composition. "
            "No letters, no icons, no UI glyphs, no creatures, no character silhouettes."
        ),
    ),
    PromptSpec(
        key="edge_front_mask_source",
        out="image2_edge_front_mask_source.png",
        role="Source image for a map-edge grey tide advance mask.",
        derive_mask=True,
        tileable=False,
        prompt=(
            "1024x1024 top-down mask source for a fantasy parchment overworld where pale grey powder advances inward from "
            "the outer edges. Irregular soft edge-fronts, drifting dusty gradients, darker thin boundary lines in places, "
            "clear untouched parchment near the center. Designed to become a grayscale shader mask. "
            "No letters, no icons, no UI, no characters, no objects, no frame."
        ),
    ),
)


def selected_prompts(only: set[str] | None) -> list[PromptSpec]:
    if not only:
        return list(PROMPTS)
    known = {spec.key for spec in PROMPTS}
    unknown = sorted(only - known)
    if unknown:
        raise SystemExit(f"Unknown prompt key(s): {', '.join(unknown)}. Known: {', '.join(sorted(known))}")
    return [spec for spec in PROMPTS if spec.key in only]


def extract_image_bytes(data: dict) -> bytes:
    b64_json = data.get("data", [{}])[0].get("b64_json", "")
    if b64_json:
        return base64.b64decode(b64_json)

    content = data.get("choices", [{}])[0].get("message", {}).get("content", "")
    if isinstance(content, str):
        match = re.search(r"data:image/[^;]+;base64,([A-Za-z0-9+/=\s]+)", content)
        if match:
            return base64.b64decode(match.group(1).replace("\n", "").replace(" ", ""))
        if len(content) > 1000:
            return base64.b64decode(content.strip())

    if isinstance(content, list):
        for part in content:
            if isinstance(part, dict) and part.get("type") == "image":
                b64 = part.get("image", {}).get("data", "")
                if b64:
                    return base64.b64decode(b64)

    raise ValueError("Image response did not include b64_json or embedded base64 image content.")


def request_image(prompt: str, timeout: int) -> bytes:
    payload = {
        "model": MODEL,
        "prompt": prompt,
        "n": 1,
        "size": "1024x1024",
        "response_format": "b64_json",
    }
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {API_KEY}",
    }

    resp = requests.post(
        API_URL,
        json=payload,
        headers=headers,
        proxies={"http": None, "https": None},
        verify=False,
        timeout=timeout,
    )
    if resp.status_code != 200:
        raise RuntimeError(f"HTTP {resp.status_code}: {resp.text[:800]}")
    return extract_image_bytes(resp.json())


def fit_square(img: Image.Image, size: int = 1024) -> Image.Image:
    img = img.convert("RGBA")
    w, h = img.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    img = img.crop((left, top, left + side, top + side))
    if img.size != (size, size):
        img = img.resize((size, size), Image.Resampling.LANCZOS)
    return img


def soften_tile_edges(img: Image.Image, blend: int = 96) -> Image.Image:
    """Small edge blend for AI textures that are close to tileable but not mathematically seamless."""
    img = img.convert("RGBA")
    w, h = img.size
    x_shift = ImageChops.offset(img, w // 2, 0)
    y_shift = ImageChops.offset(img, 0, h // 2)
    mask_x = Image.new("L", (w, h), 0)
    mask_y = Image.new("L", (w, h), 0)
    mx = mask_x.load()
    my = mask_y.load()
    for x in range(w):
        d = min(x, w - 1 - x)
        v = int(max(0, 1 - d / max(1, blend)) * 80)
        for y in range(h):
            mx[x, y] = v
    for y in range(h):
        d = min(y, h - 1 - y)
        v = int(max(0, 1 - d / max(1, blend)) * 80)
        for x in range(w):
            my[x, y] = v
    img = Image.composite(x_shift, img, mask_x.filter(ImageFilter.GaussianBlur(12)))
    img = Image.composite(y_shift, img, mask_y.filter(ImageFilter.GaussianBlur(12)))
    return img


def derive_luma_mask(img: Image.Image, key: str) -> Image.Image:
    gray = ImageOps.grayscale(img)
    if key == "dark_fissure_mask_source":
        gray = ImageOps.invert(gray)
        gray = ImageOps.autocontrast(gray, cutoff=2)
        gray = ImageEnhance.Contrast(gray).enhance(1.8)
        return gray.filter(ImageFilter.GaussianBlur(0.35))
    if key == "edge_front_mask_source":
        gray = ImageOps.autocontrast(gray, cutoff=1)
        gray = ImageEnhance.Contrast(gray).enhance(1.35)
        return gray.filter(ImageFilter.GaussianBlur(1.2))
    gray = ImageOps.autocontrast(gray, cutoff=1)
    gray = ImageEnhance.Contrast(gray).enhance(1.5)
    return gray.filter(ImageFilter.GaussianBlur(0.6))


def process_image(spec: PromptSpec, raw_path: Path) -> list[Path]:
    img = fit_square(Image.open(raw_path))
    outputs: list[Path] = []

    texture = soften_tile_edges(img) if spec.tileable else img
    texture_path = PROCESSED / spec.out
    texture.save(texture_path)
    outputs.append(texture_path)

    if spec.derive_mask:
        mask = derive_luma_mask(texture, spec.key)
        mask_path = PROCESSED / spec.out.replace(".png", "_mask.png")
        mask.save(mask_path)
        outputs.append(mask_path)

    return outputs


def write_prompt_manifest(specs: list[PromptSpec], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    rows = [
        {
            "key": spec.key,
            "out": spec.out,
            "role": spec.role,
            "tileable": spec.tileable,
            "derive_mask": spec.derive_mask,
            "prompt": spec.prompt,
        }
        for spec in specs
    ]
    path.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate Grey Tide image2 texture sources via the project-local image API.")
    parser.add_argument("--only", action="append", help="Prompt key to generate. Can be repeated.")
    parser.add_argument("--dry-run", action="store_true", help="Print prompt specs and write the prompt manifest only.")
    parser.add_argument("--skip-existing", action="store_true", help="Do not regenerate raw images that already exist.")
    parser.add_argument("--timeout", type=int, default=300)
    parser.add_argument("--prompt-manifest", type=Path, default=OUT / "grey_tide_image2_prompts.json")
    args = parser.parse_args()

    specs = selected_prompts(set(args.only) if args.only else None)
    write_prompt_manifest(specs, args.prompt_manifest)

    print(f"Prompt manifest: {args.prompt_manifest}")
    for spec in specs:
        print(f"\n[{spec.key}] {spec.role}")
        print(spec.prompt)
        print(f"raw: {RAW / spec.out}")
        print(f"processed: {PROCESSED / spec.out}")

    if args.dry_run:
        return 0

    RAW.mkdir(parents=True, exist_ok=True)
    PROCESSED.mkdir(parents=True, exist_ok=True)

    for spec in specs:
        raw_path = RAW / spec.out
        if raw_path.exists() and args.skip_existing:
            print(f"\n[{spec.key}] using existing raw image: {raw_path}")
        else:
            print(f"\n[{spec.key}] requesting image from {API_URL}")
            img_bytes = request_image(spec.prompt, args.timeout)
            raw_path.write_bytes(img_bytes)
            print(f"saved raw: {raw_path}")

        for out_path in process_image(spec, raw_path):
            print(f"saved processed: {out_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
