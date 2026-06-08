using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Map;

public static class OverworldPropRegistry
{
    private const string PropsBaseDir = "res://BladeHexFrontend/src/assets/sprites/overworld_props";

    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static readonly Dictionary<string, Texture2D> ColorCache = new();
    private static readonly object LoadLock = new();

    public static Texture2D GetTexture(string propId)
    {
        lock (LoadLock)
        {
            if (string.IsNullOrWhiteSpace(propId))
                return GetColoredPlaceholder("");

            if (Cache.TryGetValue(propId, out var texture))
                return texture;

            var resolved = TextureAssetResolver.LoadMapTexture(propId, $"{PropsBaseDir}/{propId}.png");
            if (resolved != null)
            {
                Cache[propId] = resolved;
                return resolved;
            }

            return GetColoredPlaceholder(propId);
        }
    }

    public static bool HasTexture(string propId)
    {
        if (string.IsNullOrWhiteSpace(propId))
            return false;

        if (Cache.ContainsKey(propId))
            return true;

        return AssetCatalog.TryGetPath(AssetKind.MapTexture, propId, out _)
            || ResourceLoader.Exists($"{PropsBaseDir}/{propId}.png");
    }

    public static void ClearCache()
    {
        Cache.Clear();
        ColorCache.Clear();
    }

    private static Texture2D GetColoredPlaceholder(string propId)
    {
        if (ColorCache.TryGetValue(propId, out var cached))
            return cached;

        Color color = GetDebugColor(propId);
        int width = IsTallProp(propId) ? 24 : 32;
        int height = IsTallProp(propId) ? 64 : 32;

        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        var texture = ImageTexture.CreateFromImage(image);
        ColorCache[propId] = texture;
        return texture;
    }

    private static Color GetDebugColor(string propId)
    {
        if (propId.Contains("oak"))
            return new Color(0.2f, 0.7f, 0.2f, 0.9f);
        if (propId.Contains("birch"))
            return new Color(0.5f, 0.8f, 0.3f, 0.9f);
        if (propId.Contains("pine") || propId.Contains("spruce"))
            return new Color(0.1f, 0.5f, 0.3f, 0.9f);
        if (propId.Contains("dark_oak"))
            return new Color(0.1f, 0.35f, 0.1f, 0.9f);
        if (propId.Contains("palm") || propId.Contains("jungle") || propId.Contains("vine"))
            return new Color(0.3f, 0.8f, 0.2f, 0.9f);
        if (propId.Contains("acacia"))
            return new Color(0.6f, 0.7f, 0.2f, 0.9f);
        if (propId.Contains("dead_tree"))
            return new Color(0.4f, 0.3f, 0.2f, 0.9f);
        if (propId.Contains("lone_tree"))
            return new Color(0.3f, 0.6f, 0.3f, 0.9f);
        if (propId.Contains("snow_pine"))
            return new Color(0.6f, 0.8f, 0.8f, 0.9f);

        if (propId.Contains("bush") || propId.Contains("flower"))
            return new Color(0.6f, 0.75f, 0.2f, 0.9f);
        if (propId.Contains("reed"))
            return new Color(0.5f, 0.6f, 0.3f, 0.9f);
        if (propId.Contains("cactus"))
            return new Color(0.3f, 0.7f, 0.4f, 0.9f);

        if (propId.Contains("rock") || propId.Contains("boulder"))
            return new Color(0.5f, 0.5f, 0.5f, 0.9f);
        if (propId.Contains("ice_rock") || propId.Contains("frozen"))
            return new Color(0.7f, 0.8f, 0.9f, 0.9f);
        if (propId.Contains("sand_rock"))
            return new Color(0.8f, 0.7f, 0.4f, 0.9f);
        if (propId.Contains("moss"))
            return new Color(0.4f, 0.55f, 0.35f, 0.9f);
        if (propId.Contains("cracked"))
            return new Color(0.6f, 0.45f, 0.3f, 0.9f);

        if (propId.Contains("mountain") || propId.Contains("peak"))
            return new Color(0.35f, 0.3f, 0.25f, 0.9f);
        if (propId.Contains("snow_peak"))
            return new Color(0.85f, 0.85f, 0.9f, 0.9f);
        if (propId.Contains("cliff"))
            return new Color(0.4f, 0.35f, 0.3f, 0.9f);

        if (propId.Contains("stump"))
            return new Color(0.5f, 0.35f, 0.2f, 0.9f);
        if (propId.Contains("bone"))
            return new Color(0.9f, 0.9f, 0.8f, 0.9f);
        if (propId.Contains("termite"))
            return new Color(0.7f, 0.5f, 0.3f, 0.9f);

        return new Color(0.8f, 0.2f, 0.8f, 0.9f);
    }

    private static bool IsTallProp(string propId)
    {
        return propId.Contains("tree")
            || propId.Contains("oak")
            || propId.Contains("pine")
            || propId.Contains("spruce")
            || propId.Contains("palm")
            || propId.Contains("acacia")
            || propId.Contains("peak")
            || propId.Contains("cliff");
    }
}
