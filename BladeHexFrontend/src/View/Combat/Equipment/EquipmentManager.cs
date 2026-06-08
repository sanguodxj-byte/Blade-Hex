using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 装备管理器 — 动态单位类型判定、双持/双手规则、盾牌、坐骑管理
/// </summary>
public static class EquipmentManager
{
    public enum UnitType { Melee, Ranged, Cavalry, Mage, DualWield, ShieldFighter, Mixed }

    public static UnitType GetUnitType(Unit unit)
    {
        if (unit.Data == null) return UnitType.Melee;

        var weapon = unit.Model.GetMainHand() as WeaponData;
        bool isMounted = unit.Data.IsMounted;
        bool hasShield = HasShield(unit);
        bool hasSpells = unit.Data.Skills.Count > 0; // 假设法术存在于技能列表中

        if (isMounted) return UnitType.Cavalry;

        if (weapon != null && weapon.IsCatalyst && hasSpells) return UnitType.Mage;

        if (weapon != null && weapon.IsRanged) return UnitType.Ranged;

        if (IsDualWielding(unit)) return UnitType.DualWield;

        if (hasShield) return UnitType.ShieldFighter;

        return UnitType.Melee;
    }

    public static string GetUnitTypeName(Unit unit) => GetUnitType(unit) switch
    {
        UnitType.Melee => "近战",
        UnitType.Ranged => "远程",
        UnitType.Cavalry => "骑兵",
        UnitType.Mage => "法师",
        UnitType.DualWield => "双持",
        UnitType.ShieldFighter => "盾战士",
        UnitType.Mixed => "混合",
        _ => "近战"
    };

    public static bool IsTwoHandedEquipped(Unit unit)
    {
        var weapon = unit.Model.GetMainHand() as WeaponData;
        return weapon != null && weapon.IsTwoHanded;
    }

    public static bool HasShield(Unit unit)
    {
        var offHand = unit.Model.GetOffHand();
        if (offHand is ArmorData armor && armor.armorType == ArmorData.ArmorType.Shield) return true;
        // 检查全局护甲槽位（如果盾牌被放在那里）
        // if (unit.Data.Armor != null && unit.Data.Armor.ArmorTypeValue == ArmorData.ArmorType.Shield) return true;
        return false;
    }

    public static bool IsDualWielding(Unit unit)
    {
        var main = unit.Model.GetMainHand() as WeaponData;
        var off = unit.Model.GetOffHand() as WeaponData;
        if (main != null && off != null)
        {
            return main.IsDualWieldable && !main.IsTwoHanded;
        }
        return false;
    }

    public static bool CanEquip(Unit unit, ItemData item, string slot)
    {
        if (unit.Data == null) return false;
        if (item is WeaponData weapon)
        {
            if (weapon.StrRequired > 0 && CombatStats.GetEffectiveStr(unit.Data) < weapon.StrRequired) return false;
        }
        if (item is ArmorData armor)
        {
            if (armor.StrRequired > 0 && CombatStats.GetEffectiveStr(unit.Data) < armor.StrRequired) return false;
        }
        return true;
    }

    public static void SwitchWeaponSet(Unit unit)
    {
        unit.UsingPrimaryWeapon = !unit.UsingPrimaryWeapon;
    }

    public static int GetAttackRange(Unit unit)
    {
        var weapon = unit.Model.GetMainHand() as WeaponData;
        if (weapon == null) return 1;
        if (weapon.IsReach) return 2;
        return weapon.RangeCells;
    }

    public static Godot.Collections.Dictionary GetWeaponTraits(Unit unit)
    {
        var weapon = unit.Model.GetMainHand() as WeaponData;
        var traits = new Godot.Collections.Dictionary();
        if (weapon == null) return traits;

        if (weapon.IsTwoHanded) traits["two_handed"] = true;
        if (weapon.IsFinesse) traits["finesse"] = true;
        if (weapon.IsRanged) traits["ranged"] = true;
        if (weapon.IsThrowing) traits["throwing"] = true;
        if (weapon.NeedsReload) traits["reload"] = true;
        if (weapon.IsBlunt) traits["blunt"] = true;
        if (weapon.IsArmorPiercing) traits["armor_piercing"] = true;
        if (weapon.IsReach) traits["reach"] = true;
        if (weapon.IsAntiCavalry) traits["anti_cavalry"] = true;
        if (weapon.IsSweep) traits["sweep"] = true;
        if (weapon.IsCatalyst) traits["catalyst"] = true;
        if (weapon.IsDualWieldable) traits["dual_wieldable"] = true;
        
        return traits;
    }
}
