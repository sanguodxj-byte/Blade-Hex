using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat.Skills;

namespace BladeHex.Combat;

/// <summary>
/// 技能特效执行引擎 — 注册表分发器
/// 通过 handler 注册表将技能 ID 映射到具体执行方法。
/// 具体实现分布在 Skills/ 子目录的各 Handler 类中。
/// </summary>
public static class SkillEffectExecutor
{
    // ============================================================================
    // Handler 委托类型
    // ============================================================================

    /// <summary>技能执行 handler 签名</summary>
    public delegate void SkillHandler(in SkillHandlerContext ctx);

    // ============================================================================
    // Handler 注册表
    // ============================================================================

    private static readonly Dictionary<string, SkillHandler> Handlers = new()
    {
        // --- STR 近战 ---
        ["double_attack"] = MeleeSkillHandlers.DoubleAttack,
        ["whirlwind"] = MeleeSkillHandlers.Whirlwind,
        ["battle_cry"] = MeleeSkillHandlers.BattleCry,
        ["blood_vortex"] = MeleeSkillHandlers.BloodVortex,
        ["bloodthirst"] = MeleeSkillHandlers.Bloodthirst,
        ["sword_dance"] = MeleeSkillHandlers.SwordDance,
        ["shield_bash"] = MeleeSkillHandlers.ShieldBash,

        // --- DEX 远程 ---
        ["aimed_shot"] = RangedSkillHandlers.AimedShot,
        ["double_shot"] = RangedSkillHandlers.DoubleShot,
        ["scatter_shot"] = RangedSkillHandlers.ScatterShot,
        ["multi_shot"] = RangedSkillHandlers.MultiShot,
        ["blind_arrow"] = RangedSkillHandlers.BlindArrow,
        ["trick_arrow"] = RangedSkillHandlers.TrickArrow,
        ["meteor_shower"] = RangedSkillHandlers.MeteorShower,

        // --- DEX 潜行 ---
        ["stealth"] = StealthSkillHandlers.Stealth,
        ["shadow_clone"] = StealthSkillHandlers.ShadowClone,
        ["poison_blade"] = StealthSkillHandlers.PoisonBlade,
        ["shadow_strike"] = StealthSkillHandlers.ShadowStrike,
        ["trap_master"] = StealthSkillHandlers.TrapMaster,

        // --- INT 法术 ---
        ["mana_shield"] = MagicSkillHandlers.ManaShield,
        ["time_warp"] = MagicSkillHandlers.TimeWarp,
        ["arcane_burst"] = MagicSkillHandlers.ArcaneBurst,
        ["mana_drain"] = MagicSkillHandlers.ManaDrain,
        ["chain_lightning"] = MagicSkillHandlers.ChainLightning,
        ["arcane_bomb"] = MagicSkillHandlers.ArcaneBomb,
        ["void_gate"] = MagicSkillHandlers.VoidGate,

        // --- WIS/CON 治疗与防御 ---
        ["basic_heal"] = SupportSkillHandlers.BasicHeal,
        ["field_medic"] = SupportSkillHandlers.FieldMedic,
        ["group_heal"] = SupportSkillHandlers.GroupHeal,
        ["life_circle"] = SupportSkillHandlers.LifeCircle,
        ["blessing"] = SupportSkillHandlers.Blessing,
        ["unyielding_bulwark"] = SupportSkillHandlers.UnyieldingBulwark,
        ["life_shield"] = SupportSkillHandlers.LifeShield,
        ["guardian_spirit"] = SupportSkillHandlers.GuardianSpirit,
        ["resurrect"] = SupportSkillHandlers.Resurrect,
        ["purifying_flame"] = SupportSkillHandlers.PurifyingFlame,
        ["arcane_judgment"] = SupportSkillHandlers.ArcaneJudgment,
        ["oracle"] = SupportSkillHandlers.Oracle,
        ["elemental_storm"] = SupportSkillHandlers.ElementalStorm,

        // --- CHA 领导 ---
        ["war_cry"] = SupportSkillHandlers.WarCry,
        ["inspire"] = SupportSkillHandlers.Inspire,
        ["taunt"] = SupportSkillHandlers.Taunt,
        ["command"] = SupportSkillHandlers.Command,
        ["rally"] = SupportSkillHandlers.Rally,
        ["shadow_deal"] = SupportSkillHandlers.ShadowDeal,
        ["intimidate"] = SupportSkillHandlers.Intimidate,
        ["heroic_call"] = SupportSkillHandlers.HeroicCall,
    };

    // ============================================================================
    // 基础接口（保持向后兼容）
    // ============================================================================

    public static Godot.Collections.Dictionary GetSkillConfig(string skillEffect) => SkillRegistry.GetSkillConfig(skillEffect);
    public static bool IsActiveSkill(string skillEffect) => SkillRegistry.IsActiveSkill(skillEffect);
    public static bool IsPassiveSkill(string skillEffect) => SkillRegistry.IsPassiveSkill(skillEffect);

    // ============================================================================
    // 主动技能执行 — 主入口（注册表分发）
    // ============================================================================

    public static Godot.Collections.Dictionary ExecuteActiveSkill(
        Unit attacker,
        string skillEffect,
        Vector2I targetCell,
        HexGrid? grid,
        IEnumerable<Unit> allUnits,
        IEnumerable<Unit> playerUnits,
        IEnumerable<Unit> enemyUnits
    )
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.Count == 0 || !IsActiveSkill(skillEffect))
        {
            return new Godot.Collections.Dictionary {
                { "success", false },
                { "reason", "未知或非主动技能" }
            };
        }

        var allies = !attacker.Data!.IsEnemy ? playerUnits : enemyUnits;
        var enemies = !attacker.Data!.IsEnemy ? enemyUnits : playerUnits;

        var result = new Godot.Collections.Dictionary
        {
            { "success", true },
            { "results", new Godot.Collections.Array() },
            { "action_cost", cfg.ContainsKey("action_cost") ? cfg["action_cost"].AsString() : "major" },
            { "vfx_type", cfg.ContainsKey("vfx") ? cfg["vfx"].AsString() : "" },
            { "status_effects", new Godot.Collections.Array() }
        };

        // 注册表分发
        if (Handlers.TryGetValue(skillEffect, out var handler))
        {
            var ctx = new SkillHandlerContext
            {
                Attacker = attacker,
                TargetCell = targetCell,
                Grid = grid,
                Enemies = enemies,
                Allies = allies,
                Result = result,
            };
            handler(in ctx);
        }
        else
        {
            result["success"] = false;
            result["reason"] = $"技能 {skillEffect} 逻辑尚未注册";
        }

        return result;
    }

    // ============================================================================
    // 向后兼容
    // ============================================================================

    public static bool HasQuickCast(Unit unit) => PassiveSkillResolver.HasQuickCast(unit);
}
