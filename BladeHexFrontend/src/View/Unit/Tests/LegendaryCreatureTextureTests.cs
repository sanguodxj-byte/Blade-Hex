using BladeHex.Data;
using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Unit.Tests;

public static class LegendaryCreatureTextureTests
{
    private const string HydraId = "legend_hydra";

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
        yield return (nameof(LegendaryCreature_UsesSpriteDirectoryBeforeIconRegistry), LegendaryCreature_UsesSpriteDirectoryBeforeIconRegistry);
    }

    private static (bool, string) LegendaryCreature_UsesSpriteDirectoryBeforeIconRegistry()
    {
        CreatureTextureConfig.ClearSpriteCache();
        TextureAssetResolver.ClearCache();

        var data = new UnitData
        {
            UnitName = HydraId,
            EnemyTemplateId = HydraId,
            enemyType = UnitData.EnemyType.Legendary,
            LegendaryResistanceUses = 1,
        };

        string expectedPath = CreatureTextureConfig.GetExpectedSpritePath(data);
        var expected = TextureAssetResolver.LoadPath(expectedPath);
        if (expected == null)
            return (false, $"expected sprite path did not load: {expectedPath}");

        var actual = CreatureTextureConfig.TryLoadSprite(data);
        if (actual == null)
            return (false, "TryLoadSprite returned null");

        if (actual.GetWidth() != expected.GetWidth() || actual.GetHeight() != expected.GetHeight())
        {
            return (false,
                $"loaded {actual.GetWidth()}x{actual.GetHeight()}, expected sprite {expected.GetWidth()}x{expected.GetHeight()} from {expectedPath}");
        }

        return (true, "");
    }
}
