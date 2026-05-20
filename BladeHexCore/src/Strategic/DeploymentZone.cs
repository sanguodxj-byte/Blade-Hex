using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 部署区域生成器 — 根据战斗规模和交战类型生成双方部署坐标
///
/// 比例尺统一后,战斗地图可能是六边形(HexRadius > 0)或矩形(W×H)。
///
/// 两套部署逻辑:
/// 1. **快速战斗 / 模板路径(approachDir = null)**:
///    矩形 → 玩家 q=0~1,敌方 q=W-2~W-1。
///    六边形 → 沿 q 轴对半切:q &lt; 0 玩家,q &gt; 0 敌方。
/// 2. **大地图战斗(approachDir != null)**:
///    按"敌方相对玩家的方向向量"做半平面切分,敌方在正方向半边,玩家在反方向半边。
/// </summary>
public static class DeploymentZone
{
    /// <summary>
    /// 标准签名:传入 BattleMapData,自动适配六边形 / 矩形。
    /// </summary>
    /// <param name="approachDirection">敌方相对玩家的接近方向(axial 偏移)。null 表示快速战斗,左右两端切。</param>
    public static Godot.Collections.Dictionary GenerateZones(
        BladeHex.Map.BattleMapGenerator.BattleMapData md,
        BattleContext.EngagementType engagement,
        Vector2I? approachDirection = null)
    {
        return engagement switch
        {
            BattleContext.EngagementType.Normal => GenerateNormal(md, approachDirection),
            BattleContext.EngagementType.Ambush => GenerateAmbush(md),
            BattleContext.EngagementType.Ambushed => GenerateAmbushed(md),
            _ => GenerateNormal(md, approachDirection),
        };
    }

    /// <summary>正常遭遇:双方在地图两端部署</summary>
    private static Godot.Collections.Dictionary GenerateNormal(
        BladeHex.Map.BattleMapGenerator.BattleMapData md,
        Vector2I? approachDirection)
    {
        var player = new Godot.Collections.Array<Vector2I>();
        var enemy = new Godot.Collections.Array<Vector2I>();

        if (md.HexRadius > 0)
        {
            // 六边形地图
            if (approachDirection.HasValue && (approachDirection.Value.X != 0 || approachDirection.Value.Y != 0))
            {
                // 大地图战斗:按 approachDirection 做半平面切分
                AssignByApproachDirection(md, approachDirection.Value, player, enemy);
            }
            else
            {
                // 快速战斗:沿 q 轴(东西)对半切。玩家西(q<0),敌方东(q>0)。
                AssignByQAxis(md, player, enemy);
            }
        }
        else
        {
            // 矩形:玩家 q=0~1,敌方 q=W-2~W-1
            for (int q = 0; q < 2; q++)
            {
                int qOffset = (int)Math.Floor(q / 2.0);
                for (int r = -qOffset; r < md.Height - qOffset; r++)
                {
                    var key = new Vector2I(q, r);
                    if (IsDeployable(md.Cells, key)) player.Add(key);
                }
            }
            for (int q = md.Width - 2; q < md.Width; q++)
            {
                int qOffset = (int)Math.Floor(q / 2.0);
                for (int r = -qOffset; r < md.Height - qOffset; r++)
                {
                    var key = new Vector2I(q, r);
                    if (IsDeployable(md.Cells, key)) enemy.Add(key);
                }
            }
        }

        return new Godot.Collections.Dictionary { { "player", player }, { "enemy", enemy } };
    }

    /// <summary>沿 q 轴对半切:q&lt;0 玩家(西),q&gt;0 敌方(东),q=0 中线不分配。</summary>
    private static void AssignByQAxis(
        BladeHex.Map.BattleMapGenerator.BattleMapData md,
        Godot.Collections.Array<Vector2I> player,
        Godot.Collections.Array<Vector2I> enemy)
    {
        int n = md.HexRadius;
        int halfBand = Math.Max(2, n / 3); // 部署带宽度,留出中间区
        foreach (var coord in md.IterateCoords())
        {
            if (!IsDeployable(md.Cells, coord)) continue;
            if (coord.X <= -halfBand) player.Add(coord);
            else if (coord.X >= halfBand) enemy.Add(coord);
        }
        // 兜底:任一方为空则降级用 q=0 中线对半切
        if (player.Count == 0 || enemy.Count == 0)
        {
            player.Clear(); enemy.Clear();
            foreach (var coord in md.IterateCoords())
            {
                if (!IsDeployable(md.Cells, coord)) continue;
                if (coord.X < 0) player.Add(coord);
                else if (coord.X > 0) enemy.Add(coord);
            }
        }
    }

    /// <summary>
    /// 按 approachDirection 做半平面切分。
    /// 敌方部署在 approachDirection 指向的半边(战场中心向 approachDir 方向),
    /// 玩家部署在反方向半边。
    /// 算法:把 axial 坐标转换为"在 approachDir 上的投影分量",正分量 = 敌方,负分量 = 玩家。
    /// </summary>
    private static void AssignByApproachDirection(
        BladeHex.Map.BattleMapGenerator.BattleMapData md,
        Vector2I approachDir,
        Godot.Collections.Array<Vector2I> player,
        Godot.Collections.Array<Vector2I> enemy)
    {
        int n = md.HexRadius;
        // 把 axial 偏移投影到 approachDir 的方向上,得到一个标量"前后分量"。
        // axial 距离公式与点积近似:cube_dot(coord, dir) / 2
        // 等价用 (q*dq + r*dr + (q+r)*(dq+dr)) / 2
        int adq = approachDir.X, adr = approachDir.Y;
        // 估算战场半径在该方向上的最大投影,用于设定 halfBand
        int maxProj = Math.Max(1, ProjectAxial(new Vector2I(adq, adr) * n, adq, adr));
        // halfBand:留出中间区,典型 n/3 ~ n/2
        int halfBand = Math.Max(maxProj / 3, 1);

        foreach (var coord in md.IterateCoords())
        {
            if (!IsDeployable(md.Cells, coord)) continue;
            int proj = ProjectAxial(coord, adq, adr);
            if (proj >= halfBand) enemy.Add(coord);
            else if (proj <= -halfBand) player.Add(coord);
        }

        // 兜底:任一方为空则降级用 0 中线
        if (player.Count == 0 || enemy.Count == 0)
        {
            player.Clear(); enemy.Clear();
            foreach (var coord in md.IterateCoords())
            {
                if (!IsDeployable(md.Cells, coord)) continue;
                int proj = ProjectAxial(coord, adq, adr);
                if (proj > 0) enemy.Add(coord);
                else if (proj < 0) player.Add(coord);
            }
        }
    }

    /// <summary>把 axial coord 投影到 (dq, dr) 方向上的 cube 点积(保留方向单调性,足以做半平面分类)。</summary>
    /// <remarks>
    /// 推导:axial → cube 是 (q, -q-r, r)。cube 点积 = q1·q2 + (q1+r1)(q2+r2) + r1·r2,
    /// 展开等于 2·q1·dq + q1·dr + r1·dq + 2·r1·dr。
    /// </remarks>
    private static int ProjectAxial(Vector2I coord, int dq, int dr)
    {
        return coord.X * dq + coord.Y * dr + (coord.X + coord.Y) * (dq + dr);
    }

    /// <summary>玩家伏击敌人：玩家分散在地形有利位置，敌人集中</summary>
    private static Godot.Collections.Dictionary GenerateAmbush(BladeHex.Map.BattleMapGenerator.BattleMapData md)
    {
        var player = new Godot.Collections.Array<Vector2I>();
        var enemy = new Godot.Collections.Array<Vector2I>();

        if (md.HexRadius > 0)
        {
            int n = md.HexRadius;
            // 敌方集中在 r ≤ -n+2 的内圈
            // 玩家分散在 r > -n+2 的剩余区域，且 elevation ≥ 1
            foreach (var coord in md.IterateCoords())
            {
                if (!IsDeployable(md.Cells, coord)) continue;
                if (coord.Y <= -n + 2)
                {
                    enemy.Add(coord);
                }
                else
                {
                    var cellData = (BattleCellData)md.Cells[Variant.From(coord)];
                    if (cellData.elevation >= 1 && !cellData.blocksLineOfSight)
                        player.Add(coord);
                }
            }

            if (player.Count < 4)
            {
                foreach (var coord in md.IterateCoords())
                {
                    if (!IsDeployable(md.Cells, coord)) continue;
                    if (coord.Y > -n + 2 && !player.Contains(coord)) player.Add(coord);
                }
            }
        }
        else
        {
            for (int q = 1; q < 4; q++)
            {
                int qOffset = (int)Math.Floor(q / 2.0);
                for (int r = -qOffset; r < md.Height - qOffset; r++)
                {
                    var key = new Vector2I(q, r);
                    if (IsDeployable(md.Cells, key)) enemy.Add(key);
                }
            }
            int midQ = md.Width / 2;
            for (int q = midQ - 1; q < md.Width - 1; q++)
            {
                int qOffset = (int)Math.Floor(q / 2.0);
                for (int r = -qOffset; r < md.Height - qOffset; r++)
                {
                    var key = new Vector2I(q, r);
                    if (IsDeployable(md.Cells, key))
                    {
                        var cellData = (BattleCellData)md.Cells[Variant.From(key)];
                        if (cellData.elevation >= 1 && !cellData.blocksLineOfSight)
                            player.Add(key);
                    }
                }
            }
            if (player.Count < 4)
            {
                for (int q = midQ - 1; q < md.Width - 1; q++)
                {
                    int qOffset = (int)Math.Floor(q / 2.0);
                    for (int r = -qOffset; r < md.Height - qOffset; r++)
                    {
                        var key = new Vector2I(q, r);
                        if (IsDeployable(md.Cells, key) && !player.Contains(key)) player.Add(key);
                    }
                }
            }
        }

        return new Godot.Collections.Dictionary { { "player", player }, { "enemy", enemy } };
    }

    /// <summary>玩家被伏击：玩家集中混乱，敌人分散有利</summary>
    private static Godot.Collections.Dictionary GenerateAmbushed(BladeHex.Map.BattleMapGenerator.BattleMapData md)
    {
        var player = new Godot.Collections.Array<Vector2I>();
        var enemy = new Godot.Collections.Array<Vector2I>();

        if (md.HexRadius > 0)
        {
            int n = md.HexRadius;
            foreach (var coord in md.IterateCoords())
            {
                if (!IsDeployable(md.Cells, coord)) continue;
                if (coord.Y <= -n + 2)
                {
                    player.Add(coord);
                }
                else
                {
                    var cellData = (BattleCellData)md.Cells[Variant.From(coord)];
                    if (cellData.elevation >= 1 && !cellData.blocksLineOfSight)
                        enemy.Add(coord);
                }
            }
            if (enemy.Count < 4)
            {
                foreach (var coord in md.IterateCoords())
                {
                    if (!IsDeployable(md.Cells, coord)) continue;
                    if (coord.Y > -n + 2 && !enemy.Contains(coord)) enemy.Add(coord);
                }
            }
        }
        else
        {
            for (int q = 0; q < 3; q++)
            {
                int qOffset = (int)Math.Floor(q / 2.0);
                for (int r = -qOffset; r < md.Height - qOffset; r++)
                {
                    var key = new Vector2I(q, r);
                    if (IsDeployable(md.Cells, key)) player.Add(key);
                }
            }
            int midQ = md.Width / 2;
            for (int q = midQ - 1; q < md.Width; q++)
            {
                int qOffset = (int)Math.Floor(q / 2.0);
                for (int r = -qOffset; r < md.Height - qOffset; r++)
                {
                    var key = new Vector2I(q, r);
                    if (IsDeployable(md.Cells, key))
                    {
                        var cellData = (BattleCellData)md.Cells[Variant.From(key)];
                        if (cellData.elevation >= 1 && !cellData.blocksLineOfSight)
                            enemy.Add(key);
                    }
                }
            }
            if (enemy.Count < 4)
            {
                for (int q = midQ - 1; q < md.Width; q++)
                {
                    int qOffset = (int)Math.Floor(q / 2.0);
                    for (int r = -qOffset; r < md.Height - qOffset; r++)
                    {
                        var key = new Vector2I(q, r);
                        if (IsDeployable(md.Cells, key) && !enemy.Contains(key)) enemy.Add(key);
                    }
                }
            }
        }

        return new Godot.Collections.Dictionary { { "player", player }, { "enemy", enemy } };
    }

    private static bool IsDeployable(Godot.Collections.Dictionary cells, Vector2I key)
    {
        var v = Variant.From(key);
        if (!cells.ContainsKey(v)) return false;
        var cellData = (BattleCellData)cells[v];
        if (!cellData.isPassable) return false;
        if (cellData.terrainType == BattleCellData.TerrainType.DeepWater) return false;
        if (cellData.terrainType == BattleCellData.TerrainType.Wall) return false;
        return true;
    }

    // ========================================================================
    // Backward-compatible signature — 旧调用方仍传 width/height
    // ========================================================================

    [System.Obsolete("Use GenerateZones(BattleMapData, EngagementType) for hex-shape support")]
    public static Godot.Collections.Dictionary GenerateZones(
        int mapWidth,
        int mapHeight,
        BattleContext.EngagementType engagement,
        Godot.Collections.Dictionary cells)
    {
        // 构造一个临时 BattleMapData 让新签名复用
        var md = new BladeHex.Map.BattleMapGenerator.BattleMapData
        {
            Width = mapWidth,
            Height = mapHeight,
            HexRadius = 0,
            Cells = cells,
        };
        return GenerateZones(md, engagement);
    }
}
