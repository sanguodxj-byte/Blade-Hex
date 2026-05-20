// DescriptionProvider.cs
// POI/实体交互描述文本提供器 — 三因素驱动：POI类型 × 繁荣度 × 种族/阵营
// 人类/精灵/矮人/兽人各有独立描述体系，不会混用
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 描述上下文
/// </summary>
public struct DescriptionContext
{
    public string PoiName;
    public int Prosperity;       // 0-100
    public int Garrison;
    public string OwningFaction; // 国家 ID（用于判断种族风格）
    public string RaceStyle;     // 直接指定风格：Human/Elf/Dwarf/Orc/Neutral
    public string TerrainKey;    // River/Mountain/Forest/Coast/Plain/Swamp
    public string Weather;       // Clear/Rain/Snow/Sandstorm
    public string TimeOfDay;     // Dawn/Day/Dusk/Night
    public int CurrentDay;

    public static DescriptionContext Default => new()
    {
        PoiName = "", Prosperity = 50, Garrison = 0,
        OwningFaction = "", RaceStyle = "Human",
        TerrainKey = "Plain", Weather = "Clear", TimeOfDay = "Day", CurrentDay = 1
    };
}

/// <summary>
/// 描述文本提供器 — 三因素组合生成描述。
/// 
/// 维度1: POI 类型（Town/Village/Castle/Tavern/...）
/// 维度2: 繁荣度（Rich/Mid/Poor）
/// 维度3: 种族风格（Human/Elf/Dwarf/Orc）
/// 
/// 敌对实体按种族完全分离：
/// - 人类系：山贼、劫匪、敌对领主军队
/// - 怪物系：哥布林、狗头人、牛头人、巨魔
/// - 亡灵系：骷髅、僵尸、幽灵
/// - 野兽系：狼群、熊、巨蜘蛛
/// </summary>
public static class DescriptionProvider
{
    // ========================================
    // 公共 API
    // ========================================

    /// <summary>获取友好 POI 到达描述（三因素：类型×繁荣度×种族）</summary>
    public static string GetPoiDescription(OverworldPOI.POIType type, DescriptionContext ctx)
    {
        string prosperity = ctx.Prosperity >= 70 ? "Rich" : ctx.Prosperity >= 40 ? "Mid" : "Poor";
        string race = string.IsNullOrEmpty(ctx.RaceStyle) ? "Human" : ctx.RaceStyle;
        string key = $"{type}_{prosperity}_{race}";

        var pool = GetPool(key) ?? GetPool($"{type}_{prosperity}_Human") ?? _fallbackTown;
        string template = SelectVariant(pool, ctx.PoiName, ctx.CurrentDay);
        return ApplyTemplate(template, ctx);
    }

    /// <summary>获取敌对聚落描述（按聚落种族）</summary>
    public static string GetSettlementDescription(OverworldPOI.SettlementRace race, DescriptionContext ctx)
    {
        string key = $"Settlement_{race}";
        var pool = GetPool(key) ?? _fallbackHostile;
        return ApplyTemplate(SelectVariant(pool, ctx.PoiName, ctx.CurrentDay), ctx);
    }

    /// <summary>获取巢穴描述</summary>
    public static string GetLairDescription(OverworldPOI.LairType lairType, bool isCleared, DescriptionContext ctx)
    {
        if (isCleared)
        {
            var cleared = GetPool("Lair_Cleared") ?? _fallbackCleared;
            return ApplyTemplate(SelectVariant(cleared, ctx.PoiName, ctx.CurrentDay), ctx);
        }
        string key = $"Lair_{lairType}";
        var pool = GetPool(key) ?? _fallbackHostile;
        return ApplyTemplate(SelectVariant(pool, ctx.PoiName, ctx.CurrentDay), ctx);
    }

    /// <summary>获取敌对实体遭遇描述（按实体种族分离）</summary>
    public static string GetHostileEncounter(string enemyCategory, string entityName, int partySize)
    {
        // enemyCategory: HumanBandit / HumanLord / GoblinParty / UndeadHorde / BeastPack / etc.
        string key = $"Hostile_{enemyCategory}";
        var pool = GetPool(key) ?? _fallbackHostile;
        string template = pool[Math.Abs(entityName.GetHashCode()) % pool.Length];
        return template.Replace("{name}", entityName).Replace("{count}", partySize.ToString());
    }

    /// <summary>获取中立实体遭遇描述</summary>
    public static string GetNeutralEncounter(string entityCategory, string entityName, int partySize)
    {
        string key = $"Neutral_{entityCategory}";
        var pool = GetPool(key) ?? _fallbackNeutral;
        string template = pool[Math.Abs(entityName.GetHashCode()) % pool.Length];
        return template.Replace("{name}", entityName).Replace("{count}", partySize.ToString());
    }

    // ========================================
    // 内部
    // ========================================

    private static readonly Random _rng = new();

    private static string SelectVariant(string[] pool, string name, int day)
    {
        if (pool.Length == 0) return "";
        // 同一名称+同一天（3天周期）内描述稳定
        int seed = Math.Abs((name ?? "").GetHashCode() ^ (day / 3));
        return pool[seed % pool.Length];
    }

    private static string ApplyTemplate(string t, DescriptionContext ctx)
    {
        if (string.IsNullOrEmpty(t)) return "";
        return t.Replace("{name}", ctx.PoiName)
                .Replace("{garrison}", ctx.Garrison.ToString());
    }

    private static string[]? GetPool(string key) => _pools.TryGetValue(key, out var p) ? p : null;

    private static readonly string[] _fallbackTown = ["你来到了{name}。"];
    private static readonly string[] _fallbackHostile = ["前方发现了敌人。"];
    private static readonly string[] _fallbackNeutral = ["你在路上遇到了旅人。"];
    private static readonly string[] _fallbackCleared = ["{name}已被清除，这里现在安全了。"];

    // ========================================
    // 描述池（三因素索引）
    // ========================================

    private static readonly Dictionary<string, string[]> _pools = new()
    {
        // ============================================================
        // 人类城镇
        // ============================================================
        ["Town_Rich_Human"] = [
            "你的队伍穿过{name}敞开的城门。城内主街人声鼎沸——铁匠与商人讨价还价，搬运工扛着货箱穿过人群。空气中混杂着烤肉的油香和铸币坊的金属味。",
            "{name}的城墙在阳光下泛着古老的光泽。街道上行人络绎不绝，商队的马车排起了长队。这座城镇正处于鼎盛时期。",
            "踏入{name}，你便明白为何旅人们称它为繁华之地。石砌街道一尘不染，店铺橱窗里陈列着精美的银饰和利刃。",
        ],
        ["Town_Mid_Human"] = [
            "{name}的城门半开着，一个打瞌睡的守卫被你的马蹄声惊醒。城内景象不好不坏——有些店铺开着门，有些紧闭着窗板。",
            "抵达{name}时正值黄昏。炊烟从参差不齐的屋顶升起，{garrison}名守卫例行公事地检查了你的武器。",
            "穿过{name}的城门，映入眼帘的是一条不宽不窄的主街。铁匠在门口磨刀，药剂师在递一瓶冒着绿烟的东西给顾客。",
        ],
        ["Town_Poor_Human"] = [
            "{name}的城门只剩一扇还挂在铰链上。几个穿着破旧皮甲的守军用空洞的眼神看着你走过。城内空置的店铺和坍塌的屋顶诉说着衰败。",
            "走进{name}，一股衰败的气息扑面而来。主街石板碎裂不堪，杂草从缝隙中疯长。一个老妇人沙哑地说：'这里什么都没有了。'",
        ],

        // ============================================================
        // 精灵城镇
        // ============================================================
        ["Town_Rich_Elf"] = [
            "你来到了{name}。银白色的尖塔从古木的树冠间升起，月光石铺就的道路在林间蜿蜒。精灵们的歌声如流水般在枝叶间回荡。",
            "{name}的入口被两棵千年古橡守护着。树干上刻着发光的符文，空气中弥漫着花蜜和古老魔法的气息。这是一座与自然融为一体的城市。",
        ],
        ["Town_Mid_Elf"] = [
            "{name}隐藏在密林深处。精灵的居所建在巨树的枝干间，藤桥连接着各个平台。几个精灵猎手从树冠间无声地注视着你。",
            "你来到了精灵的领地{name}。虽不如传说中那般辉煌，但树屋间的灯火和远处的竖琴声仍让人感到一种超脱尘世的宁静。",
        ],
        ["Town_Poor_Elf"] = [
            "{name}的荣光已经褪去。枯萎的古树间，精灵的居所显得空旷而寂寥。曾经发光的符文已经黯淡，只有少数精灵还留守在此。",
        ],

        // ============================================================
        // 矮人城镇
        // ============================================================
        ["Town_Rich_Dwarf"] = [
            "你来到了{name}。巨大的石门在山腹中敞开，锻炉的红光从深处涌出。矮人们推着满载矿石的矿车来来往往，锤击声如同大地的心跳。",
            "{name}的入口雕刻着矮人先祖的面容。穿过石廊，你看到了一座建在地下的城市——熔岩照亮了宏伟的石柱大厅，金属的光泽无处不在。",
        ],
        ["Town_Mid_Dwarf"] = [
            "{name}的石门厚重而古老。矮人守卫打量着你，最终点头放行。地下城市的规模不大，但每一块石头都经过精心雕琢。",
            "你进入了矮人的地下城镇{name}。虽然不如传说中的矮人王国宏伟，但锻炉的火焰从未熄灭，麦酒的香气充满了每一条走廊。",
        ],
        ["Town_Poor_Dwarf"] = [
            "{name}的矿脉已经枯竭。曾经繁忙的矿道如今空空荡荡，只有少数矮人还在坚守。锻炉的火焰微弱，但矮人的骄傲不允许他们离开。",
        ],

        // ============================================================
        // 兽人城镇
        // ============================================================
        ["Town_Rich_Orc"] = [
            "你来到了{name}。兽人的营地比你想象的更有秩序——兽皮帐篷排列整齐，战旗在风中猎猎作响。强壮的战士在操练场上比武，空气中弥漫着烤肉和铁锈的气味。",
            "{name}的木栅栏高大而坚固。兽人们用战利品装饰着入口——头骨、断剑和旗帜。这是一个强大部落的据点。",
        ],
        ["Town_Mid_Orc"] = [
            "{name}是一处兽人的聚落。帐篷和简易石屋混杂在一起，几个兽人在篝火旁磨着武器。他们用警惕但不敌对的目光打量着你。",
        ],
        ["Town_Poor_Orc"] = [
            "{name}的兽人部落显然经历了艰难时期。帐篷破旧，战士们面带饥色。但他们的眼中仍燃烧着不屈的火焰。",
        ],

        // ============================================================
        // 村庄（按种族）
        // ============================================================
        ["Village_Rich_Human"] = [
            "你来到了{name}。金色的麦浪在风中起伏，远处的磨坊缓缓转动。村口的老橡树下，孩子们在用木剑追逐。一个面色红润的村长迎了上来。",
            "炊烟袅袅升起，{name}的茅草屋顶在夕阳下泛着温暖的金色。牧羊人正赶着羊群归来，水井旁的妇人们好奇地打量着你。",
        ],
        ["Village_Mid_Human"] = [
            "{name}是个普通的小村庄。几间木屋围着一口井，村民们在田间劳作。看到你的队伍，他们停下了手中的活计。",
            "你到达了{name}。不过是几户人家和一片菜地，但村口的告示板上贴着几张委托——也许能找到些活干。",
        ],
        ["Village_Poor_Human"] = [
            "{name}只有几间歪歪斜斜的木屋和一口快要干涸的水井。田地里的庄稼稀稀拉拉。村民们用警惕的目光打量着你。",
            "这就是{name}——如果不是路边那块褪色的木牌，你甚至不会注意到这里有人居住。",
        ],
        ["Village_Rich_Elf"] = [
            "{name}坐落在一片古老的林间空地上。精灵的树屋与自然完美融合，花藤攀绕着每一根柱子。空气中弥漫着花香和露水的清新。",
        ],
        ["Village_Mid_Elf"] = [
            "精灵的小村落{name}隐藏在树冠之下。几座简朴的树屋通过藤桥相连，一个精灵猎手在远处向你点头致意。",
        ],
        ["Village_Rich_Dwarf"] = [
            "{name}是一处矮人的地表聚落。石屋坚固而整洁，每家门前都有一个小型锻炉。矮人们热情地邀请你品尝他们的麦酒。",
        ],
        ["Village_Mid_Dwarf"] = [
            "矮人的小村落{name}建在山脚下。几间石屋和一个公共锻炉构成了全部。一个矮人正在门口抽着烟斗，向你挥手。",
        ],

        // ============================================================
        // 城堡
        // ============================================================
        ["Castle_Rich_Human"] = [
            "{name}的城堡巍然耸立。厚重的吊桥缓缓放下，甲胄齐整的{garrison}名卫兵向你行礼。城头旗帜飘扬，这是一座坚不可摧的要塞。",
        ],
        ["Castle_Mid_Human"] = [
            "城堡{name}的大门在你面前敞开。庭院中士兵在操练，铁匠在为战马钉蹄铁。{garrison}名守军维持着基本的防御。",
        ],
        ["Castle_Rich_Dwarf"] = [
            "{name}的矮人堡垒如同从山体中雕刻而出。巨大的石门上刻着符文，{garrison}名矮人重甲战士守卫着入口。",
        ],

        // ============================================================
        // 敌对聚落（按种族完全分离）
        // ============================================================
        ["Settlement_Goblin"] = [
            "前方是一处哥布林营地。粗糙的尖木栅栏围成一圈，空气中弥漫着腐肉和篝火的恶臭。尖锐的嘶叫声从深处传来。",
            "你发现了哥布林的巢穴。破烂的帐篷和骨头堆散落各处，几个矮小的身影在阴影中窥视着你。",
            "一处哥布林据点出现在视野中。他们用偷来的盾牌和断剑装饰着入口，仿佛在炫耀战利品。",
        ],
        ["Settlement_Kobold"] = [
            "你发现了狗头人的矿坑入口。精巧的陷阱机关隐藏在碎石之间，洞口传来叮叮当当的挖掘声。",
            "这是一处狗头人的地下据点。他们在岩壁上刻满了奇怪的符号，微弱的火光从深处闪烁。",
        ],
        ["Settlement_Minotaur"] = [
            "一座粗犷的石堡矗立在前方。巨大的牛角装饰着城门，地面上的蹄印深可没踝。牛头人的领地。",
            "牛头人的要塞如同一座小山。巨石堆砌的城墙后传来低沉的吼声，大地似乎在微微颤抖。",
        ],
        ["Settlement_ShadowCult"] = [
            "一股不祥的气息从暗影教团据点传来。黑色旗帜上绘着扭曲的符文，紫色烟雾从祭坛升起。",
            "暗影教团的据点隐藏在阴暗之中。诡异的低语声在空气中回荡，让人不寒而栗。",
        ],
        ["Settlement_Bandit"] = [
            "前方是一处山贼营地。简陋的帐篷围着篝火，几个衣衫褴褛的家伙正在分赃。",
            "你发现了山贼的藏身处。路边的树上挂着警告标志——一个骷髅头。",
        ],
        ["Settlement_Pirate"] = [
            "一处海寇的洞穴出现在海岸边。破旧的船帆被用作帐篷，沙滩上散落着朗姆酒瓶和断桨。",
        ],

        // ============================================================
        // 巢穴
        // ============================================================
        ["Lair_DragonLair"] = [
            "一股灼热的气息从洞穴深处涌出。巨大的爪痕刻在岩壁上，散落的骨骸和熔化的金属诉说着这里的主人有多么可怕。",
            "龙巢的入口如同一张巨兽的血盆大口。空气中弥漫着硫磺的气味，地面上的焦痕还在冒着余烟。",
        ],
        ["Lair_AncientTomb"] = [
            "古老的墓穴入口被藤蔓和苔藓覆盖。石门上的封印已经破碎，一股阴冷的气息从缝隙中渗出。",
            "你发现了一座远古墓穴。台阶向下延伸到黑暗中，墙壁上的壁画描绘着早已被遗忘的王朝。",
        ],
        ["Lair_Ruins"] = [
            "远古遗迹的残垣断壁在荒草中若隐若现。曾经辉煌的文明只剩下沉默的石柱和破碎的拱门。",
            "遗迹中传来奇怪的嗡鸣声。某种古老的魔法似乎仍在运作，蓝色的光芒在裂缝中闪烁。",
        ],
        ["Lair_GolemForge"] = [
            "一座被遗弃的魔像工坊矗立在前方。巨大的熔炉虽已熄灭，但金属碰撞的声音仍从内部传来。",
        ],
        ["Lair_Cleared"] = [
            "{name}已经被清除。曾经的危险之地如今只剩下空荡荡的洞穴和散落的残骸。",
            "你再次来到{name}。这里已经安全了，只有风声在空旷的废墟中回荡。",
        ],

        // ============================================================
        // 敌对实体 — 人类系（山贼/劫匪/领主军）
        // ============================================================
        ["Hostile_HumanBandit"] = [
            "一声尖锐的口哨划破寂静。{name}从灌木丛中涌出，{count}个衣衫褴褛但手持利刃的身影切断了你的前路。领头的嘴角挂着残忍的笑意。",
            "道路在前方突然变窄——完美的伏击地点。{name}显然也这么认为。{count}个人影出现在岩壁顶端，弓弦已经拉满。",
            "前方的路上散落着一辆翻倒的马车——这是陷阱。{name}的{count}人已经从尸体旁站了起来，身上还沾着前一个受害者的血。",
        ],
        ["Hostile_HumanLord"] = [
            "一面敌对领主的战旗出现在前方。{name}的{count}名士兵排成整齐方阵，长矛如林。一个军官策马上前：'放下武器，否则格杀勿论。'",
            "战鼓声从山丘后传来。{name}的{count}名士兵翻过山脊，如同一道铁灰色的洪流。这是一支经历过真正战争的军队。",
        ],

        // ============================================================
        // 敌对实体 — 怪物系（哥布林/牛头人/巨魔）
        // ============================================================
        ["Hostile_GoblinParty"] = [
            "尖锐的嘶叫声从四面八方响起。{count}个哥布林从草丛和石缝中钻出，挥舞着生锈的匕首和削尖的木棍。它们的眼中闪烁着贪婪的光芒。",
            "一群哥布林挡住了去路。{count}个矮小的身影互相推搡着，似乎在争论谁先上前。但当它们的头目一声吼叫后，所有哥布林都安静下来，齐刷刷地看向你。",
        ],
        ["Hostile_MinotaurParty"] = [
            "大地在颤抖。{count}个牛头人从前方的岩石后现身，每一个都比你高出两个头。它们手持巨斧，鼻孔中喷出白色的热气。",
            "一声震耳欲聋的吼叫在山谷中回荡。{name}的{count}名牛头人战士列阵而来，地面在它们的蹄下龟裂。",
        ],
        ["Hostile_UndeadHorde"] = [
            "一股腐朽的气息扑面而来。{count}具亡灵从地面的裂缝中爬出，空洞的眼眶中燃烧着幽绿的火焰。",
            "大地在颤抖，枯骨从泥土中挣扎而出。{count}个亡灵正向你逼近，死亡的低语充斥着空气。",
        ],
        ["Hostile_BeastPack"] = [
            "一群野兽挡住了去路。{count}头凶猛的生物低吼着，獠牙上还残留着上一顿猎物的血迹。",
            "前方的灌木丛中传来低沉的咆哮。{count}头野兽从阴影中现身，对你虎视眈眈。",
        ],

        // ============================================================
        // 中立实体
        // ============================================================
        ["Neutral_Caravan"] = [
            "一支商队正沿道路缓缓前行。{name}的{count}名护卫警惕地注视着四周，但看到你后稍微放松了些。领队探出头来：'有兴趣看看货物吗？'",
            "道路上传来驼铃声和车轮的吱呀声。{name}的商队正在前行，{count}名护卫分散在两侧。",
        ],
        ["Neutral_Adventurer"] = [
            "你在路边遇到了{name}的营地。{count}名冒险者围坐在篝火旁。一个戴宽檐帽的弓手抬起头：'路过的？还是来找活干的？'",
            "{name}的队伍正在路旁休整。{count}个人，有战士、有法师。他们注意到了你，但没有拔出武器。",
        ],
        ["Neutral_Traveler"] = [
            "你在路上遇到了一位旅人。对方看起来并无恶意，友好地点了点头。",
            "前方有人在路边休息。从装束来看是个普通的旅行者，也许可以交换些情报。",
        ],
    };
}
