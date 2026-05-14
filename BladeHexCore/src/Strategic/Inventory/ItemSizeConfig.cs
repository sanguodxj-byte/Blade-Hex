// ItemSizeConfig.cs
// 物品默认背包格尺寸配置
// 类似暗黑破坏神2：武器高而窄，护甲宽而高，消耗品小巧
// 当 ItemData.InvWidth/InvHeight 未手动设置时，可用此配置推断默认尺寸
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 物品背包格尺寸配置工具
/// 提供基于物品类型的默认尺寸推断
/// </summary>
public static class ItemSizeConfig
{
    /// <summary>
    /// 获取物品的推荐背包尺寸（宽×高）
    /// 如果物品已手动设置了 InvWidth/InvHeight（非默认1×1），则直接返回
    /// </summary>
    public static (int Width, int Height) GetRecommendedSize(ItemData item)
    {
        // 如果已手动配置（非默认值），直接使用
        if (item.InvWidth > 1 || item.InvHeight > 1)
            return (item.InvWidth, item.InvHeight);

        return item switch
        {
            WeaponData weapon => GetWeaponSize(weapon),
            ArmorData armor => GetArmorSize(armor),
            ConsumableData => (1, 1),       // 消耗品：1×1 小巧
            AccessoryData => (1, 1),        // 饰品：1×1
            _ => (1, 1),                    // 默认材料等：1×1
        };
    }

    /// <summary>
    /// 将推荐尺寸应用到物品上（用于物品生成时）
    /// </summary>
    public static void ApplyRecommendedSize(ItemData item)
    {
        var (w, h) = GetRecommendedSize(item);
        item.InvWidth = w;
        item.InvHeight = h;
    }

    private static (int Width, int Height) GetWeaponSize(WeaponData weapon)
    {
        // 双手武器：1×4（长而窄，如巨剑/长枪/长弓）
        if (weapon.IsTwoHanded)
            return (1, 4);

        // 远程武器（非双手）：1×3
        if (weapon.IsRanged)
            return (1, 3);

        // 根据重量分类
        return weapon.Weight switch
        {
            WeaponData.WeightCategory.Light => (1, 2),   // 匕首/短剑：1×2
            WeaponData.WeightCategory.Medium => (1, 3),  // 长剑/战锤：1×3
            WeaponData.WeightCategory.Heavy => (2, 3),   // 巨斧等：2×3
            _ => (1, 3),
        };
    }

    private static (int Width, int Height) GetArmorSize(ArmorData armor)
    {
        return armor.armorType switch
        {
            ArmorData.ArmorType.Light => (2, 2),    // 轻甲：2×2
            ArmorData.ArmorType.Medium => (2, 3),   // 中甲：2×3
            ArmorData.ArmorType.Heavy => (2, 3),    // 重甲：2×3
            ArmorData.ArmorType.Shield => (2, 2),   // 盾牌：2×2
            _ => (2, 2),
        };
    }
}
