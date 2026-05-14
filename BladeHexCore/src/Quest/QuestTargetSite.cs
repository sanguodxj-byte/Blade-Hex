using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 委托任务目标点 — 大地图上接取任务时生成的建筑瓦片/地标数据模型
/// </summary>
[GlobalClass]
public partial class QuestTargetSite : Resource
{
    // ========================================
    // 目标点类型
    // ========================================

    public enum SiteType
    {
        GoblinCamp,
        KoboldMine,
        MinotaurFort,
        CultHideout,
        BanditCamp,
        WolfDen,
        DungeonEntrance,
        Ruins,
        Tomb,
        DragonLair,
        Signpost,
        VillageThreat,
    }

    // ========================================
    // 核心数据
    // ========================================

    public string QuestId = "";
    public string SiteName = "";
    public SiteType CurrentSiteType = SiteType.GoblinCamp;
    public Vector2 WorldPosition = Vector2.Zero;
    public QuestEncounterData? EncounterData = null;
    public bool IsCleared = false;
    public bool IsVisibleToPlayer = false;
    public int DangerStars = 1;

    // ========================================
    // 工厂方法
    // ========================================

    public static QuestTargetSite CreateFromQuest(QuestData quest)
    {
        var site = new QuestTargetSite();
        site.QuestId = quest.QuestId;
        site.SiteName = quest.TargetDescription;
        site.WorldPosition = quest.TargetWorldPosition;
        site.DangerStars = DifficultyToStars((int)quest.difficulty);
        site.CurrentSiteType = (SiteType)InferSiteType(quest);
        site.EncounterData = QuestEncounterData.CreateFromQuest(quest, site.CurrentSiteType);
        return site;
    }

    // ========================================
    // 辅助方法
    // ========================================

    public string GetBattleTemplateName() => CurrentSiteType switch
    {
        SiteType.GoblinCamp => "goblin_camp",
        SiteType.KoboldMine => "kobold_mine",
        SiteType.MinotaurFort => "minotaur_fortress",
        SiteType.CultHideout => "shadow_cult_hideout",
        SiteType.BanditCamp => "bandit_camp",
        SiteType.WolfDen => "wolf_den",
        SiteType.DungeonEntrance => "dungeon_entrance",
        SiteType.Ruins => "ruins_exploration",
        SiteType.Tomb => "ancient_tomb",
        SiteType.DragonLair => "dragon_lair",
        SiteType.Signpost => "plain_field",
        SiteType.VillageThreat => "village_defense",
        _ => "plain_field"
    };

    public Color GetDisplayColor() => CurrentSiteType switch
    {
        SiteType.GoblinCamp => new Color(0.6f, 0.45f, 0.2f),
        SiteType.KoboldMine => new Color(0.5f, 0.4f, 0.3f),
        SiteType.MinotaurFort => new Color(0.75f, 0.3f, 0.15f),
        SiteType.CultHideout => new Color(0.4f, 0.15f, 0.55f),
        SiteType.BanditCamp => new Color(0.55f, 0.35f, 0.2f),
        SiteType.WolfDen => new Color(0.5f, 0.5f, 0.45f),
        SiteType.DungeonEntrance => new Color(0.35f, 0.35f, 0.45f),
        SiteType.Ruins => new Color(0.6f, 0.55f, 0.35f),
        SiteType.Tomb => new Color(0.4f, 0.4f, 0.5f),
        SiteType.DragonLair => new Color(0.85f, 0.6f, 0.1f),
        SiteType.Signpost => new Color(0.3f, 0.6f, 0.8f),
        SiteType.VillageThreat => new Color(0.8f, 0.2f, 0.2f),
        _ => new Color(0.7f, 0.5f, 0.3f)
    };

    public string GetSiteTypeName() => CurrentSiteType switch
    {
        SiteType.GoblinCamp => "哥布林营地",
        SiteType.KoboldMine => "狗头人矿坑",
        SiteType.MinotaurFort => "牛头人石堡",
        SiteType.CultHideout => "暗影教团据点",
        SiteType.BanditCamp => "强盗营地",
        SiteType.WolfDen => "狼穴",
        SiteType.DungeonEntrance => "地下城入口",
        SiteType.Ruins => "遗迹",
        SiteType.Tomb => "墓穴",
        SiteType.DragonLair => "龙巢",
        SiteType.Signpost => "集结点",
        SiteType.VillageThreat => "受威胁村庄",
        _ => "未知地点"
    };

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary
        {
            { "quest_id", QuestId },
            { "site_name", SiteName },
            { "site_type", (int)CurrentSiteType },
            { "world_position", new Godot.Collections.Array { WorldPosition.X, WorldPosition.Y } },
            { "is_cleared", IsCleared },
            { "danger_stars", DangerStars }
        };
        if (EncounterData != null) data["encounter_data"] = EncounterData.Serialize();
        return data;
    }

    public static QuestTargetSite Deserialize(Godot.Collections.Dictionary data)
    {
        var site = new QuestTargetSite();
        site.QuestId = data.ContainsKey("quest_id") ? (string)data["quest_id"] : "";
        site.SiteName = data.ContainsKey("site_name") ? (string)data["site_name"] : "";
        site.CurrentSiteType = (SiteType)(data.ContainsKey("site_type") ? (int)data["site_type"] : 0);
        var posArr = data.ContainsKey("world_position") ? (Godot.Collections.Array)data["world_position"] : new Godot.Collections.Array { 0.0f, 0.0f };
        site.WorldPosition = new Vector2((float)posArr[0], (float)posArr[1]);
        site.IsCleared = data.ContainsKey("is_cleared") ? (bool)data["is_cleared"] : false;
        site.DangerStars = data.ContainsKey("danger_stars") ? (int)data["danger_stars"] : 1;
        if (data.ContainsKey("encounter_data"))
            site.EncounterData = QuestEncounterData.Deserialize((Godot.Collections.Dictionary)data["encounter_data"]);
        return site;
    }

    // ========================================
    // 内部工具
    // ========================================

    public static int InferSiteType(QuestData quest)
    {
        string desc = quest.TargetDescription.ToLower();
        if (desc.Contains("哥布林") || desc.Contains("goblin")) return (int)SiteType.GoblinCamp;
        if (desc.Contains("狗头人") || desc.Contains("kobold") || desc.Contains("矿")) return (int)SiteType.KoboldMine;
        if (desc.Contains("牛头人") || desc.Contains("minotaur")) return (int)SiteType.MinotaurFort;
        if (desc.Contains("教团") || desc.Contains("cult") || desc.Contains("暗影")) return (int)SiteType.CultHideout;
        if (desc.Contains("强盗") || desc.Contains("bandit")) return (int)SiteType.BanditCamp;
        if (desc.Contains("狼") || desc.Contains("wolf")) return (int)SiteType.WolfDen;
        if (desc.Contains("龙") || desc.Contains("dragon")) return (int)SiteType.DragonLair;
        if (desc.Contains("遗迹") || desc.Contains("ruins")) return (int)SiteType.Ruins;
        if (desc.Contains("墓穴") || desc.Contains("墓") || desc.Contains("tomb")) return (int)SiteType.Tomb;
        if (desc.Contains("地下") || desc.Contains("地牢") || desc.Contains("dungeon")) return (int)SiteType.DungeonEntrance;

        return (int)quest.questType switch
        {
            (int)QuestData.QuestType.Extermination => (int)SiteType.GoblinCamp,
            (int)QuestData.QuestType.Exploration => (int)SiteType.Ruins,
            (int)QuestData.QuestType.Escort => (int)SiteType.Signpost,
            (int)QuestData.QuestType.Defense => (int)SiteType.VillageThreat,
            (int)QuestData.QuestType.Emergency => (int)SiteType.CultHideout,
            _ => (int)SiteType.GoblinCamp
        };
    }

    private static int DifficultyToStars(int diff) => diff switch
    {
        1 => 1, // EASY (QuestData.QuestDifficulty)
        2 => 2, // MEDIUM
        3 => 3, // HARD
        4 => 5, // BOSS
        _ => 1
    };
}
