// ItemDataValidator.cs
// 启动时跨文件一致性校验
// 由 ItemDataLoader 在所有 JSON 加载完毕后调用
//
// 检查项：
//   1. ID 全局唯一性（武器/护甲/消耗品/箭筒/饰品 ID 不可重复）
//   2. 武器子类型必须在 WeaponRegistry 中注册
//   3. 价格合理（>0）
//   4. 名称非空
//
// 错误处理：累积所有错误后一次性报告，避免开发者反复改一处看一处
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Data;

/// <summary>
/// 物品数据一致性校验器。
/// </summary>
public static class ItemDataValidator
{
    /// <summary>
    /// 执行全部校验。返回错误数量。
    /// 所有错误通过 GD.PushError 输出。
    /// </summary>
    public static int Validate()
    {
        var errors = new List<string>();

        ValidateUniqueIds(errors);
        ValidateWeapons(errors);
        ValidateArmors(errors);
        ValidateConsumables(errors);
        ValidateAccessories(errors);
        ValidateQuivers(errors);

        if (errors.Count > 0)
        {
            GD.PushError($"[ItemDataValidator] {errors.Count} validation errors:");
            foreach (var err in errors)
                GD.PushError($"  • {err}");
        }
        else
        {
            GD.Print("[ItemDataValidator] ✓ All item data passed validation");
        }

        return errors.Count;
    }

    // ========================================
    // 跨类型 ID 唯一性
    // ========================================

    private static void ValidateUniqueIds(List<string> errors)
    {
        var seen = new Dictionary<string, string>(); // id → category

        foreach (var id in ItemDataLoader.GetWeapons().Keys) Track(id, "weapon");
        foreach (var id in ItemDataLoader.GetArmors().Keys) Track(id, "armor");
        foreach (var id in ItemDataLoader.GetConsumables().Keys) Track(id, "consumable");
        foreach (var id in ItemDataLoader.GetQuivers().Keys) Track(id, "quiver");
        foreach (var id in ItemDataLoader.GetAccessories().Keys) Track(id, "accessory");

        void Track(string id, string category)
        {
            if (seen.TryGetValue(id, out var prev))
                errors.Add($"Duplicate ID '{id}' across categories: {prev} and {category}");
            else
                seen[id] = category;
        }
    }

    // ========================================
    // 武器
    // ========================================

    private static void ValidateWeapons(List<string> errors)
    {
        foreach (var (id, w) in ItemDataLoader.GetWeapons())
        {
            string ctx = $"weapon '{id}'";

            if (string.IsNullOrEmpty(w.ItemName)) errors.Add($"{ctx}: missing name");
            if (w.Price < 1) errors.Add($"{ctx}: price must be >= 1 (got {w.Price})");

            // 子类型必须在 WeaponRegistry 中注册
            try
            {
                var cfg = WeaponRegistry.GetConfig(w.Subtype);
                if (string.IsNullOrEmpty(cfg.Name))
                    errors.Add($"{ctx}: subtype {w.Subtype} not registered in WeaponRegistry");
            }
            catch (System.Exception ex)
            {
                errors.Add($"{ctx}: WeaponRegistry lookup failed for {w.Subtype}: {ex.Message}");
            }

            // 投掷武器现在已允许同时为 ranged=true（共享远程武器基类行为）

            // 双手武器不应可双持
            if (w.IsTwoHanded && w.IsDualWieldable)
                errors.Add($"{ctx}: two_handed weapon cannot be dual_wield");

            if (w.Tier < 1 || w.Tier > 5)
                errors.Add($"{ctx}: tier must be 1-5 (got {w.Tier})");
        }
    }

    // ========================================
    // 护甲
    // ========================================

    private static void ValidateArmors(List<string> errors)
    {
        foreach (var (id, a) in ItemDataLoader.GetArmors())
        {
            string ctx = $"armor '{id}'";

            if (string.IsNullOrEmpty(a.ItemName)) errors.Add($"{ctx}: missing name");
            if (a.Price < 1) errors.Add($"{ctx}: price must be >= 1 (got {a.Price})");

            if (a.DrThreshold < 0)
                errors.Add($"{ctx}: dr cannot be negative (got {a.DrThreshold})");

            if (a.MaxDexBonus < 0)
                errors.Add($"{ctx}: max_dex cannot be negative");

            // 盾牌应该装备到 Body 槽（手持，不是头盔）
            if (a.armorType == ArmorData.ArmorType.Shield && a.EquipSlotTarget == ItemData.EquipSlot.Helmet)
                errors.Add($"{ctx}: shield cannot be in Helmet slot");
        }
    }

    // ========================================
    // 消耗品
    // ========================================

    private static void ValidateConsumables(List<string> errors)
    {
        foreach (var (id, c) in ItemDataLoader.GetConsumables())
        {
            string ctx = $"consumable '{id}'";
            if (string.IsNullOrEmpty(c.ItemName)) errors.Add($"{ctx}: missing name");
            if (c.Price < 1) errors.Add($"{ctx}: price must be >= 1");
        }
    }

    // ========================================
    // 饰品
    // ========================================

    private static void ValidateAccessories(List<string> errors)
    {
        foreach (var (id, a) in ItemDataLoader.GetAccessories())
        {
            string ctx = $"accessory '{id}'";
            if (string.IsNullOrEmpty(a.ItemName)) errors.Add($"{ctx}: missing name");
            if (a.Price < 1) errors.Add($"{ctx}: price must be >= 1");

            // 至少有一项加成
            bool hasAnyBonus = a.StrBonus != 0 || a.DexBonus != 0 || a.ConBonus != 0
                || a.IntBonus != 0 || a.WisBonus != 0 || a.ChaBonus != 0
                || a.HpBonus != 0 || a.AcBonus != 0 || a.MoveBonus != 0 || a.InitiativeBonus != 0
                || !string.IsNullOrEmpty(a.Resistance) || !string.IsNullOrEmpty(a.Immunity)
                || !string.IsNullOrEmpty(a.SpecialEffect);
            if (!hasAnyBonus)
                errors.Add($"{ctx}: has no bonuses or effects (likely a JSON error)");
        }
    }

    // ========================================
    // 箭筒
    // ========================================

    private static void ValidateQuivers(List<string> errors)
    {
        foreach (var (id, q) in ItemDataLoader.GetQuivers())
        {
            string ctx = $"quiver '{id}'";
            if (string.IsNullOrEmpty(q.ItemName)) errors.Add($"{ctx}: missing name");
            if (q.Price < 1) errors.Add($"{ctx}: price must be >= 1");
        }
    }
}
