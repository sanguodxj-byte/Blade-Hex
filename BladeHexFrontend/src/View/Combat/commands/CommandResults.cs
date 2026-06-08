// CommandResults.cs — 命令执行结果的类型化载荷
// 替代 Godot.Collections.Dictionary，提供类型安全 + 序列化兼容
using Godot;

namespace BladeHex.Combat.Commands;

/// <summary>
/// 命令结果载荷基类 — 所有具体结果类型继承此类
/// Serialize() 产出与原有 Dictionary 完全一致的格式，保证录像/存档兼容
/// </summary>
public abstract record CommandPayload
{
    public abstract Godot.Collections.Dictionary Serialize();
}

/// <summary>
/// 移动命令结果
/// </summary>
public record MoveResult(long UnitId, Vector2I From, Vector2I To, int PathLength) : CommandPayload
{
    public override Godot.Collections.Dictionary Serialize() => new()
    {
        { "unit_id", UnitId },
        { "from", From },
        { "to", To },
        { "path_length", PathLength },
    };
}

/// <summary>
/// 攻击命令结果
/// </summary>
public record AttackResult(long AttackerId, long DefenderId, bool IsCharge, bool IsAoo) : CommandPayload
{
    public override Godot.Collections.Dictionary Serialize() => new()
    {
        { "attacker_id", AttackerId },
        { "defender_id", DefenderId },
        { "is_charge", IsCharge },
        { "is_aoo", IsAoo },
    };
}

/// <summary>
/// 技能释放命令结果
/// </summary>
public record SkillResult(long CasterId, string SkillEffect, Vector2I TargetCell) : CommandPayload
{
    public override Godot.Collections.Dictionary Serialize() => new()
    {
        { "caster_id", CasterId },
        { "skill_effect", SkillEffect },
        { "target_cell", TargetCell },
    };
}

/// <summary>
/// 等待/结束行动命令结果
/// </summary>
public record WaitResult(long UnitId) : CommandPayload
{
    public override Godot.Collections.Dictionary Serialize() => new()
    {
        { "unit_id", UnitId },
    };
}

/// <summary>
/// 切换武器命令结果
/// </summary>
public record SwitchWeaponResult(long UnitId) : CommandPayload
{
    public override Godot.Collections.Dictionary Serialize() => new()
    {
        { "unit_id", UnitId },
    };
}
