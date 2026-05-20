// OverworldSampler.cs
// 战斗地图采样器 — 从大地图抽取本次战斗的"放大版"输入。
//
// 核心契约（requirements.md R1 / R6）：
//   - POI 战斗:Footprint 全部 hex + 沿 footprint 外扩 K 圈邻居（K = POIScaleTable.SamplingRingCount）
//   - 野外遭遇:玩家所在 hex + 沿其外扩 K 圈邻居（默认 = Tiny 档,即 1）
//   - 不依赖任何全局状态;不抛异常(grid null / footprint 空 / 跨 chunk 失败均走 fallback)
//   - 不允许根据 POI 类型注入"虚拟 sample" — 所有地形派生必须可追溯到 sample 集合中实际存在的 tile
//
// 见 .kiro/specs/combat-hex-from-overworld-state/design.md §3.1
using System.Collections.Generic;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Map.Generation;

/// <summary>采样输出三元组(R1#1)</summary>
public readonly struct SampleSet
{
    public IReadOnlyList<HexOverworldTile> Tiles { get; init; }
    public Vector2I CenterAxial { get; init; }
    public int Radius { get; init; }
    public bool IsEmpty => Tiles == null || Tiles.Count == 0;

    public static SampleSet Empty => new()
    {
        Tiles = System.Array.Empty<HexOverworldTile>(),
        CenterAxial = Vector2I.Zero,
        Radius = 0,
    };
}

/// <summary>战斗地图采样器(R1, R6)</summary>
public static class OverworldSampler
{
    /// <summary>
    /// R1 主入口。POI 战 = footprint + K 圈邻居;野外 = encounterCoord + K 圈邻居。
    /// 所有边缘场景静默兜底,不抛异常。
    /// </summary>
    public static SampleSet Sample(BattleContext context, HexOverworldGrid? grid, int samplingRingCount)
    {
        if (grid == null) return SampleSet.Empty;          // R6#2 上层走模板路径
        if (samplingRingCount < 0) samplingRingCount = 0;

        // 1. 解析 footprint
        var footprint = ResolveFootprint(context);
        if (footprint.Count == 0)
        {
            // 不可能的状态:ResolveFootprint 至少返回 EncounterCoord
            GD.PushError("[OverworldSampler] ResolveFootprint returned empty list");
            return SampleSet.Empty;
        }

        // 2. 外扩 K 圈
        var hexes = ExpandRings(footprint, samplingRingCount);

        // 3. 收集真实 tile（跨 chunk null 跳过 — R1#5）
        var tiles = new List<HexOverworldTile>(hexes.Count);
        foreach (var h in hexes)
        {
            var tile = grid.GetTileAtCoord(h);
            if (tile != null) tiles.Add(tile);
        }

        if (tiles.Count == 0) return SampleSet.Empty;       // R6#1 上层走模板兜底

        // 4. 计算 center + radius (R1#7)
        var center = ResolveCenterAxial(context);
        int radius = 0;
        foreach (var t in tiles)
        {
            int d = HexUtils.AxialDistance(t.Coord, center);
            if (d > radius) radius = d;
        }

        return new SampleSet
        {
            Tiles = tiles,
            CenterAxial = center,
            Radius = radius,
        };
    }

    /// <summary>POI 战取 OccupiedHexes;野外取 EncounterCoord;空 footprint 退化到 EncounterCoord(R6#4)</summary>
    private static List<Vector2I> ResolveFootprint(BattleContext ctx)
    {
        if (ctx.DefendingPOI != null && ctx.DefendingPOI.OccupiedHexes != null
            && ctx.DefendingPOI.OccupiedHexes.Length > 0)
        {
            return new List<Vector2I>(ctx.DefendingPOI.OccupiedHexes);
        }
        if (ctx.DefendingPOI != null)
        {
            // R6#4:DefendingPOI 非空但 OccupiedHexes 空 → warning + 退化到野外路径
            GD.PushWarning("[OverworldSampler] DefendingPOI.OccupiedHexes 为空,退化到 EncounterCoord");
        }
        return new List<Vector2I> { ctx.EncounterCoord };
    }

    /// <summary>POI 战用 POI.CenterHex(R1#3),野外用 EncounterCoord(R1#4)</summary>
    private static Vector2I ResolveCenterAxial(BattleContext ctx)
    {
        if (ctx.DefendingPOI != null && ctx.DefendingPOI.OccupiedHexes != null
            && ctx.DefendingPOI.OccupiedHexes.Length > 0)
        {
            return ctx.DefendingPOI.CenterHex;
        }
        return ctx.EncounterCoord;
    }

    /// <summary>
    /// 从 footprint 整体外扩 K 圈邻居,返回 footprint + 所有外圈 hex(去重)。
    /// "外扩"定义:取 footprint 中所有 hex 的并集,反复执行 union ← union ∪ Σ neighbors(hex) 共 K 次。
    /// </summary>
    private static HashSet<Vector2I> ExpandRings(IList<Vector2I> footprint, int K)
    {
        var union = new HashSet<Vector2I>(footprint);
        for (int i = 0; i < K; i++)
        {
            var next = new HashSet<Vector2I>(union);
            foreach (var h in union)
            {
                foreach (var nb in HexUtils.GetNeighbors(h.X, h.Y))
                    next.Add(nb);
            }
            union = next;
        }
        return union;
    }
}
