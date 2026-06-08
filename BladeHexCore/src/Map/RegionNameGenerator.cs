// RegionNameGenerator.cs
// 程序化区域命名生成器 — 低魔西幻风格 + 中文音译
// 设计参考：WarTales、Battle Brothers、Mount & Blade
// 命名逻辑：[形容词/名词] + [地理后缀]，朴素写实
using System;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 区域命名生成器 — 基于生态类型生成低魔西幻风格区域名称
/// 同时生成英文名和中文音译名
/// </summary>
public class RegionNameGenerator
{
    private readonly Random _rng;

    public RegionNameGenerator(int seed)
    {
        _rng = new Random(seed);
    }

    /// <summary>
    /// 为生态区生成名称（英文 + 中文音译）
    /// </summary>
    public (string english, string chinese) GenerateName(BiomeZone zone, int zoneIndex = 0)
    {
        return zone.DominantBiome switch
        {
            BiomeType.Plains => GeneratePlainsName(),
            BiomeType.Forest => GenerateForestName(),
            BiomeType.Mountain => GenerateMountainName(),
            BiomeType.Wasteland => GenerateWastelandName(),
            BiomeType.Swamp => GenerateSwampName(),
            BiomeType.Tundra => GenerateTundraName(),
            BiomeType.Jungle => GenerateJungleName(),
            BiomeType.Coastal => GenerateCoastalName(),
            _ => GeneratePlainsName(),
        };
    }

    // ========================================
    // 平原系
    // ========================================

    private (string, string) GeneratePlainsName()
    {
        return GenerateBiomeName(_plainsAdj, _plainsNoun, _plainsSuffix);
    }

    private static readonly (string en, string cn)[] _plainsAdj = [
        ("Golden", "金"), ("Sun", "日"), ("Green", "绿"), ("Fair", "美"),
        ("Wide", "阔"), ("Broad", "广"), ("Open", "旷"), ("Amber", "琥珀"),
        ("Wheat", "麦"), ("Barley", "麦"), ("Fallow", "休耕"), ("Mild", "温"),
        ("Calm", "宁"), ("Still", "静"), ("Dew", "露"), ("Mist", "雾"),
        ("Dawn", "晨"), ("Dusk", "暮"), ("Hazel", "榛"), ("Ash", "灰"),
        ("Elder", "长"), ("Mead", "蜜"), ("Bee", "蜂"), ("Lamb", "羔"),
        ("Plough", "犁"), ("Sheaf", "束"), ("Loam", "壤"), ("Clay", "陶"),
        ("Hay", "干草"), ("Rye", "黑麦"), ("Oat", "燕麦"), ("Clover", "苜蓿"),
        ("Thistle", "蓟"), ("Daisy", "雏菊"), ("Lily", "百合"), ("Iris", "鸢尾"),
        ("Myrtle", "桃金娘"), ("Sage", "鼠尾草"), ("Mint", "薄荷"), ("Basil", "罗勒"),
    ];

    private static readonly (string en, string cn)[] _plainsNoun = [
        ("Ox", "牛"), ("Hart", "雄鹿"), ("Stag", "牡鹿"), ("Hare", "野兔"),
        ("Lark", "云雀"), ("Wren", "鹪鹩"), ("Crow", "鸦"), ("Shepherd", "牧羊"),
        ("Plough", "犁"), ("Mill", "磨坊"), ("Stone", "石"), ("Thorn", "荆棘"),
        ("Briar", "蔷薇"), ("Hawk", "鹰"), ("Kite", "鸢"), ("Rook", "白嘴鸦"),
        ("Finch", "雀"), ("Sparrow", "麻雀"), ("Quail", "鹌鹑"), ("Dove", "鸽"),
        ("Swan", "天鹅"), ("Crane", "鹤"), ("Heron", "鹭"), ("Stork", "鹳"),
        ("Lamb", "羔羊"), ("Ewe", "母羊"), ("Ram", "公羊"), ("Bull", "公牛"),
        ("Cow", "母牛"), ("Calf", "牛犊"), ("Foal", "马驹"), ("Colt", "小公马"),
        ("Mare", "母马"), ("Stallion", "种马"), ("Donkey", "驴"), ("Mule", "骡"),
        ("Goose", "鹅"), ("Duck", "鸭"), ("Hen", "母鸡"), ("Rooster", "公鸡"),
    ];

    private static readonly (string en, string cn)[] _plainsSuffix = [
        ("field", "菲尔德"), ("meadow", "梅多"), ("vale", "维尔"), ("dale", "戴尔"),
        ("plain", "普莱恩"), ("moor", "穆尔"), ("heath", "希思"), ("down", "唐"),
        ("flat", "弗拉特"), ("reach", "里奇"), ("ward", "沃德"), ("stead", "斯特德"),
        ("ton", "顿"), ("ham", "汉姆"), ("wick", "维克"), ("ford", "福德"),
        ("bridge", "布里奇"), ("cross", "克罗斯"), ("gate", "盖特"), ("mark", "马克"),
        ("green", "格林"), ("well", "韦尔"), ("spring", "斯普林"), ("brook", "布鲁克"),
        ("run", "伦"), ("creek", "克里克"), ("branch", "布兰奇"), ("fork", "福克"),
        ("hollow", "霍洛"), ("glen", "格伦"), ("knoll", "诺尔"), ("rise", "赖斯"),
    ];

    // ========================================
    // 森林系
    // ========================================

    private (string, string) GenerateForestName()
    {
        return GenerateBiomeName(_forestAdj, _forestNoun, _forestSuffix);
    }

    private static readonly (string en, string cn)[] _forestAdj = [
        ("Deep", "深"), ("Dark", "暗"), ("Old", "古"), ("Grey", "灰"),
        ("Black", "黑"), ("Green", "绿"), ("Silent", "寂"), ("Thick", "密"),
        ("Dense", "浓"), ("Wild", "野"), ("Grim", "冷"), ("Dusk", "暮"),
        ("Shadow", "影"), ("Moss", "苔"), ("Fern", "蕨"), ("Bramble", "荆"),
        ("Thorn", "刺"), ("Raven", "鸦"), ("Wolf", "狼"), ("Bear", "熊"),
        ("Elk", "麋"), ("Hawk", "鹰"), ("Owl", "枭"), ("Fox", "狐"),
        ("Boar", "野猪"), ("Stag", "鹿"), ("Snake", "蛇"), ("Crow", "鸦"),
        ("Beech", "山毛榉"), ("Larch", "落叶松"), ("Cedar", "雪松"), ("Spruce", "云杉"),
        ("Willow", "柳"), ("Maple", "枫"), ("Chestnut", "栗"), ("Walnut", "胡桃"),
        ("Holly", "冬青"), ("Ivy", "常春藤"), ("Laurel", "月桂"), ("Myrtle", "桃金娘"),
    ];

    private static readonly (string en, string cn)[] _forestNoun = [
        ("Oak", "橡"), ("Ash", "梣"), ("Elm", "榆"), ("Yew", "紫杉"),
        ("Birch", "桦"), ("Pine", "松"), ("Hazel", "榛"), ("Holly", "冬青"),
        ("Alder", "桤"), ("Aspen", "白杨"), ("Linden", "椴"), ("Rowan", "花楸"),
        ("Willow", "柳"), ("Boar", "野猪"), ("Stag", "鹿"), ("Wolf", "狼"),
        ("Fox", "狐"), ("Owl", "枭"), ("Crow", "鸦"), ("Raven", "渡鸦"),
        ("Hawk", "鹰"), ("Eagle", "鹰"), ("Falcon", "隼"), ("Kestrel", "红隼"),
        ("Deer", "鹿"), ("Roe", "狍"), ("Hare", "野兔"), ("Badger", "獾"),
        ("Otter", "水獭"), ("Marten", "貂"), ("Weasel", "鼬"), ("Mink", "貂"),
        ("Squirrel", "松鼠"), ("Mouse", "鼠"), ("Vole", "田鼠"), ("Mole", "鼹鼠"),
        ("Bat", "蝙蝠"), ("Toad", "蟾蜍"), ("Frog", "蛙"), ("Newt", "蝾螈"),
    ];

    private static readonly (string en, string cn)[] _forestSuffix = [
        ("wood", "伍德"), ("forest", "福雷斯特"), ("grove", "格罗夫"), ("thicket", "西克特"),
        ("copse", "科普斯"), ("shade", "谢德"), ("hollow", "霍洛"), ("glade", "格莱德"),
        ("brake", "布雷克"), ("weald", "威尔德"), ("wald", "瓦尔德"), ("hurst", "赫斯特"),
        ("leigh", "利"), ("ley", "利"), ("den", "登"), ("dell", "德尔"),
        ("end", "恩德"), ("edge", "埃奇"), ("march", "马奇"), ("ride", "赖德"),
        ("path", "帕斯"), ("trail", "特雷尔"), ("track", "特拉克"), ("way", "韦"),
        ("gate", "盖特"), ("gap", "加普"), ("pass", "帕斯"), ("cross", "克罗斯"),
        ("bridge", "布里奇"), ("ford", "福德"), ("pool", "普尔"), ("spring", "斯普林"),
    ];

    // ========================================
    // 山地系
    // ========================================

    private (string, string) GenerateMountainName()
    {
        return GenerateBiomeName(_mountainAdj, _mountainNoun, _mountainSuffix);
    }

    private static readonly (string en, string cn)[] _mountainAdj = [
        ("Iron", "铁"), ("Stone", "石"), ("Grey", "灰"), ("Black", "黑"),
        ("Red", "红"), ("White", "白"), ("Rugged", "崎"), ("Steep", "陡"),
        ("Barren", "荒"), ("Bleak", "凄"), ("Cold", "寒"), ("High", "高"),
        ("Low", "低"), ("Far", "远"), ("Near", "近"), ("Old", "古"),
        ("New", "新"), ("Grim", "冷"), ("Sharp", "锐"), ("Jagged", "锯"),
        ("Crag", "岩"), ("Rock", "磐"), ("Boulder", "巨"), ("Granite", "花岗"),
        ("Slate", "板岩"), ("Flint", "燧"), ("Ore", "矿"), ("Coal", "煤"),
        ("Copper", "铜"), ("Bronze", "青铜"), ("Steel", "钢"), ("Silver", "银"),
        ("Gold", "金"), ("Crystal", "晶"), ("Quartz", "石英"), ("Marble", "大理石"),
        ("Basalt", "玄武岩"), ("Obsidian", "黑曜石"), ("Jade", "玉"), ("Amber", "琥珀"),
    ];

    private static readonly (string en, string cn)[] _mountainNoun = [
        ("Raven", "渡鸦"), ("Eagle", "鹰"), ("Hawk", "隼"), ("Wolf", "狼"),
        ("Bear", "熊"), ("Goat", "山羊"), ("Ram", "公羊"), ("Ore", "矿"),
        ("Gem", "宝石"), ("Coal", "煤"), ("Flint", "燧石"), ("Slate", "板岩"),
        ("Granite", "花岗岩"), ("Anvil", "铁砧"), ("Hammer", "锤"), ("Axe", "斧"),
        ("Sword", "剑"), ("Shield", "盾"), ("Crown", "王冠"), ("Throne", "王座"),
        ("Dagger", "匕首"), ("Spear", "矛"), ("Arrow", "箭"), ("Bow", "弓"),
        ("Helm", "头盔"), ("Mail", "锁甲"), ("Plate", "板甲"), ("Gauntlet", "铁手套"),
        ("Pick", "镐"), ("Shovel", "铲"), ("Chisel", "凿"), ("Tongs", "钳"),
        ("Forge", "锻炉"), ("Anvil", "铁砧"), ("Bellows", "风箱"), ("Crucible", "坩埚"),
        ("Lode", "矿脉"), ("Nugget", "金块"), ("Vein", "矿脉"), ("Seam", "矿层"),
    ];

    private static readonly (string en, string cn)[] _mountainSuffix = [
        ("peak", "皮克"), ("mount", "芒特"), ("ridge", "里奇"), ("crag", "克拉格"),
        ("bluff", "布拉夫"), ("cliff", "克利夫"), ("tor", "托尔"), ("fell", "费尔"),
        ("pike", "派克"), ("knoll", "诺尔"), ("hill", "希尔"), ("height", "海特"),
        ("pass", "帕斯"), ("gap", "加普"), ("notch", "诺奇"), ("saddle", "萨德尔"),
        ("spine", "斯派恩"), ("crest", "克雷斯特"), ("summit", "萨米特"), ("crown", "克朗"),
        ("stone", "斯通"), ("rock", "洛克"), ("wall", "沃尔"), ("buttress", "巴特里斯"),
        ("tower", "塔"), ("spire", "尖顶"), ("pinnacle", "尖塔"), ("dome", "穹顶"),
        ("basin", "盆地"), ("bowl", "碗"), ("sink", "沉"), ("sinkhole", "天坑"),
        ("cavern", "洞穴"), ("cave", "洞"), ("grotto", "洞窟"), ("abyss", "深渊"),
    ];

    // ========================================
    // 荒原系
    // ========================================

    private (string, string) GenerateWastelandName()
    {
        return GenerateBiomeName(_wastelandAdj, _wastelandNoun, _wastelandSuffix);
    }

    private static readonly (string en, string cn)[] _wastelandAdj = [
        ("Ash", "灰"), ("Dust", "尘"), ("Sand", "沙"), ("Scorch", "焦"),
        ("Blight", "疫"), ("Bleak", "凄"), ("Dead", "死"), ("Dry", "旱"),
        ("Barren", "荒"), ("Burnt", "焚"), ("Ruin", "废"), ("Waste", "荒"),
        ("Void", "虚"), ("Grim", "冷"), ("Pale", "苍"), ("Faded", "褪"),
        ("Rusted", "锈"), ("Broken", "碎"), ("Lost", "失"), ("Forsaken", "弃"),
        ("Cinder", "烬"), ("Ember", "余烬"), ("Smoke", "烟"), ("Soot", "煤烟"),
        ("Bitter", "苦"), ("Harsh", "涩"), ("Rough", "糙"), ("Sharp", "锐"),
        ("Hard", "坚"), ("Stark", "荒"), ("Sterile", "贫"), ("Bare", "裸"),
        ("Exposed", "露"), ("Open", "敞"), ("Flat", "平"), ("Endless", "无尽"),
        ("Vast", "浩瀚"), ("Empty", "空"), ("Hollow", "空洞"), ("Void", "虚空"),
    ];

    private static readonly (string en, string cn)[] _wastelandNoun = [
        ("Skull", "骷髅"), ("Bone", "骨"), ("Vulture", "秃鹫"), ("Serpent", "蛇"),
        ("Scorpion", "蝎"), ("Jackal", "豺"), ("Hyena", "鬣狗"), ("Thorn", "荆棘"),
        ("Briar", "蔷薇"), ("Nettle", "荨麻"), ("Rubble", "瓦砾"), ("Shard", "碎片"),
        ("Flint", "燧石"), ("Clay", "粘土"), ("Dust", "尘"), ("Ash", "灰"),
        ("Cinder", "烬"), ("Ember", "余烬"), ("Smoke", "烟"), ("Rust", "锈"),
        ("Ore", "矿"), ("Slag", "矿渣"), ("Scrap", "废铁"), ("Debris", "残骸"),
        ("Wreck", "残骸"), ("Ruin", "废墟"), ("Remnant", "遗迹"), ("Relic", "遗物"),
        ("Tomb", "墓"), ("Grave", "坟"), ("Crypt", "地窖"), ("Barrow", "古冢"),
        ("Cairn", "石冢"), ("Monument", "纪念碑"), ("Pillar", "柱"), ("Obelisk", "方尖碑"),
        ("Spire", "尖塔"), ("Tower", "塔"), ("Wall", "墙"), ("Gate", "门"),
    ];

    private static readonly (string en, string cn)[] _wastelandSuffix = [
        ("waste", "韦斯特"), ("flat", "弗拉特"), ("barren", "巴伦"), ("desert", "德泽特"),
        ("expanse", "埃克斯潘斯"), ("reach", "里奇"), ("moor", "穆尔"), ("heath", "希思"),
        ("steppe", "斯泰普"), ("badland", "巴德兰"), ("scour", "斯考尔"), ("scar", "斯卡"),
        ("rift", "里夫特"), ("gap", "加普"), ("march", "马奇"), ("ward", "沃德"),
        ("bound", "邦德"), ("edge", "埃奇"), ("rim", "里姆"), ("brink", "布林克"),
        ("verge", "弗奇"), ("end", "恩德"), ("plain", "普莱恩"), ("stretch", "斯特雷奇"),
        ("panse", "潘斯"), ("range", "兰奇"), ("tract", "特拉克特"), ("sweep", "斯威普"),
        ("span", "斯潘"), ("breadth", "布雷德思"), ("width", "威德思"),
        ("void", "沃伊德"), ("land", "兰德"),
    ];

    // ========================================
    // 沼泽系
    // ========================================

    private (string, string) GenerateSwampName()
    {
        return GenerateBiomeName(_swampAdj, _swampNoun, _swampSuffix);
    }

    private static readonly (string en, string cn)[] _swampAdj = [
        ("Black", "黑"), ("Grey", "灰"), ("Mud", "泥"), ("Mire", "沼"),
        ("Fen", "沼"), ("Bog", "沼"), ("Rot", "腐"), ("Moss", "苔"),
        ("Slime", "粘"), ("Mould", "霉"), ("Foul", "臭"), ("Stale", "腐"),
        ("Still", "静"), ("Dead", "死"), ("Dusk", "暮"), ("Gloom", "暗"),
        ("Mist", "雾"), ("Fog", "霾"), ("Haze", "霭"), ("Damp", "湿"),
        ("Wet", "潮"), ("Moist", "润"), ("Soggy", "浸"), ("Sodden", "湿透"),
        ("Murky", "浑"), ("Cloudy", "浊"), ("Turbid", "浊"), ("Stagnant", "滞"),
        ("Putrid", "腐臭"), ("Fetid", "恶臭"), ("Rank", "腥"), ("Noxious", "毒"),
        ("Venomous", "毒"), ("Poison", "毒"), ("Toxic", "毒"), ("Miasma", "瘴"),
        ("Pestilent", "疫"), ("Plague", "瘟"), ("Blight", "疫"), ("Curse", "咒"),
    ];

    private static readonly (string en, string cn)[] _swampNoun = [
        ("Frog", "蛙"), ("Toad", "蟾"), ("Eel", "鳗"), ("Snake", "蛇"),
        ("Crow", "鸦"), ("Heron", "鹭"), ("Snipe", "鹬"), ("Reed", "芦苇"),
        ("Rush", "灯芯草"), ("Sedge", "莎草"), ("Willow", "柳"), ("Alder", "桤"),
        ("Root", "根"), ("Leech", "蛭"), ("Slug", "蛞蝓"), ("Moss", "苔藓"),
        ("Lichen", "地衣"), ("Mould", "霉"), ("Fungus", "真菌"), ("Mushroom", "蘑菇"),
        ("Newt", "蝾螈"), ("Salamander", "蝾螈"), ("Turtle", "龟"), ("Crab", "蟹"),
        ("Crayfish", "小龙虾"), ("Snail", "蜗牛"), ("Worm", "蠕虫"), ("Beetle", "甲虫"),
        ("Mosquito", "蚊"), ("Gnat", "蠓"), ("Fly", "蝇"), ("Spider", "蜘蛛"),
        ("Bat", "蝙蝠"), ("Owl", "枭"), ("Raven", "渡鸦"), ("Crow", "乌鸦"),
        ("Viper", "蝰蛇"), ("Adder", "蝰蛇"), ("Asp", "角蝰"), ("Mamba", "曼巴"),
    ];

    private static readonly (string en, string cn)[] _swampSuffix = [
        ("marsh", "马什"), ("swamp", "斯旺普"), ("fen", "芬"), ("bog", "博格"),
        ("mire", "迈尔"), ("moor", "穆尔"), ("slough", "斯劳"), ("pool", "普尔"),
        ("pond", "庞德"), ("muck", "马克"), ("mud", "马德"), ("wallow", "瓦洛"),
        ("sink", "辛克"), ("pit", "皮特"), ("depth", "德普斯"), ("hollow", "霍洛"),
        ("bottom", "博顿"), ("flat", "弗拉特"), ("reach", "里奇"), ("ward", "沃德"),
        ("pocket", "波克特"), ("basin", "贝辛"), ("sink", "辛克"), ("hole", "霍尔"),
        ("dent", "登特"), ("depression", "迪普莱申"), ("dip", "迪普"), ("low", "洛"),
        ("vale", "维尔"), ("glen", "格伦"), ("dale", "戴尔"), ("delve", "德尔夫"),
    ];

    // ========================================
    // 冻土系
    // ========================================

    private (string, string) GenerateTundraName()
    {
        return GenerateBiomeName(_tundraAdj, _tundraNoun, _tundraSuffix);
    }

    private static readonly (string en, string cn)[] _tundraAdj = [
        ("Frost", "霜"), ("Ice", "冰"), ("Snow", "雪"), ("Cold", "寒"),
        ("Winter", "冬"), ("North", "北"), ("Pale", "苍"), ("White", "白"),
        ("Grey", "灰"), ("Bleak", "凄"), ("Barren", "荒"), ("Bitter", "苦"),
        ("Sharp", "锐"), ("Howling", "嚎"), ("Silent", "寂"), ("Still", "静"),
        ("Frozen", "冻"), ("Crystal", "晶"), ("Rime", "雾凇"), ("Sleet", "雨夹雪"),
        ("Hail", "冰雹"), ("Blizzard", "暴风雪"), ("Glacier", "冰川"), ("Permafrost", "永冻"),
        ("Tundra", "冻原"), ("Arctic", "极地"), ("Polar", "极"), ("Boreal", "北方"),
        ("Nordic", "北境"), ("Siberian", "西伯利亚"), ("Alpine", "高山"), ("Summit", "峰"),
        ("Peak", "顶"), ("Crest", "脊"), ("Ridge", "岭"), ("Cirque", "冰斗"),
        ("Moraine", "冰碛"), ("Erratic", "漂砾"), ("Drift", "冰碛"), ("Crevasse", "冰裂缝"),
    ];

    private static readonly (string en, string cn)[] _tundraNoun = [
        ("Wolf", "狼"), ("Bear", "熊"), ("Elk", "麋"), ("Moose", "驼鹿"),
        ("Raven", "渡鸦"), ("Owl", "枭"), ("Hare", "野兔"), ("Fox", "狐"),
        ("Ice", "冰"), ("Frost", "霜"), ("Snow", "雪"), ("Wind", "风"),
        ("Storm", "风暴"), ("Blizzard", "暴风雪"), ("Glacier", "冰川"), ("Tundra", "冻原"),
        ("Permafrost", "永冻土"), ("Sleet", "雨夹雪"), ("Hail", "冰雹"), ("Avalanche", "雪崩"),
        ("Iceberg", "冰山"), ("Icicle", "冰柱"), ("Snowflake", "雪花"), ("Crystal", "水晶"),
        ("Mammoth", "猛犸"), ("Woolly", "毛"), ("Sabre", "剑"), ("Cave", "洞"),
        ("Lair", "巢穴"), ("Den", "兽穴"), ("Burrow", "洞穴"), ("Hollow", "洞"),
        ("Ridge", "山脊"), ("Crest", "山顶"), ("Peak", "峰"), ("Summit", "顶"),
        ("Cliff", "悬崖"), ("Bluff", "峭壁"), ("Crag", "岩石"), ("Tor", "岩塔"),
    ];

    private static readonly (string en, string cn)[] _tundraSuffix = [
        ("frost", "弗罗斯特"), ("ice", "艾斯"), ("snow", "斯诺"), ("cold", "科尔德"),
        ("winter", "温特"), ("north", "诺斯"), ("reach", "里奇"), ("waste", "韦斯特"),
        ("moor", "穆尔"), ("heath", "希思"), ("flat", "弗拉特"), ("expanse", "埃克斯潘斯"),
        ("bound", "邦德"), ("ward", "沃德"), ("mark", "马克"), ("edge", "埃奇"),
        ("rim", "里姆"), ("brink", "布林克"), ("verge", "弗奇"), ("end", "恩德"),
        ("land", "兰德"), ("ground", "格朗德"), ("field", "菲尔德"), ("plain", "普莱恩"),
        ("steppe", "斯泰普"), ("tundra", "冻原"), ("barren", "巴伦"), ("waste", "韦斯特"),
        ("expanse", "埃克斯潘斯"), ("stretch", "斯特雷奇"), ("breadth", "布雷德思"), ("panse", "潘斯"),
    ];

    // ========================================
    // 丛林系
    // ========================================

    private (string, string) GenerateJungleName()
    {
        return GenerateBiomeName(_jungleAdj, _jungleNoun, _jungleSuffix);
    }

    private static readonly (string en, string cn)[] _jungleAdj = [
        ("Green", "绿"), ("Thick", "密"), ("Dense", "浓"), ("Wild", "野"),
        ("Tangled", "缠"), ("Overgrown", "蔓"), ("Humid", "湿"), ("Hot", "热"),
        ("Fever", "热"), ("Miasma", "瘴"), ("Rot", "腐"), ("Vine", "藤"),
        ("Fern", "蕨"), ("Moss", "苔"), ("Orchid", "兰"), ("Parrot", "鹦鹉"),
        ("Monkey", "猴"), ("Snake", "蛇"), ("Jaguar", "美洲豹"), ("Caiman", "凯门鳄"),
        ("Spider", "蜘蛛"), ("Scorpion", "蝎"), ("Centipede", "蜈蚣"), ("Mosquito", "蚊"),
        ("Palm", "棕榈"), ("Bamboo", "竹"), ("Ceiba", "木棉"), ("Mahogany", "桃花心木"),
        ("Teak", "柚木"), ("Ebony", "乌木"), ("Rosewood", "紫檀"), ("Sandalwood", "檀香"),
        ("Banyan", "榕"), ("Ficus", "榕"), ("Rubber", "橡胶"), ("Cacao", "可可"),
        ("Banana", "香蕉"), ("Mango", "芒果"), ("Papaya", "木瓜"), ("Guava", "番石榴"),
    ];

    private static readonly (string en, string cn)[] _jungleNoun = [
        ("Vine", "藤"), ("Root", "根"), ("Canopy", "冠"), ("Fern", "蕨"),
        ("Palm", "棕榈"), ("Bamboo", "竹"), ("Orchid", "兰"), ("Parrot", "鹦鹉"),
        ("Monkey", "猴"), ("Snake", "蛇"), ("Jaguar", "美洲豹"), ("Caiman", "凯门鳄"),
        ("Spider", "蜘蛛"), ("Scorpion", "蝎"), ("Centipede", "蜈蚣"), ("Leech", "蛭"),
        ("Mosquito", "蚊"), ("Anaconda", "森蚺"), ("Boa", "蚺"), ("Python", "蟒"),
        ("Toucan", "巨嘴鸟"), ("Macaw", "金刚鹦鹉"), ("Hummingbird", "蜂鸟"), ("Quetzal", "格查尔鸟"),
        ("Sloth", "树懒"), ("Anteater", "食蚁兽"), ("Armadillo", "犰狳"), ("Tapir", "貘"),
        ("Peccary", "西猯"), ("Agouti", "刺豚鼠"), ("Capuchin", "卷尾猴"), ("Howler", "吼猴"),
        ("Iguana", "鬣蜥"), ("Gecko", "壁虎"), ("Chameleon", "变色龙"), ("Turtle", "龟"),
        ("Tree", "树"), ("Trunk", "干"), ("Branch", "枝"), ("Leaf", "叶"),
    ];

    private static readonly (string en, string cn)[] _jungleSuffix = [
        ("jungle", "杰格尔"), ("thicket", "西克特"), ("tangle", "坦格尔"), ("wild", "怀尔德"),
        ("growth", "格罗斯"), ("depth", "德普斯"), ("heart", "哈特"), ("core", "科尔"),
        ("deep", "迪普"), ("shade", "谢德"), ("cover", "科弗"), ("canopy", "卡诺皮"),
        ("reach", "里奇"), ("ward", "沃德"), ("bound", "邦德"), ("edge", "埃奇"),
        ("march", "马奇"), ("end", "恩德"), ("rim", "里姆"), ("brink", "布林克"),
        ("verge", "弗奇"), ("bush", "布什"), ("scrub", "斯克拉布"), ("brush", "布拉什"),
        ("undergrowth", "安德格罗斯"), ("underbrush", "安德布拉什"), ("floor", "弗洛尔"), ("ground", "格朗德"),
        ("realm", "雷姆"), ("domain", "多梅恩"), ("land", "兰德"), ("country", "康特里"),
    ];

    // ========================================
    // 沿海系
    // ========================================

    private (string, string) GenerateCoastalName()
    {
        return GenerateBiomeName(_coastalAdj, _coastalNoun, _coastalSuffix);
    }

    private static readonly (string en, string cn)[] _coastalAdj = [
        ("Salt", "盐"), ("Sea", "海"), ("Blue", "蓝"), ("Grey", "灰"),
        ("White", "白"), ("Foam", "沫"), ("Wave", "浪"), ("Tide", "潮"),
        ("Storm", "风暴"), ("Gale", "大风"), ("Wind", "风"), ("Drift", "漂流"),
        ("Shell", "贝壳"), ("Pearl", "珍珠"), ("Coral", "珊瑚"), ("Sand", "沙"),
        ("Shore", "岸"), ("Coast", "海岸"), ("Bay", "湾"), ("Cove", "小湾"),
        ("Deep", "深"), ("Shallow", "浅"), ("Rocky", "岩"), ("Sandy", "沙"),
        ("Muddy", "泥"), ("Clear", "清"), ("Calm", "静"), ("Rough", "浪"),
        ("Craggy", "崎"), ("Steep", "陡"), ("Flat", "平"), ("Low", "低"),
        ("High", "高"), ("Far", "远"), ("Near", "近"), ("Old", "古"),
        ("New", "新"), ("Lost", "失"), ("Hidden", "隐"), ("Secret", "秘"),
    ];

    private static readonly (string en, string cn)[] _coastalNoun = [
        ("Gull", "海鸥"), ("Tern", "燕鸥"), ("Cormorant", "鸬鹚"), ("Seal", "海豹"),
        ("Whale", "鲸"), ("Dolphin", "海豚"), ("Crab", "蟹"), ("Oyster", "牡蛎"),
        ("Mussel", "贻贝"), ("Kelp", "海带"), ("Wrack", "海藻"), ("Drift", "漂流物"),
        ("Anchor", "锚"), ("Helm", "舵"), ("Sail", "帆"), ("Oar", "桨"),
        ("Net", "网"), ("Hook", "钩"), ("Line", "线"), ("Rope", "绳"),
        ("Mast", "桅"), ("Keel", "龙骨"), ("Hull", "船体"), ("Deck", "甲板"),
        ("Prow", "船首"), ("Stern", "船尾"), ("Galley", "桨帆船"), ("Barge", "驳船"),
        ("Skiff", "小艇"), ("Raft", "木筏"), ("Canoe", "独木舟"), ("Kayak", "皮划艇"),
        ("Fish", "鱼"), ("Shark", "鲨"), ("Ray", "鳐"), ("Eel", "鳗"),
        ("Lobster", "龙虾"), ("Shrimp", "虾"), ("Clam", "蛤"), ("Scallop", "扇贝"),
    ];

    private static readonly (string en, string cn)[] _coastalSuffix = [
        ("shore", "肖尔"), ("coast", "科斯特"), ("beach", "比奇"), ("strand", "斯特兰德"),
        ("bay", "贝"), ("cove", "科夫"), ("inlet", "因莱特"), ("harbor", "哈伯"),
        ("haven", "黑文"), ("port", "波特"), ("dock", "多克"), ("pier", "皮尔"),
        ("wharf", "沃尔夫"), ("quay", "基"), ("point", "波因特"), ("cape", "凯普"),
        ("head", "赫德"), ("ness", "尼斯"), ("march", "马奇"), ("ward", "沃德"),
        ("cliff", "克利夫"), ("bluff", "布拉夫"), ("ridge", "里奇"),
        ("rock", "洛克"), ("stone", "斯通"), ("reef", "里夫"), ("shoal", "肖尔"),
        ("bank", "班克"), ("flat", "弗拉特"), ("sand", "桑德"), ("mud", "马德"),
        ("slip", "斯利普"), ("landing", "兰丁"),
    ];

    // ========================================
    // 工具方法
    // ========================================

    private (string en, string cn) Pick((string en, string cn)[] array)
    {
        return array[_rng.Next(array.Length)];
    }

    /// <summary>
    /// 通用命名方法 — [形容词/名词] + [后缀] 模式
    /// </summary>
    private (string, string) GenerateBiomeName(
        (string en, string cn)[] adj,
        (string en, string cn)[] noun,
        (string en, string cn)[] suffix)
    {
        var (prefixE, prefixC) = _rng.Next(2) == 0 ? Pick(adj) : Pick(noun);
        var (sufE, sufC) = Pick(suffix);
        return ($"{prefixE}{sufE}", $"{prefixC}{sufC}");
    }

    /// <summary>
    /// 批量生成不重复的名称
    /// </summary>
    public List<(string english, string chinese)> GenerateUniqueNames(BiomeZone zone, int count)
    {
        var names = new HashSet<string>();
        var result = new List<(string english, string chinese)>();
        int attempts = 0;
        int maxAttempts = count * 10;

        while (names.Count < count && attempts < maxAttempts)
        {
            var (eng, chn) = GenerateName(zone, names.Count);
            if (names.Add(eng))
            {
                result.Add((eng, chn));
            }
            attempts++;
        }

        return result;
    }
}
