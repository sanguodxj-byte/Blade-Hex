using BladeHex.View.AssetSystem;
using BladeHex.View.Data;
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

        var resolved = LoadPropTexture(propId);
        if (resolved != null)
            return CacheAndReturn(propId, resolved);

        string? fallbackId = GetVariantFallbackId(propId);
        if (!string.IsNullOrWhiteSpace(fallbackId) && fallbackId != propId)
        {
            resolved = LoadPropTexture(fallbackId);
            if (resolved != null)
                return CacheAndReturn(propId, resolved);
        }

        return CacheAndReturn(propId, GetPlaceholder());
    }

    public static bool HasTexture(string propId)
    {
        if (string.IsNullOrWhiteSpace(propId))
            return false;

        if (Cache.ContainsKey(propId))
            return true;

        var resolved = LoadPropTexture(propId);
        if (resolved != null)
        {
            Cache[propId] = resolved;
            return true;
        }

        string? fallbackId = GetVariantFallbackId(propId);
        if (string.IsNullOrWhiteSpace(fallbackId) || fallbackId == propId)
            return false;

        resolved = LoadPropTexture(fallbackId);
        if (resolved == null)
            return false;

        Cache[propId] = resolved;
        return true;
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

    private static Texture2D? LoadPropTexture(string propId)
    {
        var texture = TextureAssetResolver.LoadPath($"{PropsBaseDir}/{propId}.png");
        return texture ?? ResourceRegistry.GetIcon(propId);
    }

    private static string? GetVariantFallbackId(string propId)
    {
        if (HasVariantSuffix(propId))
            return null;

        if (propId.Contains("tree") || propId.Contains("forest") || propId.Contains("pine") || propId.Contains("fir"))
        {
            int index = Math.Abs(propId.GetHashCode()) % 4;
            if (propId.Contains("pine"))
                return $"tree_pine_{index}";
            if (propId.Contains("fir"))
                return $"tree_pine_{index}";
            if (propId.Contains("dead"))
                return $"tree_dead_{index}";
            if (propId.Contains("frozen"))
                return $"tree_pine_{index}";
            return $"tree_oak_{index}";
        }

        if (propId.Contains("bush") || propId.Contains("grass") || propId.Contains("flower") || propId.Contains("fern") || propId.Contains("reed"))
        {
            int index = Math.Abs(propId.GetHashCode()) % 4;
            return propId.Contains("dry") || propId.Contains("reed") ? $"bush_dry_{index}" : $"bush_green_{index}";
        }

        if (propId.Contains("rock") || propId.Contains("stone") || propId.Contains("ruin") || propId.Contains("boulder")
            || propId.Contains("cliff") || propId.Contains("shard") || propId.Contains("patch"))
        {
            int index = Math.Abs(propId.GetHashCode()) % 4;
            if (propId.Contains("moss"))
                return $"rock_moss_{index}";
            if (propId.Contains("small"))
                return $"rock_small_{index}";
            return $"rock_large_{index}";
        }

        if (propId.Contains("log") || propId.Contains("vine"))
        {
            int index = Math.Abs(propId.GetHashCode()) % 4;
            return propId.Contains("vine") ? $"bush_green_{index}" : $"tree_dead_{index}";
        }

        if (propId.Contains("cactus"))
        {
            int index = Math.Abs(propId.GetHashCode()) % 4;
            return $"bush_dry_{index}";
        }

        return null;
    }

    private static bool HasVariantSuffix(string propId)
    {
        int underscore = propId.LastIndexOf('_');
        return underscore >= 0
            && underscore < propId.Length - 1
            && int.TryParse(propId[(underscore + 1)..], out _);
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
