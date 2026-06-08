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
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有盟友"); return; }
        int wisMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(ctx.Attacker.Data));
        int heal = RPGRuleEngine.RollDice(1, 8) + wisMod
                 + (ctx.Attacker.SkillTree?.GetHealBonus() ?? 0);
        int actual = target.Heal(heal, ctx.Attacker);
        ctx.Builder.AddHeal(target, actual);
    }

    public static void FieldMedic(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有盟友"); return; }
        int wisMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(ctx.Attacker.Data));
        int heal = RPGRuleEngine.RollDice(2, 8) + wisMod
                 + (ctx.Attacker.SkillTree?.GetHealBonus() ?? 0);
        int actual = target.Heal(heal, ctx.Attacker);
        ctx.Builder.AddHeal(target, actual);
        ctx.Builder.AddRemoveEffect(target, "bleed");
        ctx.Builder.AddRemoveEffect(target, "poison");
    }

    public static void GroupHeal(in SkillHandlerContext ctx)
    {
        int wisMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(ctx.Attacker.Data));
        var neighbors = ctx.Grid != null ? HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var ally = SkillUtils.FindUnitAt(pos, ctx.Allies);
            if (ally != null && GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                int heal = RPGRuleEngine.RollDice(1, 6) + wisMod;
                if (ctx.Attacker.HasSkillEffect("life_mastery")) heal = (int)(heal * 1.5f);
                int actual = ally.Heal(heal, ctx.Attacker);
                ctx.Builder.AddHeal(ally, actual);
            }
        }
    }

    public static void LifeCircle(in SkillHandlerContext ctx)
    {
        // v0.6 11.8 con_b07: 每场战斗最多 1 次；治疗周围友军 1d10 + CON_HP_Bonus + NodeHealAmount
        if (ctx.Attacker.Data?.Runtime.LifeCircleUsedThisCombat > 0)
        {
            SkillUtils.Fail(ctx.Builder, "本场战斗已用过生命之环");
            return;
        }
        // CON_HP_Bonus = floor(sqrt(CON/4))
        int conScore = CombatStats.GetEffectiveCon(ctx.Attacker.Data);
        int conBonus = (int)System.Math.Floor(System.Math.Sqrt(conScore / 4.0));
        int nodeHeal = ctx.Attacker.SkillTree?.GetMeleeDamageBonus() ?? 0; // 节点 heal_amount 暂用通用接口
        var neighbors = ctx.Grid != null ? HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var ally = SkillUtils.FindUnitAt(pos, ctx.Allies);
            if (ally != null && GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                int heal = RPGRuleEngine.RollDice(1, 10) + conBonus + nodeHeal;
                int actual = ally.Heal(heal, ctx.Attacker);
                ctx.Builder.AddHeal(ally, actual);
            }
        }
        if (ctx.Attacker.Data != null) ctx.Attacker.Model.LifeCircleUsedThisCombat = 1;
    }

    // ========== 增益/防御 ==========

    public static void Blessing(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有盟友"); return; }
        ctx.Builder.AddStatusEffect("bless", target, 3);
    }

    public static void UnyieldingBulwark(in SkillHandlerContext ctx)
    {
        ctx.Builder.AddStatusEffect("shield", ctx.Attacker, 2);
        ctx.Builder.AddStatusEffect("temp_hp", ctx.Attacker, 2);
    }

    public static void LifeShield(in SkillHandlerContext ctx)
    {
        // v0.6 11.8 限制：每场战斗最多 1 次
        if (ctx.Attacker.Data?.Runtime.LifeShieldUsedThisCombat > 0)
        {
            SkillUtils.Fail(ctx.Builder, "本场战斗已用过生命之盾");
            return;
        }
        ctx.Builder.AddStatusEffect("temp_hp", ctx.Attacker, 3);
        if (ctx.Attacker.Data != null) ctx.Attacker.Model.LifeShieldUsedThisCombat = 1;
    }

    public static void GuardianSpirit(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有盟友"); return; }
        ctx.Builder.AddStatusEffect("guardian_spirit", target, 3);
    }

    public static void Resurrect(in SkillHandlerContext ctx)
    {
        // 全游戏无复活机制 — 本 handler 保留只为兼容旧 SkillTreeData 节点 ID 引用，
        // 行为改为永远失败。详见 docs/法表系统.md §5.4
        SkillUtils.Fail(ctx.Builder, "本游戏不存在复活机制");
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
                ctx.Builder.AddStatusEffect("bless", ally, 2);
            }
        }
    }

    public static void Inspire(in SkillHandlerContext ctx)
    {
        // (士气鼓舞已移除)
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
                ctx.Builder.AddStatusEffect("charmed", enemy, 2);
            }
        }
    }

    public static void Command(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Allies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有盟友"); return; }
        // v0.6 11.7 / 11.8: 不能指定本回合已获额外行动的单位
        if (target.Data?.Runtime.ExtraActionsThisTurn > 0)
        {
            SkillUtils.Fail(ctx.Builder, "目标本回合已获得过额外行动");
            return;
        }
        if (target.Data != null) target.Model.ExtraActionsThisTurn += 1;
        ctx.Builder.AddStatusEffect("commanded", target, 1);
    }

    public static void Rally(in SkillHandlerContext ctx)
    {
        var neighbors = ctx.Grid != null ? HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var ally = SkillUtils.FindUnitAt(pos, ctx.Allies);
            if (ally != null && GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                ctx.Builder.AddStatusEffect("rallied", ally, 2);
            }
        }
    }

    public static void ShadowDeal(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        int chaMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveCha(ctx.Attacker.Data));
        int dc = 10 + chaMod;
        int targetBonus = CombatStats.GetSaveBonus(target.Data)
            + PassiveSkillResolver.GetRoyalPresenceAuraSaveBonus(target, ctx.Enemies);
        var save = RPGRuleEngine.MakeSave(CombatStats.GetEffectiveWis(target.Data), targetBonus, true, dc);
        bool success = !(bool)save["success"];
        ctx.Builder.AddText($"暗影交易检定: {(success ? "成功" : "失败")}");
        if (success)
        {
            ctx.Builder.AddStatusEffect("bribed", target, 99);
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
                ctx.Builder.AddStatusEffect("intimidated", enemy, 3);
            }
        }
    }

    public static void HeroicCall(in SkillHandlerContext ctx)
    {
        // v0.6 11.8 cha_b10: 每场战斗最多 1 次;半径 2 内友军命中 +2、AC +1,持续 3 回合
        if (ctx.Attacker.Data?.Runtime.HeroicCallUsedThisCombat > 0)
        {
            SkillUtils.Fail(ctx.Builder, "本场战斗已用过英雄号召");
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
        if (ctx.Attacker.Data != null) ctx.Attacker.Model.HeroicCallUsedThisCombat = 1;
    }

    // ========== 奥术攻击 ==========

    public static void PurifyingFlame(in SkillHandlerContext ctx)
    {
        int wisMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(ctx.Attacker.Data));
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
                ctx.Builder.AddDamage(target, dmg);
            }
        }
    }

    public static void ArcaneJudgment(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Builder, "目标格没有敌人"); return; }
        int wisMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(ctx.Attacker.Data));
        int dmg = RPGRuleEngine.RollDice(3, 10) + wisMod;
        if (ctx.Attacker.HasSkillEffect("knowledge_power"))
            dmg += RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(ctx.Attacker.Data));
        target.TakeDamage(dmg);
        ctx.Builder.AddDamage(target, dmg);
    }

    public static void Oracle(in SkillHandlerContext ctx)
    {
        int revealedCount = 0;
        foreach (var enemy in ctx.Enemies)
        {
            if (enemy.Data != null)
            {
                foreach (var eff in enemy.Model.ActiveStatusEffects)
                {
                    if (eff.Id == "invisibility" || eff.Id == "stealth")
                    {
                        ctx.Builder.AddRemoveEffect(enemy, eff.Id);
                        revealedCount++;
                    }
                }
            }
        }
        ctx.Builder.AddText($"揭示了 {revealedCount} 个隐形单位");
    }

    public static void ElementalStorm(in SkillHandlerContext ctx)
    {
        int wisMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(ctx.Attacker.Data));
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                int dmg = RPGRuleEngine.RollDice(2, 8) + wisMod;
                if (ctx.Attacker.HasSkillEffect("knowledge_power"))
                    dmg += RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(ctx.Attacker.Data));
                target.TakeDamage(dmg);
                ctx.Builder.AddDamage(target, dmg);
            }
        }
    }
}
