using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Combat.AI;

namespace BladeHex.Combat;

/// <summary>
/// 战斗流程总控类
/// </summary>
public partial class CombatManager : Node
{
    public enum CombatState { Init, PlayerTurn, EnemyTurn, CombatEnd }

    [Signal] public delegate void TurnStartedEventHandler(CombatState state);
    [Signal] public delegate void CombatEndedEventHandler(bool victory);
    [Signal] public delegate void SkillUsedEventHandler(Unit caster, string skillEffect, Godot.Collections.Dictionary result);

    public CombatState CurrentState { get; private set; } = CombatState.Init;
    public List<Unit> AllUnits { get; } = new();
    public List<Unit> PlayerUnits { get; } = new();
    public List<Unit> EnemyUnits { get; } = new();

    public Unit? ActiveUnit { get; set; }

    public AIDifficultyConfig? DifficultyConfig { get; set; }
    public StatusEffectManager? StatusEffectManagerInstance { get; private set; }

    public override void _Ready()
    {
        StatusEffectManagerInstance = new StatusEffectManager();
        AddChild(StatusEffectManagerInstance);
    }

    public void SetDifficulty(AIDifficultyConfig config) => DifficultyConfig = config;

    public AIDifficultyConfig GetDifficultyConfig()
    {
        if (DifficultyConfig == null) DifficultyConfig = new AIDifficultyConfig();
        return DifficultyConfig;
    }

    public void RegisterUnit(Unit unit, bool isPlayer)
    {
        AllUnits.Add(unit);
        if (isPlayer) PlayerUnits.Add(unit);
        else EnemyUnits.Add(unit);

        unit.TreeExited += () => OnUnitDied(unit, isPlayer);
    }

    public void StartCombat() => ChangeState(CombatState.PlayerTurn);

    public void ChangeState(CombatState newState)
    {
        CurrentState = newState;

        if (CurrentState == CombatState.PlayerTurn) ResetUnitsActions(PlayerUnits);
        else if (CurrentState == CombatState.EnemyTurn) ResetUnitsActions(EnemyUnits);

        EmitSignal(SignalName.TurnStarted, (int)CurrentState);
    }

    private void ResetUnitsActions(IEnumerable<Unit> units)
    {
        foreach (var u in units)
        {
            u.HasMoved = false;
            u.HasActed = false;
            u.CurrentAp = u.GetMaxAp(); // 重置 AP 到最大值 (高位值)
            if (u.Data != null) u.Data.IsRangedWeaponLoaded = true; // 回合开始重置装填状态
        }
    }

    public void EndCurrentTurn()
    {
        if (CurrentState == CombatState.PlayerTurn) ChangeState(CombatState.EnemyTurn);
        else if (CurrentState == CombatState.EnemyTurn) ChangeState(CombatState.PlayerTurn);
    }

    // ============================================================================
    // 主动技能释放
    // ============================================================================

    public Godot.Collections.Dictionary UseSkill(Unit caster, string skillEffect, Vector2I targetCell, Map.HexGrid? grid = null)
    {
        if (!GodotObject.IsInstanceValid(caster) || caster.CurrentHp <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "reason", "施放者无效" } };

        var cfg = SkillEffectExecutor.GetSkillConfig(skillEffect);
        int apCost = cfg.ContainsKey("action_cost") ? cfg["action_cost"].AsInt32() : 4;

        if (caster.CurrentAp < apCost)
            return new Godot.Collections.Dictionary { { "success", false }, { "reason", $"AP 不足 (需要 {apCost})" } };

        if (!caster.HasSkillEffect(skillEffect))
            return new Godot.Collections.Dictionary { { "success", false }, { "reason", "未拥有该技能" } };

        var result = SkillEffectExecutor.ExecuteActiveSkill(
            caster, skillEffect, targetCell, grid,
            AllUnits, PlayerUnits, EnemyUnits
        );

        if (!result.ContainsKey("success") || !result["success"].AsBool()) return result;

        caster.ConsumeAp(apCost);
        caster.HasActed = true;

        // VFX 逻辑暂略或集成 Singleton
        // string vfxType = result.ContainsKey("vfx_type") ? result["vfx_type"].AsString() : "";

        if (StatusEffectManagerInstance != null) ApplyStatusEffects(result);

        EmitSignal(SignalName.SkillUsed, caster, skillEffect, result);
        return result;
    }

    private void ApplyStatusEffects(Godot.Collections.Dictionary skillResult)
    {
        if (!skillResult.ContainsKey("status_effects")) return;
        var effects = skillResult["status_effects"].AsGodotArray();
        foreach (var effVar in effects)
        {
            if (effVar.VariantType != Variant.Type.Dictionary) continue;
            var eff = effVar.AsGodotDictionary();
            var target = eff.ContainsKey("target") ? eff["target"].As<Unit>() : null;
            if (!GodotObject.IsInstanceValid(target)) continue;

            string special = eff.ContainsKey("special") ? eff["special"].AsString() : "";
            if (special == "remove_effects")
            {
                var removeIds = eff["remove_ids"].AsGodotArray();
                foreach (var rid in removeIds) StatusEffectManagerInstance?.RemoveEffect(target, rid.AsString());
                continue;
            }
            if (special == "remove_all_negative")
            {
                StatusEffectManagerInstance?.RemoveAllNegative(target);
                continue;
            }

            string effectId = eff.ContainsKey("effect_id") ? eff["effect_id"].AsString() : "";
            if (!string.IsNullOrEmpty(effectId))
            {
                int duration = eff.ContainsKey("duration") ? eff["duration"].AsInt32() : -1;
                StatusEffectManagerInstance?.ApplyEffect(target, effectId, duration);
                
                var statMods = eff.ContainsKey("stat_modifiers") ? eff["stat_modifiers"].AsGodotDictionary() : null;
                if (statMods != null && statMods.Count > 0 && target.Data != null)
                {
                    foreach (var existing in target.Data.ActiveStatusEffects)
                    {
                        if (existing["id"].AsString() == effectId)
                        {
                            var existingMods = existing["stat_modifiers"].AsGodotDictionary();
                            foreach (var key in statMods.Keys) existingMods[key] = statMods[key];
                            break;
                        }
                    }
                }
            }
        }
    }

    private void OnUnitDied(Unit unit, bool isPlayer)
    {
        AllUnits.Remove(unit);
        if (isPlayer)
        {
            PlayerUnits.Remove(unit);
            if (PlayerUnits.Count == 0) EndCombat(false);
        }
        else
        {
            EnemyUnits.Remove(unit);
            if (EnemyUnits.Count == 0) EndCombat(true);
        }
    }

    private void EndCombat(bool victory)
    {
        CurrentState = CombatState.CombatEnd;
        EmitSignal(SignalName.CombatEnded, victory);
    }
}
