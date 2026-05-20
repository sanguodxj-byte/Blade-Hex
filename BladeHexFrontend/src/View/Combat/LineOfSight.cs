// LineOfSight.cs
// 视线 / 掩体 / 高地 / 渡河 — 战术战斗系统
//
// 设计变更（视野系统已移除）:
//   - 不再有"视野半径"概念。所有单位永久看见整张地图。
//   - LOS 不再是二元 block 检查。相反，射线沿途的地形阻挡和中间单位
//     累计成命中惩罚（accuracyMod），由 CombatRuleEngine 应用到攻击检定。
//   - 这个模型适合中小型战场（≤30×30 格），保持视觉简单且让玩家和 AI
//     永远能感知整个战场。未来引入 50×50+ 大型战场时再加视野系统。
//
// 这层是 Frontend 适配器：把 HexGrid / Unit 转成 IBattleField 调
// 用 LosCore 的纯逻辑实现。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Data;
using System.Linq;

namespace BladeHex.Combat;

/// <summary>
/// 视线 / 命中惩罚静态工具类。
/// </summary>
public static class LineOfSight
{
    /// <summary>
    /// 计算从 <paramref name="from"/> 到 <paramref name="to"/> 路径上的
    /// 累计命中惩罚（地形阻挡 + 中间单位）。返回值≤0，直接传给
    /// <c>CombatRuleEngine.AttackInput.AccuracyMod</c>。
    /// </summary>
    public static int GetPathPenalty(Vector2I from, Vector2I to, HexGrid grid, Unit? attacker = null, Unit? defender = null)
    {
        var field = new HexGridBattleField(grid);
        return LosCore.GetPathPenalty(from, to, field,
            pos => IsTileOccupiedByOtherUnit(pos, grid, attacker, defender));
    }

    /// <summary>
    /// 二元 LOS 检查 — 仅供需要硬性遮挡的特殊场景使用（如直线法术不能穿墙）。
    /// 主战斗路径请使用 <see cref="GetPathPenalty"/> 让命中累积惩罚而不是被阻挡。
    /// </summary>
    [Obsolete("视野系统移除：主战斗路径请改用 GetPathPenalty 累计命中惩罚。仅特殊场景（如直线法术穿墙）保留。")]
    public static bool HasLos(Vector2I from, Vector2I to, HexGrid grid)
        => LosCore.HasLos(from, to, new HexGridBattleField(grid));

    public static int GetCoverLevel(Vector2I targetPos, Vector2I attackerPos, HexGrid grid)
        => LosCore.GetCoverLevel(targetPos, new HexGridBattleField(grid));

    public static HighGroundResult GetHighGroundBonus(Vector2I attackerPos, Vector2I defenderPos, HexGrid grid)
    {
        var core = LosCore.GetHighGroundBonus(attackerPos, defenderPos, new HexGridBattleField(grid));
        return new HighGroundResult
        {
            Advantage    = core.Advantage,
            Disadvantage = core.Disadvantage,
            RangeBonus   = core.RangeBonus,
        };
    }

    public static bool HasRiverCrossingPenalty(Vector2I attackerPos, Vector2I defenderPos, HexGrid grid)
        => LosCore.HasRiverCrossingPenalty(attackerPos, defenderPos, new HexGridBattleField(grid));

    public static List<Vector2I> GetHexLine(Vector2I from, Vector2I to)
        => LosCore.GetHexLine(from, to);

    /// <summary>
    /// 路径上是否站着除攻击者/防御者之外的其他单位。
    /// </summary>
    private static bool IsTileOccupiedByOtherUnit(Vector2I pos, HexGrid grid, Unit? attacker, Unit? defender)
    {
        var cell = grid.GetCell(pos.X, pos.Y);
        if (cell?.Occupant == null) return false;
        if (cell.Occupant == attacker || cell.Occupant == defender) return false;
        return true;
    }

    // ========================================
    // 结果类型 (kept here for source compatibility; LosCore.HighGroundResultCore is the canonical Core type)
    // ========================================

    public struct HighGroundResult
    {
        public bool Advantage;
        public bool Disadvantage;
        public int RangeBonus;
    }
}

/// <summary>
/// HexGrid 适配器 — 把 Frontend 的 HexGrid 暴露成 IBattleField。
/// </summary>
internal readonly struct HexGridBattleField : IBattleField
{
    private readonly HexGrid _grid;
    public HexGridBattleField(HexGrid grid) { _grid = grid; }

    public int GetElevation(Vector2I pos)
    {
        var cell = _grid.GetCell(pos.X, pos.Y);
        return cell?.Elevation ?? 1;
    }

    public bool BlocksLineOfSight(Vector2I pos)
    {
        var cell = _grid.GetCell(pos.X, pos.Y);
        if (cell == null) return false;
        if (cell.CoverType >= 2) return true;
        return cell.Data?.blocksLineOfSight ?? false;
    }

    public int GetCoverLevel(Vector2I pos)
    {
        var cell = _grid.GetCell(pos.X, pos.Y);
        return cell?.Data?.coverLevel ?? 0;
    }

    public BattleCellData.TerrainType GetTerrainType(Vector2I pos)
    {
        var cell = _grid.GetCell(pos.X, pos.Y);
        return cell?.Data?.terrainType ?? BattleCellData.TerrainType.Plains;
    }

    public bool IsValid(Vector2I pos) => _grid.GetCell(pos.X, pos.Y) != null;
}
