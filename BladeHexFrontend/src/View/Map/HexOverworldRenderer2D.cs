using BladeHex.Map;
using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Map;

public partial class HexOverworldRenderer2D : Node2D
{
    private const string ShaderPath = "res://BladeHexFrontend/src/assets/shaders/overworld_parchment_2d.gdshader";
    private const string ParchmentTexturePath = "res://BladeHexFrontend/src/assets/tiles/tileable/parchment_tile.png";
    private const float HexSize = 156.0f;
    private const int DataTexSize = 768;
    private const int CoordOffset = 0;

    private static readonly Color DefaultTerrainColor = new(0.86f, 0.79f, 0.67f);

    private Sprite2D? _groundSprite;
    private ShaderMaterial? _shaderMaterial;
    private ImageTexture? _terrainDataTexture;
    private Image? _terrainDataImage;
    private readonly HashSet<Vector2I> _loadedTiles = new();
    private bool _initialized;
    private bool _dataDirty;

    private float _mapMinX;
    private float _mapMinY;
    private float _mapMaxX;
    private float _mapMaxY;

    public ShaderMaterial? GroundMaterial => _shaderMaterial;
    public int LoadedTileCount => _loadedTiles.Count;
    public int TerrainDataTextureSize => DataTexSize;
    public long TerrainDataTextureBytes => DataTexSize * DataTexSize * 4L;

    public void Initialize()
    {
        Name = "HexOverworldRenderer2D";

        var shader = ShaderAssetResolver.Load("overworld_parchment_2d", ShaderPath);
        if (shader == null)
        {
            GD.PrintErr($"[HexOverworldRenderer2D] Failed to load shader: {ShaderPath}");
            return;
        }

        var parchmentTexture = TextureAssetResolver.LoadMapTexture("parchment_tile", ParchmentTexturePath);

        _shaderMaterial = new ShaderMaterial
        {
            Shader = shader,
        };
        _shaderMaterial.SetShaderParameter("top_texture", parchmentTexture!);
        _shaderMaterial.SetShaderParameter("texture_scale", 0.0008f);
        _shaderMaterial.SetShaderParameter("detail_scale", 0.005f);
        _shaderMaterial.SetShaderParameter("detail_strength", 0.0f);
        _shaderMaterial.SetShaderParameter("hex_tile_size", 12000.0f);
        _shaderMaterial.SetShaderParameter("blend_sharpness", 8.0f);
        _shaderMaterial.SetShaderParameter("use_rotation", true);
        _shaderMaterial.SetShaderParameter("day_night_tint", Colors.White);
        _shaderMaterial.SetShaderParameter("hex_size_pixels", HexSize);

        _terrainDataImage = Image.CreateEmpty(DataTexSize, DataTexSize, true, Image.Format.Rgba8);
        _terrainDataImage.Fill(DefaultTerrainColor);
        _terrainDataTexture = ImageTexture.CreateFromImage(_terrainDataImage);

        _shaderMaterial.SetShaderParameter("terrain_data", _terrainDataTexture);
        _shaderMaterial.SetShaderParameter("use_terrain_data", true);
        _shaderMaterial.SetShaderParameter("terrain_data_size", new Vector2(DataTexSize, DataTexSize));
        _shaderMaterial.SetShaderParameter("use_ash_data", false);
        BindGreyTideTextures(_shaderMaterial);

        _mapMinX = -3000;
        _mapMinY = -3000;
        _mapMaxX = 3000;
        _mapMaxY = 3000;

        _initialized = true;
        GD.Print("[HexOverworldRenderer2D] Shader terrain renderer initialized.");
    }

    public void LoadTiles(IEnumerable<HexOverworldTile> tiles)
    {
        if (!_initialized)
            Initialize();

        bool anyNew = false;
        foreach (var tile in tiles)
        {
            if (!_loadedTiles.Add(tile.Coord))
                continue;

            anyNew = true;
            ExpandBounds(tile.PixelPos);
            WriteTerrainData(tile);
        }

        if (anyNew)
            RebuildGroundSprite();
    }

    public void LoadFromGrid(HexOverworldGrid grid)
    {
        ClearAll();
        LoadTiles(grid.Tiles.Values);
        GD.Print($"[HexOverworldRenderer2D] Loaded {_loadedTiles.Count} tiles.");
    }

    public void ClearAll()
    {
        _loadedTiles.Clear();
        if (_groundSprite != null)
        {
            RemoveChild(_groundSprite);
            _groundSprite.QueueFree();
            _groundSprite = null;
        }

        _terrainDataImage?.Fill(DefaultTerrainColor);
        _dataDirty = true;
    }

    private void ExpandBounds(Vector2 pixelPos)
    {
        _mapMinX = Mathf.Min(_mapMinX, pixelPos.X - HexSize);
        _mapMinY = Mathf.Min(_mapMinY, pixelPos.Y - HexSize);
        _mapMaxX = Mathf.Max(_mapMaxX, pixelPos.X + HexSize);
        _mapMaxY = Mathf.Max(_mapMaxY, pixelPos.Y + HexSize);
    }

    private void WriteTerrainData(HexOverworldTile tile)
    {
        int tx = tile.Coord.X + CoordOffset;
        int ty = tile.Coord.Y + CoordOffset;
        if (tx < 0 || tx >= DataTexSize || ty < 0 || ty >= DataTexSize || _terrainDataImage == null)
            return;

        var profile = TerrainVisualRegistry.Get(tile.Terrain);
        Color terrainColor = profile.DominantColor;

        if (tile.Terrain == HexOverworldTile.TerrainType.Plains)
            terrainColor = BuildPlainsColor(tx, ty);

        float elevationTweak = tile.Elevation * 0.22f;
        terrainColor = new Color(
            Mathf.Clamp(terrainColor.R + elevationTweak, 0.0f, 1.0f),
            Mathf.Clamp(terrainColor.G + elevationTweak, 0.0f, 1.0f),
            Mathf.Clamp(terrainColor.B + elevationTweak * 0.5f, 0.0f, 1.0f),
            profile.PatternType / 7.0f);

        _terrainDataImage.SetPixel(tx, ty, terrainColor);
        _dataDirty = true;
    }

    private static Color BuildPlainsColor(int tx, int ty)
    {
        float wave = Mathf.Sin(tx * 0.12f) * Mathf.Cos(ty * 0.12f);
        float fine = Mathf.Sin(tx * 0.45f + 1.0f) * Mathf.Cos(ty * 0.45f + 2.0f) * 0.3f;
        float t = Mathf.Clamp((wave + fine + 1.0f) * 0.5f, 0.0f, 1.0f);

        Color greenColor = new(0.32f, 0.52f, 0.22f);
        Color yellowColor = new(0.68f, 0.60f, 0.32f);
        return greenColor.Lerp(yellowColor, t);
    }

    private void RebuildGroundSprite()
    {
        if (!_initialized || _shaderMaterial == null)
            return;

        UploadTerrainDataIfDirty();

        float mapW = _mapMaxX - _mapMinX;
        float mapH = _mapMaxY - _mapMinY;
        _shaderMaterial.SetShaderParameter("map_offset", new Vector2(_mapMinX, _mapMinY));
        _shaderMaterial.SetShaderParameter("terrain_data_size", new Vector2(DataTexSize, DataTexSize));
        _shaderMaterial.SetShaderParameter("hex_tile_size", 6000.0f);

        if (_groundSprite == null)
        {
            var placeholderTexture = new PlaceholderTexture2D
            {
                Size = new Vector2(mapW, mapH),
            };

            _groundSprite = new Sprite2D
            {
                Name = "ParchmentGround",
                Texture = placeholderTexture,
                Material = _shaderMaterial,
                Centered = false,
                Position = new Vector2(_mapMinX, _mapMinY),
                ZIndex = 10,
            };
            AddChild(_groundSprite);
            return;
        }

        if (_groundSprite.Texture is PlaceholderTexture2D texture)
            texture.Size = new Vector2(mapW, mapH);

        _groundSprite.Position = new Vector2(_mapMinX, _mapMinY);
    }

    private void UploadTerrainDataIfDirty()
    {
        if (!_dataDirty || _terrainDataImage == null || _terrainDataTexture == null)
            return;

        var blurred = BlurTerrainData(_terrainDataImage, 2);
        blurred.GenerateMipmaps();
        _terrainDataTexture.Update(blurred);
        _dataDirty = false;
    }

    private static void BindGreyTideTextures(ShaderMaterial material)
    {
        const string basePath = "res://BladeHexFrontend/src/assets/tiles/grey_tide/textures";
        var largeNoise = LoadMapTexture("ash_noise_large", $"{basePath}/ash_noise_large.png");
        var fineNoise = LoadMapTexture("ash_noise_fine", $"{basePath}/ash_noise_fine.png");
        var cracks = LoadMapTexture("char_cracks", $"{basePath}/char_cracks.png");
        var ramp = LoadMapTexture("burn_edge_ramp", $"{basePath}/burn_edge_ramp.png");
        var voidFlow = LoadMapTexture("void_flow_greywhite", $"{basePath}/void_flow_greywhite.png");

        bool ready = largeNoise != null && fineNoise != null && cracks != null && ramp != null;
        material.SetShaderParameter("use_grey_tide_textures", ready);
        if (largeNoise != null) material.SetShaderParameter("ash_noise_large_tex", largeNoise);
        if (fineNoise != null) material.SetShaderParameter("ash_noise_fine_tex", fineNoise);
        if (cracks != null) material.SetShaderParameter("char_cracks_tex", cracks);
        if (ramp != null) material.SetShaderParameter("burn_edge_ramp", ramp);
        if (voidFlow != null) material.SetShaderParameter("void_flow_tex", voidFlow);
    }

    private static Texture2D? LoadMapTexture(string id, string fallbackPath)
    {
        return TextureAssetResolver.LoadMapTexture(id, fallbackPath);
    }

    private static Image BlurTerrainData(Image source, int radius)
    {
        int width = source.GetWidth();
        int height = source.GetHeight();
        var result = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        const float defaultThreshold = 0.02f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var center = source.GetPixel(x, y);
                if (IsDefaultTerrainColor(center, defaultThreshold))
                {
                    result.SetPixel(x, y, center);
                    continue;
                }

                Color blurred = AverageNeighborTerrainColor(source, x, y, radius, defaultThreshold);
                result.SetPixel(x, y, blurred);
            }
        }

        return result;
    }

    private static Color AverageNeighborTerrainColor(Image source, int x, int y, int radius, float defaultThreshold)
    {
        int width = source.GetWidth();
        int height = source.GetHeight();
        float r = 0.0f;
        float g = 0.0f;
        float b = 0.0f;
        float a = 0.0f;
        int count = 0;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int sx = Mathf.Clamp(x + dx, 0, width - 1);
                int sy = Mathf.Clamp(y + dy, 0, height - 1);
                var sample = source.GetPixel(sx, sy);
                if (IsDefaultTerrainColor(sample, defaultThreshold))
                    continue;

                r += sample.R;
                g += sample.G;
                b += sample.B;
                a += sample.A;
                count++;
            }
        }

        return count > 0
            ? new Color(r / count, g / count, b / count, a / count)
            : source.GetPixel(x, y);
    }

    private static bool IsDefaultTerrainColor(Color color, float threshold)
    {
        return Mathf.Abs(color.R - DefaultTerrainColor.R) < threshold
            && Mathf.Abs(color.G - DefaultTerrainColor.G) < threshold
            && Mathf.Abs(color.B - DefaultTerrainColor.B) < threshold;
    }
}
