using Godot;
using System;
using BladeHex.Data;

namespace BladeHex.Strategic;

[GlobalClass]
public partial class OverworldPOI : Resource
{
    /// <summary>POI类型枚举</summary>
    public enum POIType
    {
        Town,        // 城镇（大型，有完整设施）
        Village,     // 村庄（小型居民点）
        Castle,      // 城堡（军事要塞）
        Settlement,  // 外族聚落（敌对）
        Lair,        // 巢穴（可清除的危险地点）
        Tavern,      // 路边旅店（休息/招募/情报）
        Outpost,     // 前哨站（小型军事据点）
        Mine,        // 矿场（资源产出）
        Farm,        // 农庄（食物产出）
        Shrine,      // 药师所/祭坛（祝福/治疗）
        Port,        // 港口（贸易/渡船）
    }

    /// <summary>外族聚落子类型</summary>
    public enum SettlementRace
    {
        Goblin,      // 哥布林营地
        Kobold,      // 狗头人矿坑
        Minotaur,    // 牛头人石堡
        ShadowCult,  // 暗影教团据点
        Bandit,      // 山贼
        Robber,      // 劫匪
        Pirate,      // 海寇
    }

    /// <summary>巢穴子类型</summary>
    public enum LairType
    {
        DragonLair,    // 龙巢
        AncientTomb,   // 古代墓穴
        Ruins,          // 远古遗迹
        GolemForge,    // 魔像工坊
        BanditCamp,    // 山贼窝点
        RobberHideout, // 劫匪窝点
        PirateCove,    // 海寇洞穴
        RaiderOutpost, // 劫掠队据点
    }

    /// <summary>领主性格</summary>
    public enum LordPersonality
    {
        Cautious,   // 谨慎
        Balanced,   // 均衡
        Aggressive, // 激进
    }

    // ========================================
    // 基础字段
    // ========================================

    [Export] public string PoiName { get; set; } = "未命名地点";
    [Export] public POIType PoiTypeEnum = POIType.Village;

    /// <summary>兼容：poi_type 作为 int 访问</summary>
    [Export] public int PoiType
    {
        get => (int)PoiTypeEnum;
        set => PoiTypeEnum = (POIType)value;
    }

    [Export] public Vector2 Position { get; set; } = Vector2.Zero;
    [Export] public string OwningFaction { get; set; } = "neutral";
    [Export] public int Prosperity { get; set; } = 50; // 繁荣度 0-100

    // ========================================
    // 外族聚落专属
    // ========================================

    [Export] public SettlementRace SettlementRaceValue = SettlementRace.Goblin;
    [Export] public float ThreatLevel { get; set; } = 0.5f;
    [Export] public int RaidIntervalDays { get; set; } = 7;
    [Export] public int MaxRaidingParties { get; set; } = 2;

    // ========================================
    // 巢穴专属
    // ========================================

    [Export] public LairType LairTypeValue = LairType.AncientTomb;
    [Export] public int LairLevel { get; set; } = 1;
    [Export] public bool IsCleared { get; set; } = false;

    // ========================================
    // 城镇/村庄/城堡设施
    // ========================================

    [Export] public bool HasTavern { get; set; } = false;
    [Export] public bool HasShop { get; set; } = false;
    [Export] public bool HasBlacksmith { get; set; } = false;
    [Export] public bool HasQuestBoard { get; set; } = true;
    [Export] public bool HasBarracks { get; set; } = false;

    // ========================================
    // 城堡防御
    // ========================================

    [Export] public int CastleDefenseLevel { get; set; } = 1; // 1=木栅, 2=石堡, 3=要塞
    [Export] public int GarrisonMax { get; set; } = 50;
    [Export] public int GarrisonCurrent { get; set; } = 20;

    [Export] public LordPersonality LordPersonalityValue = LordPersonality.Balanced;

    // ========================================
    // 港口专属
    // ========================================

    /// <summary>渡船目的地列表（其他港口的 PoiName）</summary>
    public System.Collections.Generic.List<string> FerryDestinations { get; set; } = new();

    /// <summary>渡船费用（每次）</summary>
    [Export] public int FerryCost { get; set; } = 50;

    /// <summary>是否有造船厂（未来扩展）</summary>
    [Export] public bool HasShipyard { get; set; } = false;

    // ========================================
    // 运行时状态
    // ========================================

    public int DaysSinceLastRaid = 0;
    public int ActiveRaidingParties = 0;
    public bool IsUnderSiege = false;
    public OverworldEntity? SiegeBy;
    public int SiegeDays = 0;
    public OverworldEntity? LastAttackedBy;
    public int LastAttackedDay = 0;

    // ========================================
    // 辅助方法
    // ========================================

    public string GetTypeName() => PoiTypeEnum switch
    {
        POIType.Town => "城镇",
        POIType.Village => "村庄",
        POIType.Castle => "城堡",
        POIType.Settlement => GetSettlementRaceName(),
        POIType.Lair => GetLairTypeName(),
        POIType.Tavern => "旅店",
        POIType.Outpost => "前哨站",
        POIType.Mine => "矿场",
        POIType.Farm => "农庄",
        POIType.Shrine => "药师所",
        POIType.Port => "港口",
        _ => "未知"
    };

    public string GetSettlementRaceName() => SettlementRaceValue switch
    {
        SettlementRace.Goblin => "哥布林营地",
        SettlementRace.Kobold => "狗头人矿坑",
        SettlementRace.Minotaur => "牛头人石堡",
        SettlementRace.ShadowCult => "暗影教团据点",
        _ => "外族聚落"
    };

    public string GetLairTypeName() => LairTypeValue switch
    {
        LairType.DragonLair => "龙巢",
        LairType.AncientTomb => "古代墓穴",
        LairType.Ruins => "远古遗迹",
        LairType.GolemForge => "魔像工坊",
        _ => "未知巢穴"
    };

    public string GetBattleTemplateName() => PoiTypeEnum switch
    {
        POIType.Settlement => SettlementRaceValue switch
        {
            SettlementRace.Goblin => "goblin_camp",
            SettlementRace.Kobold => "kobold_mine",
            SettlementRace.Minotaur => "minotaur_fortress",
            SettlementRace.ShadowCult => "shadow_cult_hideout",
            SettlementRace.Bandit => "bandit_camp",
            SettlementRace.Robber => "robber_hideout",
            SettlementRace.Pirate => "pirate_cove",
            _ => "plain_field"
        },
        POIType.Lair => LairTypeValue switch
        {
            LairType.DragonLair => "dragon_lair",
            LairType.AncientTomb => "ancient_tomb",
            LairType.Ruins => "ruins_exploration",
            LairType.GolemForge => "golem_forge",
            LairType.BanditCamp => "bandit_camp",
            LairType.RobberHideout => "robber_hideout",
            LairType.PirateCove => "pirate_cove",
            LairType.RaiderOutpost => "raider_outpost",
            _ => "plain_field"
        },
        POIType.Village => "village_defense",
        _ => "plain_field"
    };

    public Godot.Collections.Dictionary GetEncounterConfig()
    {
        var config = new Godot.Collections.Dictionary { { "enemies", new Godot.Collections.Array<string>() }, { "cr_total", 0.0f } };
        var enemies = (Godot.Collections.Array<string>)config["enemies"];

        switch (PoiTypeEnum)
        {
            case POIType.Settlement:
                switch (SettlementRaceValue)
                {
                    case SettlementRace.Goblin:
                        enemies.AddRange(new string[] { "goblin_warrior", "goblin_archer", "goblin_chieftain" });
                        config["cr_total"] = 2.0f + ThreatLevel * 2.0f;
                        break;
                    case SettlementRace.Kobold:
                        enemies.AddRange(new string[] { "kobold_trapper", "kobold_sorcerer" });
                        config["cr_total"] = 3.0f + ThreatLevel * 2.0f;
                        break;
                    case SettlementRace.Minotaur:
                        enemies.Add("minotaur_warrior");
                        config["cr_total"] = 5.0f + ThreatLevel * 3.0f;
                        break;
                    case SettlementRace.ShadowCult:
                        enemies.AddRange(new string[] { "cultist", "shadow_acolyte" });
                        config["cr_total"] = 4.0f + ThreatLevel * 3.0f;
                        break;
                }
                break;
            case POIType.Lair:
                switch (LairTypeValue)
                {
                    case LairType.DragonLair:
                        enemies.Add("dragon");
                        config["cr_total"] = 10.0f * LairLevel;
                        break;
                    case LairType.AncientTomb:
                        enemies.AddRange(new string[] { "skeleton_warrior", "zombie", "wraith" });
                        config["cr_total"] = 3.0f * LairLevel;
                        break;
                    case LairType.Ruins:
                        enemies.AddRange(new string[] { "stone_golem", "iron_golem" });
                        config["cr_total"] = 4.0f * LairLevel;
                        break;
                    case LairType.GolemForge:
                        enemies.AddRange(new string[] { "fire_golem", "iron_golem" });
                        config["cr_total"] = 5.0f * LairLevel;
                        break;
                }
                break;
        }
        return config;
    }

    public bool ShouldSpawnRaidParty()
    {
        if (PoiTypeEnum != POIType.Settlement) return false;
        if (ActiveRaidingParties >= MaxRaidingParties) return false;
        if (DaysSinceLastRaid < RaidIntervalDays) return false;
        if (IsUnderSiege) return false;
        return true;
    }

    public void BeginSiege(OverworldEntity attacker)
    {
        IsUnderSiege = true;
        SiegeBy = attacker;
        SiegeDays = 0;
    }

    public void EndSiege()
    {
        IsUnderSiege = false;
        SiegeBy = null;
        SiegeDays = 0;
    }

    public void OnAttacked(OverworldEntity attacker, int currentDay)
    {
        LastAttackedBy = attacker;
        LastAttackedDay = currentDay;
    }

    public bool NeedsReinforcement()
    {
        if (IsUnderSiege) return true;
        if (LastAttackedDay > 0) return true;
        return Prosperity < 30;
    }

    public void OnRaidPartySpawned()
    {
        DaysSinceLastRaid = 0;
        ActiveRaidingParties++;
    }

public void OnRaidPartyDestroyed()
    {
        ActiveRaidingParties = Math.Max(0, ActiveRaidingParties - 1);
    }

    // ========================================
    // 战斗桥梁方法 — POI 防御与围攻
    // ========================================

    /// <summary>
    /// 生成防御部署 — 根据 POI 类型和驻军配置生成防御战斗单位
    /// </summary>
    public BattleUnitDeployment[] GenerateDefenseDeployment()
    {
        var deployments = new System.Collections.Generic.List<BattleUnitDeployment>();

        switch (PoiTypeEnum)
        {
            case POIType.Town:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "town_militia",
                    Count = Math.Max(1, GarrisonCurrent / 5),
                    LevelOverride = 1,
                    DeployZone = "front_line",
                });
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "town_guard",
                    Count = Math.Max(1, GarrisonCurrent / 10),
                    LevelOverride = 2,
                    DeployZone = "back_line",
                });
                break;

            case POIType.Village:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "village_militia",
                    Count = Math.Max(1, GarrisonCurrent / 5),
                    LevelOverride = 1,
                    DeployZone = "front_line",
                });
                break;

            case POIType.Castle:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "castle_knight",
                    Count = Math.Max(1, GarrisonCurrent / 10),
                    LevelOverride = CastleDefenseLevel + 2,
                    DeployZone = "front_line",
                });
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "castle_archer",
                    Count = Math.Max(1, GarrisonCurrent / 5),
                    LevelOverride = CastleDefenseLevel,
                    DeployZone = "back_line",
                });
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = "castle_infantry",
                    Count = Math.Max(1, GarrisonCurrent / 3),
                    LevelOverride = CastleDefenseLevel + 1,
                    DeployZone = "front_line",
                });
                break;

            case POIType.Settlement:
                var race = SettlementRaceValue;
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = race switch
                    {
                        SettlementRace.Goblin => "goblin_warrior",
                        SettlementRace.Kobold => "kobold_trapper",
                        SettlementRace.Minotaur => "minotaur_warrior",
                        SettlementRace.ShadowCult => "cultist",
                        _ => "goblin_warrior",
                    },
                    Count = Math.Max(1, (int)(ThreatLevel * 10)),
                    LevelOverride = (int)(ThreatLevel * 3),
                    DeployZone = "front_line",
                });
                break;

            case POIType.Lair:
                deployments.Add(new BattleUnitDeployment
                {
                    UnitTemplateId = LairTypeValue switch
                    {
                        LairType.DragonLair => "dragon",
                        LairType.AncientTomb => "undead_guardian",
                        LairType.Ruins => "construct",
                        LairType.GolemForge => "iron_golem",
                        _ => "unknown_boss",
                    },
                    Count = 1,
                    LevelOverride = LairLevel,
                    DeployZone = "front_line",
                });
                break;
        }

        return deployments.ToArray();
    }

    /// <summary>
    /// 应用围攻结果 — 更新 POI 状态
    /// </summary>
    public void ApplySiegeOutcome(BattleOutcome outcome)
    {
        if (outcome == null) return;

        // 结束围攻状态
        EndSiege();

        if (outcome.AttackerWon)
        {
            // POI 被攻占
            OwningFaction = "hostile"; // 或由 BattleOutcome 指定
            Prosperity = Math.Max(10, outcome.NewProsperity);
            GarrisonCurrent = outcome.NewGarrisonSize;
        }
        else
        {
            // 防御成功，但驻军受损
            int losses = (int)(GarrisonCurrent * outcome.DefenderLossPercent);
            GarrisonCurrent = Math.Max(0, GarrisonCurrent - losses);
        }
    }

    /// <summary>
    /// 获取 POI 的防御力量（用于 AI 评估）
    /// </summary>
    public float GetDefensePower()
    {
        float basePower = PoiTypeEnum switch
        {
            POIType.Castle => CastleDefenseLevel * 10 + GarrisonCurrent * 2.0f,
            POIType.Town => GarrisonCurrent * 1.5f,
            POIType.Village => GarrisonCurrent * 1.0f,
            POIType.Settlement => ThreatLevel * 15,
            POIType.Lair => LairLevel * 20,
            _ => 5.0f,
        };

        // 围攻加成
        if (IsUnderSiege) basePower *= 0.8f;

        return basePower;
}

// ========================================
// 序列化
// ========================================

public Godot.Collections.Dictionary Serialize()
{
    var data = new Godot.Collections.Dictionary
    {
        { "poi_name", PoiName },
        { "poi_type", (int)PoiTypeEnum },
        { "position_x", Position.X },
        { "position_y", Position.Y },
        { "owning_faction", OwningFaction },
        { "prosperity", Prosperity },
        { "settlement_race", (int)SettlementRaceValue },
        { "threat_level", ThreatLevel },
        { "raid_interval_days", RaidIntervalDays },
        { "max_raiding_parties", MaxRaidingParties },
        { "lair_type", (int)LairTypeValue },
        { "lair_level", LairLevel },
        { "is_cleared", IsCleared },
        { "has_tavern", HasTavern },
        { "has_shop", HasShop },
        { "has_blacksmith", HasBlacksmith },
        { "has_quest_board", HasQuestBoard },
        { "has_barracks", HasBarracks },
        { "castle_defense_level", CastleDefenseLevel },
        { "garrison_max", GarrisonMax },
        { "garrison_current", GarrisonCurrent },
        { "lord_personality", (int)LordPersonalityValue },
        { "ferry_cost", FerryCost },
        { "has_shipyard", HasShipyard },
        { "days_since_last_raid", DaysSinceLastRaid },
        { "active_raiding_parties", ActiveRaidingParties },
        { "is_under_siege", IsUnderSiege },
        { "siege_days", SiegeDays },
        { "last_attacked_day", LastAttackedDay },
    };
    return data;
}

public void OnDayPassed()
{
    if (PoiTypeEnum == POIType.Settlement)
        DaysSinceLastRaid++;

    // 繁荣度自然恢复
    if (Prosperity < 50 && !IsUnderSiege)
        Prosperity = Math.Min(50, Prosperity + 1);

    // 围攻天数递增
    if (IsUnderSiege)
    {
        SiegeDays++;
        Prosperity = Math.Max(0, Prosperity - 2);
    }

    // 守军自然恢复
    if (PoiTypeEnum == POIType.Castle && GarrisonCurrent < GarrisonMax)
        GarrisonCurrent = Math.Min(GarrisonMax, GarrisonCurrent + 2);
}
}
