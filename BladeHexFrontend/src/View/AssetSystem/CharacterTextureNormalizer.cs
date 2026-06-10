using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class CharacterTextureNormalizer
{
    public const int TargetSize = 256;

    private static readonly Dictionary<Texture2D, Texture2D> Cache = new();

    public static Texture2D? Normalize(Texture2D? texture)
    {
        if (texture == null)
            return null;

        if (texture.GetWidth() == TargetSize && texture.GetHeight() == TargetSize)
            return texture;

        if (Cache.TryGetValue(texture, out var cached))
            return cached;

        var image = texture.GetImage();
        if (image == null || image.GetWidth() <= 0 || image.GetHeight() <= 0)
            return texture;

        image.Resize(TargetSize, TargetSize, Image.Interpolation.Lanczos);
        var normalized = ImageTexture.CreateFromImage(image);
        Cache[texture] = normalized;
        return normalized;
    }

    public static void ClearCache()
    {
        Cache.Clear();
    }
}
