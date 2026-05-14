// RaceData.cs
// 种族数据资源 — 5个可玩种族的属性修正、种族特性、初始好感
// 对应策划案 12-种族与招募.md + 05-角色与职业.md
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class RaceData : Resource
{
    // ========================================
    // 种族枚举
    // ========================================

    public enum Race
    {
        Human,
        Elf,
        Dwarf,
        HalfOrc,
        HalfElf,
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public Race raceId = Race.Human;
    [Export] public string RaceName { get; set; } = "人类";

    // 属性修正
    [Export] public int StrMod;
    [Export] public int DexMod;
    [Export] public int ConMod;
    [Export] public int IntMod;
    [Export] public int WisMod;
    [Export] public int ChaMod;

    // 种族特性列表
    [Export] public string[] RacialTraits = [];

    // 种族特性描述
    [Export] public string TraitsDescription { get; set; } = "";

    // 招募难度系数（1.0=标准，越高越难）
    [Export] public float RecruitmentDifficulty { get; set; } = 1.0f;

    // 初始好感度表（种族ID → 好感值）
    [Export] public Godot.Collections.Dictionary StartingFavor = new();

    // 适合的职业倾向
    [Export] public string[] SuitableTendencies = [];

    // ========================================
    // 静态工厂：返回5个硬编码种族
    // ========================================

    public static RaceData[] GetAllRaces() =>
    [
        CreateHuman(),
        CreateElf(),
        CreateDwarf(),
        CreateHalfOrc(),
        CreateHalfElf(),
    ];

    public static RaceData GetRaceById(Race id)
    {
        foreach (var r in GetAllRaces())
            if (r.raceId == id) return r;
        return GetAllRaces()[0];
    }

    public static string GetRaceName(Race id) => id switch
    {
        Race.Human => "人类",
        Race.Elf => "精灵",
        Race.Dwarf => "矮人",
        Race.HalfOrc => "半兽人",
        Race.HalfElf => "半精灵",
        _ => "未知",
    };

    // ========================================
    // 各种族定义
    // ========================================

    private static RaceData CreateHuman() => new()
    {
        raceId = Race.Human,
        RaceName = "人类",
        StrMod = 1, DexMod = 1, ConMod = 1, IntMod = 1, WisMod = 1, ChaMod = 1,
        RacialTraits = ["versatile"],
        TraitsDescription = "多才多艺：额外获得1个技能点。全属性+1。",
        RecruitmentDifficulty = 0.5f,
        StartingFavor = new Godot.Collections.Dictionary
        {
            { "human", 20 }, { "elf", 5 }, { "dwarf", 10 }, { "half_orc", -5 }, { "half_elf", 5 },
        },
        SuitableTendencies = ["全能"],
    };

    private static RaceData CreateElf() => new()
    {
        raceId = Race.Elf,
        RaceName = "精灵",
        DexMod = 2, IntMod = 1, ConMod = -1,
        RacialTraits = ["dark_vision", "elf_weapon_proficiency"],
        TraitsDescription = "黑暗视觉：夜间/洞穴无惩罚。精灵武器熟练：长剑/长弓+1命中。DEX+2, INT+1, CON-1。",
        RecruitmentDifficulty = 1.0f,
        StartingFavor = new Godot.Collections.Dictionary
        {
            { "human", 5 }, { "elf", 25 }, { "dwarf", 0 }, { "half_orc", -15 }, { "half_elf", 15 },
        },
        SuitableTendencies = ["法师", "游侠", "游荡者"],
    };

    private static RaceData CreateDwarf() => new()
    {
        raceId = Race.Dwarf,
        RaceName = "矮人",
        ConMod = 2, StrMod = 1, DexMod = -1,
        RacialTraits = ["poison_resistance", "dwarven_resilience"],
        TraitsDescription = "毒素抗性：强韧豁免优势。矮人韧性：HP+1/级。CON+2, STR+1, DEX-1。",
        RecruitmentDifficulty = 1.0f,
        StartingFavor = new Godot.Collections.Dictionary
        {
            { "human", 10 }, { "elf", 0 }, { "dwarf", 25 }, { "half_orc", -20 }, { "half_elf", 5 },
        },
        SuitableTendencies = ["战士", "守护骑士", "贤者"],
    };

    private static RaceData CreateHalfOrc() => new()
    {
        raceId = Race.HalfOrc,
        RaceName = "半兽人",
        StrMod = 2, ConMod = 1, IntMod = -2, ChaMod = -1,
        RacialTraits = ["rage", "threat_instinct"],
        TraitsDescription = "狂暴：HP低于50%时伤害+2。威胁直觉：先攻+2。STR+2, CON+1, INT-2, CHA-1。",
        RecruitmentDifficulty = 2.0f,
        StartingFavor = new Godot.Collections.Dictionary
        {
            { "human", -10 }, { "elf", -20 }, { "dwarf", -15 }, { "half_orc", 20 }, { "half_elf", -5 },
        },
        SuitableTendencies = ["战士", "野蛮人", "游侠"],
    };

    private static RaceData CreateHalfElf() => new()
    {
        raceId = Race.HalfElf,
        RaceName = "半精灵",
        ChaMod = 2,
        RacialTraits = ["dual_heritage", "social_talent"],
        TraitsDescription = "双重血统：人类和精灵聚居地都视为友好。社交天赋：交涉/招募价格-10%。CHA+2，自选2项+1。",
        RecruitmentDifficulty = 0.8f,
        StartingFavor = new Godot.Collections.Dictionary
        {
            { "human", 10 }, { "elf", 15 }, { "dwarf", 5 }, { "half_orc", -5 }, { "half_elf", 25 },
        },
        SuitableTendencies = ["守护骑士", "贤者", "游荡者", "法师"],
    };
}
