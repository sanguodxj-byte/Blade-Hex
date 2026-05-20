﻿﻿// EncounterUnitFactory.cs
// 遭遇单位工厂 — 把 EncounterData 的模板 ID 列表转成可部署的 UnitData 列表
//
// 用途：大地图遭遇触发时，生成敌方 UnitData 传给 CombatScene
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic;

public static class EncounterUnitFactory
{
    /// <summary>
    /// 从 EncounterData 生成敌方 UnitData 列表
    /// </summary>
    public static List<UnitData> BuildEnemyUnits(EncounterData encounter)
    {
        var units = new List<UnitData>();
        if (encounter.EnemyTemplateIds.Count == 0) return units;

        var rng = new Random(encounter.WorldCoord.X * 997 + encounter.WorldCoord.Y * 1009);

        for (int i = 0; i < encounter.PartySize; i++)
        {
            // 从模板列表中循环选取
            string templateId = encounter.EnemyTemplateIds[i % encounter.EnemyTemplateIds.Count];
            var unit = CreateEnemyFromTemplate(templateId, encounter.EncounterLevel, rng);
            units.Add(unit);
        }

        return units;
    }

    /// <summary>
    /// 从 OverworldEntity 生成敌方 UnitData 列表
    /// </summary>
    public static List<UnitData> BuildEnemyUnitsFromEntity(OverworldEntity entity)
    {
        var units = new List<UnitData>();
        var config = entity.GetEncounterConfig();
        var enemies = (Godot.Collections.Array<string>)config["enemies"];
        var rng = new Random((int)(entity.Position.X * 31 + entity.Position.Y * 37));

        for (int i = 0; i < entity.PartySize; i++)
        {
            string templateId = enemies.Count > 0 ? enemies[i % enemies.Count] : "goblin_warrior";
            var unit = CreateEnemyFromTemplate(templateId, entity.PartyLevel, rng);
            units.Add(unit);
        }

        return units;
    }

    /// <summary>
    /// 根据模板 ID 和等级创建一个敌方 UnitData
    /// </summary>
    private static UnitData CreateEnemyFromTemplate(string templateId, int level, Random rng)
    {
        var unit = new UnitData();
        unit.IsEnemy = true;
        unit.Level = Math.Max(1, level);

        // 基础属性按模板类型分配
        var (name, type, strategy, stats) = GetTemplateStats(templateId);
        unit.UnitName = $"{name}_{rng.Next(100):D2}";
        unit.enemyType = type;
        unit.aiStrategy = strategy;

        // 属性 = 基础 + 等级缩放
        unit.Str = stats.str + level / 3;
        unit.Dex = stats.dex + level / 3;
        unit.Con = stats.con + level / 3;
        unit.Intel = stats.intel;
        unit.Wis = stats.wis;
        unit.Cha = stats.cha;

        // HP = 基础 + Con 修正 × 等级
        int conMod = Math.Max(0, (unit.Con - 10) / 2);
        unit.BaseMaxHp = stats.baseHp + conMod * level;

        // AC
        unit.BaseAc = stats.baseAc;

        // 武器
        unit.PrimaryMainHand = CreateWeaponForTemplate(templateId);

        // 士气
        unit.Morale = strategy == UnitData.AIStrategy.Reckless ? 20 : 0;

        // 威胁等级
        unit.ThreatLevel = level * 0.5f;

        return unit;
    }

    private static (string name, UnitData.EnemyType type, UnitData.AIStrategy strategy,
        (int str, int dex, int con, int intel, int wis, int cha, int baseHp, int baseAc) stats)
        GetTemplateStats(string templateId)
    {
        return templateId switch
        {
            "goblin_warrior" => ("哥布林战士", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Cautious,
                (10, 14, 10, 6, 8, 6, 7, 11)),
            "goblin_archer" => ("哥布林射手", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Cautious,
                (8, 16, 8, 6, 8, 6, 6, 11)),
            "kobold_trapper" => ("狗头人陷阱师", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Cunning,
                (8, 15, 8, 10, 8, 6, 5, 10)),
            "minotaur_warrior" => ("牛头人战士", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Reckless,
                (18, 10, 16, 6, 8, 6, 20, 12)),
            "cultist" => ("暗影教徒", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Tactical,
                (10, 12, 10, 14, 12, 10, 9, 10)),
            "wolf" => ("灰狼", UnitData.EnemyType.Beast, UnitData.AIStrategy.Instinct,
                (12, 15, 12, 3, 12, 6, 11, 11)),
            "bandit" => ("山贼", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Cautious,
                (12, 12, 12, 10, 10, 10, 11, 10)),
            "treant" => ("树人", UnitData.EnemyType.Construct, UnitData.AIStrategy.Territorial,
                (16, 6, 16, 6, 10, 6, 25, 13)),
            "lizardman" => ("蜥蜴人", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Tactical,
                (14, 12, 14, 8, 10, 8, 15, 12)),
            "ogre" => ("食人魔", UnitData.EnemyType.Giant, UnitData.AIStrategy.Reckless,
                (18, 8, 16, 4, 6, 4, 30, 9)),
            "harpy" => ("鹰身女妖", UnitData.EnemyType.Beast, UnitData.AIStrategy.Cunning,
                (10, 16, 10, 8, 10, 12, 12, 11)),
            "ice_wolf" => ("冰霜狼", UnitData.EnemyType.Beast, UnitData.AIStrategy.Instinct,
                (14, 14, 14, 3, 12, 6, 14, 11)),
            "yeti" => ("雪人", UnitData.EnemyType.Giant, UnitData.AIStrategy.Territorial,
                (18, 10, 16, 6, 10, 6, 28, 10)),
            "caravan_guard" => ("商队护卫", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Cautious,
                (14, 12, 14, 10, 10, 10, 14, 12)),
            "soldier" => ("士兵", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Tactical,
                (14, 12, 14, 10, 10, 10, 14, 13)),
            "archer" => ("弓箭手", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Cautious,
                (10, 16, 10, 10, 12, 10, 10, 11)),
            "dragon" => ("巨龙", UnitData.EnemyType.Dragon, UnitData.AIStrategy.Tactical,
                (22, 10, 20, 16, 14, 18, 80, 16)),
            "iron_golem" => ("铁魔像", UnitData.EnemyType.Construct, UnitData.AIStrategy.Territorial,
                (20, 6, 20, 3, 10, 1, 60, 16)),
            _ => ("未知敌人", UnitData.EnemyType.Humanoid, UnitData.AIStrategy.Instinct,
                (10, 10, 10, 10, 10, 10, 10, 8)),
        };
    }

    private static WeaponData CreateWeaponForTemplate(string templateId)
    {
        return templateId switch
        {
            "goblin_archer" or "archer" => new WeaponData
            {
                ItemName = "短弓", IsRanged = true, RangeCells = 6,
                DamageDiceCount = 1, DamageDiceSides = 6, IsFinesse = true
            },
            "minotaur_warrior" or "ogre" or "yeti" => new WeaponData
            {
                ItemName = "巨斧", DamageDiceCount = 1, DamageDiceSides = 12,
                WeaponDamageType = WeaponData.DamageType.Slash
            },
            "dragon" => new WeaponData
            {
                ItemName = "龙爪", DamageDiceCount = 2, DamageDiceSides = 10,
                WeaponDamageType = WeaponData.DamageType.Slash
            },
            "iron_golem" => new WeaponData
            {
                ItemName = "铁拳", DamageDiceCount = 2, DamageDiceSides = 8,
                WeaponDamageType = WeaponData.DamageType.Crush
            },
            "treant" => new WeaponData
            {
                ItemName = "树枝横扫", DamageDiceCount = 2, DamageDiceSides = 6,
                WeaponDamageType = WeaponData.DamageType.Crush
            },
            _ => new WeaponData
            {
                ItemName = "短剑", DamageDiceCount = 1, DamageDiceSides = 6,
                IsFinesse = true
            },
        };
    }

    /// <summary>
    /// 计算战斗奖励（金币 + 经验）
    /// </summary>
    public static (int gold, int xp) CalculateRewards(EncounterData encounter)
    {
        float multiplier = encounter.Type switch
        {
            EncounterType.WildMonsters => 1.0f,
            EncounterType.HostilePatrol => 1.2f,
            EncounterType.CaravanEvent => 0.5f,
            EncounterType.Mystery => 1.5f,
            _ => 1.0f,
        };

        return (RewardPricingService.GetEncounterGold(encounter.EncounterLevel, encounter.PartySize, multiplier),
            RewardPricingService.GetEncounterXp(encounter.EncounterLevel, encounter.PartySize, multiplier));
    }

    /// <summary>
    /// 计算实体战斗奖励
    /// </summary>
    public static (int gold, int xp) CalculateRewardsFromEntity(OverworldEntity entity)
    {
        float multiplier = entity.EntityTypeEnum switch
        {
            OverworldEntity.EntityType.RaidingParty => 1.2f,
            OverworldEntity.EntityType.EpicMonster => 3.0f,
            OverworldEntity.EntityType.LordArmy => 2.0f,
            OverworldEntity.EntityType.Adventurer => 1.0f,
            OverworldEntity.EntityType.Caravan => 0.3f,
            _ => 1.0f,
        };

        return (RewardPricingService.GetEncounterGold(entity.PartyLevel, entity.PartySize, multiplier, entity.GoldCarried),
            RewardPricingService.GetEncounterXp(entity.PartyLevel, entity.PartySize, multiplier));
    }
}
