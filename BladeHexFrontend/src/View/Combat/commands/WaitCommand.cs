// WaitCommand.cs — 等待/结束行动命令（可撤销）
using Godot;

namespace BladeHex.Combat.Commands;

/// <summary>
/// 等待命令 — 可撤销
/// Execute: 设置 HasActed=true, HasMoved=true, AP=0
/// Undo: 恢复到执行前的快照
/// </summary>
public class WaitCommand : CommandBase
{
    public override string CommandType => "wait";
    public override string Description => "Wait";
    public override bool IsUndoable => true;

    public long UnitId { get; }

    // Undo 快照
    private bool _originalHasActed;
    private bool _originalHasMoved;
    private float _originalAp;

    public WaitCommand(long unitId) => UnitId = unitId;

    public override CommandResult Execute(CommandContext ctx)
    {
        var unit = FindUnit(ctx);
        if (unit == null) return CommandResult.Fail("Unit not found");

        // 快照
        _originalHasActed = unit.HasActed;
        _originalHasMoved = unit.HasMoved;
        _originalAp = unit.CurrentAp;

        // 执行
        unit.HasActed = true;
        unit.HasMoved = true;
        unit.CurrentAp = 0;

        return CommandResult.Ok(new Godot.Collections.Dictionary { { "unit_id", UnitId } });
    }

    public override void Undo(CommandContext ctx)
    {
        var unit = FindUnit(ctx);
        if (unit == null) return;

        unit.HasActed = _originalHasActed;
        unit.HasMoved = _originalHasMoved;
        unit.CurrentAp = _originalAp;
    }

    public override Godot.Collections.Dictionary Serialize()
        => new() { { "command_type", CommandType }, { "unit_id", UnitId } };

    private Unit? FindUnit(CommandContext ctx)
    {
        foreach (var u in ctx.Registry.AllUnits)
        {
            if ((long)u.GetInstanceId() == UnitId) return u;
        }
        return null;
    }
}
