// AISiegeEvaluator.cs
// AI 攻守城决策评估器 — 在常规战斗 AI 之上叠加攻城行为
//
// 设计：
//   - 不替换现有 AI 策略，而是作为前置检查插入 DecideAction 流程
//   - 攻方 AI：优先攻击城门 > 架设云梯 > 常规战斗
//   - 守方 AI：优先占据城墙高地 > 保护城门 > 常规战斗
//   - 只在检测到地图有城墙结构时激活
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using Godot;

namespace BladeHex.Combat.AI;

/// <summary>
/// 攻城 AI 评估器 — 为攻/守方提供攻城专用行动建议
/// </summary>
public static class AISiegeEvaluator
{
    /// <summary>检测地图是否有城墙结构（激活攻城 AI 的前提）</summary>
    public static bool HasSiegeStructures(HexGrid hexGrid)
    {
        foreach (var kvp in hexGrid.Cells)
        {
            if (kvp.Value.Data == null) continue;
            var t = kvp.Value.Data.terrainType;
            if (t == BattleCellData.TerrainType.Rampart
                || t == BattleCellData.TerrainType.Gate
                || t == BattleCellData.TerrainType.Tower)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 攻方 AI：尝试生成攻城行动（优先于常规战斗）
    /// 返回 null 表示不需要攻城行动，走常规 AI
    /// </summary>
    public static AIAction? EvaluateAttackerAction(
        Unit actor, List<Unit> playerUnits, HexGrid hexGrid)
    {
        if (actor.Data == null) return null;

        // 优先级 1：攻击城门（如果在范围内且城门未破）
        var gateAction = TryAttackGate(actor, hexGrid);
        if (gateAction != null) return gateAction;

        // 优先级 2：架设云梯（如果在城墙旁且有足够 AP）
        var ladderAction = TryBuildLadder(actor, hexGrid);
        if (ladderAction != null) return ladderAction;

        // 优先级 3：向最近的城门/城墙移动（如果没有可攻击的敌人在视野内）
        bool hasVisibleEnemy = HasVisibleEnemy(actor, playerUnits, hexGrid);
        if (!hasVisibleEnemy)
        {
            var approachAction = TryApproachWall(actor, hexGrid);
            if (approachAction != null) return approachAction;
        }

        // 无攻城行动，走常规 AI
        return null;
    }

    /// <summary>
    /// 守方 AI：尝试生成防守行动（优先于常规战斗）
    /// 返回 null 表示不需要特殊防守行动，走常规 AI
    /// </summary>
    public static AIAction? EvaluateDefenderAction(
        Unit actor, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        if (actor.Data == null) return null;

        // 优先级 1：如果不在城墙/塔楼上，移动到最近的城墙位置
        var cell = hexGrid.GetCell(actor.GridPos.X, actor.GridPos.Y);
        if (cell?.Data != null)
        {
            var t = cell.Data.terrainType;
            bool onWall = t == BattleCellData.TerrainType.Rampart
                || t == BattleCellData.TerrainType.Tower;

            if (!onWall)
            {
                var wallAction = TryMoveToWall(actor, hexGrid);
                if (wallAction != null) return wallAction;
            }
        }

        // 在城墙上时，走常规 AI（利用高地优势攻击）
        return null;
    }

    // ========================================================================
    // 攻方行动
    // ========================================================================

    /// <summary>尝试攻击相邻城门</summary>
    private static AIAction? TryAttackGate(Unit actor, HexGrid hexGrid)
    {
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int apCost = weapon?.ApCost ?? 4;
        if (actor.CurrentAp < apCost) return null;

        // 检查 6 个邻居是否有可破坏城门
        for (int d = 0; d < 6; d++)
        {
            var nb = HexUtils.GetNeighbor(actor.GridPos.X, actor.GridPos.Y, d);
            var nbCell = hexGrid.GetCell(nb.X, nb.Y);
            if (nbCell?.Data == null) continue;
            if (!nbCell.Data.isDestructible || nbCell.Data.durability <= 0) continue;

            return new AIAction
            {
                Type = AIAction.ActionType.UseSkill,
                Actor = actor,
                TargetPosition = nb,
                SkillId = "siege_attack_gate",
                Description = $"{actor.Data!.UnitName} 攻击城门",
                PriorityScore = 80.0f,
            };
        }
        return null;
    }

    /// <summary>尝试在相邻城墙架设云梯</summary>
    private static AIAction? TryBuildLadder(Unit actor, HexGrid hexGrid)
    {
        if (actor.CurrentAp < SiegeActions.LadderApCost) return null;

        for (int d = 0; d < 6; d++)
        {
            var nb = HexUtils.GetNeighbor(actor.GridPos.X, actor.GridPos.Y, d);
            var nbCell = hexGrid.GetCell(nb.X, nb.Y);
            if (nbCell?.Data == null) continue;
            if (nbCell.Data.terrainType != BattleCellData.TerrainType.Rampart) continue;
            if (nbCell.Data.HasLadder) continue;

            return new AIAction
            {
                Type = AIAction.ActionType.UseSkill,
                Actor = actor,
                TargetPosition = nb,
                SkillId = "siege_build_ladder",
                Description = $"{actor.Data!.UnitName} 架设云梯 ({nbCell.Data.ladderProgress}/{SiegeActions.LadderRequiredSteps})",
                PriorityScore = 70.0f,
            };
        }
        return null;
    }

    /// <summary>向最近的城门或城墙移动</summary>
    private static AIAction? TryApproachWall(Unit actor, HexGrid hexGrid)
    {
        Vector2I? nearestTarget = null;
        int nearestDist = int.MaxValue;

        foreach (var kvp in hexGrid.Cells)
        {
            if (kvp.Value.Data == null) continue;
            var t = kvp.Value.Data.terrainType;
            // 优先城门，其次城墙
            bool isTarget = (t == BattleCellData.TerrainType.Gate && kvp.Value.Data.durability > 0)
                || (t == BattleCellData.TerrainType.Rampart && !kvp.Value.Data.HasLadder);
            if (!isTarget) continue;

            int dist = HexUtils.AxialDistance(actor.GridPos, kvp.Key);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestTarget = kvp.Key;
            }
        }

        if (nearestTarget == null || nearestDist <= 1) return null;

        // 找到目标旁边的可通行格作为移动目标
        var moveTarget = FindAdjacentPassable(nearestTarget.Value, hexGrid, actor);
        if (moveTarget == null) return null;

        var path = hexGrid.FindPath(actor.GridPos, moveTarget.Value);
        if (path.Count == 0) return null;

        return new AIAction
        {
            Type = AIAction.ActionType.MoveOnly,
            Actor = actor,
            TargetPosition = moveTarget.Value,
            MovePath = path,
            Description = $"{actor.Data!.UnitName} 向城墙推进",
            PriorityScore = 50.0f,
        };
    }

    // ========================================================================
    // 守方行动
    // ========================================================================

    /// <summary>移动到最近的城墙/塔楼位置</summary>
    private static AIAction? TryMoveToWall(Unit actor, HexGrid hexGrid)
    {
        Vector2I? nearestWall = null;
        int nearestDist = int.MaxValue;

        foreach (var kvp in hexGrid.Cells)
        {
            if (kvp.Value.Data == null) continue;
            var t = kvp.Value.Data.terrainType;
            if (t != BattleCellData.TerrainType.Rampart && t != BattleCellData.TerrainType.Tower) continue;
            if (kvp.Value.Occupant != null) continue; // 已被占据

            int dist = HexUtils.AxialDistance(actor.GridPos, kvp.Key);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestWall = kvp.Key;
            }
        }

        if (nearestWall == null || nearestDist == 0) return null;

        // 城墙本身不可通行（elevation=2），移动到楼梯或城门旁
        var moveTarget = FindAdjacentPassable(nearestWall.Value, hexGrid, actor);
        if (moveTarget == null) return null;

        var path = hexGrid.FindPath(actor.GridPos, moveTarget.Value);
        if (path.Count == 0) return null;

        return new AIAction
        {
            Type = AIAction.ActionType.MoveOnly,
            Actor = actor,
            TargetPosition = moveTarget.Value,
            MovePath = path,
            Description = $"{actor.Data!.UnitName} 移动到城墙防守位置",
            PriorityScore = 60.0f,
        };
    }

    // ========================================================================
    // 辅助
    // ========================================================================

    private static bool HasVisibleEnemy(Unit actor, List<Unit> enemies, HexGrid hexGrid)
    {
        int vision = 8;
        foreach (var enemy in enemies)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;
            int dist = HexUtils.AxialDistance(actor.GridPos, enemy.GridPos);
            if (dist <= vision) return true;
        }
        return false;
    }

    private static Vector2I? FindAdjacentPassable(Vector2I target, HexGrid hexGrid, Unit actor)
    {
        int bestDist = int.MaxValue;
        Vector2I? best = null;

        for (int d = 0; d < 6; d++)
        {
            var nb = HexUtils.GetNeighbor(target.X, target.Y, d);
            var cell = hexGrid.GetCell(nb.X, nb.Y);
            if (cell == null) continue;
            if (cell.Occupant != null && cell.Occupant != actor) continue;
            if (cell.Data != null && !cell.Data.isPassable) continue;

            int dist = HexUtils.AxialDistance(actor.GridPos, nb);
            if (dist < bestDist) { bestDist = dist; best = nb; }
        }
        return best;
    }
}
