// StrongholdPlacer.cs
// 据点结构生成器 — hex 直线画法
//
// 形状：从地图边缘两个相邻顶点各向中心画直线，过中点后停止，
//       两个端点之间连线形成正面城墙。
using System.Collections.Generic;
using BladeHex.Data;
using Godot;

namespace BladeHex.Map.Generation;

public static class StrongholdPlacer
{
    public static void Place(
        Dictionary<Vector2I, BattleCellData.TerrainType> tm,
        Dictionary<Vector2I, int> em,
        BattleMapGenerator.BattleMapData md,
        Vector2I? approachDir,
        Dictionary<Vector2I, int> staircaseFacings)
    {
        staircaseFacings.Clear();
        int N = md.HexRadius > 0 ? md.HexRadius : Mathf.Min(md.Width, md.Height) / 2;
        if (N < 5) return;

        // 1. 确定城墙朝向（哪条边）
        int edgeIdx = ResolveEdgeIndex(approachDir); // 0-5，对应六边形的 6 条边

        // 2. 取该边的两个顶点
        var (vertA, vertB) = GetEdgeVertices(N, edgeIdx);

        // 3. 从两个顶点向中心画直线，限制长度 7-9 cell
        int stopDist = GD.RandRange(7, 9);

        var lineA = HexLineDraw(vertA, Vector2I.Zero);
        var lineB = HexLineDraw(vertB, Vector2I.Zero);

        // 端点 C、D = 直线上第 stopDist 格
        int idxC = Mathf.Min(stopDist, lineA.Count - 1);
        int idxD = Mathf.Min(stopDist, lineB.Count - 1);
        var pointC = lineA[idxC];
        var pointD = lineB[idxD];

        // 4. 【高度规则 - 禁止修改算法】
        //    采样正面城墙位置的自然地形最终高度（模拟 EnforceTerrainElevation）
        //    城墙 = base + 2, 塔楼 = base + 3, 楼梯 = base + 1
        int naturalBase = 2;
        var frontLine = HexLineDraw(pointC, pointD);
        foreach (var pos in frontLine)
        {
            if (!em.TryGetValue(pos, out int rawElev)) continue;
            if (!tm.TryGetValue(pos, out var terrain)) continue;
            // 模拟最终高度 = 原始值 + 地形加成
            int finalElev = SimulateFinalElevation(terrain, rawElev);
            if (finalElev > naturalBase) naturalBase = finalElev;
        }

        // 5. 画城墙
        var wallSet = new HashSet<Vector2I>();
        var wallCells = new List<Vector2I>();

        // A→C 段
        for (int i = 0; i <= idxC; i++)
        {
            if (i < lineA.Count && md.ContainsCoord(lineA[i]) && tm.ContainsKey(lineA[i]))
            {
                wallCells.Add(lineA[i]);
                wallSet.Add(lineA[i]);
            }
        }
        // B→D 段
        for (int i = 0; i <= idxD; i++)
        {
            if (i < lineB.Count && md.ContainsCoord(lineB[i]) && tm.ContainsKey(lineB[i]))
            {
                if (!wallSet.Contains(lineB[i]))
                {
                    wallCells.Add(lineB[i]);
                    wallSet.Add(lineB[i]);
                }
            }
        }
        // C→D 正面城墙（frontLine 已在上面计算）
        foreach (var pos in frontLine)
        {
            if (md.ContainsCoord(pos) && tm.ContainsKey(pos) && !wallSet.Contains(pos))
            {
                wallCells.Add(pos);
                wallSet.Add(pos);
            }
        }

        if (wallCells.Count < 4) return;

        // 6. 分配类型：顶点=塔楼，C/D=塔楼，正面中点=城门，其余=城墙
        var towerPositions = new HashSet<Vector2I> { vertA, vertB, pointC, pointD };
        Vector2I gatePos = frontLine.Count > 0 ? frontLine[frontLine.Count / 2] : pointC;

        foreach (var pos in wallCells)
        {
            BattleCellData.TerrainType type;
            int elev;

            if (towerPositions.Contains(pos))
            // 【高度规则 - 禁止修改】城墙 = 周围最高自然地形 + 2，塔楼 +3，城门同城墙
            { type = BattleCellData.TerrainType.Tower; elev = naturalBase + 3; }
            else if (pos == gatePos)
            { type = BattleCellData.TerrainType.Gate; elev = naturalBase + 2; }
            else
            { type = BattleCellData.TerrainType.Rampart; elev = naturalBase + 2; }

            tm[pos] = type;
            em[pos] = Mathf.Clamp(elev, 0, 7);
        }

        // 7. 楼梯：正面城墙内侧（城内 = 靠近地图边缘方向 = 靠近 edgeMid）
        Vector2I edgeMid = new Vector2I((vertA.X + vertB.X) / 2, (vertA.Y + vertB.Y) / 2);
        int stairCount = 0;
        for (int i = 1; i < frontLine.Count - 1; i += 3)
        {
            var wallPos = frontLine[i];
            if (!wallSet.Contains(wallPos)) continue;

            // 城内 = 靠近边缘中点（edgeMid）的方向
            int bestDir = -1;
            int bestDist = int.MaxValue; // 要最靠近 edgeMid
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, d);
                if (!md.ContainsCoord(nb) || wallSet.Contains(nb)) continue;
                int dist = HexUtils.AxialDistance(nb, edgeMid);
                if (dist < bestDist) { bestDist = dist; bestDir = d; }
            }
            if (bestDir < 0) continue;

            var stairPos = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, bestDir);
            if (!tm.ContainsKey(stairPos) || wallSet.Contains(stairPos)) continue;

            tm[stairPos] = BattleCellData.TerrainType.Staircase;
            // 【高度规则 - 禁止修改】楼梯 = 周围最高自然地形 + 1
            em[stairPos] = Mathf.Clamp(naturalBase + 1, 0, 9);
            staircaseFacings[stairPos] = (bestDir + 3) % 6; // 朝向城外
            stairCount++;
        }

        // 兜底
        if (stairCount == 0 && frontLine.Count > 2)
        {
            var wallPos = frontLine[frontLine.Count / 2];
            int bestDir = -1; int bestDist = int.MaxValue;
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, d);
                if (!md.ContainsCoord(nb) || wallSet.Contains(nb)) continue;
                int dist = HexUtils.AxialDistance(nb, edgeMid);
                if (dist < bestDist) { bestDist = dist; bestDir = d; }
            }
            if (bestDir >= 0)
            {
                var stairPos = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, bestDir);
                if (tm.ContainsKey(stairPos) && !wallSet.Contains(stairPos))
                {
                    tm[stairPos] = BattleCellData.TerrainType.Staircase;
                    // 【高度规则 - 禁止修改】楼梯 = 周围最高自然地形 + 1
                    em[stairPos] = Mathf.Clamp(naturalBase + 1, 0, 9);
                    staircaseFacings[stairPos] = (bestDir + 3) % 6;
                }
            }
        }

        // 8. 城内建筑（正面城墙内侧方向）
        int placed = 0;
        for (int i = 0; i < frontLine.Count; i += 2)
        {
            if (placed >= 6) break;
            var wallPos = frontLine[i];
            // 找最靠近中心的邻居方向
            int bestDir = -1; int bestDist = int.MaxValue;
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, d);
                if (!md.ContainsCoord(nb) || wallSet.Contains(nb)) continue;
                int dist = HexUtils.AxialDistance(nb, Vector2I.Zero);
                if (dist < bestDist) { bestDist = dist; bestDir = d; }
            }
            if (bestDir < 0) continue;
            // 内侧第 2 格
            var inner1 = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, bestDir);
            var inner2 = HexUtils.GetNeighbor(inner1.X, inner1.Y, bestDir);
            if (!md.ContainsCoord(inner2) || !tm.ContainsKey(inner2) || wallSet.Contains(inner2)) continue;
            if (tm[inner2] == BattleCellData.TerrainType.Staircase) continue;
            if (GD.Randf() < 0.4f)
            {
                tm[inner2] = placed % 2 == 0 ? BattleCellData.TerrainType.Road : BattleCellData.TerrainType.Ruins;
                em[inner2] = naturalBase;
                placed++;
            }
        }

        // 9. 道路连接城门：从城门向地图中心方向画一条直线道路
        if (gatePos != Vector2I.Zero)
        {
            var roadToCenter = HexLineDraw(gatePos, Vector2I.Zero);
            // 只画前 5 格（不要太长）
            int roadLen = Mathf.Min(5, roadToCenter.Count - 1);
            for (int i = 1; i <= roadLen; i++)
            {
                var pos = roadToCenter[i];
                if (!md.ContainsCoord(pos) || !tm.ContainsKey(pos)) continue;
                if (wallSet.Contains(pos)) continue;
                if (tm[pos] == BattleCellData.TerrainType.Staircase) continue;
                tm[pos] = BattleCellData.TerrainType.Road;
                em[pos] = naturalBase;
            }
        }

        // 10. 城内铺石板路：从城门向边缘方向 BFS 扩散，遇到城墙或地图边缘停止
        var innerPaved = new HashSet<Vector2I>();
        var bfsQueue = new List<Vector2I>();

        // 从城门向边缘方向的第一个邻居开始
        int edgeDirIdx = 0;
        int edgeBestDist = int.MaxValue;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexUtils.GetNeighbor(gatePos.X, gatePos.Y, d);
            int dist = HexUtils.AxialDistance(nb, edgeMid);
            if (dist < edgeBestDist && !wallSet.Contains(nb) && md.ContainsCoord(nb))
            { edgeBestDist = dist; edgeDirIdx = d; }
        }
        var bfsStart = HexUtils.GetNeighbor(gatePos.X, gatePos.Y, edgeDirIdx);
        if (md.ContainsCoord(bfsStart) && !wallSet.Contains(bfsStart))
        {
            bfsQueue.Add(bfsStart);
            innerPaved.Add(bfsStart);
        }

        while (bfsQueue.Count > 0)
        {
            var cur = bfsQueue[0]; bfsQueue.RemoveAt(0);
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(cur.X, cur.Y, d);
                if (innerPaved.Contains(nb) || wallSet.Contains(nb)) continue;
                if (!md.ContainsCoord(nb) || !tm.ContainsKey(nb)) continue;
                innerPaved.Add(nb);
                bfsQueue.Add(nb);
            }
        }

        // 铺路（排除楼梯和废墟）
        foreach (var pos in innerPaved)
        {
            if (tm[pos] == BattleCellData.TerrainType.Staircase) continue;
            if (tm[pos] == BattleCellData.TerrainType.Ruins) continue;
            tm[pos] = BattleCellData.TerrainType.Road;
            em[pos] = naturalBase;
        }

        // 11. 强制城墙外侧高度差 ≥ 2：城墙外的格子不能比城墙低不到 2 级
        //     扫描城墙每个格子的外侧邻居，如果高度差 < 2 则压低
        int wallElev = naturalBase + 2; // 城墙高度
        foreach (var wpos in wallSet)
        {
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(wpos.X, wpos.Y, d);
                if (wallSet.Contains(nb)) continue;       // 跳过其他城墙格
                if (innerPaved.Contains(nb)) continue;    // 跳过城内格
                if (!em.ContainsKey(nb)) continue;
                if (!md.ContainsCoord(nb)) continue;

                // 城墙外侧：如果高度差 < 2，压低到 wallElev - 2
                int maxAllowed = wallElev - 2;
                if (em[nb] > maxAllowed)
                    em[nb] = maxAllowed;
            }
        }
    }

    // ========================================================================
    // 辅助方法
    // ========================================================================

    /// <summary>六边形地图的 6 个顶点坐标（半径 N）</summary>
    private static Vector2I GetVertex(int N, int vertexIdx)
    {
        // 六边形 6 个顶点（axial 坐标，flat-top）
        // 顶点 i 在方向 i 和 i+1 之间
        return vertexIdx switch
        {
            0 => new Vector2I(N, -N),    // 右上
            1 => new Vector2I(0, -N),    // 上
            2 => new Vector2I(-N, 0),    // 左上
            3 => new Vector2I(-N, N),    // 左下
            4 => new Vector2I(0, N),     // 下
            5 => new Vector2I(N, 0),     // 右下
            _ => Vector2I.Zero,
        };
    }

    /// <summary>取六边形一条边的两个顶点</summary>
    private static (Vector2I a, Vector2I b) GetEdgeVertices(int N, int edgeIdx)
    {
        var a = GetVertex(N, edgeIdx);
        var b = GetVertex(N, (edgeIdx + 1) % 6);
        return (a, b);
    }

    /// <summary>边的法线方向（指向外侧）</summary>
    private static Vector2I GetEdgeNormal(int edgeIdx)
    {
        // 6 条边的外法线方向（axial）
        var normals = new Vector2I[]
        {
            new(1, -1), // 边 0（右上→上）的外法线
            new(-1, 0), // 边 1（上→左上）
            new(-1, 1), // 边 2（左上→左下）
            new(0, 1),  // 边 3（左下→下）
            new(1, 0),  // 边 4（下→右下）
            new(1, -1), // 边 5（右下→右上）
        };
        return normals[edgeIdx % 6];
    }

    /// <summary>边对应的 hex 方向索引（用于楼梯朝向）</summary>
    private static int GetEdgeDirectionIdx(int edgeIdx)
    {
        // 边 i 的外法线对应的方向索引
        return edgeIdx switch
        {
            0 => 1, // NE
            1 => 2, // N (0,-1)
            2 => 3, // NW
            3 => 4, // SW
            4 => 5, // S
            5 => 0, // SE
            _ => 0,
        };
    }

    /// <summary>根据攻方方向确定城墙在哪条边（0-5）</summary>
    private static int ResolveEdgeIndex(Vector2I? approachDir)
    {
        if (approachDir == null || (approachDir.Value.X == 0 && approachDir.Value.Y == 0))
            return 1; // 默认城墙在上边（边 1）

        // 找攻方方向最接近的边（城墙面朝攻方 = 在攻方反方向的边）
        var hexDirs = new Vector2I[]
        {
            new(1, -1), new(0, -1), new(-1, 0),
            new(-1, 1), new(0, 1), new(1, 0),
        };

        int dq = -approachDir.Value.X;
        int dr = -approachDir.Value.Y;

        int bestIdx = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < 6; i++)
        {
            float score = dq * hexDirs[i].X + dr * hexDirs[i].Y;
            if (score > bestScore) { bestScore = score; bestIdx = i; }
        }
        return bestIdx;
    }

    /// <summary>模拟 FinalizeCells 中 EnforceTerrainElevation 的效果</summary>
    private static int SimulateFinalElevation(BattleCellData.TerrainType t, int baseElev)
    {
        int bonus = t switch
        {
            BattleCellData.TerrainType.DeepWater => -2,
            BattleCellData.TerrainType.ShallowWater or BattleCellData.TerrainType.River => -1,
            BattleCellData.TerrainType.Swamp or BattleCellData.TerrainType.Bog => -1,
            BattleCellData.TerrainType.Hills or BattleCellData.TerrainType.Rocky => 1,
            BattleCellData.TerrainType.Mountain or BattleCellData.TerrainType.MountainSnow => 2,
            _ => 0,
        };
        return Mathf.Clamp(baseElev + bonus, 0, 5);
    }

    /// <summary>Hex 直线画法（axial 坐标）</summary>
    private static List<Vector2I> HexLineDraw(Vector2I a, Vector2I b)
    {
        int dist = HexUtils.AxialDistance(a, b);
        var result = new List<Vector2I>();
        if (dist == 0) { result.Add(a); return result; }

        for (int i = 0; i <= dist; i++)
        {
            float t = (float)i / dist;
            float fq = a.X + (b.X - a.X) * t;
            float fr = a.Y + (b.Y - a.Y) * t;
            float fs = -fq - fr;
            // Cube round
            int rq = Mathf.RoundToInt(fq);
            int rr = Mathf.RoundToInt(fr);
            int rs = Mathf.RoundToInt(fs);
            float dq = Mathf.Abs(rq - fq);
            float dr = Mathf.Abs(rr - fr);
            float ds = Mathf.Abs(rs - fs);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            result.Add(new Vector2I(rq, rr));
        }
        return result;
    }
}
