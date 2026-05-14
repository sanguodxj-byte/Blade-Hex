// RaceNationMapping.cs
// 玩家种族 → 出身国家 的映射 SSOT
//
// 用途：快速游戏或选出身时，根据玩家种族决定
// - 优先级排列的匹配国家列表（按贴合度）
// - 出身地选在匹配国家首都附近
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Strategic;

public static class RaceNationMapping
{
    /// <summary>
    /// 玩家种族 → 偏好的 NationConfig.Race 列表（按优先级排列）
    /// 第一个匹配的即为"母国"；后续作为 fallback
    /// </summary>
    public static string[] GetPreferredNationRaces(RaceData.Race race) => race switch
    {
        RaceData.Race.Human => new[] { "human" },
        RaceData.Race.Elf => new[] { "elf", "human" },  // 精灵国不存时投靠人类
        RaceData.Race.Dwarf => new[] { "dwarf", "human" },
        RaceData.Race.HalfOrc => new[] { "orc", "human" }, // 半兽人回血牙部落或人类边境
        RaceData.Race.HalfElf => new[] { "human", "elf" }, // 半精灵在两族间取近
        _ => new[] { "human" },
    };

    /// <summary>
    /// 玩家种族 → 偏好的生态类型（用于在母国缺失时找"家园感"地形）
    /// </summary>
    public static BiomeType[] GetHomelandBiomes(RaceData.Race race) => race switch
    {
        RaceData.Race.Human => new[] { BiomeType.Plains, BiomeType.Coastal },
        RaceData.Race.Elf => new[] { BiomeType.Forest, BiomeType.Jungle },
        RaceData.Race.Dwarf => new[] { BiomeType.Mountain, BiomeType.Tundra },
        RaceData.Race.HalfOrc => new[] { BiomeType.Wasteland, BiomeType.Plains },
        RaceData.Race.HalfElf => new[] { BiomeType.Plains, BiomeType.Forest },
        _ => new[] { BiomeType.Plains },
    };

    /// <summary>
    /// 在生成好的国家清单中，为玩家挑选母国。
    /// 优先匹配 Race 字段，失败则回退到偏好的第二、第三项。
    /// 所有候选都失败（世界生成没给该种族分配领土）时返回第一个可用的主国。
    /// </summary>
    public static NationConfig? FindHomeNation(
        RaceData.Race playerRace,
        IReadOnlyDictionary<string, NationTerritory> territories,
        IReadOnlyList<NationConfig> nations)
    {
        var preferred = GetPreferredNationRaces(playerRace);

        // 逐项匹配
        foreach (var raceKey in preferred)
        {
            foreach (var n in nations)
            {
                if (n.Race == raceKey && territories.ContainsKey(n.Id))
                    return n;
            }
        }

        // 兜底：任何主要国家
        foreach (var n in nations)
        {
            if (n.IsMajorNation && territories.ContainsKey(n.Id))
                return n;
        }

        // 再兜底：任意有领土的国家
        foreach (var n in nations)
        {
            if (territories.ContainsKey(n.Id))
                return n;
        }

        return null;
    }
}
