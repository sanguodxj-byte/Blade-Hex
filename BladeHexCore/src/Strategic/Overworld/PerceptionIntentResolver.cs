// PerceptionIntentResolver.cs
// 感知意图判定器 — 独占"感知 → 追击/逃跑/忽略"的判定
//
// 设计目标:
//   - 合并旧行为评估器与 BattleResolver 视野检测的重复逻辑
//   - 单一 Module 负责把所有实体从普通状态改成 Chasing / Fleeing
//   - AIStrategy、LordPersonality、外交和个人关系只在一个地方参与感知判定
//   - BattleResolver 只处理接触交战，不再写入远距追逃意图
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

/// <summary>
/// 感知意图判定结果
/// </summary>
public readonly struct Intent
{
    /// <summary>意图类型</summary>
    public IntentType Type { get; }
    
    /// <summary>关联目标（Chase/Flee 时有效）</summary>
    public OverworldEntity? Target { get; }

    public enum IntentType
    {
        None,           // 无特殊行为
        Chase,          // 追击目标
        Flee,           // 逃离威胁
        ReturnHome,     // 返回基地
        DefendTerritory,// 守卫领地
    }

    public static readonly Intent None = new(IntentType.None, null);
    public static Intent Chase(OverworldEntity target) => new(IntentType.Chase, target);
    public static Intent Flee(OverworldEntity threat) => new(IntentType.Flee, threat);
    public static Intent ReturnHome => new(IntentType.ReturnHome, null);
    public static Intent DefendTerritory => new(IntentType.DefendTerritory, null);

    private Intent(IntentType type, OverworldEntity? target)
    {
        Type = type;
        Target = target;
    }
}

/// <summary>
/// 感知意图判定器 — 独占追逃判定的单一 Module。
/// 
/// 使用方式:
///   var intent = resolver.Resolve(entity, nearbyEntities, engine);
///   if (intent.Type == Intent.IntentType.Chase) { ... }
/// </summary>
public sealed class PerceptionIntentResolver
{
    private static readonly Random _rng = new();

    // ========================================
    // 各类型追/逃阈值 (战力比 = 己方CP / 目标CP)
    // ========================================

    private static class Thresholds
    {
        public static (float chase, float flee) LordArmy(OverworldPOI.LordPersonality p) => p switch
        {
            OverworldPOI.LordPersonality.Cautious   => (2.0f, 1.2f),
            OverworldPOI.LordPersonality.Aggressive => (1.0f, 0.5f),
            _                                       => (1.5f, 0.8f),
        };

        public const float AdvChase = 1.5f;
        public const float AdvFlee  = 0.7f;
        public const float RaidChase = 1.3f;
        public const float RaidFlee  = 0.5f;
        public const float MonsterChase = 0.5f;
        public const float MonsterFlee  = 5.0f;
        public const float CaravanFlee  = 0f;
    }

    /// <summary>
    /// AIStrategy 战术修正 — chaseMul < 1 = 更易追击, fleeMul > 1 = 更易逃跑
    /// </summary>
    private static (float chaseMul, float fleeMul) GetStrategyModifiers(AIStrategyEnum strategy) => strategy switch
    {
        AIStrategyEnum.Reckless    => (0.5f, 2.0f),
        AIStrategyEnum.Cautious    => (1.5f, 0.5f),
        AIStrategyEnum.Tactical    => (0.9f, 0.85f),
        AIStrategyEnum.Instinct    => (1.0f, 1.0f),
        AIStrategyEnum.Territorial => (0.8f, 1.3f),
        AIStrategyEnum.Cunning     => (0.7f, 1.2f),
        AIStrategyEnum.Intimidate  => (0.6f, 0.7f),
        AIStrategyEnum.Berserk     => (0.3f, 5.0f),
        _                          => (1.0f, 1.0f),
    };

    // ========================================
    // 公共入口
    // ========================================

    /// <summary>
    /// 判定实体对某个目标的感知意图。
    /// </summary>
    /// <param name="entity">感知者</param>
    /// <param name="target">被感知的敌对实体</param>
    /// <param name="engine">世界事件引擎（用于外交判定）</param>
    /// <param name="relationMatrix">英雄关系矩阵（可选）</param>
    public Intent Resolve(
        OverworldEntity entity,
        OverworldEntity target,
        WorldEventEngine? engine,
        Hero.HeroRelationMatrix? relationMatrix = null)
    {
        if (!OverworldHostility.AreHostile(entity, target, engine, relationMatrix))
            return Intent.None;

        return ResolveKnownHostile(entity, target);
    }

    /// <summary>
    /// Resolves intent toward the player proxy using player-specific hostility
    /// rules such as IsHostileToPlayer and the player's current nation.
    /// </summary>
    public Intent ResolvePlayer(
        OverworldEntity entity,
        OverworldEntity playerProxy,
        WorldEventEngine? engine,
        Hero.HeroRelationMatrix? relationMatrix = null)
    {
        if (!OverworldHostility.AreHostileToPlayer(entity, playerProxy, engine, relationMatrix))
            return Intent.None;

        return ResolveKnownHostile(entity, playerProxy);
    }

    /// <summary>
    /// Resolve intent when the caller already owns the hostility decision.
    /// Used by adapters such as player-proximity encounters where the target is
    /// a transient proxy rather than a normal faction participant.
    /// </summary>
    public Intent ResolveKnownHostile(OverworldEntity entity, OverworldEntity target)
    {
        float powerRatio = entity.CombatPower / System.Math.Max(target.CombatPower, 0.1f);

        switch (entity.EntityTypeEnum)
        {
            case OverworldEntity.EntityType.LordArmy:
                return ResolveLordArmy(entity, target, powerRatio);
            case OverworldEntity.EntityType.Adventurer:
                return ResolveStandard(entity, target, powerRatio, Thresholds.AdvChase, Thresholds.AdvFlee);
            case OverworldEntity.EntityType.RaidingParty:
            case OverworldEntity.EntityType.BanditParty:
            case OverworldEntity.EntityType.RobberParty:
            case OverworldEntity.EntityType.PirateCrew:
                return ResolveStandard(entity, target, powerRatio, Thresholds.RaidChase, Thresholds.RaidFlee);
            case OverworldEntity.EntityType.EpicMonster:
                return ResolveEpicMonster(entity, target, powerRatio);
            case OverworldEntity.EntityType.Caravan:
                return Intent.Flee(target);
            default:
                return Intent.None;
        }
    }

    /// <summary>
    /// 扫描所有附近敌对实体，返回最紧迫的意图。
    /// 扫描视野内敌对实体，并解析实体当前最应该采取的追击/逃跑意图。
    /// </summary>
    public (OverworldEntity? bestTarget, Intent intent) ResolveBest(
        OverworldEntity entity,
        List<OverworldEntity> allEntities,
        WorldEventEngine? engine,
        Hero.HeroRelationMatrix? relationMatrix = null,
        EntitySpatialIndex? spatialIndex = null)
    {
        // 已经在 Chasing/Fleeing/Engaged 中 → 不覆盖
        if (entity.CurrentAIState == OverworldEntity.AIState.Chasing
            || entity.CurrentAIState == OverworldEntity.AIState.Fleeing
            || entity.CurrentAIState == OverworldEntity.AIState.Engaged)
            return (null, Intent.None);

        // 史诗怪物在领地外 → 不执行行为评估
        if (entity.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster
            && entity.TerritoryCenter != Godot.Vector2.Zero
            && !entity.IsInTerritory(entity.Position))
            return (null, Intent.None);

        // 扫描最紧迫的威胁
        var (nearest, _) = ScanNearestThreat(entity, allEntities, engine, relationMatrix, spatialIndex);
        if (nearest == null)
            return (null, Intent.None);

        var intent = Resolve(entity, nearest, engine, relationMatrix);
        return (nearest, intent);
    }

    // ========================================
    // 类型策略
    // ========================================

    private Intent ResolveLordArmy(OverworldEntity lord, OverworldEntity target, float powerRatio)
    {
        var (chase, flee) = Thresholds.LordArmy(lord.LordPersonalityValue);
        return ApplyThresholds(lord, target, powerRatio, chase, flee);
    }

    private Intent ResolveStandard(OverworldEntity entity, OverworldEntity target, float powerRatio, float chaseThreshold, float fleeThreshold)
    {
        return ApplyThresholds(entity, target, powerRatio, chaseThreshold, fleeThreshold);
    }

    private Intent ResolveEpicMonster(OverworldEntity monster, OverworldEntity target, float powerRatio)
    {
        // 只在领地范围内响应
        if (monster.TerritoryCenter != Godot.Vector2.Zero
            && !monster.IsInTerritory(target.Position))
            return Intent.None;

        return ApplyThresholds(monster, target, powerRatio, Thresholds.MonsterChase, Thresholds.MonsterFlee);
    }

    // ========================================
    // 阈值判定
    // ========================================

    private Intent ApplyThresholds(OverworldEntity entity, OverworldEntity target, float powerRatio, float chaseThreshold, float fleeThreshold)
    {
        var (chaseMul, fleeMul) = GetStrategyModifiers(entity.AIStrategy);
        chaseThreshold *= chaseMul;
        fleeThreshold  *= fleeMul;

        if (powerRatio > chaseThreshold)
            return Intent.Chase(target);
        else if (powerRatio < fleeThreshold)
            return Intent.Flee(target);

        return Intent.None;
    }

    // ========================================
    // 感知扫描
    // ========================================

    private (OverworldEntity? target, float distance) ScanNearestThreat(
        OverworldEntity entity,
        List<OverworldEntity> allEntities,
        WorldEventEngine? engine,
        Hero.HeroRelationMatrix? relationMatrix = null,
        EntitySpatialIndex? spatialIndex = null)
    {
        OverworldEntity? best = null;
        float bestScore = float.MaxValue;

        IEnumerable<OverworldEntity> candidates;
        if (spatialIndex != null)
            candidates = spatialIndex.QueryRadius(entity.Position, entity.VisionRange);
        else
            candidates = allEntities;

        foreach (var other in candidates)
        {
            if (other == entity || !other.IsAlive) continue;
            if (other.Lod == OverworldEntity.EntityLod.Hibernated) continue;

            float dist = entity.Position.DistanceTo(other.Position);
            if (dist > entity.VisionRange) continue;
            if (!OverworldHostility.AreHostile(entity, other, engine, relationMatrix)) continue;

            float threat = other.CombatPower / System.Math.Max(entity.CombatPower, 0.1f);
            float score = dist / (1.0f + threat);

            if (score < bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        return (best, best != null ? entity.Position.DistanceTo(best.Position) : float.MaxValue);
    }
}
