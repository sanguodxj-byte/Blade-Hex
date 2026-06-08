// BattleDeploymentFactory.cs
// 战斗部署工厂 — 将战略层 BattleUnitDeployment 转换为战斗层 UnitData
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗部署工厂。
///
/// 职责边界：
/// - EntityCombatBridge / POI 负责产出 BattleUnitDeployment（战略层部署描述）
/// - BattleDeploymentFactory 负责将 BattleUnitDeployment 落地为 UnitData（战斗层单位数据）
/// - CombatScene 只消费 BattleContext，不再反查 OverworldEntity 生成敌人
/// </summary>
public static class BattleDeploymentFactory
{
    public static List<UnitData> BuildUnits(BattleUnitDeployment[]? deployments, int seed = 0)
    {
        var units = new List<UnitData>();
        if (deployments == null || deployments.Length == 0)
            return units;

        var rng = new Random(seed != 0 ? seed : 1337);
        foreach (var deployment in deployments)
        {
            if (deployment == null || string.IsNullOrEmpty(deployment.UnitTemplateId))
                continue;

            int count = Math.Max(1, deployment.Count);
            int level = Math.Max(1, deployment.LevelOverride);
            string templateId = NormalizeTemplateId(deployment.UnitTemplateId);

            for (int i = 0; i < count; i++)
            {
                var unit = CreateUnitFromTemplate(templateId, level, rng);
                unit.IsEnemy = !deployment.IsPlayerControlled;
                units.Add(unit);
            }
        }

        return units;
    }

    private static UnitData CreateUnitFromTemplate(string templateId, int level, Random rng)
    {
        var tpl = UnitTemplateDB.GetTemplateById(templateId);
        if (tpl != null)
        {
            var unit = CharacterGenerator.GenerateFromTemplate(tpl, level);
            unit.UnitName = $"{unit.UnitName}_{rng.Next(100):D2}";

            if (tpl.ContainsKey("weapon_subtype"))
            {
                var subtype = (WeaponData.WeaponSubtype)tpl["weapon_subtype"].AsInt32();
                var wpn = CreateWeaponForSubtype(subtype, level);
                if (wpn != null)
                    unit.PrimaryMainHand = wpn;
            }

            return unit;
        }

        var fallbackTpl = UnitTemplateDB.GruntGoblinWarrior();
        var fallbackUnit = CharacterGenerator.GenerateFromTemplate(fallbackTpl, level);
        fallbackUnit.UnitName = $"{fallbackUnit.UnitName}_{rng.Next(100):D2}";
        fallbackUnit.IsEnemy = true;
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

        if (baseItem == null)
            return null;

        int itemLevel = Math.Max(1, level);
        return EquipmentGenerator.GenerateRandomWeapon(new[] { baseItem.ItemId }, itemLevel: itemLevel);
    }

    public static string NormalizeTemplateId(string templateId) => templateId switch
    {
        "goblin_chieftain" => "goblin_warrior",
        "kobold_sorcerer" => "kobold_trapper",
        "shadow_acolyte" => "cultist",
        "bandit_warrior" => "bandit",
        "bandit_archer" => "archer",
        "dire_wolf" => "wolf",
        "stone_golem" => "iron_golem",
        "skeleton_warrior" => "grunt_skeleton_warrior",
        "zombie" => "grunt_zombie",
        "wraith" => "cultist",
        _ => templateId,
    };
}
