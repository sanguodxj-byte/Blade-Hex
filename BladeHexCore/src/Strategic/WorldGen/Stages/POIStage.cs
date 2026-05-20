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
            if (capital != null) { ApplyFootprint(capital, ctx.Chunks, usedPositions); ctx.Pois.Add(capital); }

            for (int i = 0; i < poiCount - 1; i++)
            {
                var poi = PlaceNationPOI(nation, territory, ctx, rng, usedPositions, i);
                if (poi != null) { ApplyFootprint(poi, ctx.Chunks, usedPositions); ctx.Pois.Add(poi); }
            }
        }

        PlaceWildPOIs(ctx.Chunks, ctx.Zones, ctx.Territories, ctx.Pois, rng, usedPositions);

        // 野外 POI 在 PlaceWildPOIs 内已加入 ctx.Pois，逐个 footprint
        for (int i = 0; i < ctx.Pois.Count; i++)
        {
            if (ctx.Pois[i].OccupiedHexes.Length == 0)
                ApplyFootprint(ctx.Pois[i], ctx.Chunks, usedPositions);
        }

        GD.Print($"[POIStage] {ctx.Pois.Count} 个 POI");
    }

    /// <summary>
    /// 根据 POI 类型查 preset，用 TryFit 给 POI 应用 footprint；失败时回退到 solo。
    /// </summary>
    private static void ApplyFootprint(
        OverworldPOI poi,
        Dictionary<Vector2I, ChunkData> chunks,
        HashSet<Vector2I> usedPositions)
    {
        // 通过 chunks 查 tile
        HexOverworldTile? GetTile(Vector2I hex)
        {
            var chunkCoord = ChunkData.WorldToChunk(hex.X, hex.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) return null;
            return chunk.GetTile(hex.X, hex.Y);
        }

        POIFootprintApplier.Apply(poi, GetTile, usedPositions);
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
        poi.GarrisonMax = 120;
        poi.GarrisonCurrent = 100;
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

        // 把 anchor 偏向已有 POI 附近，制造"聚落带"
        // 50% 概率随机均匀，50% 概率从已落点附近偏移 ±15 hex
        Vector2I center;
        if (usedPositions.Count > 0 && rng.NextDouble() < 0.5)
        {
            var seedPositions = new List<Vector2I>(usedPositions);
            var anchor = seedPositions[rng.Next(seedPositions.Count)];
            // 在 anchor 附近 ±15 hex 范围内找一个属于本国领土的 tile
            for (int tries = 0; tries < 10; tries++)
            {
                var c = new Vector2I(anchor.X + rng.Next(-15, 16), anchor.Y + rng.Next(-15, 16));
                if (territory.AllTiles.Contains(c))
                {
                    center = c;
                    goto FOUND;
                }
            }
            center = tileList[rng.Next(tileList.Count)];
        FOUND:;
        }
        else
        {
            center = tileList[rng.Next(tileList.Count)];
        }

        // 不同 POI 类型用不同最小间距：城堡/城镇间距大，小设施间距小
        int minDist = index < 2 ? 25 : (index < 5 ? 18 : 12);
        var pos = FindValidPosition(center, territory.AllTiles, ctx.Chunks, rng, usedPositions, minDist);
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
            poi.GarrisonMax = 50;
            poi.GarrisonCurrent = 40 + rng.Next(10);
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
            poi.GarrisonMax = 80 + rng.Next(20);
            poi.GarrisonCurrent = 60 + rng.Next(30);
            poi.Prosperity = 60 + rng.Next(20);
        }
        else if (index < 2)
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Fortress);
            poi.PoiTypeEnum = OverworldPOI.POIType.Castle;
            poi.HasBarracks = true;
            poi.HasBlacksmith = true;
            poi.GarrisonMax = 100 + rng.Next(50);
            poi.GarrisonCurrent = poi.GarrisonMax;
            poi.CastleDefenseLevel = 1 + rng.Next(2);
            poi.Prosperity = 40 + rng.Next(20);
        }
        else if (index < 5)
        {
            poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, terrainKey);
            poi.PoiTypeEnum = OverworldPOI.POIType.Village;
            poi.HasTavern = rng.Next(2) == 0;
            poi.GarrisonMax = 25 + rng.Next(10);
            poi.GarrisonCurrent = 20 + rng.Next(10);
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
                    poi.GarrisonMax = 10;
                    poi.GarrisonCurrent = 8 + rng.Next(3);
                    poi.Prosperity = 30 + rng.Next(20);
                    break;
                case "outpost":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Fortress);
                    poi.PoiTypeEnum = OverworldPOI.POIType.Outpost;
                    poi.HasBarracks = true;
                    poi.GarrisonMax = 40 + rng.Next(15);
                    poi.GarrisonCurrent = 30 + rng.Next(15);
                    poi.Prosperity = 20;
                    break;
                case "mine":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, terrainKey) + "矿场";
                    poi.PoiTypeEnum = OverworldPOI.POIType.Mine;
                    poi.GarrisonMax = 20;
                    poi.GarrisonCurrent = 15 + rng.Next(5);
                    poi.Prosperity = 40 + rng.Next(20);
                    break;
                case "farm":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Village, "Plain") + "农庄";
                    poi.PoiTypeEnum = OverworldPOI.POIType.Farm;
                    poi.GarrisonMax = 15;
                    poi.GarrisonCurrent = 10 + rng.Next(5);
                    poi.Prosperity = 35 + rng.Next(15);
                    break;
                case "shrine":
                    poi.PoiName = POINameGenerator.GeneratePOIName(POINameGenerator.POIType.Monastery);
                    poi.PoiTypeEnum = OverworldPOI.POIType.Shrine;
                    poi.GarrisonMax = 8;
                    poi.GarrisonCurrent = 6 + rng.Next(3);
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
