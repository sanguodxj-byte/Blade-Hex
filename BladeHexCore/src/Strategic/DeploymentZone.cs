using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 部署区域生成器 — 根据战斗规模和交战类型生成双方部署坐标
/// </summary>
public static class DeploymentZone
{
    /// <summary>
    /// 根据战斗规模和交战类型生成部署区域
    /// </summary>
    /// <param name="mapWidth">战场宽度</param>
    /// <param name="mapHeight">战场高度</param>
    /// <param name="engagement">交战类型 (BattleContext.EngagementType)</param>
    /// <param name="cells">地图格子数据 (Vector2I -> BattleCellData)</param>
    /// <returns>包含 "player" 和 "enemy" 坐标列表的 Dictionary</returns>
    public static Godot.Collections.Dictionary GenerateZones(
        int mapWidth,
        int mapHeight,
        BattleContext.EngagementType engagement,
        Godot.Collections.Dictionary cells
    )
    {
        return engagement switch
        {
            BattleContext.EngagementType.Normal => GenerateNormal(mapWidth, mapHeight, cells),
            BattleContext.EngagementType.Ambush => GenerateAmbush(mapWidth, mapHeight, cells),
            BattleContext.EngagementType.Ambushed => GenerateAmbushed(mapWidth, mapHeight, cells),
            _ => GenerateNormal(mapWidth, mapHeight, cells)
        };
    }

    /// <summary>正常遭遇：玩家在左2行，敌人在右2行</summary>
    private static Godot.Collections.Dictionary GenerateNormal(int mapWidth, int mapHeight, Godot.Collections.Dictionary cells)
    {
        var player = new Godot.Collections.Array<Vector2I>();
        var enemy = new Godot.Collections.Array<Vector2I>();

        // 玩家部署区：q=0 和 q=1 的可通行格子
        for (int q = 0; q < 2; q++)
        {
            int qOffset = (int)Math.Floor(q / 2.0);
            for (int r = -qOffset; r < mapHeight - qOffset; r++)
            {
                var key = new Vector2I(q, r);
                if (IsDeployable(cells, key)) player.Add(key);
            }
        }

        // 敌方部署区：q=width-2 和 q=width-1 的可通行格子
        for (int q = mapWidth - 2; q < mapWidth; q++)
        {
            int qOffset = (int)Math.Floor(q / 2.0);
            for (int r = -qOffset; r < mapHeight - qOffset; r++)
            {
                var key = new Vector2I(q, r);
                if (IsDeployable(cells, key)) enemy.Add(key);
            }
        }

        return new Godot.Collections.Dictionary { { "player", player }, { "enemy", enemy } };
    }

    /// <summary>玩家伏击敌人：玩家分散在有利位置，敌人集中在一侧</summary>
    private static Godot.Collections.Dictionary GenerateAmbush(int mapWidth, int mapHeight, Godot.Collections.Dictionary cells)
    {
        var player = new Godot.Collections.Array<Vector2I>();
        var enemy = new Godot.Collections.Array<Vector2I>();

        // 敌方被伏击：集中部署在左侧狭小区域（q=1-3）
        for (int q = 1; q < 4; q++)
        {
            int qOffset = (int)Math.Floor(q / 2.0);
            for (int r = -qOffset; r < mapHeight - qOffset; r++)
            {
                var key = new Vector2I(q, r);
                if (IsDeployable(cells, key)) enemy.Add(key);
            }
        }

        // 玩家伏击方：分散在地图中间和右侧的有利位置
        int midQ = mapWidth / 2;
        for (int q = midQ - 1; q < mapWidth - 1; q++)
        {
            int qOffset = (int)Math.Floor(q / 2.0);
            for (int r = -qOffset; r < mapHeight - qOffset; r++)
            {
                var key = new Vector2I(q, r);
                if (IsDeployable(cells, key))
                {
                    var cellData = (BattleCellData)cells[key];
                    if (cellData.elevation >= 1 && !cellData.blocksLineOfSight)
                        player.Add(key);
                }
            }
        }

        // 补充位
        if (player.Count < 4)
        {
            for (int q = midQ - 1; q < mapWidth - 1; q++)
            {
                int qOffset = (int)Math.Floor(q / 2.0);
                for (int r = -qOffset; r < mapHeight - qOffset; r++)
                {
                    var key = new Vector2I(q, r);
                    if (IsDeployable(cells, key) && !player.Contains(key))
                        player.Add(key);
                }
            }
        }

        return new Godot.Collections.Dictionary { { "player", player }, { "enemy", enemy } };
    }

    /// <summary>玩家被伏击：玩家集中在一侧阵型混乱，敌人分散在有利位置</summary>
    private static Godot.Collections.Dictionary GenerateAmbushed(int mapWidth, int mapHeight, Godot.Collections.Dictionary cells)
    {
        var player = new Godot.Collections.Array<Vector2I>();
        var enemy = new Godot.Collections.Array<Vector2I>();

        // 玩家被伏击：集中部署在左侧狭小区域（q=0-2）
        for (int q = 0; q < 3; q++)
        {
            int qOffset = (int)Math.Floor(q / 2.0);
            for (int r = -qOffset; r < mapHeight - qOffset; r++)
            {
                var key = new Vector2I(q, r);
                if (IsDeployable(cells, key)) player.Add(key);
            }
        }

        // 敌方伏击方：分散在地图中间和右侧的有利位置
        int midQ = mapWidth / 2;
        for (int q = midQ - 1; q < mapWidth; q++)
        {
            int qOffset = (int)Math.Floor(q / 2.0);
            for (int r = -qOffset; r < mapHeight - qOffset; r++)
            {
                var key = new Vector2I(q, r);
                if (IsDeployable(cells, key))
                {
                    var cellData = (BattleCellData)cells[key];
                    if (cellData.elevation >= 1 && !cellData.blocksLineOfSight)
                        enemy.Add(key);
                }
            }
        }

        // 补充位
        if (enemy.Count < 4)
        {
            for (int q = midQ - 1; q < mapWidth; q++)
            {
                int qOffset = (int)Math.Floor(q / 2.0);
                for (int r = -qOffset; r < mapHeight - qOffset; r++)
                {
                    var key = new Vector2I(q, r);
                    if (IsDeployable(cells, key) && !enemy.Contains(key))
                        enemy.Add(key);
                }
            }
        }

        return new Godot.Collections.Dictionary { { "player", player }, { "enemy", enemy } };
    }

    private static bool IsDeployable(Godot.Collections.Dictionary cells, Vector2I key)
    {
        if (!cells.ContainsKey(key)) return false;
        var cellData = (BattleCellData)cells[key];
        if (!cellData.isPassable) return false;
        if (cellData.terrainType == BattleCellData.TerrainType.DeepWater) return false;
        if (cellData.terrainType == BattleCellData.TerrainType.Wall) return false;
        return true;
    }
}
