using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 委托遭遇数据组件 — 存储任务目标点关联的敌方单位配置
/// </summary>
public partial class QuestEncounterData : RefCounted
{
    // ========================================
    // 敌方组配置
    // ========================================

    public class EnemyGroup
    {
        public string TemplateId = "";
        public int Count = 1;
        public int LevelOffset = 0;

        public Godot.Collections.Dictionary Serialize() => new()
        {
            { "template_id", TemplateId },
            { "count", Count },
            { "level_offset", LevelOffset }
        };

        public static EnemyGroup Deserialize(Godot.Collections.Dictionary data) => new()
        {
            TemplateId = data.ContainsKey("template_id") ? (string)data["template_id"] : "",
            Count = data.ContainsKey("count") ? (int)data["count"] : 1,
            LevelOffset = data.ContainsKey("level_offset") ? (int)data["level_offset"] : 0
        };
    }

    // ========================================
    // 数据字段
    // ========================================

    public List<EnemyGroup> EnemyGroups = new();
    public float CrTotal = 1.0f;
    public string[] BattleModifiers = Array.Empty<string>();

    // ========================================
    // 工厂方法
    // ========================================

    public static QuestEncounterData CreateFromQuest(QuestData quest, QuestTargetSite.SiteType siteType)
    {
        var data = new QuestEncounterData();
        int difficulty = (int)quest.difficulty;
        int targetCount = quest.TargetCount;

        switch (siteType)
        {
            case QuestTargetSite.SiteType.GoblinCamp:
                data.EnemyGroups = MakeGroups(["goblin_warrior", "goblin_archer", "goblin_chieftain"], [0.5f, 0.35f, 0.15f], targetCount, difficulty);
                break;
            case QuestTargetSite.SiteType.KoboldMine:
                data.EnemyGroups = MakeGroups(["kobold_trapper", "kobold_sorcerer"], [0.6f, 0.4f], targetCount, difficulty);
                break;
            case QuestTargetSite.SiteType.MinotaurFort:
                data.EnemyGroups = MakeGroups(["minotaur_warrior"], [1.0f], Math.Min(targetCount, 4), difficulty);
                break;
            case QuestTargetSite.SiteType.CultHideout:
                data.EnemyGroups = MakeGroups(["cultist", "shadow_acolyte"], [0.5f, 0.5f], targetCount, difficulty);
                data.BattleModifiers = ["dark"];
                break;
            case QuestTargetSite.SiteType.BanditCamp:
                data.EnemyGroups = MakeGroups(["bandit_warrior", "bandit_archer"], [0.55f, 0.45f], targetCount, difficulty);
                break;
            case QuestTargetSite.SiteType.WolfDen:
                data.EnemyGroups = MakeGroups(["wolf", "dire_wolf"], [0.7f, 0.3f], targetCount, difficulty);
                break;
            case QuestTargetSite.SiteType.Ruins:
                data.EnemyGroups = MakeGroups(["stone_golem", "iron_golem"], [0.6f, 0.4f], Math.Max(targetCount / 2, 2), difficulty);
                break;
            case QuestTargetSite.SiteType.Tomb:
                data.EnemyGroups = MakeGroups(["skeleton_warrior", "zombie", "wraith"], [0.4f, 0.35f, 0.25f], targetCount, difficulty);
                data.BattleModifiers = ["undead"];
                break;
            case QuestTargetSite.SiteType.DragonLair:
                data.EnemyGroups = MakeGroups(["dragon"], [1.0f], 1, difficulty);
                data.CrTotal = 10.0f + difficulty * 5.0f;
                break;
            case QuestTargetSite.SiteType.VillageThreat:
                data.EnemyGroups = MakeGroups(["goblin_warrior", "goblin_archer"], [0.6f, 0.4f], targetCount * 2, difficulty);
                data.BattleModifiers = ["defense"];
                break;
            default:
                data.EnemyGroups = MakeGroups(["goblin_warrior"], [1.0f], targetCount, difficulty);
                break;
        }

        if (siteType != QuestTargetSite.SiteType.DragonLair)
            data.CrTotal = CalculateCr(data.EnemyGroups, difficulty);

        return data;
    }

    public Godot.Collections.Dictionary ToEncounterConfig()
    {
        var enemyIds = new Godot.Collections.Array<string>();
        foreach (var group in EnemyGroups)
            for (int i = 0; i < group.Count; i++) enemyIds.Add(group.TemplateId);

        return new Godot.Collections.Dictionary
        {
            { "enemies", enemyIds },
            { "cr_total", CrTotal },
            { "battle_modifiers", new Godot.Collections.Array<string>(BattleModifiers) }
        };
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var groupsData = new Godot.Collections.Array();
        foreach (var g in EnemyGroups) groupsData.Add(g.Serialize());

        return new Godot.Collections.Dictionary
        {
            { "enemy_groups", groupsData },
            { "cr_total", CrTotal },
            { "battle_modifiers", new Godot.Collections.Array<string>(BattleModifiers) }
        };
    }

    public static QuestEncounterData Deserialize(Godot.Collections.Dictionary data)
    {
        var enc = new QuestEncounterData();
        enc.CrTotal = data.ContainsKey("cr_total") ? (float)data["cr_total"] : 1.0f;
        var modifiersArr = data.ContainsKey("battle_modifiers") ? (Godot.Collections.Array)data["battle_modifiers"] : new Godot.Collections.Array();
        enc.BattleModifiers = modifiersArr.Select(x => x.ToString()!).ToArray();

        var groupsData = data.ContainsKey("enemy_groups") ? (Godot.Collections.Array)data["enemy_groups"] : new Godot.Collections.Array();
        foreach (Godot.Collections.Dictionary gd in groupsData)
            enc.EnemyGroups.Add(EnemyGroup.Deserialize(gd));

        return enc;
    }

    // ========================================
    // 内部工具
    // ========================================

    private static List<EnemyGroup> MakeGroups(string[] templateIds, float[] weights, int totalCount, int difficulty)
    {
        var groups = new List<EnemyGroup>();
        int remaining = totalCount;

        for (int i = 0; i < templateIds.Length; i++)
        {
            var group = new EnemyGroup { TemplateId = templateIds[i] };
            if (i == templateIds.Length - 1) group.Count = Math.Max(remaining, 1);
            else
            {
                float weight = i < weights.Length ? weights[i] : 1.0f / templateIds.Length;
                group.Count = Math.Max((int)Math.Round(totalCount * weight), 1);
                remaining = Math.Max(remaining - group.Count, 1);
            }

            group.LevelOffset = difficulty switch
            {
                3 => 1, // HARD (QuestData.QuestDifficulty.HARD)
                4 => 2, // BOSS
                _ => 0
            };
            groups.Add(group);
        }
        return groups;
    }

    private static float CalculateCr(List<EnemyGroup> groups, int difficulty)
    {
        int totalUnits = 0;
        foreach (var g in groups) totalUnits += g.Count;
        float baseCr = totalUnits * 0.5f;
        float diffMult = difficulty switch
        {
            1 => 0.8f, // EASY
            2 => 1.0f, // MEDIUM
            3 => 1.5f, // HARD
            4 => 3.0f, // BOSS
            _ => 1.0f
        };
        return baseCr * diffMult;
    }
}
