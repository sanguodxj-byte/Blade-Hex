// SupportSkillHandlers.cs
// 辅助/治疗/领导主动技能 handler — WIS/CHA/CON 系
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.Skills;

/// <summary>辅助/治疗/领导主动技能执行器</summary>
public static class SupportSkillHandlers
{
    // ========== 治疗 ==========

    public static void BasicHeal(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有盟友"); return; }
        int wisMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Wis);
        int heal = RPGRuleEngine.RollDice(1, 8) + wisMod;
        int actual = target.Heal(heal);
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "heal" }, { "target", target }, { "value", actual }
        });
    }

    public static void FieldMedic(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有盟友"); return; }
        int wisMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Wis);
        int heal = RPGRuleEngine.RollDice(2, 8) + wisMod;
        int actual = target.Heal(heal);
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "heal" }, { "target", target }, { "value", actual }
        });
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target }, { "special", "remove_effects" },
            { "remove_ids", new Godot.Collections.Array { "bleed", "poison" } }
        });
    }

    public static void GroupHeal(in SkillHandlerContext ctx)
    {
        int wisMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Wis);
        var neighbors = ctx.Grid != null ? HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var ally = SkillUtils.FindUnitAt(pos, ctx.Allies);
            if (ally != null && GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                int heal = RPGRuleEngine.RollDice(1, 6) + wisMod;
                if (ctx.Attacker.HasSkillEffect("life_mastery")) heal = (int)(heal * 1.5f);
                int actual = ally.Heal(heal);
                ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "type", "heal" }, { "target", ally }, { "value", actual }
                });
            }
        }
    }

    public static void LifeCircle(in SkillHandlerContext ctx)
    {
        // v0.6 11.8 con_b07: 每场战斗最多 1 次；治疗周围友军 1d10 + CON_HP_Bonus + NodeHealAmount
        if (ctx.Attacker.Data?.Runtime.LifeCircleUsedThisCombat > 0)
        {
            SkillUtils.Fail(ctx.Result, "本场战斗已用过生命之环");
            return;
        }
        // CON_HP_Bonus = floor(sqrt(CON/4))
        int conScore = ctx.Attacker.Data?.Con ?? 10;
        int conBonus = (int)System.Math.Floor(System.Math.Sqrt(conScore / 4.0));
        int nodeHeal = ctx.Attacker.SkillTree?.GetMeleeDamageBonus() ?? 0; // 节点 heal_amount 暂用通用接口
        var neighbors = ctx.Grid != null ? HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var ally = SkillUtils.FindUnitAt(pos, ctx.Allies);
            if (ally != null && GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                int heal = RPGRuleEngine.RollDice(1, 10) + conBonus + nodeHeal;
                int actual = ally.Heal(heal);
                ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "type", "heal" }, { "target", ally }, { "value", actual }
                });
            }
        }
        if (ctx.Attacker.Data != null) ctx.Attacker.Data.Runtime.LifeCircleUsedThisCombat = 1;
    }

    // ========== 增益/防御 ==========

    public static void Blessing(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有盟友"); return; }
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target }, { "effect_id", "bless" }, { "duration", 3 }
        });
    }

    public static void UnyieldingBulwark(in SkillHandlerContext ctx)
    {
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", ctx.Attacker }, { "effect_id", "shield" }, { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "damage_reduction_percent", 0.5 } } }
        });
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", ctx.Attacker }, { "effect_id", "temp_hp" }, { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "temp_hp_amount", RPGRuleEngine.RollDice(2, 6) } } }
        });
    }

    public static void LifeShield(in SkillHandlerContext ctx)
    {
        // v0.6 11.8 限制：每场战斗最多 1 次
        if (ctx.Attacker.Data?.Runtime.LifeShieldUsedThisCombat > 0)
        {
            SkillUtils.Fail(ctx.Result, "本场战斗已用过生命之盾");
            return;
        }
        int shieldAmount = (int)(ctx.Attacker.Model.GetMaxHp() * 0.3f);
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", ctx.Attacker }, { "effect_id", "temp_hp" }, { "duration", 3 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "temp_hp_amount", shieldAmount } } }
        });
        if (ctx.Attacker.Data != null) ctx.Attacker.Data.Runtime.LifeShieldUsedThisCombat = 1;
    }

    public static void GuardianSpirit(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有盟友"); return; }
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target }, { "effect_id", "guardian_spirit" }, { "duration", 3 }, { "source", ctx.Attacker }
        });
    }

    public static void Resurrect(in SkillHandlerContext ctx)
    {
        // 全游戏无复活机制 — 本 handler 保留只为兼容旧 SkillTreeData 节点 ID 引用，
        // 行为改为永远失败。详见 docs/法表系统.md §5.4
        SkillUtils.Fail(ctx.Result, "本游戏不存在复活机制");
    }

    // ========== 领导/CHA ==========

    public static void WarCry(in SkillHandlerContext ctx)
    {
        if (ctx.Grid == null) return;
        var neighbors = HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y);
        foreach (var pos in neighbors)
        {
            var ally = SkillUtils.FindUnitAt(pos, ctx.Allies);
            if (ally != null && GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", ally }, { "effect_id", "bless" }, { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "attack_bonus", 1 } } }
                });
            }
        }
    }

    public static void Inspire(in SkillHandlerContext ctx)
    {
        foreach (var ally in ctx.Allies)
            if (GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
                MoraleSystem.ChangeMorale(ally, 2);
    }

    public static void Taunt(in SkillHandlerContext ctx)
    {
        if (ctx.Grid == null) return;
        var neighbors = HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y);
        foreach (var pos in neighbors)
        {
            var enemy = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (enemy != null)
            {
                ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", enemy }, { "effect_id", "charmed" }, { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "forced_target_id", (long)ctx.Attacker.GetInstanceId() } } }
                });
            }
        }
    }

    public static void Command(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有盟友"); return; }
        // v0.6 11.7 / 11.8: 不能指定本回合已获额外行动的单位
        if (target.Data?.Runtime.ExtraActionsThisTurn > 0)
        {
            SkillUtils.Fail(ctx.Result, "目标本回合已获得过额外行动");
            return;
        }
        if (target.Data != null) target.Data.Runtime.ExtraActionsThisTurn += 1;
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target }, { "effect_id", "commanded" }, { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "extra_action", true } } }
        });
    }

    public static void Rally(in SkillHandlerContext ctx)
    {
        var neighbors = ctx.Grid != null ? HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var ally = SkillUtils.FindUnitAt(pos, ctx.Allies);
            if (ally != null && GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", ally }, { "effect_id", "rallied" }, { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "attack_bonus", 2 } } }
                });
            }
        }
    }

    public static void ShadowDeal(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        int chaMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Cha);
        int prof = RPGRuleEngine.GetProficiencyBonus(ctx.Attacker.Data!.Level);
        int dc = 10 + chaMod + prof;
        int targetProf = RPGRuleEngine.GetProficiencyBonus(target.Data!.Level);
        var save = RPGRuleEngine.MakeSave(target.Data!.Wis, targetProf, true, dc);
        bool success = !(bool)save["success"];
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "bribe" }, { "target", target }, { "save_result", save }, { "success", success }
        });
        if (success)
        {
            ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                { "target", target }, { "effect_id", "bribed" }, { "duration", 99 }
            });
        }
    }

    public static void Intimidate(in SkillHandlerContext ctx)
    {
        var neighbors = ctx.Grid != null ? HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var enemy = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (enemy != null)
            {
                ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", enemy }, { "effect_id", "intimidated" }, { "duration", 3 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "attack_bonus", -2 } } }
                });
            }
        }
    }

    public static void HeroicCall(in SkillHandlerContext ctx)
    {
        // v0.6 11.8 cha_b10: 每场战斗最多 1 次;半径 2 内友军命中 +2、AC +1,持续 3 回合
        if (ctx.Attacker.Data?.Runtime.HeroicCallUsedThisCombat > 0)
        {
            SkillUtils.Fail(ctx.Result, "本场战斗已用过英雄号召");
            return;
        }
        foreach (var ally in ctx.Allies)
        {
            if (GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0 && ally.Data != null)
            {
                // 新 Buff 系统:施加 heroic_call buff
                BladeHex.Combat.Buff.BuffSystem.Apply(ally.Data, "heroic_call", duration: 3,
                    sourceUnitId: (int)ctx.Attacker.GetInstanceId(), source: "heroic_call");
            }
        }
        if (ctx.Attacker.Data != null) ctx.Attacker.Data.Runtime.HeroicCallUsedThisCombat = 1;
    }

    // ========== 奥术攻击 ==========

    public static void PurifyingFlame(in SkillHandlerContext ctx)
    {
        int wisMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Wis);
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                int dmg = RPGRuleEngine.RollDice(2, 8) + wisMod;
                if (target.Data != null && target.Data.enemyType == UnitData.EnemyType.Undead)
                    dmg = (int)(dmg * 1.5f);
                target.TakeDamage(dmg);
                ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "type", "damage" }, { "target", target }, { "value", dmg }, { "damage_type", "arcane" }
                });
            }
        }
    }

    public static void ArcaneJudgment(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        int wisMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Wis);
        int dmg = RPGRuleEngine.RollDice(3, 10) + wisMod;
        if (ctx.Attacker.HasSkillEffect("knowledge_power"))
            dmg += RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Intel);
        target.TakeDamage(dmg);
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "damage" }, { "target", target }, { "value", dmg }, { "damage_type", "arcane" }
        });
    }

    public static void Oracle(in SkillHandlerContext ctx)
    {
        var revealed = new Godot.Collections.Array();
        foreach (var enemy in ctx.Enemies)
        {
            if (enemy.Data != null)
            {
                foreach (var eff in enemy.Data.Runtime.ActiveStatusEffects)
                {
                    if (eff.Id == "invisibility" || eff.Id == "stealth")
                    {
                        revealed.Add(new Godot.Collections.Dictionary {
                            { "target", enemy }, { "special", "remove_effects" },
                            { "remove_ids", new Godot.Collections.Array { "invisibility", "stealth" } }
                        });
                    }
                }
            }
        }
        foreach (var r in revealed) ctx.Result["status_effects"].AsGodotArray().Add(r);
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "reveal" }, { "revealed_count", revealed.Count }
        });
    }

    public static void ElementalStorm(in SkillHandlerContext ctx)
    {
        int wisMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Wis);
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                int dmg = RPGRuleEngine.RollDice(2, 8) + wisMod;
                if (ctx.Attacker.HasSkillEffect("knowledge_power"))
                    dmg += RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Intel);
                target.TakeDamage(dmg);
                ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "type", "damage" }, { "target", target }, { "value", dmg }, { "damage_type", "elemental" }
                });
            }
        }
    }
}
