// EquipmentAbilityRegistry.cs
// 装备能力工厂中央注册表
//
// JSON id (snake_case) → EquipmentAbility 实例工厂
// JSON 加载器调用 Create 创建能力实例并挂到物品上
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Combat.Abilities;

/// <summary>
/// 装备能力注册表 — 单点添加新能力。
/// 加新能力时只需在 RegisterAll() 中添加一行映射。
/// </summary>
public static class EquipmentAbilityRegistry
{
    private static readonly Dictionary<string, Func<float, EquipmentAbility>> _factories = new();
    private static bool _registered = false;

    private static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;
        RegisterAll();
    }

    private static void RegisterAll()
    {
        // 战斗触发型
        _factories["life_steal"] = v => new LifestealAbility { AbilityId = "life_steal", Magnitude = v };
        _factories["thorns"] = v => new ThornsAbility { AbilityId = "thorns", Magnitude = v };

        // 静态修正型
        _factories["extra_hp_percent"] = v => new ExtraHpPercentAbility { AbilityId = "extra_hp_percent", Magnitude = v };
        _factories["damage_reduction"] = v => new DamageReductionAbility { AbilityId = "damage_reduction", Magnitude = v };
        _factories["spell_dc_bonus"] = v => new SpellDcBonusAbility { AbilityId = "spell_dc_bonus", Magnitude = v };
        _factories["shop_discount"] = v => new ShopDiscountAbility { AbilityId = "shop_discount", Magnitude = v };
        _factories["recruit_discount"] = v => new RecruitDiscountAbility { AbilityId = "recruit_discount", Magnitude = v };
        _factories["flanking_bonus"] = v => new FlankingBonusAbility { AbilityId = "flanking_bonus", Magnitude = v };
    }

    /// <summary>
    /// 通过 ID 创建能力实例。未知 ID 返回 null 并记录错误。
    /// </summary>
    public static EquipmentAbility? Create(string abilityId, float magnitude)
    {
        EnsureRegistered();
        if (string.IsNullOrEmpty(abilityId)) return null;
        if (_factories.TryGetValue(abilityId, out var factory))
            return factory(magnitude);

        GD.PushError($"[EquipmentAbilityRegistry] Unknown ability ID: '{abilityId}'");
        return null;
    }

    /// <summary>列出所有已注册的能力 ID（供 schema/调试使用）</summary>
    public static IEnumerable<string> GetAllRegisteredIds()
    {
        EnsureRegistered();
        return _factories.Keys;
    }
}
