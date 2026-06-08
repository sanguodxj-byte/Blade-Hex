// UnitFootprint.cs
// 多格单位占用系统 — 六边形网格上的矩形足迹
// 设计：以中心格为锚点，向周围扩展 W×H 格的矩形区域。
// 在平顶六边形(flat-top)轴向坐标系中，"矩形"通过沿两个轴向方向扩展实现：
//   - 宽度(W)：沿 q 轴方向（水平）
//   - 高度(H)：沿 r 轴方向（斜向）
// 足迹尺寸示例：
//   1×1 = 1格（普通单位）
//   1×2 = 2格（蛇形/细长生物）
//   2×2 = 4格（大型四足兽）
//   2×3 = 6格（巨型生物）
//   3×3 = 9格（超大型）
//   3×4 = 12格（最大传奇生物如陨星之龙）
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 单位足迹（多格占用）工具类。
/// 支持 W×H 矩形足迹，在六边形网格上以中心格为锚点展开。
/// </summary>
public static class UnitFootprint
{
    /// <summary>
    /// 根据 CreatureSize 获取默认足迹尺寸 (width, height)。
    /// </summary>
    public static (int w, int h) GetDefaultSize(UnitData.CreatureSize size) => size switch
    {
        UnitData.CreatureSize.Tiny => (1, 1),
        UnitData.CreatureSize.Small => (1, 1),
        UnitData.CreatureSize.Medium => (1, 1),
        UnitData.CreatureSize.Large => (2, 2),
        UnitData.CreatureSize.Huge => (2, 3),
        UnitData.CreatureSize.Gargantuan => (3, 4),
        _ => (1, 1),
    };

    /// <summary>
    /// 获取单位的足迹尺寸。优先使用 UnitData 的自定义值，否则根据 CreatureSize 推算。
    /// </summary>
    public static (int w, int h) GetSize(UnitData data)
    {
        if (data.FootprintW > 0 && data.FootprintH > 0)
            return (data.FootprintW, data.FootprintH);
        return GetDefaultSize(data.creatureSize);
    }

    /// <summary>
    /// 计算以 center 为锚点、指定 W×H 的矩形足迹占用格列表。
    /// 锚点位于矩形的中心（向下取整）。
    /// 在六边形轴向坐标中，宽度沿 q 轴展开，高度沿 r 轴展开。
    /// </summary>
    public static Vector2I[] GetFootprintCells(Vector2I center, int w, int h)
    {
        if (w <= 1 && h <= 1) return [center];

        var cells = new List<Vector2I>(w * h);

        // 锚点偏移：让 center 位于矩形中心
        int qOffset = -(w - 1) / 2;
        int rOffset = -(h - 1) / 2;

        for (int dq = 0; dq < w; dq++)
        {
            for (int dr = 0; dr < h; dr++)
            {
                cells.Add(new Vector2I(center.X + qOffset + dq, center.Y + rOffset + dr));
            }
        }

        return cells.ToArray();
    }

    /// <summary>
    /// 获取单位在指定中心位置的所有占用格。
    /// </summary>
    public static Vector2I[] GetCells(UnitData data, Vector2I center)
    {
        var (w, h) = GetSize(data);
        return GetFootprintCells(center, w, h);
    }

    /// <summary>
    /// 计算从一个点到多格单位的最短距离（到任意占用格的最小 hex 距离）。
    /// </summary>
    public static int DistanceTo(Vector2I from, Vector2I unitCenter, int footprintW, int footprintH)
    {
        if (footprintW <= 1 && footprintH <= 1)
            return Map.HexUtils.Distance(from.X, from.Y, unitCenter.X, unitCenter.Y);

        var cells = GetFootprintCells(unitCenter, footprintW, footprintH);
        int minDist = int.MaxValue;
        foreach (var cell in cells)
        {
            int d = Map.HexUtils.Distance(from.X, from.Y, cell.X, cell.Y);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    /// <summary>
    /// 计算两个多格单位之间的最短距离。
    /// </summary>
    public static int DistanceBetween(
        Vector2I centerA, int wA, int hA,
        Vector2I centerB, int wB, int hB)
    {
        if (wA <= 1 && hA <= 1 && wB <= 1 && hB <= 1)
            return Map.HexUtils.Distance(centerA.X, centerA.Y, centerB.X, centerB.Y);

        // 小单位 vs 大单位：只遍历大单位的格子
        if (wA <= 1 && hA <= 1)
            return DistanceTo(centerA, centerB, wB, hB);
        if (wB <= 1 && hB <= 1)
            return DistanceTo(centerB, centerA, wA, hA);

        // 双方都是多格：遍历双方所有格子对
        var cellsA = GetFootprintCells(centerA, wA, hA);
        var cellsB = GetFootprintCells(centerB, wB, hB);
        int minDist = int.MaxValue;
        foreach (var a in cellsA)
        {
            foreach (var b in cellsB)
            {
                int d = Map.HexUtils.Distance(a.X, a.Y, b.X, b.Y);
                if (d < minDist) minDist = d;
                if (d == 0) return 0;
            }
        }
        return minDist;
    }

    /// <summary>
    /// 判断单位是否为多格单位（占用超过1格）。
    /// </summary>
    public static bool IsMultiHex(UnitData? data)
    {
        if (data == null) return false;
        var (w, h) = GetSize(data);
        return w > 1 || h > 1;
    }

    /// <summary>
    /// 判断一个坐标是否在单位的足迹内。
    /// </summary>
    public static bool Contains(Vector2I center, int w, int h, Vector2I target)
    {
        if (w <= 1 && h <= 1) return center == target;

        int qOffset = -(w - 1) / 2;
        int rOffset = -(h - 1) / 2;

        int dq = target.X - center.X - qOffset;
        int dr = target.Y - center.Y - rOffset;

        return dq >= 0 && dq < w && dr >= 0 && dr < h;
    }
}
