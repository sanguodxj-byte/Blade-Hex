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
