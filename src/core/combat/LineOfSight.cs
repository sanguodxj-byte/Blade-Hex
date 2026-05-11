// LineOfSight.cs
// 视线系统 — 六边形网格上的视线检查、掩体判定、高地优势
// 对应策划案 03-战术战斗系统 → 三、视线与掩护
// 迁移自 GDScript LineOfSight.gd
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Data;
using System.Linq;

namespace BladeHex.Combat;

/// <summary>
/// 视线系统 — 静态工具类
/// 负责视线检查(LOS)、掩体判定、高地优势、渡河惩罚
/// </summary>
public static class LineOfSight
{
    // ========================================
    // 视线检查
    // ========================================

    /// <summary>
    /// 检查两点之间是否有视线。
    /// 规则：森林/密林/山地/墙壁/废墟阻挡穿越视线。
    /// 单位不阻挡视线。高处单位可越过低处障碍物。
    /// </summary>
    public static bool HasLos(Vector2I from, Vector2I to, HexGrid grid)
    {
        if (from == to)
            return true;

        var line = GetHexLine(from, to);
        var fromCell = grid.GetCell(from.X, from.Y);
        int fromElev = fromCell?.Elevation ?? 1;

        // 跳过起点和终点
        for (int i = 1; i < line.Count - 1; i++)
        {
            var cellPos = line[i];
            var cell = grid.GetCell(cellPos.X, cellPos.Y);
            if (cell == null)
                continue;

            // 高处可越过低矮障碍
            if (CanSeeOver(cell, fromElev))
                continue;

            // 检查是否阻挡视线
            if (cell.Data != null && cell.Data.blocksLineOfSight)
                return false;
            if (cell.CoverType >= 2)
                return false;
        }
        return true;
    }

    /// <summary>检查目标是否受掩体保护，返回掩体等级（0=无, 1=半掩体, 2=全掩体）</summary>
    public static int GetCoverLevel(Vector2I targetPos, Vector2I attackerPos, HexGrid grid)
    {
        var targetCell = grid.GetCell(targetPos.X, targetPos.Y);
        if (targetCell?.Data == null)
            return 0;
        return targetCell.Data.coverLevel;
    }

    /// <summary>检查是否可以过肩射击（越过1排友军射击）</summary>
    public static bool CanOverShoulder(Vector2I casterPos, Vector2I targetPos, HexGrid grid, Unit[] allyUnits)
    {
        var line = GetHexLine(casterPos, targetPos);
        int allyCount = 0;
        for (int i = 1; i < line.Count - 1; i++)
        {
            var cellPos = line[i];
            var cell = grid.GetCell(cellPos.X, cellPos.Y);
            if (cell?.Occupant != null)
            {
                foreach (var ally in allyUnits)
                    if (ally == cell.Occupant)
                        allyCount++;
            }
        }
        return allyCount == 1;
    }

    // ========================================
    // 视野范围
    // ========================================

    /// <summary>获取单位的视野范围（默认6格，丘陵/山地+2，森林-2，黑暗视觉保底6）</summary>
    public static int GetVisionRange(Unit unit, HexGrid grid)
    {
        int baseVision = 6;

        var cell = grid.GetCell(unit.GridPos.X, unit.GridPos.Y);
        if (cell?.Data != null)
        {
            switch (cell.Data.terrainType)
            {
                case BattleCellData.TerrainType.Hills:
                case BattleCellData.TerrainType.Mountain:
                    baseVision += 2;
                    break;
                case BattleCellData.TerrainType.Forest:
                case BattleCellData.TerrainType.DenseForest:
                    baseVision -= 2;
                    break;
            }
        }

        // 黑暗视觉（精灵等种族不减少夜间视野）
        if (unit.Data?.Race != null)
        {
            var traits = unit.Data.Race.RacialTraits;
            if (traits != null && traits.Contains("dark_vision"))
                baseVision = Math.Max(baseVision, 6);
        }

        return Math.Max(2, baseVision);
    }

    // ========================================
    // 高地优势
    // ========================================

    /// <summary>
    /// 获取高地优势判定
    /// 高位→低位: 优势 + 射程加成; 低位→高位: 劣势 + 射程惩罚
    /// </summary>
    public static HighGroundResult GetHighGroundBonus(Vector2I attackerPos, Vector2I defenderPos, HexGrid grid)
    {
        var atkCell = grid.GetCell(attackerPos.X, attackerPos.Y);
        var defCell = grid.GetCell(defenderPos.X, defenderPos.Y);

        if (atkCell == null || defCell == null)
            return new HighGroundResult { Advantage = false, Disadvantage = false, RangeBonus = 0 };

        int atkElev = atkCell.Elevation;
        int defElev = defCell.Elevation;
        int diff = atkElev - defElev;

        if (diff >= 2)
            return new HighGroundResult { Advantage = true, Disadvantage = false, RangeBonus = 2 };
        if (diff == 1)
            return new HighGroundResult { Advantage = true, Disadvantage = false, RangeBonus = 1 };
        if (diff == -1)
            return new HighGroundResult { Advantage = false, Disadvantage = true, RangeBonus = -1 };
        if (diff <= -2)
            return new HighGroundResult { Advantage = false, Disadvantage = true, RangeBonus = -2 };
            
        return new HighGroundResult { Advantage = false, Disadvantage = false, RangeBonus = 0 };
    }

    // ========================================
    // 渡河惩罚
    // ========================================

    /// <summary>检查攻击路径上是否有渡河惩罚（攻击路径上有浅水/沼泽格）</summary>
    public static bool HasRiverCrossingPenalty(Vector2I attackerPos, Vector2I defenderPos, HexGrid grid)
    {
        var line = GetHexLine(attackerPos, defenderPos);
        foreach (var cellPos in line)
        {
            var cell = grid.GetCell(cellPos.X, cellPos.Y);
            if (cell?.Data != null)
            {
                if (cell.Data.terrainType == BattleCellData.TerrainType.ShallowWater ||
                    cell.Data.terrainType == BattleCellData.TerrainType.Swamp)
                    return true;
            }
        }
        return false;
    }

    // ========================================
    // 内部：六边形Bresenham线
    // ========================================

    /// <summary>获取两点之间的六边形直线路径</summary>
    public static List<Vector2I> GetHexLine(Vector2I from, Vector2I to)
    {
        var result = new List<Vector2I>();
        int dist = HexUtils.Distance(from.X, from.Y, to.X, to.Y);
        if (dist <= 0)
        {
            result.Add(from);
            return result;
        }

        // 线性插值，步长 1/dist
        for (int i = 0; i <= dist; i++)
        {
            float t = (dist == 0) ? 0f : (float)i / dist;
            float q = Mathf.Lerp(from.X, to.X, t);
            float r = Mathf.Lerp(from.Y, to.Y, t);
            result.Add(HexUtils.HexRound(new Vector2(q, r)));
        }
        return result;
    }

    /// <summary>高处观察者可越过低矮障碍物</summary>
    private static bool CanSeeOver(HexCell cell, int observerElev)
    {
        // 观察者比障碍物高2级以上时可以越过
        if (observerElev > cell.Elevation + 1)
            return true;
        return false;
    }

    // ========================================
    // 结果类型
    // ========================================

    public struct HighGroundResult
    {
        public bool Advantage;
        public bool Disadvantage;
        public int RangeBonus;
    }
}