// NationAllocationStage.cs
// 世界生成阶段 4：国家版图分配。
//
// 抽取自 WorldCreator.CreateWorld 中 Stage 5 的 NationAllocator 调用 + worldScale 计算。
using System;
using BladeHex.Map;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 4：根据生态区为每个国家分配领土。
/// 同时按世界尺寸缩放 MinTerritoryTiles（小世界降低要求）。
/// </summary>
public sealed class NationAllocationStage : IWorldStage
{
    public string Name => "分配国家领土";
    public float ProgressWeight => 5f;

    public void Execute(WorldBuildContext ctx)
    {
        // 根据世界大小缩放 MinTerritoryTiles（与原 WorldCreator 一致）
        float worldScale = (float)(ctx.Config.WorldTileWidth * ctx.Config.WorldTileHeight) / (64 * 16 * 48 * 16);
        foreach (var nation in ctx.Config.Nations)
        {
            nation.MinTerritoryTiles = Math.Max(30, (int)(nation.MinTerritoryTiles * worldScale));
        }

        var allocator = new NationAllocator();
        ctx.Territories = allocator.AllocateTerritories(ctx.Zones, ctx.Config.Nations, ctx.Seed);
    }
}
