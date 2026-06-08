using BladeHex.Map;
using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Map;

public partial class OverworldDecalRenderer2D : Node2D
{
    private const float HexSize = 156.0f;
    private const float DecalPixelSize = HexSize;
    private const float LodHideThreshold = 0.4f;
    private const float LodShowThreshold = 0.5f;
    private const int GroundZIndex = 20;
    private const string DecalBaseDir = "res://BladeHexFrontend/src/assets/tiles/decals";

    private sealed class TerrainDecalConfig
    {
        public string[] Variants = Array.Empty<string>();
        public float DensityFactor;
        public float SizeScale;
        public bool RandomRotation90;
    }

    private sealed class DecalData
    {
        public Vector2I Coord;
        public Vector2 Position;
        public Rect2 SrcRect;
        public Rect2 DstRect;
        public Texture2D? Texture;
        public float Rotation;
    }

    private static readonly Dictionary<HexOverworldTile.TerrainType, TerrainDecalConfig> DecalConfigs = new()
    {
        [HexOverworldTile.TerrainType.Grassland] = new()
        {
            Variants = ["grass_flowers_1", "grass_flowers_2", "grass_patch_1", "grass_patch_2"],
            DensityFactor = 0.6f,
            SizeScale = 1.0f,
            RandomRotation90 = true,
        },
        [HexOverworldTile.TerrainType.Plains] = new()
        {
            Variants = ["grass_patch_1", "grass_patch_2"],
            DensityFactor = 0.3f,
            SizeScale = 0.9f,
            RandomRotation90 = true,
        },
        [HexOverworldTile.TerrainType.Forest] = new()
        {
            Variants = ["leaves_1", "mushroom_1", "fern_1"],
            DensityFactor = 0.4f,
            SizeScale = 0.8f,
            RandomRotation90 = true,
        },
        [HexOverworldTile.TerrainType.DenseForest] = new()
        {
            Variants = ["leaves_1", "leaves_2", "fern_1"],
            DensityFactor = 0.5f,
            SizeScale = 0.7f,
            RandomRotation90 = true,
        },
        [HexOverworldTile.TerrainType.Sand] = new()
        {
            Variants = ["dune_ripple_1", "cactus_shadow_1"],
            DensityFactor = 0.3f,
            SizeScale = 1.2f,
            RandomRotation90 = false,
        },
        [HexOverworldTile.TerrainType.Snow] = new()
        {
            Variants = ["snow_drift_1", "ice_crack_1"],
            DensityFactor = 0.5f,
            SizeScale = 1.1f,
            RandomRotation90 = true,
        },
        [HexOverworldTile.TerrainType.Ice] = new()
        {
            Variants = ["ice_crack_1", "ice_crack_2"],
            DensityFactor = 0.4f,
            SizeScale = 1.0f,
            RandomRotation90 = true,
        },
        [HexOverworldTile.TerrainType.Swamp] = new()
        {
            Variants = ["lily_pad_1", "mud_puddle_1"],
            DensityFactor = 0.5f,
            SizeScale = 0.9f,
            RandomRotation90 = true,
        },
        [HexOverworldTile.TerrainType.Hills] = new()
        {
            Variants = ["rock_debris_1", "grass_patch_1"],
            DensityFactor = 0.3f,
            SizeScale = 0.8f,
            RandomRotation90 = true,
        },
        [HexOverworldTile.TerrainType.Mountain] = new()
        {
            Variants = ["rock_debris_1", "rock_debris_2"],
            DensityFactor = 0.3f,
            SizeScale = 0.7f,
            RandomRotation90 = true,
        },
    };

    private readonly Dictionary<string, List<DecalData>> _decalsByTexture = new();
    private readonly Dictionary<string, Texture2D?> _textureCache = new();
    private readonly HashSet<Vector2I> _loadedTiles = new();
    private int _worldSeed;
    private bool _layerVisible = true;
    private bool _dirty = true;

    public int TotalCount
    {
        get
        {
            int count = 0;
            foreach (var kvp in _decalsByTexture)
                count += kvp.Value.Count;

            return count;
        }
    }

    public void Initialize(int worldSeed)
    {
        Name = "OverworldDecalRenderer2D";
        _worldSeed = worldSeed;
        ZIndex = GroundZIndex;

        int totalLoaded = 0;
        foreach (var (terrainType, config) in DecalConfigs)
        {
            foreach (var variant in config.Variants)
            {
                string cacheKey = BuildCacheKey(terrainType, variant);
                string path = BuildPath(terrainType, variant);
                var texture = TextureAssetResolver.LoadMapTexture(cacheKey, path);
                _textureCache[cacheKey] = texture;
                if (texture != null)
                    totalLoaded++;
            }
        }

        GD.Print($"[OverworldDecalRenderer2D] Initialized {DecalConfigs.Count} terrain configs with {totalLoaded} textures.");
    }

    public void LoadDecalsForTiles(IEnumerable<HexOverworldTile> tiles)
    {
        bool anyNew = false;
        int added = 0;

        foreach (var tile in tiles)
        {
            if (!_loadedTiles.Add(tile.Coord))
                continue;

            if (!DecalConfigs.TryGetValue(tile.Terrain, out var config))
                continue;

            uint hash = Hash((uint)((tile.Coord.X * 73856093) ^ (tile.Coord.Y * 19349663) ^ _worldSeed));
            float hashNorm = hash / (float)uint.MaxValue;
            if (hashNorm > config.DensityFactor)
                continue;

            int variantIndex = (int)(hash % (uint)config.Variants.Length);
            string variant = config.Variants[variantIndex];
            string cacheKey = BuildCacheKey(tile.Terrain, variant);

            if (!_textureCache.TryGetValue(cacheKey, out var texture) || texture == null)
                continue;

            AddDecal(tile, config, texture, cacheKey, hash);
            added++;
            anyNew = true;
        }

        if (!anyNew)
            return;

        _dirty = true;
        QueueRedraw();
        GD.Print($"[OverworldDecalRenderer2D] Added {added} decals; total={TotalCount}.");
    }

    public void ClearAll()
    {
        _decalsByTexture.Clear();
        _loadedTiles.Clear();
        _dirty = true;
        QueueRedraw();
    }

    public void UnloadTiles(IEnumerable<Vector2I> coords)
    {
        var coordSet = coords is HashSet<Vector2I> existingSet
            ? existingSet
            : new HashSet<Vector2I>(coords);
        if (coordSet.Count == 0)
            return;

        bool anyRemoved = false;
        foreach (var coord in coordSet)
            anyRemoved |= _loadedTiles.Remove(coord);

        foreach (var decals in _decalsByTexture.Values)
        {
            int before = decals.Count;
            decals.RemoveAll(decal => coordSet.Contains(decal.Coord));
            anyRemoved |= decals.Count != before;
        }

        if (!anyRemoved)
            return;

        _dirty = true;
        QueueRedraw();
    }

    public void UpdateLOD(float zoomLevel)
    {
        if (zoomLevel < LodHideThreshold)
        {
            if (_layerVisible)
            {
                Visible = false;
                _layerVisible = false;
            }
        }
        else if (zoomLevel > LodShowThreshold)
        {
            if (!_layerVisible)
            {
                Visible = true;
                _layerVisible = true;
            }
        }
    }

    public override void _Draw()
    {
        if (!_dirty)
            return;

        foreach (var (cacheKey, decals) in _decalsByTexture)
        {
            if (!_textureCache.TryGetValue(cacheKey, out var texture) || texture == null)
                continue;

            foreach (var decal in decals)
                DrawDecal(texture, decal);
        }

        _dirty = false;
    }

    private void AddDecal(
        HexOverworldTile tile,
        TerrainDecalConfig config,
        Texture2D texture,
        string cacheKey,
        uint hash)
    {
        Vector2 position = tile.PixelPos;
        float rotation = 0.0f;
        if (config.RandomRotation90)
        {
            int rotationStep = (int)(hash >> 8) & 3;
            rotation = rotationStep * Mathf.Pi * 0.5f;
        }

        float texW = texture.GetWidth();
        float texH = texture.GetHeight();
        float drawSize = DecalPixelSize * config.SizeScale;
        float scale = drawSize / texH;
        float drawW = texW * scale;
        float drawH = texH * scale;

        if (!_decalsByTexture.TryGetValue(cacheKey, out var decals))
        {
            decals = new List<DecalData>();
            _decalsByTexture[cacheKey] = decals;
        }

        decals.Add(new DecalData
        {
            Coord = tile.Coord,
            Position = position,
            SrcRect = new Rect2(0, 0, texW, texH),
            DstRect = new Rect2(position.X - drawW * 0.5f, position.Y - drawH * 0.5f, drawW, drawH),
            Texture = texture,
            Rotation = rotation,
        });
    }

    private void DrawDecal(Texture2D texture, DecalData decal)
    {
        if (decal.Rotation != 0.0f)
        {
            var transform = new Transform2D(decal.Rotation, decal.Position);
            DrawSetTransformMatrix(transform);
            DrawTextureRectRegion(
                texture,
                new Rect2(-decal.DstRect.Size * 0.5f, decal.DstRect.Size),
                decal.SrcRect);
            DrawSetTransformMatrix(Transform2D.Identity);
            return;
        }

        DrawTextureRectRegion(texture, decal.DstRect, decal.SrcRect);
    }

    private static string BuildCacheKey(HexOverworldTile.TerrainType terrainType, string variant)
    {
        return $"{terrainType.ToString().ToLowerInvariant()}_{variant}";
    }

    private static string BuildPath(HexOverworldTile.TerrainType terrainType, string variant)
    {
        return $"{DecalBaseDir}/{terrainType.ToString().ToLowerInvariant()}/{variant}.png";
    }

    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }
}
