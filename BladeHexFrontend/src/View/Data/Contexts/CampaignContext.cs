using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;

namespace BladeHex.Data.Contexts;

/// <summary>
/// 战役模式上下文 — 记录当前战役进度、队伍、背包、经济。
/// 复用大地图的 PartyRoster / PartyInventory / EconomyManager 机制。
/// 支持检查点自动保存（每关之间）。
/// </summary>
[GlobalClass]
public partial class CampaignContext : Resource
{
    private const string SavePath = "user://campaign_save.json";

    /// <summary>当前关卡索引（0-based）。</summary>
    [Export] public int CurrentLevel { get; set; }

    /// <summary>战役是否正在进行中。</summary>
    [Export] public bool IsActive { get; set; }

    /// <summary>队伍名册（完整复用大地图的 PartyRoster，含装备/HP/等级/技能盘）。</summary>
    public PartyRoster Roster { get; set; } = new();

    /// <summary>队伍背包（完整复用大地图的 PartyInventory）。</summary>
    public PartyInventory Inventory { get; set; } = new();

    /// <summary>金币（独立于大地图经济系统的战役金币）。</summary>
    [Export] public int Gold { get; set; } = CampaignPricingService.GetStartingGold();

    /// <summary>重置战役状态（开始新战役时调用）。</summary>
    public void Reset()
    {
        CurrentLevel = 0;
        Gold = CampaignPricingService.GetStartingGold();
        IsActive = true;
        Roster = new PartyRoster();
        Inventory = new PartyInventory();
    }

    // ========================================
    // 检查点保存/加载
    // ========================================

    /// <summary>保存当前战役状态到文件（每关之间的检查点）。</summary>
    public void SaveCheckpoint()
    {
        var data = new Godot.Collections.Dictionary
        {
            ["current_level"] = CurrentLevel,
            ["gold"] = Gold,
            ["is_active"] = IsActive,
            ["roster"] = Roster.Serialize(),
            ["inventory"] = Inventory.Serialize(),
            ["equipment"] = SerializeEquipment(),
        };

        var json = Json.Stringify(data);
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file != null)
        {
            file.StoreString(json);
            GD.Print($"[Campaign] 检查点已保存: 关卡{CurrentLevel + 1}, 金币{Gold}, 队伍{Roster.Count}人");
        }
        else
        {
            GD.PrintErr($"[Campaign] 保存失败: {FileAccess.GetOpenError()}");
        }
    }

    /// <summary>从文件加载战役存档。成功返回 true。</summary>
    public bool LoadCheckpoint()
    {
        if (!FileAccess.FileExists(SavePath))
            return false;

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null)
            return false;

        var json = file.GetAsText();
        var parsed = Json.ParseString(json);
        if (parsed.VariantType != Variant.Type.Dictionary)
            return false;

        var data = parsed.AsGodotDictionary();

        CurrentLevel = data.ContainsKey("current_level") ? data["current_level"].AsInt32() : 0;
        Gold = data.ContainsKey("gold") ? data["gold"].AsInt32() : CampaignPricingService.GetStartingGold();
        IsActive = data.ContainsKey("is_active") ? data["is_active"].AsBool() : true;

        if (data.ContainsKey("roster") && data["roster"].Obj is Godot.Collections.Dictionary rosterData)
            Roster = PartyRoster.Deserialize(rosterData);
        else
            Roster = new PartyRoster();

        if (data.ContainsKey("inventory") && data["inventory"].Obj is Godot.Collections.Dictionary invData)
            Inventory = PartyInventory.Deserialize(invData);
        else
            Inventory = new PartyInventory();

        // 恢复装备
        if (data.ContainsKey("equipment") && data["equipment"].Obj is Godot.Collections.Array equipArray)
            DeserializeEquipment(equipArray);

        GD.Print($"[Campaign] 存档已加载: 关卡{CurrentLevel + 1}, 金币{Gold}, 队伍{Roster.Count}人");
        return true;
    }

    // ========================================
    // 装备序列化（战役专用，补充 PartyRoster 不存装备的缺陷）
    // ========================================

    private Godot.Collections.Array SerializeEquipment()
    {
        var arr = new Godot.Collections.Array();
        foreach (var m in Roster.Members)
        {
            var equipDict = new Godot.Collections.Dictionary();
            if (m.PrimaryMainHand != null)
                equipDict["primary_main"] = SerializeWeapon(m.PrimaryMainHand);
            if (m.SecondaryMainHand != null)
                equipDict["secondary_main"] = SerializeWeapon(m.SecondaryMainHand);
            if (m.PrimaryOffHand != null)
                equipDict["primary_off"] = SerializeItem(m.PrimaryOffHand);
            if (m.Armor != null)
                equipDict["armor"] = SerializeArmor(m.Armor);
            if (m.Helmet != null)
                equipDict["helmet"] = SerializeArmor(m.Helmet);
            if (m.Boots != null)
                equipDict["boots"] = SerializeArmor(m.Boots);
            if (m.Gauntlets != null)
                equipDict["gauntlets"] = SerializeArmor(m.Gauntlets);
            if (m.Shield != null)
                equipDict["shield"] = SerializeArmor(m.Shield);
            arr.Add(equipDict);
        }
        return arr;
    }

    private void DeserializeEquipment(Godot.Collections.Array equipArray)
    {
        for (int i = 0; i < equipArray.Count && i < Roster.Members.Count; i++)
        {
            if (equipArray[i].Obj is not Godot.Collections.Dictionary equipDict) continue;
            var m = Roster.Members[i];

            if (equipDict.ContainsKey("primary_main") && equipDict["primary_main"].Obj is Godot.Collections.Dictionary pmDict)
                m.PrimaryMainHand = DeserializeWeapon(pmDict);
            if (equipDict.ContainsKey("secondary_main") && equipDict["secondary_main"].Obj is Godot.Collections.Dictionary smDict)
                m.SecondaryMainHand = DeserializeWeapon(smDict);
            if (equipDict.ContainsKey("armor") && equipDict["armor"].Obj is Godot.Collections.Dictionary arDict)
                m.Armor = DeserializeArmor(arDict);
            if (equipDict.ContainsKey("helmet") && equipDict["helmet"].Obj is Godot.Collections.Dictionary helDict)
                m.Helmet = DeserializeArmor(helDict);
            if (equipDict.ContainsKey("boots") && equipDict["boots"].Obj is Godot.Collections.Dictionary bootDict)
                m.Boots = DeserializeArmor(bootDict);
            if (equipDict.ContainsKey("gauntlets") && equipDict["gauntlets"].Obj is Godot.Collections.Dictionary gauntDict)
                m.Gauntlets = DeserializeArmor(gauntDict);
            if (equipDict.ContainsKey("shield") && equipDict["shield"].Obj is Godot.Collections.Dictionary shDict)
                m.Shield = DeserializeArmor(shDict);
        }
    }

    private static Godot.Collections.Dictionary SerializeWeapon(WeaponData w)
    {
        return new Godot.Collections.Dictionary
        {
            ["item_name"] = w.ItemName ?? "",
            ["damage_dice_count"] = w.DamageDiceCount,
            ["damage_dice_sides"] = w.DamageDiceSides,
            ["is_ranged"] = w.IsRanged,
            ["range_cells"] = w.RangeCells,
            ["is_finesse"] = w.IsFinesse,
            ["is_two_handed"] = w.IsTwoHanded,
            ["damage_type"] = (int)w.WeaponDamageType,
            ["price"] = w.Price,
            ["rarity"] = (int)w.ItemRarity,
        };
    }

    private static Godot.Collections.Dictionary SerializeArmor(ArmorData a)
    {
        return new Godot.Collections.Dictionary
        {
            ["item_name"] = a.ItemName ?? "",
            ["armor_type"] = (int)a.armorType,
            ["ac_bonus"] = a.AcBonus,
            ["max_dex_bonus"] = a.MaxDexBonus,
            ["price"] = a.Price,
            ["rarity"] = (int)a.ItemRarity,
            ["equip_slot"] = (int)a.EquipSlotTarget,
        };
    }

    private static Godot.Collections.Dictionary SerializeItem(ItemData item)
    {
        return new Godot.Collections.Dictionary
        {
            ["item_name"] = item.ItemName ?? "",
            ["price"] = item.Price,
            ["rarity"] = (int)item.ItemRarity,
        };
    }

    private static WeaponData DeserializeWeapon(Godot.Collections.Dictionary d)
    {
        var w = new WeaponData();
        w.ItemName = d.ContainsKey("item_name") ? d["item_name"].AsString() : "";
        w.DamageDiceCount = d.ContainsKey("damage_dice_count") ? d["damage_dice_count"].AsInt32() : 1;
        w.DamageDiceSides = d.ContainsKey("damage_dice_sides") ? d["damage_dice_sides"].AsInt32() : 6;
        w.IsRanged = d.ContainsKey("is_ranged") && d["is_ranged"].AsBool();
        w.RangeCells = d.ContainsKey("range_cells") ? d["range_cells"].AsInt32() : 0;
        w.IsFinesse = d.ContainsKey("is_finesse") && d["is_finesse"].AsBool();
        w.IsTwoHanded = d.ContainsKey("is_two_handed") && d["is_two_handed"].AsBool();
        w.WeaponDamageType = d.ContainsKey("damage_type") ? (WeaponData.DamageType)d["damage_type"].AsInt32() : WeaponData.DamageType.Slash;
        w.Price = d.ContainsKey("price") ? d["price"].AsInt32() : 10;
        w.ItemRarity = d.ContainsKey("rarity") ? (ItemData.Rarity)d["rarity"].AsInt32() : ItemData.Rarity.Common;
        return w;
    }

    private static ArmorData DeserializeArmor(Godot.Collections.Dictionary d)
    {
        var a = new ArmorData();
        a.ItemName = d.ContainsKey("item_name") ? d["item_name"].AsString() : "";
        a.armorType = d.ContainsKey("armor_type") ? (ArmorData.ArmorType)d["armor_type"].AsInt32() : ArmorData.ArmorType.Light;
        a.AcBonus = d.ContainsKey("ac_bonus") ? d["ac_bonus"].AsInt32() : 0;
        a.MaxDexBonus = d.ContainsKey("max_dex_bonus") ? d["max_dex_bonus"].AsInt32() : 99;
        a.Price = d.ContainsKey("price") ? d["price"].AsInt32() : 10;
        a.ItemRarity = d.ContainsKey("rarity") ? (ItemData.Rarity)d["rarity"].AsInt32() : ItemData.Rarity.Common;
        a.EquipSlotTarget = d.ContainsKey("equip_slot") ? (ItemData.EquipSlot)d["equip_slot"].AsInt32() : ItemData.EquipSlot.Body;
        return a;
    }

    /// <summary>删除战役存档。</summary>
    public static void DeleteCheckpoint()
    {
        if (FileAccess.FileExists(SavePath))
        {
            DirAccess.RemoveAbsolute(SavePath);
            GD.Print("[Campaign] 存档已删除");
        }
    }

    /// <summary>是否存在战役存档。</summary>
    public static bool HasCheckpoint() => FileAccess.FileExists(SavePath);
}
