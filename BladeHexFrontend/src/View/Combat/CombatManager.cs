using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Combat.AI;
using BladeHex.Combat.Commands;
using BladeHex.Events;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 战斗流程总控 Facade
/// 协调 UnitRegistry / TurnManager / CombatResultBuilder / StatusEffectManager
/// 对外保持原有 API 不变，内部职责已拆分。
/// </summary>
[GlobalClass]
public partial class CombatManager : Node
{
    public enum CombatState { Init, Deployment, PlayerTurn, EnemyTurn, CombatEnd }

    [Signal] public delegate void TurnStartedEventHandler(int state);
    [Signal] public delegate void CombatEndedEventHandler(bool victory);
    [Signal] public delegate void SkillUsedEventHandler(Unit caster, string skillEffect, Godot.Collections.Dictionary result);
    [Signal] public delegate void UnitTurnBeganEventHandler(Unit unit, bool isPlayerSide);

    // ========== 子系统 ==========
    public UnitRegistry Registry { get; } = new();
    public TurnManager Turns { get; private set; } = null!;
    public CombatResultBuilder ResultBuilder { get; private set; } = null!;
    public StatusEffectManager? StatusEffectManagerInstance { get; private set; }

    // ========== 向后兼容属性（委托给 Registry）==========
    public List<Unit> AllUnits => Registry.AllUnits;
    public List<Unit> PlayerUnits => Registry.PlayerUnits;
    public List<Unit> EnemyUnits => Registry.EnemyUnits;

    public CombatState CurrentState { get; private set; } = CombatState.Init;
    public Unit? ActiveUnit { get; set; }

    /// <summary>当前先攻行动单位（由 TurnManager 驱动）</summary>
    public Unit? CurrentInitiativeUnit { get; private set; }

    public AIDifficultyConfig? DifficultyConfig { get; set; }

    /// <summary>命令历史 — 记录所有操作，支持回放</summary>
    public CommandHistory CommandHistory { get; } = new();

    public CombatManager()
    {
        Turns = new TurnManager(Registry);
        ResultBuilder = new CombatResultBuilder(Registry);
    }

    public override void _Ready()
    {
        StatusEffectManagerInstance = new StatusEffectManager();
        AddChild(StatusEffectManagerInstance);

        // 订阅子系统事件
        Registry.UnitDied += OnUnitDied;
        Turns.PhaseChanged += OnPhaseChanged;
        Turns.UnitTurnStarted += OnUnitTurnStarted;
    }

    public override void _ExitTree()
    {
        Registry.UnitDied -= OnUnitDied;
        Turns.PhaseChanged -= OnPhaseChanged;
        Turns.UnitTurnStarted -= OnUnitTurnStarted;
    }

    // ========== 公共 API（保持向后兼容）==========

    public void SetDifficulty(AIDifficultyConfig config) => DifficultyConfig = config;

    public AIDifficultyConfig GetDifficultyConfig()
    {
        if (DifficultyConfig == null) DifficultyConfig = new AIDifficultyConfig();
        return DifficultyConfig;
    }

    public void RegisterUnit(Unit unit, bool isPlayer)
    {
        Registry.RegisterUnit(unit, isPlayer, CommandHistory);
    }

    public void StartCombat()
    {
        Registry.LockInitialCounts();

        // 战斗开始时重置所有角色的职业技能状态
        BladeHex.Data.Globals.SkillTreesOrNull?.OnBattleStart();

        // v0.6 11.8 重置所有"每场战斗 1 次"标记
        foreach (var u in Registry.AllUnits)
        {
            if (u.Data == null) continue;
            u.Data.Runtime.LifeShieldUsedThisCombat = 0;
            u.Data.Runtime.LifeCircleUsedThisCombat = 0;
            u.Data.Runtime.LastStandUsedThisCombat = 0;
            u.Data.Runtime.HeroicCallUsedThisCombat = 0;
            u.Data.Runtime.ResurrectUsedThisCombat = 0;
        }

        EventBus.Instance?.Publish(EventBus.Signals.CombatStarted, new Godot.Collections.Dictionary
        {
            { "player_count", PlayerUnits.Count },
            { "enemy_count", EnemyUnits.Count },
        });

        Turns.StartCombat();
    }

    /// <summary>进入部署阶段 — 玩家手动放置单位</summary>
    public void EnterDeployment()
    {
        Registry.LockInitialCounts();
        CurrentState = CombatState.Deployment;
        Turns.EnterDeployment();
        EmitSignal(SignalName.TurnStarted, (int)CombatState.Deployment);
    }

    /// <summary>确认部署完毕，正式开始战斗</summary>
    public void ConfirmDeployment()
    {
        // 战斗开始时重置所有角色的职业技能状态
        BladeHex.Data.Globals.SkillTreesOrNull?.OnBattleStart();

        // 重置所有"每场战斗 1 次"标记
        foreach (var u in Registry.AllUnits)
        {
            if (u.Data == null) continue;
            u.Data.Runtime.LifeShieldUsedThisCombat = 0;
            u.Data.Runtime.LifeCircleUsedThisCombat = 0;
            u.Data.Runtime.LastStandUsedThisCombat = 0;
            u.Data.Runtime.HeroicCallUsedThisCombat = 0;
            u.Data.Runtime.ResurrectUsedThisCombat = 0;
        }

        EventBus.Instance?.Publish(EventBus.Signals.CombatStarted, new Godot.Collections.Dictionary
        {
            { "player_count", PlayerUnits.Count },
            { "enemy_count", EnemyUnits.Count },
        });

        Turns.ConfirmDeployment();
    }

    /// <remarks>
    /// Deprecated: Use TurnManager for state transitions. Direct ChangeState may desync TurnManager.
    /// Kept for backward compatibility with Godot signal bindings.
    /// </remarks>
    public void ChangeState(CombatState newState)
    {
        // 向后兼容：直接调用时同步到 TurnManager
        CurrentState = newState;
        if (CurrentState == CombatState.PlayerTurn) ResetUnitsActions(PlayerUnits);
        else if (CurrentState == CombatState.EnemyTurn) ResetUnitsActions(EnemyUnits);
        EmitSignal(SignalName.TurnStarted, (int)CurrentState);
        EventBus.Instance?.Publish(EventBus.Signals.TurnStarted, new Godot.Collections.Dictionary
        {
            { "state", (int)CurrentState },
        });
    }

    internal void ResetUnitsActions(IEnumerable<Unit> units)
    {
        Registry.ResetActions(units);
        // 每回合开始重置职业技能回合计数
        BladeHex.Data.Globals.SkillTreesOrNull?.OnTurnStart();

    }

    public void EndCurrentTurn()
    {
        // 插入回合边界标记,Undo 不越过此边界
        CommandHistory.MarkTurnBoundary(GetCommandContext());
        Turns.EndCurrentTurn();
    }

    // ========== 命令系统 API ==========

    /// <summary>当前 HexGrid 引用(由 CombatScene 在初始化时设置)</summary>
    public BladeHex.Map.HexGrid? CurrentGrid { get; set; }

    /// <summary>
    /// 执行命令并入栈(玩家/AI 统一入口)
    /// </summary>
    public CommandResult ExecuteCommand(ICommand cmd)
    {
        var ctx = GetCommandContext();
        return CommandHistory.Execute(cmd, ctx);
    }

    /// <summary>
    /// 尝试撤销最近一个可撤销命令(悔棋)
    /// </summary>
    public bool TryUndoLast()
    {
        var ctx = GetCommandContext();
        bool success = CommandHistory.TryUndoLast(ctx);
        if (success)
        {
            EventBus.Instance?.Publish("command_history_changed", new Godot.Collections.Dictionary
            {
                { "can_undo", CommandHistory.CanUndo },
                { "undoable_count", CommandHistory.UndoableCount },
            });
        }
        return success;
    }

    private CommandContext GetCommandContext() => new()
    {
        Registry = Registry,
        Grid = CurrentGrid,
        EventBus = EventBus.Instance,
    };

    // ========== 技能释放 ==========

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

        // v0.6 3.3 / 10.0 技能动作限制
        bool isSpell = SkillRegistry.IsSpell(skillEffect);
        if (isSpell)
        {
            // 法术装备限制 (v0.6 10.0)：必须 Magic Focus、不能持盾、只能布甲
            if (caster.Data != null && !CombatStats.CanCastSpells(caster.Data))
                return new Godot.Collections.Dictionary { { "success", false }, { "reason", "需要法术媒介，且不能持盾或穿非布甲" } };

            // Mana 检查
            int manaCost = SkillRegistry.GetManaCost(skillEffect);
            if (caster.Data != null && caster.Data.CurrentMana < manaCost)
                return new Godot.Collections.Dictionary { { "success", false }, { "reason", $"Mana 不足 (需要 {manaCost})" } };
        }
        else
        {
            // 非 Spell 主动技能：每回合最多 1 次
            if (caster.Data != null && caster.Data.Runtime.NonSpellSkillUsedThisTurn)
                return new Godot.Collections.Dictionary { { "success", false }, { "reason", "本回合已使用过非法术主动技能" } };
        }

        if (grid != null && SkillRegistry.GetTargetType(skillEffect) != "Self")
        {
            var targetCellData = grid.GetCell(targetCell.X, targetCell.Y);
            var targetUnit = targetCellData?.Occupant;
            if (GodotObject.IsInstanceValid(targetUnit)
                && targetUnit != caster
                && targetUnit!.Data?.IsEnemy != caster.Data?.IsEnemy
                && !BuffTargetingRules.IsDirectlyTargetable(targetUnit))
            {
                return new Godot.Collections.Dictionary { { "success", false }, { "reason", "目标不可被直接指定" } };
            }
        }


        var result = SkillEffectExecutor.ExecuteActiveSkill(
            caster, skillEffect, targetCell, grid,
            AllUnits, PlayerUnits, EnemyUnits
        );

        if (!result.ContainsKey("success") || !result["success"].AsBool()) return result;

        caster.ConsumeAp(apCost);
        caster.HasActed = true;

        // 扣 Mana / 标记非 Spell 主动技能
        if (isSpell && caster.Data != null)
        {
            int manaCost = SkillRegistry.GetManaCost(skillEffect);
            caster.Data.CurrentMana = System.Math.Max(0, caster.Data.CurrentMana - manaCost);
        }
        else if (caster.Data != null)
        {
            caster.Data.Runtime.NonSpellSkillUsedThisTurn = true;
        }

        if (StatusEffectManagerInstance != null) ApplyStatusEffects(result);

        // 处理传送类结果 — 更新格子占用
        ProcessTeleportResults(result, grid);

        EventBus.Instance?.PublishSkillUsed(caster, skillEffect, true);
        EmitSignal(SignalName.SkillUsed, caster, skillEffect, result);
        return result;
    }

    // ========== 职业技能释放 ==========

    /// <summary>使用职业专属技能</summary>
    public Godot.Collections.Dictionary UseCareerSkill(Unit caster, Vector2I targetCell, Map.HexGrid? grid = null)
    {
        if (!GodotObject.IsInstanceValid(caster) || caster.CurrentHp <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "reason", "施放者无效" } };

        var result = CareerSkillExecutor.ExecuteCareerSkill(
            caster, targetCell, grid,
            AllUnits, PlayerUnits, EnemyUnits
        );

        if (!result.ContainsKey("success") || !result["success"].AsBool()) return result;

        caster.HasActed = true;

        if (StatusEffectManagerInstance != null) ApplyStatusEffects(result);

        // 处理传送类结果 — 更新格子占用
        ProcessTeleportResults(result, grid);

        string effectId = caster.GetCareerSkill()?.EffectId ?? "career_skill";
        EventBus.Instance?.PublishSkillUsed(caster, effectId, true);
        EmitSignal(SignalName.SkillUsed, caster, effectId, result);
        return result;
    }

    /// <summary>处理技能结果中的传送操作 — 更新格子占用</summary>
    private void ProcessTeleportResults(Godot.Collections.Dictionary skillResult, Map.HexGrid? grid)
    {
        if (grid == null || !skillResult.ContainsKey("results")) return;
        var results = skillResult["results"].AsGodotArray();
        foreach (var rVar in results)
        {
            if (rVar.VariantType != Variant.Type.Dictionary) continue;
            var r = rVar.AsGodotDictionary();
            if (!r.ContainsKey("type") || r["type"].AsString() != "teleport") continue;

            var target = r.ContainsKey("target") ? r["target"].As<Unit>() : null;
            if (!GodotObject.IsInstanceValid(target)) continue;

            // 清除旧格占用
            if (r.ContainsKey("origin"))
            {
                var origin = r["origin"].AsVector2I();
                var oldCell = grid.GetCell(origin.X, origin.Y);
                if (oldCell != null && oldCell.Occupant == target) oldCell.Occupant = null;
            }

            // 设置新格占用
            var dest = r.ContainsKey("destination") ? r["destination"].AsVector2I()
                     : r.ContainsKey("new_pos") ? r["new_pos"].AsVector2I()
                     : target.GridPos;
            var newCell = grid.GetCell(dest.X, dest.Y);
            if (newCell != null) newCell.Occupant = target;
        }
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
                    var existingEffect = target.Data.Runtime.ActiveStatusEffects
                        .FirstOrDefault(e => e.Id == effectId);
                    if (existingEffect != null)
                    {
                        foreach (var key in statMods.Keys)
                            existingEffect.StatModifiers[key.AsString()] = statMods[key].AsSingle();
                    }
                }
            }
        }
    }

    // ========== 内部事件处理 ==========

    private void OnPhaseChanged(TurnManager.TurnPhase phase)
    {
        CurrentState = phase switch
        {
            TurnManager.TurnPhase.Deployment => CombatState.Deployment,
            TurnManager.TurnPhase.PlayerTurn => CombatState.PlayerTurn,
            TurnManager.TurnPhase.EnemyTurn => CombatState.EnemyTurn,
            TurnManager.TurnPhase.CombatEnd => CombatState.CombatEnd,
            _ => CombatState.Init,
        };
        EmitSignal(SignalName.TurnStarted, (int)CurrentState);
        EventBus.Instance?.Publish(EventBus.Signals.TurnStarted, new Godot.Collections.Dictionary
        {
            { "state", (int)CurrentState },
        });
    }

    private void OnUnitDied(Unit unit, bool isPlayer)
    {
        // 通知先攻队列移除该单位
        Turns.OnUnitDied((long)unit.GetInstanceId());

        ResolveCombatEndAfterDeath(isPlayer);
    }

    /// <summary>
    /// 显式死亡收口：用于战斗结算已确认死亡，但 Node 退出树事件尚未触发的场景。
    /// 保持与 UnitRegistry.TreeExited 路径幂等，避免死亡单位继续留在先攻队列。
    /// </summary>
    public void HandleUnitKilled(Unit dead, Unit? killer = null)
    {
        if (dead == null || !GodotObject.IsInstanceValid(dead)) return;

        bool isPlayer = dead.IsPlayerSide;
        Turns.OnUnitDied((long)dead.GetInstanceId());
        ResolveCombatEndAfterDeath(isPlayer);
    }

    private void ResolveCombatEndAfterDeath(bool isPlayer)
    {

        if (isPlayer)
        {
            if (PlayerUnits.Count == 0) EndCombat(false);
        }
        else
        {
            if (EnemyUnits.Count == 0) EndCombat(true);
        }
    }

    private void OnUnitTurnStarted(long unitId, bool isPlayerSide)
    {
        // 找到对应的 Unit 节点
        Unit? unit = null;
        foreach (var u in AllUnits)
        {
            if (GodotObject.IsInstanceValid(u) && (long)u.GetInstanceId() == unitId)
            {
                unit = u;
                break;
            }
        }

        CurrentInitiativeUnit = unit;

        // 重置该单位的回合状态 + Buff tick
        if (unit != null)
        {
            BladeHex.Data.Globals.SkillTreesOrNull?.OnTurnStart();

            // Buff tick
            BuffTurnApplier.ApplyTurnStart(unit);

            EmitSignal(SignalName.UnitTurnBegan, unit, isPlayerSide);
        }
    }

    private void EndCombat(bool victory)
    {
        // 防重入：如果已经在 CombatEnd 状态则忽略
        if (CurrentState == CombatState.CombatEnd) return;

        Turns.EndCombat();

        var eventData = ResultBuilder.BuildEventData(victory);
        EventBus.Instance?.Publish(EventBus.Signals.CombatEnded, eventData);
        EmitSignal(SignalName.CombatEnded, victory);
    }
}
