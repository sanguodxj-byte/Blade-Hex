using BladeHex.Data;
using BladeHex.Map;
using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Map;

[GlobalClass]
public partial class CombatMaterialManager : RefCounted
{
    private const string TopTextureDir = "res://BladeHexFrontend/src/assets/tiles/battle_ground/tops";
    private const string CliffTextureDir = "res://BladeHexFrontend/src/assets/tiles/battle_ground/cliffs";
    private const string LegacyTextureDir = "res://BladeHexFrontend/src/assets/tiles/hex_terrain";

    private const string TopShaderPath = "res://BladeHexFrontend/src/assets/shaders/battle_ground_top.gdshader";
    private const string CliffShaderPath = "res://BladeHexFrontend/src/assets/shaders/battle_ground_cliff.gdshader";
    private const int BaselineElevation = 2;

    private static CombatMaterialManager? _instance;

    private readonly Dictionary<string, ShaderMaterial> _topMaterials = new();
    private readonly Dictionary<string, ShaderMaterial> _cliffMaterials = new();

    private Shader? _topShader;
    private Shader? _cliffShader;
    private StandardMaterial3D? _halfCoverMat;
    private StandardMaterial3D? _fullCoverMat;

    public static bool ForceUsePlaceholder = true;

    public static CombatMaterialManager Instance => _instance ??= new CombatMaterialManager();

    public static CombatMaterialManager GetInstance()
    {
        return Instance;
    }

    public ShaderMaterial GetMaterial(BattleCellData.TerrainType terrainType, int elevation)
    {
        return GetTopMaterial(terrainType, elevation);
    }

    public ShaderMaterial GetTopMaterial(BattleCellData.TerrainType terrainType, int elevation = 1)
    {
        string key = $"{(int)terrainType}_{elevation}";
        if (_topMaterials.TryGetValue(key, out var existing))
            return existing;

        var material = new ShaderMaterial();
        _topShader ??= ShaderAssetResolver.Load("battle_ground_top", TopShaderPath);
        if (_topShader != null)
            material.Shader = _topShader;

        var profile = BattleTerrainBridge.GetProfile(terrainType);
        var (topBase, topDark, topLight) = GetCombatTopPalette(terrainType, profile);
        topBase = ApplyElevationTint(topBase, elevation);
        topDark = ApplyElevationTint(topDark, elevation);
        topLight = ApplyElevationTint(topLight, elevation);

        var topTexture = ForceUsePlaceholder
            ? CreateProceduralTileTexture(topBase, topDark, topLight, profile.PatternType)
            : LoadTopTexture(profile) ?? CreateSolidColorTexture(topBase);
        material.SetShaderParameter("top_texture", topTexture);

        ConfigureTopMaterial(material, terrainType, profile);

        _topMaterials[key] = material;
        return material;
    }

    public ShaderMaterial GetCliffMaterial(BattleCellData.TerrainType terrainType)
    {
        var profile = BattleTerrainBridge.GetProfile(terrainType);
        string cacheKey = $"cliff_{(int)terrainType}";

        if (_cliffMaterials.TryGetValue(cacheKey, out var existing))
            return existing;

        var material = CreateCliffMaterial();
        var cliffTexture = LoadCliffTexture(profile.BattleCliffKey);
        if (cliffTexture == null)
        {
            var (baseColor, darkColor, lightColor) = GetCliffColors(terrainType, profile);
            cliffTexture = CreateProceduralTileTexture(
                baseColor,
                darkColor,
                lightColor,
                IsBrickTerrain(terrainType) ? 1 : 2);
        }

        material.SetShaderParameter("cliff_texture", cliffTexture);
        _cliffMaterials[cacheKey] = material;
        return material;
    }

    public ShaderMaterial GetCliffMaterialByKey(string cliffKey)
    {
        if (_cliffMaterials.TryGetValue(cliffKey, out var existing))
            return existing;

        var material = CreateCliffMaterial();
        var cliffTexture = LoadCliffTexture(cliffKey);
        if (cliffTexture == null)
        {
            bool isBrick = cliffKey.Contains("wall") || cliffKey.Contains("stone") || cliffKey.Contains("rock");
            cliffTexture = CreateProceduralTileTexture(
                new Color(0.40f, 0.34f, 0.28f),
                new Color(0.28f, 0.23f, 0.18f),
                new Color(0.48f, 0.42f, 0.35f),
                isBrick ? 1 : 2);
        }

        material.SetShaderParameter("cliff_texture", cliffTexture);
        _cliffMaterials[cliffKey] = material;
        return material;
    }

    public StandardMaterial3D GetCoverMaterial(int coverType)
    {
        if (coverType == 1)
        {
            _halfCoverMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.5f, 0.2f) };
            return _halfCoverMat;
        }

        _fullCoverMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.4f, 0.4f) };
        return _fullCoverMat;
    }

    private void ConfigureTopMaterial(
        ShaderMaterial material,
        BattleCellData.TerrainType terrainType,
        TerrainVisualProfile profile)
    {
        material.SetShaderParameter("use_stochastic", true);
        material.SetShaderParameter("blend_sharpness", 6.0f);
        material.SetShaderParameter("color_variance", 0.04f);
        material.SetShaderParameter("side_darken_bottom", 0.45f);
        material.SetShaderParameter("depth_tint_strength", 0.16f);

        var (sideBase, _, _) = GetCliffColors(terrainType, profile);
        material.SetShaderParameter("side_color", new Vector3(sideBase.R, sideBase.G, sideBase.B));

        var sideTexture = LoadCliffTexture(profile.BattleCliffKey);
        if (sideTexture == null)
        {
            var (baseColor, darkColor, lightColor) = GetCliffColors(terrainType, profile);
            sideTexture = CreateProceduralTileTexture(
                baseColor,
                darkColor,
                lightColor,
                IsBrickTerrain(terrainType) ? 1 : 2);
        }

        material.SetShaderParameter("side_texture", sideTexture);
        material.SetShaderParameter("use_side_texture", true);
    }

    private ShaderMaterial CreateCliffMaterial()
    {
        var material = new ShaderMaterial();
        _cliffShader ??= ShaderAssetResolver.Load("battle_ground_cliff", CliffShaderPath);
        if (_cliffShader != null)
            material.Shader = _cliffShader;

        return material;
    }

    private static Texture2D? LoadTopTexture(TerrainVisualProfile profile)
    {
        string primaryPath = $"{TopTextureDir}/{profile.BattleTopKey}_0.png";
        var primary = LoadMapTexture($"{profile.BattleTopKey}_0", primaryPath);
        if (primary != null)
            return primary;

        string legacyPath = $"{LegacyTextureDir}/{profile.OverworldKey}_0.png";
        return LoadMapTexture($"{profile.OverworldKey}_0", legacyPath);
    }

    private static Texture2D? LoadCliffTexture(string cliffKey)
    {
        string pngPath = $"{CliffTextureDir}/{cliffKey}.png";
        var png = LoadMapTexture(cliffKey, pngPath);
        if (png != null)
            return png;

        string jpegPath = $"{CliffTextureDir}/{cliffKey}.jpeg";
        return LoadMapTexture($"{cliffKey}.jpeg", jpegPath);
    }

    private static Texture2D? LoadMapTexture(string id, string path)
    {
        return TextureAssetResolver.LoadMapTexture(id, path);
    }

    private static (Color baseColor, Color darkColor, Color lightColor) GetCombatTopPalette(
        BattleCellData.TerrainType terrainType,
        TerrainVisualProfile profile)
    {
        return terrainType switch
        {
            BattleCellData.TerrainType.Snow => (
                new Color(0.66f, 0.72f, 0.74f),
                new Color(0.44f, 0.52f, 0.56f),
                new Color(0.80f, 0.85f, 0.86f)),

            BattleCellData.TerrainType.MountainSnow => (
                new Color(0.58f, 0.62f, 0.66f),
                new Color(0.38f, 0.42f, 0.46f),
                new Color(0.74f, 0.78f, 0.82f)),

            BattleCellData.TerrainType.Ice => (
                new Color(0.55f, 0.66f, 0.72f),
                new Color(0.34f, 0.46f, 0.54f),
                new Color(0.72f, 0.82f, 0.88f)),

            _ => (profile.DominantColor, profile.PaletteDark, profile.PaletteLight),
        };
    }

    private static Color ApplyElevationTint(Color color, int elevation)
    {
        int delta = Mathf.Clamp(elevation - BaselineElevation, -2, 3);
        float brightness = 1.0f + delta * 0.10f;
        float desaturate = delta * 0.08f;

        var tinted = new Color(
            Mathf.Clamp(color.R * brightness, 0.0f, 1.0f),
            Mathf.Clamp(color.G * brightness, 0.0f, 1.0f),
            Mathf.Clamp(color.B * brightness, 0.0f, 1.0f),
            color.A);

        float gray = tinted.R * 0.299f + tinted.G * 0.587f + tinted.B * 0.114f;
        return new Color(
            Mathf.Clamp(Mathf.Lerp(tinted.R, gray, desaturate), 0.0f, 1.0f),
            Mathf.Clamp(Mathf.Lerp(tinted.G, gray, desaturate), 0.0f, 1.0f),
            Mathf.Clamp(Mathf.Lerp(tinted.B, gray, desaturate), 0.0f, 1.0f),
            color.A);
    }

    private static (Color baseColor, Color darkColor, Color lightColor) GetCliffColors(
        BattleCellData.TerrainType terrainType,
        TerrainVisualProfile profile)
    {
        return terrainType switch
        {
            BattleCellData.TerrainType.Plains
                or BattleCellData.TerrainType.Grassland
                or BattleCellData.TerrainType.Savanna =>
                (new Color(0.38f, 0.30f, 0.20f), new Color(0.25f, 0.18f, 0.12f), new Color(0.45f, 0.36f, 0.26f)),

            BattleCellData.TerrainType.Forest
                or BattleCellData.TerrainType.DenseForest =>
                (new Color(0.30f, 0.22f, 0.14f), new Color(0.18f, 0.12f, 0.08f), new Color(0.38f, 0.28f, 0.18f)),

            BattleCellData.TerrainType.ShallowWater
                or BattleCellData.TerrainType.DeepWater =>
                (new Color(0.22f, 0.28f, 0.35f), new Color(0.14f, 0.18f, 0.25f), new Color(0.30f, 0.35f, 0.42f)),

            BattleCellData.TerrainType.Hills
                or BattleCellData.TerrainType.Mountain =>
                (new Color(0.42f, 0.40f, 0.38f), new Color(0.28f, 0.26f, 0.24f), new Color(0.52f, 0.50f, 0.47f)),

            BattleCellData.TerrainType.Sand =>
                (new Color(0.55f, 0.48f, 0.35f), new Color(0.40f, 0.34f, 0.24f), new Color(0.62f, 0.55f, 0.42f)),

            BattleCellData.TerrainType.Snow =>
                (new Color(0.55f, 0.56f, 0.58f), new Color(0.40f, 0.42f, 0.44f), new Color(0.65f, 0.67f, 0.70f)),

            BattleCellData.TerrainType.Swamp
                or BattleCellData.TerrainType.PoisonMushroom =>
                (new Color(0.25f, 0.30f, 0.20f), new Color(0.15f, 0.20f, 0.12f), new Color(0.32f, 0.38f, 0.26f)),

            BattleCellData.TerrainType.Road =>
                (new Color(0.42f, 0.36f, 0.28f), new Color(0.30f, 0.25f, 0.18f), new Color(0.50f, 0.44f, 0.35f)),

            BattleCellData.TerrainType.Wall
                or BattleCellData.TerrainType.Ruins =>
                (new Color(0.38f, 0.36f, 0.34f), new Color(0.25f, 0.24f, 0.22f), new Color(0.48f, 0.46f, 0.43f)),

            _ => (new Color(0.40f, 0.34f, 0.28f), new Color(0.28f, 0.23f, 0.18f), new Color(0.48f, 0.42f, 0.35f)),
        };
    }

    private static bool IsBrickTerrain(BattleCellData.TerrainType terrainType)
    {
        return terrainType == BattleCellData.TerrainType.Wall
            || terrainType == BattleCellData.TerrainType.Ruins
            || terrainType == BattleCellData.TerrainType.Rampart
            || terrainType == BattleCellData.TerrainType.Tower
            || terrainType == BattleCellData.TerrainType.Gate;
    }

    private static Texture2D CreateSolidColorTexture(Color color)
    {
        return CreateProceduralTileTexture(color, color * 0.82f, color * 1.12f);
    }

    private static Texture2D CreateProceduralTileTexture(Color baseColor, Color darkColor, Color lightColor, int patternType = 0)
    {
        const int size = 128;
        const int gridSize = 16;

        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        baseColor.A = 1.0f;
        darkColor.A = 1.0f;
        lightColor.A = 1.0f;

        var grid = new float[gridSize, gridSize];
        var rng = new System.Random(baseColor.GetHashCode());
        for (int gy = 0; gy < gridSize; gy++)
        {
            for (int gx = 0; gx < gridSize; gx++)
                grid[gx, gy] = (float)rng.NextDouble();
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = SampleTileableNoise(grid, x, y, size, gridSize);
                Color color = BlendPalette(value, baseColor, darkColor, lightColor);
                color = ApplyPattern(color, x, y, value, baseColor, darkColor, lightColor, patternType, rng);
                image.SetPixel(x, y, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static float SampleTileableNoise(float[,] grid, int x, int y, int size, int gridSize)
    {
        float gxf = (float)x / size * gridSize;
        float gyf = (float)y / size * gridSize;

        int gx0 = ((int)gxf) % gridSize;
        int gy0 = ((int)gyf) % gridSize;
        int gx1 = (gx0 + 1) % gridSize;
        int gy1 = (gy0 + 1) % gridSize;

        float fx = gxf - (int)gxf;
        float fy = gyf - (int)gyf;
        fx = fx * fx * (3.0f - 2.0f * fx);
        fy = fy * fy * (3.0f - 2.0f * fy);

        float v00 = grid[gx0, gy0];
        float v10 = grid[gx1, gy0];
        float v01 = grid[gx0, gy1];
        float v11 = grid[gx1, gy1];

        return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
    }

    private static Color BlendPalette(float value, Color baseColor, Color darkColor, Color lightColor)
    {
        if (value < 0.35f)
            return darkColor.Lerp(baseColor, value / 0.35f);

        if (value < 0.65f)
            return baseColor;

        return baseColor.Lerp(lightColor, (value - 0.65f) / 0.35f);
    }

    private static Color ApplyPattern(
        Color color,
        int x,
        int y,
        float value,
        Color baseColor,
        Color darkColor,
        Color lightColor,
        int patternType,
        System.Random rng)
    {
        if (patternType == 1)
            return ApplyBrickPattern(color, x, y, darkColor, lightColor);

        if (patternType == 2)
            return ApplyStrataPattern(y, value, baseColor, darkColor, lightColor, rng);

        return color;
    }

    private static Color ApplyBrickPattern(Color color, int x, int y, Color darkColor, Color lightColor)
    {
        const int size = 128;
        const int brickRows = 8;
        const int brickCols = 4;
        int rowHeight = size / brickRows;
        int colWidth = size / brickCols;

        int row = y / rowHeight;
        int shift = row % 2 == 1 ? colWidth / 2 : 0;
        int lx = (x + shift) % colWidth;
        int ly = y % rowHeight;

        if (ly <= 1 || ly >= rowHeight - 2 || lx <= 1 || lx >= colWidth - 2)
            return darkColor.Lerp(new Color(0.12f, 0.12f, 0.15f), 0.6f);

        if (ly == 2 || lx == 2)
            return color.Lerp(lightColor, 0.38f);

        if (ly == rowHeight - 3 || lx == colWidth - 3)
            return color.Lerp(darkColor, 0.38f);

        return color;
    }

    private static Color ApplyStrataPattern(
        int y,
        float value,
        Color baseColor,
        Color darkColor,
        Color lightColor,
        System.Random rng)
    {
        float wave = Mathf.Sin((y + value * 12.0f) * 0.22f) * 0.5f + 0.5f;
        Color color;
        if (wave < 0.3f)
            color = darkColor.Lerp(baseColor, wave / 0.3f);
        else if (wave < 0.7f)
            color = baseColor;
        else
            color = baseColor.Lerp(lightColor, (wave - 0.7f) / 0.3f);

        float noise = (float)rng.NextDouble();
        if (noise > 0.88f)
            return color.Lerp(darkColor, 0.22f);

        if (noise < 0.12f)
            return color.Lerp(lightColor, 0.18f);

        return color;
    }
}
