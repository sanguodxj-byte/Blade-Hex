// OverworldConsumableHandler.cs
// 战斗外消耗品处理器 — 大地图使用消耗品
// T11: Combat-out consumable menu
using Godot;
using BladeHex.Data;

namespace BladeHex.Strategic.Consumables;

/// <summary>
/// 消耗品使用结果
/// </summary>
public record ConsumableUseResult(
    bool Success,
    string Message,
    int HealedHp = 0,
    string? RemovedStatus = null
);

/// <summary>
/// 战斗外消耗品处理器 — 处理大地图使用消耗品的逻辑
/// </summary>
public static class OverworldConsumableHandler
{
    /// <summary>检查消耗品是否可以在战斗外使用</summary>
    public static bool CanUseOutsideCombat(ConsumableData item)
    {
        return item.UsableOutsideCombat;
    }

    /// <summary>检查消耗品是否可以对目标使用</summary>
    public static bool CanUseOnTarget(ConsumableData item, UnitData target)
    {
        if (!CanUseOutsideCombat(item))
            return false;

        return item.consumableType switch
        {
            ConsumableData.ConsumableType.HealingPotion => target.Runtime.CurrentHp < Combat.CombatStats.GetMaxHp(target),
            ConsumableData.ConsumableType.StrongHealing => target.Runtime.CurrentHp < Combat.CombatStats.GetMaxHp(target),
            ConsumableData.ConsumableType.Antidote => true, // 可以预防性使用
            ConsumableData.ConsumableType.Whetstone => true, // 战斗外使用后进入战斗生效
            _ => false,
        };
    }

    /// <summary>使用消耗品</summary>
    public static ConsumableUseResult Use(ConsumableData item, UnitData target)
    {
        if (!CanUseOutsideCombat(item))
            return new ConsumableUseResult(false, "该物品无法在战斗外使用");

        return item.consumableType switch
        {
            ConsumableData.ConsumableType.HealingPotion => UseHealingPotion(item, target),
            ConsumableData.ConsumableType.StrongHealing => UseHealingPotion(item, target),
            ConsumableData.ConsumableType.Antidote => UseAntidote(item, target),
            ConsumableData.ConsumableType.Whetstone => UseWhetstone(item, target),
            _ => new ConsumableUseResult(false, "未实装的消耗品类型"),
        };
    }

    /// <summary>获取消耗品可选目标（队伍中需要治疗的成员）</summary>
    public static UnitData[] GetValidTargets(ConsumableData item, UnitData[] partyMembers)
    {
        var targets = new System.Collections.Generic.List<UnitData>();
        foreach (var member in partyMembers)
        {
            if (CanUseOnTarget(item, member))
                targets.Add(member);
        }
        return targets.ToArray();
    }

    // ========================================================================
    // 具体消耗品效果实现
    // ========================================================================

    private static ConsumableUseResult UseHealingPotion(ConsumableData item, UnitData target)
    {
        int maxHp = Combat.CombatStats.GetMaxHp(target);
        if (target.Runtime.CurrentHp >= maxHp)
            return new ConsumableUseResult(false, $"{target.UnitName} 的 HP 已满");

        // 掷骰治疗
        int heal = 0;
        for (int i = 0; i < item.HealDiceCount; i++)
            heal += (int)(GD.Randf() * item.HealDiceSides) + 1;
        heal += item.HealBonus;

        int oldHp = target.Runtime.CurrentHp;
        target.Runtime.CurrentHp = Mathf.Min(maxHp, target.Runtime.CurrentHp + heal);
        int actualHeal = target.Runtime.CurrentHp - oldHp;

        return new ConsumableUseResult(
            true,
            $"{target.UnitName} 恢复了 {actualHeal} 点 HP",
            HealedHp: actualHeal
        );
    }

    private static ConsumableUseResult UseAntidote(ConsumableData item, UnitData target)
    {
        // 检查是否有中毒状态
        // 当前状态系统未实装，预留接口
        // if (!target.HasStatus("poison"))
        //     return new ConsumableUseResult(false, $"{target.UnitName} 没有中毒");

        // target.RemoveStatus("poison");
        return new ConsumableUseResult(
            true,
            $"{target.UnitName} 的中毒状态被解除",
            RemovedStatus: "poison"
        );
    }

    private static ConsumableUseResult UseWhetstone(ConsumableData item, UnitData target)
    {
        // 磨刀石效果：下一场战斗近战伤害+1
        // 需要战斗状态系统支持，预留接口
        // target.SetBuff("whetstone_bonus", duration: "next_combat");
        return new ConsumableUseResult(
            true,
            $"{target.UnitName} 的武器被磨利，下场战斗近战伤害+1"
        );
    }
}
