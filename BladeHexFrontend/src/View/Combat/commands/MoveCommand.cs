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
    private Vector2I[] _originalOccupiedCells = [];
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
        _originalOccupiedCells = unit.OccupiedCells ?? [];
        _originalAp = unit.CurrentAp;
        _originalHasMoved = unit.HasMoved;

        var destination = Path[^1];

        // v0.8 E4: cannot_move 检查
        if (unit.Data != null)
        {
            var moveLock = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(unit.Data, "cannot_move");
            if (moveLock.OverrideValue.HasValue && moveLock.OverrideValue.Value >= 1f)
                return CommandResult.Fail("无法移动");
        }

        // v1 职业被动: 万象代价/山岳之王/孤星之影 → 禁止移动
        if (!CareerPassiveHooks.CanMove(unit))
            return CommandResult.Fail("万象代价: 无法移动");

        // v0.8 E6: can_cross_enemies 允许穿越敌方格（但不可停留）
        // TODO: Phase E6 - integrate with HexGrid.FindPath (allowCrossEnemies flag)
        // 目前路径验证在 HexGrid.FindPath 层，需要在 FindPath 参数中加 flag 才能完整实现

        // 更新格占用（支持多格单位）
        if (ctx.Grid != null)
        {
            // 清除旧占用
            if (_originalOccupiedCells.Length > 0)
            {
                foreach (var cellPos in _originalOccupiedCells)
                {
                    var c = ctx.Grid.GetCell(cellPos.X, cellPos.Y);
                    if (c != null && c.Occupant == unit) c.Occupant = null;
                }
            }
            else
            {
                var fromCell = ctx.Grid.GetCell(_originalPos.X, _originalPos.Y);
                if (fromCell != null) fromCell.Occupant = null;
            }

            // 设置新占用
            var newCells = UnitFootprint.GetFootprintCells(destination, unit.FootprintW, unit.FootprintH);
            unit.OccupiedCells = newCells;
            foreach (var cellPos in newCells)
            {
                var toCell = ctx.Grid.GetCell(cellPos.X, cellPos.Y);
                if (toCell != null) toCell.Occupant = unit;
            }
        }

        // 更新逻辑状态
        unit.GridPos = destination;
        unit.HasMoved = true;

        // AP 消耗 = 路径步数（Path 不含起点，Path.Count 即为移动格数）
        // 地形消耗由 HexGrid.GetPathCost 计算，此处用格数作为最低消耗
        float apCost = Mathf.Max(1f, Path.Count);
        // v0.8 E2: buff 移动 AP 减免
        if (unit.Data != null)
        {
            var moveMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(unit.Data, "move_ap_reduction");
            float reduction = moveMod.FlatBonus * Path.Count; // 每格减免 N AP
            apCost = Mathf.Max(0.5f * Path.Count, apCost - reduction); // 最低 0.5 AP/格
        }

        // v1 职业被动: 风语者/灵风秘庭 — 移动消耗固定为 1 AP/格
        // v1 职业被动: 山岳之王 — 相邻敌人移动 +1 AP/格
        apCost = CareerPassiveHooks.ModifyMoveApCost(unit, apCost, Path.Count);

        // v1 职业被动: 游骑兵 — 奔袭免费移动格数减免 AP
        int freeCells = unit.Data?.Runtime?.CareerFreeMoveCellsRemaining ?? 0;
        if (freeCells > 0)
        {
            int freeUsed = System.Math.Min(freeCells, Path.Count);
            unit.Data.Runtime.CareerFreeMoveCellsRemaining -= freeUsed;
            float perCellAp = apCost / System.Math.Max(1, Path.Count);
            apCost = System.Math.Max(0.5f, apCost - perCellAp * freeUsed);
        }

        unit.ConsumeAp(apCost);

        // v1 职业被动: 移动后钩子 (记录格数/战争之风/风语者)
        CareerPassiveHooks.OnMoveCompleted(unit, Path.Count, apCost);

        return CommandResult.Ok(new MoveResult(UnitId, _originalPos, destination, Path.Count - 1));
    }

    public override void Undo(CommandContext ctx)
    {
        var unit = FindUnit(ctx);
        if (unit == null) return;

        // 恢复格占用（支持多格单位）
        if (ctx.Grid != null)
        {
            // 清除当前占用
            if (unit.OccupiedCells != null && unit.OccupiedCells.Length > 0)
            {
                foreach (var cellPos in unit.OccupiedCells)
                {
                    var c = ctx.Grid.GetCell(cellPos.X, cellPos.Y);
                    if (c != null && c.Occupant == unit) c.Occupant = null;
                }
            }
            else
            {
                var currentCell = ctx.Grid.GetCell(unit.GridPos.X, unit.GridPos.Y);
                if (currentCell != null) currentCell.Occupant = null;
            }

            // 恢复原始占用
            if (_originalOccupiedCells.Length > 0)
            {
                foreach (var cellPos in _originalOccupiedCells)
                {
                    var c = ctx.Grid.GetCell(cellPos.X, cellPos.Y);
                    if (c != null) c.Occupant = unit;
                }
            }
            else
            {
                var originalCell = ctx.Grid.GetCell(_originalPos.X, _originalPos.Y);
                if (originalCell != null) originalCell.Occupant = unit;
            }
        }

        // 恢复逻辑状态
        unit.GridPos = _originalPos;
        unit.OccupiedCells = _originalOccupiedCells;
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
