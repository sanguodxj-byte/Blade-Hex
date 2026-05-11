using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 敌方单位生成器 — 120级等级体系
/// </summary>
public static class EnemyGenerator
{
    public static UnitData? GenerateEnemy(string templateId, int levelAdjustment = 0)
    {
        var tpl = UnitTemplateDB.GetTemplateById(templateId);
        
        if (tpl == null || tpl.Count == 0)
        {
            // 回退到 PrototypeData
            var protoEnemies = PrototypeData.GetWeapons(); // 这里 PrototypeData.gd 原本可能有 get_enemies()
            // 暂时返回 null 或 默认
            GD.PushError("敌人模板不存在: " + templateId);
            return null;
        }

        var enemy = UnitTemplateDB.InstantiateTemplate(tpl);
        if (enemy == null) return null;

        if (levelAdjustment != 0) ApplyLevelScaling(enemy, levelAdjustment);

        if (enemy.enemyType == UnitData.EnemyType.Humanoid) EquipRandomGear(enemy);

        return enemy;
    }

    public static List<UnitData> GenerateEncounter(int partyLevel, int partySize, float difficulty = 1.0f)
    {
        List<UnitData> encounter = new();
        int levelBudget = CalculateLevelBudget(partyLevel, partySize, difficulty);

        var candidates = UnitTemplateDB.GetAllTemplates().Where(t => (int)t["level"] <= levelBudget * 1.2f).ToList();
        if (candidates.Count == 0) return encounter;

        int currentLevel = 0;
        var rand = new Random();
        int attempts = 0;

        while (currentLevel < levelBudget && candidates.Count > 0 && attempts < 20)
        {
            var tpl = candidates[rand.Next(candidates.Count)];
            int enemyLevel = (int)tpl["level"];

            if (currentLevel + enemyLevel <= levelBudget * 1.2f)
            {
                var enemy = UnitTemplateDB.InstantiateTemplate(tpl);
                if (enemy != null)
                {
            if (enemy.enemyType == UnitData.EnemyType.Humanoid) EquipRandomGear(enemy);
                    encounter.Add(enemy);
                    currentLevel += enemyLevel;
                }
            }
            else
            {
                candidates.Remove(tpl);
            }
            attempts++;
        }

        return encounter;
    }

    private static int CalculateLevelBudget(int partyLevel, int partySize, float difficulty)
    {
        return (int)(partyLevel * partySize * 0.8f * difficulty);
    }

    private static void EquipRandomGear(UnitData enemy)
    {
        int level = enemy.Level;
        float cr = enemy.ThreatLevel;
        int itemLevel = (cr > 0) ? EquipmentGenerator.GetItemLevelFromCr(cr) : level;
        string difficulty = (cr > 0) ? EquipmentGenerator.GetDifficultyFromCr(cr) : "normal";

        var rand = new Random();

        // 武器
        if (enemy.PrimaryMainHand == null)
        {
            if (rand.NextDouble() < 0.6)
                enemy.PrimaryMainHand = EquipmentGenerator.GenerateRandomWeapon(new[] { "longsword", "greatsword", "spear" }, (ItemData.Rarity)(-1), itemLevel, difficulty);
            else
                enemy.PrimaryMainHand = EquipmentGenerator.GenerateRandomWeapon(new[] { "longbow", "crossbow" }, (ItemData.Rarity)(-1), itemLevel, difficulty);
        }

        // 防具
        if (rand.NextDouble() < 0.7)
            enemy.Armor = EquipmentGenerator.GenerateRandomArmor(null, (ItemData.Rarity)(-1), itemLevel, difficulty);

        // 坐骑刷新加成
        enemy.RefreshAccessoryBonuses();
    }

    public static void ApplyLevelScaling(UnitData enemy, int levelAdjustment)
    {
        if (levelAdjustment == 0) return;

        int oldLevel = enemy.Level;
        int newLevel = Mathf.Clamp(oldLevel + levelAdjustment, 1, 120);

        if (newLevel == oldLevel) return;

        int oldPoints = RPGRuleEngine.GetTotalAttrPoints(oldLevel);
        int newPoints = RPGRuleEngine.GetTotalAttrPoints(newLevel);

        if (oldPoints <= 0) return;

        float scale = (float)newPoints / oldPoints;

        enemy.Str = Mathf.Clamp((int)(enemy.Str * scale), 1, 99);
        enemy.Dex = Mathf.Clamp((int)(enemy.Dex * scale), 1, 99);
        enemy.Con = Mathf.Clamp((int)(enemy.Con * scale), 1, 99);
        enemy.Intel = Mathf.Clamp((int)(enemy.Intel * scale), 1, 99);
        enemy.Wis = Mathf.Clamp((int)(enemy.Wis * scale), 1, 99);
        enemy.Cha = Mathf.Clamp((int)(enemy.Cha * scale), 1, 99);

        // 重算 HP
        int conMod = RPGRuleEngine.GetStatModifier(enemy.Con);
        // 简化重算 HP 逻辑
        enemy.BaseMaxHp = (int)(enemy.BaseMaxHp * scale); 

        enemy.Level = newLevel;
        enemy.ThreatLevel = RPGRuleEngine.GetCrFromLevel(newLevel);
    }
}
