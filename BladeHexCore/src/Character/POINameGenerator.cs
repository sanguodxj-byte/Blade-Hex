// POINameGenerator.cs
// POI 命名生成器 — D&D/被遗忘国度风格
// 使用组合式生成：前缀词根 + 后缀词根，产生大量不重复名称
// 参考：Waterdeep, Neverwinter, Silverymoon, Baldur's Gate, Candlekeep
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
    // 城市名 — 组合式：前缀 + 后缀（每种地形 ~400 种组合）
    // ========================================

    private static readonly Dictionary<string, string[]> CityPrefixZH = new()
    {
        ["River"] = ["深水", "银", "碧", "长", "雾", "清", "双", "激浪", "蓝", "翡翠", "白", "玉", "静", "奔", "幽", "晶", "冰", "暖", "金", "月"],
        ["Mountain"] = ["铁", "寒", "雷", "鹰", "云", "碎", "风暴", "灰", "黑", "赤", "霜", "龙", "巨", "石", "钢", "暗", "烈", "高", "孤", "苍"],
        ["Forest"] = ["暗影", "银月", "翠", "古", "鹿", "绿", "枫", "密", "幽", "深", "精灵", "橡", "松", "柳", "藤", "苔", "蕨", "花", "蘑菇", "荆棘"],
        ["Coast"] = ["海", "白帆", "潮", "珊瑚", "远", "碎浪", "盐", "灯塔", "蔚蓝", "贝", "鲸", "锚", "帆", "浪", "沙", "珍珠", "海鸥", "礁", "星", "风"],
        ["Plain"] = ["金", "长风", "旷", "丰", "暖阳", "自由", "奔马", "麦", "青", "广", "望", "晴", "牧", "野", "辽", "坦", "和", "宁", "安", "乐"],
        ["Swamp"] = ["暗", "雾", "黑", "幽", "沉", "枯", "迷", "朽", "腐", "毒", "瘴", "泥", "苦", "荒", "死", "灰", "冷", "湿", "暮", "影"]
    };

    private static readonly Dictionary<string, string[]> CitySuffixZH = new()
    {
        ["River"] = ["城", "渡", "津", "桥", "港", "流", "河堡", "泉", "湾", "潭", "滩", "汇", "源", "口", "洲", "浦", "溪", "瀑", "池", "涧"],
        ["Mountain"] = ["峰", "岩城", "关", "巢", "顶", "石堡", "峰城", "崖", "脊", "谷", "隘", "壁", "嶂", "岭", "坳", "垭", "峡", "台", "塬", "坡"],
        ["Forest"] = ["林城", "月", "叶", "木堡", "径", "荫堡", "谷", "林", "园", "丘", "坪", "野", "原", "泽", "溪", "潭", "涧", "坊", "寨", "庄"],
        ["Coast"] = ["门", "帆城", "汐", "湾", "望城", "浪堡", "风城", "塔城", "岸", "崖", "角", "礁", "屿", "洲", "滩", "港", "埠", "渡", "津", "浦"],
        ["Plain"] = ["穗", "风城", "野城", "饶", "阳城", "原", "马城", "田堡", "丘", "坡", "垣", "邑", "镇", "集", "墟", "坊", "庄", "屯", "营", "堡"],
        ["Swamp"] = ["沼", "沉城", "水城", "潭", "寂", "骨城", "途", "桥", "渊", "泽", "洼", "塘", "淖", "泊", "湖", "荡", "溏", "坑", "窟", "穴"]
    };

    private static readonly Dictionary<string, string[]> CityPrefixEN = new()
    {
        ["River"] = ["Water", "Silver", "Green", "Long", "Mist", "Clear", "Twin", "Torrent", "Blue", "Emerald", "White", "Jade", "Still", "Swift", "Deep", "Crystal", "Ice", "Warm", "Gold", "Moon"],
        ["Mountain"] = ["Iron", "Cold", "Thunder", "Eagle", "Cloud", "Shatter", "Storm", "Grey", "Black", "Red", "Frost", "Dragon", "Giant", "Stone", "Steel", "Dark", "Blaze", "High", "Lone", "Pale"],
        ["Forest"] = ["Shadow", "Silver", "Green", "Old", "Deer", "Verdant", "Maple", "Deep", "Dark", "Fey", "Elven", "Oak", "Pine", "Willow", "Vine", "Moss", "Fern", "Bloom", "Shroom", "Thorn"],
        ["Coast"] = ["Sea", "White", "Tide", "Coral", "Far", "Break", "Salt", "Beacon", "Azure", "Shell", "Whale", "Anchor", "Sail", "Wave", "Sand", "Pearl", "Gull", "Reef", "Star", "Wind"],
        ["Plain"] = ["Gold", "Long", "Wild", "Rich", "Sun", "Free", "Swift", "Wheat", "Green", "Wide", "Far", "Bright", "Herd", "Open", "Vast", "Flat", "Peace", "Calm", "Safe", "Joy"],
        ["Swamp"] = ["Dark", "Mist", "Black", "Gloom", "Still", "Dead", "Lost", "Rot", "Blight", "Venom", "Murk", "Mud", "Bitter", "Waste", "Death", "Ash", "Cold", "Damp", "Dusk", "Shade"]
    };

    private static readonly Dictionary<string, string[]> CitySuffixEN = new()
    {
        ["River"] = ["deep", "ford", "brook", "bridge", "port", "flow", "keep", "spring", "bay", "pool", "shore", "meet", "well", "mouth", "isle", "haven", "creek", "falls", "pond", "dale"],
        ["Mountain"] = ["peak", "rock", "gate", "crest", "top", "hold", "spire", "cliff", "ridge", "vale", "pass", "wall", "bluff", "mount", "gorge", "gap", "height", "ledge", "mesa", "slope"],
        ["Forest"] = ["wood", "moon", "leaf", "keep", "path", "grove", "hollow", "dell", "glade", "hill", "mead", "wild", "field", "marsh", "brook", "pool", "glen", "court", "stead", "manor"],
        ["Coast"] = ["gate", "sail", "crest", "bay", "watch", "break", "wind", "tower", "shore", "cliff", "point", "reef", "isle", "strand", "beach", "port", "wharf", "dock", "ferry", "haven"],
        ["Plain"] = ["field", "wind", "moor", "stead", "hearth", "hold", "mere", "keep", "hill", "slope", "wall", "town", "burg", "fair", "market", "cross", "farm", "camp", "fort", "holm"],
        ["Swamp"] = ["fen", "hollow", "water", "pool", "mere", "marsh", "moor", "bridge", "deep", "bog", "mire", "slough", "sink", "pit", "lake", "wash", "seep", "hole", "den", "lair"]
    };

    // ========================================
    // 村庄名 — 更朴素的组合
    // ========================================

    private static readonly Dictionary<string, string[]> VillagePrefixZH = new()
    {
        ["River"] = ["柳", "浅", "石", "清", "渔", "芦", "溪", "水", "蓝", "碧", "白", "小", "老", "新", "上", "下", "东", "西", "南", "北"],
        ["Mountain"] = ["山", "石", "矿", "岩", "羊", "采", "风", "隘", "铁", "铜", "锡", "煤", "盐", "硫", "金", "银", "玉", "翠", "青", "赤"],
        ["Forest"] = ["林", "蘑菇", "猎", "伐", "松", "橡", "鸟", "苔", "蕨", "花", "蜂", "鹿", "兔", "狐", "熊", "獾", "蛇", "蜘蛛", "萤", "蝶"],
        ["Coast"] = ["渔", "贝", "海", "晒", "船", "蛤", "潮", "礁", "蟹", "虾", "鱼", "盐", "珠", "螺", "藻", "浪", "沙", "岬", "湾", "角"],
        ["Plain"] = ["麦", "风", "牧", "谷", "篱", "井", "草", "犁", "牛", "羊", "马", "鸡", "鹅", "磨", "仓", "车", "桥", "路", "泉", "树"],
        ["Swamp"] = ["浮", "蛙", "泥", "苇", "水", "蚊", "朽", "菌", "蛇", "蜥", "蟾", "蝇", "蚂", "蜗", "蛭", "萍", "莲", "荷", "藕", "芦"]
    };

    private static readonly Dictionary<string, string[]> VillageSuffixZH = new()
    {
        ["River"] = ["溪", "滩", "桥村", "泉", "歌", "苇湾", "口", "磨坊", "渡", "塘", "坝", "堰", "闸", "埠", "浦", "汀", "洲", "畔", "岸", "头"],
        ["Mountain"] = ["脚村", "屋", "坑镇", "洞", "肠道", "石场", "口", "口村", "窝", "坪", "台", "崖", "坡", "岗", "梁", "峁", "塬", "沟", "壑", "坎"],
        ["Forest"] = ["间", "谷", "户屋", "木场", "针", "果村", "巢", "藓地", "坪", "坡", "湾", "沟", "岔", "弯", "角", "嘴", "尾", "根", "梢", "冠"],
        ["Coast"] = ["村", "壳湾", "草滩", "网", "坞", "蜊岬", "间", "石村", "浦", "港", "埠", "渡", "津", "滩", "岸", "角", "嘴", "头", "尾", "湾"],
        ["Plain"] = ["垛", "车镇", "羊地", "仓", "笆村", "台", "垛", "田", "庄", "屯", "营", "集", "墟", "坊", "铺", "店", "驿", "亭", "坡", "岗"],
        ["Swamp"] = ["木", "鸣", "屋", "荡", "洼村", "沼", "桥", "落", "塘", "泊", "淖", "洼", "坑", "窝", "潭", "渊", "湖", "池", "沟", "溏"]
    };

    private static readonly Dictionary<string, string[]> VillagePrefixEN = new()
    {
        ["River"] = ["Willow", "Shallow", "Stone", "Clear", "Fish", "Reed", "Creek", "Mill", "Blue", "Green", "White", "Little", "Old", "New", "Upper", "Lower", "East", "West", "South", "North"],
        ["Mountain"] = ["Hill", "Stone", "Mine", "Rock", "Goat", "Quarry", "Wind", "Narrow", "Iron", "Copper", "Tin", "Coal", "Salt", "Sulfur", "Gold", "Silver", "Jade", "Emerald", "Blue", "Red"],
        ["Forest"] = ["Glen", "Shroom", "Hunter", "Logger", "Pine", "Oak", "Bird", "Moss", "Fern", "Bloom", "Bee", "Deer", "Hare", "Fox", "Bear", "Badger", "Snake", "Spider", "Firefly", "Moth"],
        ["Coast"] = ["Fisher", "Shell", "Sea", "Net", "Dock", "Clam", "Tide", "Reef", "Crab", "Shrimp", "Fish", "Salt", "Pearl", "Snail", "Kelp", "Wave", "Sand", "Cape", "Bay", "Point"],
        ["Plain"] = ["Wheat", "Wind", "Herd", "Barn", "Hedge", "Well", "Hay", "Plow", "Ox", "Sheep", "Horse", "Hen", "Goose", "Mill", "Grain", "Cart", "Bridge", "Road", "Spring", "Tree"],
        ["Swamp"] = ["Drift", "Frog", "Mud", "Reed", "Bog", "Midge", "Rot", "Spore", "Snake", "Newt", "Toad", "Fly", "Ant", "Snail", "Leech", "Lily", "Lotus", "Pad", "Root", "Rush"]
    };

    private static readonly Dictionary<string, string[]> VillageSuffixEN = new()
    {
        ["River"] = ["brook", "ford", "bridge", "spring", "song", "bank", "mouth", "mill", "ferry", "pond", "dam", "weir", "lock", "wharf", "haven", "bar", "isle", "side", "shore", "end"],
        ["Mountain"] = ["foot", "house", "town", "cave", "path", "field", "gap", "pass", "pit", "flat", "ledge", "cliff", "slope", "ridge", "crag", "bluff", "mesa", "gulch", "gorge", "notch"],
        ["Forest"] = ["glade", "hollow", "lodge", "camp", "needle", "vale", "nest", "bed", "clearing", "slope", "bend", "fork", "turn", "corner", "tip", "tail", "root", "top", "crown", "shade"],
        ["Coast"] = ["ton", "cove", "grass", "dry", "side", "shore", "wash", "rock", "haven", "port", "wharf", "dock", "ferry", "beach", "bank", "point", "head", "end", "tail", "bay"],
        ["Plain"] = ["stack", "mill", "stead", "barn", "row", "side", "bale", "field", "farm", "camp", "fort", "fair", "market", "cross", "post", "inn", "rest", "halt", "slope", "hill"],
        ["Swamp"] = ["wood", "mere", "house", "moor", "wallow", "fen", "bridge", "croft", "pool", "lake", "mire", "bog", "pit", "hole", "deep", "sink", "wash", "seep", "drain", "slough"]
    };

    // ========================================
    // 要塞名 — 前缀 + 后缀组合（~100 种）
    // ========================================

    private static readonly string[] FortressPrefixZH = [
        "铁", "黑", "狮", "寒", "烈", "雷", "暗", "碎", "血", "钢",
        "龙", "鹰", "裂", "暴", "白", "黑曜", "赤", "霜", "战", "孤",
        "金", "银", "铜", "玄", "苍", "烽", "怒", "狂", "圣", "魔"
    ];

    private static readonly string[] FortressSuffixZH = [
        "壁要塞", "鸦堡", "鹫关", "霜塔", "焰堡", "霆关", "影堡", "盾壁垒", "誓堡", "牙塔",
        "脊要塞", "眼塔", "地关", "风堡", "骨壁垒", "石堡", "铁关", "牙堡", "锤塔", "狼堡",
        "冠堡", "月塔", "日关", "星堡", "云壁垒", "火塔", "水关", "风堡", "雷塔", "电关"
    ];

    private static readonly string[] FortressPrefixEN = [
        "Iron", "Black", "Lion", "Frost", "Blaze", "Thunder", "Shadow", "Shield", "Blood", "Steel",
        "Dragon", "Hawk", "Earth", "Storm", "Bone", "Obsidian", "Red", "Ice", "War", "Lone",
        "Gold", "Silver", "Copper", "Dark", "Pale", "Beacon", "Wrath", "Fury", "Holy", "Fell"
    ];

    private static readonly string[] FortressSuffixEN = [
        "wall Keep", "raven Hold", "guard Gate", "frost Tower", "watch Bastion", "hold Keep", "keep Hold", "break Gate", "sworn Tower", "fang Bastion",
        "spine Keep", "eye Tower", "rend Gate", "wind Hold", "march Bastion", "stone Keep", "forge Tower", "tusk Gate", "hammer Hold", "wolf Bastion",
        "crown Keep", "moon Tower", "sun Gate", "star Hold", "cloud Bastion", "fire Tower", "water Gate", "wind Hold", "bolt Tower", "spark Gate"
    ];

    // ========================================
    // 修道院/圣所名
    // ========================================

    private static readonly string[] MonasteryPrefixZH = [
        "晨", "静", "圣", "白", "银", "古", "星", "净", "月", "金",
        "清", "圣", "慈", "悲", "智", "光", "明", "暗", "幽", "玄"
    ];

    private static readonly string[] MonasterySuffixZH = [
        "光修道院", "默圣所", "泉祭坛", "鸽隐修处", "钟神殿",
        "卷修道院", "辰圣所", "土祭坛", "影隐修处", "叶神殿",
        "风修道院", "火圣所", "水祭坛", "石隐修处", "木神殿",
        "心修道院", "魂圣所", "灵祭坛", "梦隐修处", "愿神殿"
    ];

    private static readonly string[] MonasteryPrefixEN = [
        "Dawn", "Silent", "Holy", "White", "Silver", "Ancient", "Star", "Pure", "Moon", "Golden",
        "Clear", "Sacred", "Mercy", "Sorrow", "Wisdom", "Light", "Bright", "Shadow", "Quiet", "Mystic"
    ];

    private static readonly string[] MonasterySuffixEN = [
        "light Abbey", "rest Sanctuary", "fount Shrine", "dove Hermitage", "bell Temple",
        "scroll Abbey", "fall Sanctuary", "heart Shrine", "shade Hermitage", "leaf Temple",
        "wind Abbey", "fire Sanctuary", "water Shrine", "stone Hermitage", "wood Temple",
        "soul Abbey", "spirit Sanctuary", "dream Shrine", "wish Hermitage", "hope Temple"
    ];

    // ========================================
    // 酒馆名 — 形容词 + 名词 + 后缀（~800 种组合）
    // ========================================

    private static readonly string[] TavernAdjZH = [
        "金", "银", "铜", "铁", "醉", "老", "破", "独", "飞", "锈",
        "怒", "狂", "落", "断", "无头", "黑", "红", "白", "蓝", "绿",
        "深", "月", "星", "火", "冰", "风", "雷", "暗", "光", "圣",
        "野", "孤", "疯", "瞎", "跛", "胖", "瘦", "高", "矮", "秃"
    ];

    private static readonly string[] TavernNounZH = [
        "杯", "鹿", "盾", "眼", "马", "龙", "钉", "币", "熊", "欢",
        "日", "明", "剑", "骑士", "猫", "猪", "渊", "光", "狐", "锚",
        "鹰", "狼", "蛇", "鸦", "鼠", "牛", "羊", "鸡", "鱼", "蟹",
        "桶", "壶", "瓶", "碗", "勺", "叉", "刀", "斧", "锤", "弓"
    ];

    private static readonly string[] TavernSuffixesZH = ["酒馆", "客栈", "驿站", "酒肆", "旅店", "小酒馆", "酒窖", "食堂"];

    private static readonly string[] TavernAdjEN = [
        "Golden", "Silver", "Copper", "Iron", "Drunken", "Old", "Broken", "One-Eyed", "Flying", "Rusty",
        "Angry", "Mad", "Setting", "Broken", "Headless", "Black", "Red", "White", "Blue", "Green",
        "Deep", "Moon", "Star", "Fire", "Ice", "Wind", "Thunder", "Dark", "Bright", "Holy",
        "Wild", "Lone", "Crazy", "Blind", "Lame", "Fat", "Thin", "Tall", "Short", "Bald"
    ];

    private static readonly string[] TavernNounEN = [
        "Cup", "Stag", "Shield", "Eye", "Mare", "Dragon", "Nail", "Coin", "Bear", "Revel",
        "Sun", "Dawn", "Blade", "Knight", "Cat", "Boar", "Abyss", "Light", "Fox", "Anchor",
        "Eagle", "Wolf", "Serpent", "Raven", "Rat", "Bull", "Ram", "Rooster", "Fish", "Crab",
        "Barrel", "Flagon", "Bottle", "Bowl", "Spoon", "Fork", "Dagger", "Axe", "Hammer", "Bow"
    ];

    private static readonly string[] TavernSuffixesEN = ["Tavern", "Inn", "Rest", "Alehouse", "Lodge", "Pub", "Cellar", "Hall"];

    // ========================================
    // 生成入口
    // ========================================

    public static string GeneratePOIName(POIType type, string terrainKey = "")
    {
        bool isZH = NameGenerator.GetCurrentLanguage() == "zh";

        string terrain = terrainKey;
        if (string.IsNullOrEmpty(terrain))
        {
            var allKeys = new List<string>(CityPrefixZH.Keys);
            terrain = allKeys[(int)(GD.Randi() % (uint)allKeys.Count)];
        }
        else if (!CityPrefixZH.ContainsKey(terrain))
        {
            terrain = "Plain";
        }

        return type switch
        {
            POIType.City => GenerateCityName(terrain, isZH),
            POIType.Village => GenerateVillageName(terrain, isZH),
            POIType.Fortress => GenerateFortressName(isZH),
            POIType.Monastery => GenerateMonasteryName(isZH),
            POIType.Tavern => GenerateTavernName(isZH),
            _ => GenerateCityName(terrain, isZH),
        };
    }

    private static string GenerateCityName(string terrain, bool isZH)
    {
        var prefixes = isZH ? CityPrefixZH[terrain] : CityPrefixEN[terrain];
        var suffixes = isZH ? CitySuffixZH[terrain] : CitySuffixEN[terrain];
        string prefix = prefixes[GD.Randi() % (uint)prefixes.Length];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
        return isZH ? prefix + suffix : prefix + suffix;
    }

    private static string GenerateVillageName(string terrain, bool isZH)
    {
        var prefixes = isZH ? VillagePrefixZH[terrain] : VillagePrefixEN[terrain];
        var suffixes = isZH ? VillageSuffixZH[terrain] : VillageSuffixEN[terrain];
        string prefix = prefixes[GD.Randi() % (uint)prefixes.Length];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
        return isZH ? prefix + suffix : prefix + suffix;
    }

    private static string GenerateFortressName(bool isZH)
    {
        var prefixes = isZH ? FortressPrefixZH : FortressPrefixEN;
        var suffixes = isZH ? FortressSuffixZH : FortressSuffixEN;
        string prefix = prefixes[GD.Randi() % (uint)prefixes.Length];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
        return isZH ? prefix + suffix : prefix + suffix;
    }

    private static string GenerateMonasteryName(bool isZH)
    {
        var prefixes = isZH ? MonasteryPrefixZH : MonasteryPrefixEN;
        var suffixes = isZH ? MonasterySuffixZH : MonasterySuffixEN;
        string prefix = prefixes[GD.Randi() % (uint)prefixes.Length];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
        return isZH ? prefix + suffix : prefix + suffix;
    }

    private static string GenerateTavernName(bool isZH)
    {
        var adjs = isZH ? TavernAdjZH : TavernAdjEN;
        var nouns = isZH ? TavernNounZH : TavernNounEN;
        var suffixes = isZH ? TavernSuffixesZH : TavernSuffixesEN;
        string adj = adjs[GD.Randi() % (uint)adjs.Length];
        string noun = nouns[GD.Randi() % (uint)nouns.Length];
        string suffix = suffixes[GD.Randi() % (uint)suffixes.Length];
        return isZH ? adj + noun + suffix : "The " + adj + " " + noun + " " + suffix;
    }
}
