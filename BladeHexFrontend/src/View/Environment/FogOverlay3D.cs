using BladeHex.Strategic;
using BladeHex.View.AssetSystem;
using BladeHex.View.Map;
using Godot;

namespace BladeHex.View.Environment;

[GlobalClass]
public partial class FogOverlay3D : MeshInstance3D
{
    [Export] public string ParchmentTexturePath { get; set; } = "";
    [Export] public float OverlayHeight { get; set; } = 0.8f;
    [Export] public float UnexploredOpacity { get; set; } = 0.92f;
    [Export] public float RevealedOpacity { get; set; } = 0.0f;
    [Export] public int EdgeFadeWidth { get; set; } = 3;
    [Export] public float TextureTilingScale { get; set; } = 0.005f;

    private ShaderMaterial? _material;
    private ImageTexture? _fogMaskTexture;
    private Image? _fogMaskImage;
    private FogOfWar? _fogData;

    private int _maskW;
    private int _maskH;
    private bool _dirty = true;
    private int _updateFrameSkip;
    private Vector2I _lastPlayerCell = new(-1, -1);

    public void Initialize(FogOfWar fogData, float worldWidthPx, float worldHeightPx)
    {
        _fogData = fogData;
        _maskW = fogData.GridW;
        _maskH = fogData.GridH;

        _fogMaskImage = Image.CreateEmpty(_maskW, _maskH, false, Image.Format.R8);
        _fogMaskImage.Fill(new Color(1, 0, 0, 1));
        _fogMaskTexture = ImageTexture.CreateFromImage(_fogMaskImage);

        CreateOverlayMesh(worldWidthPx, worldHeightPx);
        FullUpdateFogMask();

        GD.Print($"[FogOverlay3D] Initialized {_maskW}x{_maskH} mask, world={worldWidthPx:F0}x{worldHeightPx:F0}px.");
    }

    public override void _Process(double delta)
    {
        _updateFrameSkip++;
        if (_updateFrameSkip < 3)
            return;

        _updateFrameSkip = 0;
        if (_dirty && _fogData != null)
        {
            UpdateFogMask();
            _dirty = false;
        }
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    public void FullUpdateFogMask()
    {
        if (_fogData == null || _fogMaskImage == null || _fogMaskTexture == null)
            return;

        for (int gy = 0; gy < _maskH; gy++)
        {
            for (int gx = 0; gx < _maskW; gx++)
            {
                float opacity = IsUnexplored(gx, gy) ? 1.0f : 0.0f;
                _fogMaskImage.SetPixel(gx, gy, new Color(opacity, 0, 0, 1));
            }
        }

        _fogMaskTexture.Update(_fogMaskImage);
    }

    public void SetParchmentTexture(Texture2D texture)
    {
        _material?.SetShaderParameter("parchment_texture", texture);
    }

    public void SetUnexploredOpacity(float opacity)
    {
        UnexploredOpacity = opacity;
        _material?.SetShaderParameter("unexplored_opacity", opacity);
    }

    public ImageTexture? GetFogMaskTexture()
    {
        return _fogMaskTexture;
    }

    private void CreateOverlayMesh(float worldWidthPx, float worldHeightPx)
    {
        var worldSize = CoordConverter.PixelToWorld3D(new Vector2(worldWidthPx, worldHeightPx));
        var plane = new PlaneMesh
        {
            Size = new Vector2(worldSize.X + 20.0f, worldSize.Z + 20.0f),
            SubdivideWidth = 0,
            SubdivideDepth = 0,
        };

        Mesh = plane;
        Position = new Vector3(worldSize.X * 0.5f, OverlayHeight, worldSize.Z * 0.5f);

        _material = new ShaderMaterial
        {
            Shader = CreateFogShader(),
        };

        _material.SetShaderParameter("fog_mask", _fogMaskTexture!);
        _material.SetShaderParameter("unexplored_opacity", UnexploredOpacity);
        _material.SetShaderParameter("revealed_opacity", RevealedOpacity);
        _material.SetShaderParameter("tiling_scale", TextureTilingScale);
        _material.SetShaderParameter("mask_size", new Vector2(_maskW, _maskH));
        _material.SetShaderParameter("parchment_texture", ResolveParchmentTexture());

        MaterialOverride = _material;
    }

    private Texture2D ResolveParchmentTexture()
    {
        if (!string.IsNullOrWhiteSpace(ParchmentTexturePath))
        {
            var catalogTexture = TextureAssetResolver.LoadMapTexture(ParchmentTexturePath, ParchmentTexturePath);
            if (catalogTexture != null)
                return catalogTexture;
        }

        return GenerateProceduralParchment();
    }

    private void UpdateFogMask()
    {
        if (_fogData == null || _fogMaskImage == null || _fogMaskTexture == null)
            return;

        int rangeCells = (int)(_fogData.VisionRange / _fogData.CellSize) + 4;
        Vector2I currentCell = FindVisionCenter();

        int minGx = Mathf.Max(0, currentCell.X - rangeCells);
        int maxGx = Mathf.Min(_maskW - 1, currentCell.X + rangeCells);
        int minGy = Mathf.Max(0, currentCell.Y - rangeCells);
        int maxGy = Mathf.Min(_maskH - 1, currentCell.Y + rangeCells);

        if (_lastPlayerCell.X >= 0)
        {
            minGx = Mathf.Min(minGx, Mathf.Max(0, _lastPlayerCell.X - rangeCells));
            maxGx = Mathf.Max(maxGx, Mathf.Min(_maskW - 1, _lastPlayerCell.X + rangeCells));
            minGy = Mathf.Min(minGy, Mathf.Max(0, _lastPlayerCell.Y - rangeCells));
            maxGy = Mathf.Max(maxGy, Mathf.Min(_maskH - 1, _lastPlayerCell.Y + rangeCells));
        }

        _lastPlayerCell = currentCell;

        for (int gy = minGy; gy <= maxGy; gy++)
        {
            for (int gx = minGx; gx <= maxGx; gx++)
            {
                float opacity = IsUnexplored(gx, gy) ? 1.0f : 0.0f;
                _fogMaskImage.SetPixel(gx, gy, new Color(opacity, 0, 0, 1));
            }
        }

        _fogMaskTexture.Update(_fogMaskImage);
    }

    private bool IsUnexplored(int gx, int gy)
    {
        return _fogData?.ExploredGrid[gy, gx] == (byte)FogOfWar.FogState.Unexplored;
    }

    private Vector2I FindVisionCenter()
    {
        if (_fogData == null)
            return Vector2I.Zero;

        int midY = _maskH / 2;
        for (int gy = midY - 20; gy <= midY + 20; gy++)
        {
            if (gy < 0 || gy >= _maskH)
                continue;

            for (int gx = 0; gx < _maskW; gx++)
            {
                if (_fogData.ExploredGrid[gy, gx] == (byte)FogOfWar.FogState.InVision)
                    return new Vector2I(gx, gy);
            }
        }

        return new Vector2I(_maskW / 2, _maskH / 2);
    }

    private static Shader CreateFogShader()
    {
        return new Shader
        {
            Code = """
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded;

uniform sampler2D fog_mask : filter_linear, repeat_disable;
uniform sampler2D parchment_texture : filter_linear_mipmap, repeat_enable;
uniform float unexplored_opacity : hint_range(0.0, 1.0) = 0.92;
uniform float revealed_opacity : hint_range(0.0, 1.0) = 0.0;
uniform float tiling_scale = 0.005;
uniform float edge_softness = 0.15;
uniform vec2 mask_size = vec2(256.0, 256.0);

void fragment() {
    float fog_value = texture(fog_mask, UV).r;
    float alpha = smoothstep(0.0, edge_softness, fog_value);
    alpha *= unexplored_opacity;

    vec2 tiled_uv = UV * mask_size * tiling_scale;
    vec3 parchment_color = texture(parchment_texture, tiled_uv).rgb;

    float edge_darken = smoothstep(0.0, 0.3, fog_value) * (1.0 - smoothstep(0.7, 1.0, fog_value));
    parchment_color *= mix(1.0, 0.75, edge_darken * 0.5);

    ALBEDO = parchment_color;
    ALPHA = alpha;
}
""",
        };
    }

    private static ImageTexture GenerateProceduralParchment()
    {
        const int size = 256;
        var image = Image.CreateEmpty(size, size, true, Image.Format.Rgb8);

        var baseNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = 42,
            Frequency = 0.02f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4,
        };

        var fiberNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
            Seed = 123,
            Frequency = 0.05f,
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float largeScale = (baseNoise.GetNoise2D(x, y) + 1.0f) * 0.5f;
                float fiber = (fiberNoise.GetNoise2D(x * 2, y * 2) + 1.0f) * 0.5f;
                float variation = (largeScale - 0.5f) * 0.12f + (fiber - 0.5f) * 0.06f;

                float r = Mathf.Clamp(0.82f + variation, 0.0f, 1.0f);
                float g = Mathf.Clamp(0.72f + variation * 0.8f, 0.0f, 1.0f);
                float b = Mathf.Clamp(0.55f + variation * 0.5f, 0.0f, 1.0f);
                image.SetPixel(x, y, new Color(r, g, b));
            }
        }

        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
