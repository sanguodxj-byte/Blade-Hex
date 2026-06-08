using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class CharacterPartTextureResolver
{
    private const string PartDir = "res://assets/generated_character_parts/";

    private static readonly Dictionary<string, Texture2D?> TextureCache = new();
    private static readonly HashSet<string> MissingKeysLogged = new();

    public static Texture2D? Load(string partType, string race, string gender, int index)
    {
        string key = BuildKey(partType, race, gender, index);
        if (TextureCache.TryGetValue(key, out var cached))
            return cached;

        var tex = LoadUncached(partType, race, gender, index);
        TextureCache[key] = tex;
        return tex;
    }

    public static void ClearCache()
    {
        TextureCache.Clear();
        MissingKeysLogged.Clear();
    }

    public static string GetLegacyPath(string partType, string race, string gender, int index)
    {
        return $"{PartDir}{partType}_{race}_{gender}_{index}.png";
    }

    private static Texture2D? LoadUncached(string partType, string race, string gender, int index)
    {
        foreach (string id in EnumerateCatalogIds(partType, race, gender, index))
        {
            if (AssetCatalog.TryGetPath(AssetKind.CharacterPart, id, out string catalogPath))
            {
                var catalogTex = TryLoadPath(catalogPath);
                if (catalogTex != null)
                    return catalogTex;
            }
        }

        foreach (string path in EnumerateCompatibilityPaths(partType, race, gender, index))
        {
            var tex = TryLoadPath(path);
            if (tex != null)
                return tex;
        }

        LogMissingOnce(partType, race, gender, index);
        return null;
    }

    private static Texture2D? TryLoadPath(string path)
    {
        return TextureAssetResolver.LoadPath(path);
    }

    private static IEnumerable<string> EnumerateCatalogIds(string partType, string race, string gender, int index)
    {
        string part = NormalizePart(partType);
        string raceKey = NormalizeRace(race);
        int oneBased = Mathf.Max(1, index);
        int zeroBased = Mathf.Max(0, oneBased - 1);

        foreach (string genderKey in GetGenderAliases(gender))
        {
            yield return $"{part}_{raceKey}_{genderKey}_{oneBased}";
            yield return $"{part}_{raceKey}_{genderKey}_{zeroBased}";

            if (part == "hair" || part == "decoration")
            {
                yield return $"{part}_{genderKey}_{oneBased}";
                yield return $"{part}_{genderKey}_{zeroBased}";
            }
        }
    }

    private static List<string> EnumerateCompatibilityPaths(string partType, string race, string gender, int index)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>();
        string part = NormalizePart(partType);
        string raceKey = NormalizeRace(race);
        int oneBased = Mathf.Max(1, index);
        int zeroBased = Mathf.Max(0, oneBased - 1);

        void AddPath(string path)
        {
            if (seen.Add(path))
                paths.Add(path);
        }

        foreach (string genderKey in GetGenderAliases(gender))
        {
            AddPath($"{PartDir}{part}_{raceKey}_{genderKey}_{oneBased}.png");
            AddPath($"{PartDir}{part}_{raceKey}_{genderKey}_{zeroBased}.png");

            AddPath($"{PartDir}{part}/{part}_{raceKey}_{genderKey}_{zeroBased}.png");
            AddPath($"{PartDir}{part}/{part}_{raceKey}_{genderKey}_{oneBased}.png");

            if (part == "hair" || part == "decoration")
            {
                AddPath($"{PartDir}{part}/{part}_{genderKey}_{zeroBased}.png");
                AddPath($"{PartDir}{part}/{part}_{genderKey}_{oneBased}.png");
            }
        }

        if (part != "face")
        {
            foreach (string genderKey in GetGenderAliases(gender))
            {
                AddPath($"{PartDir}backup/{part}_{raceKey}_{genderKey}_{oneBased}.png");
                AddPath($"{PartDir}backup/{part}_{raceKey}_{genderKey}_{zeroBased}.png");
            }
        }

        return paths;
    }

    private static string BuildKey(string partType, string race, string gender, int index)
    {
        return $"{NormalizePart(partType)}|{NormalizeRace(race)}|{NormalizeGender(gender)}|{Mathf.Max(1, index)}";
    }

    private static string NormalizePart(string partType)
    {
        return string.IsNullOrEmpty(partType) ? "head" : partType.ToLowerInvariant();
    }

    private static string NormalizeRace(string race)
    {
        if (string.IsNullOrEmpty(race))
            return "human";

        return race.ToLowerInvariant().Replace("_", "");
    }

    private static string NormalizeGender(string gender)
    {
        string key = string.IsNullOrEmpty(gender) ? "male" : gender.ToLowerInvariant();
        return key switch
        {
            "female" or "woman" => "female",
            _ => "male",
        };
    }

    private static IEnumerable<string> GetGenderAliases(string gender)
    {
        if (NormalizeGender(gender) == "female")
        {
            yield return "female";
            yield return "woman";
            yield break;
        }

        yield return "male";
        yield return "man";
    }

    private static void LogMissingOnce(string partType, string race, string gender, int index)
    {
        string key = BuildKey(partType, race, gender, index);
        if (MissingKeysLogged.Add(key))
            GD.PushWarning($"[CharacterPartTextureResolver] Missing character part: {key}");
    }
}
