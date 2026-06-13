// GeographicNameGenerator.cs
// 地理特征命名生成器 — 为山脉、森林、海洋、平原等大型地理区域生成名称
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Data;

/// <summary>
/// 地理特征命名生成器 — 为大型地理区域生成名称
/// </summary>
public static class GeographicNameGenerator
{
    // ========================================
    // 世界名
    // ========================================

    private static readonly string[] WorldNamesZH = ["阿瓦隆尼亚", "艾尔特兰", "诸神之地", "永恒大陆", "命运之境"];
    private static readonly string[] WorldNamesEN = ["Avalonia", "Eitran", "Godsland", "Eternal Continent", "Realm of Fate"];

    // ========================================
    // 海洋名
    // ========================================

    private static readonly string[] OceanPrefixesZH = ["北冥", "南溟", "西风", "东曦", "幽暗", "碧波", "怒涛", "永夜", "晨曦", "深渊"];
    private static readonly string[] OceanSuffixesZH = ["之海", "深渊", "之洋", "汪洋", "冥海", "大洋"];
    private static readonly string[] OceanPrefixesEN = ["Northern", "Southern", "Western", "Eastern", "Dark", "Azure", "Storm", "Eternal", "Dawn", "Abyssal"];
    private static readonly string[] OceanSuffixesEN = [" Sea", " Deep", " Ocean", " Expanse", " Abyss", " Waters"];

    // ========================================
    // 地形特征名（按 BiomeType）
    // ========================================

    private static readonly Dictionary<BiomeType, string[]> FeaturePrefixesZH = new()
    {
        [BiomeType.Mountain] = ["霜冠", "铁砧", "龙脊", "雷鸣", "孤峰", "碎骨", "苍穹", "云端", "巨人", "黑铁", "白骨", "风暴"],
        [BiomeType.Forest] = ["银叶", "翡翠", "暮光", "古木", "迷雾", "幽影", "千年", "月光", "精灵", "枯枝", "深绿", "低语"],
        [BiomeType.Plains] = ["金穗", "长风", "无垠", "丰饶", "晨露", "暖阳", "碧野", "天际", "自由", "奔马", "麦浪", "青翠"],
        [BiomeType.Wasteland] = ["焦土", "灰烬", "荒芜", "死寂", "裂隙", "枯骨", "赤红", "风蚀", "遗忘", "末日", "干涸", "烈日"],
        [BiomeType.Swamp] = ["蛮荒", "毒雾", "黑水", "沉沦", "腐朽", "暗影", "死寂", "哀鸣", "迷途", "幽暗", "瘴气", "沼泽"],
        [BiomeType.Tundra] = ["永冻", "极寒", "白霜", "冰封", "凛冬", "雪盲", "北风", "冰晶", "苍白", "寒骨", "冻土", "霜降"],
        [BiomeType.Jungle] = ["蛮荒", "翠蛇", "巨藤", "热带", "密林", "猛兽", "毒花", "原始", "野性", "深绿", "蒸汽", "雨林"],
        [BiomeType.Coastal] = ["碧波", "珊瑚", "海风", "潮汐", "白沙", "远航", "灯塔", "渔歌", "海角", "浪花", "盐风", "晨雾"],
    };

    private static readonly Dictionary<BiomeType, string[]> FeatureSuffixesZH = new()
    {
        [BiomeType.Mountain] = ["山脉", "群峰", "高地", "峻岭", "山脊", "绝壁"],
        [BiomeType.Forest] = ["森林", "密林", "林地", "丛林", "树海", "绿洲"],
        [BiomeType.Plains] = ["平原", "草原", "旷野", "原野", "牧场", "沃土"],
        [BiomeType.Wasteland] = ["荒原", "废土", "荒漠", "戈壁", "不毛之地", "死地"],
        [BiomeType.Swamp] = ["沼泽", "湿地", "泥沼", "死水", "暗沼", "烂泥地"],
        [BiomeType.Tundra] = ["冻原", "雪原", "冰原", "极地", "荒原", "冰野"],
        [BiomeType.Jungle] = ["雨林", "丛林", "热带林", "蛮荒地", "野林", "绿狱"],
        [BiomeType.Coastal] = ["海岸", "海湾", "浅滩", "礁石", "港湾", "海域"],
    };

    // ========================================
    // 公开接口
    // ========================================

    /// <summary>世界名（固定，符合世界观设定）</summary>
    public static string GenerateWorldName(int seed)
    {
        bool isZH = NameGenerator.GetCurrentLanguage() == "zh";
        return isZH ? "阿瓦隆尼亚" : "Avalonia";
    }

    /// <summary>生成海洋名</summary>
    public static string GenerateOceanName(int seed, int index)
    {
        var rng = new Random(seed ^ (0x4F434541 + index)); // "OCEA" + index
        bool isZH = NameGenerator.GetCurrentLanguage() == "zh";

        if (isZH)
        {
            string prefix = OceanPrefixesZH[rng.Next(OceanPrefixesZH.Length)];
            string suffix = OceanSuffixesZH[rng.Next(OceanSuffixesZH.Length)];
            return prefix + suffix;
        }
        else
        {
            string prefix = OceanPrefixesEN[rng.Next(OceanPrefixesEN.Length)];
            string suffix = OceanSuffixesEN[rng.Next(OceanSuffixesEN.Length)];
            return prefix + suffix;
        }
    }

    /// <summary>为地理区域（BiomeZone）生成名称</summary>
    public static string GenerateFeatureName(BiomeType biome, int seed, int index)
    {
        var rng = new Random(seed ^ (0x47454F + index)); // "GEO" + index
        bool isZH = NameGenerator.GetCurrentLanguage() == "zh";

        var prefixes = FeaturePrefixesZH.GetValueOrDefault(biome, FeaturePrefixesZH[BiomeType.Plains]);
        var suffixes = FeatureSuffixesZH.GetValueOrDefault(biome, FeatureSuffixesZH[BiomeType.Plains]);

        string prefix = prefixes[rng.Next(prefixes.Length)];
        string suffix = suffixes[rng.Next(suffixes.Length)];

        return isZH ? prefix + suffix : $"{prefix} {suffix}";
    }

    public static string GenerateFeatureName(HexOverworldTile.TerrainType terrain, int seed, int index)
    {
        bool isZH = NameGenerator.GetCurrentLanguage() == "zh";
        var generator = new RegionNameGenerator(seed ^ (0x47454F + index));
        var (english, chinese) = generator.GenerateName(terrain, index);
        return isZH ? chinese : english;
    }

    /// <summary>为 RegionDef 生成名称（基于其偏好地形）</summary>
    public static string GenerateRegionName(BiomeType dominantBiome, int seed, int regionIndex)
    {
        return GenerateFeatureName(dominantBiome, seed, regionIndex);
    }

    public static string GenerateRegionName(HexOverworldTile.TerrainType dominantTerrain, int seed, int regionIndex)
    {
        return GenerateFeatureName(dominantTerrain, seed, regionIndex);
    }
}
