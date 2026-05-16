// POICombatBridge.cs
// POI 战斗桥梁 — 将 OverworldPOI 的战略层数据转换为战斗层部署/围攻数据
// 从 OverworldPOI 提取，降低数据类的职责复杂度
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// POI 战斗桥梁 — 战略层 → 战斗层的转换逻辑
/// 负责：防御部署生成、围攻结果应用、防御力评估
/// </summary>
public static class POICombatBridge
{
    /// <summary>
    /// 生成防御部署 — 根据 POI 类型和驻军配置生成防御战斗单位
    /// </summary>
    public static BattleUnitDeployment[] GenerateDefenseDeployment(OverworldPOI poi)
    {
        var deployments = new List<BattleUnitDeployment>();

        switch (poi.PoiTypeEnum)
        {
            case OverworldPOI.POIType.Town:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "town_militia",
                    Count = Math.Max(1, poi.GarrisonCurrent / 5),
                    LevelOverride = 1,
                    DeployZone = "front_line",
                });
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "town_guard",
                    Count = Math.Max(1, poi.GarrisonCurrent / 10),
                    LevelOverride = 2,
                    DeployZone = "back_line",
                });
                break;

            case OverworldPOI.POIType.Village:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "village_militia",
                    Count = Math.Max(1, poi.GarrisonCurrent / 5),
                    LevelOverride = 1,
                    DeployZone = "front_line",
                });
                break;

            case OverworldPOI.POIType.Castle:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "castle_knight",
                    Count = Math.Max(1, poi.GarrisonCurrent / 10),
                    LevelOverride = poi.CastleDefenseLevel + 2,
                    DeployZone = "front_line",
                });
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "castle_archer",
                    Count = Math.Max(1, poi.GarrisonCurrent / 5),
                    LevelOverride = poi.CastleDefenseLevel,
                    DeployZone = "back_line",
                });
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "castle_infantry",
                    Count = Math.Max(1, poi.GarrisonCurrent / 3),
                    LevelOverride = poi.CastleDefenseLevel + 1,
                    DeployZone = "front_line",
                });
                break;

            case OverworldPOI.POIType.Settlement:
                var race = poi.SettlementRaceValue;
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
                    Count = Math.Max(1, (int)(poi.ThreatLevel * 10)),
                    LevelOverride = (int)(poi.ThreatLevel * 3),
                    DeployZone = "front_line",
                });
                break;

            case OverworldPOI.POIType.Lair:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = poi.LairTypeValue switch
                    {
                        OverworldPOI.LairType.DragonLair => "dragon",
                        OverworldPOI.LairType.AncientTomb => "undead_guardian",
                        OverworldPOI.LairType.Ruins => "construct",
                        OverworldPOI.LairType.GolemForge => "iron_golem",
                        _ => "unknown_boss",
                    },
                    Count = 1,
                    LevelOverride = poi.LairLevel,
                    DeployZone = "front_line",
                });
                break;
        }

        return deployments.ToArray();
    }

    /// <summary>
    /// 应用围攻结果 — 更新 POI 状态
    /// </summary>
    public static void ApplySiegeOutcome(OverworldPOI poi, BattleOutcome outcome)
    {
        if (outcome == null) return;

        // 结束围攻状态
        poi.EndSiege();

        if (outcome.AttackerWon)
        {
            // POI 被攻占
            poi.OwningFaction = "hostile";
            poi.Prosperity = Math.Max(10, outcome.NewProsperity);
            poi.GarrisonCurrent = outcome.NewGarrisonSize;
        }
        else
        {
            // 防御成功，但驻军受损
            int losses = (int)(poi.GarrisonCurrent * outcome.DefenderLossPercent);
            poi.GarrisonCurrent = Math.Max(0, poi.GarrisonCurrent - losses);
        }
    }

    /// <summary>
    /// 获取 POI 的防御力量（用于 AI 评估）
    /// </summary>
    public static float GetDefensePower(OverworldPOI poi)
    {
        float basePower = poi.PoiTypeEnum switch
        {
            OverworldPOI.POIType.Castle => poi.CastleDefenseLevel * 10 + poi.GarrisonCurrent * 2.0f,
            OverworldPOI.POIType.Town => poi.GarrisonCurrent * 1.5f,
            OverworldPOI.POIType.Village => poi.GarrisonCurrent * 1.0f,
            OverworldPOI.POIType.Settlement => poi.ThreatLevel * 15,
            OverworldPOI.POIType.Lair => poi.LairLevel * 20,
            _ => 5.0f,
        };

        // 围攻削弱
        if (poi.IsUnderSiege) basePower *= 0.8f;

        return basePower;
    }
}
