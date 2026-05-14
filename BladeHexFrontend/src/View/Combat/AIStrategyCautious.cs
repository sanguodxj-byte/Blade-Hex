using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.AI;

/// <summary>
/// 谨慎策略 —— 优先掩体、保持距离、放风筝、受威胁时后撤
/// 适用于：弓箭手、法师、远程单位
/// </summary>
public class AIStrategyCautious : AIStrategyBase
{
    public AIStrategyCautious(AIDifficultyConfig config) : base(config) { }

    protected override AIAction DecideStrategyAction(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        // 选评分最高的目标
        var bestEntry = scoredTargets[0];
        var target = bestEntry.Unit;

        var weapon = actor.Model.GetMainHand() as WeaponData;
        bool isRanged = weapon != null && weapon.IsRanged;
        int atkRange = weapon?.RangeCells ?? 1;
        int dist = HexUtils.Distance(actor.GridPos.X, actor.GridPos.Y, target.GridPos.X, target.GridPos.Y);

        // 如果是近战武器但有远程备选，切换远程
        if (!isRanged && actor.Data?.SecondaryMainHand is WeaponData secondary && secondary.IsRanged)
        {
            actor.SwitchWeaponSet();
            weapon = secondary;
            isRanged = true;
            atkRange = weapon.RangeCells;
        }

        if (dist <= atkRange)
        {
            // 在射程内 —— 检查是否应该换到更好的掩体位置再打
            if (DifficultyConfig.UsesTerrain)
            {
                var betterPos = FindBetterCoverPosition(actor, target, hexGrid);
                if (betterPos != actor.GridPos)
                {
                    var path = hexGrid.FindPath(actor.GridPos, betterPos);
                    if (path.Count > 0)
                    {
                        return new AIAction
                        {
                            Type = AIAction.ActionType.MoveThenAttack,
                            Actor = actor,
                            TargetUnit = target,
                            TargetPosition = betterPos,
                            AttackPosition = betterPos,
                            MovePath = path,
                            Description = $"{actor.Data!.UnitName} 移动到掩体后射击 {target.Data!.UnitName}"
                        };
                    }
                }
            }

            // 当前位置就很好，直接攻击
            return new AIAction
            {
                Type = AIAction.ActionType.Attack,
                Actor = actor,
                TargetUnit = target,
                AttackPosition = actor.GridPos,
                Description = $"{actor.Data!.UnitName} 远程攻击 {target.Data!.UnitName}"
            };
        }
        else
        {
            // 不在射程内 —— 需要移动
            if (isRanged)
            {
                int nearestMeleeDist = GetNearestMeleeDistance(actor, playerUnits, hexGrid);
                if (nearestMeleeDist <= 2)
                {
                    // 有近战威胁！风筝
                    var kitePos = FindKitePosition(actor, target, playerUnits, hexGrid);
                    if (kitePos != new Vector2I(-1, -1))
                    {
                        int kiteDist = HexUtils.Distance(kitePos.X, kitePos.Y, target.GridPos.X, target.GridPos.Y);
                        if (kiteDist <= atkRange)
                        {
                            return new AIAction
                            {
                                Type = AIAction.ActionType.MoveThenAttack,
                                Actor = actor,
                                TargetUnit = target,
                                TargetPosition = kitePos,
                                AttackPosition = kitePos,
                                MovePath = hexGrid.FindPath(actor.GridPos, kitePos),
                                Description = $"{actor.Data!.UnitName} 风筝移动并射击 {target.Data!.UnitName}"
                            };
                        }
                        else
                        {
                            return new AIAction
                            {
                                Type = AIAction.ActionType.MoveOnly,
                                Actor = actor,
                                TargetPosition = kitePos,
                                MovePath = hexGrid.FindPath(actor.GridPos, kitePos),
                                Description = $"{actor.Data!.UnitName} 风筝后撤"
                            };
                        }
                    }
                }
            }

            // 无近战威胁，正常接近并攻击
            var moveAction = CreateAttackAction(actor, target, hexGrid);
            if (DifficultyConfig.UsesTerrain)
            {
                var coverPositions = AISpatialAnalyzer.FindCoverPositions(hexGrid, actor, target, actor.Model.GetMoveRange());
                if (coverPositions.Count > 0)
                {
                    var bestCover = coverPositions[0];
                    moveAction.TargetPosition = bestCover.Position;
                    moveAction.AttackPosition = bestCover.Position;
                    moveAction.MovePath = hexGrid.FindPath(actor.GridPos, bestCover.Position);
                }
            }
            moveAction.Description = $"{actor.Data!.UnitName} 移动并攻击 {target.Data!.UnitName}";
            return moveAction;
        }
    }

    private Vector2I FindBetterCoverPosition(Unit actor, Unit target, HexGrid hexGrid)
    {
        float currentDefense = AISpatialAnalyzer.EvaluatePositionDefense(hexGrid, actor.GridPos, new[] { target });
        int moveRange = actor.Model.GetMoveRange();
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int atkRange = weapon?.RangeCells ?? 1;
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveRange);

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || (cell.Occupant != null && cell.Occupant != actor)) continue;

            int dist = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
            if (dist > atkRange) continue;

            float posDefense = AISpatialAnalyzer.EvaluatePositionDefense(hexGrid, pos, new[] { target });
            if (posDefense > currentDefense + 1.0f) return pos;
        }

        return actor.GridPos;
    }

    private int GetNearestMeleeDistance(Unit actor, List<Unit> playerUnits, HexGrid hexGrid)
    {
        int nearest = 999;
        foreach (var pu in playerUnits)
        {
            if (pu == null || pu.CurrentHp <= 0) continue;
            var weapon = pu.Model.GetMainHand() as WeaponData;
            if (weapon == null || !weapon.IsRanged)
            {
                int dist = HexUtils.Distance(actor.GridPos.X, actor.GridPos.Y, pu.GridPos.X, pu.GridPos.Y);
                if (dist < nearest) nearest = dist;
            }
        }
        return nearest;
    }

    private Vector2I FindKitePosition(Unit actor, Unit target, List<Unit> threats, HexGrid hexGrid)
    {
        int moveRange = actor.Model.GetMoveRange();
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int atkRange = weapon?.RangeCells ?? 1;
        var reachable = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, moveRange);

        Vector2I bestPos = new(-1, -1);
        float bestScore = -999.0f;

        foreach (var pos in reachable)
        {
            var cell = hexGrid.GetCell(pos.X, pos.Y);
            if (cell == null || cell.Occupant != null) continue;

            float score = 0.0f;
            foreach (var threat in threats)
            {
                if (threat == null) continue;
                var threatWeapon = threat.Model.GetMainHand() as WeaponData;
                if (threatWeapon != null && threatWeapon.IsRanged) continue;
                int dist = HexUtils.Distance(pos.X, pos.Y, threat.GridPos.X, threat.GridPos.Y);
                score += dist * 1.5f;
            }

            int targetDist = HexUtils.Distance(pos.X, pos.Y, target.GridPos.X, target.GridPos.Y);
            if (targetDist <= atkRange) score += 10.0f;

            score += cell.CoverType * 2.0f;

            if (score > bestScore)
            {
                bestScore = score;
                bestPos = pos;
            }
        }

        return bestPos;
    }
}
