// UnitTemplateDB.Legendary2.cs
// 扩展传奇生物模板 — DnD 与西幻经典神话/怪物。
// 每个生物都标注其所在的生态环境（biome），用于世界生成时正确放置巢穴/出现点。
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Data;

public static partial class UnitTemplateDB
{
    // ========================================
    // 扩展传奇模板（等级 78~120）
    //
    // 命名规则：Legendary<Name>()
    // 每个模板必须包含：template_id、name、enemy_type、level、attr_weights、
    //                  base_hp、ac_bonus、move_range、initiative_bonus、ai_strategy、
    //                  natural_dr、natural_dr_threshold、traits、description、biome（生态环境）
    // ========================================

    /// <summary>提坦巨人 — 古神时代遗留的山岳巨灵。栖息于雪山与高原。</summary>
    public static Godot.Collections.Dictionary LegendaryStormGiant()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_storm_giant" }, { "name", "风暴巨人" },
            { "enemy_type", (int)UnitData.EnemyType.Giant }, { "level", 102 },
            { "attr_weights", ToVariantArray([3.0f, 1.5f, 2.5f, 1.0f, 1.5f, 1.5f]) },
            { "base_hp", 22 }, { "ac_bonus", 7 }, { "move_range", 6 }, { "initiative_bonus", 1 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Huge },
            { "natural_dr", 280 }, { "natural_dr_threshold", 18 },
            { "resistances", new string[] { "lightning", "thunder", "cold" } },
            { "immunities", new string[] { "lightning" } },
            { "traits", new string[] { "云端步：在云海中行走如履平地", "雷霆冲击：近战附带2d8雷电", "巨人威慑：6格内敌方先攻-2" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "MountainSnow", "Mountain", "Hills" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "雷霆链击" }, { "cost", 1 }, { "desc", "射程6格6d6雷电+连锁2目标" } },
            new() { { "name", "巨石投掷" }, { "cost", 2 }, { "desc", "射程10格8d6钝击+DC22 STR倒地" } },
            new() { { "name", "风暴召唤" }, { "cost", 3 }, { "desc", "全场闪电雨3轮，每轮3d8雷电" } },
        ]);
        d["description"] = "山巅之主，掌控雷霆与狂风的远古巨灵。";
        return d;
    }

    /// <summary>九头蛇 — 沼泽霸主，每被斩首一次便长出两个新头。</summary>
    public static Godot.Collections.Dictionary LegendaryHydra()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_hydra" }, { "name", "九头蛇" },
            { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 90 },
            { "attr_weights", ToVariantArray([3.0f, 1.0f, 3.0f, 0.0f, 1.0f, 1.0f]) },
            { "base_hp", 24 }, { "ac_bonus", 6 }, { "move_range", 5 }, { "initiative_bonus", 0 },
            { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
            { "creature_size", (int)UnitData.CreatureSize.Huge },
            { "natural_dr", 240 }, { "natural_dr_threshold", 16 },
            { "resistances", new string[] { "poison" } },
            { "immunities", new string[] { "poison" } },
            { "weaknesses", new string[] { "fire×1.5", "acid×1.5" } },
            { "traits", new string[] { "九头：每回合可发动多达9次咬击", "再生头颅：被斩首部位若未受火焰/酸液烧灼，2回合后长回2个头", "毒涎：咬击附带2d6毒素" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Swamp", "Bog", "ShallowWater" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "群咬" }, { "cost", 1 }, { "desc", "对相邻所有目标各发起1次咬击" } },
            new() { { "name", "毒雾喷吐" }, { "cost", 2 }, { "desc", "锥形3格6d6毒+中毒2回合" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "沼泽吞噬" }, { "desc", "随机2格变深泥沼，DC20强韧或缚足" } },
            new() { { "name", "瘟疫之雾" }, { "desc", "全场敌方DC20体质或中毒2回合" } },
        ]);
        d["description"] = "九头并生的沼泽巨兽。砍下一头便长出两个，唯有烈焰能终结它。";
        return d;
    }

    /// <summary>魔眼暴君 — 漂浮的中央巨眼与十条触须般的次级眼。</summary>
    public static Godot.Collections.Dictionary LegendaryBeholder()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_beholder" }, { "name", "魔眼暴君" },
            { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 96 },
            { "attr_weights", ToVariantArray([1.5f, 2.0f, 2.0f, 3.0f, 2.0f, 3.0f]) },
            { "base_hp", 18 }, { "ac_bonus", 8 }, { "move_range", 5 }, { "initiative_bonus", 3 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 200 }, { "natural_dr_threshold", 14 },
            { "resistances", new string[] { "arcane", "psychic" } },
            { "immunities", new string[] { "fear", "charm", "petrification" } },
            { "traits", new string[] { "反魔领域：6格圆锥内法术失效", "悬浮：无视地形与高度", "全方位视觉：免疫背刺与隐形" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Wasteland", "Rocky", "Ruins" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "魅惑射线" }, { "cost", 1 }, { "desc", "射程10格DC22 WIS魅惑1回合" } },
            new() { { "name", "石化射线" }, { "cost", 2 }, { "desc", "射程10格DC22体质石化1回合" } },
            new() { { "name", "解离射线" }, { "cost", 3 }, { "desc", "射程10格8d6力场+DC22体质即死" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "现实扭曲" }, { "desc", "整张地图所有距离判定+1偏差" } },
            new() { { "name", "魔法压制" }, { "desc", "全场敌方法术DC-2持续2回合" } },
        ]);
        d["description"] = "妄想塑造现实的远古暴君，反魔领域使其不惧任何法师。";
        return d;
    }

    /// <summary>飞马蛇尾狮 — 鹰首狮身蛇尾的尾翼怪兽。</summary>
    public static Godot.Collections.Dictionary LegendaryChimera()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_chimera" }, { "name", "奇美拉" },
            { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 84 },
            { "attr_weights", ToVariantArray([2.5f, 2.0f, 2.0f, 0.5f, 1.5f, 1.5f]) },
            { "base_hp", 18 }, { "ac_bonus", 6 }, { "move_range", 8 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 180 }, { "natural_dr_threshold", 14 },
            { "resistances", new string[] { "fire" } },
            { "traits", new string[] { "三头：狮+羊+龙，每回合各攻击1次", "飞行", "火焰吐息：龙首吐出锥形2格火焰" } },
            { "legendary_resistance_uses", 1 }, { "legendary_action_points", 2 },
            { "biome", new string[] { "Mountain", "Hills", "Wasteland" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "三头连击" }, { "cost", 1 }, { "desc", "三头分别攻击同一目标" } },
            new() { { "name", "俯冲撕咬" }, { "cost", 2 }, { "desc", "飞行至5格内目标3d10+10撕咬" } },
        ]);
        d["description"] = "三种凶兽的诅咒融合，咆哮回荡于断崖之间。";
        return d;
    }

    /// <summary>蛇发女妖 — 凝视即石化的诅咒之女。</summary>
    public static Godot.Collections.Dictionary LegendaryMedusa()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_medusa" }, { "name", "美杜莎" },
            { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 78 },
            { "attr_weights", ToVariantArray([2.0f, 2.5f, 1.5f, 2.0f, 2.0f, 2.0f]) },
            { "base_hp", 14 }, { "ac_bonus", 6 }, { "move_range", 6 }, { "initiative_bonus", 3 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Medium },
            { "natural_dr", 100 }, { "natural_dr_threshold", 10 },
            { "immunities", new string[] { "petrification", "poison" } },
            { "traits", new string[] { "石化凝视：被注视者DC20体质石化2回合", "蛇发：相邻敌方每回合受2d6毒", "毒蛇之吻：近战附带2d6毒+中毒2回合" } },
            { "legendary_resistance_uses", 1 }, { "legendary_action_points", 2 },
            { "biome", new string[] { "Ruins", "Wasteland", "Rocky" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "致命凝视" }, { "cost", 1 }, { "desc", "射程6格DC22体质石化1回合" } },
            new() { { "name", "蛇发狂噬" }, { "cost", 2 }, { "desc", "相邻所有敌方各受3d6毒" } },
        ]);
        d["description"] = "曾经的女祭司，被神祇诅咒后化为石化怪物。";
        return d;
    }

    /// <summary>夺心魔领主 — 异界吞噬心智的恐怖存在。</summary>
    public static Godot.Collections.Dictionary LegendaryMindFlayerLord()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_mind_flayer_lord" }, { "name", "夺心魔皇" },
            { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 102 },
            { "attr_weights", ToVariantArray([1.0f, 2.0f, 1.5f, 3.0f, 2.5f, 3.0f]) },
            { "base_hp", 16 }, { "ac_bonus", 6 }, { "move_range", 5 }, { "initiative_bonus", 4 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Medium },
            { "natural_dr", 180 }, { "natural_dr_threshold", 14 },
            { "resistances", new string[] { "psychic", "necrotic" } },
            { "immunities", new string[] { "charm", "fear", "mind" } },
            { "traits", new string[] { "心灵爆破：射程8格圆锥6d8精神+DC22 INT眩晕", "提取脑髓：相邻濒死目标DC22体质即死", "心灵感应：可远程对话所有目标，揭示其位置" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Ruins", "Rocky", "Bog" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "心灵控制" }, { "cost", 1 }, { "desc", "射程6格DC22 INT支配1回合" } },
            new() { { "name", "灵能屏障" }, { "cost", 1 }, { "desc", "下次受到的伤害-50%" } },
            new() { { "name", "梦魇灌输" }, { "cost", 3 }, { "desc", "全场敌方DC22 WIS恐惧2回合" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "灵能波动" }, { "desc", "全场敌方INT/WIS豁免-2持续2回合" } },
            new() { { "name", "脑虫宿主" }, { "desc", "复活已死亡敌方为受控制的傀儡" } },
        ]);
        d["description"] = "异界的心智吞噬者，操纵思想如同操纵肉体。";
        return d;
    }

    /// <summary>克拉肯 — 深海孕育的触手巨兽，能吞食整艘战船。</summary>
    public static Godot.Collections.Dictionary LegendaryKraken()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_kraken" }, { "name", "克拉肯" },
            { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 108 },
            { "attr_weights", ToVariantArray([3.0f, 1.0f, 3.0f, 1.5f, 1.5f, 1.0f]) },
            { "base_hp", 26 }, { "ac_bonus", 8 }, { "move_range", 5 }, { "initiative_bonus", 0 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Gargantuan },
            { "natural_dr", 360 }, { "natural_dr_threshold", 22 },
            { "resistances", new string[] { "cold", "lightning" } },
            { "immunities", new string[] { "poison", "fear", "drowning" } },
            { "traits", new string[] { "水陆两栖", "十触手：每回合可发动至多5次触手攻击", "墨汁喷射：使范围4格目标致盲1回合", "海啸召唤：HP低于30%时触发" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "DeepWater", "ShallowWater" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "触手缠绕" }, { "cost", 1 }, { "desc", "射程6格3d10钝击+缚足" } },
            new() { { "name", "拖入深海" }, { "cost", 2 }, { "desc", "已缚足目标拖向自身相邻+5d6窒息" } },
            new() { { "name", "海啸" }, { "cost", 3 }, { "desc", "全场10d6水浪+DC23 STR倒地" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "暗流" }, { "desc", "敌方移动距离-2持续2回合" } },
            new() { { "name", "深海之雾" }, { "desc", "全场视野3格" } },
        ]);
        d["description"] = "传说中盘踞海底的触手巨兽，可将整支船队拖入深渊。";
        return d;
    }

    /// <summary>翼魔 — 远古恶魔的心腹大将。</summary>
    public static Godot.Collections.Dictionary LegendaryPitFiend()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_pit_fiend" }, { "name", "深坑魔王" },
            { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 108 },
            { "attr_weights", ToVariantArray([2.5f, 2.0f, 2.5f, 2.0f, 1.5f, 2.5f]) },
            { "base_hp", 22 }, { "ac_bonus", 8 }, { "move_range", 6 }, { "initiative_bonus", 3 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 320 }, { "natural_dr_threshold", 20 },
            { "resistances", new string[] { "cold", "nonmagical_physical" } },
            { "immunities", new string[] { "fire", "poison", "fear" } },
            { "traits", new string[] { "飞行", "魔法武器：攻击视为魔法", "恐惧光环：5格内敌方DC20 WIS恐惧", "瘟疫尾扫：尾巴附带2d6毒+中毒" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Wasteland", "Ruins" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "火焰陨星" }, { "cost", 1 }, { "desc", "射程10格8d6火焰+点燃" } },
            new() { { "name", "契约束缚" }, { "cost", 2 }, { "desc", "目标DC22 WIS被铭刻契约：受到伤害时反伤30%" } },
            new() { { "name", "召唤恶魔" }, { "cost", 3 }, { "desc", "召唤2个石像鬼+1个火焰恶魔" } },
        ]);
        d["description"] = "九层地狱军团的将军，以契约束缚凡人灵魂。";
        return d;
    }

    /// <summary>大法师 — 传说级人类施法者，凝聚千年魔力。</summary>
    public static Godot.Collections.Dictionary LegendaryArchmage()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_archmage" }, { "name", "大法师" },
            { "enemy_type", (int)UnitData.EnemyType.Humanoid }, { "level", 96 },
            { "attr_weights", ToVariantArray([0.5f, 1.5f, 1.5f, 3.5f, 2.5f, 2.5f]) },
            { "base_hp", 12 }, { "ac_bonus", 5 }, { "move_range", 6 }, { "initiative_bonus", 4 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Medium },
            { "natural_dr", 100 }, { "natural_dr_threshold", 8 },
            { "resistances", new string[] { "arcane" } },
            { "immunities", new string[] { "charm", "sleep" } },
            { "traits", new string[] { "法术专精：法术DC+3", "法术反制：消耗反应抵消5环以下法术", "传送精通：每回合可短距瞬移3格", "瞬发法术：每回合一次次法术变为快速施放" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Plains", "Hills", "Forest" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "陨石术" }, { "cost", 2 }, { "desc", "半径3格12d6火焰+钝击" } },
            new() { { "name", "时间停滞" }, { "cost", 3 }, { "desc", "暂停所有敌方1回合" } },
            new() { { "name", "石化" }, { "cost", 1 }, { "desc", "目标DC23体质石化2回合" } },
        ]);
        d["description"] = "千年钻研奥术的大法师，一念可移山倒海。";
        return d;
    }

    /// <summary>食人魔王 — 沼泽与丘陵深处的巨型食尸鬼。</summary>
    public static Godot.Collections.Dictionary LegendaryGhastKing()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_ghast_king" }, { "name", "食尸鬼之王" },
            { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 84 },
            { "attr_weights", ToVariantArray([2.5f, 2.0f, 2.0f, 1.0f, 1.0f, 0.5f]) },
            { "base_hp", 18 }, { "ac_bonus", 5 }, { "move_range", 7 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 160 }, { "natural_dr_threshold", 12 },
            { "resistances", new string[] { "necrotic", "cold" } },
            { "immunities", new string[] { "poison", "charm", "sleep", "fear" } },
            { "traits", new string[] { "腐臭：3格内敌方DC18体质削弱", "瘫痪之爪：DC18体质瘫痪1回合", "食尸增益：吞食尸体恢复30HP" } },
            { "legendary_resistance_uses", 1 }, { "legendary_action_points", 2 },
            { "biome", new string[] { "Swamp", "Bog", "Ruins" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "群体瘫痪" }, { "cost", 2 }, { "desc", "全场敌方DC20体质瘫痪1回合" } },
            new() { { "name", "召唤食尸鬼" }, { "cost", 1 }, { "desc", "召唤2个食尸鬼" } },
        ]);
        d["description"] = "腐烂沼泽中称王的食尸鬼，散发的腐臭可使强壮战士不支倒地。";
        return d;
    }

    /// <summary>不死君主 — 远古沙漠王陵中沉睡的木乃伊领主。</summary>
    public static Godot.Collections.Dictionary LegendaryMummyLord()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_mummy_lord" }, { "name", "木乃伊君主" },
            { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 96 },
            { "attr_weights", ToVariantArray([2.0f, 1.0f, 2.5f, 2.5f, 2.0f, 2.0f]) },
            { "base_hp", 18 }, { "ac_bonus", 7 }, { "move_range", 5 }, { "initiative_bonus", 1 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Medium },
            { "natural_dr", 220 }, { "natural_dr_threshold", 16 },
            { "resistances", new string[] { "necrotic", "nonmagical_physical" } },
            { "immunities", new string[] { "poison", "charm", "sleep", "fear" } },
            { "weaknesses", new string[] { "fire×1.5" } },
            { "traits", new string[] { "腐烂凝视：DC20 WIS恐惧+衰弱", "诅咒之触：受击者DC22 WIS被诅咒（治疗效果减半）", "墓穴之尘：相邻敌方每回合受2d6毒" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Sand", "Wasteland", "Ruins" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "沙暴" }, { "cost", 1 }, { "desc", "锥形3格6d6钝击+致盲1回合" } },
            new() { { "name", "墓穴召唤" }, { "cost", 2 }, { "desc", "召唤4个骷髅+2个木乃伊" } },
            new() { { "name", "时间诅咒" }, { "cost", 3 }, { "desc", "目标DC23 WIS老化：每回合损失5HP持续3回合" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "墓穴回响" }, { "desc", "全场敌方DC20 WIS恐惧1回合" } },
            new() { { "name", "尘风" }, { "desc", "全场视野减半" } },
        ]);
        d["description"] = "古王朝最后的法老，凝视便能令勇士衰老死去。";
        return d;
    }

    /// <summary>狮鹫之王 — 群山之巅的统治者，半狮半鹰。</summary>
    public static Godot.Collections.Dictionary LegendaryGriffinKing()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_griffin_king" }, { "name", "狮鹫之王" },
            { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 84 },
            { "attr_weights", ToVariantArray([2.5f, 3.0f, 2.0f, 0.5f, 2.0f, 1.0f]) },
            { "base_hp", 16 }, { "ac_bonus", 7 }, { "move_range", 10 }, { "initiative_bonus", 4 },
            { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 160 }, { "natural_dr_threshold", 12 },
            { "traits", new string[] { "鹰眼：远程攻击+3命中", "飞行+俯冲：俯冲后伤害+50%", "雷霆怒吼：DC18 WIS胆寒（命中-2）" } },
            { "legendary_resistance_uses", 1 }, { "legendary_action_points", 2 },
            { "biome", new string[] { "Mountain", "MountainSnow", "Hills" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "俯冲撕裂" }, { "cost", 1 }, { "desc", "飞行至5格内目标4d10+8钢爪+撕裂流血" } },
            new() { { "name", "群猎号令" }, { "cost", 2 }, { "desc", "召唤3只狮鹫" } },
        ]);
        d["description"] = "群山之巅的霸主，鹰目可见百里之遥的猎物。";
        return d;
    }

    /// <summary>幽影母亲 — 黑暗维度的精神捕食者。</summary>
    public static Godot.Collections.Dictionary LegendaryShadowMatron()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_shadow_matron" }, { "name", "幽影母亲" },
            { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 90 },
            { "attr_weights", ToVariantArray([1.5f, 3.0f, 1.5f, 2.5f, 2.0f, 2.5f]) },
            { "base_hp", 14 }, { "ac_bonus", 6 }, { "move_range", 8 }, { "initiative_bonus", 5 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Medium },
            { "natural_dr", 140 }, { "natural_dr_threshold", 10 },
            { "resistances", new string[] { "cold", "necrotic", "physical" } },
            { "immunities", new string[] { "poison", "charm", "fatigue" } },
            { "weaknesses", new string[] { "radiant×1.5" } },
            { "traits", new string[] { "无形：穿越墙壁", "黑暗潜行：暗处+5闪避", "力量吸取：近战减少目标STR2点", "影分身：HP<50%时分裂出2个10HP副本" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Ruins", "DenseForest", "Bog" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "影爪" }, { "cost", 1 }, { "desc", "近战4d8暗影+削弱" } },
            new() { { "name", "影瞬" }, { "cost", 1 }, { "desc", "瞬移至8格内任意暗处格" } },
            new() { { "name", "幽影低语" }, { "cost", 2 }, { "desc", "全场敌方DC22 WIS恐惧1回合" } },
        ]);
        d["description"] = "暗影界的低语者，在月光被遮蔽的废墟里捕食生者。";
        return d;
    }

    /// <summary>巨型蠕虫 — 沙漠地下的吞食者。</summary>
    public static Godot.Collections.Dictionary LegendaryPurpleWorm()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_purple_worm" }, { "name", "紫色蠕虫" },
            { "enemy_type", (int)UnitData.EnemyType.Beast }, { "level", 90 },
            { "attr_weights", ToVariantArray([3.5f, 1.0f, 3.0f, 0.0f, 1.0f, 0.0f]) },
            { "base_hp", 24 }, { "ac_bonus", 6 }, { "move_range", 6 }, { "initiative_bonus", -1 },
            { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
            { "creature_size", (int)UnitData.CreatureSize.Gargantuan },
            { "natural_dr", 280 }, { "natural_dr_threshold", 18 },
            { "resistances", new string[] { "poison" } },
            { "traits", new string[] { "钻地移动：可从地下任意格涌出", "吞噬：单咬可吞下中型目标，每回合内部4d6酸液", "巨大震动：每回合相邻3格DC18倒地" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Sand", "Wasteland", "Rocky" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "尾刺毒击" }, { "cost", 1 }, { "desc", "射程3格3d12+8穿刺+DC22体质或濒死毒" } },
            new() { { "name", "破地而出" }, { "cost", 2 }, { "desc", "瞬移至8格内任意格+全场DC18倒地" } },
        ]);
        d["description"] = "黄沙之下的庞然怪物，整支驼队消失只在它咽下的瞬间。";
        return d;
    }

    /// <summary>世界之蛇 — 海洋深渊的万古巨蟒。</summary>
    public static Godot.Collections.Dictionary LegendaryWorldSerpent()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_world_serpent" }, { "name", "世界之蛇" },
            { "enemy_type", (int)UnitData.EnemyType.Dragon }, { "level", 120 },
            { "attr_weights", ToVariantArray([3.0f, 1.5f, 3.0f, 2.0f, 1.5f, 2.5f]) },
            { "base_hp", 26 }, { "ac_bonus", 9 }, { "move_range", 7 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Gargantuan },
            { "natural_dr", 460 }, { "natural_dr_threshold", 24 },
            { "resistances", new string[] { "cold", "lightning", "nonmagical_physical" } },
            { "immunities", new string[] { "poison", "charm", "fear" } },
            { "traits", new string[] { "水陆两栖", "古代恐惧光环：8格内DC22 WIS恐惧", "毒雾吐息：锥形5格12d6毒+衰弱", "再生：每回合恢复50HP" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "DeepWater", "ShallowWater", "Bog" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "尾摆" }, { "cost", 1 }, { "desc", "锥形4格5d10钝击+DC22 STR推开4格" } },
            new() { { "name", "毒涡漩" }, { "cost", 2 }, { "desc", "半径3格8d6毒+缚足" } },
            new() { { "name", "缠绕粉碎" }, { "cost", 3 }, { "desc", "目标DC24 STR被缠绕，每回合6d10钝击直至挣脱" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "海啸" }, { "desc", "全场敌方DC22 STR推开2格+倒地" } },
            new() { { "name", "深海寒流" }, { "desc", "全场敌方-2先攻+移动-1持续2回合" } },
        ]);
        d["description"] = "传说中环绕世界海洋的巨蛇，被吟游诗人称为「世界终结者」。";
        return d;
    }

    /// <summary>雪族雪人 — 极北冰原的传说生物。</summary>
    public static Godot.Collections.Dictionary LegendaryYeti()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_yeti_chieftain" }, { "name", "雪人首领" },
            { "enemy_type", (int)UnitData.EnemyType.Giant }, { "level", 84 },
            { "attr_weights", ToVariantArray([3.0f, 2.0f, 2.5f, 0.5f, 1.5f, 0.5f]) },
            { "base_hp", 18 }, { "ac_bonus", 5 }, { "move_range", 7 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 180 }, { "natural_dr_threshold", 14 },
            { "immunities", new string[] { "cold" } },
            { "weaknesses", new string[] { "fire×1.5" } },
            { "traits", new string[] { "雪盲凝视：DC18体质致盲1回合", "冷霜之爪：近战附带2d6冰霜", "雪原疾行：雪地/冰原+2移动" } },
            { "legendary_resistance_uses", 1 }, { "legendary_action_points", 2 },
            { "biome", new string[] { "MountainSnow", "Snow", "Ice" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "冰雪咆哮" }, { "cost", 1 }, { "desc", "锥形3格6d6冰霜+DC18 STR推开2格" } },
            new() { { "name", "群猎号令" }, { "cost", 2 }, { "desc", "召唤2只小雪人" } },
        ]);
        d["description"] = "雪原深处沉默的猎人，凝视便能让人陷入永恒寒冬。";
        return d;
    }

    /// <summary>森林古树 — 古老的森林意志化身。</summary>
    public static Godot.Collections.Dictionary LegendaryAncientTreant()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_ancient_treant" }, { "name", "古树长老" },
            { "enemy_type", (int)UnitData.EnemyType.Construct }, { "level", 96 },
            { "attr_weights", ToVariantArray([3.0f, 0.5f, 3.0f, 2.0f, 2.0f, 1.5f]) },
            { "base_hp", 24 }, { "ac_bonus", 8 }, { "move_range", 4 }, { "initiative_bonus", -2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Territorial },
            { "creature_size", (int)UnitData.CreatureSize.Huge },
            { "natural_dr", 320 }, { "natural_dr_threshold", 20 },
            { "resistances", new string[] { "nonmagical_physical", "cold", "lightning" } },
            { "immunities", new string[] { "poison", "charm", "sleep" } },
            { "weaknesses", new string[] { "fire×1.5" } },
            { "traits", new string[] { "森林之主：在森林地形+3攻击+5HP/回合", "树根束缚：相邻所有敌方DC18 STR缚足", "苏生召唤：每回合可让一棵树长出战斗" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "DenseForest", "Forest", "Jungle" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "树枝鞭笞" }, { "cost", 1 }, { "desc", "射程4格3d10钝击+缚足" } },
            new() { { "name", "森林之怒" }, { "cost", 2 }, { "desc", "召唤3个木灵副手(Lv.20)" } },
            new() { { "name", "大地觉醒" }, { "cost", 3 }, { "desc", "全场地形变为根须，敌方移动-2持续3回合" } },
        ]);
        d["description"] = "万年屹立的古树，森林一脉的最高意志。";
        return d;
    }

    /// <summary>地狱犬王 — 三头地狱犬，火焰猎犬之主。</summary>
    public static Godot.Collections.Dictionary LegendaryCerberus()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_cerberus" }, { "name", "刻耳柏洛斯" },
            { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 90 },
            { "attr_weights", ToVariantArray([3.0f, 2.5f, 2.0f, 0.5f, 2.0f, 1.0f]) },
            { "base_hp", 18 }, { "ac_bonus", 7 }, { "move_range", 9 }, { "initiative_bonus", 3 },
            { "ai_strategy", (int)UnitData.AIStrategy.Reckless },
            { "creature_size", (int)UnitData.CreatureSize.Large },
            { "natural_dr", 220 }, { "natural_dr_threshold", 16 },
            { "resistances", new string[] { "necrotic" } },
            { "immunities", new string[] { "fire", "fear", "poison" } },
            { "traits", new string[] { "三头：每回合发动3次咬击不同目标", "地狱火咆哮：锥形3格8d6火焰", "灵魂猎手：可追踪任何曾对其造成过伤害的目标" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Wasteland", "Ruins" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "撕扯" }, { "cost", 1 }, { "desc", "对相邻所有目标各咬1次" } },
            new() { { "name", "地狱火吐息" }, { "cost", 2 }, { "desc", "锥形3格8d6火焰" } },
            new() { { "name", "嚎叫" }, { "cost", 1 }, { "desc", "全场敌方DC20 WIS无视免疫恐惧2回合" } },
        ]);
        d["description"] = "地狱之门的三头守犬，吼叫能让最勇猛的灵魂颤栗。";
        return d;
    }

    /// <summary>不朽巨像 — 远古魔像，被遗忘文明留下的最后守卫。</summary>
    public static Godot.Collections.Dictionary LegendaryColossus()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_colossus" }, { "name", "不朽巨像" },
            { "enemy_type", (int)UnitData.EnemyType.Construct }, { "level", 114 },
            { "attr_weights", ToVariantArray([3.5f, 0.0f, 3.5f, 0.0f, 0.5f, 0.0f]) },
            { "base_hp", 28 }, { "ac_bonus", 11 }, { "move_range", 4 }, { "initiative_bonus", -3 },
            { "ai_strategy", (int)UnitData.AIStrategy.Territorial },
            { "creature_size", (int)UnitData.CreatureSize.Gargantuan },
            { "natural_dr", 480 }, { "natural_dr_threshold", 24 },
            { "resistances", new string[] { "physical" } },
            { "immunities", new string[] { "poison", "mind", "fear", "fatigue", "instant_death", "charm", "sleep", "petrification" } },
            { "traits", new string[] { "不可阻挡：免疫一切控制", "震地步：每移动一次相邻3格DC22 STR倒地", "护盾矩阵：每回合恢复60HP护盾" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Ruins", "Wasteland", "Mountain" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "巨臂横扫" }, { "cost", 1 }, { "desc", "前方扇形3格5d12钝击+DC22 STR倒地" } },
            new() { { "name", "踏地震击" }, { "cost", 2 }, { "desc", "半径3格6d10+DC22倒地+地震1回合" } },
            new() { { "name", "护盾刷新" }, { "cost", 1 }, { "desc", "立即恢复50HP护盾" } },
        ]);
        d["lair_actions"] = WrapDictArray([
            new() { { "name", "古代石阵激活" }, { "desc", "随机3格石柱升起阻挡视线" } },
            new() { { "name", "守护信标" }, { "desc", "巨像下回合伤害+50%" } },
        ]);
        d["description"] = "比山脉更早矗立的远古巨像，每一步都让大地颤抖。";
        return d;
    }

    /// <summary>死亡圣者 — 被亡灵能量加冕的圣骑士。</summary>
    public static Godot.Collections.Dictionary LegendaryDeathPaladin()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_death_paladin" }, { "name", "死亡圣者" },
            { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 96 },
            { "attr_weights", ToVariantArray([2.5f, 1.5f, 2.5f, 1.0f, 2.0f, 2.0f]) },
            { "base_hp", 18 }, { "ac_bonus", 9 }, { "move_range", 6 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Medium },
            { "natural_dr", 240 }, { "natural_dr_threshold", 17 },
            { "resistances", new string[] { "necrotic", "physical" } },
            { "immunities", new string[] { "poison", "charm", "fear" } },
            { "weaknesses", new string[] { "radiant×1.5" } },
            { "traits", new string[] { "暗黑献祭：每杀死1个目标恢复30HP", "亡灵神恩光环：相邻友方亡灵+2 AC", "腐蚀剑气：射程3格直线4d8暗影" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Ruins", "Wasteland", "Bog" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "暗黑灌注" }, { "cost", 1 }, { "desc", "下次攻击伤害+8d6暗影" } },
            new() { { "name", "亡者集结" }, { "cost", 2 }, { "desc", "复活附近2个亡灵" } },
            new() { { "name", "圣印反转" }, { "cost", 3 }, { "desc", "全场敌方治疗效果反转为伤害2回合" } },
        ]);
        d["description"] = "本应守护光明的圣骑士，堕入黑暗后成为更可怕的存在。";
        return d;
    }

    /// <summary>炎魔 — 古地下之火的元素君主。</summary>
    public static Godot.Collections.Dictionary LegendaryBalrog()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_balrog" }, { "name", "炎魔" },
            { "enemy_type", (int)UnitData.EnemyType.Demon }, { "level", 108 },
            { "attr_weights", ToVariantArray([3.0f, 1.5f, 2.5f, 1.5f, 2.0f, 2.5f]) },
            { "base_hp", 22 }, { "ac_bonus", 8 }, { "move_range", 7 }, { "initiative_bonus", 2 },
            { "ai_strategy", (int)UnitData.AIStrategy.Tactical },
            { "creature_size", (int)UnitData.CreatureSize.Huge },
            { "natural_dr", 320 }, { "natural_dr_threshold", 20 },
            { "resistances", new string[] { "fire" } },
            { "immunities", new string[] { "fire", "poison", "fear" } },
            { "weaknesses", new string[] { "cold×1.5" } },
            { "traits", new string[] { "飞行（火翼）", "炽炎之鞭：射程5格附带3d8火焰+拖拽", "烈焰之剑：近战附带4d6火焰+点燃", "深渊低吼：DC22 WIS恐惧2回合" } },
            { "legendary_resistance_uses", 3 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Mountain", "Ruins", "Wasteland" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "火鞭拖拽" }, { "cost", 1 }, { "desc", "射程5格拖至相邻+3d8火焰" } },
            new() { { "name", "火焰旋风" }, { "cost", 2 }, { "desc", "半径3格8d6火焰+点燃2回合" } },
            new() { { "name", "深渊裂隙" }, { "cost", 3 }, { "desc", "脚下半径2格变熔岩+连续3回合伤害" } },
        ]);
        d["description"] = "古老地下深处的火焰君王，烈焰之鞭可撕裂巨龙之鳞。";
        return d;
    }

    /// <summary>赤红女妖 — 死于不公的灵魂凝聚成的尖啸亡魂。</summary>
    public static Godot.Collections.Dictionary LegendaryBansheeQueen()
    {
        var d = new Godot.Collections.Dictionary
        {
            { "template_id", "legend_banshee_queen" }, { "name", "女妖之女王" },
            { "enemy_type", (int)UnitData.EnemyType.Undead }, { "level", 90 },
            { "attr_weights", ToVariantArray([1.5f, 3.0f, 1.5f, 2.5f, 2.0f, 2.5f]) },
            { "base_hp", 14 }, { "ac_bonus", 6 }, { "move_range", 8 }, { "initiative_bonus", 5 },
            { "ai_strategy", (int)UnitData.AIStrategy.Cunning },
            { "creature_size", (int)UnitData.CreatureSize.Medium },
            { "natural_dr", 140 }, { "natural_dr_threshold", 10 },
            { "resistances", new string[] { "necrotic", "cold", "physical" } },
            { "immunities", new string[] { "charm", "fatigue", "poison" } },
            { "weaknesses", new string[] { "radiant×1.5" } },
            { "traits", new string[] { "无形：穿越实体", "死亡哀嚎：射程10格DC22体质即死（HP<25%目标）", "悲鸣魅惑：DC22 WIS被吸引向其移动" } },
            { "legendary_resistance_uses", 2 }, { "legendary_action_points", 3 },
            { "biome", new string[] { "Bog", "Swamp", "Ruins" } },
        };
        d["legendary_actions"] = WrapDictArray([
            new() { { "name", "锥心尖啸" }, { "cost", 1 }, { "desc", "锥形4格6d8精神+DC20 WIS恐惧" } },
            new() { { "name", "亡魂吸取" }, { "cost", 2 }, { "desc", "射程6格4d8暗影，吸取作为自身HP" } },
            new() { { "name", "亡者哀歌" }, { "cost", 3 }, { "desc", "全场敌方DC22 WIS恐惧+伤害-30%持续2回合" } },
        ]);
        d["description"] = "因极度的悲恸与不甘而生的亡灵，她的尖啸是最后的安魂曲。";
        return d;
    }

    /// <summary>第二批传奇模板列表（合并入 GetLegendaryTemplates 返回值）</summary>
    public static List<Godot.Collections.Dictionary> GetLegendaryTemplatesPart2() =>
    [
        LegendaryStormGiant(), LegendaryHydra(),
        LegendaryBeholder(), LegendaryChimera(),
        LegendaryMedusa(), LegendaryMindFlayerLord(),
        LegendaryKraken(), LegendaryPitFiend(),
        LegendaryArchmage(), LegendaryGhastKing(),
        LegendaryMummyLord(), LegendaryGriffinKing(),
        LegendaryShadowMatron(), LegendaryPurpleWorm(),
        LegendaryWorldSerpent(), LegendaryYeti(),
        LegendaryAncientTreant(), LegendaryCerberus(),
        LegendaryColossus(), LegendaryDeathPaladin(),
        LegendaryBalrog(), LegendaryBansheeQueen(),
    ];

    // ========================================
    // 生态环境查询接口（biome）
    // ========================================

    /// <summary>
    /// 根据 biome 字符串数组查询匹配的传奇生物模板。
    /// 用于世界生成时为不同地形选择合适的传奇生物。
    /// </summary>
    /// <param name="terrain">大地图地形类型</param>
    /// <returns>该地形可出现的传奇生物模板列表</returns>
    public static List<Godot.Collections.Dictionary> GetLegendariesByBiome(HexOverworldTile.TerrainType terrain)
    {
        string terrainName = terrain.ToString();
        var result = new List<Godot.Collections.Dictionary>();
        foreach (var tpl in GetLegendaryTemplates())
        {
            if (!tpl.ContainsKey("biome")) continue;
            var biomes = ToStringArrayPublic(tpl, "biome");
            foreach (var b in biomes)
            {
                if (b == terrainName)
                {
                    result.Add(tpl);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>biome 字段读取的公开包装（避免和私有 ToStringArray 冲突）</summary>
    private static string[] ToStringArrayPublic(Godot.Collections.Dictionary tpl, string key)
    {
        if (!tpl.ContainsKey(key)) return [];
        var arr = (Godot.Collections.Array)tpl[key];
        var result = new string[arr.Count];
        for (int i = 0; i < arr.Count; i++) result[i] = (string)arr[i];
        return result;
    }
}
