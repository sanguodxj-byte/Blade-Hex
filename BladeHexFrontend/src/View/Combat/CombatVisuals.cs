// CombatVisuals.cs
// 战斗表现层统一入口 — 从 CombatResolver.ResolveAttack 中提取。
// 所有 VFX/UI/音效/事件在此触发，异常不会传播到调用方（全部通过 GameLog.Exception 记录）。
//
// 设计原则：
//   - 只有主入口（HandleAttack、AIController、CombatMovementController）调用此方法
//   - 技能处理器已有独立表现管线（DamageEvent → HandleSkill），不需要触发此方法
//   - 剑舞者递归内也不触发（由外层 HandleAttack 覆盖）

using System;
using Godot;
using BladeHex.Combat;
using BladeHex.Data;
using BladeHex.Debug;
using CombatUnit = BladeHex.Combat.Unit;

namespace BladeHex.View.Combat;

/// <summary>
/// 战斗表现层统一入口 — 所有攻击结算后的 VFX/UI/音效/事件触发。
/// </summary>
public static class CombatVisuals
{
    /// <summary>
    /// 应用攻击结算后的所有表现层效果：HP 条、受击特效、受击动画、EventBus 事件、死亡动画。
    /// </summary>
    /// <param name="attacker">攻击者</param>
    /// <param name="defender">防御者</param>
    /// <param name="hpDamage">实际造成 HP 伤害</param>
    /// <param name="drDamage">盔甲伤害</param>
    /// <param name="weapon">使用的武器</param>
    /// <param name="finalCritical">是否暴击</param>
    public static void ApplyAttackVisuals(
        CombatUnit attacker,
        CombatUnit defender,
        int hpDamage,
        int drDamage,
        WeaponData? weapon,
        bool finalCritical)
    {
        // HP 条 + 盔甲条
        try { defender.UpdateHpBar(); }
        catch (Exception ex) { GameLog.Exception("[CombatVisuals] UpdateHpBar 异常", ex); }

        try { defender.UpdateArmorBar(); }
        catch (Exception ex) { GameLog.Exception("[CombatVisuals] UpdateArmorBar 异常", ex); }

        if (hpDamage <= 0 && drDamage <= 0) return;

        // 受击特效（粒子）
        if (hpDamage > 0)
        {
            try
            {
                HitEffectType hitType = CombatResolver.GetHitEffectType(attacker, defender, weapon);
                HitEffectManager.Instance?.OnUnitHit(attacker, defender, hpDamage, hitType, finalCritical);
            }
            catch (Exception ex) { GameLog.Exception("[CombatVisuals] HitEffectManager 异常", ex); }
        }

        // 受击动画
        try
        {
            if (defender.RenderBus != null)
                defender.RenderBus.NotifyHit(defender);
        }
        catch (Exception ex) { GameLog.Exception("[CombatVisuals] RenderBus.NotifyHit 异常", ex); }

        // 事件总线
        try { Events.EventBus.Instance?.PublishUnitDamaged(defender, hpDamage, defender.CurrentHp); }
        catch (Exception ex) { GameLog.Exception("[CombatVisuals] EventBus.PublishUnitDamaged 异常", ex); }

        // 死亡动画
        try { _ = defender.HandleDeathAnimIfDead(); }
        catch (Exception ex) { GameLog.Exception("[CombatVisuals] HandleDeathAnimIfDead 异常", ex); }
    }

    /// <summary>
    /// 吸血后 HP 条更新
    /// </summary>
    public static void ApplyLeechVisuals(CombatUnit attacker)
    {
        try { attacker.UpdateHpBar(); }
        catch (Exception ex) { GameLog.Exception("[CombatVisuals] 吸血 HP 条更新异常", ex); }
    }

    /// <summary>
    /// 反击/反伤事件发布
    /// </summary>
    public static void ApplyDamageEvent(CombatUnit unit, int hpDamage)
    {
        try { Events.EventBus.Instance?.PublishUnitDamaged(unit, hpDamage, unit.CurrentHp); }
        catch (Exception ex) { GameLog.Exception("[CombatVisuals] PublishUnitDamaged 异常", ex); }
    }

    /// <summary>
    /// 附加伤害后的死亡动画
    /// </summary>
    public static void ApplyExtraDamageDeathAnim(CombatUnit defender)
    {
        try { _ = defender.HandleDeathAnimIfDead(); }
        catch (Exception ex) { GameLog.Exception("[CombatVisuals] 附加伤害死亡动画异常", ex); }
    }
}
