// StealthSkillHandlers.cs
// 潜行/刺客主动技能 handler — DEX 潜行系
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.Skills;

/// <summary>潜行/刺客主动技能执行器</summary>
public static class StealthSkillHandlers
{
    public static void Stealth(in SkillHandlerContext ctx)
    {
        ctx.Builder.AddStatusEffect("invisibility", ctx.Attacker, 99);
    }

    public static void ShadowClone(in SkillHandlerContext ctx)
    {
        ctx.Builder.AddStatusEffect("phantom", ctx.Attacker, 3);
    }

    public static void PoisonBlade(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false);
        ctx.Builder.AddDamageFromResolver(target, r);
        ctx.Builder.AddStatusEffect("poison", target, 3);
    }

    public static void ShadowStrike(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }

        bool hasStealth = ctx.Attacker.Data != null &&
            ctx.Attacker.Model.ActiveStatusEffects.Any(e => e.Id == "invisibility");
        float mult = hasStealth ? 2.0f : 1.0f;
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, hasStealth, false, 0, mult);
        ctx.Builder.AddDamageFromResolver(target, r);
        if (hasStealth)
        {
            ctx.Builder.AddRemoveEffect(ctx.Attacker, "invisibility");
        }
    }

    public static void TrapMaster(in SkillHandlerContext ctx)
    {
        ctx.Builder.AddStatusEffect("trap_placed", ctx.Attacker, 99);
    }
}
