// ItemDataLoader.cs
// 从 items.json 加载武器、护甲、消耗品原型数据
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 物品数据加载器 — 从 res://BladeHexCore/src/Data/items/items.json 加载
/// 失败时回退到硬编码数据
/// </summary>
public static class ItemDataLoader
{
    private static bool _loaded = false;
    private static readonly Dictionary<string, WeaponData> _weapons = new();
    private static readonly Dictionary<string, ArmorData> _armors = new();
    private static readonly Dictionary<string, ConsumableData> _consumables = new();

    public static Dictionary<string, WeaponData> GetWeapons()
    {
        EnsureLoaded();
        return _weapons;
    }

    public static Dictionary<string, ArmorData> GetArmors()
    {
        EnsureLoaded();
        return _armors;
    }

    public static Dictionary<string, ConsumableData> GetConsumables()
    {
        EnsureLoaded();
        return _consumables;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadFromJson();
    }

    private static void LoadFromJson()
    {
        string path = "res://BladeHexCore/src/Data/items/items.json";
        if (!FileAccess.FileExists(path)) { LoadFallback(); return; }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) { LoadFallback(); return; }
        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PrintErr($"[ItemDataLoader] JSON parse error: {json.GetErrorMessage()}");
            LoadFallback();
            return;
        }
        var data = json.Data.AsGodotDictionary();
        ParseWeapons(data);
        ParseArmors(data);
        ParseConsumables(data);
        GD.Print($"[ItemDataLoader] Loaded {_weapons.Count} weapons, {_armors.Count} armors, {_consumables.Count} consumables from JSON");
    }

    // ========================================
    // 武器解析
    // ========================================
    private static void ParseWeapons(Godot.Collections.Dictionary root)
    {
        if (!root.ContainsKey("weapons")) return;
        var arr = root["weapons"].AsGodotArray();
        foreach (var item in arr)
        {
            var dict = item.AsGodotDictionary();
            string id = dict["id"].AsString();
            string subtypeStr = dict.ContainsKey("subtype") ? dict["subtype"].AsString() : "";
            int price = dict.ContainsKey("price") ? dict["price"].AsInt32() : 10;
            bool finesse = dict.ContainsKey("finesse") && dict["finesse"].AsBool();
            bool twoHanded = dict.ContainsKey("two_handed") && dict["two_handed"].AsBool();
            bool reach = dict.ContainsKey("reach") && dict["reach"].AsBool();
            bool dualWield = dict.ContainsKey("dual_wield") && dict["dual_wield"].AsBool();
            bool ranged = dict.ContainsKey("ranged") && dict["ranged"].AsBool();
            bool throwing = dict.ContainsKey("throwing") && dict["throwing"].AsBool();

            if (!Enum.TryParse<WeaponData.WeaponSubtype>(subtypeStr, out var subtype))
            {
                GD.PrintErr($"[ItemDataLoader] Unknown weapon subtype: {subtypeStr}");
                continue;
            }

            var cfg = WeaponRegistry.GetConfig(subtype);
            var w = new WeaponData
            {
                ItemId = id,
                ItemName = cfg.Name,
                Subtype = subtype,
                DamageDiceCount = cfg.DiceCount,
                DamageDiceSides = cfg.DiceSides,
                ApCost = cfg.BaseApCost,
                RangeCells = cfg.Range,
                Price = price,
                IsFinesse = finesse,
                IsTwoHanded = twoHanded,
                IsReach = reach,
                IsDualWieldable = dualWield,
                IsRanged = ranged,
                IsThrowing = throwing,
                WeaponDamageType = cfg.DamageType,
            };
            _weapons[id] = w;
        }
    }

    // ========================================
    // 护甲解析
    // ========================================
    private static void ParseArmors(Godot.Collections.Dictionary root)
    {
        if (!root.ContainsKey("armors")) return;
        var arr = root["armors"].AsGodotArray();
        foreach (var item in arr)
        {
            var dict = item.AsGodotDictionary();
            string id = dict["id"].AsString();
            string name = dict.ContainsKey("name") ? dict["name"].AsString() : id;
            string typeStr = dict.ContainsKey("type") ? dict["type"].AsString() : "Light";
            int dr = dict.ContainsKey("dr") ? dict["dr"].AsInt32() : 0;
            int price = dict.ContainsKey("price") ? dict["price"].AsInt32() : 10;
            int maxDex = dict.ContainsKey("max_dex") ? dict["max_dex"].AsInt32() : 99;
            int apPenalty = dict.ContainsKey("ap_penalty") ? dict["ap_penalty"].AsInt32() : 0;
            int strReq = dict.ContainsKey("str_req") ? dict["str_req"].AsInt32() : 0;
            bool stealthDisadv = dict.ContainsKey("stealth_disadvantage") && dict["stealth_disadvantage"].AsBool();
            bool destroyable = dict.ContainsKey("destroyable") && dict["destroyable"].AsBool();
            string slotStr = dict.ContainsKey("slot") ? dict["slot"].AsString() : "";

            // AC is now computed from DR: floor(sqrt(DR))
            int ac = (int)Math.Floor(Math.Sqrt(dr));

            var armorType = typeStr switch
            {
                "Cloth" => ArmorData.ArmorType.Light,
                "Light" => ArmorData.ArmorType.Light,
                "Medium" => ArmorData.ArmorType.Medium,
                "Heavy" => ArmorData.ArmorType.Heavy,
                "Shield" => ArmorData.ArmorType.Shield,
                _ => ArmorData.ArmorType.Light,
            };

            var equipSlot = slotStr switch
            {
                "Helmet" => ItemData.EquipSlot.Helmet,
                _ => ItemData.EquipSlot.Body,
            };

            var a = new ArmorData
            {
                ItemId = id,
                ItemName = name,
                armorType = armorType,
                AcBonus = ac,
                DrThreshold = dr,
                MaxDexBonus = maxDex,
                ApPenalty = apPenalty,
                MovementPenalty = apPenalty, // keep MovementPenalty in sync
                StrRequired = strReq,
                StealthDisadvantage = stealthDisadv,
                IsDestroyable = destroyable,
                Price = price,
                EquipSlotTarget = equipSlot,
            };
            _armors[id] = a;
        }
    }

    // ========================================
    // 消耗品解析
    // ========================================
    private static void ParseConsumables(Godot.Collections.Dictionary root)
    {
        if (!root.ContainsKey("consumables")) return;
        var arr = root["consumables"].AsGodotArray();
        foreach (var item in arr)
        {
            var dict = item.AsGodotDictionary();
            string id = dict["id"].AsString();
            string name = dict.ContainsKey("name") ? dict["name"].AsString() : id;
            string desc = dict.ContainsKey("desc") ? dict["desc"].AsString() : "";
            int price = dict.ContainsKey("price") ? dict["price"].AsInt32() : 10;

            var c = new ConsumableData
            {
                ItemId = id,
                ItemName = name,
                Description = desc,
                Price = price,
            };
            _consumables[id] = c;
        }
    }

    // ========================================
    // 回退：硬编码数据
    // ========================================
    private static void LoadFallback()
    {
        GD.PrintErr("[ItemDataLoader] Failed to load items.json, using fallback data");
        LoadFallbackWeapons();
        LoadFallbackArmors();
        LoadFallbackConsumables();
    }

    private static void LoadFallbackWeapons()
    {
        // Slash - Light
        AddFallbackWeapon("dagger", WeaponData.WeaponSubtype.Dagger, 15, true, false, false, true);
        AddFallbackWeapon("seax", WeaponData.WeaponSubtype.Seax, 25, false, false, false, true);
        AddFallbackWeapon("kukri", WeaponData.WeaponSubtype.Kukri, 30, true, false, false, true);
        // Slash - Medium
        AddFallbackWeapon("arming_sword", WeaponData.WeaponSubtype.ArmingSword, 80, false, false, false, false);
        AddFallbackWeapon("battle_axe", WeaponData.WeaponSubtype.BattleAxe, 90, false, false, false, false);
        AddFallbackWeapon("nomad_saber", WeaponData.WeaponSubtype.NomadSaber, 75, true, false, false, false);
        // Slash - Heavy
        AddFallbackWeapon("greatsword", WeaponData.WeaponSubtype.Greatsword, 180, false, true, false, false);
        AddFallbackWeapon("great_axe", WeaponData.WeaponSubtype.GreatAxe, 170, false, true, false, false);
        AddFallbackWeapon("glaive", WeaponData.WeaponSubtype.Glaive, 160, false, true, true, false);
        // Pierce - Light
        AddFallbackWeapon("stiletto", WeaponData.WeaponSubtype.Stiletto, 20, true, false, false, true);
        AddFallbackWeapon("spiked_dagger", WeaponData.WeaponSubtype.SpikedDagger, 35, true, false, false, true);
        AddFallbackWeapon("rapier", WeaponData.WeaponSubtype.Rapier, 100, true, false, false, false);
        // Pierce - Medium
        AddFallbackWeapon("infantry_spear", WeaponData.WeaponSubtype.InfantrySpear, 60, false, false, true, false);
        AddFallbackWeapon("broad_spear", WeaponData.WeaponSubtype.BroadSpear, 85, false, false, false, false);
        AddFallbackWeapon("awlpike", WeaponData.WeaponSubtype.Awlpike, 95, false, false, true, false);
        // Pierce - Heavy
        AddFallbackWeapon("lance", WeaponData.WeaponSubtype.Lance, 200, false, true, true, false);
        AddFallbackWeapon("voulge", WeaponData.WeaponSubtype.Voulge, 175, false, true, true, false);
        AddFallbackWeapon("trident", WeaponData.WeaponSubtype.Trident, 165, false, true, true, false);
        // Crush - Light
        AddFallbackWeapon("club", WeaponData.WeaponSubtype.Club, 5, false, false, false, false);
        AddFallbackWeapon("light_hammer", WeaponData.WeaponSubtype.LightHammer, 40, false, false, false, false);
        AddFallbackWeapon("cestus", WeaponData.WeaponSubtype.Cestus, 20, false, false, false, true);
        // Crush - Medium
        AddFallbackWeapon("winged_mace", WeaponData.WeaponSubtype.WingedMace, 70, false, false, false, false);
        AddFallbackWeapon("military_hammer", WeaponData.WeaponSubtype.MilitaryHammer, 110, false, false, false, false);
        AddFallbackWeapon("flail", WeaponData.WeaponSubtype.Flail, 85, false, false, false, false);
        // Crush - Heavy
        AddFallbackWeapon("maul", WeaponData.WeaponSubtype.Maul, 150, false, true, false, false);
        AddFallbackWeapon("greatclub", WeaponData.WeaponSubtype.Greatclub, 120, false, true, false, false);
        AddFallbackWeapon("polehammer", WeaponData.WeaponSubtype.Polehammer, 190, false, true, true, false);
        // Thrown
        AddFallbackWeapon("throwing_knife", WeaponData.WeaponSubtype.ThrowingKnife, 10, false, false, false, true, false, true);
        AddFallbackWeapon("dart", WeaponData.WeaponSubtype.Dart, 8, false, false, false, false, false, true);
        AddFallbackWeapon("francisca", WeaponData.WeaponSubtype.Francisca, 25, false, false, false, false, false, true);
        AddFallbackWeapon("javelin", WeaponData.WeaponSubtype.Javelin, 15, false, false, false, false, false, true);
        AddFallbackWeapon("pilum", WeaponData.WeaponSubtype.Pilum, 30, false, false, false, false, false, true);
        AddFallbackWeapon("harpoon", WeaponData.WeaponSubtype.Harpoon, 25, false, false, false, false, false, true);
        AddFallbackWeapon("stone_throw", WeaponData.WeaponSubtype.StoneThrow, 2, false, false, false, false, false, true);
        AddFallbackWeapon("heavy_javelin", WeaponData.WeaponSubtype.HeavyJavelin, 40, false, false, false, false, false, true);
        AddFallbackWeapon("throwing_hammer", WeaponData.WeaponSubtype.ThrowingHammer, 35, false, false, false, false, false, true);
        // Bows
        AddFallbackWeapon("shortbow", WeaponData.WeaponSubtype.Shortbow, 50, false, false, false, false, true);
        AddFallbackWeapon("hunting_bow", WeaponData.WeaponSubtype.HuntingBow, 80, false, false, false, false, true);
        AddFallbackWeapon("nomad_bow", WeaponData.WeaponSubtype.NomadBow, 65, false, false, false, false, true);
        AddFallbackWeapon("strongbow", WeaponData.WeaponSubtype.Strongbow, 100, false, false, false, false, true);
        AddFallbackWeapon("recurve_bow", WeaponData.WeaponSubtype.RecurveBow, 180, false, false, false, false, true);
        AddFallbackWeapon("war_bow", WeaponData.WeaponSubtype.WarBow, 130, false, false, false, false, true);
        AddFallbackWeapon("longbow", WeaponData.WeaponSubtype.Longbow, 150, false, true, false, false, true);
        AddFallbackWeapon("composite_longbow", WeaponData.WeaponSubtype.CompositeLongbow, 220, false, true, false, false, true);
        AddFallbackWeapon("greatbow", WeaponData.WeaponSubtype.Greatbow, 200, false, true, false, false, true);
        // Crossbows
        AddFallbackWeapon("pistol_crossbow", WeaponData.WeaponSubtype.PistolCrossbow, 75, false, false, false, false, true);
        AddFallbackWeapon("light_crossbow", WeaponData.WeaponSubtype.LightCrossbow, 100, false, false, false, false, true);
        AddFallbackWeapon("hunting_crossbow", WeaponData.WeaponSubtype.HuntingCrossbow, 120, false, false, false, false, true);
        AddFallbackWeapon("standard_crossbow", WeaponData.WeaponSubtype.StandardCrossbow, 140, false, false, false, false, true);
        AddFallbackWeapon("strong_crossbow", WeaponData.WeaponSubtype.StrongCrossbow, 180, false, false, false, false, true);
        AddFallbackWeapon("sniper_crossbow", WeaponData.WeaponSubtype.SniperCrossbow, 200, false, false, false, false, true);
        AddFallbackWeapon("heavy_crossbow", WeaponData.WeaponSubtype.HeavyCrossbow, 220, false, true, false, false, true);
        AddFallbackWeapon("siege_crossbow", WeaponData.WeaponSubtype.SiegeCrossbow, 350, false, true, false, false, true);
        AddFallbackWeapon("ballista", WeaponData.WeaponSubtype.Ballista, 500, false, true, false, false, true);
    }

    private static void AddFallbackWeapon(string id, WeaponData.WeaponSubtype subtype, int price,
        bool finesse, bool twoHanded, bool reach, bool dualWield,
        bool ranged = false, bool throwing = false)
    {
        var cfg = WeaponRegistry.GetConfig(subtype);
        var w = new WeaponData
        {
            ItemId = id,
            ItemName = cfg.Name,
            Subtype = subtype,
            DamageDiceCount = cfg.DiceCount,
            DamageDiceSides = cfg.DiceSides,
            ApCost = cfg.BaseApCost,
            RangeCells = cfg.Range,
            Price = price,
            IsFinesse = finesse,
            IsTwoHanded = twoHanded,
            IsReach = reach,
            IsDualWieldable = dualWield,
            IsRanged = ranged,
            IsThrowing = throwing,
            WeaponDamageType = cfg.DamageType,
        };
        _weapons[id] = w;
    }

    private static void LoadFallbackArmors()
    {
        // Body armors — AC = floor(sqrt(DR))
        _armors["cloth"] = new ArmorData { ItemId = "cloth", ItemName = "布衣", armorType = ArmorData.ArmorType.Light, AcBonus = 0, DrThreshold = 0, ApPenalty = 0, Price = 5 };
        _armors["mage_robe"] = new ArmorData { ItemId = "mage_robe", ItemName = "法师长袍", armorType = ArmorData.ArmorType.Light, AcBonus = 1, DrThreshold = 3, ApPenalty = 0, Price = 50 };
        _armors["leather"] = new ArmorData { ItemId = "leather", ItemName = "皮甲", armorType = ArmorData.ArmorType.Light, AcBonus = 2, DrThreshold = 6, ApPenalty = 0, Price = 45 };
        _armors["studded_leather"] = new ArmorData { ItemId = "studded_leather", ItemName = "镶钉皮甲", armorType = ArmorData.ArmorType.Medium, AcBonus = 2, DrThreshold = 8, MaxDexBonus = 4, ApPenalty = 1, Price = 80 };
        _armors["chain_mail"] = new ArmorData { ItemId = "chain_mail", ItemName = "链甲", armorType = ArmorData.ArmorType.Medium, AcBonus = 3, DrThreshold = 11, MaxDexBonus = 3, ApPenalty = 2, Price = 150 };
        _armors["half_plate"] = new ArmorData { ItemId = "half_plate", ItemName = "半板甲", armorType = ArmorData.ArmorType.Heavy, AcBonus = 3, DrThreshold = 15, MaxDexBonus = 1, ApPenalty = 4, Price = 300 };
        _armors["full_plate"] = new ArmorData { ItemId = "full_plate", ItemName = "全板甲", armorType = ArmorData.ArmorType.Heavy, AcBonus = 4, DrThreshold = 18, MaxDexBonus = 0, ApPenalty = 5, Price = 600 };
        // Shields
        _armors["light_wooden_shield"] = new ArmorData { ItemId = "light_wooden_shield", ItemName = "轻木盾", armorType = ArmorData.ArmorType.Shield, AcBonus = 1, DrThreshold = 3, ApPenalty = 0, Price = 25 };
        _armors["infantry_round_shield"] = new ArmorData { ItemId = "infantry_round_shield", ItemName = "步兵圆盾", armorType = ArmorData.ArmorType.Shield, AcBonus = 2, DrThreshold = 4, ApPenalty = 0, Price = 50 };
        _armors["infantry_heavy_shield"] = new ArmorData { ItemId = "infantry_heavy_shield", ItemName = "步兵重盾", armorType = ArmorData.ArmorType.Shield, AcBonus = 2, DrThreshold = 5, ApPenalty = 1, Price = 80 };
        _armors["knight_shield"] = new ArmorData { ItemId = "knight_shield", ItemName = "骑士盾", armorType = ArmorData.ArmorType.Shield, AcBonus = 2, DrThreshold = 6, ApPenalty = 1, Price = 120 };
        _armors["legion_tower_shield"] = new ArmorData { ItemId = "legion_tower_shield", ItemName = "军团塔盾", armorType = ArmorData.ArmorType.Shield, AcBonus = 2, DrThreshold = 8, ApPenalty = 2, Price = 200 };
        // Helmets
        _armors["leather_cap"] = new ArmorData { ItemId = "leather_cap", ItemName = "皮帽", armorType = ArmorData.ArmorType.Light, AcBonus = 1, DrThreshold = 2, ApPenalty = 0, Price = 15, EquipSlotTarget = ItemData.EquipSlot.Helmet };
        _armors["iron_helm"] = new ArmorData { ItemId = "iron_helm", ItemName = "铁盔", armorType = ArmorData.ArmorType.Medium, AcBonus = 2, DrThreshold = 4, ApPenalty = 0, Price = 60, EquipSlotTarget = ItemData.EquipSlot.Helmet };
        _armors["great_helm"] = new ArmorData { ItemId = "great_helm", ItemName = "大头盔", armorType = ArmorData.ArmorType.Heavy, AcBonus = 2, DrThreshold = 6, ApPenalty = 1, Price = 120, EquipSlotTarget = ItemData.EquipSlot.Helmet };
        _armors["knight_helm"] = new ArmorData { ItemId = "knight_helm", ItemName = "骑士全盔", armorType = ArmorData.ArmorType.Heavy, AcBonus = 2, DrThreshold = 8, ApPenalty = 1, Price = 200, EquipSlotTarget = ItemData.EquipSlot.Helmet };
    }

    private static void LoadFallbackConsumables()
    {
        _consumables["health_potion"] = new ConsumableData { ItemId = "health_potion", ItemName = "治疗药水", Description = "恢复2d4+2 HP", Price = 25 };
        _consumables["mana_potion"] = new ConsumableData { ItemId = "mana_potion", ItemName = "魔力药水", Description = "恢复2d4 MP", Price = 30 };
        _consumables["antidote"] = new ConsumableData { ItemId = "antidote", ItemName = "解毒剂", Description = "解除中毒状态", Price = 20 };
        _consumables["bandage"] = new ConsumableData { ItemId = "bandage", ItemName = "绷带", Description = "恢复1d6 HP", Price = 10 };
        _consumables["rations"] = new ConsumableData { ItemId = "rations", ItemName = "干粮", Description = "恢复5食物", Price = 8 };
        _consumables["whetstone"] = new ConsumableData { ItemId = "whetstone", ItemName = "磨刀石", Description = "下次战斗武器伤害+1", Price = 40 };
        _consumables["fire_oil"] = new ConsumableData { ItemId = "fire_oil", ItemName = "火油", Description = "投掷造成1d6火焰伤害", Price = 35 };
        _consumables["holy_water"] = new ConsumableData { ItemId = "holy_water", ItemName = "净化药水", Description = "对亡灵造成2d6奥术伤害", Price = 50 };
        _consumables["elixir"] = new ConsumableData { ItemId = "elixir", ItemName = "万灵药", Description = "恢复全部HP和MP", Price = 200 };
        _consumables["camp_kit"] = new ConsumableData { ItemId = "camp_kit", ItemName = "野营工具", Description = "野外休息恢复效果+50%", Price = 60 };
    }
}
