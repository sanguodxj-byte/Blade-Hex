using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.AI;

/// <summary>
/// 空间分析工具类 —— 为 AI 提供地形、包夹、视线、冲锋等静态分析能力
/// 对应策划案 09-AI系统 → 四、空间分析算法
/// </summary>
public static class AISpatialAnalyzer
{
    /// <summary>获取攻击方向关系：0=正面, 1=侧翼, 2=背后</summary>
    public static int GetAttackFacing(Vector2I attackerPos, Vector2I targetPos, int targetFacing)
    {
        if (targetFacing < 0) return 0; // 未知朝向默认正面

        var dq = attackerPos.X - targetPos.X;
        var dr = attackerPos.Y - targetPos.Y;
        var attackDir = -1;

        for (int i = 0; i < 6; i++)
        {
            if (HexUtils.Directions[i] == new Vector2I(dq, dr))
            {
                attackDir = i;
                break;
            }
        }

        if (attackDir < 0) return 0;

        var frontDirs = new[] { targetFacing, (targetFacing + 1) % 6, (targetFacing + 5) % 6 };
        var flankDirs = new[] { (targetFacing + 2) % 6, (targetFacing + 4) % 6 };
        var backDir = (targetFacing + 3) % 6;

        if (attackDir == backDir) return 2;
        if (flankDirs.Contains(attackDir)) return 1;
        return 0;
    }

    /// <summary>获取两个位置间的高程优势：1=攻击者在高处, -1=攻击者在低处, 0=同高程</summary>
    public static int GetElevationAdvantage(HexGrid hexGrid, Vector2I fromPos, Vector2I toPos)
    {
        var fromCell = hexGrid.GetCell(fromPos.X, fromPos.Y);
        var toCell = hexGrid.GetCell(toPos.X, toPos.Y);
        if (fromCell == null || toCell == null) return 0;

        if (fromCell.Elevation > toCell.Elevation) return 1;
        if (fromCell.Elevation < toCell.Elevation) return -1;
        return 0;
    }

    /// <summary>检查冲锋是否有效（移动3格以上，非沙地/沼泽）</summary>
    public static bool CanCharge(HexGrid hexGrid, List<Vector2I> path, Vector2I startPos)
    {
        if (path.Count < 3) return false;

        foreach (var pos in path)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell?.Data != null)
            {
                var terrainType = cell.Data.terrainType;
                if (terrainType == BattleCellData.TerrainType.Sand || terrainType == BattleCellData.TerrainType.Swamp)
                    return false;
            }
        }
        return true;
    }

    /// <summary>评估一个位置的防御价值（掩体 + 高程 + 逃生路线）</summary>
    public static float EvaluatePositionDefense(HexGrid hexGrid, Vector2I pos, IEnumerable<Unit> threats)
    {
        var cell = hexGrid.GetCell(pos.X, pos.Y);
        if (cell == null) return 0.0f;

        float score = 0.0f;
        score += cell.CoverType * 2.0f;

        if (cell.Data != null) score += Math.Max(0, cell.Data.acBonus);

        int posElev = cell.Elevation;
        foreach (var threat in threats)
        {
            if (threat == null) continue;
            var threatCell = hexGrid.GetCell(threat.GridPos.X, threat.GridPos.Y);
            if (threatCell != null)
            {
                int diff = posElev - threatCell.Elevation;
                if (diff >= 2) score += 3.0f;      // 显著高地优势
                else if (diff == 1) score += 1.5f;  // 普通高地优势
                else if (diff == -1) score -= 1.0f; // 低向高射击不利
                else if (diff <= -2) score -= 2.0f; // 显著劣势
            }
        }

        int escapeRoutes = 0;
        for (int dir = 0; dir < 6; dir++)
        {
            var nb = HexUtils.GetNeighbor(pos.X, pos.Y, dir);
            var nbCell = hexGrid.GetCell(nb.X, nb.Y);
            if (nbCell != null && nbCell.Occupant == null) escapeRoutes++;
        }
        score += Math.Min(escapeRoutes, 4) * 0.5f;

        return Math.Clamp(score, -5.0f, 15.0f);
    }

    /// <summary>寻找最佳掩体射击位置</summary>
    public static List<(Vector2I Position, float Score)> FindCoverPositions(HexGrid hexGrid, Unit unit, Unit target, int moveRange)
    {
        var results = new List<(Vector2I, float)>();
        var weapon = unit.Model.GetMainHand() as WeaponData;
        int atkRange = weapon?.RangeCells ?? 1;
        var reachable = hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, moveRange);

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || (cell.Occupant != null && cell.Occupant != unit)) continue;

            int dist = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
            if (dist > atkRange) continue;

            float defScore = EvaluatePositionDefense(hexGrid, pos, new[] { target });
            results.Add((pos, defScore));
        }

        return results.OrderByDescending(r => r.Item2).ToList();
    }

    /// <summary>寻找包夹位置</summary>
    public static List<(Vector2I Position, int Facing)> FindFlankingPositions(HexGrid hexGrid, Unit target, Unit attacker, int moveRange)
    {
        var results = new List<(Vector2I, int)>();
        var weapon = attacker.Model.GetMainHand() as WeaponData;
        int atkRange = weapon?.RangeCells ?? 1;
        var reachable = hexGrid.GetCellsInRange(attacker.GridPos.X, attacker.GridPos.Y, moveRange);
        int targetFacing = target.Data?.Runtime.Facing ?? -1;

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || (cell.Occupant != null && cell.Occupant != attacker)) continue;

            int dist = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
            if (dist > atkRange) continue;

            int facing = GetAttackFacing(pos, target.GridPos, targetFacing);
            if (facing > 0) results.Add((pos, facing));
        }

        return results.OrderByDescending(r => r.Item2).ToList();
    }

    /// <summary>寻找最近的撤退位置</summary>
    public static Vector2I FindRetreatPosition(HexGrid hexGrid, Unit unit, IEnumerable<Unit> playerUnits)
    {
        Vector2I bestPos = unit.GridPos;
        float bestScore = -999.0f;
        var reachable = hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, unit.Model.GetMoveRange());

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || cell.Occupant != null) continue;

            float score = 0.0f;
            int totalDist = 0;
            foreach (var pu in playerUnits)
            {
                if (pu == null) continue;
                totalDist += HexUtils.Distance(pos.X, pos.Y, pu.GridPos.X, pu.GridPos.Y);
            }
            score += totalDist * 1.0f;
            score += cell.CoverType * 2.0f;

            if (score > bestScore)
            {
                bestScore = score;
                bestPos = pos;
            }
        }
        return bestPos;
    }

    /// <summary>获取考虑高程的有效射程</summary>
    public static int GetEffectiveRange(HexGrid hexGrid, Unit attacker, Vector2I fromPos, Vector2I targetPos)
    {
        var weapon = attacker.Model.GetMainHand() as WeaponData;
        int baseRange = weapon?.RangeCells ?? 1;
        
        var fromCell = hexGrid.GetCell(fromPos.X, fromPos.Y);
        var toCell = hexGrid.GetCell(targetPos.X, targetPos.Y);
        if (fromCell == null || toCell == null) return baseRange;

        int diff = fromCell.Elevation - toCell.Elevation;
        if (diff >= 2) baseRange += 2;
        else if (diff == 1) baseRange += 1;
        else if (diff == -1) baseRange = Math.Max(1, baseRange - 1);
        else if (diff <= -2) baseRange = Math.Max(1, baseRange - 2);

        return baseRange;
    }

    /// <summary>计算目标相邻的友方数量（用于夹击加成评估）</summary>
    public static int CountAdjacentAllies(HexGrid hexGrid, Vector2I targetPos, List<Unit> allyUnits)
    {
        int count = 0;
        for (int dir = 0; dir < 6; dir++)
        {
            var nb = HexUtils.GetNeighbor(targetPos.X, targetPos.Y, dir);
            var cell = hexGrid.GetCell(nb.X, nb.Y);
            if (cell?.Occupant != null)
            {
                if (allyUnits.Any(a => GodotObject.IsInstanceValid(a) && cell.Occupant == a))
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>计算沿路径移动会触发多少次借机攻击</summary>
    public static int CountOpportunityAttacks(HexGrid hexGrid, List<Vector2I> path, List<Unit> enemyUnits)
    {
        int count = 0;
        foreach (var pos in path)
        {
            // 检查这个位置周围的敌方近战单位
            for (int dir = 0; dir < 6; dir++)
            {
                var nb = HexUtils.GetNeighbor(pos.X, pos.Y, dir);
                var cell = hexGrid.GetCell(nb.X, nb.Y);
                if (cell?.Occupant == null) continue;

                var occupant = cell.Occupant;
                var enemy = enemyUnits.FirstOrDefault(e => GodotObject.IsInstanceValid(e) && e == occupant);
                if (enemy != null)
                {
                    var weapon = enemy.Model.GetMainHand() as WeaponData;
                    if (weapon == null || !weapon.IsRanged)
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }
}
