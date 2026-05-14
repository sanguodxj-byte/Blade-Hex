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
        public int PenBonus;
        public int Range;
        public int ReloadAp;
        public WeaponData.DamageType DamageType;
    }

    private static readonly Dictionary<WeaponData.WeaponSubtype, WeaponConfig> Registry = new()
    {
        // ============================================================================
        // 近战 Melee — 7.1 Slash (砍伤)
        // ============================================================================
        // Light
        { WeaponData.WeaponSubtype.Dagger, new WeaponConfig { Name = "匕首", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, PenBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Seax, new WeaponConfig { Name = "萨克斯短刀", DiceCount = 1, DiceSides = 6, BaseApCost = 3, HitBonus = 0, PenBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Kukri, new WeaponConfig { Name = "廓尔喀刀", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = +1, PenBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        // Medium
        { WeaponData.WeaponSubtype.ArmingSword, new WeaponConfig { Name = "武装剑", DiceCount = 1, DiceSides = 10, BaseApCost = 4, HitBonus = 0, PenBonus = 1, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.BattleAxe, new WeaponConfig { Name = "战斧", DiceCount = 1, DiceSides = 12, BaseApCost = 4, HitBonus = 0, PenBonus = 1, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.NomadSaber, new WeaponConfig { Name = "游牧弯刀", DiceCount = 1, DiceSides = 8, BaseApCost = 4, HitBonus = +1, PenBonus = 1, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        // Heavy
        { WeaponData.WeaponSubtype.Greatsword, new WeaponConfig { Name = "巨剑", DiceCount = 3, DiceSides = 6, BaseApCost = 6, HitBonus = 0, PenBonus = 2, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.GreatAxe, new WeaponConfig { Name = "巨斧", DiceCount = 2, DiceSides = 10, BaseApCost = 6, HitBonus = 0, PenBonus = 2, Range = 1, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Glaive, new WeaponConfig { Name = "偃月刀", DiceCount = 2, DiceSides = 8, BaseApCost = 6, HitBonus = +1, PenBonus = 2, Range = 2, DamageType = WeaponData.DamageType.Slash }},

        // ============================================================================
        // 近战 Melee — 7.2 Pierce (刺伤)
        // ============================================================================
        // Light
        { WeaponData.WeaponSubtype.Stiletto, new WeaponConfig { Name = "锥刺", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, PenBonus = 1, Range = 1, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.SpikedDagger, new WeaponConfig { Name = "穿甲短剑", DiceCount = 1, DiceSides = 6, BaseApCost = 2, HitBonus = 0, PenBonus = 2, Range = 1, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Rapier, new WeaponConfig { Name = "刺剑", DiceCount = 1, DiceSides = 8, BaseApCost = 3, HitBonus = +1, PenBonus = 2, Range = 1, DamageType = WeaponData.DamageType.Pierce }},
        // Medium
        { WeaponData.WeaponSubtype.InfantrySpear, new WeaponConfig { Name = "步兵长矛", DiceCount = 1, DiceSides = 10, BaseApCost = 4, HitBonus = 0, PenBonus = 3, Range = 2, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.BroadSpear, new WeaponConfig { Name = "阔头矛", DiceCount = 1, DiceSides = 12, BaseApCost = 5, HitBonus = 0, PenBonus = 3, Range = 1, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Awlpike, new WeaponConfig { Name = "锥头矛", DiceCount = 1, DiceSides = 8, BaseApCost = 4, HitBonus = +1, PenBonus = 3, Range = 3, DamageType = WeaponData.DamageType.Pierce }},
        // Heavy
        { WeaponData.WeaponSubtype.Lance, new WeaponConfig { Name = "骑士长枪", DiceCount = 2, DiceSides = 10, BaseApCost = 6, HitBonus = 0, PenBonus = 4, Range = 2, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Voulge, new WeaponConfig { Name = "长柄斧", DiceCount = 2, DiceSides = 8, BaseApCost = 6, HitBonus = 0, PenBonus = 4, Range = 2, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Trident, new WeaponConfig { Name = "三叉戟", DiceCount = 2, DiceSides = 6, BaseApCost = 6, HitBonus = +1, PenBonus = 4, Range = 2, DamageType = WeaponData.DamageType.Pierce }},

        // ============================================================================
        // 近战 Melee — 7.3 Crush (钝伤)
        // ============================================================================
        // Light
        { WeaponData.WeaponSubtype.Club, new WeaponConfig { Name = "木棒", DiceCount = 1, DiceSides = 6, BaseApCost = 3, HitBonus = 0, PenBonus = 1, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.LightHammer, new WeaponConfig { Name = "轻战锤", DiceCount = 1, DiceSides = 8, BaseApCost = 3, HitBonus = 0, PenBonus = 1, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Cestus, new WeaponConfig { Name = "铁手套", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = +1, PenBonus = 1, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        // Medium
        { WeaponData.WeaponSubtype.WingedMace, new WeaponConfig { Name = "翼形锤矛", DiceCount = 1, DiceSides = 12, BaseApCost = 4, HitBonus = 0, PenBonus = 2, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.MilitaryHammer, new WeaponConfig { Name = "军用锤", DiceCount = 2, DiceSides = 8, BaseApCost = 5, HitBonus = 0, PenBonus = 2, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Flail, new WeaponConfig { Name = "连枷", DiceCount = 1, DiceSides = 10, BaseApCost = 4, HitBonus = +1, PenBonus = 2, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        // Heavy
        { WeaponData.WeaponSubtype.Maul, new WeaponConfig { Name = "大锤", DiceCount = 3, DiceSides = 8, BaseApCost = 7, HitBonus = 0, PenBonus = 3, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Greatclub, new WeaponConfig { Name = "巨型木棒", DiceCount = 2, DiceSides = 12, BaseApCost = 7, HitBonus = 0, PenBonus = 3, Range = 1, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.Polehammer, new WeaponConfig { Name = "长柄战锤", DiceCount = 2, DiceSides = 10, BaseApCost = 7, HitBonus = +1, PenBonus = 3, Range = 2, DamageType = WeaponData.DamageType.Crush }},

        // ============================================================================
        // 远程 Ranged — 7.4 Thrown (投掷)
        // ============================================================================
        // Light
        { WeaponData.WeaponSubtype.ThrowingKnife, new WeaponConfig { Name = "飞刀", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, PenBonus = 0, Range = 4, DamageType = WeaponData.DamageType.Slash }},
        { WeaponData.WeaponSubtype.Dart, new WeaponConfig { Name = "飞镖", DiceCount = 1, DiceSides = 4, BaseApCost = 2, HitBonus = 0, PenBonus = 2, Range = 5, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Francisca, new WeaponConfig { Name = "弗兰西斯卡飞斧", DiceCount = 1, DiceSides = 6, BaseApCost = 3, HitBonus = +1, PenBonus = 1, Range = 3, DamageType = WeaponData.DamageType.Slash }},
        // Medium
        { WeaponData.WeaponSubtype.Javelin, new WeaponConfig { Name = "标枪", DiceCount = 1, DiceSides = 8, BaseApCost = 4, HitBonus = 0, PenBonus = 3, Range = 6, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Pilum, new WeaponConfig { Name = "重标枪", DiceCount = 1, DiceSides = 10, BaseApCost = 5, HitBonus = 0, PenBonus = 4, Range = 5, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Harpoon, new WeaponConfig { Name = "渔叉", DiceCount = 1, DiceSides = 4, BaseApCost = 4, HitBonus = +1, PenBonus = 3, Range = 4, DamageType = WeaponData.DamageType.Pierce }},
        // Heavy
        { WeaponData.WeaponSubtype.StoneThrow, new WeaponConfig { Name = "投石", DiceCount = 1, DiceSides = 4, BaseApCost = 5, HitBonus = 0, PenBonus = 1, Range = 6, DamageType = WeaponData.DamageType.Crush }},
        { WeaponData.WeaponSubtype.HeavyJavelin, new WeaponConfig { Name = "重标枪", DiceCount = 1, DiceSides = 12, BaseApCost = 6, HitBonus = 0, PenBonus = 4, Range = 4, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.ThrowingHammer, new WeaponConfig { Name = "投掷飞锤", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = +1, PenBonus = 2, Range = 3, DamageType = WeaponData.DamageType.Crush }},

        // ============================================================================
        // 远程 Ranged — 7.5 Bows (弓, all Pierce)
        // ============================================================================
        // Light
        { WeaponData.WeaponSubtype.Shortbow, new WeaponConfig { Name = "短弓", DiceCount = 1, DiceSides = 6, BaseApCost = 4, HitBonus = 0, PenBonus = 1, Range = 8, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.HuntingBow, new WeaponConfig { Name = "狩猎弓", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = 0, PenBonus = 1, Range = 10, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.NomadBow, new WeaponConfig { Name = "游牧弓", DiceCount = 1, DiceSides = 4, BaseApCost = 4, HitBonus = +1, PenBonus = 1, Range = 7, DamageType = WeaponData.DamageType.Pierce }},
        // Medium
        { WeaponData.WeaponSubtype.Strongbow, new WeaponConfig { Name = "强弓", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = 0, PenBonus = 2, Range = 10, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.RecurveBow, new WeaponConfig { Name = "反曲弓", DiceCount = 1, DiceSides = 10, BaseApCost = 6, HitBonus = 0, PenBonus = 2, Range = 12, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.WarBow, new WeaponConfig { Name = "战弓", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = +1, PenBonus = 2, Range = 11, DamageType = WeaponData.DamageType.Pierce }},
        // Heavy
        { WeaponData.WeaponSubtype.Longbow, new WeaponConfig { Name = "长弓", DiceCount = 1, DiceSides = 10, BaseApCost = 7, HitBonus = 0, PenBonus = 3, Range = 15, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.CompositeLongbow, new WeaponConfig { Name = "复合长弓", DiceCount = 2, DiceSides = 6, BaseApCost = 8, HitBonus = 0, PenBonus = 3, Range = 14, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Greatbow, new WeaponConfig { Name = "巨弓", DiceCount = 1, DiceSides = 10, BaseApCost = 7, HitBonus = +1, PenBonus = 3, Range = 13, DamageType = WeaponData.DamageType.Pierce }},

        // ============================================================================
        // 远程 Ranged — 7.6 Crossbows (弩, all Pierce, have ReloadAp)
        // ============================================================================
        // Light
        { WeaponData.WeaponSubtype.PistolCrossbow, new WeaponConfig { Name = "手弩", DiceCount = 1, DiceSides = 4, BaseApCost = 5, HitBonus = +1, PenBonus = 2, Range = 6, ReloadAp = 2, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.LightCrossbow, new WeaponConfig { Name = "轻弩", DiceCount = 1, DiceSides = 8, BaseApCost = 5, HitBonus = 0, PenBonus = 3, Range = 10, ReloadAp = 4, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.HuntingCrossbow, new WeaponConfig { Name = "猎弩", DiceCount = 1, DiceSides = 10, BaseApCost = 5, HitBonus = 0, PenBonus = 3, Range = 12, ReloadAp = 3, DamageType = WeaponData.DamageType.Pierce }},
        // Medium
        { WeaponData.WeaponSubtype.StandardCrossbow, new WeaponConfig { Name = "标准弩", DiceCount = 1, DiceSides = 10, BaseApCost = 6, HitBonus = 0, PenBonus = 3, Range = 12, ReloadAp = 5, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.StrongCrossbow, new WeaponConfig { Name = "强弩", DiceCount = 1, DiceSides = 12, BaseApCost = 7, HitBonus = 0, PenBonus = 4, Range = 11, ReloadAp = 6, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.SniperCrossbow, new WeaponConfig { Name = "狙击弩", DiceCount = 1, DiceSides = 8, BaseApCost = 6, HitBonus = +1, PenBonus = 4, Range = 16, ReloadAp = 7, DamageType = WeaponData.DamageType.Pierce }},
        // Heavy
        { WeaponData.WeaponSubtype.HeavyCrossbow, new WeaponConfig { Name = "重弩", DiceCount = 2, DiceSides = 6, BaseApCost = 8, HitBonus = 0, PenBonus = 4, Range = 14, ReloadAp = 8, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.SiegeCrossbow, new WeaponConfig { Name = "攻城弩", DiceCount = 2, DiceSides = 8, BaseApCost = 9, HitBonus = 0, PenBonus = 5, Range = 13, ReloadAp = 10, DamageType = WeaponData.DamageType.Pierce }},
        { WeaponData.WeaponSubtype.Ballista, new WeaponConfig { Name = "床弩", DiceCount = 1, DiceSides = 12, BaseApCost = 8, HitBonus = +1, PenBonus = 5, Range = 18, ReloadAp = 12, DamageType = WeaponData.DamageType.Pierce }},

        // ============================================================================
        // 特殊
        // ============================================================================
        { WeaponData.WeaponSubtype.Unarmed, new WeaponConfig { Name = "赤手空拳", DiceCount = 1, DiceSides = 3, BaseApCost = 2, HitBonus = 0, PenBonus = 0, Range = 1, DamageType = WeaponData.DamageType.Crush }}
    };

    public static WeaponConfig GetConfig(WeaponData.WeaponSubtype subtype)
    {
        if (Registry.TryGetValue(subtype, out var config)) return config;
        return Registry[WeaponData.WeaponSubtype.Unarmed];
    }
}
