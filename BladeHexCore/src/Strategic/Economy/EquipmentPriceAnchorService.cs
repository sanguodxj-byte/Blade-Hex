// EquipmentPriceAnchorService.cs
// 装备价格锚定服务：用当前经济模型的可支配金币来推导装备价格带。
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Strategic.Economy;

/// <summary>
/// 经济锚点。
///
/// 这里的核心不是“玩家总收入”，而是“扣除工资、口粮、基础设施消费后的可支配金币”。
/// 装备价格应该主要吃掉这部分可支配金币，否则会出现两种问题：
/// 1. 装备太便宜：玩家每两三单任务就换完整套，成长过快；
/// 2. 装备太贵：玩家只能刷钱，装备成长失去节奏。
/// </summary>
public sealed class EconomyPriceAnchor
{
    /// <summary>普通委托平均奖励。</summary>
    public int AverageQuestReward { get; init; } = 90;

    /// <summary>平均几天完成一单普通委托。</summary>
    public int QuestIntervalDays { get; init; } = 3;

    /// <summary>口粮单价，用于记录模拟假设。</summary>
    public int FoodUnitPrice { get; init; } = 4;

    /// <summary>修正口粮模型后，雇佣兵小队日均可支配金币。</summary>
    public double SustainableNetGoldPerDay { get; init; } = 12.0;

    /// <summary>每单普通委托周期内的可支配金币。</summary>
    public double DiscretionaryGoldPerQuest => SustainableNetGoldPerDay * QuestIntervalDays;
}

/// <summary>物品经济分类。该分类不要求 JSON 立即新增字段，由锚定服务从现有数据推导。</summary>
public enum ItemEconomyCategory
{
    StarterGear,
    CommonWeapon,
    MilitaryWeapon,
    EliteWeapon,
    SiegeWeapon,
    Catalyst,
    BodyArmor,
    Shield,
    Helmet,
    PartArmor,
    Quiver,
    Consumable,
    Supply,
    Accessory,
    Misc,
}

/// <summary>装备价格带。</summary>
public sealed class EquipmentPriceBand
{
    public int Min { get; init; }
    public int Target { get; init; }
    public int Max { get; init; }
    public string TierLabel { get; init; } = "";
    public string Rationale { get; init; } = "";

    public bool Contains(int price) => price >= Min && price <= Max;
}

/// <summary>单个物品的价格评估结果。</summary>
public sealed class EquipmentPriceEvaluation
{
    public string ItemId { get; init; } = "";
    public string ItemName { get; init; } = "";
    public int CurrentPrice { get; init; }
    public ItemEconomyCategory Category { get; init; }
    public EquipmentPriceBand Band { get; init; } = new();
    public int SuggestedPrice => Band.Target;
    public int DeltaFromTarget => CurrentPrice - Band.Target;
    public double RatioToTarget => Band.Target <= 0 ? 1.0 : (double)CurrentPrice / Band.Target;
    public bool InBand => Band.Contains(CurrentPrice);

    /// <summary>目标价约等于几个普通委托周期的可支配金币。</summary>
    public double QuestCyclesToAfford(EconomyPriceAnchor anchor)
    {
        double perQuest = Math.Max(1.0, anchor.DiscretionaryGoldPerQuest);
        return SuggestedPrice / perQuest;
    }
}

/// <summary>
/// 装备价格锚定服务。
///
/// 服务只计算“建议价格带”，不直接改 JSON。这样可以用于：
/// - 数值评审报告；
/// - 市场动态定价；
/// - 后续批量重写 JSON 前的安全对比。
/// </summary>
public static class EquipmentPriceAnchorService
{
    /// <summary>
    /// 根据当前经济模型创建默认锚点。
    /// 默认使用“修正口粮/只按小时扣粮”模型，因为当前双扣模型已被模拟证明会过早饥饿。
    /// </summary>
    public static EconomyPriceAnchor CreateDefaultAnchor(int averageQuestReward = 90, int foodUnitPrice = 4)
        => EconomySimulation.CreatePriceAnchorFromModel(averageQuestReward, foodUnitPrice);

    /// <summary>计算物品经济分类。</summary>
    public static ItemEconomyCategory Classify(ItemData item)
    {
        if (item is WeaponData weapon) return ClassifyWeapon(weapon);
        if (item is ArmorData armor) return ClassifyArmor(armor);
        if (item is AccessoryData) return ItemEconomyCategory.Accessory;
        if (IsQuiverItem(item)) return ItemEconomyCategory.Quiver;
        if (item is ConsumableData) return IsSupply(item) ? ItemEconomyCategory.Supply : ItemEconomyCategory.Consumable;
        return ItemEconomyCategory.Misc;
    }

    /// <summary>计算单个物品的建议价格带。</summary>
    public static EquipmentPriceBand GetPriceBand(ItemData item, EconomyPriceAnchor anchor)
    {
        if (item is WeaponData weapon) return GetWeaponBand(weapon, anchor);
        if (item is ArmorData armor) return GetArmorBand(armor, anchor);
        if (item is AccessoryData accessory) return GetAccessoryBand(accessory, anchor);
        return GetGenericBand(item, anchor);
    }

    /// <summary>计算单个物品的建议基准价。</summary>
    public static int GetSuggestedPrice(ItemData item, EconomyPriceAnchor anchor)
        => GetPriceBand(item, anchor).Target;

    /// <summary>评估单个物品当前价格是否落在建议价格带内。</summary>
    public static EquipmentPriceEvaluation Evaluate(ItemData item, EconomyPriceAnchor anchor)
    {
        var band = GetPriceBand(item, anchor);
        return new EquipmentPriceEvaluation
        {
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            CurrentPrice = item.Price,
            Category = Classify(item),
            Band = band,
        };
    }

    /// <summary>评估全部基础装备与物品。</summary>
    public static List<EquipmentPriceEvaluation> EvaluateAll(EconomyPriceAnchor anchor)
    {
        var items = new List<ItemData>();
        items.AddRange(PrototypeData.GetWeapons().Values);
        items.AddRange(PrototypeData.GetArmors().Values);
        items.AddRange(PrototypeData.GetQuivers().Values);
        items.AddRange(PrototypeData.GetConsumables().Values);
        items.AddRange(PrototypeData.GetAccessories().Values);

        return items
            .Select(item => Evaluate(item, anchor))
            .OrderBy(e => e.Category)
            .ThenBy(e => e.ItemId)
            .ToList();
    }

    /// <summary>生成紧凑文本报告，供 headless sim 输出。</summary>
    public static List<string> FormatPriceReport(IEnumerable<EquipmentPriceEvaluation> evaluations, int maxOutliers = 12)
    {
        var list = evaluations.ToList();
        int inBand = list.Count(e => e.InBand);
        var lines = new List<string>
        {
            "=== 装备价格锚定报告 ===",
            $"物品总数：{list.Count}，落在建议价格带：{inBand}/{list.Count}",
            "分类覆盖：" + string.Join("，", list.GroupBy(e => e.Category).OrderBy(g => g.Key).Select(g => $"{CategoryLabel(g.Key)}={g.Count()}")),
        };

        var outliers = list
            .Where(e => !e.InBand)
            .OrderByDescending(e => Math.Abs(e.RatioToTarget - 1.0))
            .Take(maxOutliers)
            .ToList();

        if (outliers.Count == 0)
        {
            lines.Add("所有物品都位于建议价格带内。");
            return lines;
        }

        lines.Add("偏离最大的物品：");
        lines.Add("id                       | cat      | price | target | band       | ratio | note");
        foreach (var e in outliers)
        {
            lines.Add($"{Trim(e.ItemId, 24),-24} | {Trim(CategoryLabel(e.Category), 8),-8} | {e.CurrentPrice,5} | {e.Band.Target,6} | {e.Band.Min,4}-{e.Band.Max,-4} | {e.RatioToTarget,5:F2} | {e.Band.TierLabel}");
        }

        return lines;
    }

    /// <summary>生成完整模拟价格表，可直接保存到报告或 CSV 前转 markdown。</summary>
    public static List<string> FormatFullPriceTable(IEnumerable<EquipmentPriceEvaluation> evaluations, EconomyPriceAnchor anchor)
    {
        var lines = new List<string>
        {
            "| 分类 | ID | 名称 | 当前价 | 模拟价 | 建议区间 | 当前/模拟 | 任务周期 | 说明 |",
            "|---|---|---|---:|---:|---:|---:|---:|---|",
        };

        foreach (var e in evaluations.OrderBy(e => e.Category).ThenBy(e => e.SuggestedPrice).ThenBy(e => e.ItemId))
        {
            lines.Add($"| {CategoryLabel(e.Category)} | `{e.ItemId}` | {EscapePipe(e.ItemName)} | {e.CurrentPrice} | {e.SuggestedPrice} | {e.Band.Min}-{e.Band.Max} | {e.RatioToTarget:F2} | {e.QuestCyclesToAfford(anchor):F1} | {EscapePipe(e.Band.TierLabel)} |");
        }

        return lines;
    }

    public static string CategoryLabel(ItemEconomyCategory category) => category switch
    {
        ItemEconomyCategory.StarterGear => "起步装备",
        ItemEconomyCategory.CommonWeapon => "民用武器",
        ItemEconomyCategory.MilitaryWeapon => "军用武器",
        ItemEconomyCategory.EliteWeapon => "精英武器",
        ItemEconomyCategory.SiegeWeapon => "攻城器械",
        ItemEconomyCategory.Catalyst => "施法触媒",
        ItemEconomyCategory.BodyArmor => "身甲",
        ItemEconomyCategory.Shield => "盾牌",
        ItemEconomyCategory.Helmet => "头盔",
        ItemEconomyCategory.PartArmor => "部位护具",
        ItemEconomyCategory.Quiver => "箭筒",
        ItemEconomyCategory.Consumable => "消耗品",
        ItemEconomyCategory.Supply => "补给",
        ItemEconomyCategory.Accessory => "饰品",
        _ => "杂项",
    };

    private static EquipmentPriceBand GetWeaponBand(WeaponData weapon, EconomyPriceAnchor anchor)
    {
        double perQuest = Math.Max(1.0, anchor.DiscretionaryGoldPerQuest);
        var category = ClassifyWeapon(weapon);
        double cycles = WeaponQuestCycles(weapon, category);
        double target = perQuest * cycles;

        string label = $"{CategoryLabel(category)}T{weapon.Tier}";
        if (weapon.IsRanged) label += "/远程";
        if (weapon.IsThrowing) label += "/投掷";
        if (weapon.IsTwoHanded) label += "/双手";
        if (weapon.IsCatalyst) label += "/触媒";

        int floor = category == ItemEconomyCategory.StarterGear ? 2 : 10;
        return BuildBand(target, label, "武器价格按经济分类、Tier、定位与可支配任务周期推导。", floor, 0.55, 1.75);
    }

    private static EquipmentPriceBand GetArmorBand(ArmorData armor, EconomyPriceAnchor anchor)
    {
        double net = Math.Max(1.0, anchor.SustainableNetGoldPerDay);
        double perQuest = Math.Max(1.0, anchor.DiscretionaryGoldPerQuest);
        double target;
        string label;

        if (armor.armorType == ArmorData.ArmorType.Shield)
        {
            target = net * (2.0 + armor.DrThreshold * 1.7 + Math.Max(0, 1.0 - armor.RangedDamageMultiplier) * 18.0);
            label = "盾牌";
        }
        else if (armor.EquipSlotTarget == ItemData.EquipSlot.Helmet)
        {
            target = net * (1.5 + armor.DrThreshold * 1.9);
            label = "头盔";
        }
        else if (armor.EquipSlotTarget == ItemData.EquipSlot.Feet || armor.EquipSlotTarget == ItemData.EquipSlot.Hands)
        {
            target = net * (1.2 + armor.DrThreshold * 1.6);
            label = "部位护具";
        }
        else if (armor.DrThreshold <= 0)
        {
            target = perQuest * 0.75;
            label = "起步身甲";
        }
        else
        {
            target = net * (armor.armorType switch
            {
                ArmorData.ArmorType.Light => 3.0 + armor.DrThreshold * 2.2,
                ArmorData.ArmorType.Medium => 5.0 + armor.DrThreshold * 2.8,
                ArmorData.ArmorType.Heavy => 8.0 + armor.DrThreshold * 4.2,
                _ => 3.0 + armor.DrThreshold * 2.0,
            });
            label = $"{armor.GetArmorTypeName()}身甲";
        }

        return BuildBand(target, label, "防具价格按可支配金币天数、DR 阈值与部位价值推导。", minFloor: 5);
    }

    private static EquipmentPriceBand GetAccessoryBand(AccessoryData accessory, EconomyPriceAnchor anchor)
    {
        double perQuest = Math.Max(1.0, anchor.DiscretionaryGoldPerQuest);
        int statScore = Math.Abs(accessory.StrBonus) + Math.Abs(accessory.DexBonus) + Math.Abs(accessory.ConBonus)
            + Math.Abs(accessory.IntBonus) + Math.Abs(accessory.WisBonus) + Math.Abs(accessory.ChaBonus);
        int combatScore = accessory.HpBonus / 2 + accessory.AcBonus * 4 + accessory.MoveBonus * 2 + accessory.InitiativeBonus;
        if (!string.IsNullOrEmpty(accessory.Resistance)) combatScore += 3;
        if (!string.IsNullOrEmpty(accessory.Immunity)) combatScore += 6;
        if (!string.IsNullOrEmpty(accessory.SpecialEffect)) combatScore += 4;

        double rarityMultiplier = accessory.ItemRarity switch
        {
            ItemData.Rarity.Uncommon => 1.20,
            ItemData.Rarity.Rare => 1.80,
            ItemData.Rarity.Epic => 3.00,
            ItemData.Rarity.Legendary => 5.00,
            _ => 1.00,
        };
        double cycles = Math.Max(2.0, 1.5 + statScore * 0.9 + combatScore * 0.7) * rarityMultiplier;
        return BuildBand(perQuest * cycles, $"饰品/{accessory.GetRarityName()}", "饰品价格按属性加成、战斗效果与稀有度推导。", 25, 0.60, 1.90);
    }

    private static EquipmentPriceBand GetGenericBand(ItemData item, EconomyPriceAnchor anchor)
    {
        double net = Math.Max(1.0, anchor.SustainableNetGoldPerDay);
        double target;
        string label;

        if (IsSupply(item))
        {
            // rations 当前描述为“恢复5食物”，与经济模拟的口粮单价对齐。
            target = Math.Max(item.Price, anchor.FoodUnitPrice * 5.0);
            label = "补给";
        }
        else if (IsQuiverItem(item))
        {
            target = net * (1.5 + Math.Max(0, item.QuiverDamageBonus) * 0.85);
            label = "箭筒";
        }
        else if (item is ConsumableData)
        {
            target = Math.Max(item.Price, net * 1.0);
            label = "消耗品";
        }
        else
        {
            target = Math.Max(item.Price, net * 1.5);
            label = "通用物品";
        }

        return BuildBand(target, label, "通用物品按低额日可支配金币、补给单价或原始稀缺度推导。", minFloor: 1);
    }

    private static ItemEconomyCategory ClassifyWeapon(WeaponData weapon)
    {
        if (weapon.IsCatalyst) return ItemEconomyCategory.Catalyst;
        if (weapon.Subtype is WeaponData.WeaponSubtype.SiegeCrossbow or WeaponData.WeaponSubtype.Ballista)
            return ItemEconomyCategory.SiegeWeapon;
        if (weapon.Subtype is WeaponData.WeaponSubtype.Dagger or WeaponData.WeaponSubtype.Stiletto or WeaponData.WeaponSubtype.Club
            or WeaponData.WeaponSubtype.ThrowingKnife or WeaponData.WeaponSubtype.Dart or WeaponData.WeaponSubtype.StoneThrow
            or WeaponData.WeaponSubtype.Javelin or WeaponData.WeaponSubtype.Harpoon or WeaponData.WeaponSubtype.Shortbow)
            return ItemEconomyCategory.StarterGear;
        if (weapon.Subtype is WeaponData.WeaponSubtype.Greatsword or WeaponData.WeaponSubtype.GreatAxe or WeaponData.WeaponSubtype.Glaive
            or WeaponData.WeaponSubtype.Lance or WeaponData.WeaponSubtype.Voulge or WeaponData.WeaponSubtype.Trident
            or WeaponData.WeaponSubtype.Maul or WeaponData.WeaponSubtype.Greatclub or WeaponData.WeaponSubtype.Polehammer
            or WeaponData.WeaponSubtype.RecurveBow or WeaponData.WeaponSubtype.Longbow or WeaponData.WeaponSubtype.CompositeLongbow
            or WeaponData.WeaponSubtype.Greatbow or WeaponData.WeaponSubtype.HeavyCrossbow or WeaponData.WeaponSubtype.SniperCrossbow)
            return ItemEconomyCategory.EliteWeapon;
        return ItemEconomyCategory.MilitaryWeapon;
    }

    private static ItemEconomyCategory ClassifyArmor(ArmorData armor)
    {
        if (armor.armorType == ArmorData.ArmorType.Shield) return ItemEconomyCategory.Shield;
        if (armor.EquipSlotTarget == ItemData.EquipSlot.Helmet) return ItemEconomyCategory.Helmet;
        if (armor.EquipSlotTarget == ItemData.EquipSlot.Feet || armor.EquipSlotTarget == ItemData.EquipSlot.Hands) return ItemEconomyCategory.PartArmor;
        if (armor.DrThreshold <= 0) return ItemEconomyCategory.StarterGear;
        return ItemEconomyCategory.BodyArmor;
    }

    private static double WeaponQuestCycles(WeaponData weapon, ItemEconomyCategory category)
    {
        double cycles = category switch
        {
            ItemEconomyCategory.StarterGear => weapon.Tier switch { <= 1 => 0.85, 2 => 3.0, _ => 7.0 },
            ItemEconomyCategory.CommonWeapon => weapon.Tier switch { <= 1 => 1.8, 2 => 5.0, _ => 12.0 },
            ItemEconomyCategory.MilitaryWeapon => weapon.Tier switch { <= 1 => 2.5, 2 => 5.7, _ => 13.2 },
            ItemEconomyCategory.EliteWeapon => weapon.Tier switch { <= 1 => 4.5, 2 => 9.0, _ => 18.0 },
            ItemEconomyCategory.SiegeWeapon => weapon.Tier switch { <= 1 => 12.0, 2 => 24.0, _ => 48.0 },
            ItemEconomyCategory.Catalyst => weapon.Tier switch { <= 1 => 1.3, 2 => 3.0, _ => 7.0 },
            _ => weapon.Tier switch { <= 1 => 2.0, 2 => 5.0, _ => 12.0 },
        };

        if (weapon.IsThrowing) cycles *= weapon.Tier <= 1 ? 0.85 : 0.95;
        if (weapon.IsTwoHanded && category != ItemEconomyCategory.SiegeWeapon) cycles *= 1.10;
        if (weapon.Weight == WeaponData.WeightCategory.Light && category != ItemEconomyCategory.StarterGear) cycles *= 0.85;
        if (weapon.Weight == WeaponData.WeightCategory.Heavy) cycles *= 1.10;
        if (weapon.Subtype == WeaponData.WeaponSubtype.Ballista) cycles *= 1.15;
        return cycles;
    }

    private static EquipmentPriceBand BuildBand(double rawTarget, string label, string rationale, int minFloor, double minFactor = 0.55, double maxFactor = 1.75)
    {
        int target = Math.Max(minFloor, RoundToEconomyStep(rawTarget));
        int min = Math.Max(minFloor, RoundToEconomyStep(target * minFactor));
        int max = Math.Max(min, RoundToEconomyStep(target * maxFactor));
        return new EquipmentPriceBand
        {
            Min = min,
            Target = target,
            Max = max,
            TierLabel = label,
            Rationale = rationale,
        };
    }

    private static int RoundToEconomyStep(double value)
    {
        if (value < 20) return Math.Max(1, (int)Math.Round(value));
        if (value < 100) return (int)(Math.Round(value / 5.0) * 5);
        if (value < 500) return (int)(Math.Round(value / 10.0) * 10);
        return (int)(Math.Round(value / 25.0) * 25);
    }

    private static bool IsSupply(ItemData item)
        => item.ItemId.Contains("ration", StringComparison.OrdinalIgnoreCase)
            || item.ItemId.Contains("food", StringComparison.OrdinalIgnoreCase)
            || item.ItemName.Contains("粮", StringComparison.OrdinalIgnoreCase)
            || item.ItemName.Contains("食物", StringComparison.OrdinalIgnoreCase);

    private static bool IsQuiverItem(ItemData item)
        => item.IsQuiver
            || item.ItemId.Contains("quiver", StringComparison.OrdinalIgnoreCase);

    private static string Trim(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return text[..Math.Max(0, maxChars - 1)] + "…";
    }

    private static string EscapePipe(string text) => text.Replace("|", "\\|");
}
