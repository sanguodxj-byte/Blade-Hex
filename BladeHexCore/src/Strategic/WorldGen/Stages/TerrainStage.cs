// TerrainStage.cs
// 世界生成阶段 1：使用 ChunkGenerator 为所有 chunk 生成基础地形。
//
// 抽取自 WorldCreator.GenerateAllTerrain。
// RNG：无（FastNoiseLite 噪声基于 WorldSeed 内部派生，无 Random 依赖）。
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 1：使用 ChunkGenerator 为所有 chunk 生成地形数据。
/// </summary>
public sealed class TerrainStage : IWorldStage
{
    public string Name => "生成地形";
    public float ProgressWeight => 30f;

    public void Execute(WorldBuildContext ctx)
    {
        var generator = new ChunkGenerator();
        // 根据 ChunksW 反推世界尺寸，决定模板网格
        var (gridW, gridH) = InferTemplateGrid(ctx.Config.WorldChunksW);
        generator.Initialize(ctx.Seed, ctx.Config.WorldTileWidth, ctx.Config.WorldTileHeight, gridW, gridH);

        int total = ctx.Config.WorldChunksW * ctx.Config.WorldChunksH;
        int count = 0;

        for (int cq = 0; cq < ctx.Config.WorldChunksW; cq++)
        {
            for (int cr = 0; cr < ctx.Config.WorldChunksH; cr++)
            {
                ctx.Chunks[new Vector2I(cq, cr)] = generator.Generate(cq, cr);
                count++;
                if (count % 100 == 0)
                    ctx.OnProgress?.Invoke(0.4f * count / total, $"生成地形 ({count}/{total})...");
            }
        }
    }

    /// <summary>从 ChunksW 反推世界尺寸 → 模板网格维度</summary>
    private static (int gridW, int gridH) InferTemplateGrid(int chunksW) => chunksW switch
    {
        <= 21 => (1, 1),  // Small (21×12)
        <= 36 => (2, 2),  // Medium (36×24)
        <= 56 => (3, 3),  // Large (56×32)
        _     => (4, 4),  // Mega (80×56)
    };
}
