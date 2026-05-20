// BuildProfiles.cs
// Sim 用 build 配置表 — 每个 build 同时控制属性权重 + 技能盘加点目标区域，
// 让生成的角色既符合 ClassTitleResolver 的称号判定（例如"剑舞者"），又有
// 对应的技能盘大节点覆盖。
using System.Collections.Generic;

namespace BladeHex.Tests.Simulation;

/// <summary>单个 build 的配置（中文名 + 属性权重 + 技能盘目标区域）</summary>
public sealed class BuildProfile
{
    public string ChineseName = "";
    public string EnglishKey = "";
    /// <summary>属性权重（用于 CharacterGenerator 生成倾向）</summary>
    public Dictionary<string, float> AttrWeights = new();
    /// <summary>技能盘目标区域（按重要性降序），将被 AiAllocatePointsMultiRegion 使用</summary>
    public string[] TargetRegions = System.Array.Empty<string>();
    /// <summary>分类（用于报告分组）</summary>
    public int AttrCount;
}

/// <summary>
/// Build 注册表 — 与 ClassTitleResolver 的 63 个职业一一对应。
/// 包含 6 单 + 15 双 + 20 三 + 15 四 + 6 五 + 1 全 = 63 builds。
/// </summary>
public static class BuildProfiles
{
    private static List<BuildProfile>? _all;

    public static IReadOnlyList<BuildProfile> All
    {
        get
        {
            if (_all == null) Build();
            return _all!;
        }
    }

    public static List<BuildProfile> ByAttrCount(int count)
    {
        var list = new List<BuildProfile>();
        foreach (var b in All) if (b.AttrCount == count) list.Add(b);
        return list;
    }

    private static void Build()
    {
        _all = new List<BuildProfile>();

        // ---- 6 种单属性 ----
        Add("战士", "Warrior", "str");
        Add("游侠", "Ranger", "dex");
        Add("守卫", "Guardian", "con");
        Add("法师", "Mage", "int");
        Add("刺客",     "Assassin",    "wis");
        Add("诗人", "Bard", "cha");

        // ---- 15 种双属性 ----
        Add("剑舞者",   "BladeDancer", "str", "dex");
        Add("重战士",   "Juggernaut",  "str", "con");
        Add("魔剑士",   "Spellsword",  "str", "int");
        Add("守护骑士", "PaladinKnight", "str", "wis");
        Add("军阀",     "Warlord",     "str", "cha");
        Add("决斗家",   "Duelist",     "dex", "con");
        Add("秘射手",   "ArcaneArcher","dex", "int");
        Add("猎人",     "Hunter",      "dex", "wis");
        Add("浪客",     "Rogue",       "dex", "cha");
        Add("战法师",   "Battlemage",  "con", "int");
        Add("苦修者",   "Veteran",     "con", "wis");
        Add("铁壁将军", "IronCommander","con", "cha");
        Add("贤者",     "Sage",        "int", "wis"); // INT+WIS 双属性 = 贤者（学者+刺客的混合定位）
        Add("术士",     "Sorcerer",    "int", "cha");
        Add("神使",     "Prophet",     "wis", "cha");

        // ---- 20 种三属性 ----
        Add("武圣",     "Bruiser",     "str", "dex", "con");
        Add("魔武者",   "SpellWeaver", "str", "dex", "int");
        Add("审判官",   "Hawkeye",     "str", "dex", "wis");
        Add("战神",     "Champion",    "str", "dex", "cha");
        Add("铁焰魔战", "IronWeaver",  "str", "con", "int");
        Add("磐石骑士", "RockKnight",  "str", "con", "wis");
        Add("征服者",   "Conqueror",   "str", "con", "cha");
        Add("天启骑士", "DoomKnight",  "str", "int", "wis");
        Add("魔王",     "DemonLord",   "str", "int", "cha");
        Add("战术大师", "WarMaster",   "str", "wis", "cha");
        Add("影法师",   "ShadowMage",  "dex", "con", "int");
        Add("荒野守望", "WildWarden",  "dex", "con", "wis");
        Add("千面客",   "Trickster",   "dex", "con", "cha");
        Add("星辰行者", "AstralWalker","dex", "int", "wis");
        Add("幻术师",   "Illusionist", "dex", "int", "cha");
        Add("风语者",   "WindSpeaker", "dex", "wis", "cha");
        Add("远古守护", "AncientGuardian","con", "int", "wis");
        Add("铁幕领主", "IronCurtain", "con", "int", "cha");
        Add("铁壁守护", "IronBulwark", "con", "wis", "cha");
        Add("天选者",   "Chosen",      "int", "wis", "cha");

        // ---- 15 种四属性 ----
        Add("智者尊者", "ArchSage",    "con", "int", "wis", "cha");
        Add("灵风大师", "SpiritWind",  "dex", "int", "wis", "cha");
        Add("自然统帅", "NatureLord",  "dex", "con", "wis", "cha");
        Add("暗影领主", "ShadowLord",  "dex", "con", "int", "cha");
        Add("沉默之力", "SilentForce", "dex", "con", "int", "wis");
        Add("毁灭之主", "Destroyer",   "str", "int", "wis", "cha");
        Add("磐石守护", "RockGuard",   "str", "con", "wis", "cha");
        Add("霸道魔将", "TyrantMage",  "str", "con", "int", "cha");
        Add("深渊骑士", "AbyssKnight", "str", "con", "int", "wis");
        Add("战争之风", "WarWind",     "str", "dex", "wis", "cha");
        Add("狂风魔将", "StormMage",   "str", "dex", "int", "cha");
        Add("独行圣者", "LoneSaint",   "str", "dex", "int", "wis");
        Add("战争之王", "WarKing",     "str", "dex", "con", "cha");
        Add("铁壁猎手", "IronHunter",  "str", "dex", "con", "wis");
        Add("万象魔战", "AllMage",     "str", "dex", "con", "int");

        // ---- 6 种五属性 ----
        Add("万灵使者", "AllSpirits",  "dex", "con", "int", "wis", "cha");
        Add("山岳之主", "MountainLord","str", "con", "int", "wis", "cha");
        Add("星界旅者", "StarTraveler","str", "dex", "int", "wis", "cha");
        Add("自然战神", "NatureGod",   "str", "dex", "con", "wis", "cha");
        Add("铁血魔王", "IronTyrant",  "str", "dex", "con", "int", "cha");
        Add("深渊行者", "AbyssWalker", "str", "dex", "con", "int", "wis");

        // ---- 1 种全属性 ----
        Add("万象",     "Omni",        "str", "dex", "con", "int", "wis", "cha");
    }

    private static void Add(string chinese, string english, params string[] regions)
    {
        var weights = new Dictionary<string, float>
        {
            ["str"] = 0.4f, ["dex"] = 0.4f, ["con"] = 0.4f,
            ["intel"] = 0.4f, ["wis"] = 0.4f, ["cha"] = 0.4f,
        };
        // 主属性按位置降权：第一个最高，后续按 1.5x 递减
        float topWeight = 3.0f;
        for (int i = 0; i < regions.Length; i++)
        {
            string key = regions[i] == "int" ? "intel" : regions[i];
            float w = topWeight / (1.0f + i * 0.6f); // 3.0, 1.875, 1.36, 1.07, 0.88, 0.75
            if (weights.ContainsKey(key)) weights[key] = w;
        }

        _all!.Add(new BuildProfile
        {
            ChineseName = chinese,
            EnglishKey = english,
            AttrWeights = weights,
            TargetRegions = regions,
            AttrCount = regions.Length,
        });
    }
}
