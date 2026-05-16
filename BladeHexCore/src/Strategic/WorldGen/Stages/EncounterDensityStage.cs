// EncounterDensityStage.cs
// 世界生成阶段 12：遭遇密度预计算。
//
// 抽取自 WorldCreator.PrecomputeEncounterDensity。
//
// 注：当前原实现内部仅做距离归一化计算但**未将结果写回 chunk**（无副作用），
// 实际遭遇生成在运行时由 EncounterSpawner 处理。本 Stage 保留计算骨架以维持等价性，
// 待后续与 EncounterSpawner 集成时再赋实际意义。
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 12：遭遇密度预计算（当前为占位实现）。
/// </summary>
public sealed class EncounterDensityStage : IWorldStage
{
    public string Name => "计算遭遇分布";
    public float ProgressWeight => 1f;

    public void Execute(WorldBuildContext ctx)
    {
        var nationCenters = new System.Collections.Generic.Dictionary<string, Vector2I>();
        foreach (var (id, territory) in ctx.Territories)
            nationCenters[id] = territory.CoreZone.Centroid;

        foreach (var (coord, chunk) in ctx.Chunks)
        {
            var chunkCenter = ChunkData.ChunkToWorld(coord.X, coord.Y);
            chunkCenter += new Vector2I(ChunkData.ChunkSize / 2, ChunkData.ChunkSize / 2);

            float minDist = float.MaxValue;
            foreach (var center in nationCenters.Values)
            {
                float d = chunkCenter.DistanceTo(center);
                if (d < minDist) minDist = d;
            }

            float maxDist = Mathf.Sqrt(ctx.Chunks.Count) * ChunkData.ChunkSize;
            float distFactor = Mathf.Clamp(minDist / maxDist, 0.0f, 1.0f);
            // 当前不写回 chunk —— 与原实现一致
            _ = distFactor;
        }
    }
}
