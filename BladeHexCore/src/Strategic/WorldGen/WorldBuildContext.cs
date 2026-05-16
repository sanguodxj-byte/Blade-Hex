// WorldBuildContext.cs
// 世界生成 Pipeline 的共享上下文 — 服务于架构优化 spec R3。
//
// 负责承载阶段间共享数据，避免 WorldCreator 中的私有字段（如 _islandCenters）
// 成为 Stage 之间的隐式耦合。
using System;
using System.Collections.Generic;
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen;

/// <summary>
/// 世界生成 Pipeline 的共享上下文。
/// 各 Stage 从中读取依赖并写入产出，最终由 <see cref="WorldPipeline"/> 组装为 <see cref="WorldData"/>。
/// </summary>
public sealed class WorldBuildContext
{
    public int Seed { get; }
    public WorldCreationConfig Config { get; }
    public Action<float, string>? OnProgress { get; set; }

    // ========================================
    // 各 stage 累积写入的中间结果
    // ========================================

    /// <summary>所有 chunk（按 chunkCoord 索引）。由 TerrainStage 写入，后续 Stage 读写。</summary>
    public Dictionary<Vector2I, ChunkData> Chunks { get; } = new();

    /// <summary>生态区聚类结果。由 BiomeZoneStage 写入。</summary>
    public List<BiomeZone> Zones { get; set; } = new();

    /// <summary>国家领土分配结果。由 NationAllocationStage 写入。</summary>
    public Dictionary<string, NationTerritory> Territories { get; set; } = new();

    /// <summary>POI 列表（城镇、村庄、巢穴等）。由 POI 系列 Stage 写入。</summary>
    public List<OverworldPOI> Pois { get; } = new();

    /// <summary>特殊角色（领主、冒险者）。由 SpecialCharacterStage 写入。</summary>
    public List<OverworldEntity> SpecialCharacters { get; set; } = new();

    /// <summary>
    /// 海岛中心坐标 — 由 IslandStage 写入，IslandPOIStage 读取。
    /// 原 WorldCreator._islandCenters 提升至此。
    /// </summary>
    public List<Vector2I> IslandCenters { get; } = new();

    /// <summary>
    /// 已放置的海港国家 ID 集合 — 由 POIStage 内部使用，跨阶段不需要共享。
    /// 仅当 Stage 内多次调用 PlaceNationPOI 时复用。
    /// </summary>
    public HashSet<string> PortsPlaced { get; } = new();

    public WorldBuildContext(int seed, WorldCreationConfig config)
    {
        Seed = seed;
        Config = config;
    }

    /// <summary>
    /// 派生一个新的 Random 实例。
    /// 使用魔数与 seed 异或，与原 WorldCreator 各阶段保持一致：
    /// <list type="bullet">
    ///   <item>0x52495645 = "RIVE" (RiverStage)</item>
    ///   <item>0x49534C44 = "ISLD" (IslandStage)</item>
    ///   <item>0x49504F49 = "IPOI" (IslandPOIStage)</item>
    ///   <item>0x504F49 = "POI" (POIStage)</item>
    /// </list>
    /// </summary>
    public Random NewRng(int magic) => new(Seed ^ magic);
}
