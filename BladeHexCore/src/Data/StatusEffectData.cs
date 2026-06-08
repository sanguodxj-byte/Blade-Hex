// StatusEffectData.cs
// 状态效果数据 — 所有战斗中的正面/负面状态效果定义
// 对应策划案 03-战术战斗系统 → 七、战斗状态效果
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class StatusEffectData : Resource
{
    // ========================================
    // 状态效果枚举（13个负面 + 7个正面）
    // ========================================

    public enum EffectId
    {
        // 负面状态
        Poison,      // 中毒
        Burning,     // 燃烧
        Freeze,      // 冰冻
        Fear,        // 恐惧
        Silence,     // 沉默
        Blind,       // 致盲
        Stun,        // 眩晕
        Bleed,       // 流血
        Slow,        // 减速
        Root,        // 缚足
        Charmed,     // 魅惑
        Confused,    // 困惑
        Wet,         // 潮湿

        // 正面状态
        Bless,       // 祝福
        Shield,      // 护盾
        Haste,       // 加速
        Regen,       // 再生
        Invisibility,// 隐身
        Phantom,     // 幻影
        TempHp,      // 临时HP
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public EffectId effectId = EffectId.Poison;
    [Export] public string EffectName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public bool IsNegative { get; set; } = true;
    [Export] public int DefaultDuration { get; set; } = 3;

    // 每回合伤害骰子
    [Export] public int TickDamageDiceCount;
    [Export] public int TickDamageDiceSides;
    [Export] public string TickDamageType { get; set; } = "";

    // 属性修正
    [Export] public Godot.Collections.Dictionary StatModifiers = new();

    // 可通过豁免解除
    [Export] public string SaveToRemove { get; set; } = "";
    [Export] public int SaveDc { get; set; } = 12;

    // 可解除的其他效果
    [Export] public string[] RemovesEffects = [];

    // 互斥标签
    [Export] public string CancelTag { get; set; } = "";

    // 是否攻击后解除
    [Export] public bool BreaksOnAttack;

    // 是否可蔓延
    [Export] public bool CanSpread;

    // ========================================
    // 静态工厂：预定义所有效果
    // ========================================

    public static StatusEffectData CreateEffect(EffectId id)
    {
        var e = new StatusEffectData();
        e.effectId = id;

        switch (id)
        {
            // ====== 负面状态 ======
            case EffectId.Poison:
                e.EffectName = "中毒"; e.Description = "每回合开始受到1d4伤害";
                e.IsNegative = true; e.DefaultDuration = 3;
                e.TickDamageDiceCount = 1; e.TickDamageDiceSides = 4; e.TickDamageType = "poison";
                e.SaveToRemove = "fortitude"; e.SaveDc = 12;
                break;
            case EffectId.Burning:
                e.EffectName = "燃烧"; e.Description = "每回合开始受到1d6伤害，可蔓延至相邻";
                e.IsNegative = true; e.DefaultDuration = 3;
                e.TickDamageDiceCount = 1; e.TickDamageDiceSides = 6; e.TickDamageType = "fire";
                e.CancelTag = "fire"; e.RemovesEffects = ["freeze"]; e.CanSpread = true;
                break;
            case EffectId.Freeze:
                e.EffectName = "冰冻"; e.Description = "本回合不可行动，AC-2";
                e.IsNegative = true; e.DefaultDuration = 1;
                e.StatModifiers = new Godot.Collections.Dictionary { { "ac", -2 }, { "cannot_act", true } };
                e.CancelTag = "ice"; e.RemovesEffects = ["burning"];
                break;
            case EffectId.Fear:
                e.EffectName = "恐惧"; e.Description = "必须向远离源的方向移动，不可主动攻击";
                e.IsNegative = true; e.DefaultDuration = 2;
                e.SaveToRemove = "will"; e.SaveDc = 15;
                break;
            case EffectId.Silence:
                e.EffectName = "沉默"; e.Description = "不能施放法术";
                e.IsNegative = true; e.DefaultDuration = 2;
                e.StatModifiers = new Godot.Collections.Dictionary { { "cannot_cast", true } };
                break;
            case EffectId.Blind:
                e.EffectName = "致盲"; e.Description = "近战攻击劣势，远程攻击必须相邻";
                e.IsNegative = true; e.DefaultDuration = 2;
                e.StatModifiers = new Godot.Collections.Dictionary { { "melee_disadvantage", true }, { "ranged_range_override", 1 } };
                break;
            case EffectId.Stun:
                e.EffectName = "眩晕"; e.Description = "本回合只能移动或攻击（二选一）";
                e.IsNegative = true; e.DefaultDuration = 1;
                e.StatModifiers = new Godot.Collections.Dictionary { { "action_restricted", true } };
                break;
            case EffectId.Bleed:
                e.EffectName = "流血"; e.Description = "每回合开始受到1d4伤害，可叠加";
                e.IsNegative = true; e.DefaultDuration = 99;
                e.TickDamageDiceCount = 1; e.TickDamageDiceSides = 4; e.TickDamageType = "bleed";
                break;
            case EffectId.Slow:
                e.EffectName = "减速"; e.Description = "移动速度-2（最小1）";
                e.IsNegative = true; e.DefaultDuration = 2;
                e.StatModifiers = new Godot.Collections.Dictionary { { "speed", -2 } };
                break;
            case EffectId.Root:
                e.EffectName = "缚足"; e.Description = "不能移动，可攻击";
                e.IsNegative = true; e.DefaultDuration = 2;
                e.StatModifiers = new Godot.Collections.Dictionary { { "cannot_move", true } };
                e.SaveToRemove = "fortitude"; e.SaveDc = 15;
                break;
            case EffectId.Charmed:
                e.EffectName = "魅惑"; e.Description = "不能攻击施法者";
                e.IsNegative = true; e.DefaultDuration = 1;
                break;
            case EffectId.Confused:
                e.EffectName = "困惑"; e.Description = "随机行动";
                e.IsNegative = true; e.DefaultDuration = 1;
                break;
            case EffectId.Wet:
                e.EffectName = "潮湿"; e.Description = "中性状态，火焰抗性+2，冰霜/雷电弱点";
                e.IsNegative = false; e.DefaultDuration = 3;
                e.CancelTag = "wet"; e.RemovesEffects = ["burning"];
                e.StatModifiers = new Godot.Collections.Dictionary { { "fire_resist", 2 }, { "ice_weakness", true }, { "lightning_weakness", true } };
                break;

            // ====== 正面状态 ======
            case EffectId.Bless:
                e.EffectName = "祝福"; e.Description = "攻击/豁免+1d4";
                e.IsNegative = false; e.DefaultDuration = 3;
                e.StatModifiers = new Godot.Collections.Dictionary { { "attack_bonus_dice", 4 }, { "save_bonus_dice", 4 } };
                break;
            case EffectId.Shield:
                e.EffectName = "护盾"; e.Description = "AC+5";
                e.IsNegative = false; e.DefaultDuration = 1;
                e.StatModifiers = new Godot.Collections.Dictionary { { "ac", 5 } };
                break;
            case EffectId.Haste:
                e.EffectName = "加速"; e.Description = "移动+2，额外1次次要行动";
                e.IsNegative = false; e.DefaultDuration = 3;
                e.StatModifiers = new Godot.Collections.Dictionary { { "speed", 2 }, { "extra_minor_action", true } };
                break;
            case EffectId.Regen:
                e.EffectName = "再生"; e.Description = "每回合开始恢复1d6 HP";
                e.IsNegative = false; e.DefaultDuration = 3;
                e.TickDamageDiceCount = 1; e.TickDamageDiceSides = -6; // 负值=治疗
                break;
            case EffectId.Invisibility:
                e.EffectName = "隐身"; e.Description = "不可被直接瞄准，AOE有效，攻击后解除";
                e.IsNegative = false; e.DefaultDuration = 99;
                e.BreaksOnAttack = true;
                break;
            case EffectId.Phantom:
                e.EffectName = "幻影"; e.Description = "攻击者需先命中幻影(AC12)，命中则消耗幻影";
                e.IsNegative = false; e.DefaultDuration = 99;
                e.StatModifiers = new Godot.Collections.Dictionary { { "phantom_ac", 12 } };
                break;
            case EffectId.TempHp:
                e.EffectName = "临时HP"; e.Description = "额外HP层，先于本体HP消耗";
                e.IsNegative = false; e.DefaultDuration = 99;
                break;
        }

        return e;
    }

    // ========================================
    // 状态交互规则
    // ========================================

    /// <summary>
    /// 检查两个效果的交互结果
    /// 返回 Dictionary { "action": string, "value": Variant }
    /// action: "cancel_both" | "cancel_a" | "cancel_b" | "extend_b" | "extend_a" | "boost_damage" | "spread" | "none"
    /// </summary>
    public static Godot.Collections.Dictionary GetInteraction(string effectA, string effectB)
    {
        // 燃烧 + 冰冻 → 互相解除
        if ((effectA == "burning" && effectB == "freeze") ||
            (effectA == "freeze" && effectB == "burning"))
            return new Godot.Collections.Dictionary { { "action", "cancel_both" } };

        // 燃烧 + 油类 → 无特殊
        if (effectA == "burning" && effectB == "wet")
            return new Godot.Collections.Dictionary { { "action", "none" } };
        if (effectB == "burning" && effectA == "wet")
            return new Godot.Collections.Dictionary { { "action", "none" } };

        // 潮湿 + 冰系 → 冰冻持续时间+1
        if (effectA == "wet" && effectB == "freeze")
            return new Godot.Collections.Dictionary { { "action", "extend_b" }, { "value", 1 } };
        if (effectB == "wet" && effectA == "freeze")
            return new Godot.Collections.Dictionary { { "action", "extend_a" }, { "value", 1 } };

        // 潮湿 + 雷系 → 雷电伤害+50%
        if (effectA == "wet" && effectB == "lightning_damage")
            return new Godot.Collections.Dictionary { { "action", "boost_damage" }, { "value", 1.5 } };
        if (effectB == "wet" && effectA == "lightning_damage")
            return new Godot.Collections.Dictionary { { "action", "boost_damage" }, { "value", 1.5 } };

        // 中毒 + 燃烧 → 毒雾扩散
        if (effectA == "poison" && effectB == "burning")
            return new Godot.Collections.Dictionary { { "action", "spread" }, { "value", "poison_cloud" } };
        if (effectB == "poison" && effectA == "burning")
            return new Godot.Collections.Dictionary { { "action", "spread" }, { "value", "poison_cloud" } };

        // (士气系统已移除)

        return new Godot.Collections.Dictionary { { "action", "none" } };
    }

    /// <summary>获取效果显示名</summary>
    public static string GetEffectName(EffectId id) => CreateEffect(id).EffectName;
}
