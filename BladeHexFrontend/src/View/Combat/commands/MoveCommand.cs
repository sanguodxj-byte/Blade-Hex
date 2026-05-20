// MoveCommand.cs — 移动命令（可撤销）
using Godot;
using System.Collections.Generic;

namespace BladeHex.Combat.Commands;

/// <summary>
/// 移动命令 — 可撤销
/// Execute: 更新 GridPos + 格占用 + 消耗 AP + 设置 HasMoved
/// Undo: 恢复 GridPos + 格占用 + 归还 AP + 恢复 HasMoved
/// 注意: 视觉动画由 CombatScene 监听事件后播放,本命令只处理逻辑状态
/// </summary>
public class MoveCommand : CommandBase
{
    public override string CommandType => "move";
    public override string Description { get; }
    public override bool IsUndoable => true;

    public long UnitId { get; }
    public List<Vector2I> Path { get; }

    // Undo 快照
    private Vector2I _originalPos;
    private float _originalAp;
    private bool _originalHasMoved;

    public MoveCommand(long unitId, Vector2I originalPos, List<Vector2I> path)
    {
        UnitId = unitId;
        _originalPos = originalPos;
        Path = path;
        Description = $"Move to ({path[^1].X}, {path[^1].Y})";
    }

    public override CommandResult Execute(CommandContext ctx)
    {
        var unit = FindUnit(ctx);
        if (unit == null) return CommandResult.Fail("Unit not found");
        if (Path.Count == 0) return CommandResult.Fail("Empty path");

        // 快照当前状态
        _originalPos = unit.GridPos;
        _originalAp = unit.CurrentAp;
        _originalHasMoved = unit.HasMoved;

        var destination = Path[^1];

        // 更新格占用
        if (ctx.Grid != null)
        {
            var fromCell = ctx.Grid.GetCell(_originalPos.X, _originalPos.Y);
            if (fromCell != null) fromCell.Occupant = null;

            var toCell = ctx.Grid.GetCell(destination.X, destination.Y);
            if (toCell != null) toCell.Occupant = unit;
        }

        // 更新逻辑状态
        unit.GridPos = destination;
        unit.HasMoved = true;

        // AP 消耗 = 路径步数（Path 不含起点，Path.Count 即为移动格数）
        // 地形消耗由 HexGrid.GetPathCost 计算，此处用格数作为最低消耗
        float apCost = Mathf.Max(1f, Path.Count);
        unit.ConsumeAp(apCost);

        return CommandResult.Ok(new Godot.Collections.Dictionary
        {
            { "unit_id", UnitId },
            { "from", _originalPos },
            { "to", destination },
            { "path_length", Path.Count - 1 },
        });
    }

    public override void Undo(CommandContext ctx)
    {
        var unit = FindUnit(ctx);
        if (unit == null) return;

        var currentPos = unit.GridPos;

        // 恢复格占用
        if (ctx.Grid != null)
        {
            var currentCell = ctx.Grid.GetCell(currentPos.X, currentPos.Y);
            if (currentCell != null) currentCell.Occupant = null;

            var originalCell = ctx.Grid.GetCell(_originalPos.X, _originalPos.Y);
            if (originalCell != null) originalCell.Occupant = unit;
        }

        // 恢复逻辑状态
        unit.GridPos = _originalPos;
        unit.CurrentAp = _originalAp;
        unit.HasMoved = _originalHasMoved;
    }

    public override Godot.Collections.Dictionary Serialize()
    {
        var pathArr = new Godot.Collections.Array();
        foreach (var p in Path) pathArr.Add(p);
        return new Godot.Collections.Dictionary
        {
            { "command_type", CommandType },
            { "unit_id", UnitId },
            { "origin", _originalPos },
            { "path", pathArr },
        };
    }

    private Unit? FindUnit(CommandContext ctx)
    {
        foreach (var u in ctx.Registry.AllUnits)
        {
            if ((long)u.GetInstanceId() == UnitId) return u;
        }
        // Fallback: 按原始位置查找
        return ctx.Registry.FindUnitAt(_originalPos);
    }
}
