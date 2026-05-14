using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.AI;

/// <summary>
/// 目标评分引擎 —— 为 AI 评估所有可见敌方单位作为攻击目标的优先级
/// 对应策划案 09-AI系统 → 二、目标优先级评分公式
/// </summary>
public class AITargetEvaluator
{
    private readonly AIDifficultyConfig _difficultyConfig;
    private readonly Random _rand = new();

    public AITargetEvaluator(AIDifficultyConfig config)
    {
        _difficultyConfig = config;
    }

    public class ScoredTarget
    {
        public Unit Unit { get; set; } = null!;
        public float Score { get; set; }
    }

    /// <summary>评估所有可见玩家单位作为潜在目标</summary>
    public List<ScoredTarget> EvaluateTargets(Unit actor, IEnumerable<Unit> playerUnits, HexGrid hexGrid, IEnumerable<Unit> allEnemyUnits)
    {
        var results = new List<ScoredTarget>();

        // 预计算可达范围（避免对每个目标重复计算）
        float currentAp = actor.GetAp();
        var reachableCoords = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, (int)currentAp);
        var reachableSet = new HashSet<Vector2I>(reachableCoords);

        foreach (var pu in playerUnits)
        {
            if (pu == null || pu.CurrentHp <= 0) continue;

            float threat = CalculateThreatScore(pu);
            float vuln = CalculateVulnerabilityScore(pu, actor, hexGrid);
            float strategic = CalculateStrategicValue(pu, actor);
            float reachBonus = 0.0f;

            // 能直接攻击到的目标加分
            if (CanAttackFromPosition(actor, pu, hexGrid, actor.GridPos))
            {
                reachBonus = 5.0f;
            }
            else if (CanReachTargetCached(actor, pu, hexGrid, reachableSet))
            {
                reachBonus = 2.0f;
            }

            // 综合评分（难度影响权重分配精度）
            float accuracy = _difficultyConfig.TargetSelectionAccuracy;
            float noise = (1.0f - accuracy) * (float)(_rand.NextDouble() * 6.0 - 3.0);

            float score = threat * 0.3f + vuln * 0.3f + strategic * 0.2f + reachBonus * 0.2f + noise;
            results.Add(new ScoredTarget { Unit = pu, Score = score });
        }

        // 按评分降序
        return results.OrderByDescending(r => r.Score).ToList();
    }

    /// <summary>威胁评分：目标对AI方的威胁程度</summary>
    private float CalculateThreatScore(Unit target)
    {
        float score = 0.0f;

        // HP 高的单位更有威胁（存活能力强）
        float hpRatio = (float)target.CurrentHp / Math.Max(target.Model.GetMaxHp(), 1);
        score += hpRatio * 3.0f;

        // 攻击力高的更有威胁
        var weapon = target.Model.GetMainHand() as WeaponData;
        if (weapon != null)
        {
            int maxDmg = weapon.DamageDiceCount * weapon.DamageDiceSides;
            score += Math.Min(maxDmg, 15.0f) * 0.3f;
        }
        else
        {
            score += 0.5f; // 徒手威胁低
        }

        // 远程单位威胁加成
        if (weapon != null && weapon.IsRanged) score += 2.0f;

        // AC高的单位威胁较高
        score += Math.Min(target.GetEffectiveAc() - 10, 10) * 0.2f;

        return score;
    }

    /// <summary>脆弱性评分：目标被击杀的难易度</summary>
    private float CalculateVulnerabilityScore(Unit target, Unit actor, HexGrid hexGrid)
    {
        float score = 0.0f;

        // HP越低越脆弱
        float hpRatio = (float)target.CurrentHp / Math.Max(target.Model.GetMaxHp(), 1);
        score += (1.0f - hpRatio) * 5.0f;

        // AC越低越脆弱
        int targetAc = target.GetEffectiveAc();
        int atkBonus = actor.Model.GetAttackBonus();
        int hitAdvantage = atkBonus - targetAc;
        score += Math.Clamp(hitAdvantage * 0.5f, -2.0f, 4.0f);

        // 掩体减少脆弱性
        var targetCell = hexGrid.GetCell(target.GridPos.X, target.GridPos.Y);
        if (targetCell != null)
        {
            score -= targetCell.CoverType * 1.5f;
            
            // 如果攻击者处于高地，目标显得更脆弱
            var actorCell = hexGrid.GetCell(actor.GridPos.X, actor.GridPos.Y);
            if (actorCell != null)
            {
                int diff = actorCell.Elevation - targetCell.Elevation;
                if (diff >= 2) score += 3.0f;
                else if (diff == 1) score += 1.5f;
            }
        }

        return score;
    }

    /// <summary>战略价值：击杀的意义</summary>
    private float CalculateStrategicValue(Unit target, Unit actor)
    {
        float score = 0.0f;

        float hpRatio = (float)target.CurrentHp / Math.Max(target.Model.GetMaxHp(), 1);
        if (hpRatio <= 0.25f) score += 6.0f;
        else if (hpRatio <= 0.5f) score += 2.0f;

        // 低HP=可能一击击杀
        int estimatedMaxDmg = 1;
        var weapon = actor.Model.GetMainHand() as WeaponData;
        if (weapon != null)
        {
            estimatedMaxDmg = weapon.DamageDiceCount * weapon.DamageDiceSides + RPGRuleEngine.GetStatModifier(actor.Data!.Str);
        }

        if (target.CurrentHp <= estimatedMaxDmg) score += 4.0f;

        return score;
    }

    public bool CanReachTarget(Unit actor, Unit target, HexGrid hexGrid)
    {
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int atkRange = weapon?.RangeCells ?? 1;
        float currentAp = actor.GetAp();
        
        // 使用 Dijkstra 预计算的可达点（考虑到高度消耗）
        var reachableCoords = hexGrid.GetCellsInRange(actor.GridPos.X, actor.GridPos.Y, (int)currentAp);
        
        foreach (var coord in reachableCoords)
        {
            int dist = HexUtils.Distance(coord.X, coord.Y, target.GridPos.X, target.GridPos.Y);
            // 考虑从该点射击时的有效射程（可能包含高地奖励）
            int effectiveAtkRange = AISpatialAnalyzer.GetEffectiveRange(hexGrid, actor, coord, target.GridPos);
            
            if (dist <= effectiveAtkRange) return true;
        }
        
        return false;
    }

    /// <summary>使用预计算的可达集合检查是否能到达目标攻击范围内</summary>
    private bool CanReachTargetCached(Unit actor, Unit target, HexGrid hexGrid, HashSet<Vector2I> reachableSet)
    {
        foreach (var coord in reachableSet)
        {
            int dist = HexUtils.Distance(coord.X, coord.Y, target.GridPos.X, target.GridPos.Y);
            int effectiveAtkRange = AISpatialAnalyzer.GetEffectiveRange(hexGrid, actor, coord, target.GridPos);
            if (dist <= effectiveAtkRange) return true;
        }
        return false;
    }

    public bool CanAttackFromPosition(Unit actor, Unit target, HexGrid hexGrid, Vector2I fromPos)
    {
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int atkRange;
        if (weapon != null)
        {
            atkRange = AISpatialAnalyzer.GetEffectiveRange(hexGrid, actor, fromPos, target.GridPos);
        }
        else
        {
            atkRange = 1;
        }
        int dist = HexUtils.Distance(fromPos.X, fromPos.Y, target.GridPos.X, target.GridPos.Y);
        return dist <= atkRange;
    }
}
