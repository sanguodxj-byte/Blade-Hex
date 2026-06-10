// WorldPipeline.cs
// 世界生成 Pipeline 协调者 — 服务于架构优化 spec R3。
//
// 接管原 WorldCreator.CreateWorld 的串联职责，把每个阶段委托给独立的 IWorldStage。
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Diagnostics;

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
        var report = new WorldGenerationReport(seed, config, _stages);

        float totalWeight = _stages.Sum(s => s.ProgressWeight);
        if (totalWeight <= 0f) totalWeight = 1f;

        float cumulative = 0f;
        var startTime = Time.GetTicksMsec();

        try
        {
            string startMessage = $"[WorldPipeline] 开始: seed={seed}, {config.WorldChunksW}×{config.WorldChunksH} chunks, {_stages.Count} stages";
            GD.Print(startMessage);
            DiagnosticLog.Event("WorldPipeline", "start", new Dictionary<string, object?>
            {
                ["seed"] = seed,
                ["chunks"] = $"{config.WorldChunksW}x{config.WorldChunksH}",
                ["tiles"] = $"{config.WorldTileWidth}x{config.WorldTileHeight}",
                ["stage_count"] = _stages.Count,
                ["nations"] = config.Nations.Count,
                ["report_id"] = report.ReportId,
            });

            foreach (var stage in _stages)
            {
                float pct = cumulative / totalWeight;
                ctx.OnProgress?.Invoke(pct, stage.Name);

                var stageStart = Time.GetTicksMsec();
                var stageReport = report.BeginStage(stage, ctx, pct);
                DiagnosticLog.Event("WorldPipeline", "stage_start", new Dictionary<string, object?>
                {
                    ["name"] = stage.Name,
                    ["progress"] = pct.ToString("P0"),
                    ["chunks"] = ctx.Chunks.Count,
                    ["pois"] = ctx.Pois.Count,
                    ["zones"] = ctx.Zones.Count,
                    ["territories"] = ctx.Territories.Count,
                    ["specials"] = ctx.SpecialCharacters.Count,
                    ["report_id"] = report.ReportId,
                });

                try
                {
                    stage.Execute(ctx);
                    report.EndStage(stageReport, ctx);
                }
                catch (Exception ex)
                {
                    report.FailStage(stageReport, ctx, ex);
                    DiagnosticLog.Exception($"WorldPipeline stage failed: {stage.Name}", ex);
                    throw;
                }

                var stageElapsed = Time.GetTicksMsec() - stageStart;

                cumulative += stage.ProgressWeight;
                GD.Print($"[WorldPipeline] {stage.Name} 完成 ({stageElapsed}ms)");
                DiagnosticLog.Event("WorldPipeline", "stage_end", new Dictionary<string, object?>
                {
                    ["name"] = stage.Name,
                    ["elapsed_ms"] = stageElapsed,
                    ["chunks"] = ctx.Chunks.Count,
                    ["pois"] = ctx.Pois.Count,
                    ["zones"] = ctx.Zones.Count,
                    ["territories"] = ctx.Territories.Count,
                    ["specials"] = ctx.SpecialCharacters.Count,
                    ["report_id"] = report.ReportId,
                });
            }

            ctx.OnProgress?.Invoke(1f, "完成");
            var totalElapsed = Time.GetTicksMsec() - startTime;
            GD.Print($"[WorldPipeline] 完成: 总耗时 {totalElapsed}ms");
            DiagnosticLog.Event("WorldPipeline", "complete", new Dictionary<string, object?>
            {
                ["elapsed_ms"] = totalElapsed,
                ["chunks"] = ctx.Chunks.Count,
                ["pois"] = ctx.Pois.Count,
                ["zones"] = ctx.Zones.Count,
                ["territories"] = ctx.Territories.Count,
                ["specials"] = ctx.SpecialCharacters.Count,
                ["report_id"] = report.ReportId,
            });

            report.Complete(ctx);

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
        catch (Exception ex)
        {
            report.Fail(ex);
            DiagnosticLog.Event("WorldPipeline", "failed", new Dictionary<string, object?>
            {
                ["seed"] = seed,
                ["report_id"] = report.ReportId,
                ["exception"] = ex.GetType().Name,
                ["message"] = ex.Message,
            });
            throw;
        }
        finally
        {
            var paths = report.FinishAndWrite();
            if (paths.Count > 0)
            {
                DiagnosticLog.Event("WorldPipeline", "report_written", new Dictionary<string, object?>
                {
                    ["report_id"] = report.ReportId,
                    ["level"] = paths.GetValueOrDefault("level"),
                    ["json"] = paths.GetValueOrDefault("json"),
                    ["markdown"] = paths.GetValueOrDefault("markdown"),
                });
            }
        }
    }
}
