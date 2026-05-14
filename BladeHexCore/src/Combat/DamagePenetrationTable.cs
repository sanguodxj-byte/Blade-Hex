// DamagePenetrationTable.cs
// 护甲穿透系数表 — 按武器属性 (DamageType × WeightCategory) 索引
// 单一真相源：所有穿透 HP/DR 分配比例只在此文件定义
// 对应策划案 03-战术战斗系统 → 护甲穿透规则
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 穿透系数对 — 描述一次伤害在 HP 与 DR 之间的分配比例
/// </summary>
public readonly struct PenetrationCoefficient
{
    /// <summary>穿透成功时，伤害进入 HP 的比例</summary>
    public float HpRatioPenetrated { get; init; }

    /// <summary>穿透成功时，伤害进入 DR（护甲耐久）的比例</summary>
    public float DrRatioPenetrated { get; init; }

    /// <summary>未穿透时，伤害进入 HP 的比例（通常为 0，Crush 例外）</summary>
    public float HpRatioBlocked { get; init; }

    /// <summary>未穿透时，伤害进入 DR 的比例</summary>
    public float DrRatioBlocked { get; init; }
}

/// <summary>
/// 护甲穿透系数查找表 — 按 (DamageType, WeightCategory) 索引
/// <para>
/// 设计意图：
/// - Slash 轻/中型武器(匕首、单手剑)穿透后 90% 进 HP、10% 磨甲 → 高杀伤低破甲
/// - Slash 重型武器(巨剑、大斧)穿透后 70% 进 HP、30% 磨甲 → 兼顾杀伤与破甲
/// - Pierce 全重量穿透后 100% 进 HP → 极致穿刺，不磨甲
/// - Crush 全重量穿透后 30% 进 HP、70% 磨甲 → 主打破甲
/// - Magic/Fire/Frost/Lightning 无视护甲 → 100% HP
/// </para>
/// </summary>
public static class DamagePenetrationTable
{
    // ========================================
    // 系数表定义
    // ========================================

    // --- Slash ---
    private static readonly PenetrationCoefficient SlashLightMedium = new()
    {
        HpRatioPenetrated = 0.9f,
        DrRatioPenetrated = 0.1f,
        HpRatioBlocked = 0f,
        DrRatioBlocked = 0.4f,
    };

    private static readonly PenetrationCoefficient SlashHeavy = new()
    {
        HpRatioPenetrated = 0.7f,
        DrRatioPenetrated = 0.3f,
        HpRatioBlocked = 0f,
        DrRatioBlocked = 0.4f,
    };

    // --- Pierce ---
    private static readonly PenetrationCoefficient PierceAll = new()
    {
        HpRatioPenetrated = 1.0f,
        DrRatioPenetrated = 0f,
        HpRatioBlocked = 0f,
        DrRatioBlocked = 0.1f,
    };

    // --- Crush ---
    private static readonly PenetrationCoefficient CrushAll = new()
    {
        HpRatioPenetrated = 0.3f,
        DrRatioPenetrated = 0.7f,
        HpRatioBlocked = 0.1f,
        DrRatioBlocked = 0.9f,
    };

    // --- 魔法类（无视护甲）---
    private static readonly PenetrationCoefficient MagicAll = new()
    {
        HpRatioPenetrated = 1.0f,
        DrRatioPenetrated = 0f,
        HpRatioBlocked = 1.0f,
        DrRatioBlocked = 0f,
    };

    // ========================================
    // 查找 API
    // ========================================

    /// <summary>
    /// 按武器伤害类型和重量类别查找穿透系数
    /// </summary>
    /// <param name="damageType">武器伤害类型</param>
    /// <param name="weight">武器重量类别（Light/Medium/Heavy）</param>
    /// <returns>对应的穿透系数对</returns>
    public static PenetrationCoefficient Lookup(
        WeaponData.DamageType damageType,
        WeaponData.WeightCategory weight = WeaponData.WeightCategory.Medium)
    {
        return damageType switch
        {
            WeaponData.DamageType.Slash => weight == WeaponData.WeightCategory.Heavy
                ? SlashHeavy
                : SlashLightMedium,

            WeaponData.DamageType.Pierce => PierceAll,

            WeaponData.DamageType.Crush => CrushAll,

            // 魔法类伤害无视护甲
            WeaponData.DamageType.Magic => MagicAll,
            WeaponData.DamageType.Fire => MagicAll,
            WeaponData.DamageType.Frost => MagicAll,
            WeaponData.DamageType.Lightning => MagicAll,

            _ => MagicAll, // 未知类型默认全 HP
        };
    }
}
