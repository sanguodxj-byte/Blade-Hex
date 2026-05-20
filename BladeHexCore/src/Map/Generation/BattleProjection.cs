// BattleProjection.cs
// 把 SampleSet 投影到战斗 axial 平面。
//
// 核心契约（requirements.md R2 / R8#1）：
//   - scale = battleHexRadius / sampleRadius (R2#2)
//   - sampleRadius = 0 → 单 tile 投影到原点 (R2#3)
//   - water sample 用 effScale = scale × 0.6 (R2#4)
//   - 投影超出 N 仍保留 (R2#6;后续 Voronoi 自然忽略)
//   - 末尾按 (X, Y) axial 字典序稳定排序保证 byte-identical 输出 (R8#1)
//   - 不依赖 GD.Randf — 纯几何 (R2#5)
//
// 见 .kiro/specs/combat-hex-from-overworld-state/design.md §3.2
using System.Collections.Generic;
using Godot;

namespace BladeHex.Map.Generation;

/// <summary>单个 sample tile 投影到战斗平面后的结果(design §2.5)</summary>
public readonly struct SampleProjection
{
    public HexOverworldTile Tile { get; init; }
    public Vector2I BattleAxial { get; init; }
    public bool IsLand { get; init; }
    public bool IsWater { get; init; }
    public bool IsBridge { get; init; }
}

public static class BattleProjection
{
    /// <summary>R2:把 SampleSet 中每个 tile 投影到战斗 axial 平面</summary>
    public static List<SampleProjection> Project(SampleSet samples, int battleHexRadius)
    {
        var result = new List<SampleProjection>(samples.Tiles.Count);
        if (samples.IsEmpty) return result;

        if (samples.Radius == 0)
        {
            // R2#3:单 tile 直接放原点
            var t = samples.Tiles[0];
            result.Add(MakeProjection(t, Vector2I.Zero));
            // 单元素无需排序
            return result;
        }

        float scale = (float)battleHexRadius / samples.Radius;
        foreach (var tile in samples.Tiles)
        {
            int dq = tile.Coord.X - samples.CenterAxial.X;
            int dr = tile.Coord.Y - samples.CenterAxial.Y;

            // R2#4:水 sample 用降低后的有效缩放(避免大量水 cell 落地图外)
            float effScale = IsWaterSample(tile) ? scale * 0.6f : scale;
            int bq = Mathf.RoundToInt(dq * effScale);
            int br = Mathf.RoundToInt(dr * effScale);
            result.Add(MakeProjection(tile, new Vector2I(bq, br)));
        }

        // R8#1: 按 axial 字典序稳定排序 — Voronoi 平局解决依赖此顺序
        result.Sort((a, b) =>
        {
            int c = a.BattleAxial.X.CompareTo(b.BattleAxial.X);
            return c != 0 ? c : a.BattleAxial.Y.CompareTo(b.BattleAxial.Y);
        });

        return result;
    }

    /// <summary>大地图 tile 是否为"水域 sample"(深/浅水/河流/IsRiver 标记)</summary>
    public static bool IsWaterSample(HexOverworldTile t) =>
        t.Terrain is HexOverworldTile.TerrainType.DeepWater
        or HexOverworldTile.TerrainType.ShallowWater
        or HexOverworldTile.TerrainType.River
        || t.IsRiver;

    private static SampleProjection MakeProjection(HexOverworldTile tile, Vector2I battleAxial)
    {
        bool isWater = IsWaterSample(tile);
        return new SampleProjection
        {
            Tile = tile,
            BattleAxial = battleAxial,
            IsLand = !isWater,
            IsWater = isWater,
            IsBridge = tile.IsBridge,  // R11 派生:IsRoad ∧ Terrain ∈ 水
        };
    }
}
