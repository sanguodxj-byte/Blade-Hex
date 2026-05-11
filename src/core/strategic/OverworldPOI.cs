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
        Town,        // 城镇
        Village,     // 村庄
        Castle,      // 城堡
        Settlement,  // 外族聚落
        Lair,        // 巢穴
    }

    /// <summary>外族聚落子类型</summary>
    public enum SettlementRace
    {
        Goblin,      // 哥布林营地
        Kobold,      // 狗头人矿坑
        Minotaur,    // 牛头人石堡
        ShadowCult,  // 暗影教团据点
    }

    /// <summary>巢穴子类型</summary>
    public enum LairType
    {
        DragonLair,    // 龙巢
        AncientTomb,   // 古代墓穴
        Ruins,          // 远古遗迹
        GolemForge,    // 魔像工坊
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

    [Export] public string PoiName = "未命名地点";
    [Export] public POIType PoiTypeEnum = POIType.Village;
    [Export] public Vector2 Position = Vector2.Zero;
    [Export] public string OwningFaction = "neutral";
    [Export] public int Prosperity = 50; // 繁荣度 0-100

    // ========================================
    // 外族聚落专属
    // ========================================

    [Export] public SettlementRace SettlementRaceValue = SettlementRace.Goblin;
    [Export] public float ThreatLevel = 0.5f;
    [Export] public int RaidIntervalDays = 7;
    [Export] public int MaxRaidingParties = 2;

    // ========================================
    // 巢穴专属
    // ========================================

    [Export] public LairType LairTypeValue = LairType.AncientTomb;
    [Export] public int LairLevel = 1;
    [Export] public bool IsCleared = false;

    // ========================================
    // 城镇/村庄/城堡设施
    // ========================================

    [Export] public bool HasTavern = false;
    [Export] public bool HasShop = false;
    [Export] public bool HasBlacksmith = false;
    [Export] public bool HasQuestBoard = true;
    [Export] public bool HasBarracks = false;

    // ========================================
    // 城堡防御
    // ========================================

    [Export] public int CastleDefenseLevel = 1; // 1=木栅, 2=石堡, 3=要塞
    [Export] public int GarrisonMax = 50;
    [Export] public int GarrisonCurrent = 20;

    [Export] public LordPersonality LordPersonalityValue = LordPersonality.Balanced;

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
            _ => "plain_field"
        },
        POIType.Lair => LairTypeValue switch
        {
            LairType.DragonLair => "dragon_lair",
            LairType.AncientTomb => "ancient_tomb",
            LairType.Ruins => "ruins_exploration",
            LairType.GolemForge => "golem_forge",
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

    public float GetDefensePower()
    {
        float power = 0.0f;
        switch (PoiTypeEnum)
        {
            case POIType.Town:
                power = 10.0f + Prosperity * 0.3f;
                break;
            case POIType.Village:
                power = 3.0f + Prosperity * 0.1f;
                break;
            case POIType.Castle:
                power = (float)GarrisonCurrent * 1.5f;
                switch (CastleDefenseLevel)
                {
                    case 1: power += 15.0f; break; // 木栅
                    case 2: power += 35.0f; break; // 石堡
                    case 3: power += 60.0f; break; // 要塞
                }
                break;
            case POIType.Settlement:
                power = ThreatLevel * 15.0f + 5.0f;
                break;
            case POIType.Lair:
                power = (float)LairLevel * 10.0f;
                break;
        }
        return power;
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
