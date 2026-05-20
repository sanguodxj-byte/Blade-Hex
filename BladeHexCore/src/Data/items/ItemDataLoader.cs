// ItemDataLoader.cs
// 物品数据加载器 — 100% JSON 驱动
//
// 数据源（res://BladeHexCore/src/Data/items/）：
//   weapons_melee_slash.json   武器（斩击系）
//   weapons_melee_pierce.json  武器（穿刺系）
//   weapons_melee_crush.json   武器（钝击系）
//   weapons_ranged_bow.json    武器（弓系）
//   weapons_ranged_crossbow.json 武器（弩系）
//   weapons_ranged_thrown.json   武器（投掷系）
//   armors.json                 护甲、盾、头盔
//   consumables.json            消耗品
//   quivers.json                箭筒
//
// 设计原则：
//   - 加载失败 = 硬错误（GD.PushError），不再回退到硬编码
//   - 子类型枚举（WeaponSubtype）的基础数值由 WeaponRegistry 提供
//   - JSON 只覆盖差异（id/name/price/flag/tier）
//   - 纹理ID 与网格尺寸由 PostProcessItems 自动推断（约定 > 配置）
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 物品数据加载器 — 全 JSON 驱动，无硬编码兜底。
/// JSON 加载失败将记录 PushError 但不会崩溃（Items 集合为空）。
/// </summary>
public static class ItemDataLoader
{
    private const string BasePath = "res://BladeHexCore/src/Data/items/";

    private static readonly string[] WeaponFiles =
    {
        "weapons_melee_slash.json",
        "weapons_melee_pierce.json",
        "weapons_melee_crush.json",
        "weapons_ranged_bow.json",
        "weapons_ranged_crossbow.json",
        "weapons_ranged_thrown.json",
        "weapons_catalyst.json",
    };

    private static bool _loaded = false;
    private static readonly Dictionary<string, WeaponData> _weapons = new();
    private static readonly Dictionary<string, ArmorData> _armors = new();
    private static readonly Dictionary<string, ConsumableData> _consumables = new();
    private static readonly Dictionary<string, ItemData> _quivers = new();
    private static readonly Dictionary<string, AccessoryData> _accessories = new();

    public static Dictionary<string, WeaponData> GetWeapons() { EnsureLoaded(); return _weapons; }
    public static Dictionary<string, ArmorData> GetArmors() { EnsureLoaded(); return _armors; }
    public static Dictionary<string, ConsumableData> GetConsumables() { EnsureLoaded(); return _consumables; }
    public static Dictionary<string, ItemData> GetQuivers() { EnsureLoaded(); return _quivers; }
    public static Dictionary<string, AccessoryData> GetAccessories() { EnsureLoaded(); return _accessories; }

    /// <summary>强制重新加载（开发热重载用）</summary>
    public static void Reload()
    {
        _weapons.Clear();
        _armors.Clear();
        _consumables.Clear();
        _quivers.Clear();
        _accessories.Clear();
        _loaded = false;
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadAll();
    }

    private static void LoadAll()
    {
        int totalLoaded = 0;
        int totalFailed = 0;

        // 武器：分文件加载
        foreach (var wf in WeaponFiles)
        {
            var (loaded, failed) = LoadJsonArray(BasePath + wf, ParseWeaponEntry);
            totalLoaded += loaded;
            totalFailed += failed;
        }

        // 护甲、消耗品、箭筒
        var armorRes = LoadJsonArray(BasePath + "armors.json", ParseArmorEntry);
        var consumableRes = LoadJsonArray(BasePath + "consumables.json", ParseConsumableEntry);
        var quiverRes = LoadJsonArray(BasePath + "quivers.json", ParseQuiverEntry);
        var accessoryRes = LoadJsonArray(BasePath + "accessories.json", ParseAccessoryEntry);

        totalLoaded += armorRes.loaded + consumableRes.loaded + quiverRes.loaded + accessoryRes.loaded;
        totalFailed += armorRes.failed + consumableRes.failed + quiverRes.failed + accessoryRes.failed;

        PostProcessItems();

        if (totalFailed > 0)
            GD.PushError($"[ItemDataLoader] {totalFailed} item entries failed to parse — check JSON for errors");

        GD.Print($"[ItemDataLoader] Loaded {_weapons.Count} weapons, {_armors.Count} armors, " +
            $"{_consumables.Count} consumables, {_quivers.Count} quivers, {_accessories.Count} accessories " +
            $"({totalLoaded} entries, {totalFailed} failed)");

        if (_weapons.Count == 0 && _armors.Count == 0 && _consumables.Count == 0)
            GD.PushError("[ItemDataLoader] CRITICAL: No items loaded! Check that JSON files exist in " + BasePath);

        // Mod 物品加载：扫描 user://mods/items/ 目录
        LoadModItems();

        // 跨文件一致性校验
        ItemDataValidator.Validate();
    }

    // ========================================
    // Mod 物品加载
    // ========================================

    private const string ModItemsPath = "user://mods/items/";

    private static void LoadModItems()
    {
        if (!DirAccess.DirExistsAbsolute(ModItemsPath)) return;

        using var dir = DirAccess.Open(ModItemsPath);
        if (dir == null) return;

        int modCount = 0;
        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
            {
                string fullPath = ModItemsPath + fileName;
                // 根据文件名前缀判断类型
                if (fileName.StartsWith("weapons"))
                {
                    var (loaded, _) = LoadJsonArray(fullPath, ParseWeaponEntry);
                    modCount += loaded;
                }
                else if (fileName.StartsWith("armors"))
                {
                    var (loaded, _) = LoadJsonArray(fullPath, ParseArmorEntry);
                    modCount += loaded;
                }
                else if (fileName.StartsWith("consumables"))
                {
                    var (loaded, _) = LoadJsonArray(fullPath, ParseConsumableEntry);
                    modCount += loaded;
                }
                else if (fileName.StartsWith("accessories"))
                {
                    var (loaded, _) = LoadJsonArray(fullPath, ParseAccessoryEntry);
                    modCount += loaded;
                }
                else if (fileName.StartsWith("quivers"))
                {
                    var (loaded, _) = LoadJsonArray(fullPath, ParseQuiverEntry);
                    modCount += loaded;
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        if (modCount > 0)
        {
            PostProcessItems(); // 重新处理图标/网格
            GD.Print($"[ItemDataLoader] Loaded {modCount} mod items from {ModItemsPath}");
        }
    }

    // ========================================
    // 后处理：图标 ID + 网格尺寸
    // ========================================

    /// <summary>
    /// 自动分配纹理ID和背包网格尺寸。
    /// 纹理ID = PascalCase 的 ItemId（约定 res://assets/generated_*/FileName.png）。
    /// 网格尺寸根据物品类型/子类型自动推断（除非 JSON 显式覆盖）。
    /// </summary>
    private static void PostProcessItems()
    {
        foreach (var (id, w) in _weapons)
        {
            if (string.IsNullOrEmpty(w.EquipTextureId))
            {
                // 优先完整 ID 的 PascalCase，fallback 到子类型名（去掉 _t2/_t3 后缀）
                w.EquipTextureId = ToPascalCase(id);
            }
            if (string.IsNullOrEmpty(w.IconId)) w.IconId = w.EquipTextureId;
            // Tier fallback：如果 IconId 含 T2/T3 后缀但图标不存在，回退到子类型名
            if (w.Subtype != WeaponData.WeaponSubtype.Unarmed)
            {
                string subtypeName = w.Subtype.ToString(); // 已经是 PascalCase
                // 如果 IconId 不等于子类型名（说明有 tier 后缀），设置 fallback
                if (w.IconId != subtypeName)
                    w.IconFallbackId = subtypeName;
            }
            if (w.InvWidth == 1 && w.InvHeight == 1)
                (w.InvWidth, w.InvHeight) = GetWeaponGridSize(w);
        }

        foreach (var (id, a) in _armors)
        {
            if (string.IsNullOrEmpty(a.EquipTextureId)) a.EquipTextureId = ToPascalCase(id);
            if (string.IsNullOrEmpty(a.IconId)) a.IconId = a.EquipTextureId;
            if (a.InvWidth == 1 && a.InvHeight == 1)
                (a.InvWidth, a.InvHeight) = GetArmorGridSize(a);
        }

        foreach (var (id, c) in _consumables)
        {
            if (string.IsNullOrEmpty(c.EquipTextureId)) c.EquipTextureId = ToPascalCase(id);
            if (string.IsNullOrEmpty(c.IconId)) c.IconId = c.EquipTextureId;
            // 消耗品默认 1×1
        }

        foreach (var (id, q) in _quivers)
        {
            if (string.IsNullOrEmpty(q.EquipTextureId)) q.EquipTextureId = ToPascalCase(id);
            if (string.IsNullOrEmpty(q.IconId)) q.IconId = q.EquipTextureId;
            if (q.InvWidth == 1 && q.InvHeight == 1) { q.InvWidth = 1; q.InvHeight = 2; }
        }

        foreach (var (id, ac) in _accessories)
        {
            if (string.IsNullOrEmpty(ac.EquipTextureId)) ac.EquipTextureId = ToPascalCase(id);
            if (string.IsNullOrEmpty(ac.IconId)) ac.IconId = ac.EquipTextureId;
            // 饰品默认 1×1
        }
    }

    /// <summary>snake_case → PascalCase</summary>
    private static string ToPascalCase(string snakeCase)
    {
        var parts = snakeCase.Split('_');
        var sb = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            sb.Append(char.ToUpper(part[0]));
            if (part.Length > 1) sb.Append(part.AsSpan(1));
        }
        return sb.ToString();
    }

    private static (int w, int h) GetWeaponGridSize(WeaponData weapon)
    {
        if (weapon.IsThrowing || weapon.Subtype is
            WeaponData.WeaponSubtype.Dagger or
            WeaponData.WeaponSubtype.Stiletto or
            WeaponData.WeaponSubtype.SpikedDagger or
            WeaponData.WeaponSubtype.ThrowingKnife or
            WeaponData.WeaponSubtype.Dart or
            WeaponData.WeaponSubtype.Cestus or
            WeaponData.WeaponSubtype.StoneThrow)
            return (1, 1);

        if (!weapon.IsTwoHanded && !weapon.IsRanged && weapon.Subtype is
            WeaponData.WeaponSubtype.Seax or
            WeaponData.WeaponSubtype.Kukri or
            WeaponData.WeaponSubtype.Club or
            WeaponData.WeaponSubtype.LightHammer or
            WeaponData.WeaponSubtype.Francisca or
            WeaponData.WeaponSubtype.Javelin or
            WeaponData.WeaponSubtype.Pilum or
            WeaponData.WeaponSubtype.Harpoon or
            WeaponData.WeaponSubtype.HeavyJavelin or
            WeaponData.WeaponSubtype.ThrowingHammer)
            return (1, 2);

        if (!weapon.IsTwoHanded && !weapon.IsRanged) return (1, 3);
        if (weapon.IsRanged) return (2, 3);
        return (2, 4);
    }

    private static (int w, int h) GetArmorGridSize(ArmorData armor)
    {
        if (armor.EquipSlotTarget == ItemData.EquipSlot.Helmet) return (2, 2);
        if (armor.armorType == ArmorData.ArmorType.Shield) return (2, 2);

        return armor.armorType switch
        {
            ArmorData.ArmorType.Light => (2, 3),
            ArmorData.ArmorType.Medium => (3, 3),
            ArmorData.ArmorType.Heavy => (3, 4),
            _ => (2, 3),
        };
    }

    // ========================================
    // JSON 加载
    // ========================================

    private static (int loaded, int failed) LoadJsonArray(string path, Action<Godot.Collections.Dictionary> parseEntry)
    {
        if (!FileAccess.FileExists(path))
        {
            GD.PushError($"[ItemDataLoader] Missing JSON file: {path}");
            return (0, 0);
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"[ItemDataLoader] Cannot open: {path}");
            return (0, 0);
        }

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PushError($"[ItemDataLoader] JSON parse error in {path}: {json.GetErrorMessage()} (line {json.GetErrorLine()})");
            return (0, 0);
        }

        if (json.Data.VariantType != Variant.Type.Array)
        {
            GD.PushError($"[ItemDataLoader] {path} is not a JSON array");
            return (0, 0);
        }

        var arr = json.Data.AsGodotArray();
        int loaded = 0;
        int failed = 0;

        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i].VariantType != Variant.Type.Dictionary)
            {
                GD.PushError($"[ItemDataLoader] {path}[{i}] is not an object");
                failed++;
                continue;
            }
            try
            {
                parseEntry(arr[i].AsGodotDictionary());
                loaded++;
            }
            catch (Exception ex)
            {
                GD.PushError($"[ItemDataLoader] Failed to parse {path}[{i}]: {ex.Message}");
                failed++;
            }
        }

        return (loaded, failed);
    }

    // ========================================
    // 条目解析
    // ========================================

    private static void ParseWeaponEntry(Godot.Collections.Dictionary dict)
    {
        string id = RequireString(dict, "id");
        string subtypeStr = RequireString(dict, "subtype");

        if (!Enum.TryParse<WeaponData.WeaponSubtype>(subtypeStr, out var subtype))
            throw new Exception($"Unknown weapon subtype: {subtypeStr}");

        if (_weapons.ContainsKey(id))
            throw new Exception($"Duplicate weapon ID: {id}");

        var cfg = WeaponRegistry.GetConfig(subtype);
        int tier = OptInt(dict, "tier", 1);
        int diceCount = cfg.DiceCount + (tier - 1); // Tier 缩放

        string weaponName = OptString(dict, "name", "")
            ?? (tier > 1 ? $"{cfg.Name} +{tier - 1}" : cfg.Name);
        if (string.IsNullOrEmpty(weaponName))
            weaponName = tier > 1 ? $"{cfg.Name} +{tier - 1}" : cfg.Name;

        // ── 武器特性收集（统一通过 Traits flags） ──
        var traits = WeaponTraits.None;

        // 1) 旧风格 bool 字段（向后兼容）
        if (OptBool(dict, "finesse", false)) traits |= WeaponTraits.Finesse;
        if (OptBool(dict, "two_handed", false)) traits |= WeaponTraits.TwoHanded;
        if (OptBool(dict, "reach", false)) traits |= WeaponTraits.Reach;
        if (OptBool(dict, "dual_wield", false)) traits |= WeaponTraits.DualWieldable;
        if (OptBool(dict, "ranged", false)) traits |= WeaponTraits.Ranged;
        if (OptBool(dict, "throwing", false)) traits |= WeaponTraits.Throwing;
        if (OptBool(dict, "needs_reload", false)) traits |= WeaponTraits.NeedsReload;
        if (OptBool(dict, "blunt", false)) traits |= WeaponTraits.Blunt;
        if (OptBool(dict, "armor_piercing", false)) traits |= WeaponTraits.ArmorPiercing;
        if (OptBool(dict, "anti_cavalry", false)) traits |= WeaponTraits.AntiCavalry;
        if (OptBool(dict, "sweep", false)) traits |= WeaponTraits.Sweep;
        if (OptBool(dict, "catalyst", false)) traits |= WeaponTraits.Catalyst;

        // 2) 新风格 traits 数组（首选）
        if (dict.ContainsKey("traits") && dict["traits"].VariantType == Variant.Type.Array)
        {
            var arr = dict["traits"].AsGodotArray();
            foreach (var v in arr)
            {
                if (v.VariantType != Variant.Type.String) continue;
                var t = WeaponTraitsExtensions.ParseId(v.AsString());
                if (t == WeaponTraits.None)
                    throw new Exception($"Unknown weapon trait: '{v.AsString()}' in weapon '{id}'");
                traits |= t;
            }
        }

        _weapons[id] = new WeaponData
        {
            ItemId = id,
            ItemName = weaponName,
            Subtype = subtype,
            Tier = tier,
            DamageDiceCount = diceCount,
            DamageDiceSides = cfg.DiceSides,
            ApCost = cfg.BaseApCost,
            RangeCells = cfg.Range,
            Price = OptInt(dict, "price", 10),
            Traits = traits,
            WeaponDamageType = cfg.DamageType,
            WeaponPen = 0,                                         // v0.6 已废弃：武器穿透修正字段，强制 0
            Weight = WeaponRegistry.GetWeight(subtype),            // v0.6 6.9 重量分支
            Class = WeaponRegistry.IsRangedSubtype(subtype)
                ? WeaponData.WeaponClass.Ranged
                : WeaponData.WeaponClass.Melee,
            Description = OptString(dict, "desc", "") ?? "",
        };
    }

    private static void ParseArmorEntry(Godot.Collections.Dictionary dict)
    {
        string id = RequireString(dict, "id");
        if (_armors.ContainsKey(id))
            throw new Exception($"Duplicate armor ID: {id}");

        string typeStr = OptString(dict, "type", "Light") ?? "Light";
        var armorType = typeStr switch
        {
            "Cloth" or "Light" => ArmorData.ArmorType.Light,
            "Medium" => ArmorData.ArmorType.Medium,
            "Heavy" => ArmorData.ArmorType.Heavy,
            "Shield" => ArmorData.ArmorType.Shield,
            _ => throw new Exception($"Unknown armor type: {typeStr}"),
        };

        string slotStr = OptString(dict, "slot", "") ?? "";
        var equipSlot = slotStr switch
        {
            "Helmet" or "Head" => ItemData.EquipSlot.Helmet,
            "Hands" => ItemData.EquipSlot.Hands,
            "Feet" => ItemData.EquipSlot.Feet,
            _ => ItemData.EquipSlot.Body,
        };

        int dr = OptInt(dict, "dr", 0);
        // AC 由 DR 派生：floor(sqrt(DR))
        int ac = (int)Math.Floor(Math.Sqrt(dr));

        _armors[id] = new ArmorData
        {
            ItemId = id,
            ItemName = OptString(dict, "name", id) ?? id,
            armorType = armorType,
            AcBonus = ac,
            DrThreshold = dr,
            MaxDexBonus = OptInt(dict, "max_dex", 99),
            ApPenalty = OptInt(dict, "ap_penalty", 0),
            MovementPenalty = OptInt(dict, "ap_penalty", 0),
            StrRequired = OptInt(dict, "str_req", 0),
            StealthDisadvantage = OptBool(dict, "stealth_disadvantage", false),
            IsDestroyable = OptBool(dict, "destroyable", false),
            Price = OptInt(dict, "price", 10),
            EquipSlotTarget = equipSlot,
            // v0.6 6.2 盾牌远程伤害减免乘数（仅盾牌；身体甲忽略）
            RangedDamageMultiplier = dict.ContainsKey("ranged_mult")
                ? (float)dict["ranged_mult"].AsDouble()
                : 1.0f,
            Description = OptString(dict, "desc", "") ?? "",
        };
    }

    private static void ParseConsumableEntry(Godot.Collections.Dictionary dict)
    {
        string id = RequireString(dict, "id");
        if (_consumables.ContainsKey(id))
            throw new Exception($"Duplicate consumable ID: {id}");

        _consumables[id] = new ConsumableData
        {
            ItemId = id,
            ItemName = OptString(dict, "name", id) ?? id,
            Description = OptString(dict, "desc", "") ?? "",
            Price = OptInt(dict, "price", 10),
        };
    }

    private static void ParseQuiverEntry(Godot.Collections.Dictionary dict)
    {
        string id = RequireString(dict, "id");
        if (_quivers.ContainsKey(id))
            throw new Exception($"Duplicate quiver ID: {id}");

        _quivers[id] = new ItemData
        {
            ItemId = id,
            ItemName = OptString(dict, "name", id) ?? id,
            QuiverDamageBonus = OptInt(dict, "damage_bonus", 0),
            Price = OptInt(dict, "price", 10),
            Description = OptString(dict, "desc", "") ?? "",
        };
    }

    private static void ParseAccessoryEntry(Godot.Collections.Dictionary dict)
    {
        string id = RequireString(dict, "id");
        if (_accessories.ContainsKey(id))
            throw new Exception($"Duplicate accessory ID: {id}");

        string typeStr = OptString(dict, "type", "Ring") ?? "Ring";
        var accType = typeStr switch
        {
            "Ring" => AccessoryData.AccessoryType.Ring,
            "Amulet" => AccessoryData.AccessoryType.Amulet,
            "Cloak" => AccessoryData.AccessoryType.Cloak,
            "Belt" => AccessoryData.AccessoryType.Belt,
            "Bracer" => AccessoryData.AccessoryType.Bracer,
            _ => throw new Exception($"Unknown accessory type: {typeStr}"),
        };

        var rarity = ParseRarity(OptString(dict, "rarity", "Common") ?? "Common");

        _accessories[id] = new AccessoryData
        {
            ItemId = id,
            ItemName = OptString(dict, "name", id) ?? id,
            accessoryType = accType,
            ItemRarity = rarity,
            Price = OptInt(dict, "price", 10),
            Description = OptString(dict, "desc", "") ?? "",

            StrBonus = OptInt(dict, "str", 0),
            DexBonus = OptInt(dict, "dex", 0),
            ConBonus = OptInt(dict, "con", 0),
            IntBonus = OptInt(dict, "int", 0),
            WisBonus = OptInt(dict, "wis", 0),
            ChaBonus = OptInt(dict, "cha", 0),

            HpBonus = OptInt(dict, "hp", 0),
            AcBonus = OptInt(dict, "ac", 0),
            MoveBonus = OptInt(dict, "move", 0),
            InitiativeBonus = OptInt(dict, "initiative", 0),

            Resistance = OptString(dict, "resistance", "") ?? "",
            Immunity = OptString(dict, "immunity", "") ?? "",
            SpecialEffect = OptString(dict, "special_effect", "") ?? "",
            SpecialValue = (float)(dict.ContainsKey("special_value") ? dict["special_value"].AsDouble() : 0.0),
        };

        // 装备能力组件：从 special_effect/value 创建
        var acc = _accessories[id];
        if (!string.IsNullOrEmpty(acc.SpecialEffect))
        {
            var ability = BladeHex.Combat.Abilities.EquipmentAbilityRegistry.Create(
                acc.SpecialEffect, acc.SpecialValue);
            if (ability != null) acc.Abilities.Add(ability);
        }

        // 还可读取 abilities 数组（未来扩展：一个物品多个能力）
        if (dict.ContainsKey("abilities") && dict["abilities"].VariantType == Variant.Type.Array)
        {
            var abArr = dict["abilities"].AsGodotArray();
            foreach (var v in abArr)
            {
                if (v.VariantType != Variant.Type.Dictionary) continue;
                var abDict = v.AsGodotDictionary();
                string abId = abDict.ContainsKey("id") ? abDict["id"].AsString() : "";
                float mag = (float)(abDict.ContainsKey("value") ? abDict["value"].AsDouble() : 0.0);
                var ability = BladeHex.Combat.Abilities.EquipmentAbilityRegistry.Create(abId, mag);
                if (ability != null) acc.Abilities.Add(ability);
            }
        }
    }

    private static ItemData.Rarity ParseRarity(string s) => s switch
    {
        "Common" => ItemData.Rarity.Common,
        "Uncommon" => ItemData.Rarity.Uncommon,
        "Rare" => ItemData.Rarity.Rare,
        "Epic" => ItemData.Rarity.Epic,
        "Legendary" => ItemData.Rarity.Legendary,
        _ => ItemData.Rarity.Common,
    };

    // ========================================
    // 字段读取辅助
    // ========================================

    private static string RequireString(Godot.Collections.Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
            throw new Exception($"Missing required field: '{key}'");
        return dict[key].AsString();
    }

    private static string? OptString(Godot.Collections.Dictionary dict, string key, string? defaultValue)
        => dict.ContainsKey(key) ? dict[key].AsString() : defaultValue;

    private static int OptInt(Godot.Collections.Dictionary dict, string key, int defaultValue)
        => dict.ContainsKey(key) ? dict[key].AsInt32() : defaultValue;

    private static bool OptBool(Godot.Collections.Dictionary dict, string key, bool defaultValue)
        => dict.ContainsKey(key) ? dict[key].AsBool() : defaultValue;
}
