using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Map.Tests;

public static class BiomeZoneTerrainNamingTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }

        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(Analyzer_SplitsSameBiomeByExactTerrain), Analyzer_SplitsSameBiomeByExactTerrain);
        yield return Run(nameof(ZoneSerialization_KeepsDominantTerrain), ZoneSerialization_KeepsDominantTerrain);
        yield return Run(nameof(ZoneSerialization_OldBiomeMetaGetsTerrainFallback), ZoneSerialization_OldBiomeMetaGetsTerrainFallback);
        yield return Run(nameof(NameGenerator_UsesExactTerrainSuffix), NameGenerator_UsesExactTerrainSuffix);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try { var (ok, msg) = test(); return (name, ok, msg); }
        catch (Exception ex) { return (name, false, $"threw {ex.GetType().Name}: {ex.Message}"); }
    }

    private static (bool, string) Analyzer_SplitsSameBiomeByExactTerrain()
    {
        var chunk = new ChunkData { ChunkCoord = Vector2I.Zero, IsGenerated = true };
        AddTile(chunk, 0, 0, HexOverworldTile.TerrainType.Plains);
        AddTile(chunk, 1, 0, HexOverworldTile.TerrainType.Plains);
        AddTile(chunk, 2, 0, HexOverworldTile.TerrainType.Grassland);
        AddTile(chunk, 3, 0, HexOverworldTile.TerrainType.Grassland);

        var chunks = new Dictionary<Vector2I, ChunkData> { [Vector2I.Zero] = chunk };
        var zones = new BiomeZoneAnalyzer { MinZoneSize = 1 }.Analyze(chunks);

        if (zones.Count != 2)
            return (false, $"expected 2 exact terrain zones, got {zones.Count}");

        var plains = zones.SingleOrDefault(z => z.DominantTerrain == HexOverworldTile.TerrainType.Plains);
        var grassland = zones.SingleOrDefault(z => z.DominantTerrain == HexOverworldTile.TerrainType.Grassland);

        if (plains == null) return (false, "missing Plains zone");
        if (grassland == null) return (false, "missing Grassland zone");
        if (plains.DominantBiome != BiomeType.Plains || grassland.DominantBiome != BiomeType.Plains)
            return (false, "same-biome exact terrain zones should keep Plains biome for strategic consumers");
        if (plains.TileCount != 2 || grassland.TileCount != 2)
            return (false, $"expected 2 tiles each, got Plains={plains.TileCount}, Grassland={grassland.TileCount}");

        return (true, "");
    }

    private static (bool, string) ZoneSerialization_KeepsDominantTerrain()
    {
        var zone = new BiomeZone
        {
            Id = 9,
            DominantBiome = BiomeType.Forest,
            DominantTerrain = HexOverworldTile.TerrainType.DenseForest,
            Centroid = new Vector2I(4, 5),
        };

        var roundtrip = BiomeZone.DeserializeMeta(zone.Serialize());
        return roundtrip.DominantTerrain == HexOverworldTile.TerrainType.DenseForest
            ? (true, "")
            : (false, $"expected DenseForest, got {roundtrip.DominantTerrain}");
    }

    private static (bool, string) ZoneSerialization_OldBiomeMetaGetsTerrainFallback()
    {
        var data = new Godot.Collections.Dictionary
        {
            ["biome"] = (int)BiomeType.Swamp,
        };

        var zone = BiomeZone.DeserializeMeta(data);
        return zone.DominantTerrain == HexOverworldTile.TerrainType.Swamp
            ? (true, "")
            : (false, $"expected Swamp fallback, got {zone.DominantTerrain}");
    }

    private static (bool, string) NameGenerator_UsesExactTerrainSuffix()
    {
        var plainsName = new RegionNameGenerator(123).GenerateName(HexOverworldTile.TerrainType.Plains).english;
        var grassName = new RegionNameGenerator(123).GenerateName(HexOverworldTile.TerrainType.Grassland).english;
        var denseForestName = new RegionNameGenerator(123).GenerateName(HexOverworldTile.TerrainType.DenseForest).english;

        if (!EndsWithAny(plainsName, "Plain", "Flat", "Lowland", "Field"))
            return (false, $"Plains generated unexpected name '{plainsName}'");
        if (!EndsWithAny(grassName, "Grassland", "Meadow", "Pasture", "Steppe"))
            return (false, $"Grassland generated unexpected name '{grassName}'");
        if (!EndsWithAny(denseForestName, "Deepwood", "Wildwood", "Thicket", "Greenwood"))
            return (false, $"DenseForest generated unexpected name '{denseForestName}'");

        return (true, "");
    }

    private static void AddTile(ChunkData chunk, int q, int r, HexOverworldTile.TerrainType terrain)
    {
        var tile = HexOverworldTile.Create(q, r, terrain, 0.5f, 0.5f, 0.5f);
        chunk.Tiles[new Vector2I(q, r)] = tile;
    }

    private static bool EndsWithAny(string value, params string[] suffixes)
        => suffixes.Any(value.EndsWith);
}
