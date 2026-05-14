using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.AI;

/// <summary>
/// 战术策略 —— 集火、包夹机动、与友方协同、善用地形
/// 适用于：老兵、指挥官、有组织的敌军
/// </summary>
public class AIStrategyTactical : AIStrategyBase
{
    public AIStrategyTactical(AIDifficultyConfig config) : base(config) { }

    protected override AIAction DecideStrategyAction(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        // 战术策略：选集火目标（友方已经在攻击的目标优先）
        var focusTarget = SelectFocusTarget(scoredTargets, enemyUnits, hexGrid);

        if (focusTarget == null)
        {
            if (scoredTargets.Count == 0) return DecideIdleAction(actor, hexGrid);
            focusTarget = scoredTargets[0].Unit;
        }

        // 尝试找包夹位置
        if (DifficultyConfig.UsesFlanking)
        {
            var flankPositions = AISpatialAnalyzer.FindFlankingPositions(hexGrid, focusTarget, actor, actor.Model.GetMoveRange());
            if (flankPositions.Count > 0)
            {
                foreach (var entry in flankPositions)
                {
                    var flankPos = entry.Position;
                    var flankFacing = entry.Facing;
                    if (IsPositionReachable(actor, flankPos, hexGrid))
                    {
                        return BuildFlankAttackAction(actor, focusTarget, flankPos, flankFacing, hexGrid);
                    }
                }
            }
        }

        // 没有包夹机会，标准攻击（但选择最优位置）
        var action = CreateAttackAction(actor, focusTarget, hexGrid);

        // 战术加成：考虑控制区，避免触发借机攻击
        if (DifficultyConfig.UsesZoneOfControl && action.MovePath.Count > 0)
        {
            int aoos = AISpatialAnalyzer.CountOpportunityAttacks(hexGrid, action.MovePath, playerUnits);
            if (aoos > 0)
            {
                var saferAction = FindSaferApproach(actor, focusTarget, playerUnits, hexGrid);
                if (saferAction != null) return saferAction;
                action.Description = $"{actor.Data!.UnitName} 冒险攻击 {focusTarget.Data!.UnitName}";
                return action;
            }
        }

        action.Description = $"{actor.Data!.UnitName} 协同攻击 {focusTarget.Data!.UnitName}";
        return action;
    }

    private Unit? SelectFocusTarget(List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        if (scoredTargets.Count == 0) return null;

        Unit? bestTarget = null;
        float bestScore = -999.0f;

        foreach (var entry in scoredTargets)
        {
            var target = entry.Unit;
            float baseScore = entry.Score;

            // 集火加成：友方已在该目标附近越多，越值得集火
            int adjacentAllies = AISpatialAnalyzer.CountAdjacentAllies(hexGrid, target.GridPos, enemyUnits);
            float focusBonus = adjacentAllies * 3.0f;

            float totalScore = baseScore + focusBonus;
            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    private bool IsPositionReachable(Unit actor, Vector2I pos, HexGrid hexGrid)
    {
        var cell = hexGrid.GetCell(pos.X, pos.Y);
        if (cell == null || (cell.Occupant != null && cell.Occupant != actor)) return false;
        
        // 使用 AP 检查可达性
        float currentAp = actor.GetAp();
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, currentAp);
        return reachable.Contains(pos);
    }

    private AIAction BuildFlankAttackAction(Unit actor, Unit target, Vector2I flankPos, int flankFacing, HexGrid hexGrid)
    {
        var action = new AIAction
        {
            Actor = actor,
            TargetUnit = target,
            TargetPosition = flankPos,
            AttackPosition = flankPos,
            MovePath = hexGrid.FindPath(actor.GridPos, flankPos)
        };

        if (flankFacing == 2)
        {
            action.IsBackstab = true;
            action.Description = $"{actor.Data!.UnitName} 绕后偷袭 {target.Data!.UnitName}！";
        }
        else
        {
            action.IsFlanking = true;
            action.Description = $"{actor.Data!.UnitName} 侧翼包抄 {target.Data!.UnitName}！";
        }

        if (action.MovePath.Count >= 3 && DifficultyConfig.UsesCharge)
        {
            action.IsCharge = AISpatialAnalyzer.CanCharge(hexGrid, action.MovePath, actor.GridPos);
        }

        return action;
    }

    /// <summary>尝试找更安全的接近路径（避免借机攻击）</summary>
    private AIAction? FindSaferApproach(Unit actor, Unit target, List<Unit> playerUnits, HexGrid hexGrid)
    {
        float currentAp = actor.GetAp();
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, currentAp);

        Vector2I bestPos = new(-1, -1);
        float bestScore = -999.0f;

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || (cell.Occupant != null && cell.Occupant != actor)) continue;

            int distToTarget = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
            int effectiveAtkRange = AISpatialAnalyzer.GetEffectiveRange(hexGrid, actor, pos, target.GridPos);
            if (distToTarget > effectiveAtkRange) continue;

            var path = hexGrid.FindPath(actor.GridPos, pos);
            int aoos = AISpatialAnalyzer.CountOpportunityAttacks(hexGrid, path, playerUnits);

            float score = -aoos * 5.0f;
            score += cell.CoverType * 2.0f;
            
            // 考虑高地得分 (从 AISpatialAnalyzer 获取防御/战术评估)
            score += AISpatialAnalyzer.EvaluatePositionDefense(hexGrid, pos, new[] { target });

            if (score > bestScore)
            {
                bestScore = score;
                bestPos = pos;
            }
        }

        if (bestPos.X == -1 && bestPos.Y == -1) return null;

        return new AIAction
        {
            Type = AIAction.ActionType.MoveThenAttack,
            Actor = actor,
            TargetUnit = target,
            TargetPosition = bestPos,
            AttackPosition = bestPos,
            MovePath = hexGrid.FindPath(actor.GridPos, bestPos),
            Description = $"{actor.Data!.UnitName} 安全接近 {target.Data!.UnitName}"
        };
    }
}
