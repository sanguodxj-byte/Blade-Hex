using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 原型数据类 — 存储基础物品模板，供商店/掉落/装备生成使用
/// 武器从 WeaponRegistry 自动填充，护甲和消耗品手动定义
/// 数据优先从 items.json 加载，失败时回退到硬编码
/// </summary>
public static class PrototypeData
{
    private static readonly Dictionary<string, WeaponData> Weapons;
    private static readonly Dictionary<string, ArmorData> Armors;
    private static readonly Dictionary<string, ConsumableData> Consumables;

    static PrototypeData()
    {
        Weapons = ItemDataLoader.GetWeapons();
        Armors = ItemDataLoader.GetArmors();
        Consumables = ItemDataLoader.GetConsumables();
    }

    public static Dictionary<string, WeaponData> GetWeapons() => Weapons;
    public static Dictionary<string, ArmorData> GetArmors() => Armors;
    public static Dictionary<string, ConsumableData> GetConsumables() => Consumables;
}
