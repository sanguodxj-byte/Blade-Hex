// MountData.cs
// 坐骑数据资源 — 骑乘系统用
// 对应策划案 06-装备与物品 → 坐骑系统
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class MountData : Resource
{
    // ========================================
    // 数据字段
    // ========================================

    [Export] public string MountName { get; set; } = "驮马";
    [Export] public string MountId { get; set; } = "";
    [Export] public int SpeedBonus { get; set; } = 1;
    [Export] public int MaxHp { get; set; } = 15;
    [Export] public int CarryCapacity { get; set; } = 1; // 1=中, 2=高
    [Export] public float ChargeDamageBonus { get; set; } = 0.25f;
    [Export] public string[] SpecialTraits = [];
    [Export] public bool CanForest { get; set; } = true;
    [Export] public bool CanDenseForest;
    [Export] public string[] AllowedMountedWeapons = [];
    [Export] public int Price { get; set; } = 20;
    [Export] public string Description { get; set; } = "";

    /// <summary>
    /// 装备能力组件 — 从 SpecialTraits 字符串数组转换而来。
    /// 加新坐骑能力时只需在 BuildAbilitiesFromTraits 中添加映射。
    /// </summary>
    public System.Collections.Generic.List<BladeHex.Combat.Abilities.EquipmentAbility> Abilities { get; }
        = new();

    /// <summary>从 SpecialTraits 字符串构建能力组件（在工厂方法或 JSON 加载后调用一次）</summary>
    public void BuildAbilitiesFromTraits()
    {
        Abilities.Clear();
        if (SpecialTraits == null) return;

        foreach (var trait in SpecialTraits)
        {
            BladeHex.Combat.Abilities.EquipmentAbility? ab = trait switch
            {
                "immune_fear" => new BladeHex.Combat.Abilities.ImmunityAbility { AbilityId = "immune_fear", ImmunityType = "fear" },
                "forest_walk" => new BladeHex.Combat.Abilities.TerrainTraitAbility { AbilityId = "forest_walk", TerrainTrait = "forest_walk" },
                "stealth_no_break" => new BladeHex.Combat.Abilities.TerrainTraitAbility { AbilityId = "stealth_no_break", TerrainTrait = "stealth_no_break" },
                "extra_damage_1d4" => new BladeHex.Combat.Abilities.ExtraDamageDiceAbility { AbilityId = "extra_damage_1d4", DiceCount = 1, DiceSides = 4 },
                "flanking_bonus" => BladeHex.Combat.Abilities.EquipmentAbilityRegistry.Create("flanking_bonus", 1),
                _ => null,
            };
            if (ab != null) Abilities.Add(ab);
        }
    }

    // ========================================
    // 预定义坐骑工厂
    // ========================================

    public static MountData[] GetAllMounts()
    {
        var arr = new MountData[]
        {
            CreatePackHorse(),
            CreateWarHorse(),
            CreateEliteWarHorse(),
            CreateElfStag(),
            CreateDwarfWarBear(),
            CreateWolf(),
        };
        foreach (var m in arr) m.BuildAbilitiesFromTraits();
        return arr;
    }

    public static MountData GetMountById(string id)
    {
        foreach (var m in GetAllMounts())
            if (m.MountId == id) return m;
        var fallback = CreatePackHorse();
        fallback.BuildAbilitiesFromTraits();
        return fallback;
    }

    private static MountData CreatePackHorse() => new()
    {
        MountId = "pack_horse",
        MountName = "驮马",
        SpeedBonus = 1,
        MaxHp = 15,
        CarryCapacity = 2,
        ChargeDamageBonus = 0.0f,
        CanForest = true,
        CanDenseForest = false,
        Price = 20,
        Description = "无战斗加成，纯运输用途。",
    };

    private static MountData CreateWarHorse() => new()
    {
        MountId = "war_horse",
        MountName = "军马",
        SpeedBonus = 2,
        MaxHp = 20,
        CarryCapacity = 1,
        ChargeDamageBonus = 0.25f,
        CanForest = true,
        CanDenseForest = false,
        AllowedMountedWeapons = ["shortbow", "hand_crossbow"],
        Price = 80,
        Description = "冲锋伤害+25%，可骑射。",
    };

    private static MountData CreateEliteWarHorse() => new()
    {
        MountId = "elite_war_horse",
        MountName = "战马",
        SpeedBonus = 3,
        MaxHp = 25,
        CarryCapacity = 1,
        ChargeDamageBonus = 0.50f,
        SpecialTraits = ["immune_fear"],
        CanForest = true,
        CanDenseForest = false,
        AllowedMountedWeapons = ["shortbow", "hand_crossbow"],
        Price = 200,
        Description = "冲锋伤害+50%，免疫恐惧，可骑射。",
    };

    private static MountData CreateElfStag() => new()
    {
        MountId = "elf_stag",
        MountName = "精灵角鹿",
        SpeedBonus = 2,
        MaxHp = 18,
        CarryCapacity = 0,
        ChargeDamageBonus = 0.25f,
        SpecialTraits = ["forest_walk", "stealth_no_break"],
        CanForest = true,
        CanDenseForest = true,
        AllowedMountedWeapons = ["shortbow", "hand_crossbow"],
        Price = 150,
        Description = "可穿越森林（不减速），潜行不中断，可骑射。",
    };

    private static MountData CreateDwarfWarBear() => new()
    {
        MountId = "dwarf_war_bear",
        MountName = "矮人战熊",
        SpeedBonus = 1,
        MaxHp = 30,
        CarryCapacity = 2,
        ChargeDamageBonus = 0.25f,
        SpecialTraits = ["immune_fear", "extra_damage_1d4"],
        CanForest = true,
        CanDenseForest = false,
        Price = 250,
        Description = "攻击时附带1d4额外伤害，免疫恐惧。",
    };

    private static MountData CreateWolf() => new()
    {
        MountId = "wolf",
        MountName = "狼",
        SpeedBonus = 2,
        MaxHp = 12,
        CarryCapacity = 0,
        ChargeDamageBonus = 0.25f,
        SpecialTraits = ["flanking_bonus"],
        CanForest = true,
        CanDenseForest = false,
        AllowedMountedWeapons = ["shortbow", "hand_crossbow"],
        Price = 60,
        Description = "包夹时额外+1命中，可骑射。",
    };
}
