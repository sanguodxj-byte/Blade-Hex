// SwitchWeaponCommand.cs — 切换武器组命令（可撤销）
using Godot;

namespace BladeHex.Combat.Commands;

/// <summary>
/// 切换武器组命令 — 可撤销
/// Execute: 翻转 UsingPrimaryWeapon
/// Undo: 再翻转回来
/// </summary>
public class SwitchWeaponCommand : CommandBase
{
    public override string CommandType => "switch_weapon";
    public override string Description => "Switch weapon set";
    public override bool IsUndoable => true;

    public long UnitId { get; }

    public SwitchWeaponCommand(long unitId) => UnitId = unitId;

    public override CommandResult Execute(CommandContext ctx)
    {
        var unit = FindUnit(ctx);
        if (unit == null) return CommandResult.Fail("Unit not found");

        unit.SwitchWeaponSet();
        return CommandResult.Ok(new SwitchWeaponResult(UnitId));
    }

    public override void Undo(CommandContext ctx)
    {
        var unit = FindUnit(ctx);
        if (unit == null) return;

        // 再次翻转即恢复
        unit.SwitchWeaponSet();
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
