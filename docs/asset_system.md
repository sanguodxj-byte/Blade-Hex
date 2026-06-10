# Asset system

This document defines how Blade & Hex should reference, resolve, and override art
and audio assets as the project grows and mod support expands.

## Goals

- Core data stores stable string IDs, not Godot render or audio resources.
- View and Audio layers resolve IDs into `Texture2D`, `SpriteFrames`,
  `Shader`, `PackedScene`, `Animation`, `Material`, `AudioStream`, or scenes.
- Built-in assets and mod assets follow the same catalog shape.
- Missing assets have explicit fallback behavior and useful diagnostics.
- Existing direct `GD.Load` paths can be migrated incrementally.

## Terms

- **Asset ID**: Stable string used by game data, for example
  `head_human_man_0`, `combat_turn_start`, or `chain_mail`.
- **Asset kind**: Broad resource category, for example `icon`, `portrait`,
  `unit_sprite`, `character_part`, `equipment_texture`, `sfx`, `bgm`, `vfx`,
  `campaign_illustration`, `projectile_texture`, `map_texture`, or
  `sprite_frames`, `shader`, `packed_scene`, `animation`, or `material`.
- **Asset catalog**: Runtime registry of asset IDs, kinds, paths, source, and
  fallback IDs.
- **Asset resolver**: Small type-specific loader that asks the catalog for
  paths and returns Godot resources.
- **Mod source**: Asset catalog entries and files under `user://mods/...`.

## Layer rules

- `BladeHexCore` may hold asset IDs only.
- `BladeHexCore` must not reference `Texture2D`, `SpriteFrames`, `Material`,
  `Shader`, `Animation`, `AudioStream`, `PackedScene`, `Node`, or other View/Audio
  resources.
- `BladeHexFrontend` may resolve asset IDs into Godot resources.
- Path compatibility, naming aliases, fallback paths, and mod precedence belong
  in resolvers, not in render nodes.

## Built-in layout

Current built-in generated assets live under `res://assets/`. Some
source-controlled UI textures live under `res://src/assets/ui/` and are also
catalog-addressable by stable ID.

Preferred future layout:

```text
assets/
  catalog/
    built_in_assets.json
  character_parts/
    head/
    hair/
    backup/
  armor/
  helmets/
  weapons/
  audio/
    sfx/
    bgm/
```

Legacy paths remain supported by type-specific resolvers until the assets are
renamed or cataloged.

## Mod layout

Preferred mod layout:

```text
user://mods/<mod_id>/
  manifest.json
  assets.json
  assets/
    character_parts/
    audio/
    icons/
```

`manifest.json` currently supports:

```json
{
  "enabled": true,
  "load_order": 100
}
```

Mods load in ascending `load_order`, then by `<mod_id>` for ties. Later catalog
entries override earlier entries with the same `(kind, id)`. A missing manifest
is treated as enabled with `load_order = 0`.

`assets.json` should contain entries shaped like:

```json
{
  "assets": [
    {
      "id": "head_human_man_0",
      "kind": "character_part",
      "path": "assets/character_parts/head_human_man_0.png",
      "fallback_id": "head_human_male_1",
      "tags": ["human", "male", "head"]
    }
  ]
}
```

Mod entries override built-in entries with the same `(kind, id)` unless a future
trust policy disables that mod.

## Resolver precedence

Resolvers should use this order:

1. Explicit mod catalog entry.
2. Explicit built-in catalog entry.
3. Type-specific compatibility paths.
4. Type-specific fallback ID or fallback path.
5. Null result plus one diagnostic log per missing ID.

Resolvers should cache loaded resources and should not call `GD.Load` on paths
that `ResourceLoader.Exists` reports as missing unless the resolver explicitly
supports external file loading.

## Catalog loading

`AssetCatalog.Initialize()` is lazy. Resolvers call into the catalog and trigger
initialization on first use.

The catalog currently loads:

1. `res://assets/catalog/built_in_assets.json`
2. every `user://mods/<mod_id>/assets.json`

Built-in entries load first. Mod entries load after built-ins and override the
same `(kind, id)` key. Relative built-in paths resolve under `res://assets`.
Relative mod paths resolve under that mod root, for example
`user://mods/example_mod/assets/weapon.png`.

The repository keeps the root `assets/` directory ignored because it contains
large generated binaries. Commit code, docs, and small catalog files only when
they are intentionally part of source control. Large art/audio payloads should
remain distributed separately or through a release/mod package.

Generate the built-in catalog locally with:

```powershell
python tools/scripts/generate_asset_catalog.py --assets-root assets --output assets/catalog/built_in_assets.json
```

`assets/catalog/built_in_assets.json` is inside the ignored `assets/` tree. It
is a local/distribution artifact, not a source file, unless the ignore policy is
changed deliberately.

The generator scans both the ignored `assets/` tree and selected
source-controlled project asset roots, currently `src/assets/ui`,
`src/assets/tiles`, `src/assets/props`, and selected `src/assets/sprites`
subdirectories. If the same `(kind, id)` appears in both places, the
source-controlled project asset is emitted first and wins.

## Implemented slices

### Character Part Textures

- `CharacterPartTextureResolver` owns path aliases for:
  - root legacy names: `{part}_{race}_{gender}_{index}.png`
  - current subdirectories: `head/`, `hair/`
  - gender aliases: `male/female` and `man/woman`
  - one-based and zero-based indices
  - `backup/` fallback for old `body/head` assets
- `AvatarRenderer` renders and composites only; it delegates texture resolution.

### Audio Streams

- `AudioAssetResolver` owns audio loading for:
  - catalog IDs of kind `sfx`, `bgm`, and `ambient`
  - direct `res://` and `uid://` paths
  - external `.ogg` and `.mp3` files
- `AudioAssetResolver.LoadAny()` checks direct paths before catalog IDs and
  avoids producing one missing warning per audio kind.
- `AudioManager` keeps its existing SFX/BGM registration tables, but audio
  stream loading now checks the resolver before falling back to legacy logic.
- `AudioManager` and `MainMenu` no longer load audio streams directly; they
  route path, ID, and thunder SFX lookups through `AudioAssetResolver`.

### Equipment And General Textures

- `TextureAssetResolver` owns texture loading for:
  - catalog entries such as `equipment_texture`
  - `ResourceRegistry` compatibility IDs
  - direct `res://`, `uid://`, `user://`, and absolute file paths
  - generated equipment directories such as `armor`,
    `helmets`, `weapons`, and `shields`
- It also exposes typed entry points for `icon`, `portrait`, `unit_sprite`,
  `campaign_illustration`, `poi_illustration`, `origin_illustration`,
  `ui_texture`, and `fog_illustration`.
- `CharacterPresenter` uses `TextureAssetResolver.LoadEquipmentTexture()` for
  equipped single-image layers.
- `CharacterPresenter` also resolves final portrait and battle-sprite fallback
  IDs through `LoadPortrait()` and `LoadUnitSprite()`.
- Creature sprites under `assets/legendary_sprites` are cataloged as
  `unit_sprite`. `CreatureTextureConfig` now resolves creature template IDs
  through `TextureAssetResolver.LoadUnitSprite()` before using the legacy path.
- `CharacterRenderNode` resolves the unit base pedestal as an icon ID, with the
  legacy path as fallback.
- `SkeletonPreview` resolves the same unit base pedestal through the icon
  resolver, so editor previews use the same override path as runtime rendering.
- `AnimEditorPreview` uses the same resolver path for its editor-only base
  pedestal, keeping animation preview assets aligned with runtime assets.
- `CombatTextureLoader` uses the resolver for unit battle sprites, equipment
  preloading, scene sprite IDs, and direct texture path loading.
- Combat projectile textures use the `projectile_texture` kind through
  `TextureAssetResolver.LoadProjectileTexture()`. `ProjectileView` and
  `CombatTextureLoader` now resolve projectile IDs first and use legacy paths
  only as fallback.
- Map textures, terrain tiles, battle props, overworld props, and grass patches
  can be cataloged as `map_texture`. `MapAshController`,
  `BattlePropRegistry`, `OverworldPropRegistry`, `RoadRenderer`, and
  `RiverRenderer` now resolve through `TextureAssetResolver.LoadMapTexture()`.
  `OverworldDecalRenderer2D` uses the same resolver for terrain decal textures.
  `HexOverworldRenderer2D` resolves parchment and grey tide shader textures
  through the map texture resolver.
- `CombatMaterialManager` resolves battle-ground top and cliff textures through
  the map texture resolver before falling back to procedural materials.
- `ResourceRegistry` remains a legacy compatibility registry for old icon,
  sprite frame, and material IDs. New texture callers should use
  `TextureAssetResolver`; the registry is kept as a fallback behind that
  resolver. Its internal direct loading paths are compatibility exceptions,
  not a public loading entry point for new view code.
- `SpriteFramesAssetResolver` owns `SpriteFrames` loading for catalog entries,
  direct paths, and legacy `ResourceRegistry` IDs. `CombatTextureLoader` uses it
  for weapon animation frames, and `CharacterPresenter` uses it for unit and
  equipment sprite-frame IDs.
- `SpriteFramesFileLoader` owns the low-level imported `SpriteFrames` resource
  load used by both `SpriteFramesAssetResolver` and the legacy
  `ResourceRegistry`.
- `ShaderAssetResolver` owns imported `Shader` loading for catalog entries and
  direct fallback paths. Shader source files remain authored and validated by
  the shader-specific workflow; the resolver only controls how compiled shader
  resources are found.
- `PackedSceneAssetResolver` owns imported scene loading for catalog entries
  and direct fallback paths. Runtime scene checks and HUD scene instantiation
  now use it instead of direct `GD.Load<PackedScene>()`.
- `AnimationAssetResolver` owns imported `Animation` resource loading for
  catalog entries and direct fallback paths. Skeleton animation overrides use it
  before falling back to generated runtime clips.
- `MaterialAssetResolver` owns imported `Material` resource loading for catalog
  entries and direct fallback paths. `ResourceRegistry` uses it for legacy
  material IDs.
- `ResourceAssetResolver` is the low-level generic loader used by typed
  resolvers and legacy compatibility paths. New feature code should use a typed
  resolver instead of calling it directly.
- Inventory item cards, equipment slots, campaign level illustrations, POI
  panel illustrations, main menu art, origin selection illustrations, and fog
  illustrations now use the texture resolver.
- `FogOverlay3D` resolves its parchment overlay texture as `map_texture`, with
  a procedural texture fallback when no catalog entry or file path is available.
- `AnimEditorTexturePanel` resolves preview thumbnails through the texture
  resolver instead of loading scanned files directly.
- `SkillTreeInfoPanel` resolves its astral panel background through
  `TextureAssetResolver.LoadUiTexture()`.
- Combat UI textures under `src/assets/ui` are cataloged as `ui_texture` and
  still keep direct `res://src/assets/ui/...` fallback paths.
- `CursorManager` resolves custom cursor textures as `ui_texture` IDs, with
  the legacy `res://src/assets/ui/cursors/...` paths as fallback.
- `OverworldBottomBar` resolves its quick-action button icons as `ui_texture`
  IDs, with the legacy `res://src/assets/ui/...` paths as fallback.
- `OverworldDayNightClock` resolves the day/night wheel and frame as
  `ui_texture` IDs, with the legacy `res://src/assets/ui/...` paths as
  fallback.
- `OverworldUI` resolves the center skill-tree button icon as a `ui_texture`
  ID, with the legacy `res://src/assets/ui/...` path as fallback.
- `SkillTreeUI` resolves astral chart textures as `ui_texture` IDs, with
  legacy `res://src/assets/ui/...` fallback paths.
- `OverworldPropRenderer2D` resolves overworld scatter sprites as
  `map_texture` IDs, with legacy `res://src/assets/tiles/overworld/...`
  fallback paths.
- `AnimEditorScene` resolves editor preview equipment textures through the
  `equipment_texture` resolver, using the selected file path as fallback.
- `PartyPanel` resolves class-title icons as `ui_texture` IDs, with the
  `ClassTitleResolver` path as fallback.
- `GrassOverlayBatcher` resolves combat ground overlay sprite sets as
  `map_texture` IDs, with legacy `res://assets/sprites/...` paths as fallback.
- `TerrainAtlas` resolves baked and per-terrain atlas source textures through
  the map texture resolver while keeping its runtime `user://` baked-atlas
  cache.
- `TextureFileLoader` owns the low-level direct image fallback implementation,
  including imported Godot resources and external PNG/JPEG/WebP files.
  `TextureAssetResolver.LoadPath()` and the legacy `ResourceRegistry` both use
  it instead of duplicating file loading logic.
- `MainMenu` resolves thunder audio through `AudioAssetResolver` instead of
  directly loading audio streams.

### Validation

- `AssetCatalogValidator.ValidateLoadedCatalog()` reports missing paths, missing
  fallback IDs, and fallback cycles for the loaded catalog.
- `AssetCatalogCheck` is a small Godot test scene that runs the validator and
  exits with failure when catalog issues exist.
- Validation is intentionally separate from startup. It can be run in editor
  tools, tests, or debug commands without blocking normal play.

Run the catalog check with:

```powershell
& "D:\123\Godot_v4.6.2-stable_mono_win64.exe" --headless --path "D:\123\Blade&Hex" --scene "res://src/scenes/test/asset_catalog_check.tscn" --quit-after 120
```

## Next slices

- Add remaining map renderer and editor-only direct texture loads when those
  assets need mod overrides.
- Add a stricter mod trust policy if external mods are loaded from untrusted
  sources.
