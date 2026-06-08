using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

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
                var weapon = actor.Model.GetMainHand() as WeaponData;
                int attackApCost = weapon?.ApCost ?? 4;
                foreach (var entry in flankPositions)
                {
                    var flankPos = entry.Position;
                    var flankFacing = entry.Facing;
                    if (IsPositionReachable(actor, flankPos, hexGrid, reserveAp: attackApCost))
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

    private bool IsPositionReachable(Unit actor, Vector2I pos, HexGrid hexGrid, float reserveAp = 0.0f)
    {
        var cell = hexGrid.GetCell(pos.X, pos.Y);
        if (cell == null || (cell.Occupant != null && cell.Occupant != actor)) return false;
        
        // 使用 AP 检查可达性；包夹后仍需预留攻击 AP
        float moveBudget = Math.Max(0.0f, actor.GetAp() - reserveAp);
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveBudget);
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

        action.Type = action.MovePath.Count == 0 && actor.GridPos == flankPos
            ? AIAction.ActionType.Attack
            : AIAction.ActionType.MoveThenAttack;

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

        var curWeapon = actor.Model.GetMainHand() as WeaponData;
        bool isMelee = curWeapon == null || (!curWeapon.IsRanged && !curWeapon.IsCatalyst);
        if (action.MovePath.Count >= 3 && DifficultyConfig.UsesCharge && isMelee)
        {
            action.IsCharge = AISpatialAnalyzer.CanCharge(hexGrid, action.MovePath, actor.GridPos);
        }

        return action;
    }

    /// <summary>尝试找更安全的接近路径（避免借机攻击）</summary>
    private AIAction? FindSaferApproach(Unit actor, Unit target, List<Unit> playerUnits, HexGrid hexGrid)
    {
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int attackApCost = weapon?.ApCost ?? 4;
        float moveBudget = Math.Max(0.0f, actor.GetAp() - attackApCost);
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveBudget);

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
            if (path.Count == 0 || hexGrid.GetPathCost(actor.GridPos, path) > moveBudget) continue;

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

    /// <summary>v0.8 D3-C: 战术型 — AOE覆盖3+敌人或可斩杀高价值目标时使用</summary>
    protected override AIAction? EvaluateCareerSkill(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        var skill = actor.GetCareerSkill();
        if (skill == null || !skill.IsActive || !actor.CanUseCareerSkill()) return null;
        if (actor.CurrentAp < 1f) return null;

        // 条件1: 可斩杀高评分目标（评分 > 15 且目标HP低）
        bool canExecute = false;
        if (scoredTargets.Count > 0 && scoredTargets[0].Score > 15f)
        {
            var target = scoredTargets[0].Unit;
            float hpPct = (float)target.CurrentHp / Math.Max(target.Model.GetMaxHp(), 1);
            if (hpPct < 0.3f) canExecute = true;
        }

        // 条件2: 周围2格内有3+敌人（模拟AOE覆盖）
        int nearbyEnemies = 0;
        foreach (var enemy in enemyUnits)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;
            int dist = HexUtils.Distance(actor.GridPos.X, actor.GridPos.Y, enemy.GridPos.X, enemy.GridPos.Y);
            if (dist <= 2) nearbyEnemies++;
        }

        if (canExecute || nearbyEnemies >= 3)
        {
            var targetCell = SelectCareerSkillTarget(actor, skill, scoredTargets, enemyUnits, hexGrid);
            return new AIAction
            {
                Type = AIAction.ActionType.UseCareerSkill,
                Actor = actor,
                SkillTargetCell = targetCell,
                Description = $"{actor.Data!.UnitName} 战术释放职业技能 {skill.DisplayName}！"
            };
        }

        return null;
    }
}
