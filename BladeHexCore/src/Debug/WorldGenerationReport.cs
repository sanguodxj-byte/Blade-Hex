using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldGen;
using Godot;

namespace BladeHex.Diagnostics;

public sealed class WorldGenerationReport
{
    private readonly List<StageEntry> _stages = new();
    private readonly Dictionary<string, object?> _metadata = new();
    private readonly ulong _startedTicks = Time.GetTicksMsec();
    private WorldSnapshot _lastSnapshot = WorldSnapshot.Empty();

    public string ReportId { get; }
    public int Seed { get; }
    public string Status { get; private set; } = "running";
    public string StartedAt { get; } = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
    public string FinishedAt { get; private set; } = "";
    public long ElapsedMs { get; private set; }
    public string? FailedStage { get; private set; }
    public string? ExceptionType { get; private set; }
    public string? ExceptionMessage { get; private set; }
    public string? ExceptionStackTrace { get; private set; }

    public IReadOnlyList<StageEntry> Stages => _stages;

    public WorldGenerationReport(int seed, WorldCreationConfig config, IReadOnlyList<IWorldStage> stages)
    {
        Seed = seed;
        ReportId = $"worldgen_{DateTime.Now:yyyyMMdd_HHmmss}_{System.Environment.ProcessId}_{seed}";

        _metadata["seed"] = seed;
        _metadata["world_chunks_w"] = config.WorldChunksW;
        _metadata["world_chunks_h"] = config.WorldChunksH;
        _metadata["world_tile_width"] = config.WorldTileWidth;
        _metadata["world_tile_height"] = config.WorldTileHeight;
        _metadata["expected_chunks"] = config.WorldChunksW * config.WorldChunksH;
        _metadata["expected_tiles"] = config.WorldTileWidth * config.WorldTileHeight;
        _metadata["min_biome_zone_size"] = config.MinBiomeZoneSize;
        _metadata["nation_count"] = config.Nations.Count;
        _metadata["nations"] = config.Nations.Select(n => new Dictionary<string, object?>
        {
            ["id"] = n.Id,
            ["name"] = n.DisplayName,
            ["race"] = n.Race,
            ["major"] = n.IsMajorNation,
            ["population_scale"] = n.PopulationScale,
            ["preferred_biomes"] = n.PreferredBiomes.Select(b => b.ToString()).ToArray(),
        }).ToArray();
        _metadata["stage_plan"] = stages.Select((s, i) => new Dictionary<string, object?>
        {
            ["index"] = i + 1,
            ["name"] = s.Name,
            ["weight"] = s.ProgressWeight,
        }).ToArray();
    }

    public StageEntry BeginStage(IWorldStage stage, WorldBuildContext ctx, float progress)
    {
        var entry = new StageEntry
        {
            Index = _stages.Count + 1,
            Name = stage.Name,
            ProgressWeight = stage.ProgressWeight,
            ProgressAtStart = progress,
            StartedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture),
            Before = WorldSnapshot.Capture(ctx),
        };
        entry.StartTicks = Time.GetTicksMsec();
        _stages.Add(entry);
        _lastSnapshot = entry.Before;
        return entry;
    }

    public void EndStage(StageEntry entry, WorldBuildContext ctx)
    {
        entry.EndedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
        entry.ElapsedMs = (long)(Time.GetTicksMsec() - entry.StartTicks);
        entry.After = WorldSnapshot.Capture(ctx);
        entry.Delta = WorldSnapshotDelta.From(entry.Before, entry.After);
        entry.Status = "ok";
        _lastSnapshot = entry.After;
    }

    public void FailStage(StageEntry entry, WorldBuildContext ctx, Exception ex)
    {
        entry.EndedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
        entry.ElapsedMs = (long)(Time.GetTicksMsec() - entry.StartTicks);
        entry.After = WorldSnapshot.Capture(ctx);
        entry.Delta = WorldSnapshotDelta.From(entry.Before, entry.After);
        entry.Status = "failed";
        entry.ExceptionType = ex.GetType().FullName ?? ex.GetType().Name;
        entry.ExceptionMessage = ex.Message;
        entry.ExceptionStackTrace = ex.StackTrace ?? "";
        _lastSnapshot = entry.After;

        FailedStage = entry.Name;
        ExceptionType = entry.ExceptionType;
        ExceptionMessage = entry.ExceptionMessage;
        ExceptionStackTrace = entry.ExceptionStackTrace;
    }

    public void Complete(WorldBuildContext ctx)
    {
        Status = "ok";
        _lastSnapshot = WorldSnapshot.Capture(ctx);
    }

    public void Fail(Exception ex)
    {
        Status = "failed";
        ExceptionType ??= ex.GetType().FullName ?? ex.GetType().Name;
        ExceptionMessage ??= ex.Message;
        ExceptionStackTrace ??= ex.StackTrace ?? "";
    }

    public IReadOnlyDictionary<string, string> FinishAndWrite()
    {
        FinishedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
        ElapsedMs = (long)(Time.GetTicksMsec() - _startedTicks);
        if (Status == "running")
            Status = "aborted";

        var json = ToSerializable();
        return DiagnosticLog.WriteReport(GetReportLevel(), "worldgen", ReportId, json, ToMarkdown());
    }

    public Dictionary<string, object?> ToSerializable() => new()
    {
        ["report_id"] = ReportId,
        ["report_level"] = GetReportLevel().ToString().ToLowerInvariant(),
        ["status"] = Status,
        ["seed"] = Seed,
        ["started_at"] = StartedAt,
        ["finished_at"] = FinishedAt,
        ["elapsed_ms"] = ElapsedMs,
        ["failed_stage"] = FailedStage,
        ["exception_type"] = ExceptionType,
        ["exception_message"] = ExceptionMessage,
        ["exception_stack_trace"] = ExceptionStackTrace,
        ["metadata"] = _metadata,
        ["final_snapshot"] = _lastSnapshot,
        ["stages"] = _stages,
    };

    private DiagnosticReportLevel GetReportLevel()
    {
        return Status switch
        {
            "failed" => DiagnosticReportLevel.Error,
            "aborted" => DiagnosticReportLevel.Warn,
            _ => DiagnosticReportLevel.Debug,
        };
    }

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# World Generation Diagnostic Report");
        sb.AppendLine();
        sb.AppendLine($"- Report: `{ReportId}`");
        sb.AppendLine($"- Status: `{Status}`");
        sb.AppendLine($"- Seed: `{Seed}`");
        sb.AppendLine($"- Started: `{StartedAt}`");
        sb.AppendLine($"- Finished: `{FinishedAt}`");
        sb.AppendLine($"- Elapsed: `{ElapsedMs} ms`");
        if (!string.IsNullOrWhiteSpace(FailedStage))
            sb.AppendLine($"- Failed stage: `{FailedStage}`");
        if (!string.IsNullOrWhiteSpace(ExceptionMessage))
            sb.AppendLine($"- Exception: `{ExceptionType}: {ExceptionMessage}`");

        sb.AppendLine();
        sb.AppendLine("## Config");
        sb.AppendLine();
        foreach (var key in new[]
        {
            "world_chunks_w",
            "world_chunks_h",
            "world_tile_width",
            "world_tile_height",
            "expected_chunks",
            "expected_tiles",
            "min_biome_zone_size",
            "nation_count",
        })
        {
            if (_metadata.TryGetValue(key, out var value))
                sb.AppendLine($"- {key}: `{value}`");
        }

        sb.AppendLine();
        sb.AppendLine("## Final Snapshot");
        AppendSnapshot(sb, _lastSnapshot);

        sb.AppendLine();
        sb.AppendLine("## Stage Timeline");
        sb.AppendLine();
        sb.AppendLine("| # | Stage | Status | ms | Chunks | Tiles | POI | Zones | Territories | Specials |");
        sb.AppendLine("|---:|---|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var stage in _stages)
        {
            var after = stage.After ?? stage.Before;
            sb.AppendLine($"| {stage.Index} | {EscapeMd(stage.Name)} | {stage.Status} | {stage.ElapsedMs} | {after.ChunkCount} | {after.TileCount} | {after.PoiCount} | {after.ZoneCount} | {after.TerritoryCount} | {after.SpecialCharacterCount} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Stage Details");
        foreach (var stage in _stages)
        {
            sb.AppendLine();
            sb.AppendLine($"### {stage.Index}. {stage.Name}");
            sb.AppendLine();
            sb.AppendLine($"- Status: `{stage.Status}`");
            sb.AppendLine($"- Elapsed: `{stage.ElapsedMs} ms`");
            sb.AppendLine($"- Delta: chunks `{stage.Delta?.ChunkDelta ?? 0}`, tiles `{stage.Delta?.TileDelta ?? 0}`, poi `{stage.Delta?.PoiDelta ?? 0}`, zones `{stage.Delta?.ZoneDelta ?? 0}`, territories `{stage.Delta?.TerritoryDelta ?? 0}`, specials `{stage.Delta?.SpecialCharacterDelta ?? 0}`");
            if (!string.IsNullOrWhiteSpace(stage.ExceptionMessage))
                sb.AppendLine($"- Exception: `{stage.ExceptionType}: {stage.ExceptionMessage}`");
            AppendSnapshot(sb, stage.After ?? stage.Before);
        }

        if (!string.IsNullOrWhiteSpace(ExceptionStackTrace))
        {
            sb.AppendLine();
            sb.AppendLine("## Exception Stack");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine(ExceptionStackTrace);
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private static void AppendSnapshot(StringBuilder sb, WorldSnapshot snapshot)
    {
        sb.AppendLine();
        sb.AppendLine($"- Chunks: `{snapshot.ChunkCount}` generated `{snapshot.GeneratedChunkCount}` active `{snapshot.ActiveChunkCount}`");
        sb.AppendLine($"- Tiles: `{snapshot.TileCount}` passable `{snapshot.PassableTileCount}` settlements `{snapshot.SettlementTileCount}` POI centers `{snapshot.PoiCenterTileCount}`");
        sb.AppendLine($"- Roads/Rivers: road tiles `{snapshot.RoadTileCount}`, river tiles `{snapshot.RiverTileCount}`, bridge tiles `{snapshot.BridgeTileCount}`");
        sb.AppendLine($"- POI: `{snapshot.PoiCount}` ports `{snapshot.PortPoiCount}` with parent `{snapshot.ChildPoiCount}`");
        sb.AppendLine($"- Zones/Territories/Specials: zones `{snapshot.ZoneCount}`, territories `{snapshot.TerritoryCount}`, specials `{snapshot.SpecialCharacterCount}`");
        sb.AppendLine($"- Encounters: total `{snapshot.EncounterSlotCount}`, available `{snapshot.AvailableEncounterSlotCount}`, triggered `{snapshot.TriggeredEncounterSlotCount}`");
        AppendTopMap(sb, "Terrain top", snapshot.TerrainCounts);
        AppendTopMap(sb, "Biome top", snapshot.BiomeCounts);
        AppendTopMap(sb, "POI type top", snapshot.PoiTypeCounts);
        AppendTopMap(sb, "POI faction top", snapshot.PoiFactionCounts);
        AppendTopMap(sb, "Special type top", snapshot.SpecialCharacterTypeCounts);
        AppendTopMap(sb, "Territory sizes top", snapshot.TerritoryTileCounts);
    }

    private static void AppendTopMap(StringBuilder sb, string title, IReadOnlyDictionary<string, int> values)
    {
        if (values.Count == 0)
            return;

        string joined = string.Join(", ", values
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(kv => $"{kv.Key}={kv.Value}"));
        sb.AppendLine($"- {title}: `{joined}`");
    }

    private static string EscapeMd(string value) => value.Replace("|", "\\|");

    public sealed class StageEntry
    {
        internal ulong StartTicks { get; set; }
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "running";
        public float ProgressWeight { get; set; }
        public float ProgressAtStart { get; set; }
        public string StartedAt { get; set; } = "";
        public string EndedAt { get; set; } = "";
        public long ElapsedMs { get; set; }
        public WorldSnapshot Before { get; set; } = WorldSnapshot.Empty();
        public WorldSnapshot? After { get; set; }
        public WorldSnapshotDelta? Delta { get; set; }
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? ExceptionStackTrace { get; set; }
    }
}

public sealed class WorldSnapshot
{
    public int ChunkCount { get; set; }
    public int GeneratedChunkCount { get; set; }
    public int ActiveChunkCount { get; set; }
    public int TileCount { get; set; }
    public int PassableTileCount { get; set; }
    public int RoadTileCount { get; set; }
    public int RiverTileCount { get; set; }
    public int BridgeTileCount { get; set; }
    public int SettlementTileCount { get; set; }
    public int PoiCenterTileCount { get; set; }
    public int EncounterSlotCount { get; set; }
    public int AvailableEncounterSlotCount { get; set; }
    public int TriggeredEncounterSlotCount { get; set; }
    public int ZoneCount { get; set; }
    public int TerritoryCount { get; set; }
    public int TerritoryTileCount { get; set; }
    public int PoiCount { get; set; }
    public int PortPoiCount { get; set; }
    public int ChildPoiCount { get; set; }
    public int SpecialCharacterCount { get; set; }
    public Dictionary<string, int> TerrainCounts { get; set; } = new();
    public Dictionary<string, int> BiomeCounts { get; set; } = new();
    public Dictionary<string, int> PoiTypeCounts { get; set; } = new();
    public Dictionary<string, int> PoiFactionCounts { get; set; } = new();
    public Dictionary<string, int> SpecialCharacterTypeCounts { get; set; } = new();
    public Dictionary<string, int> TerritoryTileCounts { get; set; } = new();
    public List<ZoneSummary> LargestZones { get; set; } = new();
    public List<PoiSummary> SamplePois { get; set; } = new();

    public static WorldSnapshot Empty() => new();

    public static WorldSnapshot Capture(WorldBuildContext ctx)
    {
        var snapshot = new WorldSnapshot
        {
            ChunkCount = ctx.Chunks.Count,
            GeneratedChunkCount = ctx.Chunks.Values.Count(c => c.IsGenerated),
            ActiveChunkCount = ctx.Chunks.Values.Count(c => c.IsActive),
            ZoneCount = ctx.Zones.Count,
            TerritoryCount = ctx.Territories.Count,
            PoiCount = ctx.Pois.Count,
            SpecialCharacterCount = ctx.SpecialCharacters.Count,
        };

        foreach (var chunk in ctx.Chunks.Values)
        {
            snapshot.TileCount += chunk.Tiles.Count;
            snapshot.EncounterSlotCount += chunk.EncounterSlots.Count;
            foreach (var state in chunk.EncounterSlots.Values)
            {
                if (state == EncounterSlotState.Available)
                    snapshot.AvailableEncounterSlotCount++;
                else if (state == EncounterSlotState.Triggered)
                    snapshot.TriggeredEncounterSlotCount++;
            }

            foreach (var tile in chunk.Tiles.Values)
            {
                if (tile.IsPassable)
                    snapshot.PassableTileCount++;
                if (tile.IsRoad)
                    snapshot.RoadTileCount++;
                if (tile.IsRiver)
                    snapshot.RiverTileCount++;
                if (tile.IsBridge)
                    snapshot.BridgeTileCount++;
                if (tile.HasSettlement)
                    snapshot.SettlementTileCount++;
                if (tile.IsPoiCenter)
                    snapshot.PoiCenterTileCount++;

                Increment(snapshot.TerrainCounts, tile.Terrain.ToString());
                Increment(snapshot.BiomeCounts, TerrainToBiome.Map(tile.Terrain).ToString());
            }
        }

        foreach (var poi in ctx.Pois)
        {
            Increment(snapshot.PoiTypeCounts, poi.PoiTypeEnum.ToString());
            Increment(snapshot.PoiFactionCounts, string.IsNullOrWhiteSpace(poi.OwningFaction) ? "(empty)" : poi.OwningFaction);
            if (poi.IsPortCity)
                snapshot.PortPoiCount++;
            if (!string.IsNullOrWhiteSpace(poi.ParentPoiName))
                snapshot.ChildPoiCount++;
        }

        foreach (var entity in ctx.SpecialCharacters)
        {
            Increment(snapshot.SpecialCharacterTypeCounts, entity.EntityTypeEnum.ToString());
        }

        foreach (var (nationId, territory) in ctx.Territories)
        {
            int count = territory.AllTiles.Count;
            snapshot.TerritoryTileCount += count;
            snapshot.TerritoryTileCounts[nationId] = count;
        }

        snapshot.LargestZones = ctx.Zones
            .OrderByDescending(z => z.TileCount)
            .Take(16)
            .Select(z => new ZoneSummary
            {
                Id = z.Id,
                Biome = z.DominantBiome.ToString(),
                Tiles = z.TileCount,
                CentroidQ = z.Centroid.X,
                CentroidR = z.Centroid.Y,
                OwnerNationId = z.OwnerNationId,
                AverageElevation = z.AverageElevation,
                AverageTemperature = z.AverageTemperature,
                AverageMoisture = z.AverageMoisture,
            })
            .ToList();

        snapshot.SamplePois = ctx.Pois
            .Take(24)
            .Select(p => new PoiSummary
            {
                Name = p.PoiName,
                Type = p.PoiTypeEnum.ToString(),
                Faction = p.OwningFaction,
                CenterQ = p.CenterHex.X,
                CenterR = p.CenterHex.Y,
                IsPort = p.IsPortCity,
                Parent = p.ParentPoiName,
                Footprint = p.FootprintTemplateName,
            })
            .ToList();

        Sort(snapshot.TerrainCounts);
        Sort(snapshot.BiomeCounts);
        Sort(snapshot.PoiTypeCounts);
        Sort(snapshot.PoiFactionCounts);
        Sort(snapshot.SpecialCharacterTypeCounts);
        Sort(snapshot.TerritoryTileCounts);

        return snapshot;
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int current);
        counts[key] = current + 1;
    }

    private static void Sort(Dictionary<string, int> counts)
    {
        var sorted = counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToArray();
        counts.Clear();
        foreach (var (key, value) in sorted)
            counts[key] = value;
    }
}

public sealed class WorldSnapshotDelta
{
    public int ChunkDelta { get; set; }
    public int TileDelta { get; set; }
    public int PoiDelta { get; set; }
    public int ZoneDelta { get; set; }
    public int TerritoryDelta { get; set; }
    public int SpecialCharacterDelta { get; set; }
    public int RoadTileDelta { get; set; }
    public int RiverTileDelta { get; set; }
    public int EncounterSlotDelta { get; set; }

    public static WorldSnapshotDelta From(WorldSnapshot before, WorldSnapshot after) => new()
    {
        ChunkDelta = after.ChunkCount - before.ChunkCount,
        TileDelta = after.TileCount - before.TileCount,
        PoiDelta = after.PoiCount - before.PoiCount,
        ZoneDelta = after.ZoneCount - before.ZoneCount,
        TerritoryDelta = after.TerritoryCount - before.TerritoryCount,
        SpecialCharacterDelta = after.SpecialCharacterCount - before.SpecialCharacterCount,
        RoadTileDelta = after.RoadTileCount - before.RoadTileCount,
        RiverTileDelta = after.RiverTileCount - before.RiverTileCount,
        EncounterSlotDelta = after.EncounterSlotCount - before.EncounterSlotCount,
    };
}

public sealed class ZoneSummary
{
    public int Id { get; set; }
    public string Biome { get; set; } = "";
    public int Tiles { get; set; }
    public int CentroidQ { get; set; }
    public int CentroidR { get; set; }
    public string OwnerNationId { get; set; } = "";
    public float AverageElevation { get; set; }
    public float AverageTemperature { get; set; }
    public float AverageMoisture { get; set; }
}

public sealed class PoiSummary
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Faction { get; set; } = "";
    public int CenterQ { get; set; }
    public int CenterR { get; set; }
    public bool IsPort { get; set; }
    public string Parent { get; set; } = "";
    public string Footprint { get; set; } = "";
}
