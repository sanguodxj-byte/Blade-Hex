using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 原型数据类 — 存储基础物品模板，供商店/掉落/装备生成使用
/// 完全从 JSON 加载，不再有硬编码兜底（由 ItemDataLoader 处理）
/// </summary>
public static class PrototypeData
{
    private static readonly Dictionary<string, WeaponData> Weapons;
    private static readonly Dictionary<string, ArmorData> Armors;
    private static readonly Dictionary<string, ConsumableData> Consumables;
    private static readonly Dictionary<string, ItemData> Quivers;

    static PrototypeData()
    {
        Weapons = ItemDataLoader.GetWeapons();
        Armors = ItemDataLoader.GetArmors();
        Consumables = ItemDataLoader.GetConsumables();
        Quivers = ItemDataLoader.GetQuivers();
    }

    public static Dictionary<string, WeaponData> GetWeapons() => Weapons;
    public static Dictionary<string, ArmorData> GetArmors() => Armors;
    public static Dictionary<string, ConsumableData> GetConsumables() => Consumables;
    public static Dictionary<string, ItemData> GetQuivers() => Quivers;
}
