// SpecialCharacterStage.cs
// 世界生成阶段 11：生成具名特殊角色（领主 + 冒险者）。
//
// 抽取自 WorldCreator.CreateWorld 中 Stage 7.8 的 SpecialCharacterGenerator 调用。
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 11：调用 SpecialCharacterGenerator 生成领主 + 冒险者，写入 ctx.SpecialCharacters。
/// </summary>
public sealed class SpecialCharacterStage : IWorldStage
{
    public string Name => "召唤英雄与领主";
    public float ProgressWeight => 3f;

    public void Execute(WorldBuildContext ctx)
    {
        var gen = new SpecialCharacterGenerator(ctx.Seed);
        ctx.SpecialCharacters = gen.GenerateAll(
            ctx.Config.Nations,
            ctx.Territories,
            ctx.Pois,
            ctx.Config.WorldTileWidth * ctx.Config.WorldTileHeight);

        GD.Print($"[SpecialCharacterStage] {ctx.SpecialCharacters.Count} 个特殊角色");
    }
}
