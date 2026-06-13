using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class CharacterPartTextureResolver
{
    private const string PartDir = "res://assets/character_parts/";

    private static readonly Dictionary<string, Texture2D?> TextureCache = new();

    public static Texture2D? Load(string partType, string race, string gender, int index)
    {
        string key = BuildKey(partType, race, gender, index);
        if (TextureCache.TryGetValue(key, out var cached))
            return cached;

        var tex = CharacterTextureNormalizer.Normalize(LoadUncached(partType, race, gender, index));
        TextureCache[key] = tex;
        return tex;
    }

    public static void ClearCache()
    {
        TextureCache.Clear();
        CharacterTextureNormalizer.ClearCache();
    }

    public static string GetLegacyPath(string partType, string race, string gender, int index)
    {
        return $"{PartDir}{partType}_{race}_{gender}_{index}.png";
    }

    public static string ResolvePath(string partType, string race, string gender, int index)
    {
        foreach (string path in EnumerateCandidatePaths(partType, race, gender, index))
        {
            if (ResourceLoader.Exists(path) || FileAccess.FileExists(path))
                return path;
        }

        return "";
    }

    private static Texture2D? LoadUncached(string partType, string race, string gender, int index)
    {
        foreach (string path in EnumerateCandidatePaths(partType, race, gender, index))
        {
            var tex = TryLoadPath(path);
            if (tex != null)
                return tex;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string partType, string race, string gender, int index)
    {
        var seen = new HashSet<string>();

        foreach (string id in EnumerateCatalogIds(partType, race, gender, index))
        {
            if (AssetCatalog.TryGetPath(AssetKind.CharacterPart, id, out string catalogPath)
                && seen.Add(catalogPath))
            {
                yield return catalogPath;
            }
        }

        foreach (string path in EnumerateCompatibilityPaths(partType, race, gender, index))
        {
            if (seen.Add(path))
                yield return path;
        }
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

        foreach (string genderKey in GetGenderAliases(part, gender))
        {
            foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                yield return $"{part}_{raceKey}_{genderKey}_{candidateIndex}";

            if (part == "head")
            {
                foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                    yield return $"face_{raceKey}_{genderKey}_{candidateIndex}";
            }

            if (part == "hair" || part == "decoration")
            {
                foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                    yield return $"{part}_{genderKey}_{candidateIndex}";
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

        foreach (string genderKey in GetGenderAliases(part, gender))
        {
            foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                AddPath($"{PartDir}{part}_{raceKey}_{genderKey}_{candidateIndex}.png");

            foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                AddPath($"{PartDir}{part}/{part}_{raceKey}_{genderKey}_{candidateIndex}.png");

            if (part == "head")
            {
                foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                    AddPath($"{PartDir}face_{raceKey}_{genderKey}_{candidateIndex}.png");

                foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                    AddPath($"{PartDir}backup/face_{raceKey}_{genderKey}_{candidateIndex}.png");
            }

            if (part == "hair" || part == "decoration")
            {
                foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                    AddPath($"{PartDir}{part}/{part}_{genderKey}_{candidateIndex}.png");
            }
        }

        if (part != "hair")
        {
            foreach (string genderKey in GetGenderAliases(part, gender))
            {
                AddPath($"{PartDir}backup/{part}_{raceKey}_{genderKey}_{oneBased}.png");
                AddPath($"{PartDir}backup/{part}_{raceKey}_{genderKey}_{zeroBased}.png");
            }
        }

        // ── 种族回退：HalfElf→elf, HalfOrc→dwarf ──
        foreach (string aliasRace in GetRaceAliases(raceKey))
        {
            foreach (string genderKey in GetGenderAliases(part, gender))
            {
                foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
                {
                    AddPath($"{PartDir}{part}_{aliasRace}_{genderKey}_{candidateIndex}.png");
                    AddPath($"{PartDir}{part}/{part}_{aliasRace}_{genderKey}_{candidateIndex}.png");
                }
            }
        }

        // ── 跨性别回退：同种族族内尝试另一性别（解决 elf 缺 man、dwarf 缺 woman 等） ──
        string oppositeGender = (NormalizeGender(gender) == "female") ? "man" : "woman";
        foreach (string r in GetAllRaceCandidates(raceKey))
        {
            foreach (int candidateIndex in GetIndexAliases(part, oneBased, zeroBased))
            {
                AddPath($"{PartDir}{part}_{r}_{oppositeGender}_{candidateIndex}.png");
                AddPath($"{PartDir}{part}/{part}_{r}_{oppositeGender}_{candidateIndex}.png");
            }
        }

        // ── 索引取模回退：保证一定能找到同种族/性别的最低索引纹理 ──
        if (part == "head" && zeroBased > 1)
        {
            foreach (string r in GetAllRaceCandidates(raceKey))
            {
                foreach (string genderKey in GetGenderAliases(part, gender))
                {
                    AddPath($"{PartDir}{part}_{r}_{genderKey}_0.png");
                    AddPath($"{PartDir}{part}_{r}_{genderKey}_1.png");
                    AddPath($"{PartDir}{part}/{part}_{r}_{genderKey}_0.png");
                    AddPath($"{PartDir}{part}/{part}_{r}_{genderKey}_1.png");
                }
            }
        }

        return paths;
    }

    /// <summary>返回自身 + 所有种族别名（用于回退遍历）。</summary>
    private static IEnumerable<string> GetAllRaceCandidates(string normalizedRace)
    {
        yield return normalizedRace;
        foreach (var alias in GetRaceAliases(normalizedRace))
            yield return alias;
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

    /// <summary>
    /// 返回与给定种族纹理兼容的备选种族列表（不含自身）。
    /// 例如 HalfElf → elf，HalfOrc → dwarf。
    /// </summary>
    private static IEnumerable<string> GetRaceAliases(string normalizedRace)
    {
        switch (normalizedRace)
        {
            case "halfelf":
                yield return "elf";
                break;
            case "halforc":
                yield return "dwarf";
                break;
        }
    }

    private static bool IsSameRaceFamily(string a, string b)
    {
        if (a == b) return true;
        foreach (var alias in GetRaceAliases(a))
            if (alias == b) return true;
        foreach (var alias in GetRaceAliases(b))
            if (alias == a) return true;
        return false;
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

    private static IEnumerable<int> GetIndexAliases(string part, int oneBased, int zeroBased)
    {
        if (part == "head" || part == "hair" || part == "decoration")
        {
            yield return zeroBased;
            if (oneBased != zeroBased)
                yield return oneBased;
            yield break;
        }

        yield return oneBased;
        if (zeroBased != oneBased)
            yield return zeroBased;
    }

    private static IEnumerable<string> GetGenderAliases(string part, string gender)
    {
        if (NormalizeGender(gender) == "female")
        {
            if (part == "head")
            {
                yield return "woman";
                yield return "female";
            }
            else
            {
                yield return "female";
                yield return "woman";
            }
            yield break;
        }

        yield return "man";
        yield return "male";
    }

}
