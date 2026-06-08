// RestService.cs
// 休息设施规则：统一的按时间比例恢复，rateMultiplier 控制加速倍率。
//
// 恢复唯一路径：TimeBasedRecovery(roster, hours, canRestore, rateMultiplier)
// - 正常行军：1x（AdvanceTime 默认）
// - 扎营：2x（CampSystem 传入 2.0f）
// - 城内等待：4x（RestPanel 传入 4.0f）
// 欠饷 + 断粮阻断所有恢复（canRestore = false）。
using System;
using BladeHex.Combat;
using BladeHex.Data;

namespace BladeHex.Strategic.Facilities;

/// <summary>
/// 休息规则服务。只处理 Core 数据，不依赖 Godot UI 节点。
/// </summary>
public static class RestService
{
    /// <summary>每 24 小时恢复 HP 比例（20% 最大 HP）</summary>
    public const float RecoveryHpPerDay = 0.20f;

    /// <summary>每 24 小时恢复法力比例（30% 最大法力）</summary>
    public const float RecoveryManaPerDay = 0.30f;

    public static int CountMembersNeedingRest(PartyRoster? roster)
    {
        if (roster == null) return 0;
        int count = 0;
        foreach (var unit in roster.Members)
        {
            if (PartyRoster.GetCurrentHp(unit) < CombatStats.GetMaxHp(unit) ||
                unit.CurrentMana < CombatStats.GetMaxMana(unit))
                count++;
        }
        return count;
    }

    /// <summary>
    /// 统一的按时间比例恢复。每过一小时恢复 (RecoveryPerDay / 24 * rateMultiplier) 比例。
    /// 欠饷/断粮时 canRestore == false，完全不恢复。
    /// </summary>
    /// <param name="roster">队伍名册</param>
    /// <param name="hours">经过的小时数</param>
    /// <param name="canRestore">是否允许恢复（false 时欠饷/断粮阻断）</param>
    /// <param name="rateMultiplier">恢复倍率（1=正常，2=扎营，4=城内）</param>
    /// <returns>(恢复总HP, 恢复总法力)</returns>
    public static (int HpRestored, int ManaRestored) TimeBasedRecovery(
        PartyRoster? roster, float hours, bool canRestore, float rateMultiplier = 1.0f)
    {
        if (roster == null || roster.Count == 0 || !canRestore || hours <= 0 || rateMultiplier <= 0)
            return (0, 0);

        float ratio = hours / 24.0f * rateMultiplier;
        int totalHp = 0;
        int totalMana = 0;

        foreach (var unit in roster.Members)
        {
            // 按时间比例恢复 HP
            int maxHp = CombatStats.GetMaxHp(unit);
            int curHp = PartyRoster.GetCurrentHp(unit);
            if (curHp < maxHp)
            {
                int hpRecovery = Math.Max(1, (int)(maxHp * RecoveryHpPerDay * ratio));
                int newHp = Math.Min(curHp + hpRecovery, maxHp);
                int actual = newHp - curHp;
                if (actual > 0)
                {
                    totalHp += actual;
                    PartyRoster.SetCurrentHp(unit, newHp);
                }
            }

            // 按时间比例恢复法力
            int maxMana = CombatStats.GetMaxMana(unit);
            if (unit.CurrentMana < maxMana)
            {
                int manaRecovery = Math.Max(1, (int)(maxMana * RecoveryManaPerDay * ratio));
                int newMana = Math.Min(unit.CurrentMana + manaRecovery, maxMana);
                int actual = newMana - unit.CurrentMana;
                if (actual > 0)
                {
                    totalMana += actual;
                    unit.CurrentMana = newMana;
                }
            }
        }

        return (totalHp, totalMana);
    }

}
