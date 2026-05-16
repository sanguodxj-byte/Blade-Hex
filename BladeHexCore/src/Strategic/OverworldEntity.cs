using Godot;
using System;
using BladeHex.Data;

namespace BladeHex.Strategic;

[GlobalClass]
public partial class OverworldEntity : Resource
{
    /// <summary>实体类型</summary>
    public enum EntityType
    {
        Adventurer,     // 冒险者队伍
        RaidingParty,   // 外族掠夺队
        Caravan,        // 商队
        EpicMonster,    // 史诗怪物（龙/魔像）
        LordArmy,       // 领主军队
        BanditParty,    // 山贼队伍
        RobberParty,    // 劫匪队伍
        PirateCrew,     // 海寇队伍
    }

    /// <summary>实体AI状态</summary>
    public enum AIState
    {
        Idle,           // 待机
        Patrolling,     // 巡逻
        MovingToTarget, // 向目标移动
        Fleeing,        // 逃跑
        Returning,      // 返回基地
        Attacking,      // 正在攻击
        Besieging,      // 围攻中
        Reinforcing,    // 前往回援
        Chasing,        // 追击敌人
        Recruiting,     // 招募中（领主在城镇）
        Escorting,      // 护送中（领主护送商队）
    }

    // ========================================
    // 基础字段
    // ========================================

    [Export] public string EntityName { get; set; } = "未知实体";
    [Export] public EntityType EntityTypeEnum = EntityType.Adventurer;
    [Export] public Vector2 Position { get; set; } = Vector2.Zero;
    [Export] public float MoveSpeed { get; set; } = 200.0f; // 大地图移动速度(px/s)

    // ========================================
    // 特殊角色标识
    // ========================================

    /// <summary>是否为具名特殊角色（领主/冒险者）</summary>
    [Export] public bool IsNamedCharacter { get; set; } = false;
    /// <summary>称号（5级以上获得）</summary>
    [Export] public string CharacterTitle { get; set; } = "";
    /// <summary>家族姓（领主专属）</summary>
    [Export] public string FamilyName { get; set; } = "";
    /// <summary>绑定的 POI 名称（领主专属）</summary>
    [Export] public string BoundPoiName { get; set; } = "";
    /// <summary>上次升级的游戏天数</summary>
    [Export] public int LastLevelUpDay { get; set; } = 0;

    // ========================================
    // 队伍配置
    // ========================================

    [Export] public int PartySize { get; set; } = 4;         // 队伍人数
    [Export] public int PartyLevel { get; set; } = 1;        // 队伍平均等级
    [Export] public float CombatPower { get; set; } = 10.0f; // 综合战力（用于AI评估）

    // ========================================
    // 关系
    // ========================================

    [Export] public string Faction { get; set; } = "neutral"; // 所属势力
    [Export] public bool IsHostileToPlayer { get; set; } = true;

    // ========================================
    // 行为参数
    // ========================================

    [Export] public float PatrolRadius { get; set; } = 300.0f; // 巡逻半径(像素)
    [Export] public float VisionRange { get; set; } = 400.0f;  // 视野范围(像素)
    [Export] public Vector2 HomePosition { get; set; } = Vector2.Zero; // 基地/出发位置
    [Export] public Vector2 TerritoryCenter { get; set; } = Vector2.Zero; // 领地中心
    [Export] public float TerritoryRadius { get; set; } = 500.0f; // 领地范围

    // ========================================
    // 各种族专用字段
    // ========================================

    // 冒险者专属
    [Export] public string AdventurerType { get; set; } = "veteran"; // novice/veteran/elite
    [Export] public int GoldCarried { get; set; } = 50;

    // 掠夺队专属
    [Export] public OverworldPOI? SourceSettlement; // 来源聚落
    [Export] public int LootCarried { get; set; } = 0; // 携带的战利品

    // 商队专属
    [Export] public OverworldPOI? OriginTown; // 出发城镇
    [Export] public OverworldPOI? DestinationTown; // 目标城镇
    [Export] public int TradeGoods { get; set; } = 100; // 货物价值
    public bool ProsperityContribution = false; // 商队是否已到达目的地

    // 史诗怪物专属
    [Export] public string MonsterType { get; set; } = "dragon"; // dragon/ancient_golem
    [Export] public bool IsAggressive { get; set; } = false; // 是否处于攻击状态

    // 领主军队专属
    [Export] public OverworldPOI.LordPersonality LordPersonalityValue = OverworldPOI.LordPersonality.Balanced;
    [Export] public int GarrisonSize { get; set; } = 30; // 麾下兵力
    [Export] public OverworldPOI? GuardedPOI; // 守卫的POI

    // ========================================
    // 围攻/回援/追击目标
    // ========================================

    public OverworldPOI? SiegeTarget;
    public OverworldPOI? ReinforceTarget;
    public OverworldEntity? ChaseTarget;

    // ========================================
    // 运行时状态
    // ========================================

    public AIState CurrentAIState = AIState.Idle;
    public Vector2 TargetPosition = Vector2.Zero;
    public OverworldEntity? CurrentTargetEntity;
    public Godot.Collections.Array<Vector2> Path = new();
    public bool IsMoving = false;
    public int DaysAlive = 0;
    public bool IsAlive = true;

    // ========================================
    // 辅助方法
    // ========================================

    public string GetTypeName() => EntityTypeEnum switch
    {
        EntityType.Adventurer => "冒险者",
        EntityType.RaidingParty => "掠夺队",
        EntityType.Caravan => "商队",
        EntityType.EpicMonster => GetMonsterDisplayName(),
        EntityType.LordArmy => "领主军队",
        _ => "未知"
    };

    public string GetMonsterDisplayName() => MonsterType switch
    {
        "dragon" => "巨龙",
        "ancient_golem" => "远古魔像",
        "undead_lord" => "亡灵领主",
        _ => "史诗怪物"
    };

    public Color GetDisplayColor() => EntityTypeEnum switch
    {
        EntityType.Adventurer => new Color(0.2f, 0.8f, 0.4f), // 绿色
        EntityType.RaidingParty => new Color(0.9f, 0.3f, 0.2f), // 红色
        EntityType.Caravan => new Color(0.8f, 0.7f, 0.2f), // 金色
        EntityType.EpicMonster => new Color(0.8f, 0.2f, 0.8f), // 紫色
        EntityType.LordArmy => new Color(0.3f, 0.5f, 0.9f), // 蓝色
        _ => Colors.White
    };

    public float EvaluatePowerRatio(OverworldEntity other)
    {
        if (other.CombatPower <= 0) return 10.0f;
        return CombatPower / other.CombatPower;
    }

    public bool IsInVision(Vector2 targetPos)
    {
        return Position.DistanceTo(targetPos) <= VisionRange;
    }

    public bool IsInTerritory(Vector2 targetPos)
    {
        if (TerritoryCenter == Vector2.Zero) return false;
        return TerritoryCenter.DistanceTo(targetPos) <= TerritoryRadius;
    }

    public Godot.Collections.Dictionary GetEncounterConfig()
    {
        var config = new Godot.Collections.Dictionary { { "enemies", new Godot.Collections.Array<string>() }, { "cr_total", 0.0f } };
        var enemies = (Godot.Collections.Array<string>)config["enemies"];

        switch (EntityTypeEnum)
        {
            case EntityType.Adventurer:
                enemies.Add("adventurer_warrior");
                enemies.Add("adventurer_mage");
                config["cr_total"] = PartyLevel * 1.5f;
                break;
            case EntityType.RaidingParty:
                if (SourceSettlement != null)
                {
                    switch (SourceSettlement.SettlementRaceValue)
                    {
                        case OverworldPOI.SettlementRace.Goblin:
                            enemies.Add("goblin_warrior");
                            enemies.Add("goblin_archer");
                            config["cr_total"] = 2.0f + ThreatLevel() * 1.5f;
                            break;
                        case OverworldPOI.SettlementRace.Kobold:
                            enemies.Add("kobold_trapper");
                            config["cr_total"] = 3.0f + ThreatLevel() * 1.5f;
                            break;
                        case OverworldPOI.SettlementRace.Minotaur:
                            enemies.Add("minotaur_warrior");
                            config["cr_total"] = 5.0f;
                            break;
                        case OverworldPOI.SettlementRace.ShadowCult:
                            enemies.Add("cultist");
                            config["cr_total"] = 4.0f;
                            break;
                    }
                }
                else
                {
                    enemies.Add("goblin_warrior");
                    config["cr_total"] = 2.0f;
                }
                break;
            case EntityType.EpicMonster:
                switch (MonsterType)
                {
                    case "dragon":
                        enemies.Add("dragon");
                        config["cr_total"] = 10.0f + PartyLevel * 2.0f;
                        break;
                    case "ancient_golem":
                        enemies.Add("iron_golem");
                        config["cr_total"] = 6.0f + PartyLevel * 1.5f;
                        break;
                    default:
                        enemies.Add("unknown_boss");
                        config["cr_total"] = 8.0f;
                        break;
                }
                break;
            case EntityType.Caravan:
                enemies.Add("caravan_guard");
                config["cr_total"] = 1.0f;
                break;
            case EntityType.LordArmy:
                enemies.Add("soldier");
                enemies.Add("archer");
                config["cr_total"] = PartyLevel * 3.0f;
                break;
        }

        return config;
    }

    private float ThreatLevel()
    {
        return SourceSettlement?.ThreatLevel ?? 0.5f;
    }

    public void OnDayPassed()
    {
        DaysAlive++;
        // 掠夺队存活太久自动返回
        if (EntityTypeEnum == EntityType.RaidingParty && DaysAlive > 14)
        {
            CurrentAIState = AIState.Returning;
            TargetPosition = HomePosition;
        }
    }

    public string GetStateText() => CurrentAIState switch
    {
        AIState.Idle => "驻扎中",
        AIState.Patrolling => "巡逻中",
        AIState.MovingToTarget => "移动中",
        AIState.Fleeing => "逃跑中",
        AIState.Returning => "返回基地",
        AIState.Attacking => "战斗中",
        AIState.Besieging => SiegeTarget != null ? $"围攻 {SiegeTarget.PoiName}" : "围攻中",
        AIState.Reinforcing => ReinforceTarget != null ? $"前往支援 {ReinforceTarget.PoiName}" : "回援中",
        AIState.Chasing => ChaseTarget != null && GodotObject.IsInstanceValid(ChaseTarget) && ChaseTarget.IsAlive ? $"追击 {ChaseTarget.EntityName}" : "追击中",
        AIState.Recruiting => "招募中",
        AIState.Escorting => "护送中",
        _ => "未知状态"
    };

    // ========================================
    // 战斗桥梁方法 — 战略层 → 战斗层（委托到 EntityCombatBridge）
    // ========================================

    /// <summary>
    /// 生成战斗部署数据 — 根据实体类型和队伍配置生成战斗单位
    /// </summary>
    public BattleUnitDeployment[] GetDeployment(bool isAttacker)
        => EntityCombatBridge.GetDeployment(this, isAttacker);

    /// <summary>
    /// 应用战斗结果 — 战斗结束后更新实体状态
    /// </summary>
    public void ApplyBattleOutcome(BattleOutcome outcome)
        => EntityCombatBridge.ApplyBattleOutcome(this, outcome);

    /// <summary>
    /// 计算遭遇战 CR 总值 — 用于战斗难度评估
    /// </summary>
    public float GetEncounterCR()
        => EntityCombatBridge.GetEncounterCR(this);

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary
        {
            { "entity_name", EntityName },
            { "entity_type", (int)EntityTypeEnum },
            { "position_x", Position.X },
            { "position_y", Position.Y },
            { "move_speed", MoveSpeed },
            { "party_size", PartySize },
            { "party_level", PartyLevel },
            { "combat_power", CombatPower },
            { "faction", Faction },
            { "is_hostile_to_player", IsHostileToPlayer },
            { "patrol_radius", PatrolRadius },
            { "vision_range", VisionRange },
            { "home_pos_x", HomePosition.X },
            { "home_pos_y", HomePosition.Y },
            { "territory_center_x", TerritoryCenter.X },
            { "territory_center_y", TerritoryCenter.Y },
            { "territory_radius", TerritoryRadius },
            { "adventurer_type", AdventurerType },
            { "gold_carried", GoldCarried },
            { "loot_carried", LootCarried },
            { "trade_goods", TradeGoods },
            { "prosperity_contribution", ProsperityContribution },
            { "monster_type", MonsterType },
            { "is_aggressive", IsAggressive },
            { "lord_personality", (int)LordPersonalityValue },
            { "garrison_size", GarrisonSize },
            { "ai_state", (int)CurrentAIState },
            { "target_pos_x", TargetPosition.X },
            { "target_pos_y", TargetPosition.Y },
            { "days_alive", DaysAlive },
            { "is_alive", IsAlive },
        };
        return data;
    }
}
