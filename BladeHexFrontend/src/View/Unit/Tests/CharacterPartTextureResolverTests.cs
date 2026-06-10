using BladeHex.View.AssetSystem;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Unit.Tests;

public static class CharacterPartTextureResolverTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0;
        int failed = 0;

        foreach (var (name, run) in EnumerateTests())
        {
            try
            {
                var (ok, message) = run();
                if (ok)
                {
                    passed++;
                    details.Add($"  [PASS] {name}");
                }
                else
                {
                    failed++;
                    details.Add($"  [FAIL] {name}: {message}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                details.Add($"  [FAIL] {name}: Exception {ex.GetType().Name}: {ex.Message}");
            }
        }

        return (passed, failed, details);
    }

    private static IEnumerable<(string name, Func<(bool, string)> run)> EnumerateTests()
    {
        yield return (nameof(Head_UsesHeadDirectoryBeforeBackup), Head_UsesHeadDirectoryBeforeBackup);
        yield return (nameof(Hair_UsesHairDirectoryBeforeBackup), Hair_UsesHairDirectoryBeforeBackup);
        yield return (nameof(Head_DwarfFemale_FallsBackToDwarfMale), Head_DwarfFemale_FallsBackToDwarfMale);
        yield return (nameof(Head_HalfElf_FallsBackToElf), Head_HalfElf_FallsBackToElf);
        yield return (nameof(Head_HighIndex_FallsBackToLowIndex), Head_HighIndex_FallsBackToLowIndex);
    }

    private static (bool, string) Head_UsesHeadDirectoryBeforeBackup()
    {
        AssetCatalog.Reload();
        CharacterPartTextureResolver.ClearCache();

        string path = CharacterPartTextureResolver.ResolvePath("head", "human", "male", 1);
        return AssertPath(path, "res://assets/character_parts/head/head_human_man_0.png");
    }

    private static (bool, string) Hair_UsesHairDirectoryBeforeBackup()
    {
        AssetCatalog.Reload();
        CharacterPartTextureResolver.ClearCache();

        string path = CharacterPartTextureResolver.ResolvePath("hair", "human", "male", 1);
        return AssertPath(path, "res://assets/character_parts/hair/hair_man_0.png");
    }

    private static (bool, string) Head_DwarfFemale_FallsBackToDwarfMale()
    {
        AssetCatalog.Reload();
        CharacterPartTextureResolver.ClearCache();

        // dwarf 只有 man 纹理，female 应回退到 dwarf man
        string path = CharacterPartTextureResolver.ResolvePath("head", "dwarf", "female", 1);
        return AssertPath(path, "res://assets/character_parts/head/head_dwarf_man_0.png");
    }

    private static (bool, string) Head_HalfElf_FallsBackToElf()
    {
        AssetCatalog.Reload();
        CharacterPartTextureResolver.ClearCache();

        // HalfElf 没有自己的纹理，应回退到 elf
        string path = CharacterPartTextureResolver.ResolvePath("head", "halfelf", "female", 1);
        return AssertPath(path, "res://assets/character_parts/head/head_elf_woman_0.png");
    }

    private static (bool, string) Head_HighIndex_FallsBackToLowIndex()
    {
        AssetCatalog.Reload();
        CharacterPartTextureResolver.ClearCache();

        // 索引 5 超出已有纹理范围（human man 只有 0 和 1），应回退到 _0
        string path = CharacterPartTextureResolver.ResolvePath("head", "human", "male", 5);
        if (string.IsNullOrEmpty(path))
            return (false, "resolved empty path, expected a fallback texture");

        bool ok = path.Contains("head_human_man_") && (path.EndsWith("_0.png") || path.EndsWith("_1.png"));
        return (ok, ok ? "" : $"resolved '{path}', expected head_human_man_0 or _1");
    }

    private static (bool, string) AssertPath(string actual, string expected, bool allowBackup = false)
    {
        if (actual != expected)
            return (false, $"resolved '{actual}', expected '{expected}'");

        if (!allowBackup && actual.Contains("/backup/", StringComparison.OrdinalIgnoreCase))
            return (false, $"resolved backup path: {actual}");

        return (true, "");
    }
}
