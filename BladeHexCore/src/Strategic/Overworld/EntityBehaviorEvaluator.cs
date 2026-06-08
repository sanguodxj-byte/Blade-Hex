// EntityBehaviorEvaluator.cs
// AI 实体行为策略评估器 — 感知 → 战力评估 → 追击/逃跑决策
//
// 骑砍式四阶段行为管线:
//   1. 感知: 在 VisionRange 内扫描其他实体
//   2. 敌对判定: 基于 Faction 与外交关系(WorldEventEngine)
//   3. 战力评估: 己方 CombatPower / 目标 CombatPower
//   4. 类型策略: 按 EntityType 执行不同的追/逃/忽略阈值
//
// 本组件在 DailyDecisionProcessor 的 DecideDailyAction 之前调用,
// 设定 Chasing / Fleeing 意图后,由各类型的现有 switch 分支处理实际移动。
//
// 设计文档: 04-战略层系统.md § AI间战斗与追逐
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic.WorldEvents;
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// AI 实体行为策略评估器。
/// 每 tick 在 DailyDecisionProcessor 之前运行,为所有活跃实体执行
/// "感知 → 评估 → 决策"管线,设定 Chasing/Fleeing 意图。
/// </summary>
public sealed class EntityBehaviorEvaluator
{
    private static readonly Random _rng = new();

    // ================================================
    // 各类型追/逃阈值 (战力比 = 己方CP / 目标CP)
    // 设计来源: 04-战略层系统.md "追逐规则"
    // ================================================

    public static class Thresholds
    {
        // 领主军队 — 按性格分化 (04 § 领主性格表)
        //   谨慎: 追 2.0 / 逃 1.2    均衡: 追 1.5 / 逃 0.8    激进: 追 1.0 / 逃 0.5
        public static (float chase, float flee) LordArmy(OverworldPOI.LordPersonality p) => p switch
        {
            OverworldPOI.LordPersonality.Cautious   => (2.0f, 1.2f),
            OverworldPOI.LordPersonality.Aggressive => (1.0f, 0.5f),
            _                                       => (1.5f, 0.8f),
        };

        // 冒险者 — 标准 1.5 / 0.7
        public const float AdvChase = 1.5f;
        public const float AdvFlee  = 0.7f;

        // 掠夺队 — 遇弱者积极追击,遇强者果断逃跑
        public const float RaidChase = 1.3f;
        public const float RaidFlee  = 0.5f;

        // 史诗怪物 — 领地内极度好战,仅压倒性力量才能吓退
        public const float MonsterChase = 0.5f;
        public const float MonsterFlee  = 5.0f;

        // 商队 — 不追击,遇任何敌对直接逃跑
        public const float CaravanFlee = 0f; // 0 = 总是逃跑
    }

    // ================================================
    // AIStrategy 策略修正器 — 在类型阈值基础上叠加个性差异
    // chaseMul: <1 = 更易追击(更低阈值), >1 = 更不易追击
    // fleeMul:  >1 = 更易逃跑(更高阈值), <1 = 更不易逃跑
    // ================================================

    public static (float chaseMul, float fleeMul) GetStrategyModifiers(AIStrategyEnum strategy) => strategy switch
    {
        AIStrategyEnum.Reckless    => (0.5f, 2.0f),   // 鲁莽: 积极追击, 不易逃跑
        AIStrategyEnum.Cautious    => (1.5f, 0.5f),   // 谨慎: 需更大优势才追, 更早逃跑
        AIStrategyEnum.Tactical    => (0.9f, 0.85f),  // 战术: 略优的决策平衡
        AIStrategyEnum.Instinct    => (1.0f, 1.0f),   // 本能: 基准(无修正)
        AIStrategyEnum.Territorial => (0.8f, 1.3f),   // 领地: 领地内更积极, 更易撤退守护
        AIStrategyEnum.Cunning     => (0.7f, 1.2f),   // 狡诈: 倾向伏击弱敌, 避免硬仗
        AIStrategyEnum.Intimidate  => (0.6f, 0.7f),   // 恐吓: 积极施压, 不易退缩
        AIStrategyEnum.Berserk     => (0.3f, 5.0f),   // 狂暴: 几乎必追, 几乎不逃
        _                          => (1.0f, 1.0f),
    };

    // ================================================
    // 公共入口
    // ================================================

    /// <summary>
    /// 对所有活跃实体执行行为策略评估。
    /// 应在 DailyDecisionProcessor.ProcessDailyDecisions 的 foreach 循环中、
    /// DecideDailyAction 之前调用。
    /// </summary>
    public void EvaluateAll(
        List<OverworldEntity> entities,
        WorldEventEngine? engine)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!entity.IsAlive) continue;
            if (entity.Lod == OverworldEntity.EntityLod.Hibernated) continue;

            Evaluate(entity, entities, engine);
        }
    }

    // ================================================
    // 评估管线
    // ================================================

    private void Evaluate(
        OverworldEntity entity,
        List<OverworldEntity> allEntities,
        WorldEventEngine? engine)
    {
        // 2) 已在 Chasing/Fleeing/Engaged 中 → 交给类型决策处理转换
        if (entity.CurrentAIState == OverworldEntity.AIState.Chasing
            || entity.CurrentAIState == OverworldEntity.AIState.Fleeing
            || entity.CurrentAIState == OverworldEntity.AIState.Engaged)
            return;

        // 3) 只在"可被战术覆盖"的状态下评估
        if (!CanOverrideState(entity.CurrentAIState)) return;

        // 4) 史诗怪物在领地外 → 正在返回领地,不执行行为评估
        if (entity.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster
            && entity.TerritoryCenter != Vector2.Zero
            && !entity.IsInTerritory(entity.Position))
            return;

        // 2) 感知: 在 VisionRange 内扫描所有实体,选出最紧迫的威胁
        var (nearest, dist) = ScanNearestThreat(entity, allEntities, engine);

        // 4) 无威胁 → 不做任何改变,维持当前 Idle/Patrolling 行为
        if (nearest == null) return;

        // 4) 战力比
        float powerRatio = entity.CombatPower / Math.Max(nearest.CombatPower, 0.1f);

        // 5) 按实体类型分派策略
        switch (entity.EntityTypeEnum)
        {
            case OverworldEntity.EntityType.LordArmy:
                DecideLordArmy(entity, nearest, powerRatio);
                break;
            case OverworldEntity.EntityType.Adventurer:
                DecideAdventurer(entity, nearest, powerRatio);
                break;
            case OverworldEntity.EntityType.RaidingParty:
            case OverworldEntity.EntityType.BanditParty:
            case OverworldEntity.EntityType.RobberParty:
            case OverworldEntity.EntityType.PirateCrew:
                DecideRaidingParty(entity, nearest, powerRatio);
                break;
            case OverworldEntity.EntityType.EpicMonster:
                DecideEpicMonster(entity, nearest, powerRatio);
                break;
            case OverworldEntity.EntityType.Caravan:
                DecideCaravan(entity, nearest);
                break;
        }
    }

    // ================================================
    // 类型策略
    // ================================================

    // ---- 领主军队 ----
    // 按性格分化: 谨慎型需要明显优势才追,激进型几乎不逃
    private void DecideLordArmy(OverworldEntity lord, OverworldEntity target, float powerRatio)
    {
        // 如果领主正在执行战争目标,不做行为覆盖
        if (IsCommittedToWarObjective(lord)) return;

        var (chase, flee) = Thresholds.LordArmy(lord.LordPersonalityValue);
        DecideAndApply(lord, target, powerRatio, chase, flee);
    }

    // ---- 冒险者 ----
    // 标准阈值 1.5 / 0.7,遇 hostile 怪物会主动出击
    private void DecideAdventurer(OverworldEntity adv, OverworldEntity target, float powerRatio)
    {
        DecideAndApply(adv, target, powerRatio, Thresholds.AdvChase, Thresholds.AdvFlee);
    }

    // ---- 掠夺队 ----
    // 遇弱积极追,遇强果断跑
    private void DecideRaidingParty(OverworldEntity raider, OverworldEntity target, float powerRatio)
    {
        DecideAndApply(raider, target, powerRatio, Thresholds.RaidChase, Thresholds.RaidFlee);
    }

    // ---- 史诗怪物 ----
    // 领地内极度好战,仅压倒性力量 (>5:1) 才能吓退
    private void DecideEpicMonster(OverworldEntity monster, OverworldEntity target, float powerRatio)
    {
        // 只在领地范围内响应
        if (monster.TerritoryCenter != Vector2.Zero
            && !monster.IsInTerritory(target.Position))
            return;

        DecideAndApply(monster, target, powerRatio,
            Thresholds.MonsterChase, Thresholds.MonsterFlee);
    }

    // ---- 商队 ----
    // 不追击,遇任何敌对实体直接逃跑
    private void DecideCaravan(OverworldEntity caravan, OverworldEntity target)
    {
        ApplyFlee(caravan, target);
    }

    // ================================================
    // 感知
    // ================================================

    /// <summary>
    /// 在实体 VisionRange 内扫描所有其他实体,返回最近且最紧迫的敌对目标。
    /// "紧迫度" = (1 + 威胁度) / 距离,优先处理近距离高威胁目标。
    /// </summary>
    private (OverworldEntity? target, float distance) ScanNearestThreat(
        OverworldEntity entity,
        List<OverworldEntity> allEntities,
        WorldEventEngine? engine)
    {
        OverworldEntity? best = null;
        float bestScore = float.MaxValue; // 越小越紧迫

        for (int i = 0; i < allEntities.Count; i++)
        {
            var other = allEntities[i];
            if (other == entity || !other.IsAlive) continue;
            if (other.Lod == OverworldEntity.EntityLod.Hibernated) continue;

            float dist = entity.Position.DistanceTo(other.Position);
            if (dist > entity.VisionRange) continue;

            if (!OverworldHostility.AreHostile(entity, other, engine)) continue;

            // 紧迫度: 距离近 + 威胁高 = 分数低 = 优先
            float threat = other.CombatPower / Math.Max(entity.CombatPower, 0.1f);
            float score = dist / (1.0f + threat);

            if (score < bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        return (best, best != null ? entity.Position.DistanceTo(best.Position) : float.MaxValue);
    }

    // ================================================
    // 辅助方法
    // ================================================

    /// <summary>
    /// 当前 AI 状态是否可被行为评估覆盖。
    /// 仅 Idle 和 Patrolling 允许评估介入 — Chasing/Fleeing 由类型决策处理,
    /// Besieging/Reinforcing 等为高优先级行为,不允许覆盖。
    /// </summary>
    private static bool CanOverrideState(OverworldEntity.AIState state) => state switch
    {
        OverworldEntity.AIState.Idle       => true,
        OverworldEntity.AIState.Patrolling => true,
        _ => false,
    };

    /// <summary>
    /// 实体是否正在执行不应被行为评估覆盖的高级任务。
    /// 当前仅检查领主的战争目标指派。
    /// </summary>
    private static bool IsCommittedToWarObjective(OverworldEntity entity)
        => !string.IsNullOrEmpty(entity.AssignedWarTargetPoiName);

    /// <summary>
    /// 根据战力比与阈值,设定 Chasing 或 Fleeing。
    /// </summary>
    private void DecideAndApply(
        OverworldEntity entity,
        OverworldEntity target,
        float powerRatio,
        float chaseThreshold,
        float fleeThreshold)
    {
        // 应用 AIStrategy 个性修正
        var (chaseMul, fleeMul) = GetStrategyModifiers(entity.AIStrategy);
        chaseThreshold *= chaseMul;
        fleeThreshold  *= fleeMul;

        if (powerRatio > chaseThreshold)
        {
            ApplyChase(entity, target);
        }
        else if (powerRatio < fleeThreshold)
        {
            ApplyFlee(entity, target);
        }
        // 中间地带: 保持当前行为,不干预
    }

    private void ApplyChase(OverworldEntity entity, OverworldEntity target)
    {
        // 已经在追同一个目标 → 不重复设置
        if (entity.CurrentAIState == OverworldEntity.AIState.Chasing
            && entity.ChaseTarget == target)
            return;

        entity.CurrentAIState = OverworldEntity.AIState.Chasing;
        entity.ChaseTarget = target;
        GD.Print($"[Behavior] {entity.EntityName} ({entity.EntityTypeEnum}) " +
                 $"追击 {target.EntityName} (CP {entity.CombatPower:F0} vs {target.CombatPower:F0})");
    }

    private void ApplyFlee(OverworldEntity entity, OverworldEntity threat)
    {
        if (entity.CurrentAIState == OverworldEntity.AIState.Fleeing)
            return;

        entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
        entity.ChaseTarget = null;
        GD.Print($"[Behavior] {entity.EntityName} ({entity.EntityTypeEnum}) " +
                 $"逃离 {threat.EntityName} (CP {entity.CombatPower:F0} vs {threat.CombatPower:F0})");
    }
}
