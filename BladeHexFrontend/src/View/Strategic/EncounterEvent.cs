// EncounterEvent.cs
// 遭遇事件系统 — 将叙事描述与游戏机制绑定
// 每个遭遇不仅有文字描述，还携带实际的游戏效果（先手/士气/地形/选项修正等）
// 通过EventBus广播，让各系统响应遭遇产生的机制影响
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Events;

namespace BladeHex.Strategic;

/// <summary>
/// 遭遇事件数据 — 描述一次遭遇的叙事内容和机制效果
/// </summary>
public class EncounterEvent
{
    // ============================================================================
    // 遭遇类型
    // ============================================================================
    public enum EncounterType
    {
        TownEntry,          // 进入城镇
        VillageEntry,       // 进入村庄
        CastleEntry,        // 进入城堡
        BanditAmbush,       // 山贼伏击
        RaiderAttack,       // 外族掠夺
        LordArmyHostile,    // 敌对领主军
        LordArmyNeutral,    // 中立领主军
        MonsterEncounter,   // 史诗怪物
        PirateAmbush,       // 海寇伏击
        AdventurerMeet,     // 遇到冒险者
        CaravanMeet,        // 遇到商队
        KnightMeet,         // 遇到流浪骑士
        GenericHostile,     // 通用敌对
        GenericNeutral,     // 通用中立
    }

    // ============================================================================
    // 机制效果标签
    // ============================================================================
    [Flags]
    public enum EffectFlags
    {
        None = 0,
        EnemyHasInitiative = 1 << 0,    // 敌人先手（伏击成功）
        PlayerSurprised = 1 << 1,       // 玩家被突袭（第一回合无法行动）
        TerrainNarrow = 1 << 2,         // 狭窄地形（无法展开阵型）
        TerrainHighGround = 1 << 3,     // 敌人占据高地
        MoraleShock = 1 << 4,           // 士气冲击（初始士气-10）
        CanFlee = 1 << 5,              // 可以逃跑
        CanNegotiate = 1 << 6,         // 可以谈判
        CanBribe = 1 << 7,             // 可以贿赂
        CanTrade = 1 << 8,             // 可以交易
        CanRecruit = 1 << 9,           // 可以招募
        CanRest = 1 << 10,            // 可以休息
        EnemySurrounded = 1 << 11,     // 敌人包围了玩家
        WeatherPenalty = 1 << 12,      // 天气惩罚（雨天远程-2）
        NightPenalty = 1 << 13,        // 夜间惩罚（视野减半）
        QuestRelated = 1 << 14,        // 与任务相关
        ReputationAffected = 1 << 15,  // 影响声望
    }

    // ============================================================================
    // 数据字段
    // ============================================================================
    public EncounterType Type { get; set; }
    public string NarrativeText { get; set; } = "";
    public EffectFlags Effects { get; set; } = EffectFlags.None;
    public string EntityName { get; set; } = "";
    public int EntityCount { get; set; } = 1;
    public int EntityLevel { get; set; } = 1;
    public float ThreatRating { get; set; } = 1.0f;

    // 机制修正值
    public int InitiativeModifier { get; set; } = 0;    // 先攻修正
    public int MoraleModifier { get; set; } = 0;        // 士气修正
    public int FleeChancePercent { get; set; } = 50;    // 逃跑成功率
    public int BribeCost { get; set; } = 0;             // 贿赂费用
    public string TerrainOverride { get; set; } = "";   // 地形覆盖（伏击地形）

    // ============================================================================
    // 工厂方法 — 根据实体生成遭遇事件
    // ============================================================================
    public static EncounterEvent FromTown(OverworldTown town)
    {
        var evt = new EncounterEvent
        {
            Type = town.TownType == "village" ? EncounterType.VillageEntry : EncounterType.TownEntry,
            EntityName = town.TownName,
            EntityCount = town.Garrison,
            Effects = EffectFlags.CanTrade | EffectFlags.CanRecruit | EffectFlags.CanRest,
            NarrativeText = InteractionDescriptions.GetTownDescription(
                town.TownName, town.TownType, town.Prosperity, town.Garrison),
        };
        return evt;
    }

    public static EncounterEvent FromEnemy(OverworldEnemy enemy)
    {
        var entity = enemy.EntityRef;
        int partySize = entity?.PartySize ?? 1;
        int level = entity?.PartyLevel ?? 1;
        string entityType = entity?.EntityTypeEnum.ToString() ?? "BanditParty";
        bool isHostile = enemy.IsHostile;

        var evt = new EncounterEvent
        {
            EntityName = enemy.GetDisplayName(),
            EntityCount = partySize,
            EntityLevel = level,
            ThreatRating = entity?.CombatPower ?? 10f,
        };

        // 根据实体类型设置遭遇类型和效果
        if (isHostile)
        {
            evt.Type = entityType switch
            {
                "BanditParty" or "RobberParty" => EncounterType.BanditAmbush,
                "RaidingParty" => EncounterType.RaiderAttack,
                "LordArmy" => EncounterType.LordArmyHostile,
                "EpicMonster" => EncounterType.MonsterEncounter,
                "PirateCrew" => EncounterType.PirateAmbush,
                _ => EncounterType.GenericHostile,
            };

            // 伏击类遭遇：敌人有先手+地形优势
            if (evt.Type == EncounterType.BanditAmbush)
            {
                evt.Effects = EffectFlags.EnemyHasInitiative | EffectFlags.EnemySurrounded | EffectFlags.CanFlee | EffectFlags.CanBribe;
                evt.InitiativeModifier = -4;
                evt.MoraleModifier = -5;
                evt.FleeChancePercent = 40;
                evt.BribeCost = partySize * 20 + level * 10;
                evt.TerrainOverride = "forest_ambush";
            }
            else if (evt.Type == EncounterType.RaiderAttack)
            {
                evt.Effects = EffectFlags.EnemyHasInitiative | EffectFlags.CanFlee;
                evt.InitiativeModifier = -2;
                evt.MoraleModifier = -8;
                evt.FleeChancePercent = 30; // 骑兵难逃
                evt.TerrainOverride = "plains_open";
            }
            else if (evt.Type == EncounterType.LordArmyHostile)
            {
                evt.Effects = EffectFlags.CanFlee | EffectFlags.CanNegotiate | EffectFlags.ReputationAffected;
                evt.InitiativeModifier = 0;
                evt.MoraleModifier = -3;
                evt.FleeChancePercent = 45;
            }
            else if (evt.Type == EncounterType.MonsterEncounter)
            {
                evt.Effects = EffectFlags.MoraleShock | EffectFlags.CanFlee;
                evt.InitiativeModifier = -2;
                evt.MoraleModifier = -15;
                evt.FleeChancePercent = 60;
            }
            else if (evt.Type == EncounterType.PirateAmbush)
            {
                evt.Effects = EffectFlags.TerrainNarrow | EffectFlags.EnemyHasInitiative | EffectFlags.CanBribe;
                evt.InitiativeModifier = -3;
                evt.BribeCost = partySize * 15;
                evt.TerrainOverride = "river_bank";
            }
            else
            {
                evt.Effects = EffectFlags.CanFlee;
                evt.FleeChancePercent = 50;
            }
        }
        else
        {
            // 中立遭遇
            evt.Type = entityType switch
            {
                "Adventurer" => EncounterType.AdventurerMeet,
                "Caravan" => EncounterType.CaravanMeet,
                "LordArmy" => EncounterType.LordArmyNeutral,
                _ => EncounterType.GenericNeutral,
            };
            evt.Effects = EffectFlags.CanTrade | EffectFlags.CanNegotiate | EffectFlags.CanFlee;
            evt.FleeChancePercent = 90;
        }

        evt.NarrativeText = InteractionDescriptions.GetEnemyDescription(
            evt.EntityName, isHostile, partySize, entityType);

        return evt;
    }

    // ============================================================================
    // 发布到EventBus
    // ============================================================================
    public void Publish()
    {
        var bus = EventBus.Instance;
        if (bus == null) return;

        var data = new Godot.Collections.Dictionary
        {
            { "encounter_type", (int)Type },
            { "entity_name", EntityName },
            { "entity_count", EntityCount },
            { "entity_level", EntityLevel },
            { "threat_rating", ThreatRating },
            { "effects", (int)Effects },
            { "initiative_mod", InitiativeModifier },
            { "morale_mod", MoraleModifier },
            { "flee_chance", FleeChancePercent },
            { "bribe_cost", BribeCost },
            { "terrain_override", TerrainOverride },
            { "narrative", NarrativeText },
        };

        bus.Publish(EncounterSignals.EncounterStarted, data);
    }

    // ============================================================================
    // 查询效果
    // ============================================================================
    public bool HasEffect(EffectFlags flag) => (Effects & flag) != 0;
}

/// <summary>
/// 遭遇事件信号常量 — 扩展EventBus.Signals
/// </summary>
public static class EncounterSignals
{
    public const string EncounterStarted = "encounter_started";
    public const string EncounterResolved = "encounter_resolved";
    public const string EncounterFled = "encounter_fled";
    public const string EncounterBribed = "encounter_bribed";
    public const string EncounterNegotiated = "encounter_negotiated";
    public const string TownEntered = "town_entered";
    public const string TownLeft = "town_left";
    public const string TradeCompleted = "trade_completed";
    public const string RecruitCompleted = "recruit_completed";
}
