using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Map;

public static class BattlePropRegistry
{
    private const string PropsBaseDir = "res://BladeHexFrontend/src/assets/props/battle";

    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static Texture2D? _placeholder;

    public static Texture2D GetTexture(string propId)
    {
        if (string.IsNullOrWhiteSpace(propId))
            return GetPlaceholder();

        if (Cache.TryGetValue(propId, out var texture))
            return texture;

        var resolved = TextureAssetResolver.LoadMapTexture(propId, $"{PropsBaseDir}/{propId}.png");
        if (resolved != null)
            return CacheAndReturn(propId, resolved);

        string? fallbackId = GetVariantFallbackId(propId);
        if (!string.IsNullOrWhiteSpace(fallbackId) && fallbackId != propId)
            return CacheAndReturn(propId, GetTexture(fallbackId));

        return GetPlaceholder();
    }

    public static bool HasTexture(string propId)
    {
        if (string.IsNullOrWhiteSpace(propId))
            return false;

        if (Cache.ContainsKey(propId))
            return true;

        string path = $"{PropsBaseDir}/{propId}.png";
        var resolved = TextureAssetResolver.LoadMapTexture(propId, path);
        if (resolved != null)
        {
            Cache[propId] = resolved;
            return true;
        }

        string? fallbackId = GetVariantFallbackId(propId);
        return !string.IsNullOrWhiteSpace(fallbackId)
            && fallbackId != propId
            && HasTexture(fallbackId);
    }

    public static void ClearCache()
    {
        Cache.Clear();
        _placeholder = null;
    }

    private static Texture2D CacheAndReturn(string propId, Texture2D texture)
    {
        Cache[propId] = texture;
        return texture;
    }

    private static string? GetVariantFallbackId(string propId)
    {
        if (propId.Contains("tree") || propId.Contains("forest"))
        {
            int index = Math.Abs(propId.GetHashCode()) % 4;
            if (propId.Contains("pine"))
                return $"tree_pine_{index}";
            if (propId.Contains("dead"))
                return $"tree_dead_{index}";
            return $"tree_oak_{index}";
        }

        if (propId.Contains("bush") || propId.Contains("grass"))
        {
            int index = Math.Abs(propId.GetHashCode()) % 4;
            return propId.Contains("dry") ? $"bush_dry_{index}" : $"bush_green_{index}";
        }

        if (propId.Contains("rock") || propId.Contains("stone") || propId.Contains("ruin"))
        {
            int index = Math.Abs(propId.GetHashCode()) % 4;
            if (propId.Contains("moss"))
                return $"rock_moss_{index}";
            if (propId.Contains("small"))
                return $"rock_small_{index}";
            return $"rock_large_{index}";
        }

        return null;
    }

    private static Texture2D GetPlaceholder()
    {
        if (_placeholder != null)
            return _placeholder;

        var image = Image.CreateEmpty(16, 32, false, Image.Format.Rgba8);
        image.Fill(new Color(0.8f, 0.2f, 0.8f, 1f));
        _placeholder = ImageTexture.CreateFromImage(image);
        return _placeholder;
    }
}
