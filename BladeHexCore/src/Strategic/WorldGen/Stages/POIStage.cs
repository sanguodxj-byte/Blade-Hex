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

            var cores = new List<OverworldPOI>();

            // 1. 首都 (Town)
            var capital = PlaceCapital(nation, territory, ctx.Chunks, rng, usedPositions);
            if (capital != null)
            {
                var capHex = HexOverworldTile.PixelToAxial(capital.Position.X, capital.Position.Y);
                if (IsNearLargeOcean(capHex, ctx.Chunks))
                {
                    capital.IsPortCity = true;
                    capital.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City, "Coast") + "港";
                }
                ApplyFootprint(capital, ctx.Chunks, usedPositions);
                ctx.Pois.Add(capital);
                cores.Add(capital);
            }

            // 2. 额外核心 POI (Town / Castle)
            int extraTowns = 1;
            int castles = 1;
            if (territory.TotalTiles > 300) extraTowns += 1;
            if (territory.TotalTiles > 500) castles += 1;
            if (territory.TotalTiles > 800) extraTowns += 1;

            // 生成 Town
            for (int i = 0; i < extraTowns; i++)
            {
                var town = PlaceCorePOI(nation, territory, ctx.Chunks, rng, usedPositions, OverworldPOI.POIType.Town, cores);
                if (town != null)
                {
                    ApplyFootprint(town, ctx.Chunks, usedPositions);
                    ctx.Pois.Add(town);
                    cores.Add(town);
                }
            }

            // 生成 Castle
            for (int i = 0; i < castles; i++)
            {
                var castle = PlaceCorePOI(nation, territory, ctx.Chunks, rng, usedPositions, OverworldPOI.POIType.Castle, cores);
                if (castle != null)
                {
                    ApplyFootprint(castle, ctx.Chunks, usedPositions);
                    ctx.Pois.Add(castle);
                    cores.Add(castle);
                }
            }

            // 3. 生成每个核心 POI 的附属子 POI (Village, Mine, Farm)
            foreach (var core in cores)
            {
                int numVillages = core.PoiTypeEnum == OverworldPOI.POIType.Town
                    ? rng.Next(3, 7) // 3-6
                    : rng.Next(1, 4); // 1-3

                int numMines = core.PoiTypeEnum == OverworldPOI.POIType.Town ? rng.Next(1, 3) : 0; // 1-2
                int numFarms = core.PoiTypeEnum == OverworldPOI.POIType.Town ? rng.Next(1, 3) : 0; // 1-2

                // 生成 Village
                for (int j = 0; j < numVillages; j++)
                {
                    var village = PlaceSubPOI(nation, territory, ctx.Chunks, rng, usedPositions, OverworldPOI.POIType.Village, core, cores);
                    if (village != null)
                    {
                        ApplyFootprint(village, ctx.Chunks, usedPositions);
                        ctx.Pois.Add(village);
                    }
                }

                // 生成 Mine
                for (int j = 0; j < numMines; j++)
                {
                    var mine = PlaceSubPOI(nation, territory, ctx.Chunks, rng, usedPositions, OverworldPOI.POIType.Mine, core, cores);
                    if (mine != null)
                    {
                        ApplyFootprint(mine, ctx.Chunks, usedPositions);
                        ctx.Pois.Add(mine);
                    }
                }

                // 生成 Farm
                for (int j = 0; j < numFarms; j++)
                {
                    var farm = PlaceSubPOI(nation, territory, ctx.Chunks, rng, usedPositions, OverworldPOI.POIType.Farm, core, cores);
                    if (farm != null)
                    {
                        ApplyFootprint(farm, ctx.Chunks, usedPositions);
                        ctx.Pois.Add(farm);
                    }
                }
            }
        }

        PlaceWildPOIs(ctx.Chunks, ctx.Zones, ctx.Territories, ctx.Pois, rng, usedPositions);

        for (int i = 0; i < ctx.Pois.Count; i++)
        {
            if (ctx.Pois[i].OccupiedHexes.Length == 0)
                ApplyFootprint(ctx.Pois[i], ctx.Chunks, usedPositions);
        }

        GD.Print($"[POIStage] {ctx.Pois.Count} 个 POI");
    }

    private static void ApplyFootprint(
        OverworldPOI poi,
        Dictionary<Vector2I, ChunkData> chunks,
        HashSet<Vector2I> usedPositions)
    {
        HexOverworldTile? GetTile(Vector2I hex)
        {
            var chunkCoord = ChunkData.WorldToChunk(hex.X, hex.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) return null;
            return chunk.GetTile(hex.X, hex.Y);
        }

        POIFootprintApplier.Apply(poi, GetTile, usedPositions);
    }

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
        poi.GarrisonMax = 120;
        poi.GarrisonCurrent = 100;
        poi.Prosperity = 80;
        return poi;
    }

    private static OverworldPOI? PlaceCorePOI(
        NationConfig nation,
        NationTerritory territory,
        Dictionary<Vector2I, ChunkData> chunks,
        Random rng,
        HashSet<Vector2I> usedPositions,
        OverworldPOI.POIType type,
        List<OverworldPOI> existingCores)
    {
        var tileList = territory.AllTiles.ToList();
        if (tileList.Count == 0) return null;

        Vector2I center = tileList[rng.Next(tileList.Count)];
        if (existingCores.Count > 0 && rng.NextDouble() < 0.5)
        {
            var anchor = existingCores[rng.Next(existingCores.Count)];
            var anchorHex = HexOverworldTile.PixelToAxial(anchor.Position.X, anchor.Position.Y);
            for (int tries = 0; tries < 10; tries++)
            {
                var c = new Vector2I(anchorHex.X + rng.Next(-15, 16), anchorHex.Y + rng.Next(-15, 16));
                if (territory.AllTiles.Contains(c))
                {
                    center = c;
                    break;
                }
            }
        }

        int minDist = type == OverworldPOI.POIType.Town ? 18 : 15;
        var pos = FindValidPosition(center, territory.AllTiles, chunks, rng, usedPositions, minDist);
        if (pos == null) return null;

        usedPositions.Add(pos.Value);

        var poi = new OverworldPOI();
        poi.Position = HexOverworldTile.AxialToPixel(pos.Value.X, pos.Value.Y);
        poi.OwningFaction = nation.Id;
        poi.HasQuestBoard = true;

        var terrainKey = GetTerrainKeyForPosition(pos.Value, chunks);

        if (type == OverworldPOI.POIType.Town)
        {
            poi.PoiTypeEnum = OverworldPOI.POIType.Town;
            if (IsNearLargeOcean(pos.Value, chunks))
            {
                poi.IsPortCity = true;
                poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City, "Coast") + "港";
            }
            else
            {
                poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.City, terrainKey);
            }
            poi.HasTavern = true;
            poi.HasShop = true;
            poi.HasBlacksmith = true;
            poi.GarrisonMax = 80 + rng.Next(20);
            poi.GarrisonCurrent = 60 + rng.Next(30);
            poi.Prosperity = 60 + rng.Next(20);
        }
        else
        {
            poi.PoiTypeEnum = OverworldPOI.POIType.Castle;
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Fortress);
            poi.HasBarracks = true;
            poi.HasBlacksmith = true;
            poi.GarrisonMax = 100 + rng.Next(50);
            poi.GarrisonCurrent = poi.GarrisonMax;
            poi.CastleDefenseLevel = 1 + rng.Next(2);
            poi.Prosperity = 40 + rng.Next(20);
        }

        return poi;
    }

    private static OverworldPOI? PlaceSubPOI(
        NationConfig nation,
        NationTerritory territory,
        Dictionary<Vector2I, ChunkData> chunks,
        Random rng,
        HashSet<Vector2I> usedPositions,
        OverworldPOI.POIType type,
        OverworldPOI parent,
        List<OverworldPOI> existingCores)
    {
        var parentHex = HexOverworldTile.PixelToAxial(parent.Position.X, parent.Position.Y);
        
        Vector2I? finalPos = null;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int radius = rng.Next(5, 15);
            int dir = rng.Next(6);
            var offset = HexOverworldTile.GetNeighbor(0, 0, dir) * radius;
            var candidate = parentHex + offset;

            if (!territory.AllTiles.Contains(candidate)) continue;
            if (usedPositions.Contains(candidate)) continue;

            var chunkCoord = ChunkData.WorldToChunk(candidate.X, candidate.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(candidate.X, candidate.Y);
            if (tile == null || !tile.IsPassable) continue;
            if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
                tile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                tile.Terrain == HexOverworldTile.TerrainType.River ||
                tile.Terrain == HexOverworldTile.TerrainType.Ice) continue;

            bool tooClose = false;
            foreach (var pos in usedPositions)
            {
                if (candidate.DistanceTo(pos) < 2)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            var candidatePixel = HexOverworldTile.AxialToPixel(candidate.X, candidate.Y);
            float distToParent = candidatePixel.DistanceTo(parent.Position);
            bool parentIsClosest = true;
            foreach (var core in existingCores)
            {
                if (core == parent) continue;
                if (candidatePixel.DistanceTo(core.Position) <= distToParent)
                {
                    parentIsClosest = false;
                    break;
                }
            }

            if (parentIsClosest)
            {
                finalPos = candidate;
                break;
            }
        }

        if (finalPos == null) return null;

        usedPositions.Add(finalPos.Value);

        var poi = new OverworldPOI();
        poi.Position = HexOverworldTile.AxialToPixel(finalPos.Value.X, finalPos.Value.Y);
        poi.OwningFaction = nation.Id;
        poi.ParentPoiName = parent.PoiName;
        poi.PoiTypeEnum = type;

        var terrainKey = GetTerrainKeyForPosition(finalPos.Value, chunks);

        if (type == OverworldPOI.POIType.Village)
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, terrainKey);
            poi.HasTavern = rng.Next(2) == 0;
            poi.GarrisonMax = 25 + rng.Next(10);
            poi.GarrisonCurrent = 20 + rng.Next(10);
            poi.Prosperity = 30 + rng.Next(30);
        }
        else if (type == OverworldPOI.POIType.Mine)
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, terrainKey) + "矿场";
            poi.GarrisonMax = 20;
            poi.GarrisonCurrent = 15 + rng.Next(5);
            poi.Prosperity = 40 + rng.Next(20);
        }
        else if (type == OverworldPOI.POIType.Farm)
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, "Plain") + "农庄";
            poi.GarrisonMax = 15;
            poi.GarrisonCurrent = 10 + rng.Next(5);
            poi.Prosperity = 35 + rng.Next(15);
        }

        return poi;
    }

    private static bool IsNearLargeOcean(Vector2I coord, Dictionary<Vector2I, ChunkData> chunks, int oceanSizeThreshold = 30)
    {
        Vector2I? waterNeighbor = null;
        for (int dir = 0; dir < 6; dir++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, dir);
            var chunkCoord = ChunkData.WorldToChunk(nb.X, nb.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(nb.X, nb.Y);
            if (tile != null && (tile.Terrain == HexOverworldTile.TerrainType.DeepWater || tile.Terrain == HexOverworldTile.TerrainType.ShallowWater))
            {
                waterNeighbor = nb;
                break;
            }
        }

        if (waterNeighbor == null) return false;

        var queue = new Queue<Vector2I>();
        var visited = new HashSet<Vector2I>();
        queue.Enqueue(waterNeighbor.Value);
        visited.Add(waterNeighbor.Value);

        int count = 0;
        while (queue.Count > 0 && count < oceanSizeThreshold)
        {
            var curr = queue.Dequeue();
            count++;

            for (int dir = 0; dir < 6; dir++)
            {
                var nb = HexOverworldTile.GetNeighbor(curr.X, curr.Y, dir);
                if (visited.Contains(nb)) continue;

                var chunkCoord = ChunkData.WorldToChunk(nb.X, nb.Y);
                if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
                var tile = chunk.GetTile(nb.X, nb.Y);

                if (tile != null && (tile.Terrain == HexOverworldTile.TerrainType.DeepWater || tile.Terrain == HexOverworldTile.TerrainType.ShallowWater))
                {
                    visited.Add(nb);
                    queue.Enqueue(nb);
                }
            }
        }

        return count >= oceanSizeThreshold;
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
        // 加权采样：在大量候选中按"宜居度"加权抽签，
        // 让 POI 集中在水源 / 平原 / 海岸等宜居地，远离沼泽 / 荒原。
        // 候选集分两段：均匀采样 + 偏向河流附近（提高水源邻接率）。
        const int uniformCandidates = 80;
        const int riverProximityCandidates = 40;
        var candidates = new List<(Vector2I pos, float weight)>();
        float totalWeight = 0f;

        // 第一阶段：均匀采样
        for (int attempt = 0; attempt < uniformCandidates; attempt++)
        {
            int offsetQ = rng.Next(-30, 31);
            int offsetR = rng.Next(-30, 31);
            var candidate = new Vector2I(center.X + offsetQ, center.Y + offsetR);
            TryAddCandidate(candidate, validTiles, chunks, usedPositions, minDistance, candidates, ref totalWeight);
        }

        // 第二阶段：偏向 center 周围有水的格子（以邻河格为新偏移中心）
        // 在 center ±15 范围找一个邻水格作为新中心，再小范围采样
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int offsetQ = rng.Next(-15, 16);
            int offsetR = rng.Next(-15, 16);
            var probe = new Vector2I(center.X + offsetQ, center.Y + offsetR);
            var probeChunkCoord = ChunkData.WorldToChunk(probe.X, probe.Y);
            if (!chunks.TryGetValue(probeChunkCoord, out var probeChunk)) continue;
            var probeTile = probeChunk.GetTile(probe.X, probe.Y);
            if (probeTile == null) continue;
            if (!IsWaterSource(probeTile)) continue;

            // 在水源 ±5 hex 范围内多采几个候选
            for (int j = 0; j < riverProximityCandidates / 5; j++)
            {
                int oq = rng.Next(-5, 6);
                int or_ = rng.Next(-5, 6);
                var c = new Vector2I(probe.X + oq, probe.Y + or_);
                TryAddCandidate(c, validTiles, chunks, usedPositions, minDistance, candidates, ref totalWeight);
            }
            break; // 找到一个水源就够，避免偏向同一条河
        }

        if (candidates.Count == 0) return null;

        // 按权重抽签
        float roll = (float)rng.NextDouble() * totalWeight;
        float acc = 0f;
        foreach (var (pos, w) in candidates)
        {
            acc += w;
            if (roll <= acc) return pos;
        }
        return candidates[^1].pos;
    }

    private static void TryAddCandidate(
        Vector2I candidate,
        HashSet<Vector2I> validTiles,
        Dictionary<Vector2I, ChunkData> chunks,
        HashSet<Vector2I> usedPositions,
        int minDistance,
        List<(Vector2I pos, float weight)> candidates,
        ref float totalWeight)
    {
        if (!validTiles.Contains(candidate)) return;

        var chunkCoord = ChunkData.WorldToChunk(candidate.X, candidate.Y);
        if (!chunks.TryGetValue(chunkCoord, out var chunk)) return;
        var tile = chunk.GetTile(candidate.X, candidate.Y);
        if (tile == null || !tile.IsPassable) return;
        if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
            tile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
            tile.Terrain == HexOverworldTile.TerrainType.River ||
            tile.Terrain == HexOverworldTile.TerrainType.Ice) return;

        foreach (var used in usedPositions)
            if (candidate.DistanceTo(used) < minDistance) return;

        // 去重（候选可能在两阶段都被加入）
        foreach (var (pos, _) in candidates)
            if (pos == candidate) return;

        float w = HabitabilityScore(candidate, chunks);
        if (w <= 0f) return;
        candidates.Add((candidate, w));
        totalWeight += w;
    }

    /// <summary>
    /// 候选位置的宜居度评分 [0..∞]。
    /// 综合考虑：本格地形 + 水源邻近度 + 海岸位置。
    /// 评分差异大（最佳/最差 ≈ 50:1），保证选址显著偏向宜居地。
    /// </summary>
    private static float HabitabilityScore(Vector2I coord, Dictionary<Vector2I, ChunkData> chunks)
    {
        var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
        if (!chunks.TryGetValue(chunkCoord, out var chunk)) return 0f;
        var tile = chunk.GetTile(coord.X, coord.Y);
        if (tile == null) return 0f;

        // 1) 本格地形基础分（差异显著）
        float base_ = tile.Terrain switch
        {
            HexOverworldTile.TerrainType.Plains      => 5.0f,  // 最佳：开阔耕地
            HexOverworldTile.TerrainType.Grassland   => 4.0f,
            HexOverworldTile.TerrainType.Savanna     => 1.5f,
            HexOverworldTile.TerrainType.Forest      => 1.5f,
            HexOverworldTile.TerrainType.DenseForest => 0.5f,
            HexOverworldTile.TerrainType.Hills       => 1.5f,  // 山间小村
            HexOverworldTile.TerrainType.Taiga       => 0.6f,
            HexOverworldTile.TerrainType.Snow        => 0.2f,
            HexOverworldTile.TerrainType.Sand        => 0.2f,
            HexOverworldTile.TerrainType.Jungle      => 0.3f,
            HexOverworldTile.TerrainType.Wasteland   => 0.05f,  // 几乎不可居
            HexOverworldTile.TerrainType.Rocky       => 0.05f,
            HexOverworldTile.TerrainType.Swamp       => 0.05f,
            HexOverworldTile.TerrainType.Bog         => 0.02f,
            _ => 0.1f,
        };
        if (base_ <= 0f) return 0f;

        // 2) 水源加成：1 邻有河 / 海岸 → ×3.0；2 圈有 → ×1.5
        bool hasWater1 = false;
        bool hasWater2 = false;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, d);
            var nc = ChunkData.WorldToChunk(nb.X, nb.Y);
            if (chunks.TryGetValue(nc, out var nbChunk))
            {
                var nt = nbChunk.GetTile(nb.X, nb.Y);
                if (nt != null && IsWaterSource(nt)) { hasWater1 = true; break; }
            }
        }
        if (!hasWater1)
        {
            for (int dq = -2; dq <= 2 && !hasWater2; dq++)
            for (int dr = Math.Max(-2, -dq - 2); dr <= Math.Min(2, -dq + 2) && !hasWater2; dr++)
            {
                if (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr) < 4) continue;
                var p = new Vector2I(coord.X + dq, coord.Y + dr);
                var nc = ChunkData.WorldToChunk(p.X, p.Y);
                if (chunks.TryGetValue(nc, out var nbChunk))
                {
                    var nt = nbChunk.GetTile(p.X, p.Y);
                    if (nt != null && IsWaterSource(nt)) hasWater2 = true;
                }
            }
        }

        float waterMult = hasWater1 ? 3.0f : (hasWater2 ? 1.5f : 1.0f);

        // 3) 海岸加成：邻接 ShallowWater = 海岸贸易点
        bool isCoastal = false;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, d);
            var nc = ChunkData.WorldToChunk(nb.X, nb.Y);
            if (chunks.TryGetValue(nc, out var nbChunk))
            {
                var nt = nbChunk.GetTile(nb.X, nb.Y);
                if (nt != null && nt.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                { isCoastal = true; break; }
            }
        }
        float coastMult = isCoastal ? 1.5f : 1.0f;

        return base_ * waterMult * coastMult;
    }

    private static bool IsWaterSource(HexOverworldTile tile)
    {
        return tile.IsRiver
            || tile.Terrain == HexOverworldTile.TerrainType.River
            || tile.Terrain == HexOverworldTile.TerrainType.ShallowWater;
    }
}
