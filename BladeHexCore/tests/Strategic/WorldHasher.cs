// WorldHasher.cs
// 将 WorldData 序列化为确定性 SHA256 hash，用于 R3 (WorldPipeline) 重构的等价性回归测试。
//
// 核心约束：
//   1. 相同 seed → 相同 hash（跨平台、跨运行）
//   2. 浮点字段量化为 int（Elevation/Moisture/Temperature ×1000 取整）防止跨平台抖动
//   3. 字典/集合按 key 排序后写入，避免遍历顺序差异
//
// 用法：
//   var hash = WorldHasher.Hash(world);
//   Assert.Equal(expectedHash, hash);

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BladeHex.Map;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Tests.Strategic;

/// <summary>
/// 世界数据确定性哈希器 — 用于 golden seed 回归测试。
/// </summary>
public static class WorldHasher
{
    /// <summary>计算 WorldData 的 SHA256 hash（小写十六进制）。</summary>
    public static string Hash(WorldData world)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        // === Header ===
        w.Write("WorldData/v1");
        w.Write(world.Seed);
        w.Write(world.WorldChunksW);
        w.Write(world.WorldChunksH);

        // === Chunks（按 ChunkCoord 排序）===
        WriteChunks(w, world.Chunks);

        // === POIs（按 Position + Name 排序）===
        WritePois(w, world.Pois);

        // === Territories（按 NationId 排序）===
        WriteTerritories(w, world.Territories);

        // === SpecialCharacters（按引用稳定排序）===
        WriteSpecialCharacters(w, world.SpecialCharacters);

        w.Flush();
        ms.Position = 0;

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(ms);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// 详细 dump：返回每段独立 hash + 总 hash。
    /// 用于定位具体哪段差异（地形 / POI / 领土 / 特殊角色）。
    /// </summary>
    public static WorldHashBreakdown HashBreakdown(WorldData world)
    {
        return new WorldHashBreakdown
        {
            Seed = world.Seed,
            ChunksHash = HashSection(w => WriteChunks(w, world.Chunks)),
            PoisHash = HashSection(w => WritePois(w, world.Pois)),
            TerritoriesHash = HashSection(w => WriteTerritories(w, world.Territories)),
            SpecialCharactersHash = HashSection(w => WriteSpecialCharacters(w, world.SpecialCharacters)),
            TotalHash = Hash(world),
        };
    }

    // ========================================
    // 各段写入
    // ========================================

    private static void WriteChunks(BinaryWriter w, Dictionary<Vector2I, ChunkData> chunks)
    {
        w.Write("chunks:");
        w.Write(chunks.Count);

        var orderedChunks = chunks.OrderBy(kv => kv.Key.X).ThenBy(kv => kv.Key.Y);
        foreach (var (coord, chunk) in orderedChunks)
        {
            w.Write(coord.X);
            w.Write(coord.Y);
            w.Write(chunk.RegionName ?? "");
            w.Write(chunk.Tiles.Count);

            var orderedTiles = chunk.Tiles.OrderBy(t => t.Key.X).ThenBy(t => t.Key.Y);
            foreach (var (tileCoord, tile) in orderedTiles)
            {
                w.Write(tileCoord.X);
                w.Write(tileCoord.Y);
                w.Write((int)tile.Terrain);
                w.Write(QuantizeFloat(tile.Elevation));
                w.Write(QuantizeFloat(tile.Moisture));
                w.Write(QuantizeFloat(tile.Temperature));
                w.Write(tile.IsRoad);
                w.Write(tile.IsRiver);
                w.Write(tile.RoadDirections);
                w.Write(tile.RiverDirections);
                w.Write(tile.HasSettlement);
                w.Write(tile.SettlementType);
                w.Write(tile.PoiId ?? "");
            }

            w.Write(chunk.EncounterSlots.Count);
            var orderedEnc = chunk.EncounterSlots.OrderBy(e => e.Key.X).ThenBy(e => e.Key.Y);
            foreach (var (encCoord, state) in orderedEnc)
            {
                w.Write(encCoord.X);
                w.Write(encCoord.Y);
                w.Write((byte)state);
            }
        }
    }

    private static void WritePois(BinaryWriter w, List<OverworldPOI> pois)
    {
        w.Write("pois:");
        w.Write(pois.Count);

        var ordered = pois
            .OrderBy(p => p.Position.X)
            .ThenBy(p => p.Position.Y)
            .ThenBy(p => p.PoiName ?? "");

        foreach (var poi in ordered)
        {
            w.Write(poi.PoiName ?? "");
            w.Write((int)poi.PoiTypeEnum);
            w.Write(QuantizeFloat(poi.Position.X));
            w.Write(QuantizeFloat(poi.Position.Y));
            w.Write(poi.OwningFaction ?? "");
            w.Write(poi.Prosperity);
            w.Write((int)poi.SettlementRaceValue);
            w.Write(QuantizeFloat(poi.ThreatLevel));
            w.Write(poi.RaidIntervalDays);
            w.Write(poi.MaxRaidingParties);
            w.Write((int)poi.LairTypeValue);
            w.Write(poi.LairLevel);
            w.Write(poi.IsCleared);
            w.Write(poi.HasTavern);
            w.Write(poi.HasShop);
            w.Write(poi.HasBlacksmith);
            w.Write(poi.HasQuestBoard);
            w.Write(poi.HasBarracks);
            w.Write(poi.CastleDefenseLevel);
            w.Write(poi.GarrisonMax);
            w.Write(poi.GarrisonCurrent);
            w.Write((int)poi.LordPersonalityValue);
            w.Write(poi.FerryCost);
            w.Write(poi.HasShipyard);

            // 渡船目的地（按字符串排序）
            var ferryDests = (poi.FerryDestinations ?? new List<string>()).OrderBy(s => s ?? "");
            w.Write(poi.FerryDestinations?.Count ?? 0);
            foreach (var dest in ferryDests) w.Write(dest ?? "");
        }
    }

    private static void WriteTerritories(BinaryWriter w, Dictionary<string, NationTerritory> territories)
    {
        w.Write("territories:");
        w.Write(territories.Count);

        var ordered = territories.OrderBy(kv => kv.Key);
        foreach (var (id, t) in ordered)
        {
            w.Write(id);
            w.Write(t.NationId ?? "");
            w.Write(t.AllTiles.Count);

            var orderedTiles = t.AllTiles.OrderBy(c => c.X).ThenBy(c => c.Y);
            foreach (var coord in orderedTiles)
            {
                w.Write(coord.X);
                w.Write(coord.Y);
            }

            w.Write(t.Zones?.Count ?? 0);
            // BiomeZone 内容暂不深入（已通过 AllTiles 间接覆盖）
        }
    }

    private static void WriteSpecialCharacters(BinaryWriter w, List<OverworldEntity> chars)
    {
        w.Write("special_chars:");
        w.Write(chars.Count);

        // 按 (Position, Name) 排序保证确定性
        var ordered = chars
            .OrderBy(c => QuantizeFloat(c.Position.X))
            .ThenBy(c => QuantizeFloat(c.Position.Y))
            .ThenBy(c => c.EntityName ?? "");

        foreach (var c in ordered)
        {
            w.Write(c.EntityName ?? "");
            w.Write((int)c.EntityTypeEnum);
            w.Write(QuantizeFloat(c.Position.X));
            w.Write(QuantizeFloat(c.Position.Y));
            w.Write(c.Faction ?? "");
            w.Write(c.IsNamedCharacter);
            w.Write(c.CharacterTitle ?? "");
            w.Write(c.FamilyName ?? "");
            w.Write(c.BoundPoiName ?? "");
            w.Write(c.PartyLevel);
            w.Write(QuantizeFloat(c.CombatPower));
        }
    }

    // ========================================
    // 工具
    // ========================================

    /// <summary>浮点量化为 int（×1000 取整），消除跨平台浮点抖动。</summary>
    private static int QuantizeFloat(float f)
    {
        if (float.IsNaN(f)) return int.MinValue;
        if (float.IsInfinity(f)) return f > 0 ? int.MaxValue : int.MinValue + 1;
        return (int)Math.Round(f * 1000.0);
    }

    private static string HashSection(Action<BinaryWriter> writer)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        writer(w);
        w.Flush();
        ms.Position = 0;
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(ms);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>世界 hash 分段结果，便于诊断哪段差异。</summary>
public sealed class WorldHashBreakdown
{
    public int Seed { get; set; }
    public string ChunksHash { get; set; } = "";
    public string PoisHash { get; set; } = "";
    public string TerritoriesHash { get; set; } = "";
    public string SpecialCharactersHash { get; set; } = "";
    public string TotalHash { get; set; } = "";

    public override string ToString()
    {
        return $"Seed={Seed}\n" +
               $"  Chunks       : {ChunksHash}\n" +
               $"  POIs         : {PoisHash}\n" +
               $"  Territories  : {TerritoriesHash}\n" +
               $"  SpecialChars : {SpecialCharactersHash}\n" +
               $"  TOTAL        : {TotalHash}";
    }
}
