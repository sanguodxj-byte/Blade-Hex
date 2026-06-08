import argparse
import json
from pathlib import Path


ASSETS_ROOT_MAPPINGS = [
    ("generated_character_parts/head", "character_part"),
    ("generated_character_parts/hair", "character_part"),
    ("generated_character_parts/backup", "character_part"),
    ("generated_armor", "equipment_texture"),
    ("generated_helmets", "equipment_texture"),
    ("generated_weapons", "equipment_texture"),
    ("generated_weapons_backup", "equipment_texture"),
    ("generated_shields", "equipment_texture"),
    ("generated_staves", "equipment_texture"),
    ("generated_spellbooks", "equipment_texture"),
    ("generated_class_icons", "icon"),
    ("generated_consumables", "icon"),
    ("generated_skill_icons", "icon"),
    ("generated_ui_icons", "icon"),
    ("generated_ui", "ui_texture"),
    ("generated_ui_main", "ui_texture"),
    ("generated_legendary_sprites", "unit_sprite"),
    ("generated_campaign_illust", "campaign_illustration"),
    ("generated_origin_illust", "origin_illustration"),
    ("generated_poi_illust", "poi_illustration"),
    ("generated_wound_vfx", "vfx"),
    ("fog_illustrations", "fog_illustration"),
    ("tiles", "map_texture"),
    ("props", "map_texture"),
    ("sprites/projectiles", "projectile_texture"),
    ("sprites/overworld_props", "map_texture"),
    ("sprites/grass_patches", "map_texture"),
    ("sprites/weapons", "sprite_frames"),
    ("audio/sfx", "sfx"),
    ("audio/bgm", "bgm"),
    ("audio/ambient", "ambient"),
]

PROJECT_ROOT_MAPPINGS = [
    ("src/assets/ui", "ui_texture", "res://src/assets/ui"),
    ("src/assets/tiles", "map_texture", "res://src/assets/tiles"),
    ("src/assets/props", "map_texture", "res://src/assets/props"),
    ("src/assets/sprites/overworld_props", "map_texture", "res://src/assets/sprites/overworld_props"),
    ("src/assets/sprites/grass_patches", "map_texture", "res://src/assets/sprites/grass_patches"),
    ("src/assets/shaders", "shader", "res://src/assets/shaders"),
    ("BladeHexFrontend/src/View/UI/Shaders", "shader", "res://BladeHexFrontend/src/View/UI/Shaders"),
    ("src/scenes", "packed_scene", "res://src/scenes"),
    ("BladeHexFrontend/src/View/Unit", "packed_scene", "res://BladeHexFrontend/src/View/Unit"),
    ("animations", "animation", "res://animations"),
]

EXTENSIONS = {
    ".png",
    ".jpg",
    ".jpeg",
    ".webp",
    ".ogg",
    ".mp3",
    ".wav",
    ".tres",
    ".res",
    ".gdshader",
    ".tscn",
}


def iter_root_entries(root: Path, mappings):
    seen = set()
    for relative_dir, kind in mappings:
        directory = root / relative_dir
        if not directory.exists():
            continue

        for path in sorted(directory.rglob("*")):
            if not path.is_file() or path.suffix.lower() not in EXTENSIONS:
                continue

            relative_path = path.relative_to(root).as_posix()
            asset_id = path.stem
            key = (kind, asset_id.lower())
            if key in seen:
                continue
            seen.add(key)

            yield {
                "id": asset_id,
                "kind": kind,
                "path": relative_path,
                "source_id": "built_in",
                "tags": build_tags(relative_path, kind),
            }


def iter_project_entries(project_root: Path, mappings):
    seen = set()
    for relative_dir, kind, catalog_base in mappings:
        directory = project_root / relative_dir
        if not directory.exists():
            continue

        for path in sorted(directory.rglob("*")):
            if not path.is_file() or path.suffix.lower() not in EXTENSIONS:
                continue

            asset_id = path.stem
            key = (kind, asset_id.lower())
            if key in seen:
                continue
            seen.add(key)

            asset_relative_path = path.relative_to(directory).as_posix()
            catalog_path = f"{catalog_base.rstrip('/')}/{asset_relative_path}"
            relative_path = path.relative_to(project_root).as_posix()
            yield {
                "id": asset_id,
                "kind": kind,
                "path": catalog_path,
                "source_id": "built_in",
                "tags": build_tags(relative_path, kind),
            }


def iter_entries(assets_root: Path, project_root: Path):
    seen = set()
    for entry in iter_project_entries(project_root, PROJECT_ROOT_MAPPINGS):
        key = (entry["kind"], entry["id"].lower())
        if key in seen:
            continue
        seen.add(key)
        yield entry

    for entry in iter_root_entries(assets_root, ASSETS_ROOT_MAPPINGS):
        key = (entry["kind"], entry["id"].lower())
        if key in seen:
            continue
        seen.add(key)
        yield entry


def build_tags(relative_path: str, kind: str):
    parts = Path(relative_path).parts
    tags = [kind]
    tags.extend(part for part in parts[:-1] if part not in {"generated_character_parts"})
    return sorted(set(tags))


def main():
    parser = argparse.ArgumentParser(description="Generate Blade & Hex asset catalog JSON.")
    parser.add_argument("--assets-root", default="assets", help="Path to the local assets directory.")
    parser.add_argument("--project-root", default=".", help="Project root used for source-controlled assets.")
    parser.add_argument(
        "--output",
        default="assets/catalog/built_in_assets.json",
        help="Catalog output path. This usually lives under ignored assets/.",
    )
    args = parser.parse_args()

    assets_root = Path(args.assets_root)
    project_root = Path(args.project_root)
    output = Path(args.output)
    entries = list(iter_entries(assets_root, project_root))

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps({"assets": entries}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Wrote {len(entries)} asset entries to {output}")


if __name__ == "__main__":
    main()
