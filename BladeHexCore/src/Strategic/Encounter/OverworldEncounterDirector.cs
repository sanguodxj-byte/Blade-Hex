using Godot;
using System;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// Decides encounter activation budgets for newly active overworld chunks.
/// </summary>
public sealed class OverworldEncounterDirector
{
    public const float CivilizedPoiSafetyRadiusPx = 1800.0f;

    public ChunkEncounterPlan BuildChunkPlan(
        ChunkData chunk,
        float dangerLevel,
        OverworldSimulationContext ctx,
        int remainingBatchSpawns,
        int remainingTotalSpawns)
    {
        float density = Math.Clamp(chunk.EncounterDensity, 0.0f, 1.0f);
        float clampedDanger = Math.Clamp(dangerLevel, 0.0f, 1.0f) * density;
        bool suppressedByCivilizedPoi = IsWithinCivilizedSafetyRadius(chunk.GetCenterPixel(), ctx);

        int perChunkCap = Math.Max(0, ctx.EncounterSpawner.MaxChunkSlotSpawnsPerChunk);
        if (suppressedByCivilizedPoi || clampedDanger <= 0.05f)
            perChunkCap = 0;

        int budget = Math.Min(
            perChunkCap,
            Math.Min(Math.Max(0, remainingBatchSpawns), Math.Max(0, remainingTotalSpawns)));

        return new ChunkEncounterPlan(clampedDanger, budget, suppressedByCivilizedPoi);
    }

    private static bool IsWithinCivilizedSafetyRadius(Vector2 chunkCenter, OverworldSimulationContext ctx)
    {
        foreach (var poi in ctx.Pois)
        {
            if (!IsCivilizedPoi(poi))
                continue;

            if (chunkCenter.DistanceTo(poi.Position) <= CivilizedPoiSafetyRadiusPx)
                return true;
        }

        return false;
    }

    private static bool IsCivilizedPoi(OverworldPOI poi)
    {
        return poi.PoiTypeEnum is OverworldPOI.POIType.Town
            or OverworldPOI.POIType.Village
            or OverworldPOI.POIType.Castle
            or OverworldPOI.POIType.Mine
            or OverworldPOI.POIType.Farm;
    }
}

public readonly struct ChunkEncounterPlan
{
    public ChunkEncounterPlan(float dangerLevel, int maxWildMonsterSpawns, bool suppressedByCivilizedPoi)
    {
        DangerLevel = dangerLevel;
        MaxWildMonsterSpawns = maxWildMonsterSpawns;
        SuppressedByCivilizedPoi = suppressedByCivilizedPoi;
    }

    public float DangerLevel { get; }
    public int MaxWildMonsterSpawns { get; }
    public bool SuppressedByCivilizedPoi { get; }
}
