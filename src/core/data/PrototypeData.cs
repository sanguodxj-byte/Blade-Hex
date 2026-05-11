using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 原型数据类 — 存储基础物品模板
/// </summary>
public static class PrototypeData
{
    private static readonly Dictionary<string, WeaponData> Weapons = new();
    private static readonly Dictionary<string, ArmorData> Armors = new();
    private static readonly Dictionary<string, ConsumableData> Consumables = new();

    static PrototypeData()
    {
        // 预定义一些基础模板
        Weapons["longsword"] = new WeaponData {
            ItemId = "longsword",
            ItemName = "长剑",
            DamageDiceCount = 1,
            DamageDiceSides = 8,
            WeaponDamageType = WeaponData.DamageType.Slash
        };
        
        Armors["leather"] = new ArmorData {
            ItemId = "leather",
            ItemName = "皮甲",
            armorType = ArmorData.ArmorType.Light,
            AcBonus = 2
        };
    }

    public static Dictionary<string, WeaponData> GetWeapons() => Weapons;
    public static Dictionary<string, ArmorData> GetArmors() => Armors;
    public static Dictionary<string, ConsumableData> GetConsumables() => Consumables;
}