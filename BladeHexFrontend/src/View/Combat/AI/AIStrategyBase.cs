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
    // ========================================
    // 战斗常量
    // ========================================
    
    /// <summary>基础视野距离（格）</summary>
    protected const int BaseMaxVision = 8;
    /// <summary>高地视野加成</summary>
    protected const int HighGroundVisionBonus = 1;

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
        var forcedTarget = BuffTargetingRules.ResolveForcedTarget(actor, playerUnits);
        if (forcedTarget != null
            && BuffTargetingRules.IsDirectlyTargetable(forcedTarget)
            && !BuffTargetingRules.ShouldAiIgnore(forcedTarget))
        {
            targets.RemoveAll(t => t.Unit != forcedTarget);
            if (targets.Count == 0) targets.Add(new AITargetEvaluator.ScoredTarget { Unit = forcedTarget, Score = 9999f });
        }
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
        if (actor.Data!.enemyType == UnitData.EnemyType.Undead) return null;

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
        if (actor.Data!.enemyType == UnitData.EnemyType.Undead) return null;
        if (actor.Data.aiStrategy == UnitData.AIStrategy.Reckless) return null;

        float hpPct = (float)actor.CurrentHp / Math.Max(actor.Model.GetMaxHp(), 1);
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
            Description = $"{actor.Data!.UnitName} 待机。"
        };
    }

    /// <summary>创建攻击行动（通用辅助方法）</summary>
    protected AIAction CreateAttackAction(Unit actor, Unit target, HexGrid hexGrid)
    {
        var action = new AIAction
        {
            Actor = actor,
            TargetUnit = target,
            TargetPosition = actor.GridPos,
            AttackPosition = actor.GridPos,
        };

        var weapon = actor.Model.GetMainHand() as WeaponData;
        int weaponRange = weapon?.RangeCells ?? 1;

        // 视野限制：最大攻击距离 = min(武器射程, maxVision)，高地+1
        int maxVision = BaseMaxVision;
        var actorCell = hexGrid.GetCell(actor.GridPos.X, actor.GridPos.Y);
        if (actorCell != null && actorCell.Elevation >= 2)
            maxVision = BaseMaxVision + HighGroundVisionBonus;
        int atkRange = Math.Min(weaponRange, maxVision);

        int dist = HexUtils.Distance(actor.GridPos.X, actor.GridPos.Y, target.GridPos.X, target.GridPos.Y);

        if (dist <= atkRange)
        {
            action.Type = AIAction.ActionType.Attack;
            action.AttackPosition = actor.GridPos;
        }
        else
        {
            var bestPos = FindBestAttackPosition(actor, target, hexGrid);
            action = BuildMoveThenAttackOrMoveOnly(
                actor,
                target,
                bestPos,
                hexGrid,
                $"{actor.Data!.UnitName} 攻击 {target.Data!.UnitName}",
                $"{actor.Data!.UnitName} 接近 {target.Data!.UnitName}");

            if (action.MovePath.Count >= 3 && DifficultyConfig.UsesCharge)
            {
                action.IsCharge = AISpatialAnalyzer.CanCharge(hexGrid, action.MovePath, actor.GridPos);
            }
        }

        return action;
    }

    /// <summary>寻找最佳攻击位置（综合评估掩体、高程、包夹）</summary>
    protected Vector2I FindBestAttackPosition(Unit actor, Unit target, HexGrid hexGrid)
    {
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int weaponRange = weapon?.RangeCells ?? 1;

        // 视野限制
        int maxVision = BaseMaxVision;
        int atkRange = Math.Min(weaponRange, maxVision);

        // 预留攻击 AP 后再计算移动可达范围，避免移动后无 AP 攻击
        int attackApCost = weapon?.ApCost ?? 4;
        float moveBudget = Math.Max(0.0f, actor.CurrentAp - attackApCost);
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveBudget);

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
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int weaponRange = weapon?.RangeCells ?? 1;
        int maxVision = BaseMaxVision;
        int atkRange = Math.Min(weaponRange, maxVision);

        int attackApCost = weapon?.ApCost ?? 4;
        float moveBudget = Math.Max(0.0f, actor.CurrentAp - attackApCost);
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveBudget);

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

    /// <summary>无法在本次移动后攻击时，选择一个向目标逼近的可行位置。</summary>
    protected Vector2I FindNearestApproachPosition(Unit actor, Unit target, HexGrid hexGrid, float moveBudget)
    {
        if (moveBudget <= 0.0f) return actor.GridPos;

        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveBudget);
        Vector2I bestPos = actor.GridPos;
        int bestDist = HexUtils.Distance(actor.GridPos.X, actor.GridPos.Y, target.GridPos.X, target.GridPos.Y);
        float bestPathCost = 0.0f;

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || (cell.Occupant != null && cell.Occupant != actor)) continue;

            int dist = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
            if (dist > bestDist) continue;

            var path = hexGrid.FindPath(actor.GridPos, pos);
            float pathCost = hexGrid.GetPathCost(actor.GridPos, path);
            if (path.Count == 0 || pathCost > moveBudget) continue;

            bool strictlyCloser = dist < bestDist;
            bool sameDistanceMoreCommitment = dist == bestDist && pathCost > bestPathCost;
            if (strictlyCloser || sameDistanceMoreCommitment)
            {
                bestDist = dist;
                bestPathCost = pathCost;
                bestPos = pos;
            }
        }

        return bestPos;
    }

    protected int GetAttackApCost(Unit actor)
    {
        var weapon = actor.Model.GetMainHand() as WeaponData;
        return weapon?.ApCost ?? 4;
    }

    protected float GetMoveBudgetAfterAttack(Unit actor)
    {
        return Math.Max(0.0f, actor.CurrentAp - GetAttackApCost(actor));
    }

    protected bool CanAffordMoveThenAttack(Unit actor, HexGrid hexGrid, List<Vector2I> path)
    {
        if (path == null)
            return false;

        if (path.Count == 0)
            return actor.CurrentAp >= GetAttackApCost(actor);

        float moveCost = hexGrid.GetPathCost(actor.GridPos, path);
        return moveCost + GetAttackApCost(actor) <= actor.CurrentAp;
    }

    protected bool CanAttackFrom(Unit actor, Unit target, HexGrid hexGrid, Vector2I fromPos)
    {
        if (!BuffTargetingRules.IsDirectlyTargetable(target)) return false;
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int weaponRange = weapon?.RangeCells ?? 1;
        int maxVision = BaseMaxVision;
        var fromCell = hexGrid.GetCell(fromPos.X, fromPos.Y);
        if (fromCell != null && fromCell.Elevation >= 2)
            maxVision = BaseMaxVision + HighGroundVisionBonus;
        int atkRange = Math.Min(weaponRange, maxVision);
        int dist = HexUtils.Distance(fromPos.X, fromPos.Y, target.GridPos.X, target.GridPos.Y);
        return dist <= atkRange;
    }

    protected AIAction BuildMoveThenAttackOrMoveOnly(Unit actor, Unit target, Vector2I desiredPos, HexGrid hexGrid, string attackDescription, string moveOnlyDescription)
    {
        if (desiredPos == actor.GridPos)
        {
            if (CanAttackFrom(actor, target, hexGrid, actor.GridPos) && actor.CurrentAp >= GetAttackApCost(actor))
            {
                return new AIAction
                {
                    Type = AIAction.ActionType.Attack,
                    Actor = actor,
                    TargetUnit = target,
                    TargetPosition = actor.GridPos,
                    AttackPosition = actor.GridPos,
                    Description = attackDescription
                };
            }
        }

        var path = hexGrid.FindPath(actor.GridPos, desiredPos);
        if (path.Count > 0 && CanAttackFrom(actor, target, hexGrid, desiredPos) && CanAffordMoveThenAttack(actor, hexGrid, path))
        {
            return new AIAction
            {
                Type = AIAction.ActionType.MoveThenAttack,
                Actor = actor,
                TargetUnit = target,
                TargetPosition = desiredPos,
                AttackPosition = desiredPos,
                MovePath = path,
                Description = attackDescription
            };
        }

        float moveOnlyBudget = Math.Max(0, actor.CurrentAp);
        var approachPos = FindNearestApproachPosition(actor, target, hexGrid, moveOnlyBudget);
        var approachPath = approachPos == actor.GridPos ? new List<Vector2I>() : hexGrid.FindPath(actor.GridPos, approachPos);
        return new AIAction
        {
            Type = approachPath.Count > 0 ? AIAction.ActionType.MoveOnly : AIAction.ActionType.Idle,
            Actor = actor,
            TargetUnit = target,
            TargetPosition = approachPos,
            AttackPosition = approachPos,
            MovePath = approachPath,
            Description = approachPath.Count > 0 ? moveOnlyDescription : $"{actor.Data!.UnitName} 行动力不足，无法接近 {target.Data!.UnitName}"
        };
    }
}
