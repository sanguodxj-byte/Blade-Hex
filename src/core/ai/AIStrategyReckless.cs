using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.AI;

/// <summary>
/// 鲁莽策略 —— 总是冲向最近敌人，优先冲锋，从低HP不撤退
/// 适用于：狂战士、兽类、野蛮人
/// </summary>
public class AIStrategyReckless : AIStrategyBase
{
    public AIStrategyReckless(AIDifficultyConfig config) : base(config) { }

    protected override AIAction DecideStrategyAction(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        // 鲁莽策略无视评分，总是选最近的敌人
        Unit? closestTarget = null;
        int closestDist = 999;

        foreach (var entry in scoredTargets)
        {
            var target = entry.Unit;
            int dist = HexUtils.Distance(actor.GridPos.X, actor.GridPos.Y, target.GridPos.X, target.GridPos.Y);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestTarget = target;
            }
        }

        if (closestTarget == null) return DecideIdleAction(actor, hexGrid);

        // 如果手持远程但敌人在近战范围，切近战
        var weapon = actor.GetMainHand() as WeaponData;
        if (weapon != null && weapon.IsRanged && closestDist <= 1)
        {
            if (actor.Data?.SecondaryMainHand is WeaponData secondary && !secondary.IsRanged)
            {
                actor.SwitchWeaponSet();
            }
        }

        // 创建攻击行动
        var action = CreateAttackAction(actor, closestTarget, hexGrid);

        // 鲁莽加成：尽量拉长移动距离以获得冲锋
        if (action.Type == AIAction.ActionType.MoveThenAttack && action.MovePath.Count > 0)
        {
            int moveRange = actor.GetMoveRange();
            var path = hexGrid.FindPath(actor.GridPos, closestTarget.GridPos);
            if (path.Count >= 3)
            {
                // 取路径上移动范围内的最远点
                int chargeIdx = Math.Min(moveRange - 1, path.Count - 1);
                var chargePos = path[chargeIdx];
                var chargeCell = hexGrid.GetCell(chargePos.X, chargePos.Y);
                int chargeDist = HexUtils.Distance(chargePos.X, chargePos.Y, closestTarget.GridPos.X, closestTarget.GridPos.Y);
                var curWeapon = actor.GetMainHand() as WeaponData;
                int curRange = curWeapon?.RangeCells ?? 1;

                if (chargeCell != null && chargeCell.Occupant == null && chargeDist <= curRange)
                {
                    action.TargetPosition = chargePos;
                    action.AttackPosition = chargePos;
                    action.MovePath = hexGrid.FindPath(actor.GridPos, chargePos);
                    action.IsCharge = true;
                }
            }
        }

        action.Description = $"{actor.Data.UnitName} 狂暴地冲向 {closestTarget.Data.UnitName}！";
        return action;
    }

    /// <summary>覆盖：鲁莽型不会因低HP撤退</summary>
    protected override AIAction? CheckRetreat(Unit actor, List<Unit> playerUnits, HexGrid hexGrid)
    {
        return null;
    }
}
