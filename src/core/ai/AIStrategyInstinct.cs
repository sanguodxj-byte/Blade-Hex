using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.AI;

/// <summary>
/// 本能策略 —— 随机目标、无战术、追击到死
/// 适用于：低智力怪物（野兽群、虫类等）
/// </summary>
public class AIStrategyInstinct : AIStrategyBase
{
    public AIStrategyInstinct(AIDifficultyConfig config) : base(config) { }

    protected override AIAction DecideStrategyAction(Unit actor, List<AITargetEvaluator.ScoredTarget> scoredTargets, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        // 本能策略：从评分目标中随机选一个
        var targetIdx = Rand.Next(scoredTargets.Count);
        var target = scoredTargets[targetIdx].Unit;

        // 直接追击攻击，无战术
        var action = CreateAttackAction(actor, target, hexGrid);
        action.Description = $"{actor.Data.UnitName} 本能地扑向 {target.Data.UnitName}！";
        return action;
    }

    /// <summary>覆盖：本能生物不会因低HP撤退（只有士气溃逃才退）</summary>
    protected override AIAction? CheckRetreat(Unit actor, List<Unit> playerUnits, HexGrid hexGrid)
    {
        return null;
    }
}
