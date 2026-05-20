// WorldPipeline.cs
// 世界生成 Pipeline 协调者 — 服务于架构优化 spec R3。
//
// 接管原 WorldCreator.CreateWorld 的串联职责，把每个阶段委托给独立的 IWorldStage。
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Strategic.WorldGen;

/// <summary>
/// 世界生成 Pipeline — 按顺序运行 IWorldStage 列表，最终输出 WorldData。
/// </summary>
public sealed class WorldPipeline
{
    private readonly IReadOnlyList<IWorldStage> _stages;

    public WorldPipeline(IReadOnlyList<IWorldStage> stages)
    {
        _stages = stages ?? throw new ArgumentNullException(nameof(stages));
    }

    /// <summary>
    /// 默认 Pipeline — 与原 WorldCreator.CreateWorld 等价的 Stage 顺序。
    /// </summary>
    public static WorldPipeline Default() => new(new IWorldStage[]
    {
        new Stages.TerrainStage(),
        new Stages.TerrainSmoothingStage(),
        new Stages.MountainDepthStage(),
        new Stages.BiomeZoneStage(),
        new Stages.NationAllocationStage(),
        new Stages.RiverStage(),
        new Stages.IslandStage(),
        new Stages.POIStage(),
        new Stages.IslandPOIStage(),
        new Stages.FerryRouteStage(),
        new Stages.RoadStage(),
        new Stages.SpecialCharacterStage(),
        new Stages.EncounterDensityStage(),
    });

    /// <summary>运行 Pipeline，输出完整 <see cref="WorldData"/>。</summary>
    public WorldData Build(int seed, WorldCreationConfig config, Action<float, string>? onProgress = null)
    {
        var ctx = new WorldBuildContext(seed, config) { OnProgress = onProgress };

        float totalWeight = _stages.Sum(s => s.ProgressWeight);
        if (totalWeight <= 0f) totalWeight = 1f;

        float cumulative = 0f;
        var startTime = Time.GetTicksMsec();

        GD.Print($"[WorldPipeline] 开始: seed={seed}, {config.WorldChunksW}×{config.WorldChunksH} chunks, {_stages.Count} stages");

        foreach (var stage in _stages)
        {
            float pct = cumulative / totalWeight;
            ctx.OnProgress?.Invoke(pct, stage.Name);

            var stageStart = Time.GetTicksMsec();
            stage.Execute(ctx);
            var stageElapsed = Time.GetTicksMsec() - stageStart;

            cumulative += stage.ProgressWeight;
            GD.Print($"[WorldPipeline] {stage.Name} 完成 ({stageElapsed}ms)");
        }

        ctx.OnProgress?.Invoke(1f, "完成");
        var totalElapsed = Time.GetTicksMsec() - startTime;
        GD.Print($"[WorldPipeline] 完成: 总耗时 {totalElapsed}ms");

        return new WorldData
        {
            Seed = ctx.Seed,
            WorldChunksW = config.WorldChunksW,
            WorldChunksH = config.WorldChunksH,
            Chunks = ctx.Chunks,
            Pois = ctx.Pois,
            Skeleton = null,
            Zones = ctx.Zones,
            Territories = ctx.Territories,
            Nations = config.Nations,
            SpecialCharacters = ctx.SpecialCharacters,
        };
    }
}
