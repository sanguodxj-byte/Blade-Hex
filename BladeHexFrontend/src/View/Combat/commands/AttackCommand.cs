// AttackCommand.cs — 攻击命令（不可撤销）
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat.Commands;

/// <summary>
/// 攻击命令 — 不可撤销
/// Execute: 记录攻击意图,实际结算由 CombatResolver 在外部完成
/// 本命令的职责是"标记行动已发生 + 入栈记录",不直接调 CombatResolver
/// (CombatResolver 需要 Unit 引用,而 Core 层命令不持有 Frontend 类型)
/// CombatManager.ExecuteCommand 在收到 AttackCommand 后会调用 CombatResolver
/// </summary>
public class AttackCommand : CommandBase
{
    public override string CommandType => "attack";
    public override string Description { get; }
    public override bool IsUndoable => false;

    public long AttackerId { get; }
    public long DefenderId { get; }
    public bool IsCharge { get; }
    public bool IsAoo { get; }

    /// <summary>攻击结算结果(由 CombatManager 在 Execute 后填入)</summary>
    public Godot.Collections.Dictionary? ResolveResult { get; set; }

    public AttackCommand(long attackerId, long defenderId, bool isCharge = false, bool isAoo = false)
    {
        AttackerId = attackerId;
        DefenderId = defenderId;
        IsCharge = isCharge;
        IsAoo = isAoo;
        Description = $"Attack unit {defenderId}";
    }

    public override CommandResult Execute(CommandContext ctx)
    {
        // 攻击命令的 Execute 只做"标记意图成功"
        // 实际结算由 CombatManager 在外部调用 CombatResolver.ResolveAttack
        return CommandResult.Ok(new Godot.Collections.Dictionary
        {
            { "attacker_id", AttackerId },
            { "defender_id", DefenderId },
            { "is_charge", IsCharge },
            { "is_aoo", IsAoo },
        });
    }

    public override Godot.Collections.Dictionary Serialize()
        => new()
        {
            { "command_type", CommandType },
            { "attacker_id", AttackerId },
            { "defender_id", DefenderId },
            { "is_charge", IsCharge },
            { "is_aoo", IsAoo },
        };
}
