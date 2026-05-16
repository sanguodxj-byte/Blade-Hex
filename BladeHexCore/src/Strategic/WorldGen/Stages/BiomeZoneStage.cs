// BiomeZoneStage.cs
// 世界生成阶段 3：生态区聚类。
//
// 抽取自 WorldCreator.CreateWorld 中 Stage 4 的 BiomeZoneAnalyzer 调用。
using BladeHex.Map;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 3：将相邻的同生态瓦片聚类成 BiomeZone，供国家版图分配使用。
/// </summary>
public sealed class BiomeZoneStage : IWorldStage
{
    public string Name => "分析生态区";
    public float ProgressWeight => 5f;

    public void Execute(WorldBuildContext ctx)
    {
        var analyzer = new BiomeZoneAnalyzer { MinZoneSize = ctx.Config.MinBiomeZoneSize };
        ctx.Zones = analyzer.Analyze(ctx.Chunks);
    }
}
