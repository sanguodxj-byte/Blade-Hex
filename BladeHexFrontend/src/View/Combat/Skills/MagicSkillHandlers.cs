// MagicSkillHandlers.cs
// 法术主动技能 handler — INT 系
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat.Skills;

/// <summary>法术主动技能执行器</summary>
public static class MagicSkillHandlers
{
    public static void ManaShield(in SkillHandlerContext ctx)
    {
        int manaCost = 5;
        if (!ConsumeSpellResource(in ctx, manaCost)) return;
        ctx.Builder.AddStatusEffect("shield", ctx.Attacker, 3);
    }

    public static void TimeWarp(in SkillHandlerContext ctx)
    {
        int manaCost = 10;
        if (!ConsumeSpellResource(in ctx, manaCost)) return;
        ctx.Builder.AddStatusEffect("haste", ctx.Attacker, 1);
    }

    private static bool ConsumeSpellResource(in SkillHandlerContext ctx, int baseManaCost)
    {
        if (ctx.Attacker.Data == null)
        {
            SkillUtils.Fail(ctx.Builder, "施法者数据无效");
            return false;
        }

        int effectiveManaCost = SkillTreeKeystoneResolver.ApplySpellManaCost(ctx.Attacker.Data, baseManaCost);
        int hpCost = SkillTreeKeystoneResolver.GetSpellHpCost(ctx.Attacker.Data, baseManaCost);
        if (ctx.Attacker.Data.CurrentMana < effectiveManaCost)
        {
            SkillUtils.Fail(ctx.Builder, "魔力不足");
            return false;
        }
        if (hpCost > 0 && ctx.Attacker.CurrentHp <= hpCost)
        {
            SkillUtils.Fail(ctx.Builder, "生命不足");
            return false;
        }

        ctx.Attacker.Model.CurrentMana -= effectiveManaCost;
        if (hpCost > 0)
            ctx.Attacker.SetHp(Math.Max(1, ctx.Attacker.CurrentHp - hpCost));
        if (effectiveManaCost > 0)
            CareerPassiveHooks.OnManaSpent(ctx.Attacker, effectiveManaCost);
        return true;
    }

    public static void ArcaneBurst(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        int dmg = RPGRuleEngine.RollDice(2, 8);
        int intMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(ctx.Attacker.Data));
        if (ctx.Attacker.HasSkillEffect("knowledge_power")) dmg += intMod;
        target.TakeDamage(dmg);
        ctx.Builder.AddDamage(target, dmg);
    }

    public static void ManaDrain(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        int drained = RPGRuleEngine.RollDice(2, 6);
        if (target.Data != null) target.Model.CurrentMana = Math.Max(0, target.Model.CurrentMana - drained);
        if (ctx.Attacker.Data != null) ctx.Attacker.Model.CurrentMana += drained;
        ctx.Builder.AddDamage(target, drained);
    }

    public static void ChainLightning(in SkillHandlerContext ctx)
    {
        var firstTarget = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (firstTarget == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }

        int intMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(ctx.Attacker.Data));
        var hitTargets = new List<Unit> { firstTarget };
        int dmg1 = RPGRuleEngine.RollDice(3, 6) + intMod;
        firstTarget.TakeDamage(dmg1);
        ctx.Builder.AddDamage(firstTarget, dmg1);

        int jumps = 0;
        foreach (var enemy in ctx.Enemies)
        {
            if (jumps >= 2) break;
            if (hitTargets.Contains(enemy) || !GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;
            var lastHit = hitTargets[^1];
            int dist = HexUtils.Distance(lastHit.GridPos.X, lastHit.GridPos.Y, enemy.GridPos.X, enemy.GridPos.Y);
            if (dist <= 2)
            {
                int dmg = RPGRuleEngine.RollDice(2, 6) + intMod;
                enemy.TakeDamage(dmg);
                ctx.Builder.AddDamage(enemy, dmg);
                hitTargets.Add(enemy);
                jumps++;
            }
        }
    }

    public static void ArcaneBomb(in SkillHandlerContext ctx)
    {
        int intMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(ctx.Attacker.Data));
        int baseDmg = RPGRuleEngine.RollDice(3, 6) + intMod;
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                int dmg = Math.Max(1, baseDmg / 2 + RPGRuleEngine.RollDice(1, 4));
                if (ctx.Attacker.HasSkillEffect("knowledge_power")) dmg += intMod;
                target.TakeDamage(dmg);
                ctx.Builder.AddDamage(target, dmg);
            }
        }
    }

    public static void VoidGate(in SkillHandlerContext ctx)
    {
        var oldPos = ctx.Attacker.GridPos;
        ctx.Attacker.GridPos = ctx.TargetCell;
        ctx.Builder.AddTeleport(ctx.Attacker, ctx.TargetCell, oldPos);
    }
}
