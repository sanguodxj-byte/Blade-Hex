using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class AnimationAssetResolver
{
    private static readonly Dictionary<string, Animation?> AnimationCache = new();
    private static readonly HashSet<string> MissingKeysLogged = new();

    public static Animation? Load(string? idOrPath, string fallbackId = "")
    {
        if (string.IsNullOrWhiteSpace(idOrPath))
            return LoadFallback(fallbackId);

        string key = $"{idOrPath}|{fallbackId}";
        if (AnimationCache.TryGetValue(key, out var cached))
            return cached;

        var animation = LoadUncached(idOrPath);
        if (animation == null)
            animation = LoadFallback(fallbackId);

        if (animation == null)
            LogMissingOnce(idOrPath, fallbackId);

        AnimationCache[key] = animation;
        return animation;
    }

    public static void ClearCache()
    {
        AnimationCache.Clear();
        MissingKeysLogged.Clear();
    }

    private static Animation? LoadUncached(string idOrPath)
    {
        if (AssetCatalog.TryGetPath(AssetKind.Animation, idOrPath, out var catalogPath))
        {
            var catalogAnimation = LoadPath(catalogPath);
            if (catalogAnimation != null)
                return catalogAnimation;
        }

        return LoadPath(idOrPath);
    }

    private static Animation? LoadFallback(string fallbackId)
    {
        return string.IsNullOrWhiteSpace(fallbackId) ? null : LoadUncached(fallbackId);
    }

    private static Animation? LoadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!path.StartsWith("res://") && !path.StartsWith("uid://") && !path.StartsWith("user://"))
            return null;

        if (!ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<Animation>(path);
    }

    private static void LogMissingOnce(string idOrPath, string fallbackId)
    {
        string key = $"{idOrPath}|{fallbackId}";
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[AnimationAssetResolver] Missing animation asset: {key}");
    }
}
