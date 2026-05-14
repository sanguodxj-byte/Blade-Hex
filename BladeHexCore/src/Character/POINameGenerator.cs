// POINameGenerator.cs
// POI (兴趣点) 命名生成器 — 处理城镇、村庄、要塞等地理位置的命名
// 采用西幻风格命名法：城市直接使用地名（无后缀），村庄保留少量后缀
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

    // ========================================
    // 城市名 — 西幻风格，直接作为地名使用（无"城""市"后缀）
    // 类似 Whiterun, Solitude, Novigrad, Oxenfurt 的命名方式
    // ========================================

    private static readonly Dictionary<string, string[]> CityNamesZH = new()
    {
        ["River"] = ["碧水镇", "渡鸦渡", "银滩", "清溪", "双桥", "激流堡", "雾津", "长河"],
        ["Mountain"] = ["铁砧", "鹰巢", "石门", "霜顶", "断崖", "风暴关", "灰岩", "高塔"],
        ["Forest"] = ["翠谷", "影木", "鹿鸣", "绿荫", "枫丹", "幽径", "古橡", "密林"],
        ["Coast"] = ["白帆", "潮汐", "海门", "盐风", "远望", "碎浪", "珊瑚", "灯塔"],
        ["Plain"] = ["金穗", "长风", "旷野", "丰饶", "暖阳", "麦田", "自由原", "奔马"],
        ["Swamp"] = ["暗沼", "雾沉", "黑水", "孤灯", "沉寂", "幽潭", "迷途", "枯骨"]
    };

    private static readonly Dictionary<string, string[]> CityNamesEN = new()
    {
        ["River"] = ["Riverwatch", "Ravenford", "Silvershore", "Clearbrook", "Twinbridge", "Torrenthold", "Mistford", "Longwater"],
        ["Mountain"] = ["Ironhearth", "Eaglecrest", "Stonegate", "Frostpeak", "Cliffmere", "Stormpass", "Greyrock", "Hightower"],
        ["Forest"] = ["Greenhollow", "Shadowmere", "Stagrun", "Verdant", "Maplewood", "Deeppath", "Oldoak", "Thornwall"],
        ["Coast"] = ["Whitesail", "Tidecrest", "Seagate", "Saltmere", "Farwatch", "Breakwater", "Coralport", "Beacon"],
        ["Plain"] = ["Goldfield", "Longwind", "Wildmoor", "Bountiful", "Sunhearth", "Wheatholm", "Freehold", "Swiftmere"],
        ["Swamp"] = ["Darkfen", "Misthollow", "Blackwater", "Lonelight", "Stillmere", "Gloompool", "Lostmoor", "Bleakmarsh"]
    };

    // ========================================
    // 村庄名 — 保留少量后缀，更朴素的命名
    // ========================================

    private static readonly Dictionary<string, string[]> VillageNamesZH = new()
    {
        ["River"] = ["柳溪", "浅滩", "石桥", "清泉", "渔歌", "芦苇", "溪口", "水磨"],
        ["Mountain"] = ["山脚", "石屋", "矿坑", "岩洞", "羊肠", "采石", "风口", "隘口"],
        ["Forest"] = ["林间", "蘑菇", "猎户", "伐木", "松针", "橡果", "鸟巢", "苔藓"],
        ["Coast"] = ["渔村", "贝壳", "海草", "晒网", "船坞", "蛤蜊", "潮间", "礁石"],
        ["Plain"] = ["麦垛", "风车", "牧羊", "谷仓", "篱笆", "井台", "草垛", "犁田"],
        ["Swamp"] = ["浮木", "蛙鸣", "泥屋", "苇荡", "水洼", "蚊沼", "朽桥", "菌落"]
    };

    private static readonly Dictionary<string, string[]> VillageNamesEN = new()
    {
        ["River"] = ["Willowbrook", "Shallows", "Stonebridge", "Clearspring", "Fishsong", "Reedbank", "Creekmouth", "Millwater"],
        ["Mountain"] = ["Foothill", "Stonehouse", "Mineshaft", "Cavern", "Goatpath", "Quarry", "Windgap", "Narrowpass"],
        ["Forest"] = ["Glade", "Mushroom", "Huntsman", "Loggers", "Pineneedle", "Acorn", "Birdnest", "Mossbed"],
        ["Coast"] = ["Fisherton", "Shellcove", "Seagrass", "Netdry", "Dockside", "Clamshore", "Tidewash", "Reefside"],
        ["Plain"] = ["Haystack", "Windmill", "Shepherd", "Granary", "Hedgerow", "Wellside", "Haybale", "Plowfield"],
        ["Swamp"] = ["Driftwood", "Frogmere", "Mudhouse", "Reedmoor", "Puddle", "Midgefen", "Rotbridge", "Sporecroft"]
    };

    // ========================================
    // 要塞名 — 威严、军事化
    // ========================================

    private static readonly string[] FortressNamesZH = [
        "铁壁", "黑鸦", "狮鹫", "寒霜", "烈焰", "雷霆", "暗影",
        "碎盾", "血誓", "钢牙", "风暴", "龙脊", "鹰眼", "裂地"
    ];

    private static readonly string[] FortressNamesEN = [
        "Ironwall", "Blackraven", "Griffin", "Frostguard", "Blazewatch", "Thunderhold", "Shadowkeep",
        "Shieldbreak", "Bloodsworn", "Steelfang", "Stormwatch", "Dragonspine", "Hawkeye", "Earthrend"
    ];

    private static readonly string[] FortressSuffixesZH = ["要塞", "堡", "关", "壁垒", "哨塔"];
    private static readonly string[] FortressSuffixesEN = ["Fortress", "Keep", "Gate", "Bastion", "Watchtower"];

    // ========================================
    // 修道院名
    // ========================================

    private static readonly string[] MonasteryNamesZH = [
        "晨光", "静默", "圣泉", "白鸽", "银钟", "古卷", "星辰", "净土"
    ];

    private static readonly string[] MonasteryNamesEN = [
        "Dawnlight", "Silence", "Holyfount", "Whitedove", "Silverbell", "Oldscroll", "Starfall", "Pureheart"
    ];

    private static readonly string[] MonasterySuffixesZH = ["修道院", "圣所", "祭坛", "隐修处"];
    private static readonly string[] MonasterySuffixesEN = ["Monastery", "Sanctuary", "Altar", "Hermitage"];

    // ========================================
    // 酒馆名 — 保持原有风格
    // ========================================

    private static readonly string[] TavernNounsZH = [
        "金杯", "醉鹿", "破盾", "独眼", "老马", "飞龙", "锈钉", "银币",
        "怒熊", "狂欢", "落日", "黎明", "断剑", "无头", "黑猫", "野猪"
    ];

    private static readonly string[] TavernNounsEN = [
        "Golden Cup", "Drunken Deer", "Broken Shield", "One-Eye", "Old Horse", "Flying Dragon",
        "Rusty Nail", "Silver Coin", "Angry Bear", "Merry", "Sunset", "Dawn",
        "Broken Sword", "Headless", "Black Cat", "Wild Boar"
    ];

    private static readonly string[] TavernSuffixesZH = ["酒馆", "客栈", "驿站", "酒肆", "旅店"];
    private static readonly string[] TavernSuffixesEN = ["Tavern", "Inn", "Waystation", "Pub", "Lodge"];

    // ========================================
    // 通用修饰词（低概率前置）
    // ========================================

    private static readonly string[] GenericPrefixesZH = ["新", "老", "上", "下", "远", "圣"];
    private static readonly string[] GenericPrefixesEN = ["New", "Old", "Upper", "Lower", "Far", "Saint"];

    /// <summary>
    /// 生成 POI 名称 — 西幻风格命名。
    /// 城市：直接使用地名，不加"城""市"后缀（如 "铁砧"、"碧水镇"、"Ironhearth"）
    /// 村庄：朴素地名（如 "柳溪"、"Willowbrook"）
    /// 要塞/修道院/酒馆：保留类型后缀
    /// </summary>
    /// <param name="type">POI 类型</param>
    /// <param name="terrainKey">地形特征键 (River, Mountain, Forest, Coast, Plain, Swamp)。若留空则随机。</param>
    public static string GeneratePOIName(POIType type, string terrainKey = "")
    {
        bool isZH = NameGenerator.GetCurrentLanguage() == "zh";

        // 确定地形键
        string terrain = terrainKey;
        if (string.IsNullOrEmpty(terrain))
        {
            var allKeys = new List<string>(CityNamesZH.Keys);
            terrain = allKeys[(int)(GD.Randi() % (uint)allKeys.Count)];
        }
        else if (!CityNamesZH.ContainsKey(terrain))
        {
            terrain = "Plain";
        }

        switch (type)
        {
            case POIType.City:
                return GenerateCityName(terrain, isZH);

            case POIType.Village:
                return GenerateVillageName(terrain, isZH);

            case POIType.Fortress:
                return GenerateFortressName(isZH);

            case POIType.Monastery:
                return GenerateMonasteryName(isZH);

            case POIType.Tavern:
                return GenerateTavernName(isZH);

            default:
                return GenerateCityName(terrain, isZH);
        }
    }

    /// <summary>城市名 — 直接地名，无后缀</summary>
    private static string GenerateCityName(string terrain, bool isZH)
    {
        var pool = isZH ? CityNamesZH[terrain] : CityNamesEN[terrain];
        string name = pool[GD.Randi() % (uint)pool.Length];

        // 15% 概率加修饰词
        if (GD.Randf() < 0.15f)
        {
            var prefixes = isZH ? GenericPrefixesZH : GenericPrefixesEN;
            string prefix = prefixes[GD.Randi() % (uint)prefixes.Length];
            name = isZH ? prefix + name : prefix + " " + name;
        }

        return name;
    }

    /// <summary>村庄名 — 朴素地名</summary>
    private static string GenerateVillageName(string terrain, bool isZH)
    {
        var pool = isZH ? VillageNamesZH[terrain] : VillageNamesEN[terrain];
        return pool[GD.Randi() % (uint)pool.Length];
    }

    /// <summary>要塞名 — 名称 + 后缀</summary>
    private static string GenerateFortressName(bool isZH)
    {
        var names = isZH ? FortressNamesZH : FortressNamesEN;
        var suffixes = isZH ? FortressSuffixesZH : FortressSuffixesEN;
        string name = names[GD.Randi() % (uint)names.Length];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
        return isZH ? name + suffix : name + " " + suffix;
    }

    /// <summary>修道院名 — 名称 + 后缀</summary>
    private static string GenerateMonasteryName(bool isZH)
    {
        var names = isZH ? MonasteryNamesZH : MonasteryNamesEN;
        var suffixes = isZH ? MonasterySuffixesZH : MonasterySuffixesEN;
        string name = names[GD.Randi() % (uint)names.Length];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
        return isZH ? name + suffix : name + " " + suffix;
    }

    /// <summary>酒馆名 — 名词 + 后缀</summary>
    private static string GenerateTavernName(bool isZH)
    {
        var nouns = isZH ? TavernNounsZH : TavernNounsEN;
        var suffixes = isZH ? TavernSuffixesZH : TavernSuffixesEN;
        string noun = nouns[GD.Randi() % (uint)nouns.Length];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
        return isZH ? noun + suffix : noun + " " + suffix;
    }
}
