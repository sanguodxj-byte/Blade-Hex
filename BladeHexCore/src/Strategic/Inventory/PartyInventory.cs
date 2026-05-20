// PartyInventory.cs
// 队伍背包 — 持有所有战利品、装备、消耗品
//
// 设计：
// - 按 slot 存储（每个 slot = 一种物品 × 数量）
// - 容量受队伍人数影响（每人 +10 格）
// - 提供 Add/Remove/Equip/Sell 接口
// - 序列化/反序列化
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 背包格子
/// </summary>
[GlobalClass]
public partial class InventorySlot : Resource
{
    [Export] public string ItemName { get; set; } = "";
    [Export] public int Quantity { get; set; } = 1;
    [Export] public int Value { get; set; } = 0; // 单价
    [Export] public string Description { get; set; } = "";
    [Export] public LootEntry.LootType ItemType = LootEntry.LootType.Material;

    // 装备数据引用（如果是装备类）
    public ArmorData? ArmorRef;
    public WeaponData? WeaponRef;

    public int TotalValue => Value * Quantity;

    public InventorySlot() { }

    public InventorySlot(LootEntry entry)
    {
        ItemName = entry.ItemName;
        Quantity = entry.Quantity;
        Value = entry.Value;
        Description = entry.Description;
        ItemType = entry.Type;
    }
}

/// <summary>
/// 队伍背包
/// </summary>
[GlobalClass]
public partial class PartyInventory : Resource
{
    /// <summary>背包格子列表</summary>
    public List<InventorySlot> Slots { get; set; } = new();

    /// <summary>基础容量（每个队员 +10）</summary>
    public int BaseCapacity = 30;

    /// <summary>当前容量（受队伍人数影响）</summary>
    public int Capacity(int partySize) => BaseCapacity + partySize * 10;

    /// <summary>当前已用格数</summary>
    public int UsedSlots => Slots.Count;

    /// <summary>总物品数</summary>
    public int TotalItems => Slots.Sum(s => s.Quantity);

    /// <summary>总估价</summary>
    public int TotalValue => Slots.Sum(s => s.TotalValue);

    // ========================================
    // 增删
    // ========================================

    /// <summary>添加战利品到背包（自动堆叠同名物品）</summary>
    public bool Add(LootEntry entry, int partySize = 6)
    {
        // 尝试堆叠
        var existing = Slots.FirstOrDefault(s => s.ItemName == entry.ItemName && s.ItemType == entry.Type);
        if (existing != null)
        {
            existing.Quantity += entry.Quantity;
            return true;
        }

        // 新格子
        if (UsedSlots >= Capacity(partySize))
        {
            GD.Print($"[Inventory] 背包已满，无法添加 {entry.ItemName}");
            return false;
        }

        Slots.Add(new InventorySlot(entry));
        return true;
    }

    /// <summary>添加多个战利品</summary>
    public int AddAll(List<LootEntry> entries, int partySize = 6)
    {
        int added = 0;
        foreach (var e in entries)
            if (Add(e, partySize)) added++;
        return added;
    }

    /// <summary>移除物品（按名字 + 数量）</summary>
    public bool Remove(string itemName, int quantity = 1)
    {
        var slot = Slots.FirstOrDefault(s => s.ItemName == itemName);
        if (slot == null || slot.Quantity < quantity) return false;

        slot.Quantity -= quantity;
        if (slot.Quantity <= 0)
            Slots.Remove(slot);
        return true;
    }

    /// <summary>按索引移除整个格子</summary>
    public InventorySlot? RemoveAt(int index)
    {
        if (index < 0 || index >= Slots.Count) return null;
        var slot = Slots[index];
        Slots.RemoveAt(index);
        return slot;
    }

    /// <summary>查找物品</summary>
    public InventorySlot? Find(string itemName)
    {
        return Slots.FirstOrDefault(s => s.ItemName == itemName);
    }

    /// <summary>是否有某物品</summary>
    public bool Has(string itemName, int quantity = 1)
    {
        var slot = Find(itemName);
        return slot != null && slot.Quantity >= quantity;
    }

    // ========================================
    // 装备操作
    // ========================================

    /// <summary>
    /// 把背包中的装备穿到队员身上
    /// 返回被替换下来的旧装备（如果有）
    /// </summary>
    public InventorySlot? EquipArmor(int slotIndex, UnitData target)
    {
        if (slotIndex < 0 || slotIndex >= Slots.Count) return null;
        var slot = Slots[slotIndex];
        if (slot.ItemType != LootEntry.LootType.Armor) return null;

        InventorySlot? replaced = null;

        // 卸下旧装备放回背包
        if (target.Armor != null)
        {
            replaced = new InventorySlot
            {
                ItemName = target.Armor.ItemName,
                Quantity = 1,
                Value = 10 + target.Armor.AcBonus * 15,
                ItemType = LootEntry.LootType.Armor,
                Description = $"闪避+{target.Armor.AcBonus}",
                ArmorRef = target.Armor,
            };
        }

        // 穿新装备
        if (slot.ArmorRef != null)
            target.Armor = slot.ArmorRef;

        // 从背包移除
        Slots.RemoveAt(slotIndex);

        // 旧装备放回
        if (replaced != null)
            Slots.Add(replaced);

        return replaced;
    }

    /// <summary>把背包中的武器装备到队员主手</summary>
    public InventorySlot? EquipWeapon(int slotIndex, UnitData target)
    {
        if (slotIndex < 0 || slotIndex >= Slots.Count) return null;
        var slot = Slots[slotIndex];
        if (slot.ItemType != LootEntry.LootType.Weapon) return null;

        InventorySlot? replaced = null;

        if (target.PrimaryMainHand != null)
        {
            replaced = new InventorySlot
            {
                ItemName = target.PrimaryMainHand.ItemName,
                Quantity = 1,
                Value = 5 + target.PrimaryMainHand.DamageDiceCount * target.PrimaryMainHand.DamageDiceSides * 3,
                ItemType = LootEntry.LootType.Weapon,
                Description = $"{target.PrimaryMainHand.DamageDiceCount}-{target.PrimaryMainHand.DamageDiceCount * target.PrimaryMainHand.DamageDiceSides}",
                WeaponRef = target.PrimaryMainHand,
            };
        }

        if (slot.WeaponRef != null)
            target.PrimaryMainHand = slot.WeaponRef;

        Slots.RemoveAt(slotIndex);
        if (replaced != null) Slots.Add(replaced);

        return replaced;
    }

    // ========================================
    // 商店
    // ========================================

    /// <summary>卖出物品（按索引），返回获得金币</summary>
    public int Sell(int slotIndex, int quantity = 1)
    {
        if (slotIndex < 0 || slotIndex >= Slots.Count) return 0;
        var slot = Slots[slotIndex];
        int sellQty = Math.Min(quantity, slot.Quantity);
        int gold = (int)(slot.Value * sellQty * 0.5f); // 卖价 = 估价 × 50%

        slot.Quantity -= sellQty;
        if (slot.Quantity <= 0) Slots.RemoveAt(slotIndex);

        return gold;
    }

    // ========================================
    // 兼容
    // ========================================

    /// <summary>获取 Godot Array 格式的背包内容（用）</summary>
    public Godot.Collections.Array GetSlotsGd()
    {
        var arr = new Godot.Collections.Array();
        foreach (var s in Slots) arr.Add(s);
        return arr;
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var slotsArr = new Godot.Collections.Array();
        foreach (var s in Slots)
        {
            slotsArr.Add(new Godot.Collections.Dictionary
            {
                ["name"] = s.ItemName,
                ["qty"] = s.Quantity,
                ["value"] = s.Value,
                ["desc"] = s.Description,
                ["type"] = (int)s.ItemType,
            });
        }
        return new Godot.Collections.Dictionary
        {
            ["slots"] = slotsArr,
            ["base_capacity"] = BaseCapacity,
        };
    }

    public static PartyInventory Deserialize(Godot.Collections.Dictionary data)
    {
        var inv = new PartyInventory();
        inv.BaseCapacity = data.ContainsKey("base_capacity") ? data["base_capacity"].AsInt32() : 30;

        if (data.ContainsKey("slots") && data["slots"].Obj is Godot.Collections.Array slotsArr)
        {
            foreach (var slotVar in slotsArr)
            {
                if (slotVar.Obj is not Godot.Collections.Dictionary sd) continue;
                inv.Slots.Add(new InventorySlot
                {
                    ItemName = sd.ContainsKey("name") ? sd["name"].AsString() : "",
                    Quantity = sd.ContainsKey("qty") ? sd["qty"].AsInt32() : 1,
                    Value = sd.ContainsKey("value") ? sd["value"].AsInt32() : 0,
                    Description = sd.ContainsKey("desc") ? sd["desc"].AsString() : "",
                    ItemType = sd.ContainsKey("type") ? (LootEntry.LootType)sd["type"].AsInt32() : LootEntry.LootType.Material,
                });
            }
        }

        return inv;
    }
}
