using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class AudioAssetResolver
{
    private static readonly AssetKind[] DefaultAudioKinds =
    [
        AssetKind.Sfx,
        AssetKind.Bgm,
        AssetKind.Ambient,
    ];

    private static readonly Dictionary<string, AudioStream?> StreamCache = new();
    private static readonly HashSet<string> MissingKeysLogged = new();

    public static AudioStream? Load(AssetKind kind, string idOrPath)
    {
        if (string.IsNullOrWhiteSpace(idOrPath))
            return null;

        string key = $"{kind}|{idOrPath}";
        if (StreamCache.TryGetValue(key, out var cached))
            return cached;

        var stream = LoadUncached(kind, idOrPath);
        StreamCache[key] = stream;
        return stream;
    }

    public static AudioStream? LoadAny(string idOrPath, params AssetKind[] kinds)
    {
        if (string.IsNullOrWhiteSpace(idOrPath))
            return null;

        if (kinds.Length == 0)
            kinds = DefaultAudioKinds;

        string key = $"any|{string.Join(",", kinds)}|{idOrPath}";
        if (StreamCache.TryGetValue(key, out var cached))
            return cached;

        var stream = LoadAnyUncached(idOrPath, kinds);
        StreamCache[key] = stream;
        return stream;
    }

    public static void ClearCache()
    {
        StreamCache.Clear();
        MissingKeysLogged.Clear();
    }

    private static AudioStream? LoadUncached(AssetKind kind, string idOrPath)
    {
        if (AssetCatalog.TryGetPath(kind, idOrPath, out string catalogPath))
        {
            var catalogStream = TryLoadPath(catalogPath);
            if (catalogStream != null)
                return catalogStream;
        }

        var directStream = TryLoadPath(idOrPath);
        if (directStream != null)
            return directStream;

        LogMissingOnce(kind, idOrPath);
        return null;
    }

    private static AudioStream? LoadAnyUncached(string idOrPath, AssetKind[] kinds)
    {
        if (LooksLikePath(idOrPath))
        {
            var directStream = TryLoadPath(idOrPath);
            if (directStream != null)
                return directStream;
        }

        foreach (var kind in kinds)
        {
            if (AssetCatalog.TryGetPath(kind, idOrPath, out string catalogPath))
            {
                var catalogStream = TryLoadPath(catalogPath);
                if (catalogStream != null)
                    return catalogStream;
            }
        }

        if (!LooksLikePath(idOrPath))
        {
            var directStream = TryLoadPath(idOrPath);
            if (directStream != null)
                return directStream;

            LogMissingAnyOnce(idOrPath, kinds);
        }

        return null;
    }

    private static AudioStream? TryLoadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (path.StartsWith("res://") || path.StartsWith("uid://"))
        {
            if (!ResourceLoader.Exists(path))
                return null;

            return GD.Load<AudioStream>(path);
        }

        return LoadExternalAudio(path);
    }

    private static bool LooksLikePath(string value)
    {
        return value.StartsWith("res://")
            || value.StartsWith("uid://")
            || value.Contains('/')
            || value.Contains('\\')
            || value.GetExtension() is "ogg" or "mp3";
    }

    public static AudioStream? LoadExternalAudio(string path)
    {
        if (!FileAccess.FileExists(path))
            return null;

        string extension = path.GetExtension().ToLowerInvariant();
        if (extension == "ogg")
            return AudioStreamOggVorbis.LoadFromFile(path);

        if (extension == "mp3")
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
                return null;

            var mp3Stream = new AudioStreamMP3();
            mp3Stream.Data = file.GetBuffer((long)file.GetLength());
            return mp3Stream;
        }

        return null;
    }

    private static void LogMissingOnce(AssetKind kind, string idOrPath)
    {
        string key = $"{kind}|{idOrPath}";
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[AudioAssetResolver] Missing audio asset: {key}");
    }

    private static void LogMissingAnyOnce(string idOrPath, AssetKind[] kinds)
    {
        string key = $"any({string.Join(",", kinds)})|{idOrPath}";
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[AudioAssetResolver] Missing audio asset: {key}");
    }
}
