// FactionNameGenerator.cs
// 势力命名生成器 — 处理国家、部落、联盟等政治实体的命名逻辑
using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

public static class FactionNameGenerator
{
    private static readonly Dictionary<RaceData.Race, string[]> FactionPrefixesZH = new()
    {
        [RaceData.Race.Human] = ["艾尔特兰", "罗德里安", "瓦伦西亚", "诺斯曼", "奥斯特", "卡斯提尔", "布列塔尼", "阿尔比恩", "弗兰德", "萨克森"],
        [RaceData.Race.Elf] = ["银叶", "月影", "晨曦", "星辰", "永恒", "翡翠", "琥珀", "幽蓝", "碧落", "苍穹"],
        [RaceData.Race.Dwarf] = ["铁炉堡", "深根", "霜锤", "金石", "雷岩", "黑铁", "赤铜", "白银", "碎石", "熔岩"],
        [RaceData.Race.HalfOrc] = ["赤铜", "血牙", "黑石", "战歌", "碎骨", "裂颅", "铁蹄", "焦土", "怒嚎", "断脊"],
    };

    private static readonly Dictionary<string, string[]> ExtendedPrefixesZH = new()
    {
        ["orc"] = ["血牙", "碎骨", "裂颅", "铁蹄", "焦土", "怒嚎", "断脊", "黑岩", "战吼", "蛮荒"],
        ["goblin"] = ["毒牙", "暗洞", "锈爪", "鼠窝", "腐沼", "尖啸", "黑影", "蛛巢"],
        ["kobold"] = ["掘地", "矿鼠", "暗道", "碎石", "铜爪", "洞穴", "深掘", "矿脉"],
        ["minotaur"] = ["铁角", "碎地", "蛮牛", "雷蹄", "裂岩", "血角", "怒牛", "巨角"],
        ["shadow_cult"] = ["暗影", "虚空", "深渊", "黑曜", "幽冥", "蚀月", "噬魂", "永夜"],
    };

    private static readonly Dictionary<string, string[]> ExtendedSuffixesZH = new()
    {
        ["orc"] = ["部落", "氏族", "战团", "盟约", "军团", "血盟"],
        ["goblin"] = ["部落", "巢穴", "群落", "窝点"],
        ["kobold"] = ["矿会", "掘进团", "洞穴联盟", "矿工会"],
        ["minotaur"] = ["氏族", "战团", "角斗会", "蛮族"],
        ["shadow_cult"] = ["教团", "结社", "密会", "暗盟"],
    };

    private static readonly Dictionary<RaceData.Race, string[]> FactionPrefixesEN = new()
    {
        [RaceData.Race.Human] = ["Eitran", "Rhoderian", "Valencia", "Northman", "Oster"],
        [RaceData.Race.Elf] = ["Silverleaf", "Moonshadow", "Dawnstar", "Starlight", "Eternal"],
        [RaceData.Race.Dwarf] = ["Ironforge", "Deeproot", "Frosthammer", "Goldstone", "Thunderock"],
        [RaceData.Race.HalfOrc] = ["Copper", "Bloodfang", "Blackrock", "Warsong", "Bonecrush"],
    };

    private static readonly Dictionary<RaceData.Race, string[]> FactionSuffixesZH = new()
    {
        [RaceData.Race.Human] = ["王国", "公国", "联邦", "帝国"],
        [RaceData.Race.Elf] = ["王庭", "议会", "林地", "仙境"],
        [RaceData.Race.Dwarf] = ["联盟", "城邦", "矿主会", "要塞群"],
        [RaceData.Race.HalfOrc] = ["部落", "氏族", "战团", "盟约"],
    };

    private static readonly Dictionary<RaceData.Race, string[]> FactionSuffixesEN = new()
    {
        [RaceData.Race.Human] = ["Kingdom", "Duchy", "Federation", "Empire"],
        [RaceData.Race.Elf] = ["Court", "Council", "Woodland", "Realm"],
        [RaceData.Race.Dwarf] = ["Alliance", "City-States", "Combine", "Strongholds"],
        [RaceData.Race.HalfOrc] = ["Tribe", "Clan", "Warband", "Covenant"],
    };

    public static string GenerateFactionName(RaceData.Race race)
    {
        bool isZH = NameGenerator.GetCurrentLanguage() == "zh";
        var prefixes = isZH ? FactionPrefixesZH : FactionPrefixesEN;
        var suffixes = isZH ? FactionSuffixesZH : FactionSuffixesEN;

        if (!prefixes.ContainsKey(race)) return isZH ? "未知势力" : "Unknown Faction";

        string prefix = prefixes[race][GD.Randi() % (uint)prefixes[race].Length];
        string suffix = suffixes[race][GD.Randi() % (uint)suffixes[race].Length];

        return isZH ? $"{prefix}{suffix}" : $"{prefix} {suffix}";
    }

    /// <summary>根据种族字符串生成势力名（支持扩展种族）</summary>
    public static string GenerateFactionNameByRace(string raceStr)
    {
        var raceMap = new Dictionary<string, RaceData.Race>
        {
            ["human"] = RaceData.Race.Human,
            ["elf"] = RaceData.Race.Elf,
            ["dwarf"] = RaceData.Race.Dwarf,
            ["orc"] = RaceData.Race.HalfOrc,
        };

        if (raceMap.TryGetValue(raceStr, out var race))
            return GenerateFactionName(race);

        // 扩展种族
        if (ExtendedPrefixesZH.TryGetValue(raceStr, out var prefixes) &&
            ExtendedSuffixesZH.TryGetValue(raceStr, out var suffixes))
        {
            string prefix = prefixes[GD.Randi() % (uint)prefixes.Length];
            string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
            return $"{prefix}{suffix}";
        }

        return "未知势力";
    }
}
