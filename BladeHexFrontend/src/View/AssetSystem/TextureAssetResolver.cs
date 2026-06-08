using BladeHex.Data;
using BladeHex.View.Data;
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class TextureAssetResolver
{
    private static readonly string[] EquipmentTextureDirs =
    [
        "res://assets/armor",
        "res://assets/helmets",
        "res://assets/weapons",
        "res://assets/weapons_backup",
        "res://assets/shields",
        "res://assets/staves",
        "res://assets/spellbooks",
    ];

    private static readonly Dictionary<string, Texture2D?> TextureCache = new();
    private static readonly HashSet<string> MissingKeysLogged = new();

    public static Texture2D? Load(AssetKind kind, string? idOrPath, string fallbackId = "")
    {
        if (string.IsNullOrWhiteSpace(idOrPath))
            return LoadFallback(kind, fallbackId);

        string key = $"{kind}|{idOrPath}|{fallbackId}";
        if (TextureCache.TryGetValue(key, out var cached))
            return cached;

        bool hasFallback = !string.IsNullOrWhiteSpace(fallbackId);
        var texture = LoadUncached(kind, idOrPath, allowRegistryFallback: !hasFallback);
        if (texture == null)
            texture = LoadFallback(kind, fallbackId);
        if (texture == null && hasFallback)
            texture = LoadUncached(kind, idOrPath, allowRegistryFallback: true);

        if (texture == null)
            LogMissingOnce(kind, idOrPath, fallbackId);

        texture = NormalizeCharacterScopedTexture(kind, texture);
        TextureCache[key] = texture;
        return texture;
    }

    public static Texture2D? LoadEquipmentTexture(ItemData? item)
    {
        if (item == null)
            return null;

        return CharacterTextureNormalizer.Normalize(
            Load(AssetKind.EquipmentTexture, item.EquipTextureId, item.IconFallbackId));
    }

    public static Texture2D? LoadIcon(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.Icon, idOrPath, fallbackId);
    }

    public static Texture2D? LoadPortrait(string? idOrPath, string fallbackId = "")
    {
        return CharacterTextureNormalizer.Normalize(
            Load(AssetKind.Portrait, idOrPath, fallbackId));
    }

    public static Texture2D? LoadUnitSprite(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.UnitSprite, idOrPath, fallbackId);
    }

    public static Texture2D? LoadItemIcon(ItemData? item)
    {
        if (item == null)
            return null;

        return CharacterTextureNormalizer.Normalize(
            LoadIcon(item.IconId, item.IconFallbackId));
    }

    public static Texture2D? LoadCampaignIllustration(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.CampaignIllustration, idOrPath, fallbackId);
    }

    public static Texture2D? LoadPoiIllustration(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.PoiIllustration, idOrPath, fallbackId);
    }

    public static Texture2D? LoadOriginIllustration(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.OriginIllustration, idOrPath, fallbackId);
    }

    public static Texture2D? LoadUiTexture(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.UiTexture, idOrPath, fallbackId);
    }

    public static Texture2D? LoadFogIllustration(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.FogIllustration, idOrPath, fallbackId);
    }

    public static Texture2D? LoadProjectileTexture(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.ProjectileTexture, idOrPath, fallbackId);
    }

    public static Texture2D? LoadMapTexture(string? idOrPath, string fallbackId = "")
    {
        return Load(AssetKind.MapTexture, idOrPath, fallbackId);
    }

    public static Texture2D? LoadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string key = $"path|{path}";
        if (TextureCache.TryGetValue(key, out var cached))
            return cached;

        var texture = TryLoadPath(path);
        TextureCache[key] = texture;
        return texture;
    }

    public static void ClearCache()
    {
        TextureCache.Clear();
        MissingKeysLogged.Clear();
        CharacterTextureNormalizer.ClearCache();
    }

    private static Texture2D? LoadUncached(AssetKind kind, string idOrPath)
    {
        return LoadUncached(kind, idOrPath, [], allowRegistryFallback: true);
    }

    private static Texture2D? LoadUncached(AssetKind kind, string idOrPath, bool allowRegistryFallback)
    {
        return LoadUncached(kind, idOrPath, [], allowRegistryFallback);
    }

    private static Texture2D? LoadUncached(
        AssetKind kind,
        string idOrPath,
        HashSet<string> fallbackChain,
        bool allowRegistryFallback)
    {
        string fallbackKey = $"{kind}|{idOrPath}";
        if (!fallbackChain.Add(fallbackKey))
            return null;

        if (AssetCatalog.TryGet(kind, idOrPath, out var catalogEntry))
        {
            var catalogTexture = TryLoadPath(catalogEntry.Path);
            if (catalogTexture != null)
                return catalogTexture;

            if (!string.IsNullOrWhiteSpace(catalogEntry.FallbackId))
                return LoadUncached(kind, catalogEntry.FallbackId, fallbackChain, allowRegistryFallback);
        }

        var directTexture = TryLoadPath(idOrPath);
        if (directTexture != null)
            return directTexture;

        if (allowRegistryFallback && AllowsResourceRegistryFallback(kind))
        {
            var registeredTexture = ResourceRegistry.GetIcon(idOrPath);
            if (registeredTexture != null)
                return registeredTexture;
        }

        if (kind == AssetKind.EquipmentTexture)
            return LoadFromCompatibilityDirs(idOrPath, EquipmentTextureDirs);

        return null;
    }

    private static Texture2D? LoadFallback(AssetKind kind, string fallbackId)
    {
        if (string.IsNullOrWhiteSpace(fallbackId))
            return null;

        return LoadUncached(kind, fallbackId, [], allowRegistryFallback: true);
    }

    private static bool AllowsResourceRegistryFallback(AssetKind kind)
    {
        return kind is AssetKind.Icon
            or AssetKind.UiTexture
            or AssetKind.Portrait
            or AssetKind.EquipmentTexture
            or AssetKind.MapTexture;
    }

    private static Texture2D? NormalizeCharacterScopedTexture(AssetKind kind, Texture2D? texture)
    {
        return kind is AssetKind.EquipmentTexture or AssetKind.Portrait
            ? CharacterTextureNormalizer.Normalize(texture)
            : texture;
    }

    private static Texture2D? LoadFromCompatibilityDirs(string id, IReadOnlyList<string> dirs)
    {
        foreach (var fileName in BuildFileNames(id))
        {
            foreach (var dir in dirs)
            {
                var texture = TryLoadPath($"{dir}/{fileName}");
                if (texture != null)
                    return texture;
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildFileNames(string id)
    {
        if (id.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)
            || id.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase)
            || id.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase)
            || id.EndsWith(".webp", System.StringComparison.OrdinalIgnoreCase))
        {
            yield return id;
            yield break;
        }

        yield return $"{id}.png";
        yield return $"{id}.jpg";
        yield return $"{id}.jpeg";
        yield return $"{id}.webp";
    }

    private static Texture2D? TryLoadPath(string path)
    {
        return TextureFileLoader.Load(path);
    }

    private static void LogMissingOnce(AssetKind kind, string idOrPath, string fallbackId)
    {
        string key = $"{kind}|{idOrPath}|{fallbackId}";
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[TextureAssetResolver] Missing texture asset: {key}");
    }
}
