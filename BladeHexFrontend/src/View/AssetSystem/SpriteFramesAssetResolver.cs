using BladeHex.View.Data;
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class SpriteFramesAssetResolver
{
    private static readonly Dictionary<string, SpriteFrames?> FramesCache = new();
    private static readonly HashSet<string> MissingKeysLogged = new();

    public static SpriteFrames? Load(string? idOrPath, string fallbackId = "")
    {
        if (string.IsNullOrWhiteSpace(idOrPath))
            return LoadFallback(fallbackId);

        string key = $"{idOrPath}|{fallbackId}";
        if (FramesCache.TryGetValue(key, out var cached))
            return cached;

        var frames = LoadUncached(idOrPath);
        if (frames == null)
            frames = LoadFallback(fallbackId);

        if (frames == null)
            LogMissingOnce(idOrPath, fallbackId);

        FramesCache[key] = frames;
        return frames;
    }

    public static void ClearCache()
    {
        FramesCache.Clear();
        MissingKeysLogged.Clear();
    }

    private static SpriteFrames? LoadUncached(string idOrPath)
    {
        if (AssetCatalog.TryGetPath(AssetKind.SpriteFrames, idOrPath, out var catalogPath))
        {
            var catalogFrames = TryLoadPath(catalogPath);
            if (catalogFrames != null)
                return catalogFrames;
        }

        var directFrames = TryLoadPath(idOrPath);
        if (directFrames != null)
            return directFrames;

        return ResourceRegistry.GetSpriteFrames(idOrPath);
    }

    private static SpriteFrames? LoadFallback(string fallbackId)
    {
        return string.IsNullOrWhiteSpace(fallbackId) ? null : LoadUncached(fallbackId);
    }

    private static SpriteFrames? TryLoadPath(string path)
    {
        return SpriteFramesFileLoader.Load(path);
    }

    private static void LogMissingOnce(string idOrPath, string fallbackId)
    {
        string key = $"{idOrPath}|{fallbackId}";
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[SpriteFramesAssetResolver] Missing sprite frames: {key}");
    }
}
