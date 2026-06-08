using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class PackedSceneAssetResolver
{
    private static readonly Dictionary<string, PackedScene?> SceneCache = new();
    private static readonly HashSet<string> MissingKeysLogged = new();

    public static PackedScene? Load(string? idOrPath, string fallbackId = "")
    {
        if (string.IsNullOrWhiteSpace(idOrPath))
            return LoadFallback(fallbackId);

        string key = $"{idOrPath}|{fallbackId}";
        if (SceneCache.TryGetValue(key, out var cached))
            return cached;

        var scene = LoadUncached(idOrPath);
        if (scene == null)
            scene = LoadFallback(fallbackId);

        if (scene == null)
            LogMissingOnce(idOrPath, fallbackId);

        SceneCache[key] = scene;
        return scene;
    }

    public static void ClearCache()
    {
        SceneCache.Clear();
        MissingKeysLogged.Clear();
    }

    private static PackedScene? LoadUncached(string idOrPath)
    {
        if (AssetCatalog.TryGetPath(AssetKind.PackedScene, idOrPath, out var catalogPath))
        {
            var catalogScene = LoadPath(catalogPath);
            if (catalogScene != null)
                return catalogScene;
        }

        return LoadPath(idOrPath);
    }

    private static PackedScene? LoadFallback(string fallbackId)
    {
        return string.IsNullOrWhiteSpace(fallbackId) ? null : LoadUncached(fallbackId);
    }

    private static PackedScene? LoadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!path.StartsWith("res://") && !path.StartsWith("uid://") && !path.StartsWith("user://"))
            return null;

        if (!ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<PackedScene>(path);
    }

    private static void LogMissingOnce(string idOrPath, string fallbackId)
    {
        string key = $"{idOrPath}|{fallbackId}";
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[PackedSceneAssetResolver] Missing packed scene asset: {key}");
    }
}
