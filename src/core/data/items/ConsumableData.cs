// ConsumableData.cs
// 消耗品数据 — 战斗中可使用的药剂、投掷物、卷轴等
// 对应策划案 06-装备与物品 → 物品与消耗品
// 迁移自 GDScript ConsumableData.gd
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class ConsumableData : ItemData
{
    // ========================================
    // 消耗品类型
    // ========================================

    public enum ConsumableType
    {
        HealingPotion,   // 治疗药水
        StrongHealing,   // 强效治疗药水
        Antidote,        // 解毒剂
        FireOil,         // 火油瓶
        HolyWater,       // 圣水
        SpellScroll,     // 法术卷轴
        Whetstone,       // 磨刀石（战斗外）
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public ConsumableType consumableType = ConsumableType.HealingPotion;

    // 治疗骰子
    [Export] public int HealDiceCount;
    [Export] public int HealDiceSides;
    [Export] public int HealBonus;

    // 可解除的状态效果ID列表
    [Export] public string[] RemovesStatus = [];

    // 伤害骰子（投掷物用）
    [Export] public int DamageDiceCount;
    [Export] public int DamageDiceSides;
    [Export] public string DamageType = "";

    // 范围伤害半径（0=单体，1=周围1格）
    [Export] public int AoeRadius;

    // 投掷射程（格子）
    [Export] public int ThrowRange = 4;

    // 关联法术（卷轴用）— 存储 SpellData 的 SpellId
    [Export] public string LinkedSpellId = "";

    // 使用时机：main_action / minor_action
    [Export] public string UseAction = "main_action";

    // 战斗外使用
    [Export] public bool UsableOutsideCombat;

    // 使用后附带的状态效果
    [Export] public string AppliedStatus = "";
    [Export] public int AppliedStatusDuration;

    // ========================================
    // 辅助方法
    // ========================================

    public string GetConsumableTypeName() => consumableType switch
    {
        ConsumableType.HealingPotion => "治疗药水",
        ConsumableType.StrongHealing => "强效治疗药水",
        ConsumableType.Antidote => "解毒剂",
        ConsumableType.FireOil => "火油瓶",
        ConsumableType.HolyWater => "圣水",
        ConsumableType.SpellScroll => "法术卷轴",
        ConsumableType.Whetstone => "磨刀石",
        _ => "未知",
    };

    public bool IsThrowable() =>
        consumableType == ConsumableType.FireOil || consumableType == ConsumableType.HolyWater;

    public string GetEffectText() => consumableType switch
    {
        ConsumableType.HealingPotion => $"恢复{HealDiceCount}d{HealDiceSides}+{HealBonus} HP",
        ConsumableType.StrongHealing => $"恢复{HealDiceCount}d{HealDiceSides}+{HealBonus} HP",
        ConsumableType.Antidote => "解除中毒状态",
        ConsumableType.FireOil => $"投掷至目标格，范围内{DamageDiceCount}d{DamageDiceSides}火伤×{AppliedStatusDuration}轮",
        ConsumableType.HolyWater => $"投掷至目标格，亡灵{DamageDiceCount}d{DamageDiceSides}伤害",
        ConsumableType.SpellScroll => LinkedSpellId != "" ? $"施放一次{LinkedSpellId}" : "施放一次法术",
        ConsumableType.Whetstone => "本场战斗近战伤害+1",
        _ => "",
    };
}
