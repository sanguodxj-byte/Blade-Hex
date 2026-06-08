using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class ShaderAssetResolver
{
    private static readonly Dictionary<string, Shader?> ShaderCache = new();
    private static readonly HashSet<string> MissingKeysLogged = new();

    public static Shader? Load(string? idOrPath, string fallbackId = "")
    {
        if (string.IsNullOrWhiteSpace(idOrPath))
            return LoadFallback(fallbackId);

        string key = $"{idOrPath}|{fallbackId}";
        if (ShaderCache.TryGetValue(key, out var cached))
            return cached;

        var shader = LoadUncached(idOrPath);
        if (shader == null)
            shader = LoadFallback(fallbackId);

        if (shader == null)
            LogMissingOnce(idOrPath, fallbackId);

        ShaderCache[key] = shader;
        return shader;
    }

    public static void ClearCache()
    {
        ShaderCache.Clear();
        MissingKeysLogged.Clear();
    }

    private static Shader? LoadUncached(string idOrPath)
    {
        if (AssetCatalog.TryGetPath(AssetKind.Shader, idOrPath, out var catalogPath))
        {
            var catalogShader = LoadPath(catalogPath);
            if (catalogShader != null)
                return catalogShader;
        }

        return LoadPath(idOrPath);
    }

    private static Shader? LoadFallback(string fallbackId)
    {
        return string.IsNullOrWhiteSpace(fallbackId) ? null : LoadUncached(fallbackId);
    }

    private static Shader? LoadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!path.StartsWith("res://") && !path.StartsWith("uid://") && !path.StartsWith("user://"))
            return null;

        if (!ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<Shader>(path);
    }

    private static void LogMissingOnce(string idOrPath, string fallbackId)
    {
        string key = $"{idOrPath}|{fallbackId}";
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[ShaderAssetResolver] Missing shader asset: {key}");
    }
}
