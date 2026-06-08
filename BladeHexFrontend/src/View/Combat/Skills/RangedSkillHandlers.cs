// RangedSkillHandlers.cs
// 远程主动技能 handler — DEX 射击系
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.Skills;

/// <summary>远程主动技能执行器</summary>
public static class RangedSkillHandlers
{
    public static void AimedShot(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, 0, 2.0f);
        ctx.Builder.AddDamageFromResolver(target, r);
    }

    public static void DoubleShot(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        var r1 = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -2);
        ctx.Builder.AddDamageFromResolver(target, r1);
        var r2 = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -2);
        ctx.Builder.AddDamageFromResolver(target, r2);
    }

    public static void ScatterShot(in SkillHandlerContext ctx)
    {
        if (ctx.Grid == null) return;
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -2);
                ctx.Builder.AddDamageFromResolver(target, r);
            }
        }
    }

    public static void MultiShot(in SkillHandlerContext ctx)
    {
        // v0.6 11.8 dex_b03 连珠箭：连射 3 支箭，每支 -2 命中，节点平伤每支 50%
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null)
        {
            // Fallback：扫一圈邻格
            var targets = new List<Vector2I> { ctx.TargetCell };
            targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
            int shotCount = 0;
            foreach (var pos in targets)
            {
                if (shotCount >= 3) break;
                var t = SkillUtils.FindUnitAt(pos, ctx.Enemies);
                if (t != null)
                {
                    var r = CombatResolver.ResolveAttack(ctx.Attacker, t, ctx.Grid, false, false, -2, 1.0f, null, 0.5f);
                    ctx.Builder.AddDamageFromResolver(t, r);
                    shotCount++;
                }
            }
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            if (target.CurrentHp <= 0) break;
            var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -2, 1.0f, null, 0.5f);
            ctx.Builder.AddDamageFromResolver(target, r);
        }
    }

    public static void BlindArrow(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false);
        ctx.Builder.AddDamageFromResolver(target, r);
        if (r.ContainsKey("hit") && r["hit"].AsBool())
        {
            ctx.Builder.AddStatusEffect("blind", target, 2);
        }
    }

    public static void TrickArrow(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        int dmg = RPGRuleEngine.RollDice(1, 10);
        target.TakeDamage(dmg);
        ctx.Builder.AddDamage(target, dmg);
        string[] debuffs = { "blind", "stun", "fear" };
        string chosen = debuffs[new Random().Next(debuffs.Length)];
        ctx.Builder.AddStatusEffect(chosen, target, 1);
    }

    public static void MeteorShower(in SkillHandlerContext ctx)
    {
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                int dmg = RPGRuleEngine.RollDice(2, 8);
                target.TakeDamage(dmg);
                ctx.Builder.AddDamage(target, dmg);
            }
        }
    }
}
