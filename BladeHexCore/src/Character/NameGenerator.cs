// NameGenerator.cs
// 角色与势力命名逻辑实现 — 处理不同种族、阶层与高级角色的称号生成
// 对应策划案 naming_logic_design.md
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 命名生成器 — 处理角色名、称号、势力名等生成逻辑
/// </summary>
public static class NameGenerator
{
    // ========================================================================
    // 数据池
    // ========================================================================

    private static readonly Dictionary<RaceData.Race, string[]> BaseNames = new()
    {
        [RaceData.Race.Human] = ["Alaric", "Cedric", "Edward", "Roland", "Valerius", "Finn", "Jax", "Toby", "Silas", "Mira"],
        [RaceData.Race.Elf] = ["Thalathas", "Elenariel", "Sylvaron", "Valindra", "Aelwyn", "Luthien", "Faelan", "Nymue"],
        [RaceData.Race.Dwarf] = ["Balgruuf", "Thrain", "Durin", "Grog", "Helga", "Thorin", "Balin", "Dwalin", "Gimli"],
        [RaceData.Race.HalfOrc] = ["Grom", "Azog", "Garosh", "Mok", "Krog", "Urgash", "Shaka", "Mukar", "Zagra"],
        [RaceData.Race.HalfElf] = ["Aela", "Aiden", "Miriel", "Thal", "Lia", "Kaelen", "Elowen", "Arinn"],
    };

    private static readonly Dictionary<RaceData.Race, string[]> BaseNamesZH = new()
    {
        [RaceData.Race.Human] = ["阿拉里克", "塞德里克", "爱德华", "罗兰", "瓦勒留", "芬恩", "贾克斯", "托比", "塞拉斯", "米拉"],
        [RaceData.Race.Elf] = ["萨拉萨斯", "埃伦娜莉", "赛瓦隆", "瓦琳卓", "艾洛温", "露西安", "法伦", "妮妙"],
        [RaceData.Race.Dwarf] = ["巴尔古夫", "索恩", "都灵", "格罗格", "赫尔加", "索林", "巴林", "德瓦林", "吉姆利"],
        [RaceData.Race.HalfOrc] = ["格罗姆", "阿佐格", "加尔鲁什", "莫克", "克罗格", "乌加什", "夏卡", "穆卡尔", "扎格拉"],
        [RaceData.Race.HalfElf] = ["艾拉", "艾登", "米瑞尔", "塔尔", "莉娅", "凯伦", "艾洛温", "阿琳"],
    };

    // 称号前缀 (Prefixes) - 扩展至 30+
    private static readonly Dictionary<string, string> EpithetPrefixes = new()
    {
        // 自然与元素
        ["Storm"] = "怒风", ["Wind"] = "风行", ["Frost"] = "霜语", ["Flame"] = "烈焰",
        ["Thunder"] = "雷鸣", ["Cloud"] = "云端", ["Star"] = "星辰", ["Moon"] = "月影",
        ["Sun"] = "炽阳", ["Ocean"] = "深海", ["Earth"] = "大地", ["Sky"] = "苍穹",
        ["Fire"] = "烈火", ["Rock"] = "磐石", ["Stone"] = "坚石", ["Gold"] = "黄金",
        ["Silver"] = "白银", ["Deep"] = "深渊", ["Ancient"] = "远古",
        // 战斗与金属
        ["War"] = "战争", ["Iron"] = "铁血", ["Steel"] = "钢铁", ["Blood"] = "鲜血",
        ["Gore"] = "血染", ["Skull"] = "碎颅", ["Bone"] = "白骨", ["Oath"] = "守誓",
        ["Shield"] = "坚盾", ["Blade"] = "锋刃", ["Hammer"] = "重锤", ["Anvil"] = "碎砧",
        // 神秘与阴影
        ["Shadow"] = "暗影", ["Night"] = "永夜", ["Spell"] = "奥术", ["Soul"] = "灵魂",
        ["Void"] = "虚空", ["Light"] = "光明", ["Ash"] = "灰烬", ["Ghost"] = "幽灵",
        ["Dream"] = "幻梦", ["Chaos"] = "混乱",
        // 生物与自然
        ["Lion"] = "狮心", ["Wolf"] = "孤狼", ["Bear"] = "悍熊", ["Eagle"] = "苍鹰",
        ["Serpent"] = "毒蛇", ["Leaf"] = "绿叶", ["Thorn"] = "荆棘", ["Mountain"] = "高山",
    };

    // 称号后缀 (Suffixes) - 扩展至 30+
    private static readonly Dictionary<string, string> EpithetSuffixes = new()
    {
        // 职业与身份
        ["runner"] = "行者", ["walker"] = "步者", ["seeker"] = "寻路者", ["keeper"] = "守护者",
        ["bringer"] = "使者", ["weaver"] = "编织者", ["gazer"] = "观星者", ["shaper"] = "塑形者",
        ["master"] = "大师", ["warden"] = "守卫", ["delver"] = "掘者", ["stalker"] = "猎手",
        // 动作与后果
        ["rage"] = "怒", ["song"] = "之歌", ["cry"] = "咆哮", ["whisper"] = "细语",
        ["breaker"] = "碎", ["crusher"] = "裂", ["slayer"] = "戮", ["binder"] = "缚",
        ["striker"] = "击", ["dancer"] = "舞", ["chewer"] = "噬", ["fiend"] = "魔",
        // 器物与身体
        ["heart"] = "心", ["blade"] = "刃", ["shield"] = "盾", ["hand"] = "手",
        ["eye"] = "之眼", ["fang"] = "牙", ["claw"] = "爪", ["wing"] = "翼",
        ["hoof"] = "蹄", ["tail"] = "尾", ["step"] = "步", ["will"] = "志",
        ["brand"] = "烙印", ["thorn"] = "刺", ["veil"] = "面纱",
        ["axe"] = "斧", ["wall"] = "壁", ["beard"] = "须", ["skin"] = "肤",
    };

    // 种族偏好组合 (按种族文化倾向分配词根，支持 200+ 变体)
    private static readonly Dictionary<RaceData.Race, string[][]> EpithetPools = new()
    {
        [RaceData.Race.Human] = [
            ["Lion", "heart"], ["Light", "bringer"], ["Oath", "keeper"], ["Ash", "walker"], ["Iron", "will"],
            ["War", "song"], ["Shield", "warden"], ["Steel", "shaper"], ["Sun", "brand"], ["Cloud", "runner"],
            ["Sky", "warden"], ["Ocean", "seeker"], ["Earth", "shaper"], ["Blade", "master"], ["Silver", "hand"]
        ],
        [RaceData.Race.Elf] = [
            ["Star", "gazer"], ["Moon", "blade"], ["Leaf", "whisper"], ["Spell", "weaver"], ["Wind", "runner"],
            ["Night", "veil"], ["Dream", "walker"], ["Frost", "song"], ["Thorn", "dancer"], ["Sky", "wing"],
            ["Eagle", "eye"], ["Serpent", "fang"], ["Ocean", "whisper"], ["Flame", "dancer"], ["Void", "walker"]
        ],
        [RaceData.Race.Dwarf] = [
            ["Stone", "wall"], ["Deep", "delver"], ["Anvil", "breaker"], ["Mountain", "eye"], ["Iron", "heart"],
            ["Earth", "binder"], ["Steel", "shield"], ["Hammer", "striker"], ["Frost", "beard"], ["Gold", "seeker"],
            ["Ancient", "keeper"], ["Rock", "shaper"], ["Fire", "brand"], ["Shield", "breaker"], ["Stone", "will"]
        ],
        [RaceData.Race.HalfOrc] = [
            ["Blood", "axe"], ["Bone", "chewer"], ["Thunder", "hoof"], ["Gore", "fiend"], ["Skull", "crusher"],
            ["War", "cry"], ["Steel", "claw"], ["Wolf", "slayer"], ["Chaos", "bringer"], ["Storm", "rage"],
            ["Bear", "heart"], ["Shadow", "stalker"], ["Blade", "breaker"], ["Iron", "skin"], ["Gore", "hand"]
        ],
        [RaceData.Race.HalfElf] = [
            ["Wind", "runner"], ["Moon", "blade"], ["Shadow", "step"], ["Light", "bringer"], ["Dream", "weaver"],
            ["Star", "seeker"], ["Storm", "whisper"], ["Sun", "walker"], ["Void", "seeker"], ["Ghost", "walker"]
        ]
    };

    // ========================================================================
    // 公开接口
    // ========================================================================

    /// <summary>获取当前语言设置 (zh 或 en)</summary>
    public static string GetCurrentLanguage()
    {
        string locale = TranslationServer.GetLocale();
        if (locale.StartsWith("zh")) return "zh";
        return "en";
    }

    /// <summary>生成基础名</summary>
    public static string GenerateBaseName(RaceData.Race race)
    {
        var namesZH = BaseNamesZH.GetValueOrDefault(race, BaseNamesZH[RaceData.Race.Human]);
        var namesEN = BaseNames.GetValueOrDefault(race, BaseNames[RaceData.Race.Human]);
        
        uint idx = GD.Randi() % (uint)namesZH.Length;
        
        return GetCurrentLanguage() == "zh" ? namesZH[idx] : namesEN[idx];
    }

    /// <summary>生成角色全名（含称号逻辑）</summary>
    public static string GenerateFullName(RaceData.Race race, int level)
    {
        var namesZH = BaseNamesZH.GetValueOrDefault(race, BaseNamesZH[RaceData.Race.Human]);
        var namesEN = BaseNames.GetValueOrDefault(race, BaseNames[RaceData.Race.Human]);
        uint idx = GD.Randi() % (uint)namesZH.Length;
        
        string baseNameZH = namesZH[idx];
        string baseNameEN = namesEN[idx];

        if (level >= 5)
        {
            var epithet = RollEpithet(race);
            string titleEN = epithet.Item1 + epithet.Item2;
            string titleZH = EpithetPrefixes[epithet.Item1] + EpithetSuffixes[epithet.Item2];
            
            // 修正特殊中文呈现（如 怒风者 -> 怒风）
            if (titleZH.EndsWith("者者")) titleZH = titleZH.Replace("者者", "者");
            if (titleZH == "怒风者") titleZH = "怒风"; // 特例匹配策划案

            if (GetCurrentLanguage() == "zh")
                return $"{titleZH} {baseNameZH}";
            else
                return $"{FormatTitleEN(epithet.Item1, epithet.Item2)} {baseNameEN}";
        }

        return GetCurrentLanguage() == "zh" ? baseNameZH : baseNameEN;
    }

    /// <summary>格式化英文称号（如 Storm-rage -> Stormrage）</summary>
    private static string FormatTitleEN(string prefix, string suffix)
    {
        // 这里可以根据需要添加更复杂的格式化逻辑，目前简单拼接
        return prefix + suffix;
    }

    /// <summary>为势力生成名称</summary>
    public static string GenerateFactionName(RaceData.Race race)
    {
        return FactionNameGenerator.GenerateFactionName(race);
    }

    // ========================================================================
    // 内部逻辑
    // ========================================================================

    private static Tuple<string, string> RollEpithet(RaceData.Race race)
    {
        var pool = EpithetPools.GetValueOrDefault(race, EpithetPools[RaceData.Race.Human]);
        var choice = pool[GD.Randi() % (uint)pool.Length];
        return new Tuple<string, string>(choice[0], choice[1]);
    }
}
