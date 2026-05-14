// UseSkillCommand.cs — 技能释放命令（不可撤销）
using Godot;

namespace BladeHex.Combat.Commands;

/// <summary>
/// 技能释放命令 — 不可撤销
/// 与 AttackCommand 类似,Execute 只标记意图;
/// 实际结算由 CombatManager 在外部调用 SkillEffectExecutor
/// </summary>
public class UseSkillCommand : CommandBase
{
    public override string CommandType => "use_skill";
    public override string Description { get; }
    public override bool IsUndoable => false;

    public long CasterId { get; }
    public string SkillEffect { get; }
    public Vector2I TargetCell { get; }

    /// <summary>技能结算结果(由 CombatManager 在 Execute 后填入)</summary>
    public Godot.Collections.Dictionary? ResolveResult { get; set; }

    public UseSkillCommand(long casterId, string skillEffect, Vector2I targetCell)
    {
        CasterId = casterId;
        SkillEffect = skillEffect;
        TargetCell = targetCell;
        Description = $"Use {skillEffect}";
    }

    public override CommandResult Execute(CommandContext ctx)
    {
        return CommandResult.Ok(new Godot.Collections.Dictionary
        {
            { "caster_id", CasterId },
            { "skill_effect", SkillEffect },
            { "target_cell", TargetCell },
        });
    }

    public override Godot.Collections.Dictionary Serialize()
        => new()
        {
            { "command_type", CommandType },
            { "caster_id", CasterId },
            { "skill_effect", SkillEffect },
            { "target_cell", TargetCell },
        };
}
