using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Combat.AI;
using BladeHex.Combat.Commands;
using BladeHex.Combat.Skills;
using BladeHex.Events;
using BladeHex.Map;
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

    public sealed record BattleAnchorState(
        string AnchorId,
        string Source,
        Vector2I Position,
        int Duration,
        bool Destructible,
        int Hp
    );

    [Signal] public delegate void TurnStartedEventHandler(int state);
    [Signal] public delegate void CombatEndedEventHandler(bool victory);
    [Signal] public delegate void SkillUsedEventHandler(Unit caster, string skillEffect, Godot.Collections.Dictionary result);
    [Signal] public delegate void UnitTurnBeganEventHandler(Unit unit, bool isPlayerSide);
    [Signal] public delegate void BattleAnchorCreatedEventHandler(Godot.Collections.Dictionary anchor);
    [Signal] public delegate void BattleAnchorChangedEventHandler(Godot.Collections.Dictionary anchor);
    [Signal] public delegate void BattleAnchorDestroyedEventHandler(string source);

    // ========== 子系统 ==========
    public UnitRegistry Registry { get; } = new();
    public TurnManager Turns { get; private set; } = null!;
    public CombatResultBuilder ResultBuilder { get; private set; } = null!;
    public StatusEffectManager? StatusEffectManagerInstance { get; private set; }
    public SkillCooldownTracker CooldownTracker { get; } = new();
    private readonly Dictionary<long, Dictionary<string, int>> _skillUsesThisBattle = new();
    private readonly Dictionary<string, BattleAnchorState> _battleAnchorsBySource = new();
    private int _lastBattleAnchorRound = 0;

    // ========== 向后兼容属性（委托给 Registry）==========
    public List<Unit> AllUnits => Registry.AllUnits;
    public List<Unit> PlayerUnits => Registry.PlayerUnits;
    public List<Unit> EnemyUnits => Registry.EnemyUnits;
    public IReadOnlyCollection<BattleAnchorState> BattleAnchors => _battleAnchorsBySource.Values;

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
        Turns.PhaseChanged += OnPhaseChanged;
        Turns.UnitTurnStarted += OnUnitTurnStarted;
    }

    public override void _ExitTree()
    {
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
        unit.CombatManager = this;
    }

    public void StartCombat()
    {
        Registry.LockInitialCounts();

        // 重置技能冷却
        CooldownTracker.Reset();
        _skillUsesThisBattle.Clear();
        _battleAnchorsBySource.Clear();
        _lastBattleAnchorRound = 0;

        // 战斗开始时重置所有角色的职业技能状态
        BladeHex.Data.Globals.SkillTreesOrNull?.OnBattleStart();

        // v0.6 11.8 重置所有"每场战斗 1 次"标记
        foreach (var u in Registry.AllUnits)
        {
            if (u.Data == null) continue;
            u.Model.LifeShieldUsedThisCombat = 0;
            u.Model.LifeCircleUsedThisCombat = 0;
            u.Model.LastStandUsedThisCombat = 0;
            u.Model.HeroicCallUsedThisCombat = 0;
            u.Model.ResurrectUsedThisCombat = 0;
        }

        // v1 被动钩子: 设置全局战斗单位列表
        CareerPassiveHooks.SetCombatState(Registry.AllUnits);

        CharacterRenderBus.Instance?.RefreshAllStatus();

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
        // 重置技能冷却
        CooldownTracker.Reset();
        _skillUsesThisBattle.Clear();
        _battleAnchorsBySource.Clear();
        _lastBattleAnchorRound = 0;

        // 战斗开始时重置所有角色的职业技能状态
        BladeHex.Data.Globals.SkillTreesOrNull?.OnBattleStart();

        // 重置所有"每场战斗 1 次"标记
        foreach (var u in Registry.AllUnits)
        {
            if (u.Data == null) continue;
            u.Model.LifeShieldUsedThisCombat = 0;
            u.Model.LifeCircleUsedThisCombat = 0;
            u.Model.LastStandUsedThisCombat = 0;
            u.Model.HeroicCallUsedThisCombat = 0;
            u.Model.ResurrectUsedThisCombat = 0;
        }

        // v1 被动钩子: 设置全局战斗单位列表
        CareerPassiveHooks.SetCombatState(Registry.AllUnits);

        CharacterRenderBus.Instance?.RefreshAllStatus();

        EventBus.Instance?.Publish(EventBus.Signals.CombatStarted, new Godot.Collections.Dictionary
        {
            { "player_count", PlayerUnits.Count },
            { "enemy_count", EnemyUnits.Count },
        });

        Turns.ConfirmDeployment();
    }

    internal void ResetUnitsActions(IEnumerable<Unit> units)
    {
        Registry.ResetActions(units);
        // 每回合开始重置职业技能回合计数
        BladeHex.Data.Globals.SkillTreesOrNull?.OnTurnStart();

    }

    public void EndCurrentTurn()
    {
        // v1 职业被动: 回合结束钩子 (敌法师等)
        if (CurrentInitiativeUnit != null && GodotObject.IsInstanceValid(CurrentInitiativeUnit))
            CareerPassiveHooks.OnTurnEnd(CurrentInitiativeUnit);

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

    public SkillExecutionResult UseSkill(Unit caster, string skillEffect, Vector2I targetCell, Map.HexGrid? grid = null)
    {
        if (!GodotObject.IsInstanceValid(caster) || caster.CurrentHp <= 0)
            return SkillExecutionResult.Fail("施放者无效");

        long casterId = (long)caster.GetInstanceId();
        var cfg = SkillEffectExecutor.GetSkillConfig(skillEffect);
        int apCost = SkillRegistry.GetActionCost(skillEffect, caster, targetCell);

        bool isSpell = SkillRegistry.IsSpell(skillEffect);
        bool freeAp = isSpell && caster.Data?.Runtime?.CareerNextSpellFreeAp == true;

        if (!freeAp && caster.CurrentAp < apCost)
            return SkillExecutionResult.Fail($"AP 不足 (需要 {apCost})");

        if (!caster.HasSkillEffect(skillEffect))
            return SkillExecutionResult.Fail("未拥有该技能");

        if (!SkillRegistry.CanUseWithEquipment(skillEffect, caster, out var equipmentReason))
            return SkillExecutionResult.Fail(equipmentReason);

        // 冷却检查
        if (CooldownTracker.IsOnCooldown(casterId, skillEffect))
        {
            int remaining = CooldownTracker.GetRemainingCooldown(casterId, skillEffect);
            return SkillExecutionResult.Fail($"技能冷却中 (剩余 {remaining} 回合)");
        }

        int usesPerBattle = SkillRegistry.GetUsesPerBattle(skillEffect);
        if (usesPerBattle >= 0)
        {
            int used = 0;
            if (_skillUsesThisBattle.TryGetValue(casterId, out var unitUses))
                unitUses.TryGetValue(skillEffect, out used);
            if (used >= usesPerBattle)
                return SkillExecutionResult.Fail("本场战斗已达到使用次数上限");
        }

        // v0.6 3.3 / 10.0 技能动作限制
        if (isSpell)
        {
            // 法术装备限制 (v0.6 10.0)：必须 Magic Focus、不能持盾、只能布甲
            if (caster.Data != null && !CombatStats.CanCastSpells(caster.Data))
                return SkillExecutionResult.Fail("需要法术媒介，且不能持盾或穿非布甲");

            // v1 职业被动: 沉默/万象代价检查
            if (!CareerPassiveHooks.CanCastSpell(caster))
                return SkillExecutionResult.Fail("无法施法");

            // Mana 检查 — v1 职业被动: 免费法力豁免
            bool freeMana = caster.Data?.Runtime?.CareerNextSpellFreeMana == true;
            int manaCost = SkillRegistry.GetManaCost(skillEffect);
            int effectiveManaCost = caster.Data != null ? SkillTreeKeystoneResolver.ApplySpellManaCost(caster.Data, manaCost) : manaCost;
            int hpCost = caster.Data != null ? SkillTreeKeystoneResolver.GetSpellHpCost(caster.Data, manaCost) : 0;
            if (!freeMana && caster.Data != null && caster.Data.CurrentMana < effectiveManaCost)
                return SkillExecutionResult.Fail($"Mana 不足 (需要 {effectiveManaCost})");
            if (hpCost > 0 && caster.CurrentHp <= hpCost)
                return SkillExecutionResult.Fail($"HP 不足 (需要 {hpCost})");
        }
        else
        {
            // 非 Spell 主动技能：每回合最多 1 次
            if (apCost > 0 && caster.Data != null && caster.Data.Runtime.NonSpellSkillUsedThisTurn)
                return SkillExecutionResult.Fail("本回合已使用过非法术主动技能");
        }

        if (grid != null && SkillRegistry.GetTargetType(skillEffect) != "Self")
        {
            var targetCellData = grid.GetCell(targetCell.X, targetCell.Y);
            var targetUnit = targetCellData?.Occupant;
            if (targetCellData != null && CombatAttackRules.IsMeleeSkillElevationBlocked(caster, skillEffect, targetCellData, grid))
            {
                return SkillExecutionResult.Fail(CombatAttackRules.MeleeElevationBlockedReason);
            }

            if (GodotObject.IsInstanceValid(targetUnit)
                && targetUnit != caster
                && targetUnit!.Data?.IsEnemy != caster.Data?.IsEnemy
                && !BuffTargetingRules.IsDirectlyTargetable(targetUnit))
            {
                return SkillExecutionResult.Fail("目标不可被直接指定");
            }
        }


        var typedResult = SkillEffectExecutor.ExecuteActiveSkill(
            caster, skillEffect, targetCell, grid,
            AllUnits, PlayerUnits, EnemyUnits
        );

        if (!typedResult.Success)
        {
            return SkillExecutionResult.Fail(typedResult.FailureReason ?? "未知原因");
        }

        // v1 职业被动: 鏖战骑士/天启骑士 — 免费法术不消耗 AP
        if (!freeAp)
        {
            caster.ConsumeAp(apCost);
        }
        else if (caster.Data?.Runtime != null)
        {
            caster.Data.Runtime.CareerNextSpellFreeAp = false; // 消耗标记
        }
        if (apCost > 0 || isSpell)
            caster.HasActed = true;

        // 扣 Mana / 标记非 Spell 主动技能
        if (isSpell && caster.Data != null)
        {
            int baseManaCost = SkillRegistry.GetManaCost(skillEffect);
            int effectiveManaCost = SkillTreeKeystoneResolver.ApplySpellManaCost(caster.Data, baseManaCost);
            int hpCost = SkillTreeKeystoneResolver.GetSpellHpCost(caster.Data, baseManaCost);
            int actualManaCost = CareerPassiveHooks.ModifySpellManaCost(caster, effectiveManaCost);
            caster.Model.CurrentMana = System.Math.Max(0, caster.Model.CurrentMana - actualManaCost);
            caster.Data.CurrentMana = caster.Model.CurrentMana;
            if (hpCost > 0)
            {
                caster.SetHp(System.Math.Max(1, caster.CurrentHp - hpCost));
            }
            // v1 职业被动: 血契之环 — 消耗法力时等额回血
            if (actualManaCost > 0)
                CareerPassiveHooks.OnManaSpent(caster, actualManaCost);
        }
        else if (apCost > 0 && caster.Data != null)
        {
            caster.Model.NonSpellSkillUsedThisTurn = true;
        }

        // v1 职业被动: 施法后钩子 (焰风之怒/幻术师/鏖战骑士/魔武者)
        if (isSpell)
            CareerPassiveHooks.OnSpellCast(caster);

        // 进入冷却
        int skillCooldown = SkillCooldownTracker.GetSkillCooldown(caster, skillEffect);
        if (skillCooldown > 0)
            CooldownTracker.UseSkill(casterId, skillEffect, skillCooldown);

        if (usesPerBattle >= 0)
        {
            if (!_skillUsesThisBattle.TryGetValue(casterId, out var unitUses))
            {
                unitUses = new Dictionary<string, int>();
                _skillUsesThisBattle[casterId] = unitUses;
            }
            unitUses.TryGetValue(skillEffect, out int used);
            unitUses[skillEffect] = used + 1;
        }

        if (StatusEffectManagerInstance != null) ApplyStatusEffects(typedResult, caster);
        RegisterBattleAnchors(typedResult);

        // 处理传送类结果 — 更新格子占用
        ProcessTeleportResults(typedResult, grid);

        EventBus.Instance?.PublishSkillUsed(caster, skillEffect, true);
        EmitSignal(SignalName.SkillUsed, caster, skillEffect, typedResult.ToDictionary());
        return typedResult;
    }

    // ========== 职业技能释放 ==========

    /// <summary>使用职业专属技能</summary>
    public SkillExecutionResult UseCareerSkill(Unit caster, Vector2I targetCell, Map.HexGrid? grid = null)
    {
        if (!GodotObject.IsInstanceValid(caster) || caster.CurrentHp <= 0)
            return SkillExecutionResult.Fail("施放者无效");

        var careerSkill = caster.GetCareerSkill();
        if (grid != null && careerSkill != null)
        {
            var targetCellData = grid.GetCell(targetCell.X, targetCell.Y);
            if (targetCellData != null && CombatAttackRules.IsMeleeCareerElevationBlocked(caster, careerSkill, targetCellData, grid))
            {
                return SkillExecutionResult.Fail(CombatAttackRules.MeleeElevationBlockedReason);
            }
        }

        var typedResult = CareerSkillExecutor.ExecuteCareerSkill(
            caster, targetCell, grid,
            AllUnits, PlayerUnits, EnemyUnits
        );

        if (!typedResult.Success)
        {
            return SkillExecutionResult.Fail(typedResult.FailureReason ?? "未知原因");
        }

        caster.HasActed = true;

        if (StatusEffectManagerInstance != null) ApplyStatusEffects(typedResult, caster);
        RegisterBattleAnchors(typedResult);

        // 处理传送类结果 — 更新格子占用
        ProcessTeleportResults(typedResult, grid);

        string effectId = caster.GetCareerSkill()?.EffectId ?? "career_skill";
        EventBus.Instance?.PublishSkillUsed(caster, effectId, true);
        EmitSignal(SignalName.SkillUsed, caster, effectId, typedResult.ToDictionary());
        return typedResult;
    }

    /// <summary>把技能结果中的 Core 模型映射回运行时 Unit。优先引用匹配，避免默认 CharacterId 误配。</summary>
    private Unit? FindRuntimeUnit(BattleUnitModel targetModel)
    {
        var targetData = targetModel.Data;
        var byReference = AllUnits.FirstOrDefault(u =>
            GodotObject.IsInstanceValid(u)
            && (ReferenceEquals(u.Model, targetModel) || ReferenceEquals(u.Model.Data, targetData) || ReferenceEquals(u.Data, targetData)));
        if (byReference != null) return byReference;

        int characterId = targetData?.CharacterId ?? -1;
        if (characterId >= 0)
        {
            return AllUnits.FirstOrDefault(u =>
                GodotObject.IsInstanceValid(u)
                && u.Data != null
                && u.Data.CharacterId == characterId);
        }

        return null;
    }

    /// <summary>处理技能结果中的传送操作 — 更新格子占用</summary>
    private void ProcessTeleportResults(SkillExecutionResult typedResult, Map.HexGrid? grid)
    {
        if (grid == null || !typedResult.Success) return;
        foreach (var sub in typedResult.SubResults)
        {
            if (sub is TeleportEvent tp)
            {
                var target = FindRuntimeUnit(tp.Unit);
                if (!GodotObject.IsInstanceValid(target)) continue;

                // 清除旧格占用（支持多格单位）
                if (tp.PreviousPosition.HasValue)
                {
                    if (target.OccupiedCells != null && target.OccupiedCells.Length > 0)
                    {
                        foreach (var cellPos in target.OccupiedCells)
                        {
                            var c = grid.GetCell(cellPos.X, cellPos.Y);
                            if (c != null && c.Occupant == target) c.Occupant = null;
                        }
                    }
                    else
                    {
                        var origin = tp.PreviousPosition.Value;
                        var oldCell = grid.GetCell(origin.X, origin.Y);
                        if (oldCell != null && oldCell.Occupant == target) oldCell.Occupant = null;
                    }
                }

                // 设置新格占用（支持多格单位）
                var dest = tp.Destination;
                var newCells = UnitFootprint.GetFootprintCells(dest, target.FootprintW, target.FootprintH);
                target.OccupiedCells = newCells;
                foreach (var cellPos in newCells)
                {
                    var newCell = grid.GetCell(cellPos.X, cellPos.Y);
                    if (newCell != null) newCell.Occupant = target;
                }
            }
        }
    }

    private void ApplyStatusEffects(SkillExecutionResult typedResult, Unit? sourceUnit = null)
    {
        if (!typedResult.Success) return;
        foreach (var sub in typedResult.SubResults)
        {
            if (sub is StatusEffectApplication eff)
            {
                var target = FindRuntimeUnit(eff.Target);
                if (!GodotObject.IsInstanceValid(target)) continue;

                if (eff.Special == StatusEffectSpecial.RemoveEffects)
                {
                    StatusEffectManagerInstance?.RemoveEffect(target, eff.EffectId);
                    continue;
                }
                if (eff.Special == StatusEffectSpecial.RemoveAllNegative)
                {
                    StatusEffectManagerInstance?.RemoveAllNegative(target);
                    continue;
                }

                if (!string.IsNullOrEmpty(eff.EffectId))
                {
                    StatusEffectManagerInstance?.ApplyEffect(target, eff.EffectId, eff.Duration, sourceUnit);
                }
            }
        }
    }

    private void RegisterBattleAnchors(SkillExecutionResult typedResult)
    {
        if (!typedResult.Success) return;
        foreach (var sub in typedResult.SubResults)
        {
            if (sub is not BattleAnchorEvent anchor) continue;
            _lastBattleAnchorRound = Turns.TurnNumber;
            var state = new BattleAnchorState(
                anchor.AnchorId,
                anchor.Source,
                anchor.Position,
                anchor.Duration,
                anchor.Destructible,
                anchor.Hp);
            bool alreadyRegistered = _battleAnchorsBySource.ContainsKey(anchor.Source);
            _battleAnchorsBySource[anchor.Source] = state;
            EmitSignal(
                alreadyRegistered ? SignalName.BattleAnchorChanged : SignalName.BattleAnchorCreated,
                ToBattleAnchorDictionary(state));
        }
    }

    public bool TryGetBattleAnchor(string source, out BattleAnchorState anchor)
        => _battleAnchorsBySource.TryGetValue(source, out anchor!);

    public bool TryGetBattleAnchorAt(Vector2I position, out BattleAnchorState anchor)
    {
        foreach (var item in _battleAnchorsBySource.Values)
        {
            if (item.Position == position)
            {
                anchor = item;
                return true;
            }
        }

        anchor = default!;
        return false;
    }

    public bool DamageBattleAnchor(string source, int damage)
    {
        if (string.IsNullOrEmpty(source) || damage <= 0) return false;
        if (!_battleAnchorsBySource.TryGetValue(source, out var anchor) || !anchor.Destructible)
            return false;

        int remainingHp = Math.Max(0, anchor.Hp - damage);
        if (remainingHp <= 0)
            return DestroyBattleAnchor(source);

        var updated = anchor with { Hp = remainingHp };
        _battleAnchorsBySource[source] = updated;
        EmitSignal(SignalName.BattleAnchorChanged, ToBattleAnchorDictionary(updated));
        return true;
    }

    public bool DestroyBattleAnchor(string source)
    {
        if (string.IsNullOrEmpty(source)) return false;
        if (!_battleAnchorsBySource.TryGetValue(source, out var anchor) || !anchor.Destructible)
            return false;

        RemoveBattleAnchor(source);
        return true;
    }

    private void RemoveBattleAnchor(string source)
    {
        foreach (var unit in AllUnits)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.Data == null) continue;
            BladeHex.Combat.Buff.BuffSystem.RemoveBySource(unit.Data, source);
        }

        _battleAnchorsBySource.Remove(source);
        CharacterRenderBus.Instance?.RefreshAllStatus();
        EmitSignal(SignalName.BattleAnchorDestroyed, source);
    }

    private static Godot.Collections.Dictionary ToBattleAnchorDictionary(BattleAnchorState anchor) => new()
    {
        { "anchor_id", anchor.AnchorId },
        { "source", anchor.Source },
        { "q", anchor.Position.X },
        { "r", anchor.Position.Y },
        { "duration", anchor.Duration },
        { "destructible", anchor.Destructible },
        { "hp", anchor.Hp },
    };

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

    /// <summary>已死亡单位的 InstanceId 集合，用于幂等保护。</summary>
    private readonly HashSet<long> _deadUnitIds = new();

    /// <summary>
    /// 统一的死亡处理入口（SSOT）。
    /// 负责：先攻队列维护、格子占用清理、UI 更新、战斗结束判定。
    /// 幂等：同一单位的多次调用只生效一次。
    /// </summary>
    public void HandleUnitKilled(Unit dead, Unit? killer = null)
    {
        if (dead == null || !GodotObject.IsInstanceValid(dead)) return;

        long deadId = (long)dead.GetInstanceId();
        // 幂等保护
        if (_deadUnitIds.Contains(deadId)) return;
        _deadUnitIds.Add(deadId);

        // 统一把单位从 Registry 移出 (SSOT)
        Registry.RemoveUnit(dead);

        // 清理冷却记录
        CooldownTracker.RemoveUnit(deadId);

        bool isPlayer = dead.IsPlayerSide;

        // 1. 维护先攻队列
        Turns.OnUnitDied(deadId);

        // 2. 清理格子占用
        if (CurrentGrid != null)
        {
            // 多格单位：清理所有占用格
            if (dead.OccupiedCells != null && dead.OccupiedCells.Length > 0)
            {
                foreach (var cellPos in dead.OccupiedCells)
                {
                    var c = CurrentGrid.GetCell(cellPos.X, cellPos.Y);
                    if (c != null && c.Occupant == dead)
                        c.Occupant = null;
                }
            }
            else
            {
                var cell = CurrentGrid.GetCell(dead.GridPos.X, dead.GridPos.Y);
                if (cell != null && cell.Occupant == dead)
                    cell.Occupant = null;
            }
        }

        // 3. 发布事件（供 UI/SFX 订阅处理）
        EventBus.Instance?.PublishUnitDied(dead, isPlayer);

        // 4. v0.8 E4-B: 灵风大师-风之眷顾 → on-kill 叠层
        if (GodotObject.IsInstanceValid(killer)
            && killer.CurrentHp > 0
            && CareerSkillResolver.HasWindFavor(killer))
        {
            CareerSkillResolver.AddWindStack(killer);
        }

        // 5. 检查战斗结束
        ResolveCombatEndAfterDeath(isPlayer);
    }

    private void ResolveCombatEndAfterDeath(bool isPlayer)
    {
        // 注意：PlayerUnits / EnemyUnits 列表只有在单位节点被 QueueFree 后才会减少。
        // 游戏逻辑中死亡单位不立即释放节点（保留尸体/动画），因此必须按 CurrentHp 统计存活数。
        int alivePlayers = PlayerUnits.Count(u => GodotObject.IsInstanceValid(u) && u.CurrentHp > 0);
        int aliveEnemies = EnemyUnits.Count(u => GodotObject.IsInstanceValid(u) && u.CurrentHp > 0);

        if (alivePlayers == 0)
        {
            EndCombat(false);
        }
        else if (aliveEnemies == 0)
        {
            EndCombat(true);
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
            // 递减技能冷却
            CooldownTracker.OnTurnStart(unitId);

            BladeHex.Data.Globals.SkillTreesOrNull?.OnTurnStart();

            // Buff tick
            BuffTurnApplier.ApplyTurnStart(unit);
            AdvanceBattleAnchorDurations(Turns.TurnNumber);

            // (恐惧光环已移除)

            // v1 山岳之王: 不动如山回合递减
            if (unit.Data?.Runtime.CareerMountainThroneTurns > 0)
            {
                unit.Data.Runtime.CareerMountainThroneTurns--;
            }

            // v1 荒原之心: 狂暴回合递减
            if (unit.Data?.Runtime.CareerWarchiefDamageBonusTurns > 0)
            {
                unit.Data.Runtime.CareerWarchiefDamageBonusTurns--;
            }

            // v1 秘院贤师: 每回合第一次法术免费
            if (unit.HasCareerSkillEffect("archsage_ally_spell_damage") && unit.Data?.Runtime != null)
            {
                unit.Data.Runtime.CareerNextSpellFreeMana = true;
            }

            // v0.8 E4-C: 老兵-临危不乱 → HP首次降至50%以下触发AC+3
            if (CareerSkillResolver.HasOldTimer(unit)
                && unit.Data != null
                && unit.Data.Runtime.OldTimerTriggeredThisCombat == 0
                && unit.CurrentHp < unit.Model.GetMaxHp() / 2)
            {
                unit.Model.OldTimerTriggeredThisCombat = 1;
                // 授予AC+3 效果持续到战斗结束 — 通过buff实现
                var inst = BladeHex.Combat.Buff.BuffSystem.Apply(unit.Data, "old_timer_ac", 99, sourceUnitId: (int)unit.GetInstanceId());
                if (inst != null)
                {
                    var acMod = inst.Modifiers.Find(m => m.Stat == "ac");
                    if (acMod != null) acMod.Value = 3;
                }
            }

            EmitSignal(SignalName.UnitTurnBegan, unit, isPlayerSide);
        }
    }

    private void AdvanceBattleAnchorDurations(int currentRound)
    {
        if (_battleAnchorsBySource.Count == 0)
        {
            _lastBattleAnchorRound = currentRound;
            return;
        }
        if (currentRound <= _lastBattleAnchorRound) return;

        int elapsedRounds = currentRound - _lastBattleAnchorRound;
        _lastBattleAnchorRound = currentRound;
        foreach (var anchor in _battleAnchorsBySource.Values.ToArray())
        {
            if (anchor.Duration < 0) continue;

            int remainingDuration = Math.Max(0, anchor.Duration - elapsedRounds);
            if (remainingDuration <= 0)
            {
                RemoveBattleAnchor(anchor.Source);
                continue;
            }

            var updated = anchor with { Duration = remainingDuration };
            _battleAnchorsBySource[anchor.Source] = updated;
            EmitSignal(SignalName.BattleAnchorChanged, ToBattleAnchorDictionary(updated));
        }
    }

    private void EndCombat(bool victory)
    {
        // 防重入：如果已经在 CombatEnd 状态则忽略
        if (CurrentState == CombatState.CombatEnd) return;

        // v1 被动钩子: 清理全局战斗引用
        CareerPassiveHooks.ClearCombatState();

        Turns.EndCombat();

        var eventData = ResultBuilder.BuildEventData(victory);
        EventBus.Instance?.Publish(EventBus.Signals.CombatEnded, eventData);
        EmitSignal(SignalName.CombatEnded, victory);
    }
}
