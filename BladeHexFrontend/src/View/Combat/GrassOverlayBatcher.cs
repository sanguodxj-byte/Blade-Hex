using BladeHex.Data;
using BladeHex.Map;
using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Combat;

[GlobalClass]
public partial class GrassOverlayBatcher : Node3D
{
    private const float OverflowScale = 1.2f;
    private const float CliffOverhang = 3.0f;
    private const float PlacementChance = 1.0f;
    private const string OverlayShaderPath = "res://BladeHexFrontend/src/assets/shaders/battle_ground_overlay.gdshader";
    private const string OverlayMacroDir = "res://BladeHexFrontend/src/assets/tiles/battle_ground/overlays";
    private const string OverlayDetailDir = "res://BladeHexFrontend/src/assets/tiles/battle_ground/detail";

    private static readonly float YOffset = CombatLayerHeight.TextureLayer;
    private static Shader? _overlayShader;

    private static readonly HashSet<BattleCellData.TerrainType> GrasslandTerrains = new()
    {
        BattleCellData.TerrainType.Grassland,
        BattleCellData.TerrainType.Savanna,
        BattleCellData.TerrainType.Jungle,
    };

    private static readonly HashSet<BattleCellData.TerrainType> PlainsTerrains = new()
    {
        BattleCellData.TerrainType.Plains,
        BattleCellData.TerrainType.Forest,
        BattleCellData.TerrainType.DenseForest,
        BattleCellData.TerrainType.Taiga,
    };

    private static readonly HashSet<BattleCellData.TerrainType> WastelandTerrains = new()
    {
        BattleCellData.TerrainType.Wasteland,
    };

    private static readonly HashSet<BattleCellData.TerrainType> DirtTerrains = new()
    {
        BattleCellData.TerrainType.Hills,
        BattleCellData.TerrainType.Swamp,
        BattleCellData.TerrainType.Bog,
    };

    private static readonly HashSet<BattleCellData.TerrainType> SandTerrains = new()
    {
        BattleCellData.TerrainType.Sand,
    };

    private static readonly HashSet<BattleCellData.TerrainType> RoadTerrains = new()
    {
        BattleCellData.TerrainType.Road,
        BattleCellData.TerrainType.Bridge,
    };

    private static readonly HashSet<BattleCellData.TerrainType> SnowTerrains = new()
    {
        BattleCellData.TerrainType.Snow,
        BattleCellData.TerrainType.Ice,
        BattleCellData.TerrainType.MountainSnow,
    };

    private static readonly HashSet<BattleCellData.TerrainType> RockTerrains = new()
    {
        BattleCellData.TerrainType.Mountain,
        BattleCellData.TerrainType.Rocky,
    };

    private static readonly HashSet<BattleCellData.TerrainType> PoisonMushroomTerrains = new()
    {
        BattleCellData.TerrainType.PoisonMushroom,
    };

    private readonly List<Sprite3D> _sprites = new();

    public int OverlayCount => _sprites.Count;

    private Texture2D? _plainsTexture;
    private Texture2D? _grasslandTexture;
    private Texture2D? _wastelandTexture;
    private Texture2D? _dirtTexture;
    private Texture2D? _sandTexture;

    private Texture2D? _plainsDetailTexture;
    private Texture2D? _grasslandDetailTexture;
    private Texture2D? _wastelandDetailTexture;
    private Texture2D? _dirtDetailTexture;
    private Texture2D? _sandDetailTexture;

    private Texture2D[]? _roadTextures;
    private Texture2D[]? _snowTextures;
    private Texture2D[]? _rockTextures;
    private Texture2D[]? _poisonMushroomTextures;

    private Vector2 _uvWorldOrigin = Vector2.Zero;
    private Vector2 _uvWorldSize = Vector2.One;

    public void PlaceGrassOverlays(HexGrid hexGrid, BattleMapGenerator.BattleMapData? mapData)
    {
        if (hexGrid == null)
            return;

        LoadBattlefieldTextures();
        LoadRoadTextures();
        LoadSnowTextures();
        LoadRockTextures();
        LoadPoisonMushroomTextures();
        ComputeBattlefieldUvBounds(hexGrid);

        int placed = 0;
        foreach (var kvp in hexGrid.Cells)
        {
            var cell = kvp.Value;
            if (cell == null || !GodotObject.IsInstanceValid(cell))
                continue;

            if (PlacementChance < 1.0f && GD.Randf() > PlacementChance)
                continue;

            var terrain = ResolveOverlayTerrain(hexGrid, cell);
            float dappleStrength = GetForestDappleStrength(terrain);

            if (GrasslandTerrains.Contains(terrain) && _grasslandTexture != null)
            {
                PlaceOverlaySprite(hexGrid, cell, _grasslandTexture, _grasslandDetailTexture, 0.055f, 0.20f, dappleStrength);
                placed++;
            }
            else if (PlainsTerrains.Contains(terrain) && _plainsTexture != null)
            {
                PlaceOverlaySprite(hexGrid, cell, _plainsTexture, _plainsDetailTexture, 0.048f, 0.14f, dappleStrength);
                placed++;
            }
            else if (WastelandTerrains.Contains(terrain) && _wastelandTexture != null)
            {
                PlaceOverlaySprite(hexGrid, cell, _wastelandTexture, _wastelandDetailTexture, 0.042f, 0.18f, dappleStrength);
                placed++;
            }
            else if (DirtTerrains.Contains(terrain) && _dirtTexture != null)
            {
                PlaceOverlaySprite(hexGrid, cell, _dirtTexture, _dirtDetailTexture, 0.040f, 0.16f, dappleStrength);
                placed++;
            }
            else if (SandTerrains.Contains(terrain) && _sandTexture != null)
            {
                PlaceOverlaySprite(hexGrid, cell, _sandTexture, _sandDetailTexture, 0.040f, 0.14f, dappleStrength);
                placed++;
            }
            else if (RoadTerrains.Contains(terrain) && _roadTextures != null && _roadTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, PickTextureVariant(_roadTextures, cell.GridPos, 11), null, 0.0f, 0.0f, dappleStrength);
                placed++;
            }
            else if (SnowTerrains.Contains(terrain) && _snowTextures != null && _snowTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, PickTextureVariant(_snowTextures, cell.GridPos, 23), null, 0.0f, 0.0f, dappleStrength);
                placed++;
            }
            else if (RockTerrains.Contains(terrain) && _rockTextures != null && _rockTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, PickTextureVariant(_rockTextures, cell.GridPos, 37), null, 0.0f, 0.0f, dappleStrength);
                placed++;
            }
            else if (PoisonMushroomTerrains.Contains(terrain) && _poisonMushroomTextures != null && _poisonMushroomTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, PickTextureVariant(_poisonMushroomTextures, cell.GridPos, 51), null, 0.0f, 0.0f, dappleStrength);
                placed++;
            }
        }

        GD.Print($"[GrassOverlayBatcher] 放置了 {placed} 个地形覆盖精灵");
    }

    public void ClearOverlays()
    {
        foreach (var sprite in _sprites)
        {
            if (GodotObject.IsInstanceValid(sprite))
                sprite.QueueFree();
        }
        _sprites.Clear();
    }

    private void LoadBattlefieldTextures()
    {
        if (_plainsTexture != null && _plainsDetailTexture != null)
            return;

        _plainsTexture = TextureAssetResolver.LoadMapTexture("battle_overlay_moss_grass_plains", $"{OverlayMacroDir}/moss_grass_1254.png");
        _grasslandTexture = TextureAssetResolver.LoadMapTexture("battle_overlay_moss_grass_grassland", $"{OverlayMacroDir}/moss_grass_1254.png");
        _wastelandTexture = TextureAssetResolver.LoadMapTexture("battle_overlay_stony_mud_wasteland", $"{OverlayMacroDir}/stony_mud_1254.png");
        _dirtTexture = TextureAssetResolver.LoadMapTexture("battle_overlay_stony_mud_dirt", $"{OverlayMacroDir}/stony_mud_1254.png");
        _sandTexture = TextureAssetResolver.LoadMapTexture("battle_overlay_stony_mud_sand", $"{OverlayMacroDir}/stony_mud_1254.png");

        _plainsDetailTexture = TextureAssetResolver.LoadMapTexture("battle_detail_moss_grass_plains", $"{OverlayDetailDir}/moss_grass_detail.png");
        _grasslandDetailTexture = TextureAssetResolver.LoadMapTexture("battle_detail_moss_grass_grassland", $"{OverlayDetailDir}/moss_grass_detail.png");
        _wastelandDetailTexture = TextureAssetResolver.LoadMapTexture("battle_detail_stony_mud_wasteland", $"{OverlayDetailDir}/stony_mud_detail.png");
        _dirtDetailTexture = TextureAssetResolver.LoadMapTexture("battle_detail_stony_mud_dirt", $"{OverlayDetailDir}/stony_mud_detail.png");
        _sandDetailTexture = TextureAssetResolver.LoadMapTexture("battle_detail_stony_mud_sand", $"{OverlayDetailDir}/stony_mud_detail.png");

        GD.Print("[GrassOverlayBatcher] Loaded battlefield macro/detail textures.");
    }

    private void LoadRoadTextures()
    {
        if (_roadTextures != null)
            return;

        _roadTextures = LoadTextureSet("res://assets/sprites/road_patches/", "road", 16);
        GD.Print($"[GrassOverlayBatcher] Loaded {_roadTextures.Length} road textures");
    }

    private void LoadSnowTextures()
    {
        if (_snowTextures != null)
            return;

        _snowTextures = LoadTextureSet("res://assets/sprites/snow_patches/", "snow", 16);
        GD.Print($"[GrassOverlayBatcher] Loaded {_snowTextures.Length} snow textures");
    }

    private void LoadRockTextures()
    {
        if (_rockTextures != null)
            return;

        _rockTextures = LoadTextureSet("res://assets/sprites/rock_patches/", "rock", 16);
        GD.Print($"[GrassOverlayBatcher] Loaded {_rockTextures.Length} rock textures");
    }

    private void LoadPoisonMushroomTextures()
    {
        if (_poisonMushroomTextures != null)
            return;

        _poisonMushroomTextures = LoadTextureSet("res://assets/sprites/poison_mushroom_patches/", "poison_mushroom", 16);
        GD.Print($"[GrassOverlayBatcher] Loaded {_poisonMushroomTextures.Length} poison mushroom textures");
    }

    private static Texture2D[] LoadTextureSet(string basePath, string prefix, int count)
    {
        var textures = new List<Texture2D>();
        for (int i = 0; i < count; i++)
        {
            string path = $"{basePath}{prefix}_{i:D2}.png";
            var texture = TextureAssetResolver.LoadPath(path);
            if (texture != null)
                textures.Add(texture);
        }

        return textures.ToArray();
    }

    private static Texture2D PickTextureVariant(Texture2D[] textures, Vector2I gridPos, int salt)
    {
        int hash = (gridPos.X * 73856093) ^ (gridPos.Y * 19349663) ^ (salt * 83492791);
        int index = (hash & int.MaxValue) % textures.Length;
        return textures[index];
    }

    private static BattleCellData.TerrainType ResolveOverlayTerrain(HexGrid hexGrid, HexCell cell)
    {
        var terrain = cell.Data?.terrainType ?? BattleCellData.TerrainType.Plains;
        if (terrain != BattleCellData.TerrainType.Ruins)
            return terrain;

        var neighbors = HexUtils.GetNeighbors(cell.GridPos.X, cell.GridPos.Y);
        foreach (var nbPos in neighbors)
        {
            if (!hexGrid.Cells.TryGetValue(nbPos, out var nbCell) || nbCell?.Data == null)
                continue;

            var nbTerrain = nbCell.Data.terrainType;
            if (nbTerrain != BattleCellData.TerrainType.Ruins &&
                nbTerrain != BattleCellData.TerrainType.Wall &&
                nbTerrain != BattleCellData.TerrainType.Rampart &&
                nbTerrain != BattleCellData.TerrainType.Tower &&
                nbTerrain != BattleCellData.TerrainType.Gate &&
                nbTerrain != BattleCellData.TerrainType.Staircase &&
                nbTerrain != BattleCellData.TerrainType.DeepWater &&
                nbTerrain != BattleCellData.TerrainType.ShallowWater &&
                nbTerrain != BattleCellData.TerrainType.River)
            {
                return nbTerrain;
            }
        }

        return BattleCellData.TerrainType.Plains;
    }

    private void ComputeBattlefieldUvBounds(HexGrid hexGrid)
    {
        if (hexGrid.Cells.Count == 0)
        {
            _uvWorldOrigin = Vector2.Zero;
            _uvWorldSize = Vector2.One;
            return;
        }

        float pad = HexUtils.Size * OverflowScale;
        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxZ = float.MinValue;

        foreach (var cell in hexGrid.Cells.Values)
        {
            if (cell == null || !GodotObject.IsInstanceValid(cell))
                continue;

            minX = Mathf.Min(minX, cell.Position.X - pad);
            minZ = Mathf.Min(minZ, cell.Position.Z - pad);
            maxX = Mathf.Max(maxX, cell.Position.X + pad);
            maxZ = Mathf.Max(maxZ, cell.Position.Z + pad);
        }

        if (minX == float.MaxValue)
        {
            _uvWorldOrigin = Vector2.Zero;
            _uvWorldSize = Vector2.One;
            return;
        }

        _uvWorldOrigin = new Vector2(minX, minZ);
        _uvWorldSize = new Vector2(
            Mathf.Max(1.0f, maxX - minX),
            Mathf.Max(1.0f, maxZ - minZ));
    }

    private void PlaceOverlaySprite(
        HexGrid hexGrid,
        HexCell cell,
        Texture2D texture,
        Texture2D? detailTexture,
        float detailWorldScale,
        float detailStrength,
        float dappleStrength)
    {
        if (texture == null)
            return;

        float targetWorldSize = HexUtils.Size * 2.0f * OverflowScale;
        float textureWidth = Mathf.Max(1.0f, texture.GetWidth());
        float pixelSize = targetWorldSize / textureWidth;

        var sprite = new Sprite3D
        {
            Texture = texture,
            PixelSize = pixelSize,
            RotationDegrees = new Vector3(-90, 0, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            Shaded = true,
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
            AlphaScissorThreshold = 0.5f,
            Transparent = true,
            NoDepthTest = false,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            RenderPriority = -1,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        Color tint = ElevationTint(cell.Elevation);
        int cliffMask = ComputeCliffMask(hexGrid, cell);

        _overlayShader ??= ShaderAssetResolver.Load("battle_ground_overlay", OverlayShaderPath);
        if (_overlayShader != null)
        {
            float apothem = HexUtils.Size * Mathf.Sqrt(3.0f) / 2.0f + CliffOverhang;
            float halfExtent = targetWorldSize * 0.5f;
            var mat = new ShaderMaterial { Shader = _overlayShader };

            bool useDetail = detailTexture != null && detailStrength > 0.001f;
            mat.SetShaderParameter("tex_albedo", texture);
            mat.SetShaderParameter("detail_tex_albedo", detailTexture ?? texture);
            mat.SetShaderParameter("use_detail_texture", useDetail);
            mat.SetShaderParameter("detail_world_scale", detailWorldScale);
            mat.SetShaderParameter("detail_strength", detailStrength);
            mat.SetShaderParameter("detail_contrast", 1.0f);
            mat.SetShaderParameter("cliff_mask", cliffMask);
            mat.SetShaderParameter("apothem", apothem);
            mat.SetShaderParameter("half_extent", halfExtent);
            mat.SetShaderParameter("modulate", tint);
            mat.SetShaderParameter("fake_light_strength", 0.16f);
            mat.SetShaderParameter("fake_light_world_scale", 0.0045f);
            mat.SetShaderParameter("forest_dapple_strength", dappleStrength);
            mat.SetShaderParameter("forest_dapple_scale", 0.014f);
            mat.SetShaderParameter("use_battlefield_uv", true);
            mat.SetShaderParameter("uv_world_origin", _uvWorldOrigin);
            mat.SetShaderParameter("uv_world_size", _uvWorldSize);
            mat.SetShaderParameter("uv_min", new Vector2(0.075f, 0.075f));
            mat.SetShaderParameter("uv_max", new Vector2(0.925f, 0.925f));
            mat.SetShaderParameter("hex_edge_feather", 14.0f);
            sprite.MaterialOverride = mat;
        }
        else
        {
            sprite.Modulate = tint;
        }

        int gridHash = (cell.GridPos.X * 73856093) ^ (cell.GridPos.Y * 19349663);
        float yJitter = (gridHash & 0xFF) / 255.0f * 2.0f;
        float hexHeight = HexUtils.Size * 0.5f;
        sprite.Position = cell.Position + new Vector3(0, hexHeight / 2.0f + YOffset + yJitter, 0);
        sprite.SortingOffset = cell.Position.Z * 0.001f;

        AddChild(sprite);
        _sprites.Add(sprite);
    }

    private static int ComputeCliffMask(HexGrid hexGrid, HexCell cell)
    {
        int mask = 0;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexUtils.GetNeighbor(cell.GridPos.X, cell.GridPos.Y, d);
            if (hexGrid.Cells.TryGetValue(nb, out var nbCell) &&
                nbCell != null &&
                Mathf.Abs(cell.Elevation - nbCell.Elevation) >= 1)
            {
                mask |= (1 << d);
            }
        }

        return mask;
    }

    private const int BaselineElevation = 2;

    private static Color ElevationTint(int elevation)
    {
        int delta = Mathf.Clamp(elevation - BaselineElevation, -2, 3);

        float brightness = 1.0f + delta * 0.10f;
        float warmCool = delta * 0.015f;
        float r = Mathf.Clamp(brightness - warmCool, 0f, 2f);
        float g = Mathf.Clamp(brightness, 0f, 2f);
        float b = Mathf.Clamp(brightness + warmCool, 0f, 2f);

        return new Color(r, g, b, 1.0f);
    }

    private static float GetForestDappleStrength(BattleCellData.TerrainType terrain)
    {
        return terrain switch
        {
            BattleCellData.TerrainType.DenseForest => 0.42f,
            BattleCellData.TerrainType.Jungle => 0.36f,
            BattleCellData.TerrainType.Forest => 0.30f,
            BattleCellData.TerrainType.Taiga => 0.24f,
            _ => 0.0f,
        };
    }
}
