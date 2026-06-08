// CareerBuffRegistry.cs
// 职业大招 buff 注册表 — v0.8: 47 个职业技能专用 buff
// 从 BuffRegistry.cs 拆分，减少主文件膨胀
using System.Collections.Generic;

namespace BladeHex.Combat.Buff;

/// <summary>
/// 职业大招 buff 模板注册表。
/// 所有职业技能的 buff 在此定义，由 BuffRegistry.RegisterAll() 调用。
/// </summary>
public static class CareerBuffRegistry
{
    /// <summary>注册所有职业 buff 模板</summary>
    public static void RegisterAll()
    {
        // A1: 单属性 + 双属性 (10个)
        RegisterArmorBreak();
        RegisterVolley();
        RegisterLivingWall();
        RegisterArcaneOverload();
        RegisterDeathMark();
        RegisterBattleHymn();
        RegisterBladeDance();
        RegisterUnstoppable();
        RegisterRuneImbue();
        RegisterDeathSentence();

        // A2: 双属性 + 三属性 (10个)
        RegisterLeadFront();
        RegisterRiposte();
        RegisterHoming();
        RegisterHawksMark();
        RegisterMisdirected();
        RegisterManaShield();
        RegisterOldTimer();
        RegisterHoldLine();
        RegisterForewarning();
        RegisterBloodResonance();

        // A3: 三属性 + 四属性 (10个)
        RegisterFateProtect();
        RegisterIronRush();
        RegisterSpellweave();
        RegisterHawkeyeAura();
        RegisterChampionAura();
        RegisterCrushWeak();
        RegisterGazeRuin();
        RegisterDreadAura();
        RegisterVanguard();
        RegisterShadowForm();

        // A4: 四属性 + 五属性 (10个)
        RegisterHuntersMark();
        RegisterDisguise();
        RegisterStarMap();
        RegisterMirrorImage();
        RegisterTailwind();
        RegisterBulwarkLore();
        RegisterIronLaw();
        RegisterMartyrsGuard();
        RegisterTwistFate();
        RegisterOmnibus();

        // A5: 五属性 + 全属性 (13个)
        RegisterWindFavor();
        RegisterFeralSelf();
        RegisterWolfPack();
        RegisterPuppetAura();
        RegisterSilentStrike();
        RegisterHarbinger();
        RegisterStoneBody();
        RegisterIronGrip();
        RegisterDeepChains();
        RegisterStormBanner();
        RegisterLoneSaint();
        RegisterWarKing();
        RegisterSkyHunter();
        RegisterMyriad();
        RegisterJackOfAll();
        RegisterMountainStance();
        RegisterTwilightStride();
        RegisterSavage();
        RegisterTyrantWrath();
        RegisterLoneOp();
        RegisterParagon();
        RegisterRuneAura();
    }

    // ============================================================================
    // 辅助方法
    // ============================================================================

    private static void Register(string id, BuffInstance template)
    {
        BuffRegistry.Register(id, template);
    }

    // ============================================================================
    // A1: 单属性 + 双属性 (10个)
    // ============================================================================

    private static void RegisterArmorBreak()
    {
        Register("armor_break", new BuffInstance
        {
            Id = "armor_break", Name = "碎甲打击", Description = "DR阻抗-3, AC-1",
            IconId = "IconSlash", IsNegative = true, Duration = -1,
            Tags = new[] { "debuff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "dr_threshold", Layer = ModifierLayer.Base, Value = -3 },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = -1 },
            }
        });
    }

    private static void RegisterVolley()
    {
        Register("volley", new BuffInstance
        {
            Id = "volley", Name = "箭雨", Description = "远程命中+2",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 2, Condition = "ranged_only" },
            }
        });
    }

    private static void RegisterLivingWall()
    {
        Register("living_wall", new BuffInstance
        {
            Id = "living_wall", Name = "铜墙铁壁", Description = "AC+3, DR阈值+2, 免疫推移/位移",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "dr_threshold", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "immune_displacement", Layer = ModifierLayer.Override, Value = 1 },
            }
        });
    }

    private static void RegisterArcaneOverload()
    {
        Register("arcane_overload", new BuffInstance
        {
            Id = "arcane_overload", Name = "以太过载", Description = "下次法术DC+5",
            IconId = "IconLightning", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "next_spell_dc_bonus", Layer = ModifierLayer.Base, Value = 5 },
            }
        });
    }

    private static void RegisterDeathMark()
    {
        Register("death_mark", new BuffInstance
        {
            Id = "death_mark", Name = "致命标记", Description = "被攻击时暴击阈值-3",
            IconId = "IconDark", IsNegative = true, Duration = -1,
            Tags = new[] { "debuff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "crit_threshold", Layer = ModifierLayer.Base, Value = -3 },
            }
        });
    }

    private static void RegisterBattleHymn()
    {
        Register("battle_hymn", new BuffInstance
        {
            Id = "battle_hymn", Name = "军魂战歌", Description = "移动-1AP/格, AC+1, 伤害+10%",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "move_ap_reduction", Layer = ModifierLayer.Base, Value = 1 },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 1 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Increased, Value = 0.10f },
            }
        });
    }

    private static void RegisterBladeDance()
    {
        Register("blade_dance", new BuffInstance
        {
            Id = "blade_dance", Name = "连旋斩", Description = "反击范围扩展至周围3格",
            IconId = "IconSlash", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "counter_range", Layer = ModifierLayer.Base, Value = 3 },
            }
        });
    }

    private static void RegisterUnstoppable()
    {
        Register("unstoppable", new BuffInstance
        {
            Id = "unstoppable", Name = "不可阻挡", Description = "免疫控制和推移位移",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "temp_hp_amount", Layer = ModifierLayer.Base, Value = 0 },
                new StatModifier { Stat = "immune_cc", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "immune_displacement", Layer = ModifierLayer.Override, Value = 1 },
            }
        });
    }

    private static void RegisterRuneImbue()
    {
        Register("rune_imbue", new BuffInstance
        {
            Id = "rune_imbue", Name = "符文武器", Description = "近战攻击额外1d6火焰, 命中燃烧1d6",
            IconId = "IconFire", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnDealDamage, Effect = "deal_damage:1d6:fire" }
            }
        });
    }

    private static void RegisterDeathSentence()
    {
        Register("death_sentence", new BuffInstance
        {
            Id = "death_sentence", Name = "终结宣告", Description = "攻击HP≤30%目标时暴击阈值-4",
            IconId = "IconSlash", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "crit_threshold", Layer = ModifierLayer.Base, Value = -4, Condition = "target_hp_below_30%" }
            }
        });
    }

    // ============================================================================
    // A2: 双属性 + 三属性 (10个)
    // ============================================================================

    private static void RegisterLeadFront()
    {
        Register("lead_front", new BuffInstance
        {
            Id = "lead_front", Name = "身先士卒", Description = "周围2格友军移动-1AP/格",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "move_ap_reduction", Layer = ModifierLayer.Base, Value = 1, Condition = "ally_only" }
            }
        });
    }

    private static void RegisterRiposte()
    {
        Register("riposte", new BuffInstance
        {
            Id = "riposte", Name = "以伤换伤", Description = "被近战命中后进行反击",
            IconId = "IconSlash", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnTakeDamage, Effect = "trigger_riposte" }
            }
        });
    }

    private static void RegisterHoming()
    {
        Register("homing", new BuffInstance
        {
            Id = "homing", Name = "魔矢追踪", Description = "远程攻击无视半掩体",
            IconId = "IconPierce", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "ignore_half_cover", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterHawksMark()
    {
        Register("hawks_mark", new BuffInstance
        {
            Id = "hawks_mark", Name = "鹰眼锁定", Description = "失去地形AC与掩护, 暴击阈值-2",
            IconId = "IconDark", IsNegative = true, Duration = -1,
            Tags = new[] { "debuff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "lose_terrain_ac", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "crit_threshold", Layer = ModifierLayer.Base, Value = -2 }
            }
        });
    }

    private static void RegisterMisdirected()
    {
        Register("misdirected", new BuffInstance
        {
            Id = "misdirected", Name = "声东击西", Description = "下回合首次攻击劣势",
            IconId = "IconStun", IsNegative = true, Duration = -1,
            Tags = new[] { "debuff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "next_attack_disadvantage", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterManaShield()
    {
        Register("mana_shield", new BuffInstance
        {
            Id = "mana_shield", Name = "法力护盾", Description = "受伤时消耗法力减免伤害",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnTakeDamage, Effect = "mana_shield_absorb" }
            }
        });
    }

    private static void RegisterOldTimer()
    {
        Register("old_timer", new BuffInstance
        {
            Id = "old_timer", Name = "临危不乱", Description = "AC+3, 反击范围+6, 反击伤害×1.5",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "counter_range", Layer = ModifierLayer.Base, Value = 6 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.More, Value = 0.5f, Condition = "counter_attack" }
            }
        });
    }

    private static void RegisterHoldLine()
    {
        Register("hold_line", new BuffInstance
        {
            Id = "hold_line", Name = "坚守阵线", Description = "AC+2, 免疫恐惧, 士气下限-20",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "immune_fear", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterForewarning()
    {
        Register("forewarning", new BuffInstance
        {
            Id = "forewarning", Name = "预知回避", Description = "豁免+2, 每回合首次被攻击消耗法力使其劣势",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "save_bonus", Layer = ModifierLayer.Base, Value = 2 }
            },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnBeforeDefend, Effect = "forewarning_trigger" }
            }
        });
    }

    private static void RegisterBloodResonance()
    {
        Register("blood_resonance", new BuffInstance
        {
            Id = "blood_resonance", Name = "血脉共鸣", Description = "法术DC+2, 伤害+20%",
            IconId = "IconFire", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "spell_dc_bonus", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Increased, Value = 0.20f }
            }
        });
    }

    // ============================================================================
    // A3: 三属性 + 四属性 (10个)
    // ============================================================================

    private static void RegisterFateProtect()
    {
        Register("fate_protect", new BuffInstance
        {
            Id = "fate_protect", Name = "命运干涉", Description = "致死伤害骰强制重掷取低",
            IconId = "IconHoly", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnTakeDamage, Effect = "fate_protect_trigger" }
            }
        });
    }

    private static void RegisterIronRush()
    {
        Register("iron_rush", new BuffInstance
        {
            Id = "iron_rush", Name = "铁壁冲锋", Description = "AC+4, 伤害Base+2, 50%概率反击",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 4 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 2 }
            },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnTakeDamage, Effect = "iron_rush_counter", Chance = 0.5f }
            }
        });
    }

    private static void RegisterSpellweave()
    {
        Register("spellweave", new BuffInstance
        {
            Id = "spellweave", Name = "奥战姿态", Description = "命中使下次施法DC+1; 施法使下次近战伤害+1d",
            IconId = "IconLightning", IsNegative = false, Duration = -1, MaxStacks = 3,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "melee_to_spell_dc_per_stack", Layer = ModifierLayer.Base, Value = 1 },
                new StatModifier { Stat = "spell_to_melee_dice_per_stack", Layer = ModifierLayer.Base, Value = 1 }
            }
        });
    }

    private static void RegisterHawkeyeAura()
    {
        Register("hawkeye_aura", new BuffInstance
        {
            Id = "hawkeye_aura", Name = "审判官", Description = "远程伤害增加+25%",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Increased, Value = 0.25f, Condition = "ranged_only" }
            }
        });
    }

    private static void RegisterChampionAura()
    {
        Register("champion_aura", new BuffInstance
        {
            Id = "champion_aura", Name = "战神战吼", Description = "攻击获得优势",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attack_advantage", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterCrushWeak()
    {
        Register("crush_weak", new BuffInstance
        {
            Id = "crush_weak", Name = "弱点粉碎", Description = "近战护甲穿透+3",
            IconId = "IconCrush", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "dr_pen_bonus", Layer = ModifierLayer.Base, Value = 3 }
            }
        });
    }

    private static void RegisterGazeRuin()
    {
        Register("gaze_ruin", new BuffInstance
        {
            Id = "gaze_ruin", Name = "毁灭凝视", Description = "被攻击时暴击阈值固定为12",
            IconId = "IconDark", IsNegative = true, Duration = -1,
            Tags = new[] { "debuff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "crit_threshold", Layer = ModifierLayer.Override, Value = 12 }
            }
        });
    }

    private static void RegisterDreadAura()
    {
        Register("dread_aura", new BuffInstance
        {
            Id = "dread_aura", Name = "恐惧光环", Description = "周围3格敌方士气压制, 敌方劣势",
            IconId = "IconDark", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
            }
        });
    }

    private static void RegisterVanguard()
    {
        Register("vanguard", new BuffInstance
        {
            Id = "vanguard", Name = "身后先驱", Description = "身后1格友军获冲锋优势",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "rear_ally_charge_advantage", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterShadowForm()
    {
        Register("shadow_form", new BuffInstance
        {
            Id = "shadow_form", Name = "暗影置换", Description = "每回合首次移动可瞬移",
            IconId = "IconDark", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "first_move_teleport", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    // ============================================================================
    // A4: 四属性 + 五属性 (10个)
    // ============================================================================

    private static void RegisterHuntersMark()
    {
        Register("hunters_mark", new BuffInstance
        {
            Id = "hunters_mark", Name = "暗杀标记", Description = "施法者对该目标攻击拥有优势",
            IconId = "IconDark", IsNegative = true, Duration = -1,
            Tags = new[] { "debuff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "advantage_for_caster", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterDisguise()
    {
        Register("disguise", new BuffInstance
        {
            Id = "disguise", Name = "伪装", Description = "外观仿制, AI不主动攻击",
            IconId = "IconDark", IsNegative = false, Duration = -1, BreaksOnAttack = true,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "ai_ignores_self", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterStarMap()
    {
        Register("star_map", new BuffInstance
        {
            Id = "star_map", Name = "星图", Description = "每回合开始可免费瞬移",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "free_teleport_per_turn", Layer = ModifierLayer.Base, Value = 1 }
            }
        });
    }

    private static void RegisterMirrorImage()
    {
        Register("mirror_image", new BuffInstance
        {
            Id = "mirror_image", Name = "镜影分身", Description = "分身存在时所有攻击者劣势",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attacker_disadvantage_while_phantom", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterTailwind()
    {
        Register("tailwind", new BuffInstance
        {
            Id = "tailwind", Name = "顺风", Description = "移动-1AP/格, 攻击+1命中",
            IconId = "IconLightning", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "move_ap_reduction", Layer = ModifierLayer.Base, Value = 1 },
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 1 }
            }
        });
    }

    private static void RegisterBulwarkLore()
    {
        Register("bulwark_lore", new BuffInstance
        {
            Id = "bulwark_lore", Name = "知识壁垒", Description = "受法术伤害时DC检定加值+5",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "spell_damage_check_bonus", Layer = ModifierLayer.Base, Value = 5 }
            }
        });
    }

    private static void RegisterIronLaw()
    {
        Register("iron_law", new BuffInstance
        {
            Id = "iron_law", Name = "铁律", Description = "禁冲锋与潜行, 士气变化减半",
            IconId = "IconCrush", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "no_charge", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "no_stealth", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterMartyrsGuard()
    {
        Register("martyrs_guard", new BuffInstance
        {
            Id = "martyrs_guard", Name = "殉道守护", Description = "友军伤害代受50%并获临时HP",
            IconId = "IconHoly", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnTakeDamage, Effect = "martyrs_guard_trigger" }
            }
        });
    }

    private static void RegisterTwistFate()
    {
        Register("twist_fate", new BuffInstance
        {
            Id = "twist_fate", Name = "天选", Description = "攻击与豁免受到命运修正",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 1 },
                new StatModifier { Stat = "save_bonus", Layer = ModifierLayer.Base, Value = 1 }
            }
        });
    }

    private static void RegisterOmnibus()
    {
        Register("omnibus", new BuffInstance
        {
            Id = "omnibus", Name = "万卷通鉴", Description = "命中+2, 豁免+2",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "save_bonus", Layer = ModifierLayer.Base, Value = 2 }
            }
        });
    }

    // ============================================================================
    // A5: 五属性 + 全属性 (13个)
    // ============================================================================

    private static void RegisterWindFavor()
    {
        Register("wind_favor", new BuffInstance
        {
            Id = "wind_favor", Name = "风眷", Description = "每移动获得风痕, 提升伤害与射程",
            IconId = "IconLightning", IsNegative = false, Duration = -1, MaxStacks = 5,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 2, Condition = "wind_favor_stack" },
                new StatModifier { Stat = "range_bonus", Layer = ModifierLayer.Base, Value = 1, Condition = "wind_favor_stack" }
            }
        });
    }

    private static void RegisterFeralSelf()
    {
        Register("feral_self", new BuffInstance
        {
            Id = "feral_self", Name = "野性自身", Description = "移动力+2, 侦测潜行, 击兽伤害+2",
            IconId = "IconFire", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "speed", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "detect_stealth", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 2, Condition = "vs_beast" }
            }
        });
    }

    private static void RegisterWolfPack()
    {
        Register("wolf_pack", new BuffInstance
        {
            Id = "wolf_pack", Name = "狼群", Description = "对野兽攻击优势, 移动消耗-1AP/格",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "move_ap_reduction", Layer = ModifierLayer.Base, Value = 1, Condition = "beast_only" }
            }
        });
    }

    private static void RegisterPuppetAura()
    {
        Register("puppet_aura", new BuffInstance
        {
            Id = "puppet_aura", Name = "幕后操纵", Description = "士气低于-30的敌方跳过玩家攻击",
            IconId = "IconDark", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "broken_enemy_skip_attack", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterSilentStrike()
    {
        Register("silent_strike", new BuffInstance
        {
            Id = "silent_strike", Name = "无声击", Description = "潜行不破隐, 首次伤害+50% More",
            IconId = "IconDark", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "stealth_attack_no_break", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.More, Value = 0.5f, Condition = "first_attack" }
            }
        });
    }

    private static void RegisterHarbinger()
    {
        Register("harbinger", new BuffInstance
        {
            Id = "harbinger", Name = "毁灭预兆", Description = "每损10%HP提升DC, AC固定10",
            IconId = "IconFire", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "spell_dc_bonus", Layer = ModifierLayer.Base, Value = 1, Condition = "per_10pct_hp_lost" },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Override, Value = 10f }
            }
        });
    }

    private static void RegisterStoneBody()
    {
        Register("stone_body", new BuffInstance
        {
            Id = "stone_body", Name = "石化之躯", Description = "无法行动, DR限制+5, 免疫负面, HP最低为1",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "cannot_act", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "dr_threshold", Layer = ModifierLayer.Base, Value = 5 },
                new StatModifier { Stat = "immune_negative", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "hp_floor_1", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterIronGrip()
    {
        Register("iron_grip", new BuffInstance
        {
            Id = "iron_grip", Name = "铁腕统御", Description = "免疫恐惧, 攻击+2, AC+1, 禁撤",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "immune_fear", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 1 },
                new StatModifier { Stat = "no_retreat", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterDeepChains()
    {
        Register("deep_chains", new BuffInstance
        {
            Id = "deep_chains", Name = "深渊锁链", Description = "无法移动与防御, 攻击具有劣势",
            IconId = "IconDark", IsNegative = true, Duration = -1,
            Tags = new[] { "debuff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "cannot_move", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "no_defend", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "attack_disadvantage", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterStormBanner()
    {
        Register("storm_banner", new BuffInstance
        {
            Id = "storm_banner", Name = "风暴战旗", Description = "周围2格友军移动力消耗-1AP/格",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "move_ap_reduction", Layer = ModifierLayer.Base, Value = 1 }
            }
        });
    }

    private static void RegisterLoneSaint()
    {
        Register("lone_saint", new BuffInstance
        {
            Id = "lone_saint", Name = "独行术", Description = "周围无友军时 AC+4, 伤害+4, 暴击阈值-2",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 4, Condition = "solo" },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 4, Condition = "solo" },
                new StatModifier { Stat = "crit_threshold", Layer = ModifierLayer.Base, Value = -2, Condition = "solo" }
            }
        });
    }

    private static void RegisterWarKing()
    {
        Register("war_king", new BuffInstance
        {
            Id = "war_king", Name = "战争之王", Description = "攻击+2, 伤害+2, HP上限+5",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "max_hp_bonus", Layer = ModifierLayer.Base, Value = 5 }
            }
        });
    }

    private static void RegisterSkyHunter()
    {
        Register("sky_hunter", new BuffInstance
        {
            Id = "sky_hunter", Name = "追迹猎手", Description = "对标记目标命中+3, 伤害+5",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 3, Condition = "vs_marked" },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 5, Condition = "vs_marked" },
                new StatModifier { Stat = "crit_threshold", Layer = ModifierLayer.Base, Value = -2, Condition = "hp_below_50%" }
            }
        });
    }

    private static void RegisterMyriad()
    {
        Register("myriad", new BuffInstance
        {
            Id = "myriad", Name = "万象", Description = "近战伤害+5, 法DC+3, 移耗-1, 护甲惩罚减半",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 5, Condition = "melee_only" },
                new StatModifier { Stat = "spell_dc_bonus", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "move_ap_reduction", Layer = ModifierLayer.Base, Value = 1 },
                new StatModifier { Stat = "armor_ap_penalty_reduction", Layer = ModifierLayer.Base, Value = 2 }
            }
        });
    }

    private static void RegisterJackOfAll()
    {
        Register("jack_of_all", new BuffInstance
        {
            Id = "jack_of_all", Name = "万通通识", Description = "大节点数值+20%, 豁免+3, 攻击+2",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "node_bonus", Layer = ModifierLayer.Base, Value = 0.20f },
                new StatModifier { Stat = "save_bonus", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 2 }
            }
        });
    }

    private static void RegisterMountainStance()
    {
        Register("mountain_stance", new BuffInstance
        {
            Id = "mountain_stance", Name = "山岳姿态", Description = "不能移, 免疫推移, AC+4, DR阈值+6, 50%反击",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "cannot_move", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "immune_displacement", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 4 },
                new StatModifier { Stat = "dr_threshold", Layer = ModifierLayer.Base, Value = 6 },
                new StatModifier { Stat = "immune_negative", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 6, Condition = "melee_only" }
            },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnTakeDamage, Effect = "mountain_stance_counter", Chance = 0.5f }
            }
        });
    }

    private static void RegisterTwilightStride()
    {
        Register("twilight_stride", new BuffInstance
        {
            Id = "twilight_stride", Name = "暮光步", Description = "不触发借机, 可穿过敌方, 命中+2, 回合始HP低恢复1d6",
            IconId = "IconLightning", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "no_aoo_on_move", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "can_cross_enemies", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 2 }
            },
            Triggers = new()
            {
                new BuffTrigger { Event = TriggerEvent.OnTurnStart, Effect = "heal:1d6", Condition = "hp_below_50%" }
            }
        });
    }

    private static void RegisterSavage()
    {
        Register("savage", new BuffInstance
        {
            Id = "savage", Name = "斩杀野蛮", Description = "对HP≤30%敌方命中拥有优势, 击杀回5AP",
            IconId = "IconFire", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "auto_target_lowest_hp", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "attack_advantage", Layer = ModifierLayer.Override, Value = 1, Condition = "target_hp_below_30%" },
                new StatModifier { Stat = "kill_ap_recovery", Layer = ModifierLayer.Base, Value = 5 }
            }
        });
    }

    private static void RegisterTyrantWrath()
    {
        Register("tyrant_wrath", new BuffInstance
        {
            Id = "tyrant_wrath", Name = "暴君之怒", Description = "免损伤, 伤+30%, 暴击阈固定20",
            IconId = "IconFire", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "immune_damage_debuff", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "immune_damage_reduction", Layer = ModifierLayer.Override, Value = 1 },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Increased, Value = 0.30f },
                new StatModifier { Stat = "crit_threshold", Layer = ModifierLayer.Override, Value = 20 }
            }
        });
    }

    private static void RegisterLoneOp()
    {
        Register("lone_op", new BuffInstance
        {
            Id = "lone_op", Name = "孤军行", Description = "周围无友军全效 AC+4, AP-1, 伤+4, 暴-2; 有友军减半",
            IconId = "IconDark", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 4, Condition = "lone_operative" },
                new StatModifier { Stat = "move_ap_reduction", Layer = ModifierLayer.Base, Value = 1, Condition = "lone_operative" },
                new StatModifier { Stat = "damage", Layer = ModifierLayer.Base, Value = 4, Condition = "lone_operative" },
                new StatModifier { Stat = "crit_threshold", Layer = ModifierLayer.Base, Value = -2, Condition = "lone_operative" }
            }
        });
    }

    private static void RegisterParagon()
    {
        Register("paragon", new BuffInstance
        {
            Id = "paragon", Name = "万象之王", Description = "全属性+3, 攻/AC/DR/豁+3, 移+2, 大节点+30%, 士下保0",
            IconId = "IconBless", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career" },
            Modifiers = new()
            {
                new StatModifier { Stat = "all_stats", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "node_bonus", Layer = ModifierLayer.Base, Value = 0.30f },
                new StatModifier { Stat = "attack_bonus", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "ac", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "dr_threshold", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "speed", Layer = ModifierLayer.Base, Value = 2 },
                new StatModifier { Stat = "save_bonus", Layer = ModifierLayer.Base, Value = 3 },
                new StatModifier { Stat = "lockout_other_career_skills", Layer = ModifierLayer.Override, Value = 1 }
            }
        });
    }

    private static void RegisterRuneAura()
    {
        Register("rune_aura", new BuffInstance
        {
            Id = "rune_aura", Name = "铁焰壁垒", Description = "相邻友军DR原值+1",
            IconId = "IconShield", IsNegative = false, Duration = -1,
            Tags = new[] { "buff", "career", "aura" },
            Modifiers = new()
            {
                new StatModifier { Stat = "dr_threshold", Layer = ModifierLayer.Base, Value = 1 }
            }
        });
    }
}
