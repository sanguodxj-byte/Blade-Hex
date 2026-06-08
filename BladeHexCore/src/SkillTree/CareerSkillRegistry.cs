// CareerSkillRegistry.cs
// 职业专属技能注册表 — v1.0 分阶职业技能 (63 个)
//
// v1.0 设计宪法:
//   1. 一至四属性 = 常驻被动 (Passive)
//   2. 五属性 = 主动(OncePerBattle), 满AP才能用, 消耗最大AP
//   3. 六属性(万象) = 主动(OncePerTurn), 代价型
//   4. 数据权威源: 本文件内联注册, 不再读取 career_skill_configs.json
//
// v0.8 旧版 JSON 配置保留在 career_skill_configs.json 仅供历史参考,不参与 v1 职业技能。
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 职业专属技能注册表 — 职业称号 flags → CareerSkillData 映射 (v1.0)
/// </summary>
public static class CareerSkillRegistry
{
    // ============================================================================
    // 标志位 (与 ClassTitleResolver 保持一致)
    // ============================================================================
    private const int F_STR = 1;
    private const int F_DEX = 2;
    private const int F_CON = 4;
    private const int F_INT = 8;
    private const int F_WIS = 16;
    private const int F_CHA = 32;

    private static Dictionary<int, CareerSkillData>? _registry;
    private static Dictionary<string, CareerSkillData>? _byEffectId;
    private static bool _loaded;

    public static Dictionary<int, CareerSkillData> Registry
    {
        get { EnsureLoaded(); return _registry!; }
    }

    public static Dictionary<string, CareerSkillData> ByEffectId
    {
        get { EnsureLoaded(); return _byEffectId!; }
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _registry = new Dictionary<int, CareerSkillData>();
        _byEffectId = new Dictionary<string, CareerSkillData>();
        RegisterAllSkills();
    }

    public static CareerSkillData? GetByTitleFlags(int flags)
    {
        EnsureLoaded();
        return _registry!.GetValueOrDefault(flags);
    }

    public static CareerSkillData? GetByEffectId(string effectId)
    {
        EnsureLoaded();
        return _byEffectId!.GetValueOrDefault(effectId);
    }

    public static bool HasCareerSkill(int titleFlags) => GetByTitleFlags(titleFlags) != null;

    /// <summary>计算给定 flags 的属性数 (popcount on 6-bit flag)</summary>
    public static int CountAttributes(int flags)
    {
        int count = 0;
        for (int i = 0; i < 6; i++)
            if ((flags & (1 << i)) != 0) count++;
        return count;
    }

    // ============================================================================
    // v1.0 全部 63 个职业技能注册
    // ============================================================================

    private static void RegisterAllSkills()
    {
        // ---- 单属性 Passive (6) ----

        AddPassive(F_STR, "warrior_melee_damage", "战士", "Warrior",
            "近战武器伤害 ×1.2", new Godot.Collections.Dictionary
            { { "melee_damage_multiplier", 1.2f } });

        AddPassive(F_DEX, "ranger_ranged_cover_half", "游侠", "Ranger",
            "远程攻击无视一半障碍物命中减益", new Godot.Collections.Dictionary
            { { "cover_penalty_reduction", 0.5f } });

        AddPassive(F_CON, "guardian_first_hit_reduction", "守卫", "Guardian",
            "每回合首次受到的伤害 -30%", new Godot.Collections.Dictionary
            { { "first_hit_damage_reduction", 0.3f } });

        AddPassive(F_INT, "mage_spell_damage", "法师", "Mage",
            "法术伤害 +15%", new Godot.Collections.Dictionary
            { { "spell_damage_bonus", 0.15f } });

        AddPassive(F_WIS, "assassin_backstab_double", "刺客", "Assassin",
            "背面攻击伤害 ×2", new Godot.Collections.Dictionary
            { { "backstab_damage_multiplier", 2.0f } });

        AddPassive(F_CHA, "bard_ally_damage_aura", "诗人", "Bard",
            "周围 2 格内友军伤害 +5%", new Godot.Collections.Dictionary
            { { "ally_damage_bonus", 0.05f }, { "ally_aura_range", 2 } });

        // ---- 双属性 Passive (15) ----

        AddPassive(F_STR | F_DEX, "blade_dancer_extra_attack", "剑舞者", "Blade Dancer",
            "近战命中后, 强制对另一个相邻敌方发动一次额外攻击(不会再次触发旋斩)",
            new Godot.Collections.Dictionary { { "whirlwind_extra_target", true } });

        AddPassive(F_STR | F_CON, "juggernaut_charge_damage", "重战士", "Juggernaut",
            "移动至少 3 格后发动的近战攻击, 伤害 ×1.5",
            new Godot.Collections.Dictionary { { "min_move_for_bonus", 3 }, { "charge_damage_multiplier", 1.5f } });

        AddPassive(F_STR | F_INT, "spellsword_mana_burn", "魔剑士", "Spellsword",
            "近战攻击时, 消耗当前法力 5%, 命中造成等额额外伤害; 法力为 0 时无效",
            new Godot.Collections.Dictionary { { "mana_cost_pct", 0.05f }, { "convert_ratio", 1.0f } });

        AddPassive(F_STR | F_WIS, "executioner_low_hp_damage", "处刑人", "Executioner",
            "对 HP ≤ 30% 的敌人伤害 +50%",
            new Godot.Collections.Dictionary { { "hp_threshold", 0.3f }, { "damage_bonus", 0.5f } });

        AddPassive(F_STR | F_CHA, "warlord_ally_per_ally_damage", "征讨者", "Warlord",
            "周围 2 格内每有一个友军(含自身), 所有受影响友军造成的伤害 +5%(最多 +20%)",
            new Godot.Collections.Dictionary { { "per_ally_bonus", 0.05f }, { "max_bonus", 0.20f }, { "range", 2 } });

        AddPassive(F_DEX | F_CON, "duelist_riposte_counter", "决斗者", "Duelist",
            "被近战攻击后, 立即对攻击者发动一次近战攻击(需命中判定), 每回合 1 次",
            new Godot.Collections.Dictionary { { "counter_on_melee_hit", true }, { "counter_per_turn", 1 } });

        AddPassive(F_DEX | F_INT, "arcane_archer_sniper_stack", "秘射手", "Arcane Archer",
            "对同一目标连续远程攻击时, 每次命中后下一次伤害 +10%(最多 +30%, 切换目标重置)",
            new Godot.Collections.Dictionary { { "stack_damage_bonus", 0.10f }, { "max_stacks", 3 } });

        AddPassive(F_DEX | F_WIS, "falconer_injured_accuracy", "狩猎者", "Falconer",
            "攻击已受伤的敌人时, 命中 +30%",
            new Godot.Collections.Dictionary { { "injured_accuracy_bonus", 0.30f } });

        AddPassive(F_DEX | F_CHA, "rogue_miss_ap_refund", "游荡者", "Rogue",
            "近战攻击未命中时, 返还 1 AP(不限次数)",
            new Godot.Collections.Dictionary { { "miss_ap_refund", 1 } });

        AddPassive(F_CON | F_INT, "battlemage_mana_shield", "战法师", "Battlemage",
            "受到伤害时, 自动消耗 1 法力抵消 1 点伤害; 法力为 0 时不触发",
            new Godot.Collections.Dictionary { { "mana_to_damage_ratio", 1 }, { "mana_per_trigger", 1 } });

        AddPassive(F_CON | F_WIS, "veteran_low_hp_crit", "苦修者", "Veteran",
            "HP 低于 80% 时, 暴击率 +5%",
            new Godot.Collections.Dictionary { { "hp_threshold", 0.8f }, { "crit_rate_bonus", 0.05f } });

        AddPassive(F_CON | F_CHA, "iron_commander_adjacent_ac", "守御者", "Iron Commander",
            "自身和相邻友军 AC +2; 若友军本回合未移动, 则该友军受到的近战伤害 -20%",
            new Godot.Collections.Dictionary { { "ac_bonus", 2 }, { "stationary_damage_reduction", 0.20f } });

        AddPassive(F_INT | F_WIS, "sage_mana_threshold_damage", "大贤者", "Sage",
            "法力值为 100% 时法术伤害 +50%; 法力值 >50% 且 <100% 时法术伤害 +30%",
            new Godot.Collections.Dictionary { { "full_mana_bonus", 0.50f }, { "high_mana_bonus", 0.30f }, { "high_mana_threshold", 0.50f } });

        AddPassive(F_INT | F_CHA, "sorcerer_ac_curse", "指引者", "Sorcerer",
            "法术伤害使目标 AC -25%(向下取整), 持续 3 回合",
            new Godot.Collections.Dictionary { { "ac_reduction_pct", 0.25f }, { "duration", 3 } });

        AddPassive(F_WIS | F_CHA, "prophet_fatal_ally_protect", "预言者", "Prophet",
            "周围 2 格内友军受到致命伤害时, 该次伤害 -20%",
            new Godot.Collections.Dictionary { { "fatal_damage_reduction", 0.20f }, { "range", 2 } });

        // ---- 三属性 Passive (20) ----

        AddPassive(F_STR | F_DEX | F_CON, "grandmaster_stacking_damage", "大宗师", "Grandmaster",
            "近战命中后, 自身近战伤害 +5%(最多 +50%)",
            new Godot.Collections.Dictionary { { "per_hit_bonus", 0.05f }, { "max_bonus", 0.50f } });

        AddPassive(F_STR | F_DEX | F_INT, "spellweaver_melee_spell_cycle", "魔武者", "Spellweaver",
            "近战命中后, 下一次法术伤害 +8%; 施法命中后, 下一次近战伤害 +8%(各最多 3 层)",
            new Godot.Collections.Dictionary { { "melee_to_spell_bonus", 0.08f }, { "spell_to_melee_bonus", 0.08f }, { "max_stacks", 3 } });

        AddPassive(F_STR | F_DEX | F_WIS, "hawkeye_full_ap_crit", "审判官", "Hawkeye",
            "自身 AP 为最大值时, 该次攻击暴击率 +20%",
            new Godot.Collections.Dictionary { { "full_ap_crit_rate_bonus", 0.20f } });

        AddPassive(F_STR | F_DEX | F_CHA, "champion_move_3_boost_ally", "战誓者", "Champion",
            "自身移动 3 格后, 本回合下一次近战攻击命中 +2; 若命中, 相邻友军本回合下一次攻击伤害 +15%",
            new Godot.Collections.Dictionary { { "min_move", 3 }, { "self_hit_bonus", 2 }, { "ally_damage_bonus", 0.15f } });

        AddPassive(F_STR | F_CON | F_INT, "ironweaver_stationary_spell_immune", "述法者", "Ironweaver",
            "自身未移动并结束回合时, 消耗 50% 最大法力值, 周围 1 格内友军本回合免疫法术伤害",
            new Godot.Collections.Dictionary { { "mana_cost_pct", 0.50f }, { "ally_range", 1 } });

        AddPassive(F_STR | F_CON | F_WIS, "skullcrusher_armor_pierce_double", "惩罚者", "Skullcrusher",
            "近战攻击穿透护甲时, 不对护甲造成伤害, 转而造成 200% 伤害直接作用于血量",
            new Godot.Collections.Dictionary { { "armor_pierce_to_hp_multiplier", 2.0f } });

        AddPassive(F_STR | F_CON | F_CHA, "conqueror_move_5_aoe_melee", "征服者", "Conqueror",
            "移动至少 5 格后, 下次近战攻击对所有相邻敌方单位各发动一次攻击",
            new Godot.Collections.Dictionary { { "min_move", 5 }, { "aoe_melee_on_next", true } });

        AddPassive(F_STR | F_INT | F_WIS, "doom_knight_kill_free_spell", "毁灭者", "Doom Knight",
            "击杀敌人后, 可立即免费释放一次法术(不消耗 AP 和法力)",
            new Godot.Collections.Dictionary { { "free_spell_on_kill", true } });

        AddPassive(F_STR | F_INT | F_CHA, "overlord_enemy_accuracy_debuff", "支配者", "Overlord",
            "周围 2 格内敌方命中 -10%; 已受伤的敌方命中 -20%",
            new Godot.Collections.Dictionary { { "base_penalty", 0.10f }, { "injured_penalty", 0.20f }, { "range", 2 } });

        AddPassive(F_STR | F_WIS | F_CHA, "crusader_move_ally_damage", "十字军", "Crusader",
            "自身移动后, 周围 1 格内友军下一次攻击伤害 +10%",
            new Godot.Collections.Dictionary { { "ally_range", 1 }, { "ally_next_damage_bonus", 0.10f } });

        AddPassive(F_DEX | F_CON | F_INT, "shadow_shroud_cover_cloak", "影缄者", "Shadow Shroud",
            "回合结束时若位于掩体地形, 获得 1 回合影幕: 远程命中-2 对自身; 自身下次远程命中+2 伤害+15%; 全掩体再无视一半障碍物命中减益",
            new Godot.Collections.Dictionary { { "cloak_duration", 1 }, { "defense_hit_penalty", -2 },
                { "offense_hit_bonus", 2 }, { "offense_damage_bonus", 0.15f }, { "full_cover_ignore_half", true } });

        AddPassive(F_DEX | F_CON | F_WIS, "hawkeye_guard_cover_ignore", "鹰眼卫", "Hawkeye Guard",
            "远程攻击无视掩体和障碍物的命中减益",
            new Godot.Collections.Dictionary { { "ignore_all_cover", true } });

        AddPassive(F_DEX | F_CON | F_CHA, "outrider_benxi", "游骑兵", "Outrider",
            "自身移动 5 格后, 回合结束时范围 1 格内友军获得奔袭: 下次行动可免费移动 5 格(不触发借机攻击)",
            new Godot.Collections.Dictionary { { "min_move", 5 }, { "free_move_cells", 5 }, { "ally_range", 1 }, { "no_aoo", true } });

        AddPassive(F_DEX | F_INT | F_WIS, "starcaller_height_spell", "唤星者", "Starcaller",
            "自身本回合第一次法术射程改为: 所有比自身所处高度更低的地图格; 每低 1 高度伤害 +20%",
            new Godot.Collections.Dictionary { { "height_damage_per_step", 0.20f }, { "downward_only", true } });

        AddPassive(F_DEX | F_INT | F_CHA, "illusionist_phantom_ac", "幻术师", "Illusionist",
            "自身释放法术后获得 1 层幻影, 每层 AC +1(最多 10 层); 被攻击后失去 1 层",
            new Godot.Collections.Dictionary { { "ac_per_stack", 1 }, { "max_stacks", 10 }, { "lose_on_hit", true } });

        AddPassive(F_DEX | F_WIS | F_CHA, "windwalker_fixed_move_crit", "风语者", "Windwalker",
            "自身移动消耗固定为 1; 每移动 1 格, 本场战斗暴击率 +1%(最多 +50%)",
            new Godot.Collections.Dictionary { { "fixed_move_cost", 1 }, { "crit_per_cell", 0.01f }, { "max_crit_bonus", 0.50f } });

        AddPassive(F_CON | F_INT | F_WIS, "antimage_full_ap_mana_immune", "敌法师", "Antimage",
            "若自身以 AP 和法力均为最大值的状态结束回合, 则消耗所有法力, 本场战斗免疫法术伤害",
            new Godot.Collections.Dictionary { { "requires_full_ap_and_mana", true }, { "consume_all_mana", true } });

        AddPassive(F_CON | F_INT | F_CHA, "iron_sovereign_aura_ac_pct", "铁幕领主", "Iron Sovereign",
            "相邻友军和自身 AC +20%(向上取整)",
            new Godot.Collections.Dictionary { { "ac_bonus_pct", 0.20f }, { "affects_self", true }, { "affects_adjacent_allies", true } });

        AddPassive(F_CON | F_WIS | F_CHA, "oathshield_adjacent_crit_negate", "誓盾卫", "Oathshield",
            "相邻友军受到暴击时, 免除该次伤害",
            new Godot.Collections.Dictionary { { "negate_adjacent_crit", true } });

        AddPassive(F_INT | F_WIS | F_CHA, "chosen_one_spell_can_crit", "天选者", "Chosen One",
            "自身法术可以暴击",
            new Godot.Collections.Dictionary { { "spell_can_crit", true } });

        // ---- 四属性 Passive (15) ----

        AddPassive(F_CON | F_INT | F_WIS | F_CHA, "archsage_ally_spell_damage", "秘院贤师", "Archsage",
            "自身和相邻友军法术伤害 +30%; 自身每回合第一次法术不消耗法力",
            new Godot.Collections.Dictionary { { "spell_damage_bonus", 0.30f }, { "first_spell_free_mana", true }, { "ally_range", 1 } });

        AddPassive(F_DEX | F_INT | F_WIS | F_CHA, "zephyr_master_move_spell", "灵风秘庭", "Zephyr Master",
            "自身移动消耗固定为 1; 每移动 1 格, 本回合下一次法术伤害 +10%(最多 +50%); 达到 +50% 时受击单位下回合无法移动",
            new Godot.Collections.Dictionary { { "fixed_move_cost", 1 }, { "spell_damage_per_cell", 0.10f },
                { "max_spell_bonus", 0.50f }, { "immobilize_on_max", true } });

        AddPassive(F_DEX | F_CON | F_WIS | F_CHA, "warchief_same_height_charge", "荒原之心", "Warchief",
            "连续在同一高度进行移动时不受借机攻击影响; 本场战斗累计移动超过 10 格后, 获得 3 回合伤害 +50%(不可叠加)",
            new Godot.Collections.Dictionary { { "same_height_no_aoo", true }, { "accumulate_move_threshold", 10 },
                { "rage_damage_bonus", 0.50f }, { "rage_duration", 3 } });

        AddPassive(F_DEX | F_CON | F_INT | F_CHA, "blood_pact_hp_mana_exchange", "血契之环", "Blood Pact",
            "自身消耗法力时等额恢复生命; 自身受到血量伤害时等额恢复法力; 不超出上限",
            new Godot.Collections.Dictionary { { "mana_to_hp_ratio", 1.0f }, { "hp_to_mana_ratio", 1.0f } });

        AddPassive(F_DEX | F_CON | F_INT | F_WIS, "silent_edge_adjacent_silence", "静默之刃", "Silent Edge",
            "相邻敌方单位无法施法",
            new Godot.Collections.Dictionary { { "adjacent_silence", true } });

        AddPassive(F_STR | F_INT | F_WIS | F_CHA, "crown_of_ruin_low_hp_spell", "毁灭王冠", "Crown of Ruin",
            "法术伤害 +40%; 每损失 10% 最大生命, 法术伤害再 +5%(最多额外 +30%)",
            new Godot.Collections.Dictionary { { "base_spell_damage_bonus", 0.40f },
                { "per_lost_10pct_bonus", 0.05f }, { "max_extra_bonus", 0.30f } });

        AddPassive(F_STR | F_CON | F_WIS | F_CHA, "stone_saint_melee_reduction", "磐石守护", "Stone Saint",
            "自身和相邻友军受到近战伤害 -30%; HP < 50% 时提高到 -50%",
            new Godot.Collections.Dictionary { { "base_reduction", 0.30f }, { "low_hp_reduction", 0.50f },
                { "low_hp_threshold", 0.50f }, { "ally_range", 1 } });

        AddPassive(F_STR | F_CON | F_INT | F_CHA, "ironbound_lord_melee_ac", "铁铸领主", "Ironbound Lord",
            "自身和相邻友军近战伤害 +30%、AC +2",
            new Godot.Collections.Dictionary { { "melee_damage_bonus", 0.30f }, { "ac_bonus", 2 }, { "ally_range", 1 } });

        AddPassive(F_STR | F_CON | F_INT | F_WIS, "void_knight_extra_ap_damage", "渊狱骑士", "Void Knight",
            "自身攻击命中后, 额外消耗剩余 AP, 对目标和目标范围 1 格内敌人造成 消耗 AP × 10% 额外伤害",
            new Godot.Collections.Dictionary { { "extra_damage_per_ap", 0.10f }, { "aoe_range", 1 } });

        AddPassive(F_STR | F_DEX | F_WIS | F_CHA, "storm_banner_move_5_crit", "战争之风", "Storm Banner",
            "自身移动 5 格后, 本回合下一次攻击必定暴击; 若击杀目标, 恢复该次攻击消耗的 AP",
            new Godot.Collections.Dictionary { { "min_move", 5 }, { "guaranteed_crit", true }, { "refund_ap_on_kill", true } });

        AddPassive(F_STR | F_DEX | F_INT | F_CHA, "tempest_wrath_spell_chain", "焰风之怒", "Tempest Wrath",
            "自身每次成功释放法术后, 本回合后续法术伤害 +30%, 可叠加",
            new Godot.Collections.Dictionary { { "per_spell_bonus", 0.30f }, { "reset_on_turn_end", true } });

        AddPassive(F_STR | F_DEX | F_INT | F_WIS, "lone_blade_isolated_buff", "孤刃之誓", "Lone Blade",
            "周围 2 格内无友军时, 攻击命中 +3、伤害 +40%; 若本回合未受到伤害, 下一次攻击暴击率 +15%",
            new Godot.Collections.Dictionary { { "solo_range", 2 }, { "solo_hit_bonus", 3 },
                { "solo_damage_bonus", 0.40f }, { "unharmed_crit_rate", 0.15f } });

        AddPassive(F_STR | F_DEX | F_CON | F_CHA, "war_king_melee_leader", "战争领主", "War King",
            "自身和相邻友军近战攻击命中 +2、伤害 +30%; 自身击杀后, 相邻友军恢复 2 AP",
            new Godot.Collections.Dictionary { { "hit_bonus", 2 }, { "damage_bonus", 0.30f },
                { "kill_ap_refund", 2 }, { "ally_range", 1 } });

        AddPassive(F_STR | F_DEX | F_CON | F_WIS, "steelstring_knight_switch_free", "钢弦骑士", "Steelstring Knight",
            "自身近战攻击后, 下次切换武器和远程攻击不消耗 AP(每回合 1 次)",
            new Godot.Collections.Dictionary { { "free_switch_and_ranged", true }, { "per_turn", 1 } });

        AddPassive(F_STR | F_DEX | F_CON | F_INT, "arcane_war_knight_cycle", "鏖战骑士", "Arcane War Knight",
            "自身近战命中后, 本回合下一次法术不消耗 AP; 自身释放法术后, 本回合下一次近战攻击伤害 +40%",
            new Godot.Collections.Dictionary { { "melee_to_free_spell", true }, { "spell_to_damage_bonus", 0.40f } });

        // ---- 五属性 Active (6) ----

        AddActive5(F_DEX | F_CON | F_INT | F_WIS | F_CHA,
            "emissary_pact_seal", "万灵之约印", "Emissary",
            "目标友军: 回满 HP、法力、AP; 目标因 AP 满可继续被选中行动(不插入先攻队列)",
            CareerSkillData.TargetType.SingleAlly, 6,
            new Godot.Collections.Dictionary { { "restore_hp_pct", 1.0f }, { "restore_mana_pct", 1.0f },
                { "restore_ap", true }, { "no_initiative_insert", true } });

        AddActive5(F_STR | F_CON | F_INT | F_WIS | F_CHA,
            "mountain_throne", "山岳之王座", "Mountain Lord",
            "自身: 3 回合不可移动、伤害 -60%、近战伤害 +60%、相邻敌人移动每格 +1 AP",
            CareerSkillData.TargetType.Self, 0,
            new Godot.Collections.Dictionary { { "duration", 3 }, { "no_move", true },
                { "incoming_damage_reduction", 0.60f }, { "melee_damage_bonus", 0.60f },
                { "adjacent_enemy_move_ap_penalty", 1 } });

        AddActive5(F_STR | F_DEX | F_INT | F_WIS | F_CHA,
            "astral_rift", "星界之裂隙", "Astral Walker",
            "目标地图格: 沿直线跳跃到目标格, 对路径上所有敌人各进行一次攻击",
            CareerSkillData.TargetType.Ground, 8,
            new Godot.Collections.Dictionary { { "line_charge", true }, { "attack_all_in_path", true } });

        AddActive5(F_STR | F_DEX | F_CON | F_WIS | F_CHA,
            "waste_avatar", "荒芜之化身", "Wrath Avatar",
            "自身: 本场战斗剩余时间内, 紧邻的敌人护甲 100% 被穿透",
            CareerSkillData.TargetType.Self, 0,
            new Godot.Collections.Dictionary { { "armor_pierce_pct", 1.0f }, { "adjacent_only", true },
                { "combat_duration", true } });

        AddActive5(F_STR | F_DEX | F_CON | F_INT | F_CHA,
            "iron_blood_edict", "铁血之律令", "Iron Tyrant",
            "全体友军获得 1 次铁血: 下一次未命中的攻击转为暴击",
            CareerSkillData.TargetType.AllAllies, 0,
            new Godot.Collections.Dictionary { { "miss_to_crit", true }, { "charges", 1 } });

        AddActive5(F_STR | F_DEX | F_CON | F_INT | F_WIS,
            "lone_star_shadow", "孤星之刃影", "Lone Shadow",
            "紧邻敌人: 仅当自身周围 6 格内只有 1 个敌方时可用; 对该目标必中必暴, 目标不能离开控制区",
            CareerSkillData.TargetType.SingleEnemy, 1,
            new Godot.Collections.Dictionary { { "require_solo_enemy", true }, { "solo_range", 6 },
                { "guaranteed_hit", true }, { "guaranteed_crit", true }, { "immobilize_target", true } });

        // ---- 全属性 Active (1) ----

        AddActive6(F_STR | F_DEX | F_CON | F_INT | F_WIS | F_CHA,
            "paragon_all_aspects", "万象", "Paragon",
            "自身: 随机抽取无法移动/无法施法/无法攻击之一(不重复); 恢复 HP/法力/AP; 三个代价后本场不可再用",
            new Godot.Collections.Dictionary { { "costs", new Godot.Collections.Array { "no_move", "no_spell", "no_attack" } },
                { "restore_hp", true }, { "restore_mana", true }, { "restore_ap", true } });

        GD.Print($"[CareerSkillRegistry] Registered {_registry?.Count ?? 0} v1 career skills");
    }

    // ============================================================================
    // 辅助注册方法
    // ============================================================================

    /// <summary>注册常驻被动 (1-4 属性)</summary>
    private static void AddPassive(int flags, string effectId, string displayName,
        string englishName, string description, Godot.Collections.Dictionary effectParams)
    {
        var skill = new CareerSkillData
        {
            SkillId = effectId,
            DisplayName = displayName,
            EnglishName = englishName,
            RequiredTitleFlags = flags,
            Type = CareerSkillData.SkillType.Passive,
            ApCost = 0,
            LimitType = CareerSkillData.UsageLimit.Unlimited,
            MaxUses = 0,
            Cooldown = 0,
            RequiresFullAp = false,
            ConsumesMaxAp = false,
            ShowInCombatUi = false,
            AttributeCount = CountAttributes(flags),
            SkillTargetType = CareerSkillData.TargetType.Self,
            Range = 0,
            EffectId = effectId,
            Description = description,
            EffectParams = effectParams,
        };
        _registry![flags] = skill;
        _byEffectId![effectId] = skill;
    }

    /// <summary>注册五属性主动 (OncePerBattle, 满AP, 消耗最大AP)</summary>
    private static void AddActive5(int flags, string effectId, string displayName,
        string englishName, string description, CareerSkillData.TargetType targetType, int range,
        Godot.Collections.Dictionary effectParams)
    {
        effectParams["target_type"] = targetType.ToString();
        effectParams["range"] = range;

        var skill = new CareerSkillData
        {
            SkillId = effectId,
            DisplayName = displayName,
            EnglishName = englishName,
            RequiredTitleFlags = flags,
            Type = CareerSkillData.SkillType.Active,
            ApCost = 0,
            LimitType = CareerSkillData.UsageLimit.OncePerBattle,
            MaxUses = 1,
            Cooldown = 0,
            RequiresFullAp = true,
            ConsumesMaxAp = true,
            ShowInCombatUi = true,
            AttributeCount = 5,
            SkillTargetType = targetType,
            Range = range,
            EffectId = effectId,
            Description = description,
            EffectParams = effectParams,
        };
        _registry![flags] = skill;
        _byEffectId![effectId] = skill;
    }

    /// <summary>注册六属性主动 (OncePerTurn, 代价型)</summary>
    private static void AddActive6(int flags, string effectId, string displayName,
        string englishName, string description, Godot.Collections.Dictionary effectParams)
    {
        effectParams["target_type"] = CareerSkillData.TargetType.Self.ToString();
        effectParams["range"] = 0;

        var skill = new CareerSkillData
        {
            SkillId = effectId,
            DisplayName = displayName,
            EnglishName = englishName,
            RequiredTitleFlags = flags,
            Type = CareerSkillData.SkillType.Active,
            ApCost = 0,
            LimitType = CareerSkillData.UsageLimit.OncePerTurn,
            MaxUses = 1,
            Cooldown = 0,
            RequiresFullAp = false,
            ConsumesMaxAp = false,
            ShowInCombatUi = true,
            AttributeCount = 6,
            SkillTargetType = CareerSkillData.TargetType.Self,
            Range = 0,
            EffectId = effectId,
            Description = description,
            EffectParams = effectParams,
        };
        _registry![flags] = skill;
        _byEffectId![effectId] = skill;
    }
}
