using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

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
    public AIAction DecideAction(Unit actor, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid, CombatManager? combatMgr = null)
    {
        if (actor.Data == null) return DecideIdleAction(actor, hexGrid);

        // 第1步：HP过低撤退检查
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

        // 第4步：评估是否使用职业技能
        var careerSkillAction = EvaluateCareerSkill(actor, targets, playerUnits, enemyUnits, hexGrid);
        if (careerSkillAction != null) return careerSkillAction;

        // 第4.5步：评估是否使用与技能盘无关的天生技能
        var intrinsicSkillAction = EvaluateIntrinsicSkill(actor, targets, playerUnits, enemyUnits, hexGrid, combatMgr);
        if (intrinsicSkillAction != null) return intrinsicSkillAction;

        // 第5步：策略特定决策（子类实现）
        return DecideStrategyAction(actor, targets, playerUnits, enemyUnits, hexGrid);
    }

    /// <summary>核心策略逻辑，由子类实现</summary>
    protected abstract AIAction DecideStrategyAction(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid);

    /// <summary>v0.8 D3-A: 评估是否使用职业技能。返回 null = 不使用。</summary>
    protected virtual AIAction? EvaluateCareerSkill(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        return null; // 默认不使用
    }

    /// <summary>评估是否使用非人形怪物的天生技能</summary>
    protected virtual AIAction? EvaluateIntrinsicSkill(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid, CombatManager? combatMgr)
    {
        if (actor.Data == null || actor.Data.enemyType == UnitData.EnemyType.Humanoid) return null;
        if (actor.CurrentAp < 1) return null;

        // 如果本回合已经使用过主动技能，则跳过
        if (actor.Data.Runtime.NonSpellSkillUsedThisTurn) return null;

        var availableSkills = new List<SkillData>();
        foreach (var skill in actor.Data.Skills)
        {
            if (skill == null || string.IsNullOrEmpty(skill.SkillName)) continue;

            if (combatMgr != null)
            {
                long casterId = (long)actor.GetInstanceId();
                if (combatMgr.CooldownTracker.IsOnCooldown(casterId, skill.SkillName)) continue;
            }

            if (actor.CurrentAp < skill.ApCost) continue;

            availableSkills.Add(skill);
        }

        if (availableSkills.Count == 0) return null;

        var sortedSkills = availableSkills.OrderByDescending(s => s.Cooldown).ToList();

        foreach (var skill in sortedSkills)
        {
            int apCost = skill.ApCost;
            int range = skill.RangeCells;

            foreach (var scored in scoredTargets)
            {
                var target = scored.Unit;
                if (!GodotObject.IsInstanceValid(target) || target.CurrentHp <= 0) continue;

                int dist = actor.DistanceTo(target);
                var targetCell = hexGrid.GetCell(target.GridPos.X, target.GridPos.Y);
                if (dist <= range && (targetCell == null || !CombatAttackRules.IsMeleeSkillElevationBlocked(actor, skill.SkillName, targetCell, hexGrid)))
                {
                    return new AIAction
                    {
                        Type = AIAction.ActionType.UseSkill,
                        Actor = actor,
                        TargetUnit = target,
                        TargetPosition = target.GridPos,
                        SkillId = skill.SkillName,
                        Description = $"{actor.Data.UnitName} 施展天生技能 {skill.SkillName} 攻击 {target.Data!.UnitName}！",
                        PriorityScore = 95f
                    };
                }

                float moveBudget = actor.CurrentAp - apCost;
                if (moveBudget > 0)
                {
                    var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveBudget);
                    Vector2I? bestMovePos = null;
                    float minPathCost = 999f;
                    List<Vector2I>? bestPath = null;

                    foreach (var pos in reachable)
                    {
                        var cell = hexGrid.GetCell(pos.X, pos.Y);
                        if (cell == null || (cell.Occupant != null && cell.Occupant != actor)) continue;

                        int d = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
                        if (d > range) continue;

                        var targetCell2 = hexGrid.GetCell(target.GridPos.X, target.GridPos.Y);
                        if (targetCell2 == null || CombatAttackRules.IsMeleeSkillElevationBlocked(actor, skill.SkillName, targetCell2, hexGrid, pos))
                            continue;

                        var path = hexGrid.FindPath(actor.GridPos, pos);
                        if (path.Count == 0) continue;

                        float cost = hexGrid.GetPathCost(actor.GridPos, path);
                        if (cost <= moveBudget && cost < minPathCost)
                        {
                            minPathCost = cost;
                            bestMovePos = pos;
                            bestPath = path;
                        }
                    }

                    if (bestMovePos.HasValue && bestPath != null)
                    {
                        return new AIAction
                        {
                            Type = AIAction.ActionType.UseSkill,
                            Actor = actor,
                            TargetUnit = target,
                            TargetPosition = bestMovePos.Value,
                            AttackPosition = bestMovePos.Value,
                            MovePath = bestPath,
                            SkillId = skill.SkillName,
                            Description = $"{actor.Data.UnitName} 接近并施展天生技能 {skill.SkillName} 攻击 {target.Data!.UnitName}！",
                            PriorityScore = 95f
                        };
                    }
                }
            }
        }

        return null;
    }

    /// <summary>v0.8 D4-A: 根据职业技能效果类型选择目标格</summary>
    protected Vector2I SelectCareerSkillTarget(Unit actor, CareerSkillData skill, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        string effectId = skill.EffectId ?? "";
        // 自Buff类技能：目标为自己
        if (effectId.Contains("living_wall") || effectId.Contains("arcane_overload")
            || effectId.Contains("rune_imbue") || effectId.Contains("blood_resonance")
            || effectId.Contains("battle_hymn") || effectId.Contains("whirling_strike")
            || effectId.Contains("hold_the_line") || effectId.Contains("spellweave")
            || effectId.Contains("stone_body") || effectId.Contains("mountain_stance")
            || effectId.Contains("juggernaut") || effectId.Contains("unstoppable")
            || effectId.Contains("death_sentence") || effectId.Contains("riposte")
            || effectId.Contains("mana_shield") || effectId.Contains("forewarning")
            || effectId.Contains("twist_fate") || effectId.Contains("dread_aura")
            || effectId.Contains("wind_favor") || effectId.Contains("silent_strike")
            || effectId.Contains("harbinger") || effectId.Contains("iron_grip")
            || effectId.Contains("jack_of_all") || effectId.Contains("tyrant_wrath")
            || effectId.Contains("twilight_stride") || effectId.Contains("paragon")
            || effectId.Contains("myriad") || effectId.Contains("war_king")
            || effectId.Contains("iron_law") || effectId.Contains("omnibus")
            || effectId.Contains("tailwind") || effectId.Contains("lead_front"))
        {
            return actor.GridPos;
        }

        // 对敌技能：选最高评分目标的位置
        if (scoredTargets.Count > 0)
        {
            var best = scoredTargets[0].Unit;
            return best.GridPos;
        }

        // 没有目标则默认自身位置
        return actor.GridPos;
    }

    /// <summary>HP过低撤退检查</summary>
    protected virtual AIAction? CheckRetreat(Unit actor, List<Unit> playerUnits, HexGrid hexGrid)
    {
        if (actor.Data!.enemyType == UnitData.EnemyType.Undead) return null;
        if (actor.Data.aiStrategy == UnitData.AIStrategy.Reckless) return null;
        if (!SkillTreeKeystoneResolver.CanRetreat(actor.Data)) return null;

        float hpPct = (float)actor.CurrentHp / Math.Max(actor.Model.GetMaxHp(), 1);
        float threshold = 0.25f * DifficultyConfig.RetreatThresholdMultiplier;

        if (hpPct <= threshold)
        {
            // 50%概率撤退
            if (Rand.NextDouble() < 0.5)
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

            var curWeapon = actor.Model.GetMainHand() as WeaponData;
            bool isMelee = curWeapon == null || (!curWeapon.IsRanged && !curWeapon.IsCatalyst);
            if (action.MovePath.Count >= 3 && DifficultyConfig.UsesCharge && isMelee)
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
        return dist <= atkRange
            && !CombatAttackRules.IsMeleeElevationBlocked(actor, target, hexGrid, fromPos);
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
