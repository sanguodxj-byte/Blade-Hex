// CareerSkillRegistry.cs
// 职业专属技能注册表 — 63 个职业技能的配置数据
// 数据来源: docs/职业专精技能.md
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 职业专属技能注册表 — 职业称号 flags → CareerSkillData 映射
/// </summary>
public static class CareerSkillRegistry
{
    private static Dictionary<int, CareerSkillData>? _registry;
    private static Dictionary<string, CareerSkillData>? _byEffectId;

    public static Dictionary<int, CareerSkillData> Registry
    {
        get
        {
            EnsureBuilt();
            return _registry!;
        }
    }

    public static Dictionary<string, CareerSkillData> ByEffectId
    {
        get
        {
            EnsureBuilt();
            return _byEffectId!;
        }
    }

    private static void EnsureBuilt()
    {
        if (_registry != null) return;
        _registry = new Dictionary<int, CareerSkillData>();
        _byEffectId = new Dictionary<string, CareerSkillData>();
        BuildAll();
        foreach (var kvp in _registry)
            _byEffectId[kvp.Value.EffectId] = kvp.Value;
    }

    public static CareerSkillData? GetByTitleFlags(int flags)
    {
        EnsureBuilt();
        return _registry!.GetValueOrDefault(flags);
    }

    public static CareerSkillData? GetByEffectId(string effectId)
    {
        EnsureBuilt();
        return _byEffectId!.GetValueOrDefault(effectId);
    }

    public static bool HasCareerSkill(int titleFlags) => GetByTitleFlags(titleFlags) != null;

    // ================================================================
    //  构建所有 63 个职业技能
    // ================================================================

    private static void BuildAll()
    {
        // ---- 单属性 (6) ----
        Add(CF.Str, "warrior_armor_break", "碎甲打击", "Armor Break",
            CareerSkillData.SkillType.Active, 5, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "dr_threshold_reduction", 5 },
                { "ac_reduction", 1 },
                { "duration", 2 }
            },
            desc: "近战攻击命中后：目标装甲阈值-5持续2回合；若目标无装甲则闪避-1持续2回合");

        Add(CF.Dex, "ranger_evade_volley", "散射回避", "Evade Volley",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "trigger_limit_per_turn", 1 }
            },
            desc: "被远程命中后可位移1格重新计算命中，每回合1次");

        Add(CF.Con, "guardian_living_wall", "铜墙铁壁", "Living Wall",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "control_range", 6 },
                { "duration", 1 }
            },
            desc: "控制区扩展至周围6格，站在通道中敌方不可穿越控制区");

        Add(CF.Int, "mage_arcane_overload", "以太过载", "Arcane Overload",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "dc_per_mana", 1 },
                { "dice_per_mana", 1 },
                { "range_bonus_mana_threshold", 5 },
                { "range_bonus", 1 }
            },
            desc: "消耗剩余法力，下个法术强度+法力×1，伤害骰+法力，≥5法力范围+1");

        Add(CF.Wis, "assassin_expose_weakness", "弱点暴露", "Expose Weakness",
            CareerSkillData.SkillType.Active, 3, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "crit_threshold_reduction", 3 },
                { "crit_threshold_extra_below_full_hp", 2 },
                { "duration", 2 },
                { "max_targets", 1 }
            },
            desc: "标记目标2回合，被友军攻击时暴击阈值-3，HP<100%时再-2");

        Add(CF.Cha, "bard_battle_hymn", "战歌切换", "Battle Hymn",
            CareerSkillData.SkillType.Active, 2, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "aura_range", 3 },
                { "march_ap_reduction", 1 },
                { "bulwark_ac_bonus", 1 },
                { "fury_damage_bonus", 0.1f }
            },
            desc: "在进军号/铁壁颂/嗜血曲三种战歌间切换，对周围3格友军生效");

        // ---- 双属性 (15) ----
        Add(CF.Str | CF.Dex, "bladedancer_whirling_strike", "连旋斩", "Whirling Strike",
            CareerSkillData.SkillType.Active, 6, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "fan_range", 3 },
                { "damage_multiplier", 0.7f }
            },
            desc: "对正面3格扇形范围敌人各攻击一次，每次命中后旋转位移1格");

        Add(CF.Str | CF.Con, "juggernaut_unstoppable", "不可阻挡", "Unstoppable",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "temp_hp_con_multiplier", 3 }
            },
            desc: "冲锋破障推友不中断，冲锋后获得临时HP=CON修正×3");

        Add(CF.Str | CF.Int, "spellsword_rune_imbue", "符文武器", "Rune Imbue",
            CareerSkillData.SkillType.Active, 3, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "fire_dice_count", 1 },
                { "fire_dice_sides", 6 },
                { "burn_duration", 1 },
                { "burn_dice", 1 },
                { "burn_sides", 6 },
                { "imbue_duration", 3 },
                { "cooldown", 3 }
            },
            desc: "为武器注入符文3回合，下次命中额外1d6火焰+目标燃烧1回合");

        Add(CF.Str | CF.Wis, "executioner_death_sentence", "终结宣告", "Death Sentence",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "target_hp_threshold", 0.3f },
                { "crit_threshold_reduction", 3 },
                { "crit_damage_bonus", 0.5f },
                { "dot_extra_reduction", 1 }
            },
            desc: "攻击HP≤30%目标时暴击阈值-3，暴击倍率+0.5x，流血/中毒目标再-1");

        Add(CF.Str | CF.Cha, "warlord_lead_from_front", "身先士卒", "Lead from Front",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "ally_range", 1 },
                { "max_allies", 99 }
            },
            desc: "冲锋时终点相邻友军免费向同方向移动1格");

        Add(CF.Dex | CF.Con, "duelist_riposte", "以伤换伤", "Riposte",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "counter_damage_multiplier", 1.0f },
                { "crit_ap_recovery", 2 },
                { "trigger_limit_per_turn", 1 }
            },
            desc: "被近战命中后100%伤害反击，暴击回2AP，每回合1次");

        Add(CF.Dex | CF.Int, "arcanearcher_homing_shot", "魔矢追踪", "Homing Shot",
            CareerSkillData.SkillType.Active, 6, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "range_bonus", 3 },
                { "arcane_dice_count", 1 },
                { "arcane_dice_sides", 8 },
                { "ignore_cover", true },
                { "cooldown", 2 }
            },
            desc: "追踪箭矢，无视掩体，射程+3，命中额外1d8奥术伤害");

        Add(CF.Dex | CF.Wis, "falconer_hawks_mark", "鹰眼锁定", "Hawk's Mark",
            CareerSkillData.SkillType.Active, 3, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "duration", 3 },
                { "range_bonus_attacker", 1 },
                { "max_targets", 1 }
            },
            desc: "标记目标3回合，失去地形AC和掩护，远程射程视为+1");

        Add(CF.Dex | CF.Cha, "rogue_misdirection", "声东击西", "Misdirection",
            CareerSkillData.SkillType.Active, 5, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "stealth_duration", 99 },
                { "target_disadvantage_duration", 1 },
                { "cooldown", 2 }
            },
            desc: "强制敌方转向，自身潜行，该目标下回合首次攻击劣势");

        Add(CF.Con | CF.Int, "battlemage_mana_shield", "法力护盾", "Mana Shield",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "damage_reduction_per_mana", 2 },
                { "mana_cost_per_trigger", 1 },
                { "min_reduction", 1 }
            },
            desc: "受伤时消耗1法力减伤INT修正×2点，无限次触发");

        Add(CF.Con | CF.Wis, "veteran_old_timer", "临危不乱", "Old Timer",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "hp_threshold", 0.5f },
                { "ac_bonus", 3 },
                { "counter_damage_multiplier", 1.5f }
            },
            desc: "HP首次降至50%以下时AC+3，反击范围扩展至6格，反击伤害×1.5");

        Add(CF.Con | CF.Cha, "ironcommander_hold_the_line", "坚守阵线", "Hold the Line",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "range", 2 },
                { "ac_bonus", 2 },
                { "dr_bonus_if_defending", 3 },
                { "duration", 1 }
            },
            desc: "周围2格友军不可移动但AC+2免疫恐惧，防御模式额外伤害减免3");

        Add(CF.Int | CF.Wis, "sage_forewarning", "预知回避", "Forewarning",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "mana_cost", 1 },
                { "trigger_limit_per_turn", 1 }
            },
            desc: "每回合首次被攻击时可消耗1法力使攻击获得劣势");

        Add(CF.Int | CF.Cha, "sorcerer_blood_resonance", "血脉共鸣", "Blood Resonance",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "hp_cost_percent", 0.1f },
                { "hp_cost_min", 5 },
                { "mana_recover_multiplier", 2 },
                { "low_hp_multiplier", 3 },
                { "low_hp_threshold", 0.3f }
            },
            desc: "消耗10%HP恢复HP×2法力，HP<30%时免费且恢复×3");

        Add(CF.Wis | CF.Cha, "prophet_fate_intervention", "命运干涉", "Fate Intervention",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "reflect_damage_percent", 0.25f }
            },
            desc: "友军将被打死时可强制重掷伤害骰取低值，仍致死则受25%反射");

        BuildThreeAttribute();
        BuildFourAttribute();
        BuildFiveAttribute();
        BuildSixAttribute();
    }

    // ================================================================
    //  构建第二部分：三属性 (20)
    // ================================================================

    private static void BuildThreeAttribute()
    {
        // ---- 三属性 (20) ----
        Add(CF.Str | CF.Dex | CF.Con, "bruiser_iron_rush", "铁壁冲锋", "Iron Rush",
            CareerSkillData.SkillType.Active, 7, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "charge_range", 3 },
                { "ac_bonus", 3 },
                { "push_allies", true }
            },
            desc: "冲锋3格期间AC+3，命中敌人后AC+3持续到回合结束");

        Add(CF.Str | CF.Dex | CF.Int, "spellweaver_instant_glyph", "瞬发符印", "Instant Glyph",
            CareerSkillData.SkillType.Active, 3, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "heal_dice", "1d4" },
                { "damage_dice", "1d4" },
                { "duration", 3 },
                { "max_glyphs", 2 }
            },
            desc: "放置符印3回合，友军经过恢复1d4，敌军经过受1d4奥术伤害");

        Add(CF.Str | CF.Dex | CF.Wis, "hawkeye_kill_shot", "致命弹道", "Kill Shot",
            CareerSkillData.SkillType.Active, 8, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "crit_threshold_reduction", 4 },
                { "crit_damage_extra", 1.0f },
                { "ap_refund_on_miss", 4 },
                { "cooldown", 3 }
            },
            desc: "远程攻击，暴击阈值-4且暴击+1.0x倍率，未命中返还4AP");

        Add(CF.Str | CF.Dex | CF.Cha, "champion_war_cry_charge", "战吼冲锋", "War Cry Charge",
            CareerSkillData.SkillType.Active, 7, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "morale_bonus", 8 },
                { "advantage_range", 3 },
                { "cooldown", 3 }
            },
            desc: "冲锋攻击，终点周围3格友军士气+8且下回合首次攻击获得优势");

        Add(CF.Str | CF.Con | CF.Int, "ironweaver_rune_barricade", "符文壁垒", "Rune Barricade",
            CareerSkillData.SkillType.Active, 5, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "hp_per_con", 5 },
                { "destroy_aoe_dice", "1d6" },
                { "duration", 3 }
            },
            desc: "相邻格召唤符文屏障HP=CON修正×5，被毁时周围1格敌人受1d6奥术");

        Add(CF.Str | CF.Con | CF.Wis, "skullcrusher_crush_weakpoint", "弱点粉碎", "Crush the Weak Point",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "dr_penetration_bonus", 3 }
            },
            desc: "攻击HP<100%目标额外+已损HP%÷10伤害，DR穿透+3");

        Add(CF.Str | CF.Con | CF.Cha, "conqueror_subjugate", "镇压", "Subjugate",
            CareerSkillData.SkillType.Active, 5, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "morale_damage", 15 },
                { "fear_duration", 1 },
                { "save_dc_base", 10 },
                { "cooldown", 2 }
            },
            desc: "近战命中后目标士气-15且下回合被恐惧，WIS豁免可抵抗");

        Add(CF.Str | CF.Int | CF.Wis, "doomknight_gaze_of_ruin", "毁灭凝视", "Gaze of Ruin",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "crit_threshold_override", 13 },
                { "base_duration", 1 },
                { "debuff_extra_duration", 2 }
            },
            desc: "凝视目标1-2回合，被友军攻击命中时暴击阈值视为13");

        Add(CF.Str | CF.Int | CF.Cha, "overlord_aura_of_dread", "恐惧光环", "Aura of Dread",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "range", 2 },
                { "morale_drain_per_turn", 3 },
                { "morale_threshold_for_disadvantage", -40 }
            },
            desc: "周围2格敌方每回合士气-3，士气≤-40时攻击霸主获得劣势");

        Add(CF.Str | CF.Wis | CF.Cha, "crusader_arcane_charge", "奥术冲锋", "Arcane Charge",
            CareerSkillData.SkillType.Active, 7, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "heal_dice", "1d4" },
                { "trail_duration", 2 },
                { "cooldown", 3 }
            },
            desc: "冲锋无视地形惩罚，路径留奥术痕迹2回合，友军经过恢复1d4");

        Add(CF.Dex | CF.Con | CF.Int, "shadowmage_shadow_swap", "暗影置换", "Shadow Swap",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "max_memory_positions", 3 },
                { "stealth_duration", 1 }
            },
            desc: "与记忆的法术落点交换位置，获得1回合潜行");

        Add(CF.Dex | CF.Con | CF.Wis, "nightstalker_death_mark", "暗杀标记", "Death Mark",
            CareerSkillData.SkillType.Active, 5, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "duration", 2 },
                { "ignore_dr", true },
                { "ap_recovery_on_kill", 3 },
                { "cooldown", 3 }
            },
            desc: "标记目标2回合，对该目标攻击无视DR且获得优势，击杀恢复3AP");

        Add(CF.Dex | CF.Con | CF.Cha, "faceless_identity_theft", "身份窃取", "Identity Theft",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "duration", 1 },
                { "cooldown", 4 }
            },
            desc: "模仿敌方单位外观，AI本回合不主动攻击千面客，攻击时解除");

        Add(CF.Dex | CF.Int | CF.Wis, "stargazer_star_map", "星图定位", "Star Map",
            CareerSkillData.SkillType.Active, 3, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "max_memory_positions", 99 }, // = Mod(INT)
                { "teleport_delay", 1 }  // 下回合开始时传送
            },
            desc: "标记位置，下回合开始时免费传送至该位置，每场可用Mod(INT)次");

        Add(CF.Dex | CF.Int | CF.Cha, "illusionist_mirror_image", "镜像分身", "Mirror Image",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "phantom_ac", 12 },
                { "phantom_hp", 1 },
                { "redirect_chance", 0.5f },
                { "max_phantoms", 2 }
            },
            desc: "生成幻影分身，被攻击时50%概率打到分身上，最多2个");

        Add(CF.Dex | CF.Wis | CF.Cha, "windwalker_tailwind", "顺风传递", "Tailwind",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "teleport_range", 2 },
                { "move_ap_reduction", 1 },
                { "cooldown", 2 }
            },
            desc: "传送视野内友军至自身周围2格，双方本回合移动消耗-1AP/格");

        Add(CF.Con | CF.Int | CF.Wis, "arcanewarden_bulwark_of_lore", "知识壁垒", "Bulwark of Lore",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "dc_reduction", 5 }
            },
            desc: "受法术伤害时智力检定≥法术强度-5则伤害减半");

        Add(CF.Con | CF.Int | CF.Cha, "ironsovereign_iron_law", "铁律", "Iron Law",
            CareerSkillData.SkillType.Active, 6, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "range", 3 },
                { "duration", 2 },
                { "morale_change_divisor", 2 },
                { "cooldown", 4 }
            },
            desc: "周围3格禁止冲锋/潜行，士气变化÷2，持续2回合");

        Add(CF.Con | CF.Wis | CF.Cha, "ironbulwark_martyrs_guard", "殉道守护", "Martyr's Guard",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "damage_share_percent", 0.5f },
                { "range", 1 },
                { "min_triggers", 1 },
                { "hp_threshold_disable", 0.25f }
            },
            desc: "周围1格友军将致死时代替承受50%伤害，每场触发WIS修正次");

        Add(CF.Int | CF.Wis | CF.Cha, "chosenone_twist_of_fate", "命运转折", "Twist of Fate",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "disadvantage_duration", 1 }
            },
            desc: "强制将一次d20结果改为自然20或1，使用后下回合所有检定劣势");
    }

    private static void BuildFourAttribute()
    {
        // ---- 四属性 (15) ----
        Add(CF.Con | CF.Int | CF.Wis | CF.Cha, "archsage_omnibus", "万卷通鉴", "Omnibus",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "advice_ap_cost", 2 },
                { "advice_hit_bonus", 1 },
                { "range", 3 }
            },
            desc: "战斗开始揭示敌方最低豁免，每回合可花2AP给友军攻击+1");

        Add(CF.Dex | CF.Int | CF.Wis | CF.Cha, "zephyrmaster_wind_favor", "风之眷顾", "Wind's Favor",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "move_threshold", 3 },
                { "max_stacks", 3 },
                { "range_per_stack", 1 }
            },
            desc: "每回合移动≥3格获得风痕(最多3层)，每层远程射程+1，被近战命中清零");

        Add(CF.Dex | CF.Con | CF.Wis | CF.Cha, "warchief_feral_instinct", "野性直觉", "Feral Instinct",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "move_ap_reduction", 1 },
                { "beast_advantage", true }
            },
            desc: "在野外地形移动力+1，自动感知潜行单位，对野兽攻击优势");

        Add(CF.Dex | CF.Con | CF.Int | CF.Cha, "shadowlord_puppet_master", "幕后操纵", "Puppet Master",
            CareerSkillData.SkillType.Active, 6, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "range", 2 },
                { "morale_threshold", -40 },
                { "forced_move_range", 2 },
                { "forced_damage_multiplier", 0.5f },
                { "cooldown", 3 }
            },
            desc: "控制士气≤-40的敌方移动2格并攻击最近单位(伤害×0.5)");

        Add(CF.Dex | CF.Con | CF.Int | CF.Wis, "silentdeath_silent_strike", "无声击", "Silent Strike",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "stealth_attack_damage_multiplier", 0.5f }
            },
            desc: "从潜行攻击时可不解除潜行(伤害×0.5)，每场可用DEX修正次");

        Add(CF.Str | CF.Int | CF.Wis | CF.Cha, "lordofruin_harbinger", "毁灭预兆", "Harbinger of Ruin",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "dc_per_hp_percent", 10 },
                { "max_dc_bonus", 5 },
                { "fixed_ac", 10 }
            },
            desc: "每失去10%HP法术强度+1(最多+5)，代价闪避永远固定为10");

        Add(CF.Str | CF.Con | CF.Wis | CF.Cha, "stonesaint_stone_body", "石化之躯", "Stone Body",
            CareerSkillData.SkillType.Active, 5, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "duration", 2 },
                { "dr_threshold_bonus", 3 },
                { "aoe_damage_dice", "1d6" },
                { "aoe_range", 1 }
            },
            desc: "石化2回合不可动/攻击/施法，装甲阈值+3免疫负面，恢复时周围受1d6");

        Add(CF.Str | CF.Con | CF.Int | CF.Cha, "dreadgeneral_iron_grip", "铁腕统御", "Iron Grip",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "range", 2 },
                { "morale_floor", -40 }
            },
            desc: "周围2格友军免疫恐惧/溃逃且士气不低于-40，但不可主动撤退");

        Add(CF.Str | CF.Con | CF.Int | CF.Wis, "voidknight_chains_of_deep", "深渊锁链", "Chains of the Deep",
            CareerSkillData.SkillType.Active, 5, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "duration", 1 },
                { "escape_dc", 15 },
                { "cooldown", 2 }
            },
            desc: "近战命中后目标下回合不可移动且不可防御，力量检定≥15可挣脱");

        Add(CF.Str | CF.Dex | CF.Wis | CF.Cha, "stormbanner_lightning_raid", "闪电突击", "Lightning Raid",
            CareerSkillData.SkillType.Active, 7, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "max_allies", 2 },
                { "move_range", 2 },
                { "cooldown", 3 }
            },
            desc: "自身和周围1格最多2友军同时移动2格后各攻击一次");

        Add(CF.Str | CF.Dex | CF.Int | CF.Cha, "tempestlord_inferno_surge", "烈焰喷涌", "Inferno Surge",
            CareerSkillData.SkillType.Active, 6, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "range", 2 },
                { "fire_dice", "2d6" },
                { "burning_fire_dice", "3d6" },
                { "mana_per_hit", 1 },
                { "cooldown", 3 }
            },
            desc: "周围2格敌方受2d6火焰，每命中1个恢复1法力，燃烧时3d6");

        Add(CF.Str | CF.Dex | CF.Con | CF.Wis, "ironwall_hunter", "铁壁猎手", "Ironwall Hunter",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "track_range", 3 },
                { "damage_bonus", 2 }
            },
            desc: "追踪最近受伤目标，攻击受伤目标额外+2伤害");

        Add(CF.Str | CF.Dex | CF.Con | CF.Int, "myriad_battle mage", "万象魔战", "Myriad Battlemage",
            CareerSkillData.SkillType.Active, 6, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "melee_damage_bonus", 3 },
                { "spell_dc_bonus", 2 },
                { "cooldown", 3 }
            },
            desc: "近战攻击额外+3伤害且法术强度+2，持续到回合结束");

        Add(CF.Str | CF.Dex | CF.Con | CF.Cha, "warking_domination", "战争之王", "War King Domination",
            CareerSkillData.SkillType.Active, 7, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "morale_bonus", 10 },
                { "range", 3 },
                { "cooldown", 3 }
            },
            desc: "怒吼使周围3格友军士气+10，敌方士气-10");

        Add(CF.Str | CF.Dex | CF.Int | CF.Wis, "lone_saint", "独行圣者", "Lone Saint",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "solo_ac_bonus", 2 },
                { "solo_damage_bonus", 2 }
            },
            desc: "周围2格内无友军时AC+2、伤害+2");
    }

    private static void BuildFiveAttribute()
    {
        // ---- 五属性 (6) ----
        Add(CF.Dex | CF.Con | CF.Int | CF.Wis | CF.Cha, "emissary_jack_of_all_trades", "万法通识", "Jack of All Trades",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "bonus_percent", 0.05f }
            },
            desc: "所有大节点数值效果+5%（不适用于Keystone）");

        Add(CF.Str | CF.Con | CF.Int | CF.Wis | CF.Cha, "mountainlord_mountain_stance", "磐石姿态", "Mountain Stance",
            CareerSkillData.SkillType.Active, 4, CareerSkillData.UsageLimit.OncePerBattle,
            effectParams: new Godot.Collections.Dictionary
            {
                { "dr_threshold_bonus", 3 },
                { "next_turn_ac_penalty", 2 }
            },
            desc: "不可移动，装甲阈值+3免疫负面，下回合闪避-2");

        Add(CF.Str | CF.Dex | CF.Int | CF.Wis | CF.Cha, "twilight_walker_stride", "暮光步", "Twilight Stride",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "first_move_no_aoo", true },
                { "can_cross_enemies", true },
                { "heal_dice", "1d4" },
                { "heal_hp_threshold", 0.5f }
            },
            desc: "每回合首次移动不触发借机攻击且可穿越敌方格，HP<50%恢复1d4");

        Add(CF.Str | CF.Dex | CF.Con | CF.Int | CF.Cha, "irontyrant_wrath", "暴君之怒", "Tyrant's Wrath",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "fixed_crit_threshold", 20 }
            },
            desc: "伤害永不因debuff降低，代价暴击阈值固定为20");

        Add(CF.Str | CF.Dex | CF.Con | CF.Int | CF.Wis, "loneshadow_lone_operative", "独行术", "Lone Operative",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "solo_range", 2 },
                { "ally_ac_penalty", 1 },
                { "min_ac", 8 },
                { "solo_ac_bonus", 2 },
                { "solo_move_ap_reduction", 1 },
                { "combat_start_stealth", true }
            },
            desc: "周围2格内每有1友军AC-1，无友军时AC+2移动-1AP/格，战斗开始潜行");

        Add(CF.Str | CF.Dex | CF.Con | CF.Wis | CF.Cha, "wrathavatar_savage_instinct", "野蛮直觉", "Savage Instinct",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.Unlimited,
            effectParams: new Godot.Collections.Dictionary
            {
                { "auto_target_lowest_hp", true },
                { "execute_hp_threshold", 0.2f }
            },
            desc: "自动攻击范围内HP最低的敌人，目标HP≤20%自动获得优势");
    }

    private static void BuildSixAttribute()
    {
        // ---- 全属性 (1) ----
        Add(CF.Str | CF.Dex | CF.Con | CF.Int | CF.Wis | CF.Cha,
            "paragon_hand_of_all", "万能之手", "Hand of All",
            CareerSkillData.SkillType.Passive, 0, CareerSkillData.UsageLimit.PerBattleCount,
            effectParams: new Godot.Collections.Dictionary
            {
                { "max_uses_per_battle", 3 },
                { "effect_multiplier", 0.5f },
                { "duration_reduction", 1 },
                { "min_duration", 1 }
            },
            desc: "每回合可选一种已解锁职业技能临时使用(效果×0.5)，每场最多3次");
    }

    // ================================================================
    //  辅助
    // ================================================================

    private static void Add(
        int titleFlags, string effectId, string displayName, string englishName,
        CareerSkillData.SkillType type, int apCost, CareerSkillData.UsageLimit limitType,
        Godot.Collections.Dictionary effectParams, string desc,
        int cooldown = 0, int maxUses = 1)
    {
        var skill = new CareerSkillData
        {
            SkillId = effectId,
            DisplayName = displayName,
            EnglishName = englishName,
            RequiredTitleFlags = titleFlags,
            Type = type,
            ApCost = apCost,
            LimitType = limitType,
            MaxUses = maxUses,
            Cooldown = cooldown,
            EffectId = effectId,
            Description = desc,
            EffectParams = effectParams
        };
        _registry![titleFlags] = skill;
    }

    /// <summary>属性 flags 常量（与 ClassTitleResolver 一致）</summary>
    private static class CF
    {
        public const int Str = 1;
        public const int Dex = 2;
        public const int Con = 4;
        public const int Int = 8;
        public const int Wis = 16;
        public const int Cha = 32;
    }
}
