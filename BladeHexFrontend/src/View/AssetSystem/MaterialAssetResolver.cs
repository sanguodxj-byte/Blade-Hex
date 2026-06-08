using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class MaterialAssetResolver
{
    private static readonly Dictionary<string, Material?> MaterialCache = new();
    private static readonly HashSet<string> MissingKeysLogged = new();

    public static Material? Load(string? idOrPath, string fallbackId = "")
    {
        if (string.IsNullOrWhiteSpace(idOrPath))
            return LoadFallback(fallbackId);

        string key = $"{idOrPath}|{fallbackId}";
        if (MaterialCache.TryGetValue(key, out var cached))
            return cached;

        var material = LoadUncached(idOrPath);
        if (material == null)
            material = LoadFallback(fallbackId);

        if (material == null)
            LogMissingOnce(idOrPath, fallbackId);

        MaterialCache[key] = material;
        return material;
    }

    public static void ClearCache()
    {
        MaterialCache.Clear();
        MissingKeysLogged.Clear();
    }

    private static Material? LoadUncached(string idOrPath)
    {
        if (AssetCatalog.TryGetPath(AssetKind.Material, idOrPath, out var catalogPath))
        {
            var catalogMaterial = ResourceAssetResolver.LoadPath<Material>(catalogPath);
            if (catalogMaterial != null)
                return catalogMaterial;
        }

        return ResourceAssetResolver.LoadPath<Material>(idOrPath);
    }

    private static Material? LoadFallback(string fallbackId)
    {
        return string.IsNullOrWhiteSpace(fallbackId) ? null : LoadUncached(fallbackId);
    }

    private static void LogMissingOnce(string idOrPath, string fallbackId)
    {
        string key = $"{idOrPath}|{fallbackId}";
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[MaterialAssetResolver] Missing material asset: {key}");
    }
}
