using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Data;

public static class ResourceRegistry
{
    private const string ManifestPath = "res://assets/resource_manifest.json";

    private static readonly Dictionary<string, string> IconPaths = new();
    private static readonly Dictionary<string, string> SpriteFramesPaths = new();
    private static readonly Dictionary<string, string> MaterialPaths = new();

    private static readonly Dictionary<string, Texture2D> IconCache = new();
    private static readonly Dictionary<string, SpriteFrames> SpriteFramesCache = new();
    private static readonly Dictionary<string, Material> MaterialCache = new();

    private static readonly string[] DefaultIconDirs =
    [
        "res://assets",
        "res://BladeHexFrontend/src/assets/generated/class_icons",
        "res://BladeHexFrontend/src/assets/generated/accessories",
        "res://BladeHexFrontend/src/assets/generated/armor",
        "res://BladeHexFrontend/src/assets/generated/consumables",
        "res://BladeHexFrontend/src/assets/tiles",
        "res://BladeHexFrontend/src/assets/props",
    ];

    public static bool IsInitialized { get; private set; }

    public static void Initialize()
    {
        if (IsInitialized)
            return;

        ScanIconDirectories();
        LoadManifest();
        IsInitialized = true;
        GD.Print($"[ResourceRegistry] Initialized: {IconPaths.Count} icons, {SpriteFramesPaths.Count} sprite frames, {MaterialPaths.Count} materials");
    }

    public static void Reload()
    {
        Clear();
        Initialize();
    }

    public static void Clear()
    {
        IconPaths.Clear();
        SpriteFramesPaths.Clear();
        MaterialPaths.Clear();
        IconCache.Clear();
        SpriteFramesCache.Clear();
        MaterialCache.Clear();
        IsInitialized = false;
    }

    public static void RegisterIcon(string id, string resPath)
    {
        IconPaths[id] = resPath;
    }

    public static void RegisterIcon(string id, Texture2D texture)
    {
        IconCache[id] = texture;
    }

    public static void RegisterSpriteFrames(string id, string resPath)
    {
        SpriteFramesPaths[id] = resPath;
    }

    public static void RegisterSpriteFrames(string id, SpriteFrames frames)
    {
        SpriteFramesCache[id] = frames;
    }

    public static void RegisterMaterial(string id, string resPath)
    {
        MaterialPaths[id] = resPath;
    }

    public static void RegisterMaterial(string id, Material material)
    {
        MaterialCache[id] = material;
    }

    public static Texture2D? GetIcon(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (IconCache.TryGetValue(id, out var cached))
            return cached;

        if (TryGetPathCaseInsensitive(IconPaths, id, out var path))
        {
            var texture = LoadTexture(path);
            if (texture != null)
                IconCache[id] = texture;

            return texture;
        }

        return null;
    }

    public static SpriteFrames? GetSpriteFrames(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (SpriteFramesCache.TryGetValue(id, out var cached))
            return cached;

        if (SpriteFramesPaths.TryGetValue(id, out var path))
        {
            var frames = SpriteFramesFileLoader.Load(path);
            if (frames != null)
                SpriteFramesCache[id] = frames;

            return frames;
        }

        return null;
    }

    public static Material? GetMaterial(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (MaterialCache.TryGetValue(id, out var cached))
            return cached;

        if (MaterialPaths.TryGetValue(id, out var path))
        {
            var material = MaterialAssetResolver.Load(path);
            if (material != null)
                MaterialCache[id] = material;

            return material;
        }

        return null;
    }

    public static bool TryGet<T>(string? id, out T? resource) where T : Resource
    {
        resource = null;
        if (string.IsNullOrEmpty(id))
            return false;

        if (typeof(T) == typeof(Texture2D))
            resource = GetIcon(id) as T;
        else if (typeof(T) == typeof(SpriteFrames))
            resource = GetSpriteFrames(id) as T;
        else if (typeof(T) == typeof(Material))
            resource = GetMaterial(id) as T;
        else if (IconPaths.TryGetValue(id, out var iconPath))
            resource = ResourceAssetResolver.LoadPath<T>(iconPath);
        else if (SpriteFramesPaths.TryGetValue(id, out var framesPath))
            resource = ResourceAssetResolver.LoadPath<T>(framesPath);
        else if (MaterialPaths.TryGetValue(id, out var materialPath))
            resource = ResourceAssetResolver.LoadPath<T>(materialPath);

        return resource != null;
    }

    private static void LoadManifest()
    {
        if (!FileAccess.FileExists(ManifestPath))
            return;

        using var file = FileAccess.Open(ManifestPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return;

        string json = file.GetAsText();
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var parser = new Json();
            if (parser.Parse(json) != Error.Ok)
                return;

            var data = parser.Data;
            if (data.VariantType != Variant.Type.Dictionary)
                return;

            var manifest = data.AsGodotDictionary();
            int before = IconPaths.Count;
            LoadManifestMap(manifest, "icons", IconPaths);
            LoadManifestMap(manifest, "sprite_frames", SpriteFramesPaths);
            LoadManifestMap(manifest, "materials", MaterialPaths);
            GD.Print($"[ResourceRegistry] Manifest loaded: {IconPaths.Count - before} new icons");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[ResourceRegistry] Manifest parse error: {ex.Message}");
        }
    }

    private static void LoadManifestMap(
        Godot.Collections.Dictionary manifest,
        string key,
        Dictionary<string, string> target)
    {
        if (!manifest.TryGetValue(key, out var value) || value.VariantType != Variant.Type.Dictionary)
            return;

        var map = value.AsGodotDictionary();
        foreach (var entryKey in map.Keys)
            target[entryKey.AsString()] = map[entryKey].AsString();
    }

    private static void ScanIconDirectories()
    {
        foreach (var dir in DefaultIconDirs)
            TryScanDirRecursive(dir, IconPaths, ".png", stripExt: true);
    }

    private static void TryScanDirRecursive(
        string dirPath,
        Dictionary<string, string> target,
        string extension,
        bool stripExt)
    {
        using var dir = DirAccess.Open(dirPath);
        if (dir == null)
            return;

        dir.ListDirBegin();
        try
        {
            for (string file = dir.GetNext(); !string.IsNullOrEmpty(file); file = dir.GetNext())
            {
                if (dir.CurrentIsDir())
                {
                    if (file.StartsWith('.') || file.StartsWith('_'))
                        continue;

                    TryScanDirRecursive($"{dirPath}/{file}", target, extension, stripExt);
                    continue;
                }

                if (!file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                string id = stripExt ? System.IO.Path.GetFileNameWithoutExtension(file) : file;
                string path = $"{dirPath}/{file}";
                target.TryAdd(id, path);

                if (id.Length > 2 && id[^2] == '_' && id[^1] is >= 'a' and <= 'c')
                {
                    string baseId = id[..^2];
                    if (id[^1] == 'a')
                        target.TryAdd(baseId, path);
                }
            }
        }
        finally
        {
            dir.ListDirEnd();
        }
    }

    private static bool TryGetPathCaseInsensitive(
        Dictionary<string, string> paths,
        string id,
        out string path)
    {
        if (paths.TryGetValue(id, out path!))
            return true;

        foreach (var kvp in paths)
        {
            if (string.Equals(kvp.Key, id, StringComparison.OrdinalIgnoreCase))
            {
                path = kvp.Value;
                return true;
            }
        }

        path = "";
        return false;
    }

    private static Texture2D? LoadTexture(string path)
    {
        return TextureFileLoader.Load(path);
    }
}
