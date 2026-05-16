// IWorldStage.cs
// 世界生成阶段接口 — 服务于架构优化 spec R3。
//
// 每个 IWorldStage 实现负责一个独立的生成阶段（地形、河流、POI 等），
// 通过 WorldBuildContext 共享中间结果，由 WorldPipeline 串联调度。
//
// 约束：
//   1. Stage 必须是确定性的：相同 ctx + 相同 seed → 相同输出
//   2. 不同 Stage 的 RNG 必须隔离（用 ctx.NewRng(magic) 派生）
//   3. Stage 之间通过 ctx 共享状态，禁止持有可变私有字段（除非是无副作用的缓存）
namespace BladeHex.Strategic.WorldGen;

/// <summary>
/// 世界生成 Pipeline 中的一个阶段。
/// </summary>
public interface IWorldStage
{
    /// <summary>阶段名称（用于进度回调和日志）。</summary>
    string Name { get; }

    /// <summary>
    /// 进度权重 — 用于 OnProgress 计算累计百分比。
    /// 通常按相对耗时设置（重的阶段权重大）。
    /// </summary>
    float ProgressWeight { get; }

    /// <summary>执行阶段，从 ctx 读取依赖并写入产出。</summary>
    void Execute(WorldBuildContext ctx);
}
