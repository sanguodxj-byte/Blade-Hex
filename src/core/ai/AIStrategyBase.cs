using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.AI;

/// <summary>
/// AI策略基类 —— 定义决策模板方法，子类覆盖核心策略逻辑
/// 对应策划案 09-AI系统 → 一、策略模式架构
/// </summary>
public abstract class AIStrategyBase
{
    protected readonly AIDifficultyConfig DifficultyConfig;
    protected readonly AITargetEvaluator TargetEvaluator;
    protected readonly Random Rand = new();

    protected AIStrategyBase(AIDifficultyConfig config)
    {
        DifficultyConfig = config;
        TargetEvaluator = new AITargetEvaluator(config);
    }

    /// <summary>主入口：决定本回合行为，返回 AIAction</summary>
    public AIAction DecideAction(Unit actor, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        if (actor.Data == null) return DecideIdleAction(actor, hexGrid);

        // 第1步：士气强制行为（溃逃等）
        var moraleLevel = actor.Data.GetMoraleLevel();
        var forced = CheckMoraleOverride(actor, moraleLevel, playerUnits, hexGrid);
        if (forced != null) return forced;

        // 第2步：HP过低撤退检查
        var retreat = CheckRetreat(actor, playerUnits, hexGrid);
        if (retreat != null) return retreat;

        // 第3步：评估目标
        var targets = TargetEvaluator.EvaluateTargets(actor, playerUnits, hexGrid, enemyUnits);
        if (targets.Count == 0) return DecideIdleAction(actor, hexGrid);

        // 第4步：策略特定决策（子类实现）
        return DecideStrategyAction(actor, targets, playerUnits, enemyUnits, hexGrid);
    }

    /// <summary>核心策略逻辑，由子类实现</summary>
    protected abstract AIAction DecideStrategyAction(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid);

    /// <summary>士气强制行为检查</summary>
    protected virtual AIAction? CheckMoraleOverride(Unit actor, UnitData.MoraleLevel moraleLevel, List<Unit> playerUnits, HexGrid hexGrid)
    {
        // 亡灵永远不会因士气溃逃
        if (actor.Data.enemyType == UnitData.EnemyType.Undead) return null;

        if (moraleLevel == UnitData.MoraleLevel.Routing)
        {
            return new AIAction
            {
                Type = AIAction.ActionType.Retreat,
                Actor = actor,
                TargetPosition = AISpatialAnalyzer.FindRetreatPosition(hexGrid, actor, playerUnits),
                Description = $"{actor.Data.UnitName} 士气崩溃，正在溃逃！",
                PriorityScore = 100.0f
            };
        }

        return null;
    }

    /// <summary>HP过低撤退检查</summary>
    protected virtual AIAction? CheckRetreat(Unit actor, List<Unit> playerUnits, HexGrid hexGrid)
    {
        if (actor.Data.enemyType == UnitData.EnemyType.Undead) return null;
        if (actor.Data.aiStrategy == UnitData.AIStrategy.Reckless) return null;

        float hpPct = (float)actor.CurrentHp / Math.Max(actor.GetMaxHp(), 1);
        float threshold = 0.25f * DifficultyConfig.RetreatThresholdMultiplier;

        if (hpPct <= threshold)
        {
            // 士气崩溃时必定撤退，否则50%概率
            var moraleLevel = actor.Data.GetMoraleLevel();
            if (moraleLevel >= UnitData.MoraleLevel.Broken || Rand.NextDouble() < 0.5)
            {
                return new AIAction
                {
                    Type = AIAction.ActionType.Retreat,
                    Actor = actor,
                    TargetPosition = AISpatialAnalyzer.FindRetreatPosition(hexGrid, actor, playerUnits),
                    Description = $"{actor.Data.UnitName} 受到重创，正在撤退！",
                    PriorityScore = 90.0f
                };
            }
        }

        return null;
    }

    /// <summary>默认待机行为</summary>
    protected AIAction DecideIdleAction(Unit actor, HexGrid hexGrid)
    {
        return new AIAction
        {
            Type = AIAction.ActionType.Idle,
            Actor = actor,
            Description = $"{actor.Data.UnitName} 待机。"
        };
    }

    /// <summary>创建攻击行动（通用辅助方法）</summary>
    protected AIAction CreateAttackAction(Unit actor, Unit target, HexGrid hexGrid)
    {
        var action = new AIAction
        {
            Actor = actor,
            TargetUnit = target
        };

        var weapon = actor.GetMainHand() as WeaponData;
        int atkRange = weapon?.RangeCells ?? 1;
        int dist = HexUtils.Distance(actor.GridPos.X, actor.GridPos.Y, target.GridPos.X, target.GridPos.Y);

        if (dist <= atkRange)
        {
            action.Type = AIAction.ActionType.Attack;
            action.AttackPosition = actor.GridPos;
        }
        else
        {
            action.Type = AIAction.ActionType.MoveThenAttack;
            var bestPos = FindBestAttackPosition(actor, target, hexGrid);
            action.TargetPosition = bestPos;
            action.AttackPosition = bestPos;
            var path = hexGrid.FindPath(actor.GridPos, bestPos);
            action.MovePath = path;

            if (path.Count >= 3 && DifficultyConfig.UsesCharge)
            {
                action.IsCharge = AISpatialAnalyzer.CanCharge(hexGrid, path, actor.GridPos);
            }
        }

        action.Description = $"{actor.Data.UnitName} 攻击 {target.Data.UnitName}";
        return action;
    }

    /// <summary>寻找最佳攻击位置（综合评估掩体、高程、包夹）</summary>
    protected Vector2I FindBestAttackPosition(Unit actor, Unit target, HexGrid hexGrid)
    {
        var weapon = actor.GetMainHand() as WeaponData;
        int atkRange = weapon?.RangeCells ?? 1;
        int moveRange = actor.GetMoveRange();
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveRange);

        Vector2I bestPos = actor.GridPos;
        float bestScore = -999.0f;

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || (cell.Occupant != null && cell.Occupant != actor)) continue;

            int distToTarget = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
            if (distToTarget > atkRange) continue;

            float score = 0.0f;
            if (cell.Data != null) score += Math.Max(0, cell.Data.acBonus) * 2.0f;
            score += cell.CoverType * 3.0f;

            int elevAdv = AISpatialAnalyzer.GetElevationAdvantage(hexGrid, pos, target.GridPos);
            if (elevAdv > 0) score += 5.0f;
            else if (elevAdv < 0) score -= 3.0f;

            if (DifficultyConfig.UsesFlanking)
            {
                int facing = AISpatialAnalyzer.GetAttackFacing(pos, target.GridPos, -1);
                if (facing == 2) score += 8.0f;
                else if (facing == 1) score += 4.0f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestPos = pos;
            }
        }

        return bestPos;
    }

    /// <summary>寻找最近的可行攻击位置</summary>
    protected Vector2I FindNearestAttackPosition(Unit actor, Unit target, HexGrid hexGrid)
    {
        var weapon = actor.GetMainHand() as WeaponData;
        int atkRange = weapon?.RangeCells ?? 1;
        int moveRange = actor.GetMoveRange();
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveRange);

        Vector2I bestPos = actor.GridPos;
        int bestDist = 999;

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || (cell.Occupant != null && cell.Occupant != actor)) continue;

            int distToTarget = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
            if (distToTarget <= atkRange)
            {
                int moveDist = HexUtils.Distance(actor.GridPos.X, actor.GridPos.Y, pos.X, pos.Y);
                if (moveDist < bestDist)
                {
                    bestDist = moveDist;
                    bestPos = pos;
                }
            }
        }

        return bestPos;
    }
}
