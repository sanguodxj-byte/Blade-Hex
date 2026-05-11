using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Data;

public static class WeaponRegistry
{
    public struct WeaponConfig
    {
        public string Name;
        public int DiceCount;
        public int DiceSides;
        public int BaseApCost;
        public int HitBonus;
        public int Range;
        public int ReloadAp;
        public WeaponData.DamageType DamageType; // 新增伤害类型
    }

    private static readonly Dictionary<WeaponData.WeaponSubtype, WeaponConfig> Registry = new()
    {
        // Slash (砍伤)
        { WeaponData.WeaponSubtype.Dagger, new WeaponConfig { Name = "匕首", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Seax, new WeaponConfig { Name = "萨克斯短刀", DiceCount = 1, DiceSides = 6, BaseApCost = 3, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Kukri, new WeaponConfig { Name = "廓尔喀刀", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = +1, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.ArmingSword, new WeaponConfig { Name = "武装剑", DiceCount = 1, DiceSides = 8, BaseApCost = 4, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.BattleAxe, new WeaponConfig { Name = "战斧", DiceCount = 1, DiceSides = 10, BaseApCost = 4, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.NomadSaber, new WeaponConfig { Name = "游牧弯刀", DiceCount = 1, DiceSides = 6, BaseApCost = 4, HitBonus = +1, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Greatsword, new WeaponConfig { Name = "巨剑", DiceCount = 2, DiceSides = 6, BaseApCost = 6, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.GreatAxe, new WeaponConfig { Name = "巨斧", DiceCount = 1, DiceSides = 12, BaseApCost = 6, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Glaive, new WeaponConfig { Name = "偃月刀", DiceCount = 1, DiceSides = 10, BaseApCost = 6, HitBonus = +1, Range = 2, DamageType = WeaponData.DamageType.Slash }},

        // Pierce (刺伤)
        { WeaponData.WeaponSubtype.Stiletto, new WeaponConfig { Name = "锥刺", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.SpikedDagger, new WeaponConfig { Name = "穿甲短剑", DiceCount = 1, DiceSides = 6, BaseApCost = 2, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Rapier, new WeaponConfig { Name = "刺剑", DiceCount = 1, DiceSides = 6, BaseApCost = 3, HitBonus = +1, Range = 1, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.InfantrySpear, new WeaponConfig { Name = "步兵长矛", DiceCount = 1, DiceSides = 8, BaseApCost = 4, HitBonus = 0, Range = 2, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.BroadSpear, new WeaponConfig { Name = "阔头矛", DiceCount = 1, DiceSides = 10, BaseApCost = 5, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Awlpike, new WeaponConfig { Name = "锥头矛", DiceCount = 1, DiceSides = 6, BaseApCost = 4, HitBonus = +1, Range = 3, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Lance, new WeaponConfig { Name = "骑士长枪", DiceCount = 1, DiceSides = 12, BaseApCost = 6, HitBonus = 0, Range = 2, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Voulge, new WeaponConfig { Name = "长柄斧", DiceCount = 1, DiceSides = 10, BaseApCost = 6, HitBonus = 0, Range = 2, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Trident, new WeaponConfig { Name = "三叉戟", DiceCount = 1, DiceSides = 8, BaseApCost = 6, HitBonus = +1, Range = 2, DamageType = WeaponData.DamageType.Pierce }},

        // Crush (钝伤)
        { WeaponData.WeaponSubtype.Club, new WeaponConfig { Name = "木棒", DiceCount = 1, DiceSides = 6, BaseApCost = 3, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.LightHammer, new WeaponConfig { Name = "轻战锤", DiceCount = 1, DiceSides = 8, BaseApCost = 3, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Cestus, new WeaponConfig { Name = "铁手套", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = +1, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.WingedMace, new WeaponConfig { Name = "翼形锤矛", DiceCount = 1, DiceSides = 8, BaseApCost = 4, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.MilitaryHammer, new WeaponConfig { Name = "军用锤", DiceCount = 1, DiceSides = 10, BaseApCost = 5, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Flail, new WeaponConfig { Name = "连枷", DiceCount = 1, DiceSides = 6, BaseApCost = 4, HitBonus = +1, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Maul, new WeaponConfig { Name = "大锤", DiceCount = 2, DiceSides = 6, BaseApCost = 7, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Greatclub, new WeaponConfig { Name = "巨型木棒", DiceCount = 1, DiceSides = 12, BaseApCost = 7, HitBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Polehammer, new WeaponConfig { Name = "长柄战锤", DiceCount = 1, DiceSides = 10, BaseApCost = 7, HitBonus = +1, Range = 2, DamageType = WeaponData.DamageType.Crush }},

        // Ranged 略 (逻辑同上，统一映射)
        { WeaponData.WeaponSubtype.ThrowingKnife, new WeaponConfig { Name = "飞刀", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, Range = 4, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Javelin, new WeaponConfig { Name = "标枪", DiceCount = 1, DiceSides = 6, BaseApCost = 4, HitBonus = 0, Range = 6, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Shortbow, new WeaponConfig { Name = "短弓", DiceCount = 1, DiceSides = 6, BaseApCost = 4, HitBonus = 0, Range = 8, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.StandardCrossbow, new WeaponConfig { Name = "标准弩", DiceCount = 1, DiceSides = 10, BaseApCost = 6, HitBonus = 0, Range = 12, DamageType = WeaponData.DamageType.Pierce, ReloadAp = 5 }},

        // ============================================================================
        // 远程 Ranged (L:2-4 AP, M:5-6 AP, H:7-10 AP)
        // ============================================================================
        // Thrown
        { WeaponData.WeaponSubtype.ThrowingKnife, new WeaponConfig { Name = "飞刀", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, Range = 4 }},
        { WeaponData.WeaponSubtype.Dart, new WeaponConfig { Name = "飞镖", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, Range = 5 }},
        { WeaponData.WeaponSubtype.Francisca, new WeaponConfig { Name = "弗兰西斯卡飞斧", DiceCount = 1, DiceSides = 4, BaseApCost = 3, HitBonus = +1, Range = 3 }},

        { WeaponData.WeaponSubtype.Javelin, new WeaponConfig { Name = "标枪", DiceCount = 1, DiceSides = 6, BaseApCost = 4, HitBonus = 0, Range = 6 }},
        { WeaponData.WeaponSubtype.Pilum, new WeaponConfig { Name = "重标枪", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = 0, Range = 5 }},
        { WeaponData.WeaponSubtype.Harpoon, new WeaponConfig { Name = "渔叉", DiceCount = 1, DiceSides = 4, BaseApCost = 4, HitBonus = +1, Range = 4 }},

        { WeaponData.WeaponSubtype.StoneThrow, new WeaponConfig { Name = "投石", DiceCount = 1, DiceSides = 4, BaseApCost = 5, HitBonus = 0, Range = 6 }},
        { WeaponData.WeaponSubtype.HeavyJavelin, new WeaponConfig { Name = "重标枪", DiceCount = 1, DiceSides = 10, BaseApCost = 6, HitBonus = 0, Range = 4 }},
        { WeaponData.WeaponSubtype.ThrowingHammer, new WeaponConfig { Name = "投掷飞锤", DiceCount = 1, DiceSides = 6, BaseApCost = 5, HitBonus = +1, Range = 3 }},

        // Bows
        { WeaponData.WeaponSubtype.Shortbow, new WeaponConfig { Name = "短弓", DiceCount = 1, DiceSides = 6, BaseApCost = 4, HitBonus = 0, Range = 8 }},
        { WeaponData.WeaponSubtype.HuntingBow, new WeaponConfig { Name = "狩猎弓", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = 0, Range = 10 }},
        { WeaponData.WeaponSubtype.NomadBow, new WeaponConfig { Name = "游牧弓", DiceCount = 1, DiceSides = 4, BaseApCost = 4, HitBonus = +1, Range = 7 }},

        { WeaponData.WeaponSubtype.Strongbow, new WeaponConfig { Name = "强弓", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = 0, Range = 10 }},
        { WeaponData.WeaponSubtype.RecurveBow, new WeaponConfig { Name = "反曲弓", DiceCount = 1, DiceSides = 10, BaseApCost = 6, HitBonus = 0, Range = 12 }},
        { WeaponData.WeaponSubtype.WarBow, new WeaponConfig { Name = "战弓", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = +1, Range = 11 }},

        { WeaponData.WeaponSubtype.Longbow, new WeaponConfig { Name = "长弓", DiceCount = 1, DiceSides = 10, BaseApCost = 7, HitBonus = 0, Range = 15 }},
        { WeaponData.WeaponSubtype.CompositeLongbow, new WeaponConfig { Name = "复合长弓", DiceCount = 2, DiceSides = 6, BaseApCost = 8, HitBonus = 0, Range = 14 }},
        { WeaponData.WeaponSubtype.Greatbow, new WeaponConfig { Name = "巨弓", DiceCount = 1, DiceSides = 10, BaseApCost = 7, HitBonus = +1, Range = 13 }},

        // Crossbows
        { WeaponData.WeaponSubtype.LightCrossbow, new WeaponConfig { Name = "轻弩", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = 0, Range = 10, ReloadAp = 4 }},
        { WeaponData.WeaponSubtype.HuntingCrossbow, new WeaponConfig { Name = "猎弩", DiceCount = 1, DiceSides = 10, BaseApCost = 5, HitBonus = 0, Range = 12, ReloadAp = 3 }},
        { WeaponData.WeaponSubtype.PistolCrossbow, new WeaponConfig { Name = "手弩", DiceCount = 1, DiceSides = 4, BaseApCost = 5, HitBonus = +1, Range = 6, ReloadAp = 2 }},

        { WeaponData.WeaponSubtype.StandardCrossbow, new WeaponConfig { Name = "标准弩", DiceCount = 1, DiceSides = 10, BaseApCost = 6, HitBonus = 0, Range = 12, ReloadAp = 5 }},
        { WeaponData.WeaponSubtype.StrongCrossbow, new WeaponConfig { Name = "强弩", DiceCount = 1, DiceSides = 12, BaseApCost = 7, HitBonus = 0, Range = 11, ReloadAp = 6 }},
        { WeaponData.WeaponSubtype.SniperCrossbow, new WeaponConfig { Name = "狙击弩", DiceCount = 1, DiceSides = 8, BaseApCost = 6, HitBonus = +1, Range = 16, ReloadAp = 7 }},

        { WeaponData.WeaponSubtype.HeavyCrossbow, new WeaponConfig { Name = "重弩", DiceCount = 2, DiceSides = 6, BaseApCost = 8, HitBonus = 0, Range = 14, ReloadAp = 8 }},
        { WeaponData.WeaponSubtype.SiegeCrossbow, new WeaponConfig { Name = "攻城弩", DiceCount = 2, DiceSides = 8, BaseApCost = 9, HitBonus = 0, Range = 13, ReloadAp = 10 }},
        { WeaponData.WeaponSubtype.Ballista, new WeaponConfig { Name = "床弩", DiceCount = 1, DiceSides = 12, BaseApCost = 8, HitBonus = +1, Range = 18, ReloadAp = 12 }},

        { WeaponData.WeaponSubtype.Unarmed, new WeaponConfig { Name = "赤手空拳", DiceCount = 1, DiceSides = 3, BaseApCost = 2, HitBonus = 0, Range = 1 }}
    };

    public static WeaponConfig GetConfig(WeaponData.WeaponSubtype subtype)
    {
        if (Registry.TryGetValue(subtype, out var config)) return config;
        return Registry[WeaponData.WeaponSubtype.Unarmed];
    }
}
