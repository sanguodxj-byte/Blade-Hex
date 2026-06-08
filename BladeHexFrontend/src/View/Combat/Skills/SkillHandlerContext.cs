// SkillHandlerContext.cs
// 技能执行上下文 — 统一传递给所有技能 handler 的参数包
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Combat.Skills;

/// <summary>
/// 技能执行上下文 — 封装技能 handler 所需的全部运行时数据
/// 避免每个 handler 方法签名不一致
/// </summary>
public readonly struct SkillHandlerContext
{
    /// <summary>施放者</summary>
    public Unit Attacker { get; init; }

    /// <summary>目标格坐标</summary>
    public Vector2I TargetCell { get; init; }

    /// <summary>战斗网格（可能为 null）</summary>
    public HexGrid? Grid { get; init; }

    /// <summary>敌方单位列表</summary>
    public IEnumerable<Unit> Enemies { get; init; }

    /// <summary>友方单位列表</summary>
    public IEnumerable<Unit> Allies { get; init; }

    /// <summary>强类型结果（handler 不直接写入此字段，而是通过 Builder 构建）</summary>
    public SkillExecutionResult Result { get; init; }

    /// <summary>结果构建器（handler 通过此对象添加伤害/治疗/状态效果等）</summary>
    public SkillResultBuilder Builder { get; init; }
}