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
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", ctx.Attacker }, { "effect_id", "invisibility" }, { "duration", 99 }
        });
    }

    public static void ShadowClone(in SkillHandlerContext ctx)
    {
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", ctx.Attacker }, { "effect_id", "phantom" }, { "duration", 3 }
        });
    }

    public static void PoisonBlade(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false);
        ctx.Result["results"].AsGodotArray().Add(r);
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target }, { "effect_id", "poison" }, { "duration", 3 }
        });
    }

    public static void ShadowStrike(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }

        bool hasStealth = ctx.Attacker.Data != null &&
            ctx.Attacker.Data.Runtime.ActiveStatusEffects.Any(e => e.Id == "invisibility");
        float mult = hasStealth ? 2.0f : 1.0f;
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, hasStealth, false, 0, mult);
        ctx.Result["results"].AsGodotArray().Add(r);
        if (hasStealth)
        {
            ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                { "target", ctx.Attacker }, { "special", "remove_effects" },
                { "remove_ids", new Godot.Collections.Array { "invisibility" } }
            });
        }
    }

    public static void TrapMaster(in SkillHandlerContext ctx)
    {
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", ctx.Attacker }, { "effect_id", "trap_placed" }, { "duration", 99 },
            { "trap_position", ctx.TargetCell }, { "trap_damage", RPGRuleEngine.RollDice(2, 6) },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "slow", 1 } } }
        });
    }
}
