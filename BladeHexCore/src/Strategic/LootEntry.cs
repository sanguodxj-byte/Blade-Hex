// LootEntry.cs
// 战利品条目 — 战斗结束后掉落的单个物品描述
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 战利品条目
/// </summary>
[GlobalClass]
public partial class LootEntry : Resource
{
    public enum LootType { Weapon, Armor, Shield, Helmet, Consumable, Gold, Material }

    [Export] public string ItemName { get; set; } = "";
    [Export] public LootType Type = LootType.Material;
    [Export] public int Quantity { get; set; } = 1;
    [Export] public int Value { get; set; } = 0; // 估价（金币）
    [Export] public string Description { get; set; } = "";

    // 如果是装备，持有实际数据引用的 ID
    [Export] public string ItemDataId { get; set; } = "";

    public LootEntry() { }

    public LootEntry(string name, LootType type, int qty = 1, int value = 0, string desc = "")
    {
        ItemName = name;
        Type = type;
        Quantity = qty;
        Value = value;
        Description = desc;
    }
}
