// EncounterDensityStage.cs
// 世界生成阶段 12：遭遇密度预计算。
//
// 抽取自 WorldCreator.PrecomputeEncounterDensity。
//
// 将距离国家核心区的归一化结果写回 ChunkData.EncounterDensity，
// 供运行时 EncounterSpawner/OverworldEncounterDirector 调整激活遭遇预算。
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 12：遭遇密度预计算。
/// </summary>
public sealed class EncounterDensityStage : IWorldStage
{
    public string Name => "计算遭遇分布";
    public float ProgressWeight => 1f;

    public void Execute(WorldBuildContext ctx)
    {
        var nationCenters = new System.Collections.Generic.List<Vector2I>();
        foreach (var territory in ctx.Territories.Values)
            nationCenters.Add(territory.CoreZone.Centroid);

        if (nationCenters.Count == 0)
        {
            foreach (var chunk in ctx.Chunks.Values)
                chunk.EncounterDensity = 1.0f;
            return;
        }

        float maxDist = Mathf.Max(1.0f, Mathf.Sqrt(ctx.Chunks.Count) * ChunkData.ChunkSize);
        foreach (var (coord, chunk) in ctx.Chunks)
        {
            var chunkCenter = ChunkData.ChunkToWorld(coord.X, coord.Y);
            chunkCenter += new Vector2I(ChunkData.ChunkSize / 2, ChunkData.ChunkSize / 2);

            float minDist = float.MaxValue;
            foreach (var center in nationCenters)
            {
                float d = chunkCenter.DistanceTo(center);
                if (d < minDist) minDist = d;
            }

            chunk.EncounterDensity = Mathf.Clamp(minDist / maxDist, 0.05f, 1.0f);
        }
    }
}
