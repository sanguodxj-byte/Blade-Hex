// BridgePlacer.cs
// 桥从大地图 sample 派生 — 服务于 spec combat-hex-from-overworld-state R11。
//
// 核心契约：
//   - 桥的存在 ⇔ 大地图 IsBridge = IsRoad ∧ Terrain ∈ {River, ShallowWater, DeepWater}
//   - 战斗端不再扫水带 / 自造桥 hex line;只对 IsBridge sample 投影点放置 Bridge cell
//   - 桥规模:River=2 cell / ShallowWater=3 / DeepWater=4(R11#3)
//   - 延展方向:邻近 IsRoad sample 投影点方向(R11#4);无相邻 Road sample → 1 cell + warning(R11#5)
//   - 桥不进 R4 水域硬封顶(R11#7)
//   - 桥与 Road 重叠时桥优先(R11#8)
//   - 超出地图边界静默截断(R11#11)
//   - 没有 IsBridge sample → 战斗端不会出现 Bridge cell(R11#12)
//
// 见 .kiro/specs/combat-hex-from-overworld-state/design.md §3.4
using System.Collections.Generic;
using BladeHex.Data;
using Godot;

namespace BladeHex.Map.Generation;

public static class BridgePlacer
{
    /// <summary>R11 主入口。对每个 IsBridge sample 投影点放置 Bridge cell + 配套水带</summary>
    public static void Place(
        IReadOnlyList<SampleProjection> projections,
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        Dictionary<Vector2I, int> elevationMap,
        BattleMapGenerator.BattleMapData mapData)
    {
        foreach (var bridge in projections)
        {
            if (!bridge.IsBridge) continue;

            int length = bridge.Tile.Terrain switch
            {
                HexOverworldTile.TerrainType.River        => 2,
                HexOverworldTile.TerrainType.ShallowWater => 3,
                HexOverworldTile.TerrainType.DeepWater    => 4,
                _ => 2,
            };
            int splashRadius = bridge.Tile.Terrain switch
            {
                HexOverworldTile.TerrainType.River => 1,
                _ => 2,
            };

            // 找邻近 Road sample 决定延展方向
            var dir = ResolveBridgeDirection(bridge, projections);
            if (dir == Vector2I.Zero)
            {
                // R11#5:孤立桥 sample → 降级为 1 cell
                GD.PushWarning("[BridgePlacer] 桥 sample 无相邻 Road sample,降级为 1 cell");
                length = 1;
                dir = new Vector2I(1, 0);  // 任意方向占位(单 cell 时方向不影响)
            }

            // 1) 沿方向放置 length 个 Bridge cell
            for (int i = 0; i < length; i++)
            {
                var pos = new Vector2I(bridge.BattleAxial.X + dir.X * i, bridge.BattleAxial.Y + dir.Y * i);
                if (!mapData.ContainsCoord(pos)) continue;  // R11#11 静默截断
                terrainMap[pos] = BattleCellData.TerrainType.Bridge;
                elevationMap[pos] = 1;  // 桥面比水高一档(R11#6)
            }

            // 2) 桥两端配套水带:在 bridge sample 周围 splashRadius 内放 ShallowWater
            //    跳过桥本身的 cell(避免"水覆盖桥")
            PlaceSplashWater(bridge.BattleAxial, dir, length, splashRadius, terrainMap, elevationMap, mapData);
        }
    }

    /// <summary>
    /// 取与 bridge sample 在大地图上相邻、IsRoad=true 的 sample 投影点方向作为桥延展方向。
    /// </summary>
    private static Vector2I ResolveBridgeDirection(
        SampleProjection bridge,
        IReadOnlyList<SampleProjection> projections)
    {
        // 大地图上的邻居坐标
        var neighbors = HexUtils.GetNeighbors(bridge.Tile.Coord.X, bridge.Tile.Coord.Y);

        // 在 projections 里找 Tile.Coord ∈ neighbors 且 IsRoad=true 的 sample
        foreach (var p in projections)
        {
            if (!p.Tile.IsRoad) continue;
            // 桥本身也是 IsRoad,排除
            if (p.Tile.Coord == bridge.Tile.Coord) continue;
            bool isNeighbor = false;
            foreach (var nb in neighbors)
            {
                if (p.Tile.Coord == nb) { isNeighbor = true; break; }
            }
            if (!isNeighbor) continue;

            // 取从 bridge 投影点指向 road 邻居投影点的方向(归一化到单步 axial 单位向量)
            int dq = p.BattleAxial.X - bridge.BattleAxial.X;
            int dr = p.BattleAxial.Y - bridge.BattleAxial.Y;
            return NormalizeAxialDirection(dq, dr);
        }
        return Vector2I.Zero;
    }

    /// <summary>把 axial 偏移归一化到 6 个 axial 单位方向之一(取夹角最小者)</summary>
    private static Vector2I NormalizeAxialDirection(int dq, int dr)
    {
        if (dq == 0 && dr == 0) return Vector2I.Zero;

        // 6 个 axial 单位方向
        var dirs = new[]
        {
            new Vector2I( 1,  0), new Vector2I( 0,  1), new Vector2I(-1,  1),
            new Vector2I(-1,  0), new Vector2I( 0, -1), new Vector2I( 1, -1),
        };
        // 用点积(也即向量同向程度)选最大者
        Vector2I best = dirs[0];
        float bestScore = float.MinValue;
        foreach (var d in dirs)
        {
            float score = dq * d.X + dr * d.Y;
            if (score > bestScore) { bestScore = score; best = d; }
        }
        return best;
    }

    /// <summary>桥两端 splashRadius 范围内放 ShallowWater(避免覆盖桥本身的 cell)</summary>
    private static void PlaceSplashWater(
        Vector2I bridgeStart, Vector2I dir, int length, int splashRadius,
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        Dictionary<Vector2I, int> elevationMap,
        BattleMapGenerator.BattleMapData mapData)
    {
        // 先收集桥占用的 cell(防止水覆盖桥)
        var bridgeCells = new HashSet<Vector2I>();
        for (int i = 0; i < length; i++)
            bridgeCells.Add(new Vector2I(bridgeStart.X + dir.X * i, bridgeStart.Y + dir.Y * i));

        for (int dq = -splashRadius; dq <= splashRadius; dq++)
        {
            int r1 = Mathf.Max(-splashRadius, -dq - splashRadius);
            int r2 = Mathf.Min(splashRadius, -dq + splashRadius);
            for (int dr = r1; dr <= r2; dr++)
            {
                var pos = new Vector2I(bridgeStart.X + dq, bridgeStart.Y + dr);
                if (bridgeCells.Contains(pos)) continue;
                if (!mapData.ContainsCoord(pos)) continue;
                if (!terrainMap.TryGetValue(pos, out var existing)) continue;
                // 不覆盖已有结构 / 道路 / 水域
                if (existing == BattleCellData.TerrainType.Wall
                    || existing == BattleCellData.TerrainType.Road
                    || existing == BattleCellData.TerrainType.Bridge
                    || existing == BattleCellData.TerrainType.ShallowWater
                    || existing == BattleCellData.TerrainType.DeepWater
                    || existing == BattleCellData.TerrainType.River) continue;
                terrainMap[pos] = BattleCellData.TerrainType.ShallowWater;
                elevationMap[pos] = 0;
            }
        }
    }
}
