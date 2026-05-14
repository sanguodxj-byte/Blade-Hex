using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Combat;

/// <summary>
/// 技能注册表 — 纯数据类，存储所有技能效果配置
/// 作为技能数据的单一真相源
/// </summary>
public static class SkillRegistry
{
    // ============================================================================
    // 技能分类与目标类型
    // ============================================================================

    public enum SkillCategory
    {
        MeleeActive,    // 近战主动
        RangedActive,   // 远程主动
        MagicActive,    // 法术主动
        HealActive,     // 治疗主动
        SupportActive,  // 辅助主动
        Passive,        // 被动（常驻修正）
        Keystone,       // 代价型被动
        OutOfCombat,    // 非战斗效果
    }

    public enum TargetType
    {
        Self,           // 自身
        SingleEnemy,    // 单个敌人
        SingleAlly,     // 单个友军（含自身）
        AllAdjacent,    // 周围所有（六邻格）
        AoeSmall,       // 小范围 AoE（半径1）
        AoeCone,        // 锥形范围
        RangedSingle,   // 远程单体
        RangedAoe,      // 远程 AoE
        AllAllies,      // 所有友军
    }

    // ============================================================================
    // 技能注册表数据
    // ============================================================================

    private static readonly Dictionary<string, Godot.Collections.Dictionary> Registry = new()
    {
        // STR 力量区域 — 主动技能
        { "double_attack", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MeleeActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "连击" },
            { "description", "攻击2次，第二次-3命中" },
            { "vfx", "melee_combo" },
            { "action_cost", 4 }
        }},
        { "whirlwind", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MeleeActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "旋风斩" },
            { "description", "攻击周围所有敌人" },
            { "vfx", "whirlwind" },
            { "action_cost", 5 }
        }},
        { "battle_cry", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "战斗怒吼" },
            { "description", "震慑周围敌人下回合攻击-2，友军士气+3" },
            { "vfx", "war_cry" },
            { "action_cost", 4 }
        }},
        { "blood_vortex", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MeleeActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "血腥漩涡" },
            { "description", "横扫周围所有敌人，每命中1个恢复1d6 HP" },
            { "vfx", "blood_vortex" },
            { "action_cost", 5 }
        }},

        // STR 力量区域 — 被动技能
        { "melee_hit_plus_1", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "基础剑术" },
            { "description", "近战命中+1" }
        }},
        { "weapon_mastery", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "武器精通" },
            { "description", "武器伤害+10%/级（不加护甲/盾牌任何数值）" }
        }},
        { "critical_x3", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "重击" },
            { "description", "暴击伤害x3" }
        }},
        { "iron_will", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "坚韧意志" },
            { "description", "致命伤害时强韧豁免DC15存活于1HP" }
        }},
        { "berserk_power", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Keystone },
            { "target", (int)TargetType.Self },
            { "name", "狂暴之力" },
            { "description", "近战伤害+50%" },
            { "cost", "AC-3，不能使用盾牌" }
        }},
        { "heavy_armor", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "重甲精通" },
            { "description", "AC+3，速度-1" }
        }},
        { "critical_master", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "暴击大师" },
            { "description", "暴击伤害×3" }
        }},
        { "bloodthirst", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Keystone },
            { "target", (int)TargetType.Self },
            { "name", "嗜血" },
            { "description", "近战击杀后获得额外行动" },
            { "cost", "每回合首次受伤+50%" }
        }},

        // DEX 灵巧区域 — 主动技能
        { "aimed_shot", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.RangedActive },
            { "target", (int)TargetType.RangedSingle },
            { "name", "精准射击" },
            { "description", "瞄准后射击优势+伤害x2" },
            { "vfx", "aimed_shot" },
            { "action_cost", 8 }
        }},
        { "double_shot", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.RangedActive },
            { "target", (int)TargetType.RangedSingle },
            { "name", "双重射击" },
            { "description", "射击2个目标各-2命中" },
            { "vfx", "double_shot" },
            { "action_cost", 6 }
        }},
        { "scatter_shot", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.RangedActive },
            { "target", (int)TargetType.AoeCone },
            { "name", "散射" },
            { "description", "锥形范围射击" },
            { "vfx", "scatter_shot" },
            { "action_cost", 6 }
        }},
        { "stealth", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.Self },
            { "name", "隐匿" },
            { "description", "进入潜行状态" },
            { "vfx", "stealth" },
            { "action_cost", 4 }
        }},
        { "shadow_clone", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.Self },
            { "name", "影分身" },
            { "description", "位移+残影，下次攻击自动闪避1次" },
            { "vfx", "shadow_clone" },
            { "action_cost", 4 }
        }},
        { "trick_arrow", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.RangedActive },
            { "target", (int)TargetType.RangedSingle },
            { "name", "元素箭" },
            { "description", "1d10+随机debuff(失明/倒地/震慑)" },
            { "vfx", "trick_arrow" },
            { "action_cost", 4 }
        }},
        { "poison_blade", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MeleeActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "毒刃" },
            { "description", "攻击附带中毒" },
            { "vfx", "poison_blade" },
            { "action_cost", 3 }
        }},
        { "multi_shot", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.RangedActive },
            { "target", (int)TargetType.RangedAoe },
            { "name", "连射" },
            { "description", "连射3支箭攻击区域内目标" },
            { "vfx", "multi_shot" },
            { "action_cost", 6 }
        }},
        { "blind_arrow", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.RangedActive },
            { "target", (int)TargetType.RangedSingle },
            { "name", "盲射" },
            { "description", "命中后目标-4命中持续2回合" },
            { "vfx", "blind_arrow" },
            { "action_cost", 4 }
        }},
        { "shadow_strike", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MeleeActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "暗影突袭" },
            { "description", "潜行状态下突袭，伤害翻倍" },
            { "vfx", "shadow_strike" },
            { "action_cost", 5 }
        }},
        { "sword_dance", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MeleeActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "剑舞" },
            { "description", "攻击周围所有敌人（高伤害倍率）" },
            { "vfx", "sword_dance" },
            { "action_cost", 6 }
        }},
        { "trap_master", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.Self },
            { "name", "陷阱大师" },
            { "description", "放置陷阱，触发时造成伤害+减速" },
            { "vfx", "trap" },
            { "action_cost", 4 }
        }},
        { "lightning_reflex", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Keystone },
            { "target", (int)TargetType.Self },
            { "name", "闪电反射" },
            { "description", "先攻+5，首轮攻击优势" },
            { "cost", "AC-1" }
        }},
        { "meteor_shower", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.RangedActive },
            { "target", (int)TargetType.RangedAoe },
            { "name", "流星箭雨" },
            { "description", "区域箭雨2d8伤害" },
            { "vfx", "meteor_shower" },
            { "action_cost", 8 }
        }},

        // CON 体魄区域
        { "shield_bash", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MeleeActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "盾击" },
            { "description", "攻击+推开目标1格" },
            { "vfx", "shield_bash" },
            { "action_cost", 5 }
        }},
        { "taunt", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "嘲讽" },
            { "description", "强制周围敌人攻击自己" },
            { "vfx", "taunt" },
            { "action_cost", 4 }
        }},
        { "unyielding_bulwark", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.Self },
            { "name", "不屈壁垒" },
            { "description", "受伤减半+临时HP" },
            { "vfx", "bulwark" },
            { "action_cost", 4 }
        }},
        { "fortify", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "坚守" },
            { "description", "受伤时临时AC+2" }
        }},
        { "iron_wall", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "铁壁" },
            { "description", "物理伤害减免3" }
        }},
        { "unyielding", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "不屈" },
            { "description", "HP<25%时受到伤害减半" }
        }},
        { "life_shield", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.Self },
            { "name", "生命护盾" },
            { "description", "获得临时护盾=30%最大HP" },
            { "vfx", "life_shield" },
            { "action_cost", 4 }
        }},
        { "immortal_body", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Keystone },
            { "target", (int)TargetType.Self },
            { "name", "不灭之躯" },
            { "description", "HP归0时CON检定DC15存活于1HP（每战1次）" },
            { "cost", "速度-2" }
        }},
        { "life_circle", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.HealActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "生命之环" },
            { "description", "治疗周围友军2d10+CON修正" },
            { "vfx", "life_circle" },
            { "action_cost", 5 }
        }},
        { "giant_strength", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "巨人之力" },
            { "description", "近战伤害+3，单手武器视为双手" }
        }},
        { "field_medic", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.HealActive },
            { "target", (int)TargetType.SingleAlly },
            { "name", "战地医疗" },
            { "description", "恢复友军HP并解除流血/中毒" },
            { "vfx", "heal" },
            { "action_cost", 4 }
        }},

        // INT 智力区域
        { "mana_shield", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.Self },
            { "name", "魔力护盾" },
            { "description", "消耗5魔力获得护盾" },
            { "vfx", "mana_shield" },
            { "action_cost", 4 }
        }},
        { "time_warp", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.Self },
            { "name", "时间扭曲" },
            { "description", "消耗10魔力获得额外次要行动" },
            { "vfx", "time_warp" },
            { "action_cost", 4 }
        }},
        { "spell_hit_plus_1", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "基础法术" },
            { "description", "法术命中+1" }
        }},
        { "arcane_burst", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "奥术爆发" },
            { "description", "2d8奥术伤害" },
            { "vfx", "arcane_burst" },
            { "action_cost", 5 }
        }},
        { "mana_drain", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "法力汲取" },
            { "description", "吸取目标法力恢复自身" },
            { "vfx", "mana_drain" },
            { "action_cost", 4 }
        }},
        { "chain_lightning", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "连锁闪电" },
            { "description", "闪电跳跃攻击最多3个目标" },
            { "vfx", "chain_lightning" },
            { "action_cost", 6 }
        }},
        { "spell_reflect", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "法术反射" },
            { "description", "每回合反射1次法术" }
        }},
        { "arcane_bomb", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.AoeSmall },
            { "name", "奥术炸弹" },
            { "description", "3d6范围奥术伤害" },
            { "vfx", "arcane_bomb" },
            { "action_cost", 6 }
        }},
        { "absolute_focus", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "绝对专注" },
            { "description", "法术强度+4" }
        }},
        { "knowledge_power", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "知识之力" },
            { "description", "法术伤害+INT修正值" }
        }},
        { "void_gate", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.Self },
            { "name", "虚空之门" },
            { "description", "传送至目标位置" },
            { "vfx", "void_gate" },
            { "action_cost", 5 }
        }},
        { "fate_eye", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Keystone },
            { "target", (int)TargetType.Self },
            { "name", "命运之眼" },
            { "description", "每回合可重骰1次失败豁免" },
            { "cost", "法力上限-10" }
        }},

        // WIS 感知区域
        { "basic_heal", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.HealActive },
            { "target", (int)TargetType.SingleAlly },
            { "name", "基础治疗" },
            { "description", "恢复友军1d8+WIS修正HP" },
            { "vfx", "heal" },
            { "action_cost", 4 }
        }},
        { "blessing", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.SingleAlly },
            { "name", "祈福" },
            { "description", "友军加成" },
            { "vfx", "blessing" },
            { "action_cost", 4 }
        }},
        { "group_heal", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.HealActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "群体治疗" },
            { "description", "治疗周围友军1d6+WIS修正" },
            { "vfx", "group_heal" },
            { "action_cost", 5 }
        }},
        { "purifying_flame", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.AoeSmall },
            { "name", "净化之焰" },
            { "description", "照亮区域+亡灵额外伤害" },
            { "vfx", "purifying_flame" },
            { "action_cost", 5 }
        }},
        { "guardian_spirit", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.SingleAlly },
            { "name", "守护之灵" },
            { "description", "替目标挡下一次致命攻击" },
            { "vfx", "guardian_spirit" },
            { "action_cost", 5 }
        }},
        { "resurrect", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.HealActive },
            { "target", (int)TargetType.SingleAlly },
            { "name", "复活" },
            { "description", "复活倒下友军恢复50%HP" },
            { "vfx", "resurrect" },
            { "action_cost", 8 }
        }},
        { "arcane_judgment", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "奥术审判" },
            { "description", "3d10奥术伤害" },
            { "vfx", "arcane_judgment" },
            { "action_cost", 7 }
        }},
        { "life_mastery", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "生命精通" },
            { "description", "治疗效果+50%" }
        }},
        { "oracle", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.Self },
            { "name", "神谕" },
            { "description", "揭示隐藏敌人/陷阱" },
            { "vfx", "oracle" },
            { "action_cost", 3 }
        }},
        { "elemental_storm", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.MagicActive },
            { "target", (int)TargetType.RangedAoe },
            { "name", "元素风暴" },
            { "description", "区域2d8元素伤害" },
            { "vfx", "elemental_storm" },
            { "action_cost", 7 }
        }},
        { "soul_guardian", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Keystone },
            { "target", (int)TargetType.Self },
            { "name", "灵魂守护" },
            { "description", "友军死亡时触发恢复（每战1次）" },
            { "cost", "自身最大HP-10%" }
        }},

        // CHA 魅力区域
        { "war_cry", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "战吼" },
            { "description", "范围内友军加攻" },
            { "vfx", "war_cry" },
            { "action_cost", 4 }
        }},
        { "inspire", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.AllAllies },
            { "name", "鼓舞士气" },
            { "description", "所有友军士气+2" },
            { "vfx", "inspire" },
            { "action_cost", 4 }
        }},
        { "command", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.SingleAlly },
            { "name", "指挥" },
            { "description", "指令友军立即行动一次" },
            { "vfx", "command" },
            { "action_cost", 5 }
        }},
        { "rally", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "集结" },
            { "description", "周围友军攻击+2持续2回合" },
            { "vfx", "rally" },
            { "action_cost", 4 }
        }},
        { "diplomat", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.OutOfCombat },
            { "target", (int)TargetType.Self },
            { "name", "外交官" },
            { "description", "商店/招募-15%" }
        }},
        { "shadow_deal", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.SingleEnemy },
            { "name", "暗影交易" },
            { "description", "贿赂敌人使其退出战斗" },
            { "vfx", "shadow_deal" },
            { "action_cost", 6 }
        }},
        { "command_aura", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Passive },
            { "target", (int)TargetType.Self },
            { "name", "指挥光环" },
            { "description", "周围友军攻击+1，AC+1" }
        }},
        { "intimidate", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.AllAdjacent },
            { "name", "威慑" },
            { "description", "周围敌人攻击-2持续3回合" },
            { "vfx", "intimidate" },
            { "action_cost", 4 }
        }},
        { "royal_presence", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Keystone },
            { "target", (int)TargetType.Self },
            { "name", "王者之威" },
            { "description", "全队豁免+2" },
            { "cost", "自身最大HP-20%" }
        }},
        { "heroic_call", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.SupportActive },
            { "target", (int)TargetType.AllAllies },
            { "name", "英雄号召" },
            { "description", "所有友军攻击+2，AC+1持续2回合" },
            { "vfx", "heroic_call" },
            { "action_cost", 6 }
        }},
        { "merchant_empire", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.OutOfCombat },
            { "target", (int)TargetType.Self },
            { "name", "商业帝国" },
            { "description", "金币+20%，稀有物品概率+15%" }
        }},
        { "vow_of_vengeance", new Godot.Collections.Dictionary {
            { "category", (int)SkillCategory.Keystone },
            { "target", (int)TargetType.Self },
            { "name", "复仇誓言" },
            { "description", "标记目标对其伤害+25%，目标死亡全队恢复10%" },
            { "cost", "不能治疗被标记目标以外的敌人" }
        }}
    };

    // ============================================================================
    // 查询接口
    // ============================================================================

    public static Godot.Collections.Dictionary GetSkillConfig(string skillEffect)
    {
        if (Registry.TryGetValue(skillEffect, out var config)) return config;
        return new Godot.Collections.Dictionary();
    }

    public static bool IsActiveSkill(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.Count == 0) return false;
        int cat = cfg["category"].AsInt32();
        return cat == (int)SkillCategory.MeleeActive || cat == (int)SkillCategory.RangedActive ||
               cat == (int)SkillCategory.MagicActive || cat == (int)SkillCategory.HealActive ||
               cat == (int)SkillCategory.SupportActive;
    }

    public static bool IsPassiveSkill(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.Count == 0) return false;
        int cat = cfg["category"].AsInt32();
        return cat == (int)SkillCategory.Passive || cat == (int)SkillCategory.Keystone || cat == (int)SkillCategory.OutOfCombat;
    }

    public static string[] GetAllActiveSkillIds()
    {
        return Registry.Keys.Where(IsActiveSkill).ToArray();
    }
}
