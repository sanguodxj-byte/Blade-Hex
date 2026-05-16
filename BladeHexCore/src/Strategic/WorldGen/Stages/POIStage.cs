// POIStage.cs
// 世界生成阶段 7：在国家领土与野外区域放置 POI（首都/城镇/城堡/村庄/小型设施/野外巢穴）。
//
// 抽取自 WorldCreator.PlacePOIs + PlaceCapital + PlaceNationPOI + PlaceWildPOIs +
//   ChooseFacilityByTerrain + GetTerrainKeyForPosition + GetTerrainTypeAtPosition +
//   IsCoastalTile + GenerateWildLairName + FindValidPosition。
//
// RNG：seed ^ 0x504F49 ("POI")，与原实现一致。
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 7：放置国家 POI（首都 + 国家点）和野外巢穴 POI。
/// 沿海位置自动生成港口（每国一个）。
/// </summary>
public sealed class POIStage : IWorldStage
{
    public string Name => "放置城镇与据点";
    public float ProgressWeight => 8f;

    public void Execute(WorldBuildContext ctx)
    {
        var rng = ctx.NewRng(0x504F49); // "POI"
        var usedPositions = new HashSet<Vector2I>();

        foreach (var nation in ctx.Config.Nations)
        {
            if (!ctx.Territories.TryGetValue(nation.Id, out var territory)) continue;

            int poiCount = Math.Max(1, (int)(territory.TotalTiles * nation.PoiDensityPer1000Tiles / 1000.0f));

            var capital = PlaceCapital(nation, territory, ctx.Chunks, rng, usedPositions);
            if (capital != null) ctx.Pois.Add(capital);

            for (int i = 0; i < poiCount - 1; i++)
            {
                var poi = PlaceNationPOI(nation, territory, ctx, rng, usedPositions, i);
                if (poi != null) ctx.Pois.Add(poi);
            }
        }

        PlaceWildPOIs(ctx.Chunks, ctx.Zones, ctx.Territories, ctx.Pois, rng, usedPositions);

        GD.Print($"[POIStage] {ctx.Pois.Count} 个 POI");
    }

    // ========================================
    // 首都
    // ========================================

    private static OverworldPOI? PlaceCapital(
        NationConfig nation,
        NationTerritory territory,
        Dictionary<Vector2I, ChunkData> chunks,
        Random rng,
        HashSet<Vector2I> usedPositions)
    {
        var pos = FindValidPosition(territory.CoreZone.Centroid, territory.AllTiles, chunks, rng, usedPositions, 20);
        if (pos == null) return null;

        usedPositions.Add(pos.Value);
        var terrainKey = GetTerrainKeyForPosition(pos.Value, chunks);
        var poi = new OverworldPOI();
        poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City, terrainKey);
        poi.PoiTypeEnum = OverworldPOI.POIType.Town;
        poi.Position = HexOverworldTile.AxialToPixel(pos.Value.X, pos.Value.Y);
        poi.OwningFaction = nation.Id;
        poi.HasTavern = true;
        poi.HasShop = true;
        poi.HasBlacksmith = true;
        poi.HasQuestBoard = true;
        poi.GarrisonMax = 50;
        poi.GarrisonCurrent = 50;
        poi.Prosperity = 80;
        return poi;
    }

    // ========================================
    // 国家 POI
    // ========================================

    private static OverworldPOI? PlaceNationPOI(
        NationConfig nation,
        NationTerritory territory,
        WorldBuildContext ctx,
        Random rng,
        HashSet<Vector2I> usedPositions,
        int index)
    {
        var tileList = territory.AllTiles.ToList();
        var center = tileList[rng.Next(tileList.Count)];
        var pos = FindValidPosition(center, territory.AllTiles, ctx.Chunks, rng, usedPositions, 20);
        if (pos == null) return null;

        usedPositions.Add(pos.Value);

        var poi = new OverworldPOI();
        poi.Position = HexOverworldTile.AxialToPixel(pos.Value.X, pos.Value.Y);
        poi.OwningFaction = nation.Id;
        poi.HasQuestBoard = true;

        var terrainKey = GetTerrainKeyForPosition(pos.Value, ctx.Chunks);
        var terrainType = GetTerrainTypeAtPosition(pos.Value, ctx.Chunks);

        // 沿海位置 → 强制港口（每国一个）
        if (IsCoastalTile(pos.Value, ctx.Chunks) && !ctx.PortsPlaced.Contains(nation.Id))
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City, "Coast") + "港";
            poi.PoiTypeEnum = OverworldPOI.POIType.Port;
            poi.HasTavern = true;
            poi.HasShop = true;
            poi.FerryCost = 40 + rng.Next(30);
            poi.GarrisonMax = 20;
            poi.GarrisonCurrent = 20;
            poi.Prosperity = 50 + rng.Next(30);
            ctx.PortsPlaced.Add(nation.Id);
            return poi;
        }

        if (index < 1)
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City, terrainKey);
            poi.PoiTypeEnum = OverworldPOI.POIType.Town;
            poi.HasTavern = true;
            poi.HasShop = true;
            poi.HasBlacksmith = true;
            poi.GarrisonMax = 30;
            poi.GarrisonCurrent = 30;
            poi.Prosperity = 60 + rng.Next(20);
        }
        else if (index < 2)
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Fortress);
            poi.PoiTypeEnum = OverworldPOI.POIType.Castle;
            poi.HasBarracks = true;
            poi.HasBlacksmith = true;
            poi.GarrisonMax = 50 + rng.Next(30);
            poi.GarrisonCurrent = poi.GarrisonMax;
            poi.CastleDefenseLevel = 1 + rng.Next(2);
            poi.Prosperity = 40 + rng.Next(20);
        }
        else if (index < 5)
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, terrainKey);
            poi.PoiTypeEnum = OverworldPOI.POIType.Village;
            poi.HasTavern = rng.Next(2) == 0;
            poi.GarrisonMax = 10;
            poi.GarrisonCurrent = 10;
            poi.Prosperity = 30 + rng.Next(30);
        }
        else
        {
            var facilityType = ChooseFacilityByTerrain(terrainType, rng);
            switch (facilityType)
            {
                case "tavern":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Tavern);
                    poi.PoiTypeEnum = OverworldPOI.POIType.Tavern;
                    poi.HasTavern = true;
                    poi.HasShop = true;
                    poi.GarrisonMax = 5;
                    poi.GarrisonCurrent = 5;
                    poi.Prosperity = 30 + rng.Next(20);
                    break;
                case "outpost":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Fortress);
                    poi.PoiTypeEnum = OverworldPOI.POIType.Outpost;
                    poi.HasBarracks = true;
                    poi.GarrisonMax = 15;
                    poi.GarrisonCurrent = 15;
                    poi.Prosperity = 20;
                    break;
                case "mine":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, terrainKey) + "矿场";
                    poi.PoiTypeEnum = OverworldPOI.POIType.Mine;
                    poi.GarrisonMax = 8;
                    poi.GarrisonCurrent = 8;
                    poi.Prosperity = 40 + rng.Next(20);
                    break;
                case "farm":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, "Plain") + "农庄";
                    poi.PoiTypeEnum = OverworldPOI.POIType.Farm;
                    poi.GarrisonMax = 5;
                    poi.GarrisonCurrent = 5;
                    poi.Prosperity = 35 + rng.Next(15);
                    break;
                case "shrine":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Monastery);
                    poi.PoiTypeEnum = OverworldPOI.POIType.Shrine;
                    poi.GarrisonMax = 3;
                    poi.GarrisonCurrent = 3;
                    poi.Prosperity = 25;
                    break;
            }
        }

        return poi;
    }

    private static string ChooseFacilityByTerrain(HexOverworldTile.TerrainType terrain, Random rng)
    {
        return terrain switch
        {
            HexOverworldTile.TerrainType.Hills or
            HexOverworldTile.TerrainType.Rocky => rng.Next(2) == 0 ? "mine" : "outpost",

            HexOverworldTile.TerrainType.Plains or
            HexOverworldTile.TerrainType.Grassland => rng.Next(2) == 0 ? "farm" : "tavern",

            HexOverworldTile.TerrainType.Forest or
            HexOverworldTile.TerrainType.DenseForest or
            HexOverworldTile.TerrainType.Taiga => rng.Next(2) == 0 ? "tavern" : "shrine",

            HexOverworldTile.TerrainType.Swamp or
            HexOverworldTile.TerrainType.Bog or
            HexOverworldTile.TerrainType.Wasteland => "outpost",

            HexOverworldTile.TerrainType.Sand or
            HexOverworldTile.TerrainType.Savanna => "tavern",

            _ => new[] { "tavern", "farm", "outpost", "mine", "shrine" }[rng.Next(5)],
        };
    }

    private static string GetTerrainKeyForPosition(Vector2I coord, Dictionary<Vector2I, ChunkData> chunks)
    {
        var terrain = GetTerrainTypeAtPosition(coord, chunks);
        return terrain switch
        {
            HexOverworldTile.TerrainType.Plains or
            HexOverworldTile.TerrainType.Grassland or
            HexOverworldTile.TerrainType.Savanna => "Plain",

            HexOverworldTile.TerrainType.Forest or
            HexOverworldTile.TerrainType.DenseForest or
            HexOverworldTile.TerrainType.Taiga or
            HexOverworldTile.TerrainType.Jungle => "Forest",

            HexOverworldTile.TerrainType.Hills or
            HexOverworldTile.TerrainType.Rocky or
            HexOverworldTile.TerrainType.Snow or
            HexOverworldTile.TerrainType.Ice => "Mountain",

            HexOverworldTile.TerrainType.ShallowWater or
            HexOverworldTile.TerrainType.Sand => "Coast",

            HexOverworldTile.TerrainType.Swamp or
            HexOverworldTile.TerrainType.Bog or
            HexOverworldTile.TerrainType.Wasteland => "Swamp",

            _ => "Plain",
        };
    }

    private static HexOverworldTile.TerrainType GetTerrainTypeAtPosition(
        Vector2I coord, Dictionary<Vector2I, ChunkData> chunks)
    {
        var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
        if (!chunks.TryGetValue(chunkCoord, out var chunk)) return HexOverworldTile.TerrainType.Plains;
        var tile = chunk.GetTile(coord.X, coord.Y);
        return tile?.Terrain ?? HexOverworldTile.TerrainType.Plains;
    }

    // ========================================
    // 野外 POI
    // ========================================

    private static void PlaceWildPOIs(
        Dictionary<Vector2I, ChunkData> chunks,
        List<BiomeZone> zones,
        Dictionary<string, NationTerritory> territories,
        List<OverworldPOI> pois,
        Random rng,
        HashSet<Vector2I> usedPositions)
    {
        var assignedTiles = new HashSet<Vector2I>();
        foreach (var t in territories.Values)
            assignedTiles.UnionWith(t.AllTiles);

        var wildZones = zones.Where(z => !z.IsAssigned && z.TileCount >= 100).ToList();

        foreach (var zone in wildZones)
        {
            var pos = FindValidPosition(zone.Centroid, zone.TileCoords, chunks, rng, usedPositions, 15);
            if (pos == null) continue;

            usedPositions.Add(pos.Value);
            var terrainType = GetTerrainTypeAtPosition(pos.Value, chunks);

            var poi = new OverworldPOI();
            poi.Position = HexOverworldTile.AxialToPixel(pos.Value.X, pos.Value.Y);
            poi.OwningFaction = "";
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;

            switch (zone.DominantBiome)
            {
                case BiomeType.Mountain:
                    poi.LairTypeValue = rng.Next(2) == 0
                        ? OverworldPOI.LairType.DragonLair
                        : OverworldPOI.LairType.GolemForge;
                    poi.PoiName = poi.LairTypeValue == OverworldPOI.LairType.DragonLair
                        ? GenerateWildLairName("龙", terrainType, rng)
                        : GenerateWildLairName("魔像", terrainType, rng);
                    break;

                case BiomeType.Swamp:
                    poi.LairTypeValue = OverworldPOI.LairType.AncientTomb;
                    poi.PoiName = GenerateWildLairName("古墓", terrainType, rng);
                    break;

                case BiomeType.Forest:
                    poi.LairTypeValue = rng.Next(2) == 0
                        ? OverworldPOI.LairType.BanditCamp
                        : OverworldPOI.LairType.Ruins;
                    poi.PoiName = poi.LairTypeValue == OverworldPOI.LairType.BanditCamp
                        ? GenerateWildLairName("匪", terrainType, rng)
                        : GenerateWildLairName("遗迹", terrainType, rng);
                    break;

                case BiomeType.Tundra:
                    poi.LairTypeValue = OverworldPOI.LairType.Ruins;
                    poi.PoiName = GenerateWildLairName("冰封", terrainType, rng);
                    break;

                case BiomeType.Wasteland:
                    poi.LairTypeValue = rng.Next(2) == 0
                        ? OverworldPOI.LairType.RaiderOutpost
                        : OverworldPOI.LairType.Ruins;
                    poi.PoiName = GenerateWildLairName("荒", terrainType, rng);
                    break;

                default:
                    poi.LairTypeValue = OverworldPOI.LairType.BanditCamp;
                    poi.PoiName = GenerateWildLairName("野", terrainType, rng);
                    break;
            }

            poi.LairLevel = 1 + rng.Next(3);
            pois.Add(poi);
        }
    }

    private static string GenerateWildLairName(string themePrefix, HexOverworldTile.TerrainType terrain, Random rng)
    {
        string[] locationWords = terrain switch
        {
            HexOverworldTile.TerrainType.Hills or HexOverworldTile.TerrainType.Rocky
                => new[] { "岩窟", "山洞", "峭壁", "石穴", "崖巢" },
            HexOverworldTile.TerrainType.Forest or HexOverworldTile.TerrainType.DenseForest
                => new[] { "林寨", "密林", "古树", "幽谷", "荆棘" },
            HexOverworldTile.TerrainType.Swamp or HexOverworldTile.TerrainType.Bog
                => new[] { "沼穴", "泥潭", "腐地", "暗渊", "枯井" },
            HexOverworldTile.TerrainType.Snow or HexOverworldTile.TerrainType.Ice
                => new[] { "冰窟", "雪穴", "冻土", "霜墓", "寒渊" },
            HexOverworldTile.TerrainType.Sand or HexOverworldTile.TerrainType.Wasteland
                => new[] { "沙坑", "废墟", "枯骨", "风蚀", "荒冢" },
            _ => new[] { "巢穴", "据点", "营地", "废墟", "洞窟" },
        };

        string location = locationWords[rng.Next(locationWords.Length)];
        return themePrefix + location;
    }

    // ========================================
    // Helpers
    // ========================================

    private static bool IsCoastalTile(Vector2I coord, Dictionary<Vector2I, ChunkData> chunks)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, dir);
            var chunkCoord = ChunkData.WorldToChunk(nb.X, nb.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(nb.X, nb.Y);
            if (tile == null) continue;
            if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
                tile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                tile.Terrain == HexOverworldTile.TerrainType.Sand)
                return true;
        }
        return false;
    }

    private static Vector2I? FindValidPosition(
        Vector2I center,
        HashSet<Vector2I> validTiles,
        Dictionary<Vector2I, ChunkData> chunks,
        Random rng,
        HashSet<Vector2I> usedPositions,
        int minDistance)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int offsetQ = rng.Next(-30, 31);
            int offsetR = rng.Next(-30, 31);
            var candidate = new Vector2I(center.X + offsetQ, center.Y + offsetR);

            if (!validTiles.Contains(candidate)) continue;

            var chunkCoord = ChunkData.WorldToChunk(candidate.X, candidate.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(candidate.X, candidate.Y);
            if (tile == null || !tile.IsPassable) continue;
            if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
                tile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                tile.Terrain == HexOverworldTile.TerrainType.River ||
                tile.Terrain == HexOverworldTile.TerrainType.Ice) continue;

            bool tooClose = false;
            foreach (var used in usedPositions)
            {
                if (candidate.DistanceTo(used) < minDistance) { tooClose = true; break; }
            }
            if (tooClose) continue;

            return candidate;
        }

        return null;
    }
}
