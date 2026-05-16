// WorldCreator.cs
// 世界创建器 — 在架构优化 spec R3 重构后，退化为 WorldPipeline 的薄 wrapper。
//
// 历史：原文件约 1900 行，承担地形/河流/海岛/POI/道路/特殊角色等所有生成职责。
// 现状：所有阶段已抽取为独立的 IWorldStage 实现，详见 BladeHex.Strategic.WorldGen.Stages.*
using System;
using BladeHex.Strategic.WorldGen;

namespace BladeHex.Strategic;

/// <summary>
/// 世界创建器 — 委托给 <see cref="WorldPipeline"/> 完成实际生成工作。
/// 保留此类以维持外部 API 兼容（OverworldScene3D.World.cs 等持有 <c>OnProgress</c> 回调）。
/// </summary>
public class WorldCreator
{
    /// <summary>进度回调（0~1, message）。</summary>
    public Action<float, string>? OnProgress;

    /// <summary>
    /// 创建完整世界。等价于 <c>WorldPipeline.Default().Build(seed, config, OnProgress)</c>。
    /// </summary>
    public WorldData CreateWorld(int seed, WorldCreationConfig config)
        => WorldPipeline.Default().Build(seed, config, OnProgress);
}
