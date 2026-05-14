// ICommand.cs — Command Pattern 基础设施（Phase 2.1 实装版）
using Godot;

namespace BladeHex.Combat.Commands;

/// <summary>
/// 命令执行上下文 — 由 CombatManager 在 ExecuteCommand 时注入
/// 命令通过此上下文访问战斗系统,避免直接持有 CombatManager 引用
/// </summary>
public class CommandContext
{
    public UnitRegistry Registry { get; init; } = null!;
    public BladeHex.Map.HexGrid? Grid { get; init; }
    public Events.EventBus? EventBus { get; init; }
}

/// <summary>
/// 命令执行结果
/// </summary>
public class CommandResult
{
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public Godot.Collections.Dictionary? Payload { get; init; }

    public static CommandResult Ok(Godot.Collections.Dictionary? payload = null)
        => new() { Success = true, Payload = payload };
    public static CommandResult Fail(string reason)
        => new() { Success = false, FailureReason = reason };
}

/// <summary>
/// 战斗命令接口 — 所有玩家/AI 行动的统一协议
/// </summary>
public interface ICommand
{
    /// <summary>命令类型标识</summary>
    string CommandType { get; }

    /// <summary>人类可读描述</summary>
    string Description { get; }

    /// <summary>是否可撤销(Move/SwitchWeapon/Wait 可撤销;Attack/UseSkill 不可)</summary>
    bool IsUndoable { get; }

    /// <summary>执行命令,返回结果</summary>
    CommandResult Execute(CommandContext ctx);

    /// <summary>撤销命令(仅 IsUndoable=true 时有效)</summary>
    void Undo(CommandContext ctx);

    /// <summary>序列化(录像/存档)</summary>
    Godot.Collections.Dictionary Serialize();
}

/// <summary>
/// 命令基类 — 提供默认实现
/// </summary>
public abstract class CommandBase : ICommand
{
    public abstract string CommandType { get; }
    public abstract string Description { get; }
    public virtual bool IsUndoable => false;

    public abstract CommandResult Execute(CommandContext ctx);

    public virtual void Undo(CommandContext ctx)
    {
        if (!IsUndoable)
            throw new System.NotSupportedException($"Command '{CommandType}' is not undoable.");
    }

    public virtual Godot.Collections.Dictionary Serialize()
        => new() { { "command_type", CommandType }, { "description", Description } };
}

/// <summary>
/// 回合边界哨兵 — 标记回合切换点,Undo 不越过此边界
/// </summary>
public sealed class TurnBoundaryMarker : CommandBase
{
    public override string CommandType => "_turn_boundary";
    public override string Description => "Turn boundary";
    public override bool IsUndoable => false;

    public override CommandResult Execute(CommandContext ctx)
        => CommandResult.Ok();
}
