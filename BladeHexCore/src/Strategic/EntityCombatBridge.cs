// EntityCombatBridge.cs
// 实体战斗桥梁 — 将 OverworldEntity 的战略层数据转换为战斗层部署数据
// 从 OverworldEntity 提取，降低数据类的职责复杂度
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 实体战斗桥梁 — 战略层 → 战斗层的转换逻辑
/// 负责：部署生成、战斗结果应用、CR 计算
/// </summary>
public static class EntityCombatBridge
{
    /// <summary>
    /// 生成战斗部署数据 — 根据实体类型和队伍配置生成战斗单位
    /// </summary>
    public static BattleUnitDeployment[] GetDeployment(OverworldEntity entity, bool isAttacker)
    {
        var deployments = new List<BattleUnitDeployment>();

        switch (entity.EntityTypeEnum)
        {
            case OverworldEntity.EntityType.Adventurer:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "adventurer_warrior",
                    Count = Math.Max(1, entity.PartySize / 2),
                    LevelOverride = entity.PartyLevel,
                    DeployZone = "front_line",
                    IsPlayerControlled = false,
                });
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "adventurer_mage",
                    Count = Math.Max(1, entity.PartySize - entity.PartySize / 2),
                    LevelOverride = entity.PartyLevel,
                    DeployZone = "back_line",
                    IsPlayerControlled = false,
                });
                break;

            case OverworldEntity.EntityType.RaidingParty:
                var race = entity.SourceSettlement?.SettlementRaceValue ?? OverworldPOI.SettlementRace.Goblin;
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = race switch
                    {
                        OverworldPOI.SettlementRace.Goblin => "goblin_warrior",
                        OverworldPOI.SettlementRace.Kobold => "kobold_trapper",
                        OverworldPOI.SettlementRace.Minotaur => "minotaur_warrior",
                        OverworldPOI.SettlementRace.ShadowCult => "cultist",
                        _ => "goblin_warrior",
                    },
                    Count = entity.PartySize,
                    LevelOverride = entity.PartyLevel,
                    DeployZone = "front_line",
                    IsPlayerControlled = false,
                });
                break;

            case OverworldEntity.EntityType.Caravan:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "caravan_guard",
                    Count = Math.Max(1, entity.PartySize),
                    LevelOverride = 1,
                    DeployZone = "front_line",
                    IsPlayerControlled = false,
                });
                break;

            case OverworldEntity.EntityType.EpicMonster:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = entity.MonsterType switch
                    {
                        "dragon" => "dragon",
                        "ancient_golem" => "iron_golem",
                        _ => "unknown_boss",
                    },
                    Count = 1,
                    LevelOverride = entity.PartyLevel,
                    DeployZone = "front_line",
                    IsPlayerControlled = false,
                });
                break;

            case OverworldEntity.EntityType.LordArmy:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "soldier",
                    Count = Math.Max(1, entity.PartySize * 2 / 3),
                    LevelOverride = entity.PartyLevel,
                    DeployZone = "front_line",
                    IsPlayerControlled = false,
                });
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "archer",
                    Count = Math.Max(1, entity.PartySize / 3),
                    LevelOverride = entity.PartyLevel,
                    DeployZone = "back_line",
                    IsPlayerControlled = false,
                });
                break;
        }

        return deployments.ToArray();
    }

    /// <summary>
    /// 应用战斗结果 — 战斗结束后更新实体状态
    /// </summary>
    public static void ApplyBattleOutcome(OverworldEntity entity, BattleOutcome outcome)
    {
        ApplyBattleOutcome(entity, outcome, null, null, null, null, null, null);
    }

    /// <summary>
    /// 应用战斗结果(带英雄网络) — 战斗结束后更新实体状态。
    /// 战败时统一走 HeroDefeatResolver,触发被俘/战死/关系传播/新闻。
    /// </summary>
    public static void ApplyBattleOutcome(
        OverworldEntity entity,
        BattleOutcome outcome,
        OverworldEntity? winner,
        BladeHex.Strategic.WorldEvents.WorldEventEngine? engine,
        BladeHex.Strategic.Hero.HeroRegistry? registry,
        BladeHex.Strategic.Hero.PrisonerLedger? ledger,
        BladeHex.Strategic.Hero.HeroRelationMatrix? relations,
        List<OverworldPOI>? pois)
    {
        if (outcome == null) return;

        // 更新队伍人数
        if (outcome.AttackerLossPercent > 0)
        {
            int losses = (int)(entity.PartySize * outcome.AttackerLossPercent);
            entity.PartySize = Math.Max(0, entity.PartySize - losses);
        }

        // 更新战力
        entity.CombatPower = entity.PartySize * entity.PartyLevel
            * (entity.EntityTypeEnum == OverworldEntity.EntityType.LordArmy ? 1.5f : 2.0f);

        // 全灭处理 — 走统一战败入口(英雄网络可空,降级为 IsAlive=false)
        if (outcome.AttackerDestroyed || entity.PartySize <= 0)
        {
            BladeHex.Strategic.Hero.HeroDefeatResolver.Resolve(
                entity, winner, engine, registry, ledger, relations, pois);
            return;
        }

        // 战败逃跑
        if (!outcome.AttackerWon && entity.IsAlive)
        {
            entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
            entity.TargetPosition = entity.HomePosition;
        }

        // 更新掠夺队状态
        if (entity.EntityTypeEnum == OverworldEntity.EntityType.RaidingParty && outcome.AttackerWon)
        {
            entity.LootCarried += outcome.GoldGranted;
        }
    }

    /// <summary>
    /// 计算遭遇战 CR 总值 — 用于战斗难度评估
    /// </summary>
    public static float GetEncounterCR(OverworldEntity entity)
    {
        return entity.EntityTypeEnum switch
        {
            OverworldEntity.EntityType.Adventurer => entity.PartyLevel * 1.5f,
            OverworldEntity.EntityType.RaidingParty => 2.0f + (entity.SourceSettlement?.ThreatLevel ?? 0.5f) * 1.5f,
            OverworldEntity.EntityType.Caravan => 1.0f,
            OverworldEntity.EntityType.EpicMonster => 10.0f + entity.PartyLevel * 2.0f,
            OverworldEntity.EntityType.LordArmy => entity.PartyLevel * 3.0f,
            _ => 1.0f,
        };
    }
}
