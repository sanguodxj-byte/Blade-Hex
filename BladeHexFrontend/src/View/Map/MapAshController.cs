using Godot;
using BladeHex.Map;
using BladeHex.View.AssetSystem;

namespace BladeHex.View.Map;

/// <summary>
/// Runtime ash/corruption mask for the 2D overworld ground shader.
/// Stores one ash amount per axial tile in a low-resolution data texture.
/// </summary>
public partial class MapAshController : Node
{
    private const int DataTexSize = 768;
    private Image? _ashImage;
    private ImageTexture? _ashTexture;
    private HexOverworldRenderer2D? _renderer;
    private bool _dirty;

    public int AshDataTextureSize => DataTexSize;
    public long AshDataTextureBytes => DataTexSize * DataTexSize * 4L;

    public void Initialize(HexOverworldRenderer2D renderer)
    {
        _renderer = renderer;
        _ashImage = Image.CreateEmpty(DataTexSize, DataTexSize, false, Image.Format.Rgba8);
        _ashImage.Fill(Colors.Black);
        _ashTexture = ImageTexture.CreateFromImage(_ashImage);
        BindMaterial();
    }

    public override void _Process(double delta)
    {
        Flush();
    }

    public void SetTileAsh(Vector2I coord, float amount)
    {
        if (_ashImage == null) return;
        if (coord.X < 0 || coord.Y < 0 || coord.X >= DataTexSize || coord.Y >= DataTexSize) return;

        float clamped = Mathf.Clamp(amount, 0f, 1f);
        _ashImage.SetPixel(coord.X, coord.Y, new Color(clamped, clamped, clamped, 1f));
        _dirty = true;
    }

    public void MaxTileAsh(Vector2I coord, float amount)
    {
        if (_ashImage == null) return;
        if (coord.X < 0 || coord.Y < 0 || coord.X >= DataTexSize || coord.Y >= DataTexSize) return;

        float existing = _ashImage.GetPixel(coord.X, coord.Y).R;
        SetTileAsh(coord, Mathf.Max(existing, amount));
    }

    public void PaintAshCircle(Vector2I center, int radius, float amount)
    {
        int r2 = radius * radius;
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                var coord = new Vector2I(x, y);
                int dx = x - center.X;
                int dy = y - center.Y;
                if (dx * dx + dy * dy > r2) continue;

                float falloff = 1f - Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(1f, radius);
                MaxTileAsh(coord, amount * Mathf.Clamp(falloff, 0f, 1f));
            }
        }
    }

    public void Clear()
    {
        _ashImage?.Fill(Colors.Black);
        _dirty = true;
    }

    public bool LoadAshMask(Texture2D texture)
    {
        if (_ashImage == null || texture == null) return false;
        var source = texture.GetImage();
        if (source == null) return false;

        if (source.GetWidth() != DataTexSize || source.GetHeight() != DataTexSize)
            source.Resize(DataTexSize, DataTexSize, Image.Interpolation.Lanczos);

        for (int y = 0; y < DataTexSize; y++)
        {
            for (int x = 0; x < DataTexSize; x++)
            {
                float v = source.GetPixel(x, y).R;
                _ashImage.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        }

        _dirty = true;
        return true;
    }

    public bool LoadAshMask(string resourcePath)
    {
        var texture = TextureAssetResolver.LoadMapTexture(resourcePath);
        return texture != null && LoadAshMask(texture);
    }

    public void Flush()
    {
        if (!_dirty || _ashImage == null || _ashTexture == null) return;
        _ashTexture.Update(_ashImage);
        _dirty = false;
    }

    private void BindMaterial()
    {
        var mat = _renderer?.GroundMaterial;
        if (mat == null || _ashTexture == null) return;

        mat.SetShaderParameter("ash_data", _ashTexture);
        mat.SetShaderParameter("use_ash_data", true);
        mat.SetShaderParameter("ash_data_size", new Vector2(DataTexSize, DataTexSize));
        mat.SetShaderParameter("ash_color", new Color(0.56f, 0.55f, 0.50f));
        mat.SetShaderParameter("char_color", new Color(0.13f, 0.12f, 0.11f));
        mat.SetShaderParameter("burn_color", new Color(1.0f, 0.38f, 0.08f));
        mat.SetShaderParameter("ash_edge_width", 0.16f);
        mat.SetShaderParameter("ash_noise_strength", 0.18f);
        mat.SetShaderParameter("ash_desaturate", 0.85f);
    }
}
