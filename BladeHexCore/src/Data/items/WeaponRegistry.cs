using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 武器注册表 — 从 JSON 加载武器子类型配置
/// </summary>
public static class WeaponRegistry
{
    public struct WeaponConfig
    {
        public string Name;
        public int DiceCount;
        public int DiceSides;
        public int BaseApCost;
        public int HitBonus;
        public int PenBonus;
        public int Range;
        public int ReloadAp;
        public WeaponData.DamageType DamageType;
    }

    // ============================================================================
    // JSON 驱动加载
    // ============================================================================

    private static Dictionary<WeaponData.WeaponSubtype, WeaponConfig>? _cachedRegistry;
    private static Dictionary<WeaponData.WeaponSubtype, WeaponData.WeightCategory>? _cachedWeights;
    private const string JsonPath = "res://BladeHexCore/src/Data/items/weapon_subtypes.json";
    private const string ModPath = "user://mods/weapon_subtypes/";

    private static Dictionary<WeaponData.WeaponSubtype, WeaponConfig> Registry
    {
        get
        {
            if (_cachedRegistry != null) return _cachedRegistry;
            LoadFromJson();
            return _cachedRegistry!;
        }
    }

    private static Dictionary<WeaponData.WeaponSubtype, WeaponData.WeightCategory> Weights
    {
        get
        {
            if (_cachedWeights != null) return _cachedWeights;
            LoadFromJson();
            return _cachedWeights!;
        }
    }

    /// <summary>强制重新加载（热重载用）</summary>
    public static void Reload()
    {
        _cachedRegistry = null;
        _cachedWeights = null;
    }

    // ============================================================================
    // 查询接口
    // ============================================================================

    public static WeaponConfig GetConfig(WeaponData.WeaponSubtype subtype)
    {
        if (Registry.TryGetValue(subtype, out var config)) return config;
        return Registry[WeaponData.WeaponSubtype.Unarmed];
    }

    /// <summary>
    /// 武器重量分类 (v0.6 7.0)
    /// </summary>
    public static WeaponData.WeightCategory GetWeight(WeaponData.WeaponSubtype subtype)
    {
        if (Weights.TryGetValue(subtype, out var weight)) return weight;
        return WeaponData.WeightCategory.Light;
    }

    /// <summary>武器是否为远程类（包括投掷、弓、弩）。Catalyst 不算远程。</summary>
    public static bool IsRangedSubtype(WeaponData.WeaponSubtype subtype)
        => subtype >= WeaponData.WeaponSubtype.ThrowingKnife
        && subtype <= WeaponData.WeaponSubtype.Ballista;

    /// <summary>武器是否为法术媒介（v0.6 10.0）。</summary>
    public static bool IsCatalystSubtype(WeaponData.WeaponSubtype subtype)
        => subtype == WeaponData.WeaponSubtype.Wand
        || subtype == WeaponData.WeaponSubtype.Orb
        || subtype == WeaponData.WeaponSubtype.Staff;

    // ============================================================================
    // JSON 解析
    // ============================================================================

    private static void LoadFromJson()
    {
        var registry = new Dictionary<WeaponData.WeaponSubtype, WeaponConfig>();
        var weights = new Dictionary<WeaponData.WeaponSubtype, WeaponData.WeightCategory>();

        // 内置武器
        LoadWeaponsFromFile(JsonPath, registry, weights);

        // Mod 武器
        if (DirAccess.DirExistsAbsolute(ModPath))
        {
            using var dir = DirAccess.Open(ModPath);
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName = dir.GetNext();
                while (!string.IsNullOrEmpty(fileName))
                {
                    if (fileName.EndsWith(".json"))
                        LoadWeaponsFromFile(ModPath + fileName, registry, weights);
                    fileName = dir.GetNext();
                }
                dir.ListDirEnd();
            }
        }

        if (registry.Count == 0)
        {
            GD.PushError("[WeaponRegistry] No weapons loaded! Using emergency fallback.");
            registry[WeaponData.WeaponSubtype.Unarmed] = new WeaponConfig
            {
                Name = "赤手空拳", DiceCount = 1, DiceSides = 3, BaseApCost = 2,
                HitBonus = 0, PenBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Crush
            };
            weights[WeaponData.WeaponSubtype.Unarmed] = WeaponData.WeightCategory.Light;
        }
        else
        {
            GD.Print($"[WeaponRegistry] Loaded {registry.Count} weapon subtypes");
        }

        _cachedRegistry = registry;
        _cachedWeights = weights;
    }

    private static void LoadWeaponsFromFile(string path,
        Dictionary<WeaponData.WeaponSubtype, WeaponConfig> registry,
        Dictionary<WeaponData.WeaponSubtype, WeaponData.WeightCategory> weights)
    {
        if (!FileAccess.FileExists(path)) return;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return;

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PushError($"[WeaponRegistry] JSON parse error in {path}: {json.GetErrorMessage()}");
            return;
        }

        if (json.Data.VariantType != Variant.Type.Array) return;
        var arr = json.Data.AsGodotArray();

        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i].VariantType != Variant.Type.Dictionary) continue;
            var entry = arr[i].AsGodotDictionary();

            try
            {
                ParseWeaponEntry(entry, registry, weights);
            }
            catch (Exception ex)
            {
                GD.PushError($"[WeaponRegistry] Failed to parse {path}[{i}]: {ex.Message}");
            }
        }
    }

    private static void ParseWeaponEntry(Godot.Collections.Dictionary entry,
        Dictionary<WeaponData.WeaponSubtype, WeaponConfig> registry,
        Dictionary<WeaponData.WeaponSubtype, WeaponData.WeightCategory> weights)
    {
        string subtypeStr = entry.ContainsKey("subtype") ? entry["subtype"].AsString() : "";
        if (!Enum.TryParse<WeaponData.WeaponSubtype>(subtypeStr, out var subtype))
            return;

        // DamageType
        var dmgType = WeaponData.DamageType.Crush;
        if (entry.ContainsKey("damage_type"))
        {
            string dmgStr = entry["damage_type"].AsString();
            if (Enum.TryParse<WeaponData.DamageType>(dmgStr, out var parsed))
                dmgType = parsed;
        }

        var config = new WeaponConfig
        {
            Name = entry.ContainsKey("name") ? entry["name"].AsString() : subtypeStr,
            DiceCount = OptInt(entry, "dice_count", 1),
            DiceSides = OptInt(entry, "dice_sides", 4),
            BaseApCost = OptInt(entry, "base_ap_cost", 4),
            HitBonus = OptInt(entry, "hit_bonus", 0),
            PenBonus = OptInt(entry, "pen_bonus", 0),
            Range = OptInt(entry, "range", 1),
            ReloadAp = OptInt(entry, "reload_ap", 0),
            DamageType = dmgType,
        };

        registry[subtype] = config;

        // Weight
        if (entry.ContainsKey("weight"))
        {
            string weightStr = entry["weight"].AsString();
            if (Enum.TryParse<WeaponData.WeightCategory>(weightStr, out var w))
                weights[subtype] = w;
            else
                weights[subtype] = WeaponData.WeightCategory.Light;
        }
        else
        {
            weights[subtype] = WeaponData.WeightCategory.Light;
        }
    }

    private static int OptInt(Godot.Collections.Dictionary dict, string key, int def)
        => dict.ContainsKey(key) ? dict[key].AsInt32() : def;
}
