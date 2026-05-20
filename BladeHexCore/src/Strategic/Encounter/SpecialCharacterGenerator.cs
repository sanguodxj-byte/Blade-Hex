// SpecialCharacterGenerator.cs
// 特殊角色生成器 — 世界初始化时生成领主和冒险者
// 生成后立即收容到 DormantEntityPool，运行时按需激活
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 特殊角色生成器 — 在世界创建阶段生成具名领主和冒险者
/// </summary>
public class SpecialCharacterGenerator
{
    private readonly Random _rng;

    // 领主家族姓池（按种族）
    private static readonly Dictionary<string, string[]> LordSurnamesZH = new()
    {
        ["human"] = ["布莱克伍德", "阿什莫尔", "瑞德克里夫", "瓦勒隆", "蒙特福德", "格雷斯顿", "温特沃斯", "哈特菲尔德"],
        ["elf"] = ["月语", "星花", "银露", "叶生", "晨曦", "暮光", "风歌", "霜叶"],
        ["dwarf"] = ["铁足", "石拳", "深锻", "铜须", "金锤", "黑铁", "岩心", "熔炉"],
        ["orc"] = ["碎颅", "血牙", "裂骨", "焚天", "噬魂", "铁脊", "雷蹄", "暴风"],
    };

    private static readonly Dictionary<string, string[]> LordGivenNamesZH = new()
    {
        ["human"] = ["阿拉里克", "塞德里克", "罗兰", "瓦勒留", "爱德华", "雷蒙德", "加文", "奥斯瓦尔德"],
        ["elf"] = ["萨拉萨斯", "赛瓦隆", "瓦琳卓", "埃伦娜莉", "法伦", "艾洛温", "塞拉诺", "伊瑟拉"],
        ["dwarf"] = ["巴尔古夫", "索恩", "都灵", "索林", "格罗格", "布洛克", "达因", "弗林"],
        ["orc"] = ["格罗姆", "阿佐格", "加尔鲁什", "莫克", "克罗格", "乌加什", "扎格拉", "萨尔"],
    };

    private static readonly string[] AdventurerNamesZH =
    [
        "芬恩", "贾克斯", "托比", "塞拉斯", "米拉", "凯恩", "莉娅", "达克斯",
        "艾拉", "诺兰", "薇拉", "加尔", "伊莎", "雷克斯", "塔莉亚", "奥林",
        "瑟琳", "布兰", "尼娅", "沃恩", "卡拉", "泰隆", "露娜", "马库斯",
    ];

    public SpecialCharacterGenerator(int seed)
    {
        _rng = new Random(seed ^ 0x4C4F5244); // "LORD"
    }

    // ========================================
    // 主入口
    // ========================================

    /// <summary>
    /// 生成所有特殊角色（领主 + 冒险者），返回实体列表
    /// </summary>
    public List<OverworldEntity> GenerateAll(
        List<NationConfig> nations,
        Dictionary<string, NationTerritory> territories,
        List<OverworldPOI> pois,
        int worldTileCount)
    {
        var result = new List<OverworldEntity>();

        // 生成领主
        foreach (var nation in nations)
        {
            if (!territories.TryGetValue(nation.Id, out var territory)) continue;

            int lordCount = nation.IsMajorNation ? 3 + _rng.Next(3) : 1 + _rng.Next(2); // 主要国家 3~5，小势力 1~2
            var nationPois = GetNationPois(pois, nation.Id);

            for (int i = 0; i < lordCount; i++)
            {
                var lord = GenerateLord(nation, territory, nationPois, i);
                if (lord != null) result.Add(lord);
            }
        }

        // 生成冒险者
        int adventurerCount = Math.Max(12, worldTileCount / 8000 + 8);
        for (int i = 0; i < adventurerCount; i++)
        {
            var adventurer = GenerateAdventurer(i, pois);
            result.Add(adventurer);
        }

        GD.Print($"[SpecialCharacterGenerator] 生成完成: {result.Count} 个特殊角色 ({result.Count - adventurerCount} 领主 + {adventurerCount} 冒险者)");
        return result;
    }

    // ========================================
    // 领主生成
    // ========================================

    private OverworldEntity? GenerateLord(
        NationConfig nation,
        NationTerritory territory,
        List<OverworldPOI> nationPois,
        int index)
    {
        // 绑定 POI（优先城堡，其次城镇）
        OverworldPOI? boundPoi = null;
        foreach (var poi in nationPois)
        {
            if (poi.PoiTypeEnum == OverworldPOI.POIType.Castle && !IsPoiBound(poi))
            { boundPoi = poi; break; }
        }
        if (boundPoi == null)
        {
            foreach (var poi in nationPois)
            {
                if (poi.PoiTypeEnum == OverworldPOI.POIType.Town && !IsPoiBound(poi))
                { boundPoi = poi; break; }
            }
        }
        if (boundPoi == null && nationPois.Count > 0)
            boundPoi = nationPois[index % nationPois.Count];

        if (boundPoi == null) return null;

        // 等级：主要国家 40~80，小势力 30~60
        int level = nation.IsMajorNation
            ? 40 + _rng.Next(41)
            : 30 + _rng.Next(31);

        // 种族映射
        var raceId = MapNationRaceToPlayerRace(nation.Race);

        // 生成名字
        string givenName = PickLordGivenName(nation.Race);
        string surname = PickLordSurname(nation.Race);
        string fullName = $"{givenName}·{surname}";

        // 属性
        var attrs = AllocateLordAttrs(level, raceId);

        // 性格
        var personality = (OverworldPOI.LordPersonality)_rng.Next(3);

        // 兵力
        int garrisonSize = 30 + _rng.Next(71); // 30~100

        // 战力
        float combatPower = CalculateCombatPower(attrs, level, garrisonSize);

        var entity = new OverworldEntity
        {
            EntityName = fullName,
            EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
            Position = boundPoi.Position,
            HomePosition = boundPoi.Position,
            TerritoryCenter = boundPoi.Position,
            TerritoryRadius = 1200f,
            MoveSpeed = 100f,
            PartySize = garrisonSize,
            PartyLevel = level,
            CombatPower = combatPower,
            Faction = nation.Id,
            IsHostileToPlayer = false, // 领主初始不敌对（除非玩家攻击）
            VisionRange = 800f,
            PatrolRadius = 600f,
            CurrentAIState = OverworldEntity.AIState.Patrolling,
            IsAlive = true,
            LordPersonalityValue = personality,
            GarrisonSize = garrisonSize,
            GuardedPOI = boundPoi,

            // 特殊角色标识
            IsNamedCharacter = true,
            CharacterTitle = "",
            FamilyName = surname,
            BoundPoiName = boundPoi.PoiName,
        };

        // 5 级倍数时生成称号
        if (level >= 5)
        {
            entity.CharacterTitle = NameGenerator.GenerateFullName(raceId, level).Split(' ')[0];
            // 如果称号和名字一样就清空
            if (entity.CharacterTitle == givenName) entity.CharacterTitle = "";
        }

        _boundPois.Add(boundPoi.PoiName);
        return entity;
    }

    // 已绑定的 POI 名称集合（避免重复绑定）
    private readonly HashSet<string> _boundPois = new();
    private bool IsPoiBound(OverworldPOI poi) => _boundPois.Contains(poi.PoiName);

    // ========================================
    // 冒险者生成
    // ========================================

    private OverworldEntity GenerateAdventurer(int index, List<OverworldPOI> pois)
    {
        // 类型分布
        var advType = PickAdventurerType();

        // 等级按类型
        int level = advType switch
        {
            "novice" => 5 + _rng.Next(20),     // 5~24
            "veteran" => 25 + _rng.Next(26),   // 25~50
            "elite" => 50 + _rng.Next(31),     // 50~80
            "outlaw" => 20 + _rng.Next(41),    // 20~60
            _ => 15,
        };

        // 随机种族
        var allRaces = RaceData.GetAllRaces();
        var race = allRaces[_rng.Next(allRaces.Length)];

        // 名字
        string name = AdventurerNamesZH[index % AdventurerNamesZH.Length];
        if (level >= 5)
        {
            string title = NameGenerator.GenerateFullName(race.raceId, level).Split(' ')[0];
            name = $"{title} {name}";
        }

        // 属性
        var attrs = AllocateAdventurerAttrs(level, race.raceId, advType);
        float combatPower = CalculateCombatPower(attrs, level, 0);

        // 随机起始位置（靠近某个城镇）
        Vector2 startPos = Vector2.Zero;
        if (pois.Count > 0)
        {
            var townPois = new List<OverworldPOI>();
            foreach (var p in pois)
                if (p.PoiTypeEnum == OverworldPOI.POIType.Town || p.PoiTypeEnum == OverworldPOI.POIType.Village)
                    townPois.Add(p);
            if (townPois.Count > 0)
                startPos = townPois[_rng.Next(townPois.Count)].Position;
        }

        var entity = new OverworldEntity
        {
            EntityName = name,
            EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
            Position = startPos,
            HomePosition = startPos,
            TerritoryCenter = startPos,
            TerritoryRadius = 2000f,
            MoveSpeed = 120f,
            PartySize = 2 + _rng.Next(4), // 2~5 人小队
            PartyLevel = level,
            CombatPower = combatPower,
            Faction = "neutral",
            IsHostileToPlayer = false,
            RaceId = (int)race.raceId,
            VisionRange = 500f,
            PatrolRadius = 800f,
            CurrentAIState = OverworldEntity.AIState.Patrolling,
            IsAlive = true,
            AdventurerType = advType,
            GoldCarried = 50 + _rng.Next(200),

            // 特殊角色标识
            IsNamedCharacter = true,
            CharacterTitle = "",
            FamilyName = "",
        };

        return entity;
    }

    private string PickAdventurerType()
    {
        int roll = _rng.Next(100);
        if (roll < 40) return "novice";    // 40%
        if (roll < 70) return "veteran";   // 30%
        if (roll < 85) return "elite";     // 15%
        return "outlaw";                    // 15%
    }

    // ========================================
    // 属性分配
    // ========================================

    /// <summary>领主属性分配 — 高基础 + 等级加成</summary>
    private Dictionary<string, int> AllocateLordAttrs(int level, RaceData.Race race)
    {
        // 基础模板（领主起点高于普通角色）
        var baseAttrs = race switch
        {
            RaceData.Race.Elf => new Dictionary<string, int>
                { ["str"] = 12, ["dex"] = 18, ["con"] = 12, ["intel"] = 18, ["wis"] = 16, ["cha"] = 14 },
            RaceData.Race.Dwarf => new Dictionary<string, int>
                { ["str"] = 18, ["dex"] = 10, ["con"] = 20, ["intel"] = 12, ["wis"] = 14, ["cha"] = 10 },
            RaceData.Race.HalfOrc => new Dictionary<string, int>
                { ["str"] = 22, ["dex"] = 12, ["con"] = 18, ["intel"] = 8, ["wis"] = 10, ["cha"] = 12 },
            _ => new Dictionary<string, int> // Human / HalfElf
                { ["str"] = 14, ["dex"] = 12, ["con"] = 14, ["intel"] = 14, ["wis"] = 14, ["cha"] = 16 },
        };

        // 等级加成：每级 +1 点，60% 分配到优势属性
        var weights = GetRaceWeights(race);
        for (int i = 0; i < level; i++)
        {
            string attr = _rng.NextDouble() < 0.6
                ? weights[_rng.Next(weights.Length)]
                : AllAttrs[_rng.Next(AllAttrs.Length)];
            baseAttrs[attr] = Math.Min(40, baseAttrs[attr] + 1);
        }

        return baseAttrs;
    }

    /// <summary>冒险者属性分配 — 标准基础 + 等级加成</summary>
    private Dictionary<string, int> AllocateAdventurerAttrs(int level, RaceData.Race race, string advType)
    {
        var baseAttrs = new Dictionary<string, int>
            { ["str"] = 10, ["dex"] = 10, ["con"] = 10, ["intel"] = 10, ["wis"] = 10, ["cha"] = 10 };

        // 类型偏向
        string[] typeWeights = advType switch
        {
            "novice" => AllAttrs, // 均匀
            "veteran" => ["str", "dex", "con", "str", "dex"],
            "elite" => ["str", "str", "dex", "con", "intel"],
            "outlaw" => ["dex", "dex", "cha", "dex", "cha"],
            _ => AllAttrs,
        };

        // 等级加成
        for (int i = 0; i < level + 15; i++) // +15 = 初始 25 点分配
        {
            string attr = _rng.NextDouble() < 0.5
                ? typeWeights[_rng.Next(typeWeights.Length)]
                : AllAttrs[_rng.Next(AllAttrs.Length)];
            baseAttrs[attr] = Math.Min(40, baseAttrs[attr] + 1);
        }

        return baseAttrs;
    }

    private static readonly string[] AllAttrs = ["str", "dex", "con", "intel", "wis", "cha"];

    private static string[] GetRaceWeights(RaceData.Race race) => race switch
    {
        RaceData.Race.Elf => ["dex", "intel", "wis"],
        RaceData.Race.Dwarf => ["str", "con", "wis"],
        RaceData.Race.HalfOrc => ["str", "con", "str"],
        _ => ["str", "cha", "con"],
    };

    // ========================================
    // 战力计算
    // ========================================

    private static float CalculateCombatPower(Dictionary<string, int> attrs, int level, int garrisonSize)
    {
        float baseStatPower = (attrs["str"] + attrs["dex"] + attrs["con"]) * 0.8f;
        float levelPower = level * level * 0.1f + level * 3.0f;
        float armyPower = garrisonSize * 2.0f;
        return baseStatPower + levelPower + armyPower;
    }

    // ========================================
    // 辅助
    // ========================================

    private string PickLordGivenName(string nationRace)
    {
        string key = nationRace == "orc" ? "orc" : nationRace;
        if (!LordGivenNamesZH.ContainsKey(key)) key = "human";
        var pool = LordGivenNamesZH[key];
        return pool[_rng.Next(pool.Length)];
    }

    private string PickLordSurname(string nationRace)
    {
        string key = nationRace == "orc" ? "orc" : nationRace;
        if (!LordSurnamesZH.ContainsKey(key)) key = "human";
        var pool = LordSurnamesZH[key];
        return pool[_rng.Next(pool.Length)];
    }

    private static RaceData.Race MapNationRaceToPlayerRace(string nationRace) => nationRace switch
    {
        "human" => RaceData.Race.Human,
        "elf" => RaceData.Race.Elf,
        "dwarf" => RaceData.Race.Dwarf,
        "orc" => RaceData.Race.HalfOrc,
        _ => RaceData.Race.Human,
    };

    private static List<OverworldPOI> GetNationPois(List<OverworldPOI> allPois, string nationId)
    {
        var result = new List<OverworldPOI>();
        foreach (var poi in allPois)
            if (poi.OwningFaction == nationId) result.Add(poi);
        return result;
    }
}
