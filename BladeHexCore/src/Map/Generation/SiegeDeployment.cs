// SiegeDeployment.cs
// 攻城战部署区生成 — 守方在城墙上/城内，攻方在城墙外
//
// 规则：
//   - 守方部署区：城墙格（Rampart/Tower）+ 城内格（楼梯内侧的可通行格）
//   - 攻方部署区：城墙外、距城墙 3-6 格的可通行格
//   - 攻城战中玩家是攻方（AttackingSide=Player）→ 玩家部署在城外
//   - 守城战中玩家是守方（AttackingSide=Enemy）→ 玩家部署在城内
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Map.Generation;

public static class SiegeDeployment
{
    /// <summary>
    /// 生成攻城战部署区
    /// </summary>
    /// <returns>Dictionary with "player" and "enemy" keys</returns>
    public static Godot.Collections.Dictionary GenerateZones(
        BattleMapGenerator.BattleMapData md,
        BattleContext.BattleSide attackingSide)
    {
        var attackerZone = new Godot.Collections.Array<Vector2I>();
        var defenderZone = new Godot.Collections.Array<Vector2I>();

        // 收集城墙格和城内格
        var wallCells = new HashSet<Vector2I>();
        var innerCells = new HashSet<Vector2I>();

        // 第一遍：找所有城墙格
        foreach (Variant keyV in md.Cells.Keys)
        {
            var key = keyV.AsVector2I();
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null) continue;

            if (cd.terrainType == BattleCellData.TerrainType.Rampart
                || cd.terrainType == BattleCellData.TerrainType.Tower
                || cd.terrainType == BattleCellData.TerrainType.Gate
                || cd.terrainType == BattleCellData.TerrainType.Staircase)
            {
                wallCells.Add(key);
            }
        }

        // 第二遍：城内 = 城墙内侧的 Road 格（由 StrongholdPlacer 铺设）
        foreach (Variant keyV in md.Cells.Keys)
        {
            var key = keyV.AsVector2I();
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null || !cd.isPassable) continue;
            if (wallCells.Contains(key)) continue;

            // 城内判定：与城墙相邻且是 Road 类型，或者距城墙 1-2 格内的 Road
            if (cd.terrainType == BattleCellData.TerrainType.Road)
            {
                bool nearWall = false;
                for (int d = 0; d < 6; d++)
                {
                    var nb = HexUtils.GetNeighbor(key.X, key.Y, d);
                    if (wallCells.Contains(nb)) { nearWall = true; break; }
                    // 也检查 2 格距离
                    for (int d2 = 0; d2 < 6; d2++)
                    {
                        var nb2 = HexUtils.GetNeighbor(nb.X, nb.Y, d2);
                        if (wallCells.Contains(nb2)) { nearWall = true; break; }
                    }
                    if (nearWall) break;
                }
                if (nearWall) innerCells.Add(key);
            }
        }

        // 守方部署区：城墙上（Rampart/Tower 可站人）+ 城内格
        foreach (Variant keyV in md.Cells.Keys)
        {
            var key = keyV.AsVector2I();
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null || !cd.isPassable) continue;

            if (wallCells.Contains(key) && cd.terrainType != BattleCellData.TerrainType.Gate)
            {
                // 城墙上可部署（Rampart/Tower/Staircase）
                defenderZone.Add(key);
            }
            else if (innerCells.Contains(key))
            {
                defenderZone.Add(key);
            }
        }

        // 攻方部署区：城墙外、距城墙 3-6 格的可通行格
        foreach (Variant keyV in md.Cells.Keys)
        {
            var key = keyV.AsVector2I();
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null || !cd.isPassable) continue;
            if (wallCells.Contains(key) || innerCells.Contains(key)) continue;

            // 计算到最近城墙的距离
            int minDist = int.MaxValue;
            foreach (var w in wallCells)
            {
                int dist = HexUtils.AxialDistance(key, w);
                if (dist < minDist) minDist = dist;
            }

            // 距城墙 3-6 格
            if (minDist >= 3 && minDist <= 6)
                attackerZone.Add(key);
        }

        // 兜底：如果某方部署区太少，放宽条件
        if (defenderZone.Count < 4)
        {
            foreach (var pos in innerCells)
                if (!defenderZone.Contains(pos)) defenderZone.Add(pos);
        }
        if (attackerZone.Count < 4)
        {
            // 放宽到距城墙 2-8 格
            foreach (Variant keyV in md.Cells.Keys)
            {
                var key = keyV.AsVector2I();
                var cd = md.Cells[keyV].As<BattleCellData>();
                if (cd == null || !cd.isPassable) continue;
                if (wallCells.Contains(key) || innerCells.Contains(key)) continue;
                if (attackerZone.Contains(key)) continue;
                int minDist = int.MaxValue;
                foreach (var w in wallCells)
                {
                    int dist = HexUtils.AxialDistance(key, w);
                    if (dist < minDist) minDist = dist;
                }
                if (minDist >= 2 && minDist <= 8)
                    attackerZone.Add(key);
            }
        }

        // 根据 AttackingSide 决定谁是玩家
        var result = new Godot.Collections.Dictionary();
        if (attackingSide == BattleContext.BattleSide.Player)
        {
            // 玩家攻城：玩家在城外，敌人在城内
            result["player"] = attackerZone;
            result["enemy"] = defenderZone;
        }
        else
        {
            // 玩家守城：玩家在城内，敌人在城外
            result["player"] = defenderZone;
            result["enemy"] = attackerZone;
        }

        return result;
    }
}
