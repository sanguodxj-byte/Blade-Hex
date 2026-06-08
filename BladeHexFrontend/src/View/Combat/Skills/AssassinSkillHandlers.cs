// AssassinSkillHandlers.cs
// 刺客系（WIS）主动技能 handler — 2026-05-17 重设计
// WIS = 暴击/刺杀主属性 + 法力续航
using Godot;
using System;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.Skills;

/// <summary>WIS 系刺客主动技能执行器</summary>
public static class AssassinSkillHandlers
{
    /// <summary>
    /// 法力涌动 (wis_b01) — 立即将自身 Mana 恢复至上限，每场战斗 1 次。
    /// </summary>
    public static void ManaSurge(in SkillHandlerContext ctx)
    {
        if (ctx.Attacker.Data == null) { SkillUtils.Fail(ctx.Builder, "无效单位"); return; }
        if (ctx.Attacker.Data.Runtime.ManaSurgeUsedThisCombat > 0)
        {
            SkillUtils.Fail(ctx.Builder, "本场战斗已用过法力涌动");
            return;
        }
        int maxMana = BladeHex.Combat.CombatStats.GetMaxMana(ctx.Attacker.Data);
        int restored = maxMana - ctx.Attacker.Data.CurrentMana;
        ctx.Attacker.Model.CurrentMana = maxMana;
        ctx.Attacker.Model.ManaSurgeUsedThisCombat = 1;
        ctx.Builder.AddHeal(ctx.Attacker, restored);
    }

    /// <summary>
    /// 爆头突袭 (wis_b02) — 下一次武器攻击必定暴击且伤害 ×1.5。
    /// 通过 status_effect "guaranteed_crit" 标记，CombatResolver 检测后强制暴击。
    /// 简化实现：本回合立即对目标做一次伤害 ×1.5 攻击（视为已强制暴击）。
    /// </summary>
    public static void HeadShot(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        // 直接结算：模拟"必定暴击 + 1.5x 伤害"
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, +5, 1.5f);
        ctx.Builder.AddDamageFromResolver(target, r);
    }

    /// <summary>
    /// 暗杀 (wis_b07) — 指定 HP 低于 30% 的敌方单位直接斩杀；boss 改 50% 当前 HP 真伤。
    /// 每场战斗 1 次。
    /// </summary>
    public static void Assassinate(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        if (ctx.Attacker.Data == null) { SkillUtils.Fail(ctx.Builder, "无效单位"); return; }
        if (ctx.Attacker.Data.Runtime.AssassinateUsedThisCombat > 0)
        {
            SkillUtils.Fail(ctx.Builder, "本场战斗已用过暗杀");
            return;
        }
        int maxHp = target.Model.GetMaxHp();
        int curHp = target.CurrentHp;
        bool isBoss = target.Data?.enemyType == UnitData.EnemyType.Legendary
            || target.Data?.enemyType == UnitData.EnemyType.Dragon;
        int dmg;
        if (curHp * 1.0f / maxHp >= 0.30f)
        {
            // 不在斩杀阈值内：作普通真伤 1d8 + WIS_mod
            int wisMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(ctx.Attacker.Data));
            dmg = RPGRuleEngine.RollDice(1, 8) + wisMod;
        }
        else if (isBoss)
        {
            dmg = curHp / 2;
        }
        else
        {
            dmg = curHp; // 直接斩杀
        }
        target.TakeDamage(dmg);
        ctx.Attacker.Model.AssassinateUsedThisCombat = 1;
        ctx.Builder.AddDamage(target, dmg);
    }
}
