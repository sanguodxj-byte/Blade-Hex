// POINameGenerator.cs
// POI (兴趣点) 命名生成器 — 处理城镇、村庄、要塞等地理位置的命名
using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

public static class POINameGenerator
{
    public enum POIType
    {
        City,
        Village,
        Fortress,
        Monastery,
        Tavern
    }

    // --- 扩展地形特定前缀 ---
    private static readonly Dictionary<string, string[]> TerrainPrefixesZH = new()
    {
        ["River"] = ["河湾", "渡口", "激流", "碧水", "两岸", "溯源", "沉舟", "清溪"],
        ["Mountain"] = ["山脚", "峻岭", "岩巅", "石柱", "断崖", "避风", "铁砧", "孤峰"],
        ["Forest"] = ["绿溪", "松林", "幽叶", "密林", "枯木", "影歌", "翠冠", "迷雾"],
        ["Coast"] = ["晨曦", "怒涛", "咸风", "海角", "听潮", "碎浪", "远航", "汐海"],
        ["Plain"] = ["风车", "麦浪", "丰收", "平野", "无际", "长风", "暖阳", "青草"],
        ["Swamp"] = ["黑泥", "沉沦", "毒雾", "暗鳞", "死水", "孤岛", "迷途", "哀鸣"]
    };

    private static readonly Dictionary<string, string[]> TerrainPrefixesEN = new()
    {
        ["River"] = ["Riverbend", "Crossriver", "Swiftwater", "Clearstream", "Riverwalk", "Upstream", "Riverend", "Greywater"],
        ["Mountain"] = ["Mountainfoot", "Highpeak", "Rocktop", "Stonepillar", "Cliffside", "Windshield", "Ironanvil", "Lonepeak"],
        ["Forest"] = ["Greencreek", "Pinewood", "Deepwood", "Shadowleaf", "Ironwood", "Shadowsong", "Greenleaf", "Mistwood"],
        ["Coast"] = ["Sunbreak", "Stormsurf", "Saltwind", "Seacape", "Tidelisten", "Brokenwave", "Farvoyage", "Tidesea"],
        ["Plain"] = ["Windmill", "Wheatwave", "Harvest", "Plainfield", "Boundless", "Longwind", "Warmsun", "Greengrass"],
        ["Swamp"] = ["Blackmud", "Sinking", "Poisonmist", "Darkscale", "Stillwater", "Loneisland", "Lostway", "Moaning"]
    };

    // 通用修饰词 (增加变体)
    private static readonly string[] GenericPrefixesZH = ["新", "老", "旧", "大", "小", "上", "下", "远", "近", "圣", "废"];
    private static readonly string[] GenericPrefixesEN = ["New", "Old", "Elder", "Great", "Little", "Upper", "Lower", "Far", "Near", "Saint", "Lost"];

    // --- 扩展后缀 ---
    private static readonly Dictionary<POIType, string[]> SuffixesZH = new()
    {
        [POIType.City] = ["城", "都", "市", "港", "堡", "都城", "卫城", "自由港"],
        [POIType.Village] = ["村", "镇", "聚落", "农场", "庄园", "邑", "领", "哨所"],
        [POIType.Fortress] = ["要塞", "堡", "岗哨", "关卡", "壁垒", "哨塔", "铁卫"],
        [POIType.Monastery] = ["修道院", "圣所", "药师所", "塔", "祭坛", "隐修处", "经堂"],
        [POIType.Tavern] = ["酒馆", "客栈", "驿站", "酒肆", "旅店", "歇脚处"]
    };

    private static readonly Dictionary<POIType, string[]> SuffixesEN = new()
    {
        [POIType.City] = ["City", "Haven", "Port", "Metropolis", "Burg", "Capital", "Acre", "Harbor"],
        [POIType.Village] = ["Village", "Hamlet", "Settlement", "Farm", "Manor", "Steading", "Camp", "Township"],
        [POIType.Fortress] = ["Fortress", "Keep", "Outpost", "Gate", "Bastion", "Watch", "Citadel", "Stronghold"],
        [POIType.Monastery] = ["Monastery", "Sanctuary", "Temple", "Tower", "Altar", "Hermitage", "Shrine"],
        [POIType.Tavern] = ["Tavern", "Inn", "Waystation", "Pub", "Hostel", "Lounge"]
    };

    // 酒馆名扩展 (Noun + Noun/Adj)
    private static readonly string[] TavernNounsZH = ["金杯", "醉鹿", "破盾", "独眼", "老马", "飞龙", "锈钉", "银币", "怒熊", "狂欢", "落日", "黎明", "断剑", "无头", "黑猫", "野猪"];
    private static readonly string[] TavernNounsEN = ["Golden Cup", "Drunken Deer", "Broken Shield", "One-Eye", "Old Horse", "Flying Dragon", "Rusty Nail", "Silver Coin", "Angry Bear", "Merry-making", "Sunset", "Dawn", "Broken Sword", "Headless", "Black Cat", "Wild Boar"];

    /// <summary>
    /// 生成 POI 名称。
    /// </summary>
    /// <param name="type">POI 类型</param>
    /// <param name="terrainKey">地形特征键 (River, Mountain, Forest, Coast, Plain, Swamp)。若留空则随机。</param>
    public static string GeneratePOIName(POIType type, string terrainKey = "")
    {
        bool isZH = NameGenerator.GetCurrentLanguage() == "zh";

        // 1. 特殊酒馆逻辑
        if (type == POIType.Tavern && GD.Randf() > 0.4f)
        {
            var nouns = isZH ? TavernNounsZH : TavernNounsEN;
            string name = nouns[GD.Randi() % (uint)nouns.Length];
            var taverns = isZH ? SuffixesZH[POIType.Tavern] : SuffixesEN[POIType.Tavern];
            string tavernSfx = taverns[GD.Randi() % (uint)taverns.Length];
            return isZH ? $"{name}{tavernSfx}" : $"{name} {tavernSfx}";
        }

        // 2. 确定前缀池
        string[] prefixPool;
        if (!string.IsNullOrEmpty(terrainKey) && TerrainPrefixesZH.ContainsKey(terrainKey))
        {
            prefixPool = isZH ? TerrainPrefixesZH[terrainKey] : TerrainPrefixesEN[terrainKey];
        }
        else
        {
            // 随机选一个地形池或使用通用池
            var allKeys = new List<string>(TerrainPrefixesZH.Keys);
            string randomKey = allKeys[(int)(GD.Randi() % (uint)allKeys.Count)];
            prefixPool = isZH ? TerrainPrefixesZH[randomKey] : TerrainPrefixesEN[randomKey];
        }

        string prefix = prefixPool[GD.Randi() % (uint)prefixPool.Length];

        // 3. 概率性添加通用修饰词 (如 "新" 河湾村)
        if (GD.Randf() < 0.2f)
        {
            var generics = isZH ? GenericPrefixesZH : GenericPrefixesEN;
            string gen = generics[GD.Randi() % (uint)generics.Length];
            prefix = isZH ? gen + prefix : gen + " " + prefix;
        }

        // 4. 获取后缀
        var suffixes = isZH ? SuffixesZH[type] : SuffixesEN[type];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];

        return isZH ? $"{prefix}{suffix}" : $"{prefix} {suffix}";
    }

}
