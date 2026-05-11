// SpellShapeResolver.cs
// 法术范围形状解析器 — 在六边形网格上计算各种法术范围形状
// 对应策划案 07-法术系统 → 六、范围形状定义
// 迁移自 GDScript SpellShapeResolver.gd
using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 法术范围形状解析器 — 静态工具类
/// </summary>
public static class SpellShapeResolver
{
    // ========================================
    // 形状接口 — 使用 Godot.Collections.Dictionary 作为 grid.cells
    // ========================================

    /// <summary>
    /// 获取指定形状范围内的所有格子坐标
    /// </summary>
    /// <param name="shape">SpellData.SpellShape 枚举值</param>
    /// <param name="origin">目标格子（或施法者位置）</param>
    /// <param name="casterPos">施法者位置</param>
    /// <param name="size">范围大小</param>
    /// <param name="hasCell">判断格子是否存在于网格中</param>
    public static Vector2I[] GetCellsInShape(
        int shape, Vector2I origin, Vector2I casterPos, int size,
        System.Func<Vector2I, bool> hasCell)
    {
        return shape switch
        {
            0 => ShapeSingle(origin, hasCell),           // SINGLE
            1 => ShapeRay(casterPos, origin, size, hasCell),  // RAY
            2 => ShapeCone(casterPos, origin, size, hasCell), // CONE
            3 => ShapeSphere(origin, size, hasCell),      // SPHERE
            4 => ShapeLine(casterPos, origin, size, hasCell), // LINE
            5 => ShapeCross(origin, size, hasCell),       // CROSS
            6 => ShapeSelf(casterPos, size, hasCell),     // SELF
            7 => ShapeTouch(casterPos, hasCell),          // TOUCH
            _ => [origin],
        };
    }

    // ========================================
    // 形状实现
    // ========================================

    /// <summary>单体 — 1个目标格子</summary>
    public static Vector2I[] ShapeSingle(Vector2I target, System.Func<Vector2I, bool> hasCell)
    {
        if (hasCell(target)) return [target];
        return [];
    }

    /// <summary>射线 — 从施法者出发经过目标的直线，N格长</summary>
    public static Vector2I[] ShapeRay(Vector2I caster, Vector2I target, int length, System.Func<Vector2I, bool> hasCell)
    {
        var cells = new List<Vector2I>();
        int direction = GetHexDirection(caster, target);
        var current = caster;
        for (int i = 0; i < length; i++)
        {
            current = Map.HexUtils.GetNeighbor(current.X, current.Y, direction);
            if (hasCell(current))
                cells.Add(current);
            else
                break;
        }
        return cells.ToArray();
    }

    /// <summary>锥形 — 施法者面前120°扇形，N格长</summary>
    public static Vector2I[] ShapeCone(Vector2I caster, Vector2I target, int length, System.Func<Vector2I, bool> hasCell)
    {
        var cells = new List<Vector2I>();
        int mainDir = GetHexDirection(caster, target);
        int[] dirs = [mainDir, (mainDir + 1) % 6, (mainDir + 5) % 6];
        foreach (int dir in dirs)
        {
            var current = caster;
            for (int i = 0; i < length; i++)
            {
                current = Map.HexUtils.GetNeighbor(current.X, current.Y, dir);
                if (hasCell(current))
                    cells.Add(current);
            }
        }
        return cells.ToArray();
    }

    /// <summary>球形 — 以目标格为中心，N格半径 (半径1=7格, 半径2=19格)</summary>
    public static Vector2I[] ShapeSphere(Vector2I center, int radius, System.Func<Vector2I, bool> hasCell)
    {
        var cells = new List<Vector2I> { center };
        var visited = new HashSet<Vector2I> { center };
        var fringe = new List<Vector2I> { center };

        for (int k = 1; k <= radius; k++)
        {
            var newFringe = new List<Vector2I>();
            foreach (var hex in fringe)
            {
                for (int dir = 0; dir < 6; dir++)
                {
                    var neighbor = Map.HexUtils.GetNeighbor(hex.X, hex.Y, dir);
                    if (hasCell(neighbor) && visited.Add(neighbor))
                    {
                        cells.Add(neighbor);
                        newFringe.Add(neighbor);
                    }
                }
            }
            fringe = newFringe;
        }
        return cells.ToArray();
    }

    /// <summary>线形 — 两点之间的直线，N格长</summary>
    public static Vector2I[] ShapeLine(Vector2I from, Vector2I to, int maxLength, System.Func<Vector2I, bool> hasCell)
    {
        var cells = new List<Vector2I>();
        var hexLine = GetHexLine(from, to);
        int count = 0;
        foreach (var cell in hexLine)
        {
            if (cell == from) continue;
            if (hasCell(cell))
            {
                cells.Add(cell);
                count++;
                if (count >= maxLength) break;
            }
            else break;
        }
        return cells.ToArray();
    }

    /// <summary>十字 — 以目标格为中心的十字形，N格长</summary>
    public static Vector2I[] ShapeCross(Vector2I center, int length, System.Func<Vector2I, bool> hasCell)
    {
        var cells = new List<Vector2I> { center };
        for (int dir = 0; dir < 6; dir++)
        {
            var current = center;
            for (int i = 0; i < length; i++)
            {
                current = Map.HexUtils.GetNeighbor(current.X, current.Y, dir);
                if (hasCell(current))
                    cells.Add(current);
            }
        }
        return cells.ToArray();
    }

    /// <summary>自身 — 以施法者为中心</summary>
    public static Vector2I[] ShapeSelf(Vector2I caster, int radius, System.Func<Vector2I, bool> hasCell)
    {
        if (radius <= 0) return [caster];
        return ShapeSphere(caster, radius, hasCell);
    }

    /// <summary>触碰 — 近战范围内的相邻格子</summary>
    public static Vector2I[] ShapeTouch(Vector2I caster, System.Func<Vector2I, bool> hasCell)
    {
        var cells = new List<Vector2I>();
        for (int dir = 0; dir < 6; dir++)
        {
            var neighbor = Map.HexUtils.GetNeighbor(caster.X, caster.Y, dir);
            if (hasCell(neighbor))
                cells.Add(neighbor);
        }
        return cells.ToArray();
    }

    // ========================================
    // 辅助方法
    // ========================================

    /// <summary>获取从一个六角格指向另一个六角格的方向 (0-5)</summary>
    public static int GetHexDirection(Vector2I from, Vector2I to)
    {
        int bestDir = 0;
        int bestDist = 999999;
        for (int dir = 0; dir < 6; dir++)
        {
            var offset = Map.HexUtils.Directions[dir];
            var candidate = from + offset;
            int dist = Map.HexUtils.Distance(candidate.X, candidate.Y, to.X, to.Y);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestDir = dir;
            }
        }
        return bestDir;
    }

    /// <summary>六边形网格上的直线算法 (简易 Bresenham)</summary>
    public static Vector2I[] GetHexLine(Vector2I from, Vector2I to)
    {
        var line = new List<Vector2I> { from };
        int dist = Map.HexUtils.Distance(from.X, from.Y, to.X, to.Y);
        if (dist == 0) return line.ToArray();

        var current = from;
        for (int i = 0; i < dist; i++)
        {
            int dir = GetHexDirection(current, to);
            current = Map.HexUtils.GetNeighbor(current.X, current.Y, dir);
            line.Add(current);
        }
        return line.ToArray();
    }
}
