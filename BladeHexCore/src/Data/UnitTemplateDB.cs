// UnitTemplateDB.cs
// 单位模板数据库 — 120级等级体系
// 每级+1属性点，1级总属性=25
// 等级定位：杂兵1~5, 熟练6~18, 精英18~36, 首领42~66, 传奇78~120
// CR = floor(level/6)，可由模板手动覆盖
using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 单位模板数据库 — 纯静态工具类
/// </summary>
public static class UnitTemplateDB
{
    // ========================================
    // 属性分配权重预设
    // ========================================

    public static readonly float[] WMeleeBruiser = [3.0f, 1.5f, 2.5f, 0.5f, 1.0f, 0.5f];
    public static readonly float[] WRangedAgility = [1.0f, 3.0f, 1.0f, 0.5f, 1.5f, 1.0f];
    public static readonly float[] WMage = [0.5f, 1.0f, 1.0f, 3.0f, 2.0f, 1.5f];
    public static readonly float[] WBeast = [2.5f, 2.0f, 2.0f, 1.0f, 0.5f, 0.0f];
    public static readonly float[] WTank = [2.5f, 0.5f, 3.0f, 0.5f, 1.5f, 1.0f];
    public static readonly float[] WLeader = [1.5f, 1.0f, 1.5f, 1.5f, 1.0f, 2.5f];
    public static readonly float[] WConstruct = [3.0f, 1.0f, 3.0f, 0.0f, 0.5f, 0.0f];
    public static readonly float[] WDragon = [2.0f, 1.5f, 2.0f, 1.5f, 1.5f, 1.5f];
    public static readonly float[] WCunning = [0.5f, 1.5f, 1.0f, 2.5f, 1.5f, 2.0f];

    // ========================================
    // 属性分配引擎
    // ========================================

    /// <summary>属性分配权重 → 实际属性值</summary>
    public static Dictionary<string, int> DistributeAttrs(float[] weights, int level)
    {
        int totalPoints = RPGRuleEngine.GetTotalAttrPoints(level);
        float weightSum = 0f;
        foreach (var w in weights) weightSum += Mathf.Max(w, 0.01f);

        var attrs = new Dictionary<string, int>();
        string[] keys = RPGRuleEngine.AttrKeys;
        int allocated = 0;

        for (int i = 0; i < 6; i++)
        {
            int share = Mathf.RoundToInt(totalPoints * weights[i] / weightSum);
            share = Mathf.Clamp(share, RPGRuleEngine.AttrMin, RPGRuleEngine.AttrMax);
            attrs[keys[i]] = share;
            allocated += share;
        }

        // 修正舍入误差
        int diff = totalPoints - allocated;
        int primaryIdx = 0;
        float maxW = -1f;
        for (int i = 0; i < 6; i++)
            if (weights[i] > maxW) { maxW = weights[i]; primaryIdx = i; }

        while (diff > 0)
        {
            attrs[keys[primaryIdx]] = Mathf.Min(attrs[keys[primaryIdx]] + 1, RPGRuleEngine.AttrMax);
            diff--;
        }
        while (diff < 0)
        {
            int minIdx = 0;
            float minW = 999f;
            for (int i = 0; i < 6; i++)
                if (weights[i] < minW && attrs[keys[i]] > RPGRuleEngine.AttrMin)
                { minW = weights[i]; minIdx = i; }
            attrs[keys[minIdx]] = Mathf.Max(attrs[keys[minIdx]] - 1, RPGRuleEngine.AttrMin);
            diff++;
        }

        return attrs;
    }

    /// <summary>从模板数据构建完整属性</summary>
    public static Dictionary<string, int> BuildAttrsFromTemplate(Godot.Collections.Dictionary tpl)
    {
        var weightsArr = ToFloatArray((Godot.Collections.Array)tpl["attr_weights"]);
        var attrs = DistributeAttrs(weightsArr, tpl["level"].AsInt32());
        if (tpl.ContainsKey("attr_overrides"))
        {
            var overrides = tpl["attr_overrides"].AsGodotDictionary();
            foreach (var key in overrides.Keys)
                attrs[key.AsString()] = overrides[key].AsInt32();
        }
        return attrs;
    }

    /// <summary>从模板计算HP</summary>
    public static int CalculateHpFromTemplate(Godot.Collections.Dictionary tpl)
    {
        var attrs = BuildAttrsFromTemplate(tpl);
        int baseHp = tpl.ContainsKey("base_hp") ? tpl["base_hp"].AsInt32() : 10;
        return RPGRuleEngine.CalculateMaxHp(baseHp, attrs["con"], tpl["level"].AsInt32());
    }

    /// <summary>从模板计算CR</summary>
    public static float CalculateCrFromTemplate(Godot.Collections.Dictionary tpl)
    {
        if (tpl.ContainsKey("cr_override"))
            return tpl["cr_override"].AsSingle();
        return RPGRuleEngine.GetCrFromLevel(tpl["level"].AsInt32());
    }

    /// <summary>从模板字典创建 UnitData 实例</summary>
    public static UnitData InstantiateTemplate(Godot.Collections.Dictionary tpl)
    {
        var unit = new UnitData();
        unit.UnitName = tpl["name"].AsString();
        unit.Level = tpl["level"].AsInt32();
        unit.IsEnemy = true;
        unit.enemyType = (UnitData.EnemyType)tpl["enemy_type"].AsInt32();
        unit.creatureSize = tpl.ContainsKey("creature_size")
            ? (UnitData.CreatureSize)tpl["creature_size"].AsInt32()
            : UnitData.CreatureSize.Medium;
        unit.ThreatLevel = CalculateCrFromTemplate(tpl);
        unit.aiStrategy = (UnitData.AIStrategy)tpl["ai_strategy"].AsInt32();
        unit.Morale = tpl.ContainsKey("morale") ? tpl["morale"].AsInt32() : 0;

        // 属性
        var attrs = BuildAttrsFromTemplate(tpl);
        unit.Str = attrs["str"];
        unit.Dex = attrs["dex"];
        unit.Con = attrs["con"];
        unit.Intel = attrs["intel"];
        unit.Wis = attrs["wis"];
        unit.Cha = attrs["cha"];

        // HP
        unit.BaseMaxHp = CalculateHpFromTemplate(tpl);
        unit.BaseAc = 10 + (tpl.ContainsKey("ac_bonus") ? tpl["ac_bonus"].AsInt32() : 0);
        unit.BaseMoveRange = tpl.ContainsKey("move_range") ? tpl["move_range"].AsInt32() : 4;
        unit.BaseInitiative = tpl.ContainsKey("initiative_bonus") ? tpl["initiative_bonus"].AsInt32() : 0;

        // 免疫/抗性/弱点/特性
        unit.Immunities = ToStringArray(tpl, "immunities");
        unit.Resistances = ToStringArray(tpl, "resistances");
        unit.Weaknesses = ToStringArray(tpl, "weaknesses");
        unit.Traits = ToStringArray(tpl, "traits");

        // 天然装甲
        unit.NaturalDr = tpl.ContainsKey("natural_dr") ? tpl["natural_dr"].AsInt32() : 0;
        unit.NaturalDrThreshold = tpl.ContainsKey("natural_dr_threshold") ? tpl["natural_dr_threshold"].AsInt32() : 0;

        // 传奇属性
        unit.LegendaryResistanceUses = tpl.ContainsKey("legendary_resistance_uses") ? tpl["legendary_resistance_uses"].AsInt32() : 0;
        unit.LegendaryActionPoints = tpl.ContainsKey("legendary_action_points") ? tpl["legendary_action_points"].AsInt32() : 0;
        unit.LegendaryActions = CopyDictArray(tpl, "legendary_actions");
        unit.LairActions = CopyDictArray(tpl, "lair_actions");
        unit.Phases = CopyDictArray(tpl, "phases");
        unit.UniqueDropId = tpl.ContainsKey("unique_drop_id") ? tpl["unique_drop_id"].AsString() : "";

        return unit;
    }

    public static Dictionary<string, System.Func<Godot.Collections.Dictionary>> Templates = new()
    {
        { "grunt_goblin_warrior", GruntGoblinWarrior },
        { "grunt_goblin_archer", GruntGoblinArcher },
        { "grunt_skeleton_warrior", GruntSkeletonWarrior },
        { "grunt_forest_wolf", GruntForestWolf },
        { "grunt_zombie", GruntZombie },
        { "grunt_slime", GruntSlime },
        { "grunt_imp", GruntImp },
        { "grunt_lava_slime", GruntLavaSlime },
        // ... 其他模板可以在这里注册
    };

    public static Godot.Collections.Dictionary? GetTemplateById(string id)
    {
        if (Templates.TryGetValue(id, out var func)) return func();
        return null;
    }

    public static List<Godot.Collections.Dictionary> GetAllTemplates()
    {
        var list = new List<Godot.Collections.Dictionary>();
        foreach (var func in Templates.Values) list.Add(func());
        return list;
    }

    // ========================================
    // 杂兵模板（等级 1~5）
    // ========================================

    public static Godot.Collections.Dictionary GruntGoblinWarrior() => new()
    {
        { "template_id", "grunt_goblin_warrior" }, { "name", "哥布林战士" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 2 },
        { "attr_weights", ToVariantArray([1.5f, 2.5f, 1.0f, 1.0f, 0.5f, 0.5f]) },
        { "base_hp", 6 }, { "ac_bonus", 1 }, { "move_range", 5 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Instinct },
        { "traits", new string[] { "群体战术", "卑鄙偷袭" } },
        { "description", "矮小狡猾的哥布林，数量众多时格外危险。" },
    };

    public static Godot.Collections.Dictionary GruntGoblinArcher() => new()
    {
        { "template_id", "grunt_goblin_archer" }, { "name", "哥布林弓手" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 2 },
        { "attr_weights", ToVariantArray([0.5f, 3.0f, 0.5f, 1.0f, 1.0f, 0.5f]) },
        { "base_hp", 5 }, { "ac_bonus", 0 }, { "move_range", 5 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "traits", new string[] { "远程骚扰", "游击战" } },
        { "description", "擅长在远处用淬毒箭矢骚扰敌人。" },
    };

    public static Godot.Collections.Dictionary GruntSkeletonWarrior() => new()
    {
        { "template_id", "grunt_skeleton_warrior" }, { "name", "骷髅战士" },
        { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 3 },
        { "attr_weights", ToVariantArray([1.5f, 2.0f, 1.0f, 0.5f, 0.5f, 0.0f]) },
        { "base_hp", 6 }, { "ac_bonus", 2 }, { "move_range", 5 }, { "initiative_bonus", -1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Instinct },
        { "natural_dr", 12 }, { "natural_dr_threshold", 3 },
        { "immunities", new string[] { "poison", "mind" } }, { "resistances", new string[] { "pierce" } },
        { "traits", new string[] { "亡灵坚韧", "不知疲倦" } },
        { "description", "被黑暗魔法唤醒的骸骨战士，没有痛觉和恐惧。" },
    };

    public static Godot.Collections.Dictionary GruntForestWolf() => new()
    {
        { "template_id", "grunt_forest_wolf" }, { "name", "森林狼" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 3 },
        { "attr_weights", ToVariantArray(WBeast) },
        { "base_hp", 7 }, { "ac_bonus", 1 }, { "move_range", 8 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Instinct },
        { "natural_dr", 8 }, { "natural_dr_threshold", 2 },
        { "traits", new string[] { "嗅觉追踪", "群猎：相邻每有1个友方狼，攻击+1" } },
        { "description", "成群出没的森林狼，嗅觉灵敏，善于包围猎物。" },
    };

    public static Godot.Collections.Dictionary GruntZombie() => new()
    {
        { "template_id", "grunt_zombie" }, { "name", "腐尸" },
        { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 4 },
        { "attr_weights", ToVariantArray([2.0f, 0.5f, 2.0f, 0.5f, 0.5f, 0.0f]) },
        { "base_hp", 8 }, { "ac_bonus", -2 }, { "move_range", 4 }, { "initiative_bonus", -3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "natural_dr", 4 }, { "natural_dr_threshold", 1 },
        { "immunities", new string[] { "poison", "mind" } },
        { "traits", new string[] { "腐烂之躯：近战攻击者中毒" } },
        { "description", "行动迟缓的腐尸，但肉体异常坚韧。" },
    };

    public static Godot.Collections.Dictionary GruntSlime() => new()
    {
        { "template_id", "grunt_slime" }, { "name", "史莱姆" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 3 },
        { "attr_weights", ToVariantArray([1.0f, 0.5f, 3.0f, 0.0f, 0.5f, 0.0f]) },
        { "base_hp", 7 }, { "ac_bonus", -2 }, { "move_range", 4 }, { "initiative_bonus", -2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Instinct },
        { "natural_dr", 30 }, { "natural_dr_threshold", 4 },
        { "immunities", new string[] { "poison", "mind" } }, { "resistances", new string[] { "pierce", "slash" } },
        { "traits", new string[] { "分裂：HP<50%时分裂为2个小史莱姆", "腐蚀：近战攻击者武器受损" } },
        { "description", "黏液构成的生物，物理攻击对它效果甚微。" },
    };

    public static Godot.Collections.Dictionary GruntImp() => new()
    {
        { "template_id", "grunt_imp" }, { "name", "小恶魔" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 4 },
        { "attr_weights", ToVariantArray([0.5f, 2.5f, 1.5f, 1.0f, 1.0f, 2.0f]) },
        { "base_hp", 5 }, { "ac_bonus", 1 }, { "move_range", 5 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "creature_size", (int)UnitData.CreatureSize.Tiny },
        { "natural_dr", 6 }, { "natural_dr_threshold", 1 },
        { "immunities", new string[] { "fire", "poison" } }, { "resistances", new string[] { "cold" } },
        { "traits", new string[] { "飞行：无视地形", "隐身：1次/战斗" } },
        { "description", "体型微小的恶魔，善于飞行和隐身偷袭。" },
    };

    public static Godot.Collections.Dictionary GruntLavaSlime() => new()
    {
        { "template_id", "grunt_lava_slime" }, { "name", "熔岩史莱姆" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 5 },
        { "attr_weights", ToVariantArray([1.0f, 0.5f, 3.0f, 0.0f, 0.5f, 0.0f]) },
        { "base_hp", 8 }, { "ac_bonus", -1 }, { "move_range", 4 }, { "initiative_bonus", -2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Instinct },
        { "natural_dr", 35 }, { "natural_dr_threshold", 5 },
        { "immunities", new string[] { "fire" } }, { "resistances", new string[] { "physical" } },
        { "traits", new string[] { "液态体", "灼热：近战攻击者受1d4火焰", "分裂" } },
        { "description", "从火山裂缝中涌出的液态火球，触碰即灼伤。" },
    };

    // ========================================
    // 熟练模板（等级 6~17）
    // ========================================

    public static Godot.Collections.Dictionary StandardGoblinChieftain() => new()
    {
        { "template_id", "std_goblin_chieftain" }, { "name", "哥布林首领" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 8 },
        { "attr_weights", ToVariantArray(WLeader) },
        { "base_hp", 8 }, { "ac_bonus", 2 }, { "move_range", 5 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "immunities", new string[] { "fear" } },
        { "traits", new string[] { "领袖气场", "战吼：友方攻击+2持续1回合" } },
        { "description", "哥布林部落中最强壮的首领，狡猾而残忍。" },
    };

    public static Godot.Collections.Dictionary StandardOcBerserker() => new()
    {
        { "template_id", "std_orc_berserker" }, { "name", "兽人狂战" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 9 },
        { "attr_weights", ToVariantArray(WMeleeBruiser) },
        { "base_hp", 8 }, { "ac_bonus", 1 }, { "move_range", 5 }, { "initiative_bonus", 1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless }, { "morale", 10 },
        { "traits", new string[] { "鲁莽攻击：攻击优势但被攻击也有优势" } },
        { "description", "嗜血的兽人战士，崇尚绝对的力量。" },
    };

    public static Godot.Collections.Dictionary StandardGiantSpider() => new()
    {
        { "template_id", "std_giant_spider" }, { "name", "巨型蜘蛛" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 10 },
        { "attr_weights", ToVariantArray([1.0f, 3.0f, 1.5f, 0.5f, 1.0f, 0.0f]) },
        { "base_hp", 8 }, { "ac_bonus", 2 }, { "move_range", 8 }, { "initiative_bonus", 4 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "natural_dr", 20 }, { "natural_dr_threshold", 4 },
        { "traits", new string[] { "蛛网行走：无视蛛网地形惩罚", "吐丝：射程4格，目标缚足2回合" } },
        { "description", "体型如牛的巨型蜘蛛，毒液能让成年男子瞬间麻痹。" },
    };

    public static Godot.Collections.Dictionary StandardGhoul() => new()
    {
        { "template_id", "std_ghoul" }, { "name", "食尸鬼" },
        { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 11 },
        { "attr_weights", ToVariantArray([2.0f, 2.0f, 1.5f, 0.5f, 0.5f, 0.0f]) },
        { "base_hp", 8 }, { "ac_bonus", 1 }, { "move_range", 6 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "immunities", new string[] { "poison", "mind" } }, { "resistances", new string[] { "necrotic" } },
        { "traits", new string[] { "腐臭爪击", "麻痹之咬：命中后DC12强韧麻痹1回合" } },
        { "description", "以腐肉为食的亡灵，爪牙带有麻痹毒素。" },
    };

    public static Godot.Collections.Dictionary StandardBlackBear() => new()
    {
        { "template_id", "std_black_bear" }, { "name", "洞穴巨熊" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 12 },
        { "attr_weights", ToVariantArray(WBeast) },
        { "base_hp", 10 }, { "ac_bonus", 2 }, { "move_range", 6 }, { "initiative_bonus", -1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 40 }, { "natural_dr_threshold", 6 },
        { "traits", new string[] { "多重攻击：熊掌+啃咬", "狂暴：HP<25%时攻击+2，AC-2" } },
        { "description", "体型巨大的洞穴熊，一掌可拍碎铠甲。" },
    };

    public static Godot.Collections.Dictionary StandardGiantScorpion() => new()
    {
        { "template_id", "std_giant_scorpion" }, { "name", "沙漠巨蝎" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 12 },
        { "attr_weights", ToVariantArray([2.0f, 1.5f, 2.0f, 0.0f, 1.0f, 0.0f]) },
        { "base_hp", 10 }, { "ac_bonus", 3 }, { "move_range", 6 }, { "initiative_bonus", 1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Instinct },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 50 }, { "natural_dr_threshold", 7 },
        { "traits", new string[] { "毒尾穿刺：命中附带中毒DC13", "钳制：命中后目标缚足" } },
        { "description", "沙漠中潜伏的巨蝎，尾针的毒液致命无比。" },
    };

    public static Godot.Collections.Dictionary StandardDireWolf() => new()
    {
        { "template_id", "std_dire_wolf" }, { "name", "巨狼" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 14 },
        { "attr_weights", ToVariantArray(WBeast) },
        { "base_hp", 9 }, { "ac_bonus", 2 }, { "move_range", 10 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Instinct },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 30 }, { "natural_dr_threshold", 5 },
        { "traits", new string[] { "群猎：相邻每有1个友方狼，攻击+2", "扑击：冲锋命中后目标倒地" } },
        { "description", "比普通狼大一倍的巨狼，群猎时致命异常。" },
    };

    public static Godot.Collections.Dictionary StandardHarpy() => new()
    {
        { "template_id", "std_harpy" }, { "name", "鹰身女妖" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 13 },
        { "attr_weights", ToVariantArray([1.0f, 2.5f, 1.0f, 0.5f, 1.0f, 2.0f]) },
        { "base_hp", 7 }, { "ac_bonus", 1 }, { "move_range", 6 }, { "initiative_bonus", 4 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "traits", new string[] { "飞行：无视地形", "魅惑之歌：DC13WIS否则向其移动" } },
        { "description", "半人半鸟的鹰身女妖，歌声能迷惑旅人。" },
    };

    public static Godot.Collections.Dictionary StandardHellhound() => new()
    {
        { "template_id", "std_hellhound" }, { "name", "地狱犬" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 15 },
        { "attr_weights", ToVariantArray(WBeast) },
        { "base_hp", 9 }, { "ac_bonus", 2 }, { "move_range", 7 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "natural_dr", 15 }, { "natural_dr_threshold", 3 },
        { "immunities", new string[] { "fire" } }, { "resistances", new string[] { "magic" } },
        { "traits", new string[] { "火焰吐息：锥形3格4d6火焰冷却2回合", "追踪灵魂" } },
        { "description", "来自深渊的猎犬，口中喷吐着不灭的地狱之火。" },
    };

    public static Godot.Collections.Dictionary StandardSkeletonArcher() => new()
    {
        { "template_id", "std_skeleton_archer" }, { "name", "骷髅弓手" },
        { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 10 },
        { "attr_weights", ToVariantArray([0.5f, 3.0f, 1.0f, 0.5f, 0.5f, 0.0f]) },
        { "base_hp", 6 }, { "ac_bonus", 2 }, { "move_range", 5 }, { "initiative_bonus", 1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "natural_dr", 12 }, { "natural_dr_threshold", 3 },
        { "immunities", new string[] { "poison", "mind" } },
        { "traits", new string[] { "亡灵坚韧", "不知疲倦" } },
        { "description", "被黑暗魔法唤醒的骸骨射手，箭术精准。" },
    };

    public static Godot.Collections.Dictionary StandardGriffin() => new()
    {
        { "template_id", "std_griffin" }, { "name", "狮鹫" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 15 },
        { "attr_weights", ToVariantArray([2.0f, 2.0f, 1.5f, 0.5f, 1.5f, 0.5f]) },
        { "base_hp", 10 }, { "ac_bonus", 3 }, { "move_range", 8 }, { "initiative_bonus", 4 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "immunities", new string[] { "fear" } },
        { "traits", new string[] { "飞行：无视地形", "鹰眼：远程攻击+1", "俯冲攻击：冲锋伤害+1d8" } },
        { "description", "雄鹰与雄狮的结合体，天空的王者。" },
    };

    public static Godot.Collections.Dictionary StandardTroll() => new()
    {
        { "template_id", "std_troll" }, { "name", "森林巨魔" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 17 },
        { "attr_weights", ToVariantArray([3.0f, 0.5f, 3.0f, 0.5f, 1.0f, 0.5f]) },
        { "base_hp", 10 }, { "ac_bonus", 1 }, { "move_range", 6 }, { "initiative_bonus", -1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 35 }, { "natural_dr_threshold", 5 },
        { "resistances", new string[] { "physical" } },
        { "weaknesses", new string[] { "fire×1.5", "acid×1.5" } },
        { "traits", new string[] { "再生：每回合恢复等级×1HP", "巨力：攻击附带推后1格" } },
        { "description", "力大无穷且拥有恐怖再生能力的巨魔。" },
    };

    // ========================================
    // 精英模板（等级 18~36）
    // ========================================

    public static Godot.Collections.Dictionary EliteOgre() => new()
    {
        { "template_id", "elite_ogre" }, { "name", "食人魔" },
        { "enemy_type", (int)UnitData.EnemyType.Giant }, { "level", 18 },
        { "attr_weights", ToVariantArray(WTank) },
        { "base_hp", 12 }, { "ac_bonus", 2 }, { "move_range", 7 }, { "initiative_bonus", -2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 55 }, { "natural_dr_threshold", 7 },
        { "traits", new string[] { "厚皮：物理伤害-3", "巨力：攻击附带推后1格" } },
        { "description", "笨重但力大无穷的食人魔，一根木棍就能横扫战场。" },
    };

    public static Godot.Collections.Dictionary EliteMinotaur() => new()
    {
        { "template_id", "elite_minotaur" }, { "name", "牛头人" },
        { "enemy_type", (int)UnitData.EnemyType.Giant }, { "level", 24 },
        { "attr_weights", ToVariantArray([3.0f, 1.0f, 2.5f, 0.5f, 1.5f, 0.5f]) },
        { "base_hp", 12 }, { "ac_bonus", 3 }, { "move_range", 8 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 70 }, { "natural_dr_threshold", 9 },
        { "traits", new string[] { "冲锋：冲锋时伤害+2d6", "迷宫直觉：不会迷路" } },
        { "description", "半人半牛的迷宫守护者，冲锋势不可挡。" },
    };

    public static Godot.Collections.Dictionary EliteGargoyle() => new()
    {
        { "template_id", "elite_gargoyle" }, { "name", "石像鬼" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 24 },
        { "attr_weights", ToVariantArray(WTank) },
        { "base_hp", 12 }, { "ac_bonus", 5 }, { "move_range", 6 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "natural_dr", 80 }, { "natural_dr_threshold", 10 },
        { "resistances", new string[] { "physical", "magic" } }, { "immunities", new string[] { "poison" } },
        { "traits", new string[] { "石化伪装：第一轮攻击优势", "飞行", "魔法抗性：法术豁免+2" } },
        { "description", "伪装成石像的恶魔，一旦靠近就会苏醒发起突袭。" },
    };

    public static Godot.Collections.Dictionary EliteCorruptedTreant() => new()
    {
        { "template_id", "elite_corrupted_treant" }, { "name", "腐化树人" },
        { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 24 },
        { "attr_weights", ToVariantArray([3.0f, 0.5f, 3.0f, 0.5f, 1.5f, 0.0f]) },
        { "base_hp", 14 }, { "ac_bonus", 2 }, { "move_range", 4 }, { "initiative_bonus", -3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 90 }, { "natural_dr_threshold", 11 },
        { "resistances", new string[] { "physical" } }, { "immunities", new string[] { "mind" } },
        { "traits", new string[] { "树皮护甲：额外DR20", "根须缠绕：射程3格缚足2回合", "腐化孢子：周围1格中毒" } },
        { "description", "被黑暗力量腐化的远古树人，树皮坚硬如铁。" },
    };

    public static Godot.Collections.Dictionary EliteLamia() => new()
    {
        { "template_id", "elite_lamia" }, { "name", "毒蛇女妖" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 25 },
        { "attr_weights", ToVariantArray(WCunning) },
        { "base_hp", 10 }, { "ac_bonus", 2 }, { "move_range", 7 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "resistances", new string[] { "poison" } }, { "immunities", new string[] { "poison" } },
        { "traits", new string[] { "蛇身：移动不受困难地形影响", "毒牙：命中中毒DC15", "魅惑凝视：DC15WIS否则被控制1回合" } },
        { "description", "半人半蛇的诱惑者，目光能令猎物丧失战意。" },
    };

    public static Godot.Collections.Dictionary EliteShadowAssassin() => new()
    {
        { "template_id", "elite_shadow_assassin" }, { "name", "暗影刺客" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 24 },
        { "attr_weights", ToVariantArray(WRangedAgility) },
        { "base_hp", 8 }, { "ac_bonus", 2 }, { "move_range", 6 }, { "initiative_bonus", 6 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "traits", new string[] { "潜行大师：首轮隐身", "致命一击：暴击范围19-20", "暗影步：传送4格" } },
        { "description", "来自暗影公会的精英刺客，擅长从暗处给予致命一击。" },
    };

    public static Godot.Collections.Dictionary EliteDeathKnight() => new()
    {
        { "template_id", "elite_death_knight" }, { "name", "亡灵骑士" },
        { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 30 },
        { "attr_weights", ToVariantArray(WMeleeBruiser) },
        { "base_hp", 12 }, { "ac_bonus", 4 }, { "move_range", 6 }, { "initiative_bonus", 1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "natural_dr", 90 }, { "natural_dr_threshold", 10 },
        { "immunities", new string[] { "poison", "mind", "fear" } }, { "resistances", new string[] { "necrotic", "cold" } },
        { "traits", new string[] { "骑士冲锋：冲锋时伤害×1.5", "恐惧光环：3格内敌方恐惧", "寒冰之剑：命中附带1d6冰霜" } },
        { "description", "堕落的骑士被黑暗力量复活，手持散发寒气的漆黑长剑。" },
    };

    public static Godot.Collections.Dictionary EliteFireElemental() => new()
    {
        { "template_id", "elite_fire_elemental" }, { "name", "火元素" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 30 },
        { "attr_weights", ToVariantArray([1.0f, 2.0f, 2.0f, 0.5f, 1.5f, 0.5f]) },
        { "base_hp", 10 }, { "ac_bonus", 4 }, { "move_range", 8 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Berserk },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 70 }, { "natural_dr_threshold", 8 },
        { "immunities", new string[] { "fire", "poison", "mind", "fear" } }, { "weaknesses", new string[] { "cold×2" } },
        { "traits", new string[] { "灵焰之躯：近战攻击者受1d6火焰反击", "火焰爆发：半径2格6d6火焰，冷却3回合" } },
        { "description", "纯粹的火焰元素体，接触即灼伤。" },
    };

    public static Godot.Collections.Dictionary EliteIceElemental() => new()
    {
        { "template_id", "elite_ice_elemental" }, { "name", "冰元素" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 30 },
        { "attr_weights", ToVariantArray(WTank) },
        { "base_hp", 12 }, { "ac_bonus", 5 }, { "move_range", 6 }, { "initiative_bonus", 0 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 100 }, { "natural_dr_threshold", 10 },
        { "immunities", new string[] { "cold", "poison", "mind" } }, { "weaknesses", new string[] { "fire×1.5" } },
        { "traits", new string[] { "冰霜之躯：近战攻击者受1d6冰霜+减速", "冰锥风暴：锥形4格6d6冰霜，冷却3回合" } },
        { "description", "极寒的冰元素体，触碰即冰冻。" },
    };

    public static Godot.Collections.Dictionary EliteDemonGuard() => new()
    {
        { "template_id", "elite_demon_guard" }, { "name", "恶魔卫士" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 36 },
        { "attr_weights", ToVariantArray(WTank) },
        { "base_hp", 14 }, { "ac_bonus", 4 }, { "move_range", 5 }, { "initiative_bonus", 1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 100 }, { "natural_dr_threshold", 11 },
        { "resistances", new string[] { "magic", "fire", "cold" } }, { "immunities", new string[] { "poison", "fear" } },
        { "traits", new string[] { "魔法抗性：法术豁免+3", "重击：暴击范围18-20", "恐惧光环：3格内敌方恐惧" } },
        { "description", "身披甲壳的深渊卫士，魔法几乎无法伤害它。" },
    };

    public static Godot.Collections.Dictionary EliteFrostWitch() => new()
    {
        { "template_id", "elite_frost_witch" }, { "name", "冰霜魔女" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 36 },
        { "attr_weights", ToVariantArray(WMage) },
        { "base_hp", 10 }, { "ac_bonus", 4 }, { "move_range", 5 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
        { "resistances", new string[] { "nonmagical_physical" } }, { "immunities", new string[] { "cold", "poison", "mind" } },
        { "weaknesses", new string[] { "fire×1.5" } },
        { "traits", new string[] { "冰霜光环：周围1格每回合受1d6冰霜", "不死之身：HP归零化为冰雕3回合后满血复活，火焰可阻止" } },
        { "description", "操纵冰霜的魔女，不碎冰就无法真正杀死她。" },
    };

    public static Godot.Collections.Dictionary EliteShadowInquisitor() => new()
    {
        { "template_id", "elite_shadow_inquisitor" }, { "name", "暗影审判官" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 30 },
        { "attr_weights", ToVariantArray(WLeader) },
        { "base_hp", 10 }, { "ac_bonus", 5 }, { "move_range", 5 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "resistances", new string[] { "necrotic" } }, { "immunities", new string[] { "fear", "charm" } },
        { "traits", new string[] { "暗影步：传送4格", "指挥光环：3格内友方攻击+1", "不屈信仰：免疫恐惧和魅惑" } },
        { "description", "暗影教团的精锐审判官，暗影步绕后集火。" },
    };

    public static Godot.Collections.Dictionary EliteNightmareBeast() => new()
    {
        { "template_id", "elite_nightmare_beast" }, { "name", "梦魇兽" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 30 },
        { "attr_weights", ToVariantArray(WCunning) },
        { "base_hp", 10 }, { "ac_bonus", 2 }, { "move_range", 7 }, { "initiative_bonus", 4 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "resistances", new string[] { "magic", "physical" } }, { "immunities", new string[] { "mind", "fear" } },
        { "traits", new string[] { "梦魇之体：50%闪避非魔法攻击", "心灵侵蚀：攻击附带WIS DC14恐惧", "虚化：1次/战斗免疫所有伤害1回合" } },
        { "description", "从噩梦中诞生的生物，以恐惧为食。" },
    };

    public static Godot.Collections.Dictionary EliteMinotaurChieftain() => new()
    {
        { "template_id", "elite_minotaur_chieftain" }, { "name", "牛头人酋长" },
        { "enemy_type", (int)UnitData.EnemyType.Giant }, { "level", 36 },
        { "attr_weights", ToVariantArray([3.0f, 1.0f, 2.5f, 0.5f, 1.5f, 1.0f]) },
        { "base_hp", 14 }, { "ac_bonus", 4 }, { "move_range", 8 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Intimidate },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 90 }, { "natural_dr_threshold", 10 },
        { "traits", new string[] { "冲锋领主：冲锋伤害+3d6", "怒吼：4格内敌方攻击-2持续2回合", "领袖光环：友方攻击+2", "厚皮：物理伤害-3" } },
        { "description", "牛头人部落的至强战神，冲锋时如山崩地裂。" },
    };

    // ========================================
    // 构造体模板
    // ========================================

    public static Godot.Collections.Dictionary ConstructWoodenSentinel() => new()
    {
        { "template_id", "con_wooden_sentinel" }, { "name", "木质哨兵" },
        { "enemy_type", (int)UnitData.EnemyType.Construct }, { "level", 18 },
        { "attr_weights", ToVariantArray(WConstruct) },
        { "base_hp", 10 }, { "ac_bonus", 4 }, { "move_range", 5 }, { "initiative_bonus", -1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Territorial },
        { "natural_dr", 50 }, { "natural_dr_threshold", 6 },
        { "immunities", new string[] { "poison", "mind", "fear", "fatigue", "instant_death" } },
        { "weaknesses", new string[] { "fire×1.5" } },
        { "traits", new string[] { "魔法抗性：法术豁免+2", "不动如山：免疫推撞、缚足" } },
        { "description", "古代遗留的木质守护者，遵循古老指令守卫特定区域。" },
    };

    public static Godot.Collections.Dictionary ConstructStoneGolem() => new()
    {
        { "template_id", "con_stone_golem" }, { "name", "石魔像" },
        { "enemy_type", (int)UnitData.EnemyType.Construct }, { "level", 24 },
        { "attr_weights", ToVariantArray(WConstruct) },
        { "base_hp", 14 }, { "ac_bonus", 6 }, { "move_range", 4 }, { "initiative_bonus", -2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Territorial },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 160 }, { "natural_dr_threshold", 14 },
        { "immunities", new string[] { "poison", "mind", "fatigue", "instant_death", "necrotic" } },
        { "weaknesses", new string[] { "lightning_ac_minus_2" } },
        { "traits", new string[] { "魔法免疫：2环以下法术完全免疫", "不动如山：免疫一切控制" } },
        { "description", "石质构造体，缓慢但坚不可摧。" },
    };

    // ========================================
    // 首领模板（等级 42~66）
    // ========================================

    public static Godot.Collections.Dictionary BossIronGolem() => new()
    {
        { "template_id", "boss_iron_golem" }, { "name", "钢铁魔像" },
        { "enemy_type", (int)UnitData.EnemyType.Construct }, { "level", 42 },
        { "attr_weights", ToVariantArray(WConstruct) },
        { "base_hp", 16 }, { "ac_bonus", 8 }, { "move_range", 4 }, { "initiative_bonus", -3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Territorial },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 200 }, { "natural_dr_threshold", 16 },
        { "immunities", new string[] { "poison", "mind", "fatigue", "fire", "cold", "necrotic", "instant_death" } },
        { "weaknesses", new string[] { "lightning_ac_minus_3" } },
        { "traits", new string[] { "魔法免疫：3环以下法术完全免疫", "不动如山：免疫一切控制", "铁拳：攻击附带倒地" } },
        { "description", "全钢铁铸造的战争兵器，几乎无懈可击。" },
    };

    public static Godot.Collections.Dictionary BossOgreChief() => new()
    {
        { "template_id", "boss_ogre_chief" }, { "name", "食人魔酋长" },
        { "enemy_type", (int)UnitData.EnemyType.Giant }, { "level", 42 },
        { "attr_weights", ToVariantArray([3.0f, 1.0f, 2.5f, 0.5f, 1.5f, 1.0f]) },
        { "base_hp", 16 }, { "ac_bonus", 3 }, { "move_range", 7 }, { "initiative_bonus", 0 },
        { "ai_strategy", (int)UnitData.AIStrategy.Intimidate },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 120 }, { "natural_dr_threshold", 12 },
        { "traits", new string[] { "冲锋：冲锋伤害+2d8", "怒吼：4格内敌方恐惧2回合", "领袖光环：友方攻击+2" } },
        { "description", "食人魔部落的至高酋长，一击能劈开城墙。" },
    };

    public static Godot.Collections.Dictionary BossDeathGeneral() => new()
    {
        { "template_id", "boss_death_general" }, { "name", "死亡将军" },
        { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 48 },
        { "attr_weights", ToVariantArray(WLeader) },
        { "base_hp", 14 }, { "ac_bonus", 6 }, { "move_range", 6 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "natural_dr", 130 }, { "natural_dr_threshold", 13 },
        { "immunities", new string[] { "poison", "mind", "fear", "necrotic" } },
        { "traits", new string[] { "召唤亡灵：每2回合召唤2个骷髅战士", "死亡光环：3格内敌方每回合受1d6暗影", "指挥：友方亡灵攻击+2" } },
        { "description", "统御亡灵军团的黑暗将军，战场上的不死指挥官。" },
    };

    public static Godot.Collections.Dictionary BossHillGiant() => new()
    {
        { "template_id", "boss_hill_giant" }, { "name", "丘陵巨人" },
        { "enemy_type", (int)UnitData.EnemyType.Giant }, { "level", 48 },
        { "attr_weights", ToVariantArray([3.0f, 0.5f, 3.0f, 0.5f, 1.0f, 0.5f]) },
        { "base_hp", 18 }, { "ac_bonus", 2 }, { "move_range", 8 }, { "initiative_bonus", -2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "creature_size", (int)UnitData.CreatureSize.Huge },
        { "natural_dr", 150 }, { "natural_dr_threshold", 14 },
        { "traits", new string[] { "投石：射程8格4d6钝击", "践踏：移动路径上的敌方受2d6+倒地" } },
        { "description", "体型如山的巨人，随手抛出的巨石能砸毁一座塔楼。" },
    };

    public static Godot.Collections.Dictionary BossAbyssalDemon() => new()
    {
        { "template_id", "boss_abyssal_demon" }, { "name", "深渊恶魔" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 54 },
        { "attr_weights", ToVariantArray([2.0f, 1.5f, 2.0f, 1.5f, 1.0f, 1.5f]) },
        { "base_hp", 14 }, { "ac_bonus", 5 }, { "move_range", 6 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 160 }, { "natural_dr_threshold", 15 },
        { "resistances", new string[] { "nonmagical_physical" } }, { "immunities", new string[] { "fire", "poison", "fear" } },
        { "traits", new string[] { "魔法抗性：法术豁免+3", "恐惧光环：5格内敌方恐惧", "混沌吐息：锥形4格6d8火焰+暗影" } },
        { "description", "来自深渊的强大恶魔，散发着令人窒息的恐惧。" },
    };

    public static Godot.Collections.Dictionary BossAncientStoneGolem() => new()
    {
        { "template_id", "boss_ancient_stone_golem" }, { "name", "远古石魔像" },
        { "enemy_type", (int)UnitData.EnemyType.Construct }, { "level", 54 },
        { "attr_weights", ToVariantArray(WConstruct) },
        { "base_hp", 20 }, { "ac_bonus", 8 }, { "move_range", 4 }, { "initiative_bonus", -3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Territorial },
        { "creature_size", (int)UnitData.CreatureSize.Huge },
        { "natural_dr", 250 }, { "natural_dr_threshold", 18 },
        { "immunities", new string[] { "poison", "mind", "fatigue", "instant_death", "necrotic", "fire", "cold" } },
        { "weaknesses", new string[] { "lightning_ac_minus_4", "thunder_shatter" } },
        { "traits", new string[] { "魔法免疫：4环以下法术完全免疫", "不动如山：免疫一切控制", "地裂拳：攻击附带半径1格震动+倒地" } },
        { "description", "远古文明遗留的终极守护者，近乎无敌的防御。" },
    };

    public static Godot.Collections.Dictionary BossNecromancer() => new()
    {
        { "template_id", "boss_necromancer" }, { "name", "死灵法师" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 60 },
        { "attr_weights", ToVariantArray(WMage) },
        { "base_hp", 10 }, { "ac_bonus", 4 }, { "move_range", 5 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
        { "resistances", new string[] { "necrotic" } }, { "immunities", new string[] { "poison", "fear" } },
        { "traits", new string[] { "亡灵召唤：每回合召唤1个骷髅战士（上限6）", "暗影箭：射程6格3d6暗影", "死亡之握：DC16强韧束缚1回合" } },
        { "description", "操纵死亡之力的大法师，亡灵军团的主宰。" },
    };

    public static Godot.Collections.Dictionary BossBanshee() => new()
    {
        { "template_id", "boss_banshee" }, { "name", "女妖" },
        { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 60 },
        { "attr_weights", ToVariantArray(WCunning) },
        { "base_hp", 10 }, { "ac_bonus", 3 }, { "move_range", 8 }, { "initiative_bonus", 5 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "immunities", new string[] { "poison", "mind", "necrotic", "cold", "nonmagical_physical" } },
        { "weaknesses", new string[] { "arcane×1.5" } },
        { "traits", new string[] { "幽灵之躯：50%闪避非魔法攻击", "哀嚎：全场DC16WIS恐惧2回合，冷却3回合", "生命汲取：攻击恢复伤害50%HP" } },
        { "description", "怨念凝聚的幽灵，哀嚎声能令听到的人恐惧至死。" },
    };

    // ========================================
    // 领主模板（等级 48~72）
    // ========================================

    public static Godot.Collections.Dictionary LordKnightCommander() => new()
    {
        { "template_id", "lord_knight_commander" }, { "name", "骑士团长" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 48 },
        { "attr_weights", ToVariantArray(WLeader) },
        { "base_hp", 14 }, { "ac_bonus", 6 }, { "move_range", 6 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "resistances", new string[] { "necrotic" } }, { "immunities", new string[] { "fear" } },
        { "traits", new string[] { "领袖光环：5格内友方攻击+2", "守护誓言：对亡灵伤害+2d8", "铁壁术：每战1次完全格挡" } },
        { "description", "王国的守护者，率领骑士团冲锋陷阵。" },
    };

    public static Godot.Collections.Dictionary LordShadowDominus() => new()
    {
        { "template_id", "lord_shadow_dominus" }, { "name", "暗影领主" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 54 },
        { "attr_weights", ToVariantArray(WCunning) },
        { "base_hp", 12 }, { "ac_bonus", 4 }, { "move_range", 6 }, { "initiative_bonus", 4 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
        { "resistances", new string[] { "necrotic" } }, { "immunities", new string[] { "fear", "charm" } },
        { "traits", new string[] { "暗影步：传送6格", "暗影箭雨：射程8格4d6暗影", "恐惧光环：5格内敌方攻击-2" } },
        { "description", "暗影公会的最高领袖，操纵暗影如臂使指。" },
    };

    public static Godot.Collections.Dictionary LordBarbarianChieftain() => new()
    {
        { "template_id", "lord_barbarian_chieftain" }, { "name", "蛮族大酋长" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 54 },
        { "attr_weights", ToVariantArray(WMeleeBruiser) },
        { "base_hp", 16 }, { "ac_bonus", 3 }, { "move_range", 7 }, { "initiative_bonus", 1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "traits", new string[] { "狂暴：HP<50%时伤害+3，AC-2", "战吼：全场友方攻击+3持续2回合", "不屈：首次致死伤害时1HP存活" } },
        { "description", "蛮荒之地最强的战士，狂暴时如同一头猛兽。" },
    };

    public static Godot.Collections.Dictionary LordElfRangerGeneral() => new()
    {
        { "template_id", "lord_elf_ranger_general" }, { "name", "精灵游侠将军" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 60 },
        { "attr_weights", ToVariantArray(WRangedAgility) },
        { "base_hp", 10 }, { "ac_bonus", 4 }, { "move_range", 7 }, { "initiative_bonus", 5 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "resistances", new string[] { "magic" } }, { "immunities", new string[] { "fear" } },
        { "traits", new string[] { "三连射：每回合3次远程攻击", "自然之助：森林地形额外+2AC", "暗夜之眼：夜间无惩罚" } },
        { "description", "精灵王国最出色的游侠将军，三箭齐发无人能避。" },
    };

    public static Godot.Collections.Dictionary LordDwarfFortressKing() => new()
    {
        { "template_id", "lord_dwarf_fortress_king" }, { "name", "矮人堡王" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 60 },
        { "attr_weights", ToVariantArray(WTank) },
        { "base_hp", 18 }, { "ac_bonus", 7 }, { "move_range", 4 }, { "initiative_bonus", -1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Territorial },
        { "resistances", new string[] { "fire", "physical" } }, { "immunities", new string[] { "poison", "fear" } },
        { "traits", new string[] { "矮人韧性：HP+1/级", "铁壁：防御时AC+6", "熔炉之怒：反击伤害+2d6火焰" } },
        { "description", "矮人王国的铁壁堡王，防御无人能破。" },
    };

    public static Godot.Collections.Dictionary LordDesertSultan() => new()
    {
        { "template_id", "lord_desert_sultan" }, { "name", "沙漠苏丹" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 66 },
        { "attr_weights", ToVariantArray(WMage) },
        { "base_hp", 12 }, { "ac_bonus", 4 }, { "move_range", 5 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
        { "resistances", new string[] { "fire" } }, { "immunities", new string[] { "fear" } },
        { "traits", new string[] { "沙暴术：全场非友方视距-4持续2回合", "火焰环：被近战攻击时攻击者受2d6火焰", "沙漠之主：沙地地形移动+2" } },
        { "description", "沙漠帝国的苏丹，操纵沙暴和火焰的大法师。" },
    };

    public static Godot.Collections.Dictionary LordLichKing() => new()
    {
        { "template_id", "lord_lich_king" }, { "name", "巫妖王" },
        { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 72 },
        { "attr_weights", ToVariantArray(WMage) },
        { "base_hp", 14 }, { "ac_bonus", 6 }, { "move_range", 5 }, { "initiative_bonus", 4 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
        { "resistances", new string[] { "nonmagical_physical", "necrotic", "cold" } },
        { "immunities", new string[] { "poison", "mind", "fear", "instant_death" } },
        { "traits", new string[] { "亡灵大军：每回合召唤2个骷髅战士（上限8）", "死亡凝视：射程6格DC18强韧即死", "命匣：命匣存在时无法被消灭" } },
        { "description", "永生的巫妖之王，只有摧毁命匣才能真正杀死他。" },
    };

    public static Godot.Collections.Dictionary LordPirateQueen() => new()
    {
        { "template_id", "lord_pirate_queen" }, { "name", "海盗女王" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 48 },
        { "attr_weights", ToVariantArray([1.5f, 2.5f, 1.5f, 1.0f, 1.5f, 2.0f]) },
        { "base_hp", 12 }, { "ac_bonus", 4 }, { "move_range", 7 }, { "initiative_bonus", 5 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "traits", new string[] { "双枪连射：每回合2次远程攻击", "烟雾弹：1次/战斗全体友方隐身1回合", "海上女王：水域地形移动无惩罚" } },
        { "description", "统治七海的海盗女王，双枪之下无人幸免。" },
    };

    public static Godot.Collections.Dictionary LordBalor() => new()
    {
        { "template_id", "lord_balor" }, { "name", "炎魔巴尔" },
        { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 66 },
        { "attr_weights", ToVariantArray([2.0f, 1.5f, 2.0f, 2.0f, 1.5f, 2.0f]) },
        { "base_hp", 16 }, { "ac_bonus", 5 }, { "move_range", 6 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Berserk },
        { "creature_size", (int)UnitData.CreatureSize.Large },
        { "natural_dr", 160 }, { "natural_dr_threshold", 15 },
        { "resistances", new string[] { "nonmagical_physical" } },
        { "immunities", new string[] { "fire", "poison", "fear" } },
        { "traits", new string[] { "火焰鞭笞：射程4格拖拽至相邻+3d6火焰+缚足", "死亡之触：DC16强韧HP减半", "爆燃：死亡时全场8d6火焰" } },
        { "description", "深渊最强的炎魔，死亡时爆发出毁灭性的火焰。" },
    };

    // ========================================
    // 冒险者模板（等级 6~30）
    // ========================================

    public static Godot.Collections.Dictionary AdventurerNoviceMercenary() => new()
    {
        { "template_id", "adv_novice_merc" }, { "name", "新手佣兵" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 6 },
        { "attr_weights", ToVariantArray(WMeleeBruiser) },
        { "base_hp", 8 }, { "ac_bonus", 1 }, { "move_range", 5 }, { "initiative_bonus", 0 },
        { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
        { "description", "刚入行的佣兵，装备简陋但干劲十足。" },
    };

    public static Godot.Collections.Dictionary AdventurerVeteranHunter() => new()
    {
        { "template_id", "adv_veteran_hunter" }, { "name", "老练猎人" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 18 },
        { "attr_weights", ToVariantArray(WRangedAgility) },
        { "base_hp", 8 }, { "ac_bonus", 2 }, { "move_range", 5 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "traits", new string[] { "追踪者：揭示3格内隐身单位", "陷阱专家：设置1个陷阱/战斗" } },
        { "description", "经验丰富的猎人，擅长远程攻击和陷阱。" },
    };

    public static Godot.Collections.Dictionary AdventurerBattleMage() => new()
    {
        { "template_id", "adv_battle_mage" }, { "name", "战斗法师" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 24 },
        { "attr_weights", ToVariantArray(WMage) },
        { "base_hp", 7 }, { "ac_bonus", 1 }, { "move_range", 5 }, { "initiative_bonus", 2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "resistances", new string[] { "magic" } },
        { "traits", new string[] { "法术亲和：施法DC+2", "魔力涌动：每回合恢复1点魔力" } },
        { "description", "受过军事训练的法师，能在战场上释放毁灭性法术。" },
    };

    public static Godot.Collections.Dictionary AdventurerElitePaladin() => new()
    {
        { "template_id", "adv_elite_paladin" }, { "name", "精英守护骑士" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 30 },
        { "attr_weights", ToVariantArray(WLeader) },
        { "base_hp", 12 }, { "ac_bonus", 5 }, { "move_range", 5 }, { "initiative_bonus", 0 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "resistances", new string[] { "necrotic" } }, { "immunities", new string[] { "fear", "disease" } },
        { "traits", new string[] { "守护誓言：对亡灵伤害+2d8", "光之庇护：被攻击时5%完全闪避" } },
        { "description", "坚定的守护骑士，以奥术之力守护同伴。" },
    };

    public static Godot.Collections.Dictionary AdventurerWarDrumShaman() => new()
    {
        { "template_id", "adv_war_drum_shaman" }, { "name", "战鼓萨满" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 18 },
        { "attr_weights", ToVariantArray([1.0f, 1.0f, 1.5f, 1.5f, 2.5f, 1.0f]) },
        { "base_hp", 10 }, { "ac_bonus", 1 }, { "move_range", 5 }, { "initiative_bonus", 1 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "resistances", new string[] { "magic" } }, { "immunities", new string[] { "fear" } },
        { "traits", new string[] { "先祖之灵：每回合恢复2HP给最低HP友方", "自然链接：自然地形移动+2" } },
        { "description", "部落的灵魂导师，敲响战鼓激励同伴。" },
    };

    public static Godot.Collections.Dictionary AdventurerHeavyMercenary() => new()
    {
        { "template_id", "adv_heavy_mercenary" }, { "name", "重装佣兵" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 24 },
        { "attr_weights", ToVariantArray(WTank) },
        { "base_hp", 14 }, { "ac_bonus", 6 }, { "move_range", 4 }, { "initiative_bonus", -2 },
        { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
        { "resistances", new string[] { "physical" } },
        { "traits", new string[] { "铁壁：防御时AC+4", "不屈：首次致死伤害时1HP存活" } },
        { "description", "身穿重甲的防御型佣兵，如同一座不可动摇的堡垒。" },
    };

    public static Godot.Collections.Dictionary AdventurerBard() => new()
    {
        { "template_id", "adv_bard" }, { "name", "吟游诗人" },
        { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 18 },
        { "attr_weights", ToVariantArray(WCunning) },
        { "base_hp", 7 }, { "ac_bonus", 1 }, { "move_range", 5 }, { "initiative_bonus", 3 },
        { "ai_strategy", (int)UnitData.AIStrategy.Cautious },
        { "immunities", new string[] { "charm" } },
        { "traits", new string[] { "音乐之力：友方攻击+2持续2回合", "鼓舞人心：友方士气+10" } },
        { "description", "用音乐改变战场的吟游诗人。" },
    };

    // ========================================
    // 传奇模板（等级 78~120）
    // ========================================

    public static Godot.Collections.Dictionary LegendaryYoungRedDragon()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_young_red_dragon" }, { "name", "少年红龙" },
            { "enemy_type", (int)UnitData.EnemyType.Dragon }, { "level", 78 },
            { "attr_weights", ToVariantArray(WDragon) },
            { "base_hp", 16 }, { "ac_bonus", 6 }, { "move_range", 7 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 180 }, { "natural_dr_threshold", 16 },
            { "immunities", new string[] { "fire" } },
            { "traits", new string[] { "龙族恐惧", "飞行", "吐息：锥形4格8d6火焰冷却3回合" } },
            { "description", "刚刚成年的红龙，吐息已能熔化钢铁。" },
        };
        return d;
    }

    public static Godot.Collections.Dictionary LegendaryAdultRedDragon()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_adult_red_dragon" }, { "name", "成年红龙" },
            { "enemy_type", (int)UnitData.EnemyType.Dragon }, { "level", 90 },
            { "attr_weights", ToVariantArray(WDragon) },
            { "base_hp", 20 }, { "ac_bonus", 8 }, { "move_range", 7 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Huge },
            { "natural_dr", 300 }, { "natural_dr_threshold", 20 },
            { "immunities", new string[] { "fire" } },
            { "traits", new string[] { "龙族恐惧", "厚鳞：物理伤害-5", "飞行", "远古魔法：法术豁免+4" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "尾扫" }, { "cost", 1 }, { "desc", "身后3格扇形DC20强韧倒地+2d10+8钝击" } },
            new() { { "name", "翼击" }, { "cost", 2 }, { "desc", "全方向3格DC20推后4格+倒地" } },
            new() { { "name", "迷你吐息" }, { "cost", 2 }, { "desc", "锥形3格8d6火焰" } },
        ]);
        d["description"] = "翱翔天际的恐怖红龙，吐息足以焚烧一座城镇。";
        return d;
    }

    public static Godot.Collections.Dictionary LegendaryAncientFrostDragon()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_ancient_frost_dragon" }, { "name", "远古冰龙" },
            { "enemy_type", (int)UnitData.EnemyType.Dragon }, { "level", 96 },
            { "attr_weights", ToVariantArray(WDragon) },
            { "base_hp", 20 }, { "ac_bonus", 8 }, { "move_range", 7 }, { "initiative_bonus", 1 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Huge },
            { "natural_dr", 320 }, { "natural_dr_threshold", 20 },
            { "immunities", new string[] { "cold", "poison" } },
            { "traits", new string[] { "龙族恐惧", "冰霜之躯：近战攻击者受2d6冰霜", "飞行", "远古魔法：法术豁免+5" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "冰霜吐息" }, { "cost", 2 }, { "desc", "锥形4格10d6冰霜+冻结1回合" } },
            new() { { "name", "暴风雪" }, { "cost", 3 }, { "desc", "全场8d6冰霜+减速2回合" } },
        ]);
        d["description"] = "沉睡万年的冰龙，苏醒后带来永恒凛冬。";
        return d;
    }

    public static Godot.Collections.Dictionary LegendaryLich()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_lich" }, { "name", "远古巫妖" },
            { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 96 },
            { "attr_weights", ToVariantArray(WMage) },
            { "base_hp", 12 }, { "ac_bonus", 6 }, { "move_range", 5 }, { "initiative_bonus", 4 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "resistances", new string[] { "nonmagical_physical", "necrotic", "cold" } },
            { "immunities", new string[] { "poison", "mind", "fear", "instant_death" } },
            { "traits", new string[] { "命匣存在：无法被消灭", "死亡凝视：射程8格DC20强韧即死", "亡灵大军：每回合召唤3个骷髅" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "暗影箭" }, { "cost", 1 }, { "desc", "射程8格4d8暗影" } },
            new() { { "name", "死亡之触" }, { "cost", 2 }, { "desc", "射程3格DC20强韧HP减半" } },
            new() { { "name", "亡者复生" }, { "cost", 3 }, { "desc", "复活场上所有已亡单位为己方骷髅" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "死亡凝视" }, { "desc", "随机1个敌方DC20强韧着火2回合" } },
            new() { { "name", "亡灵增援" }, { "desc", "召唤4个骷髅战士(Lv.12)" } },
        ]);
        d["description"] = "最古老的巫妖，只有找到并摧毁命匣才能消灭他。";
        return d;
    }

    public static Godot.Collections.Dictionary LegendaryDeathKnightKing()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_death_knight_king" }, { "name", "死亡骑士王" },
            { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 102 },
            { "attr_weights", ToVariantArray(WMeleeBruiser) },
            { "base_hp", 18 }, { "ac_bonus", 8 }, { "move_range", 6 }, { "initiative_bonus", 3 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 250 }, { "natural_dr_threshold", 18 },
            { "resistances", new string[] { "nonmagical_physical" } },
            { "immunities", new string[] { "poison", "mind", "fear", "necrotic", "cold" } },
            { "traits", new string[] { "恐惧光环：6格内敌方恐惧", "寒冰之剑：攻击附带3d6冰霜+冻结", "死亡冲锋：冲锋伤害×2" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "寒冰斩" }, { "cost", 1 }, { "desc", "近战伤害翻倍+3d6冰霜" } },
            new() { { "name", "亡灵号令" }, { "cost", 2 }, { "desc", "所有友方亡灵立即行动1次" } },
            new() { { "name", "死亡风暴" }, { "cost", 3 }, { "desc", "半径3格10d6暗影+冰霜" } },
        ]);
        d["description"] = "亡灵军团至高统帅，寒冰与死亡之力于一身。";
        return d;
    }

    public static Godot.Collections.Dictionary LegendaryLavaLord()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_lava_lord" }, { "name", "熔岩领主" },
            { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 108 },
            { "attr_weights", ToVariantArray([2.0f, 1.0f, 2.5f, 1.5f, 1.0f, 1.5f]) },
            { "base_hp", 18 }, { "ac_bonus", 7 }, { "move_range", 5 }, { "initiative_bonus", 1 },
            { "ai_strategy", (int)UnitData.AIStrategy.Berserk },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 280 }, { "natural_dr_threshold", 19 },
            { "immunities", new string[] { "fire", "poison" } },
            { "weaknesses", new string[] { "cold×1.5" } },
            { "traits", new string[] { "元素裂解：被冰霜攻击后AC-3持续2回合" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "灼热凝视" }, { "cost", 1 }, { "desc", "射程8格，DC21强韧着火2回合" } },
            new() { { "name", "岩浆涌出" }, { "cost", 1 }, { "desc", "周围1格变为熔岩地形" } },
            new() { { "name", "火焰鞭笞" }, { "cost", 2 }, { "desc", "射程4格拖拽至相邻+3d6火焰+缚足" } },
            new() { { "name", "火山之怒" }, { "cost", 3 }, { "desc", "半径2格8d6火焰+地面变熔岩" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "熔岩喷涌" }, { "desc", "随机4格喷出熔岩，4d6火焰+着火" } },
            new() { { "name", "火山震颤" }, { "desc", "随机2格塌陷为熔岩池" } },
            new() { { "name", "烈焰风暴" }, { "desc", "全场非火焰免疫受3d6火焰" } },
        ]);
        d["description"] = "火山核心的元素领主，改造地形为熔岩海。";
        return d;
    }

    public static Godot.Collections.Dictionary LegendaryAbyssalLord()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_abyssal_lord" }, { "name", "深渊领主·谎言之王" },
            { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 108 },
            { "attr_weights", ToVariantArray([2.0f, 1.5f, 2.0f, 2.0f, 1.5f, 2.5f]) },
            { "base_hp", 18 }, { "ac_bonus", 8 }, { "move_range", 6 }, { "initiative_bonus", 4 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 300 }, { "natural_dr_threshold", 20 },
            { "resistances", new string[] { "cold", "lightning", "necrotic", "nonmagical_physical" } },
            { "immunities", new string[] { "fire", "poison", "fear" } },
            { "weaknesses", new string[] { "arcane×1.5", "arcane_spell_dc_plus_3" } },
            { "traits", new string[] { "魔法抗性：法术豁免+4", "深渊再生：每回合恢复40HP", "传送：传送至视野内任意位置", "混乱光环：5格内敌方攻击-2", "谎言之盾：50%闪避非魔法远程", "不死之身：命匣碎裂后满血复活1次" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "暗影之语" }, { "cost", 1 }, { "desc", "射程6格DC23 WIS混乱1回合" } },
            new() { { "name", "空间裂隙" }, { "cost", 1 }, { "desc", "传送至视野内任意位置" } },
            new() { { "name", "深渊之手" }, { "cost", 2 }, { "desc", "射程8格拖拽至相邻+2d6暗影+缚足" } },
            new() { { "name", "虚伪之镜" }, { "cost", 2 }, { "desc", "创造自身幻影(HP50)，被击破爆发6d6暗影" } },
            new() { { "name", "谎言崩塌" }, { "cost", 3 }, { "desc", "半径3格12d6暗影+DC23 WIS魅惑2回合" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "深渊凝视" }, { "desc", "随机1个敌方DC23 WIS魅惑1回合" } },
            new() { { "name", "裂隙拉扯" }, { "desc", "靠近边缘敌方被拉向中心2格" } },
            new() { { "name", "低语诱惑" }, { "desc", "全场敌方DC21 WIS攻击-2" } },
            new() { { "name", "恶魔增援" }, { "desc", "召唤2个小恶魔(Lv.30)" } },
        ]);
        d["description"] = "谎言之王，支配凝视控制最强输出打队友。";
        return d;
    }

    public static Godot.Collections.Dictionary LegendaryAwakenedSentinel()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_awakened_sentinel" }, { "name", "觉醒远古机兵·守望者" },
            { "enemy_type", (int)UnitData.EnemyType.Construct }, { "level", 108 },
            { "attr_weights", ToVariantArray(WConstruct) },
            { "base_hp", 22 }, { "ac_bonus", 10 }, { "move_range", 4 }, { "initiative_bonus", -2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Territorial },
            { "creature_size", (int)UnitData.CreatureSize.Gargantuan },
            { "natural_dr", 400 }, { "natural_dr_threshold", 22 },
            { "resistances", new string[] { "physical" } },
            { "immunities", new string[] { "poison", "mind", "fear", "fatigue", "instant_death", "fire", "cold", "necrotic", "lightning", "arcane", "charm", "confusion" } },
            { "traits", new string[] { "远古魔法免疫：5环以下法术完全免疫", "不动如山：免疫一切控制", "减伤外壳：物理伤害-8", "自我修复：每回合恢复40HP", "能量护盾：额外100HP吸收层", "远古守卫：HP<30%时伤害×3" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "检测" }, { "cost", 1 }, { "desc", "全场震动感知，揭示所有隐藏单位" } },
            new() { { "name", "重击" }, { "cost", 1 }, { "desc", "铁拳攻击1次，伤害翻倍" } },
            new() { { "name", "护盾刷新" }, { "cost", 2 }, { "desc", "恢复能量护盾(30HP吸收)" } },
            new() { { "name", "歼灭光束·散射" }, { "cost", 3 }, { "desc", "3道光束各射程6格12d6力场" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "防御矩阵" }, { "desc", "随机2道墙壁升起/降下" } },
            new() { { "name", "能量脉冲" }, { "desc", "全场敌方DC22强韧眩晕1回合" } },
            new() { { "name", "机械修复" }, { "desc", "守望者恢复30HP" } },
            new() { { "name", "陷阱激活" }, { "desc", "随机3格远古陷阱6d6力场+缚足" } },
        ]);
        d["description"] = "远古文明最强守卫，HP<30%时核心暴露伤害×3。";
        return d;
    }

    public static Godot.Collections.Dictionary LegendaryMeteorDragon()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_meteor_dragon" }, { "name", "陨星之龙·末日降临" },
            { "enemy_type", (int)UnitData.EnemyType.Dragon }, { "level", 120 },
            { "attr_weights", ToVariantArray([2.0f, 1.5f, 2.0f, 1.5f, 1.5f, 2.0f]) },
            { "base_hp", 24 }, { "ac_bonus", 10 }, { "move_range", 6 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Gargantuan },
            { "natural_dr", 500 }, { "natural_dr_threshold", 24 },
            { "resistances", new string[] { "nonmagical_physical" } },
            { "immunities", new string[] { "fire", "cold", "lightning", "poison", "fear", "instant_death" } },
            { "traits", new string[] { "龙族恐惧", "厚鳞：物理伤害-8", "陨星之躯：每回合全场受1d6力场震动", "飞行", "远古魔法：法术豁免+5", "陨星共鸣：被攻击时10%概率陨石坠落" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "unique_drop_id", "drop_meteor_dragon_crystal" },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "检测" }, { "cost", 1 }, { "desc", "全场真实视觉" } },
            new() { { "name", "尾扫" }, { "cost", 1 }, { "desc", "身后4格扇形DC24强韧倒地+3d10+10钝击" } },
            new() { { "name", "翼击风暴" }, { "cost", 2 }, { "desc", "全方向4格DC24推后6格+倒地+3d8+10" } },
            new() { { "name", "迷你吐息" }, { "cost", 2 }, { "desc", "锥形4格12d6火焰DC24 DEX半伤" } },
            new() { { "name", "星陨碎片" }, { "cost", 2 }, { "desc", "3个目标各受8d6力场DC24 DEX半伤" } },
            new() { { "name", "龙族威压" }, { "cost", 3 }, { "desc", "半径6格DC24 WIS恐惧2回合+麻痹1回合" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "陨石雨" }, { "desc", "随机6格坠落陨石6d6火焰+力场+变深坑" } },
            new() { { "name", "地震" }, { "desc", "全场DC22强韧倒地" } },
            new() { { "name", "放射" }, { "desc", "全场敌方2d6力场辐射无豁免" } },
            new() { { "name", "重力扭曲" }, { "desc", "全场敌方速度-2，飞行高度-1" } },
        ]);
        d["description"] = "阿瓦隆尼亚终极挑战。三阶段Boss：空中轰炸→地面暴怒→超新星倒计时。";
        return d;
    }

    // ========================================
    // 模板查询接口
    // ========================================

    public static List<Godot.Collections.Dictionary> GetGruntTemplates() =>
    [
        GruntGoblinWarrior(), GruntGoblinArcher(),
        GruntSkeletonWarrior(), GruntForestWolf(),
        GruntZombie(), GruntSlime(), GruntImp(),
        GruntLavaSlime(),
    ];

    public static List<Godot.Collections.Dictionary> GetStandardTemplates() =>
    [
        StandardGoblinChieftain(), StandardOcBerserker(),
        StandardGiantSpider(), StandardGhoul(),
        StandardBlackBear(), StandardGiantScorpion(),
        StandardDireWolf(), StandardHarpy(),
        StandardHellhound(), StandardSkeletonArcher(),
        StandardGriffin(), StandardTroll(),
    ];

    public static List<Godot.Collections.Dictionary> GetEliteTemplates() =>
    [
        EliteOgre(), EliteMinotaur(), EliteGargoyle(),
        EliteCorruptedTreant(), EliteLamia(),
        EliteShadowAssassin(), EliteDeathKnight(),
        EliteFireElemental(), EliteIceElemental(),
        EliteDemonGuard(), EliteFrostWitch(),
        EliteShadowInquisitor(), EliteNightmareBeast(),
        EliteMinotaurChieftain(),
    ];

    public static List<Godot.Collections.Dictionary> GetConstructTemplates() =>
    [
        ConstructWoodenSentinel(), ConstructStoneGolem(),
    ];

    public static List<Godot.Collections.Dictionary> GetBossTemplates() =>
    [
        BossIronGolem(), BossOgreChief(),
        BossDeathGeneral(), BossHillGiant(),
        BossAbyssalDemon(), BossAncientStoneGolem(),
        BossNecromancer(), BossBanshee(),
    ];

    public static List<Godot.Collections.Dictionary> GetLordTemplates() =>
    [
        LordKnightCommander(), LordShadowDominus(),
        LordBarbarianChieftain(), LordElfRangerGeneral(),
        LordDwarfFortressKing(), LordDesertSultan(),
        LordLichKing(), LordPirateQueen(),
        LordBalor(),
    ];

    public static List<Godot.Collections.Dictionary> GetMonsterTemplates()
    {
        var result = new List<Godot.Collections.Dictionary>();
        result.AddRange(GetGruntTemplates());
        result.AddRange(GetStandardTemplates());
        result.AddRange(GetEliteTemplates());
        result.AddRange(GetConstructTemplates());
        result.AddRange(GetBossTemplates());
        return result;
    }

    public static List<Godot.Collections.Dictionary> GetAdventurerTemplates() =>
    [
        AdventurerNoviceMercenary(), AdventurerVeteranHunter(),
        AdventurerBattleMage(), AdventurerElitePaladin(),
        AdventurerWarDrumShaman(), AdventurerHeavyMercenary(),
        AdventurerBard(),
    ];

    public static List<Godot.Collections.Dictionary> GetLegendaryTemplates() =>
    [
        LegendaryYoungRedDragon(), LegendaryAdultRedDragon(),
        LegendaryAncientFrostDragon(), LegendaryLich(),
        LegendaryDeathKnightKing(), LegendaryLavaLord(),
        LegendaryAbyssalLord(), LegendaryAwakenedSentinel(),
        LegendaryMeteorDragon(),
    ];

    public static List<Godot.Collections.Dictionary> GetTemplatesByCr(float minCr, float maxCr)
    {
        var result = new List<Godot.Collections.Dictionary>();
        foreach (var tpl in GetAllTemplates())
        {
            float cr = CalculateCrFromTemplate(tpl);
            if (cr >= minCr && cr <= maxCr)
                result.Add(tpl);
        }
        return result;
    }

    // ========================================
    // 工具方法
    // ========================================

    private static float[] ToFloatArray(Godot.Collections.Array arr)
    {
        var result = new float[arr.Count];
        for (int i = 0; i < arr.Count; i++) result[i] = (float)arr[i];
        return result;
    }

    private static Godot.Collections.Array ToVariantArray(float[] arr)
    {
        var result = new Godot.Collections.Array();
        foreach (var f in arr) result.Add(Variant.From(f));
        return result;
    }

    private static Godot.Collections.Array WrapDictArray(Godot.Collections.Dictionary[] arr)
    {
        var result = new Godot.Collections.Array();
        foreach (var d in arr) result.Add(d);
        return result;
    }

    private static string[] ToStringArray(Godot.Collections.Dictionary tpl, string key)
    {
        if (!tpl.ContainsKey(key)) return [];
        var arr = (Godot.Collections.Array)tpl[key];
        var result = new string[arr.Count];
        for (int i = 0; i < arr.Count; i++) result[i] = (string)arr[i];
        return result;
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> CopyDictArray(
        Godot.Collections.Dictionary tpl, string key)
    {
        if (!tpl.ContainsKey(key)) return new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var src = (Godot.Collections.Array)tpl[key];
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var d in src) result.Add(((Godot.Collections.Dictionary)d).Duplicate());
        return result;
    }
}