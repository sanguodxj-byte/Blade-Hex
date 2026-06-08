// EncounterUnitFactory.cs
// 遭遇单位工厂 — 把 EncounterData 的模板 ID 列表转成可部署的 UnitData 列表
//
// 用途：大地图遭遇触发时，生成敌方 UnitData 传给 CombatScene
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic.Economy;
using BladeHex.Combat;


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
        int seed = (int)(entity.Position.X * 31 + entity.Position.Y * 37);
        return BattleDeploymentFactory.BuildUnits(EntityCombatBridge.GetDeployment(entity, false), seed);
    }

    /// <summary>
    /// 从委托目标点生成敌方 UnitData 列表。
    /// </summary>
    public static List<UnitData> BuildEnemyUnitsFromQuestTarget(QuestTargetSite site, int playerLevel = 1)
    {
        var units = new List<UnitData>();
        if (site.EncounterData == null) return units;

        int seed = site.QuestId.GetHashCode() ^ (int)(site.WorldPosition.X * 31 + site.WorldPosition.Y * 37);
        var rng = new Random(seed);
        int baseLevel = Math.Max(1, playerLevel + Math.Max(0, site.DangerStars - 1));

        foreach (var group in site.EncounterData.EnemyGroups)
        {
            int count = Math.Max(1, group.Count);
            int level = Math.Max(1, baseLevel + group.LevelOffset);

            for (int i = 0; i < count; i++)
            {
                var unit = CreateEnemyFromTemplate(NormalizeTemplateId(group.TemplateId), level, rng);
                units.Add(unit);
            }
        }

        return units;
    }

    /// <summary>
    /// 根据模板 ID 和等级创建一个敌方 UnitData
    /// </summary>
    private static UnitData CreateEnemyFromTemplate(string templateId, int level, Random rng)
    {
        string normId = NormalizeTemplateId(templateId);
        var tpl = UnitTemplateDB.GetTemplateById(normId);

        if (tpl != null)
        {
            var unit = CharacterGenerator.GenerateFromTemplate(tpl, level);
            unit.UnitName = $"{unit.UnitName}_{rng.Next(100):D2}";

            // 如果有武器配置，为怪物生成合适的等级武器
            if (tpl.ContainsKey("weapon_subtype"))
            {
                var subtype = (WeaponData.WeaponSubtype)tpl["weapon_subtype"].AsInt32();
                var wpn = CreateWeaponForSubtype(subtype, level);
                if (wpn != null)
                {
                    unit.PrimaryMainHand = wpn;
                }
            }

            return unit;
        }

        // Fallback: 如果拿不到模板，使用默认的 grunt_goblin_warrior
        var fallbackTpl = UnitTemplateDB.GruntGoblinWarrior();
        var fallbackUnit = CharacterGenerator.GenerateFromTemplate(fallbackTpl, level);
        fallbackUnit.UnitName = $"{fallbackUnit.UnitName}_{rng.Next(100):D2}";
        return fallbackUnit;
    }

    private static WeaponData? CreateWeaponForSubtype(WeaponData.WeaponSubtype subtype, int level)
    {
        var allWeapons = PrototypeData.GetWeapons();
        WeaponData? baseItem = null;
        foreach (var w in allWeapons.Values)
        {
            if (w.Subtype == subtype)
            {
                baseItem = w;
                break;
            }
        }
        if (baseItem == null) return null;

        // 动态生成的物品等级大约对应怪物等级
        int itemLevel = Math.Max(1, level);
        return EquipmentGenerator.GenerateRandomWeapon(new string[] { baseItem.ItemId }, itemLevel: itemLevel);
    }

    private static string NormalizeTemplateId(string templateId) => BattleDeploymentFactory.NormalizeTemplateId(templateId);



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
