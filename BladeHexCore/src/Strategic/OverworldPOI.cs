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
        Mine,        // 矿场（资源产出）
        Farm,        // 农庄（食物产出）
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
    [Export] public bool IsPortCity { get; set; } = false;
    [Export] public string ParentPoiName { get; set; } = "";

    // 运行时内存绑定引用
    public OverworldPOI? ParentPoi { get; set; }
    public System.Collections.Generic.List<OverworldPOI> SubPois { get; } = new();

    public static void BindParentChildRelationships(System.Collections.Generic.List<OverworldPOI> allPois)
    {
        foreach (var p in allPois)
        {
            p.SubPois.Clear();
            p.ParentPoi = null;
        }

        var poiMap = new System.Collections.Generic.Dictionary<string, OverworldPOI>();
        foreach (var p in allPois)
        {
            if (!string.IsNullOrEmpty(p.PoiName))
            {
                poiMap[p.PoiName] = p;
            }
        }

        foreach (var p in allPois)
        {
            if (!string.IsNullOrEmpty(p.ParentPoiName) && poiMap.TryGetValue(p.ParentPoiName, out var parent))
            {
                p.ParentPoi = parent;
                parent.SubPois.Add(p);
            }
        }
    }

    /// <summary>兼容：poi_type 作为 int 访问</summary>
    [Export] public int PoiType
    {
        get => (int)PoiTypeEnum;
        set => PoiTypeEnum = (POIType)value;
    }

    [Export] public Vector2 Position { get; set; } = Vector2.Zero;
    private string _owningFaction = "neutral";
    [Export]
    public string OwningFaction
    {
        get => _owningFaction;
        set
        {
            _owningFaction = value;
            if (PoiTypeEnum == POIType.Town || PoiTypeEnum == POIType.Castle)
            {
                if (SubPois != null && SubPois.Count > 0)
                {
                    foreach (var sub in SubPois)
                    {
                        if (sub.OwningFaction != value)
                        {
                            sub.OwningFaction = value;
                            sub.EndSiege();
                            sub.GarrisonCurrent = sub.GarrisonMax / 4;
                        }
                    }
                }
            }
        }
    }
    [Export] public int Prosperity { get; set; } = 50; // 繁荣度 0-100

    // ========================================
    // Scale & Footprint (比例尺统一)
    // ========================================

    /// <summary>POI 占用形状的中心 hex axial 坐标（Position 是 pixel 坐标，CenterHex 是 hex 坐标）</summary>
    public Vector2I CenterHex { get; set; } = Vector2I.Zero;

    /// <summary>Footprint 模板名（如 "solo" / "village_3" / "port_city_4"）</summary>
    [Export] public string FootprintTemplateName { get; set; } = "solo";

    /// <summary>Footprint 在大地图上的旋转方向（0~5），由世界生成 TryFit 决定</summary>
    [Export] public int FootprintRotation { get; set; } = 0;

    /// <summary>Footprint 实际占用的 hex（中心 + 旋转后的 cells）。运行时计算缓存。</summary>
    public Vector2I[] OccupiedHexes { get; set; } = System.Array.Empty<Vector2I>();

    /// <summary>POI 尺度档（动态计算）</summary>
    public POIScale Scale
    {
        get
        {
            if (PoiTypeEnum == POIType.Town)
            {
                int undamagedCount = 0;
                foreach (var sub in SubPois)
                {
                    if (!sub.IsCleared && sub.Prosperity > 0)
                        undamagedCount++;
                }
                if (undamagedCount >= 5) return POIScale.Large;
                if (undamagedCount <= 1) return POIScale.Small;
                return POIScale.Medium;
            }
            else if (PoiTypeEnum == POIType.Castle)
            {
                int undamagedCount = 0;
                foreach (var sub in SubPois)
                {
                    if (!sub.IsCleared && sub.Prosperity > 0)
                        undamagedCount++;
                }
                if (undamagedCount == 0) return POIScale.Medium;
                return POIScale.Large;
            }
            return POIBattlePresetRegistry.ScaleOf(this);
        }
    }

    /// <summary>检查指定 hex 是否在本 POI 的 footprint 内</summary>
    public bool ContainsHex(Vector2I hex)
    {
        for (int i = 0; i < OccupiedHexes.Length; i++)
            if (OccupiedHexes[i] == hex) return true;
        return false;
    }

    /// <summary>从 footprint 模板 + 中心 + 旋转重新计算 OccupiedHexes 缓存</summary>
    public void RebuildOccupiedHexes()
    {
        var tpl = FootprintTemplateRegistry.Get(FootprintTemplateName);
        var result = new Vector2I[tpl.Cells.Count];
        for (int i = 0; i < tpl.Cells.Count; i++)
        {
            var rotated = FootprintTemplate.RotateOffset(tpl.Cells[i].Offset, FootprintRotation);
            result[i] = new Vector2I(CenterHex.X + rotated.X, CenterHex.Y + rotated.Y);
        }
        OccupiedHexes = result;
    }

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
    [Export] public int GarrisonMax { get; set; } = 0;
    [Export] public int GarrisonCurrent { get; set; } = 0;

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
        POIType.Town => IsPortCity ? "港口城市" : "城镇",
        POIType.Village => "村庄",
        POIType.Castle => "城堡",
        POIType.Settlement => GetSettlementRaceName(),
        POIType.Lair => GetLairTypeName(),
        POIType.Mine => "矿场",
        POIType.Farm => "农庄",
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

    public string GetBattleTemplateName() => POIBattlePresetRegistry.Resolve(this).TemplateName;

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
    // 战斗桥梁方法 — POI 防御与围攻（委托到 POICombatBridge）
    // ========================================

    /// <summary>
    /// 生成防御部署 — 根据 POI 类型和驻军配置生成防御战斗单位
    /// </summary>
    public BattleUnitDeployment[] GenerateDefenseDeployment()
        => POICombatBridge.GenerateDefenseDeployment(this);

    /// <summary>
    /// 应用围攻结果 — 更新 POI 状态
    /// </summary>
    public void ApplySiegeOutcome(BattleOutcome outcome)
        => POICombatBridge.ApplySiegeOutcome(this, outcome);

    /// <summary>
    /// 获取 POI 的防御力量（用于 AI 评估）
    /// </summary>
    public float GetDefensePower()
        => POICombatBridge.GetDefensePower(this);

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
        { "center_hex_q", CenterHex.X },
        { "center_hex_r", CenterHex.Y },
        { "footprint_template", FootprintTemplateName },
        { "footprint_rotation", FootprintRotation },
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
        { "is_port_city", IsPortCity },
        { "parent_poi_name", ParentPoiName },
    };
    return data;
}

public void OnDayPassed()
{
    if (PoiTypeEnum == POIType.Settlement)
        DaysSinceLastRaid++;

    // 繁荣度自然恢复
    if (Prosperity < 50 && !IsUnderSiege)
    {
        if (PoiTypeEnum == POIType.Town || PoiTypeEnum == POIType.Castle)
        {
            int damagedSubs = 0;
            foreach (var sub in SubPois)
            {
                if (sub.IsCleared || sub.Prosperity <= 0)
                    damagedSubs++;
            }
            int maxAllowedProsperity = Math.Max(10, 50 - damagedSubs * 10);
            if (Prosperity < maxAllowedProsperity)
                Prosperity = Math.Min(maxAllowedProsperity, Prosperity + 1);
            else if (Prosperity > maxAllowedProsperity)
                Prosperity = Math.Max(maxAllowedProsperity, Prosperity - 1);
        }
        else
        {
            Prosperity = Math.Min(50, Prosperity + 1);
        }
    }

    // 围攻天数递增
    if (IsUnderSiege)
    {
        SiegeDays++;
        Prosperity = Math.Max(0, Prosperity - 2);
    }

    // 守军自然恢复（非围攻状态下）
    if (!IsUnderSiege && GarrisonCurrent < GarrisonMax)
    {
        int recovery = PoiTypeEnum switch
        {
            POIType.Castle => 2,   // 城堡：每天恢复 2
            POIType.Town => 1,     // 城镇：每天恢复 1
            _ => 0,                // 其他类型不自动恢复
        };
        if (recovery > 0)
            GarrisonCurrent = Math.Min(GarrisonMax, GarrisonCurrent + recovery);
    }
}
}
