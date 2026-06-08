using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BladeHex.Combat;
using BladeHex.Combat.Skills;
using BladeHex.Data;
using BladeHex.Events;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat.AI;

/// <summary>
/// AI 主控制器 —— 编排所有敌方单位的回合行动
/// 对应策划案 09-AI系统 → 五、决策主流程
/// </summary>
[GlobalClass]
public partial class AIController : Node
{
    [Signal] public delegate void AllActionsCompletedEventHandler();

    private AIDifficultyConfig _difficultyConfig = null!;
    private readonly Dictionary<UnitData.AIStrategy, AIStrategyBase> _strategies = new();
    private CombatManager? _combatManager;

    public void SetCombatManager(CombatManager manager)
    {
        _combatManager = manager;
    }

    // 战斗场景引用（目前假设为 Node，具体可能需要接口或基类）
    // 战斗场景适配器（类型化接口，替代 HasMethod/Call 反射）
    private ICombatSceneAdapter? _adapter;

    // 所有单位缓存（用于士气系统跨阵营结算）
    private List<Unit> _allUnitsCache = new();

    // 攻击动画编排器（由 CombatSceneBase 注入）
    private CombatAttackAnimator _attackAnimator = null!;

    // 攻城结构检测缓存（每场战斗初始化一次）
    private bool _hasSiegeStructures = false;

    /// <summary>难度配置，可通过属性注入</summary>
    [Export]
    public AIDifficultyConfig? DifficultyConfig
    {
        get => _difficultyConfig;
        set => _difficultyConfig = value ?? new AIDifficultyConfig();
    }

    /// <summary>无参初始化，可调用</summary>
    public void Initialize()
    {
        _difficultyConfig ??= new AIDifficultyConfig();
        InitStrategies();
    }

    private void InitStrategies()
    {
        var reckless = new AIStrategyReckless(_difficultyConfig);
        var cautious = new AIStrategyCautious(_difficultyConfig);
        var tactical = new AIStrategyTactical(_difficultyConfig);
        var instinct = new AIStrategyInstinct(_difficultyConfig);

        _strategies[UnitData.AIStrategy.Reckless] = reckless;
        _strategies[UnitData.AIStrategy.Berserk] = reckless;
        _strategies[UnitData.AIStrategy.Intimidate] = reckless;
        _strategies[UnitData.AIStrategy.Cautious] = cautious;
        _strategies[UnitData.AIStrategy.Tactical] = tactical;
        _strategies[UnitData.AIStrategy.Cunning] = tactical;
        _strategies[UnitData.AIStrategy.Territorial] = tactical;
        _strategies[UnitData.AIStrategy.Instinct] = instinct;
    }

    public void SetCombatScene(ICombatSceneAdapter adapter)
    {
        _adapter = adapter;
    }

    /// <summary>注入攻击动画编排器</summary>
    public void SetAttackAnimator(CombatAttackAnimator animator)
    {
        _attackAnimator = animator;
    }

    /// <summary>主入口：执行所有敌方单位的回合行动</summary>
    public async Task ExecuteEnemyTurn(List<Unit> enemyUnits, List<Unit> playerUnits, HexGrid hexGrid, Node combatUi)
    {
        // 攻城结构检测（每回合检查一次，因为城门可能被破坏）
        _hasSiegeStructures = AISiegeEvaluator.HasSiegeStructures(hexGrid);

        // 构建全单位缓存（士气系统需要跨阵营结算）
        _allUnitsCache = new List<Unit>(enemyUnits.Count + playerUnits.Count);
        _allUnitsCache.AddRange(enemyUnits);
        _allUnitsCache.AddRange(playerUnits);

        // 按策略优先级排序执行（战术 > 谨慎 > 鲁莽 > 本能）
        var sortedEnemies = SortByPriority(enemyUnits);

        foreach (var enemy in sortedEnemies)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;

            // 多动作循环：持续行动直到 AP 耗尽或无法行动
            const int MaxActionsPerUnit = 5; // 防止无限循环
            for (int actionIdx = 0; actionIdx < MaxActionsPerUnit; actionIdx++)
            {
                if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) break;
                if (enemy.CurrentAp < 1) break; // AP 不足以做任何事

                // v0.8 E4: cannot_act 检查 — buff 封锁行动
                if (enemy.Data != null)
                {
                    var actLock = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(enemy.Data, "cannot_act");
                    if (actLock.OverrideValue.HasValue && actLock.OverrideValue.Value >= 1f)
                        break; // 跳过本回合
                }

                // v0.8 D1: AI 职业大招决策（在 Move/Attack/Retreat 之前）
                if (TryUseCareerSkill(enemy, playerUnits, enemyUnits))
                    break; // 大招释放成功，本回合结束

                // 检查是否还有存活的玩家单位
                var alivePlayerUnits = playerUnits.Where(p => GodotObject.IsInstanceValid(p) && p.CurrentHp > 0).ToList();
                if (alivePlayerUnits.Count == 0) break;

                var action = DecideActionForUnit(enemy, alivePlayerUnits, enemyUnits, hexGrid);

                // Idle/Overwatch 意味着主动结束回合
                if (action.Type == AIAction.ActionType.Idle || action.Type == AIAction.ActionType.Overwatch)
                {
                    await ExecuteAction(action, hexGrid, combatUi);
                    break;
                }

                // 撤退后不再行动
                if (action.Type == AIAction.ActionType.Retreat)
                {
                    await ExecuteAction(action, hexGrid, combatUi);
                    break;
                }

                await ExecuteAction(action, hexGrid, combatUi);

                // 行动间短暂延迟(可被快进倍率缩短)
                await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.3);
            }

            // 单位间延迟(可被快进倍率缩短)
            await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.2);
        }

        EmitSignal(SignalName.AllActionsCompleted);
    }

    public AIAction DecideActionForUnit(Unit actor, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        // 攻城 AI 前置检查：如果地图有城墙结构，优先评估攻城行动
        if (_hasSiegeStructures)
        {
            AIAction? siegeAction = null;
            // 敌方单位（AI 控制）= 攻方 → 攻城行为
            // 注意：这里假设 AI 控制的是攻方；守城战中 AI 是守方
            if (actor.Data!.IsEnemy)
                siegeAction = AISiegeEvaluator.EvaluateAttackerAction(actor, playerUnits, hexGrid);
            else
                siegeAction = AISiegeEvaluator.EvaluateDefenderAction(actor, enemyUnits, hexGrid);

            if (siegeAction != null) return siegeAction;
        }

        // T07: Personality-based strategy selection
        var strategyKey = SelectStrategyByPersonality(actor.Data!);
        if (!_strategies.TryGetValue(strategyKey, out var strategy))
        {
            strategy = _strategies[UnitData.AIStrategy.Instinct];
        }

        // 难度失误注入
        if (GD.Randf() < _difficultyConfig.MistakeChance)
        {
            strategy = _strategies[UnitData.AIStrategy.Instinct];
        }

        return strategy.DecideAction(actor, playerUnits, enemyUnits, hexGrid, _combatManager);
    }

    // T07: Select AI strategy based on personality scores from traits
    private UnitData.AIStrategy SelectStrategyByPersonality(UnitData data)
    {
        // If no traits, use default strategy
        if (data.CharacterTraits == null || data.CharacterTraits.Count == 0)
            return data.aiStrategy;

        var scores = data.GetAllPersonalityScores();

        // Personality-based strategy adjustments
        // calculating ≥ 1 → Tactical +50% weight
        // valor ≥ 1 → Reckless +50% weight
        // calculating ≤ -1 → Cautious +50% weight

        float tacticalWeight = 1.0f;
        float recklessWeight = 1.0f;
        float cautiousWeight = 1.0f;

        if (scores["calculating"] >= 1.0f)
            tacticalWeight += 0.5f;
        if (scores["valor"] >= 1.0f)
            recklessWeight += 0.5f;
        if (scores["calculating"] <= -1.0f)
            cautiousWeight += 0.5f;

        // Weighted random selection
        float totalWeight = tacticalWeight + recklessWeight + cautiousWeight;
        float roll = GD.Randf() * totalWeight;

        if (roll < tacticalWeight)
            return UnitData.AIStrategy.Tactical;
        else if (roll < tacticalWeight + recklessWeight)
            return UnitData.AIStrategy.Reckless;
        else
            return UnitData.AIStrategy.Cautious;
    }

    private async Task ExecuteAction(AIAction action, HexGrid hexGrid, Node combatUi)
    {
        if (action == null || !GodotObject.IsInstanceValid(action.Actor)) return;

        var actor = action.Actor!;

        switch (action.Type)
        {
            case AIAction.ActionType.MoveThenAttack:
                if (!PrepareMoveThenAttack(action, hexGrid))
                {
                    await ExecuteReservedMoveOnly(action, hexGrid, combatUi);
                    actor.HasMoved = true;
                    break;
                }
                await ExecuteMove(action, hexGrid, combatUi);
                if (GodotObject.IsInstanceValid(actor) && actor.CurrentHp > 0)
                {
                    await ExecuteAttack(action, hexGrid, combatUi);
                }
                actor.HasMoved = true;
                actor.HasActed = true;
                break;

            case AIAction.ActionType.Attack:
                await ExecuteAttack(action, hexGrid, combatUi);
                actor.HasActed = true;
                break;

            case AIAction.ActionType.MoveOnly:
                await ExecuteMove(action, hexGrid, combatUi);
                actor.HasMoved = true;
                break;

            case AIAction.ActionType.Retreat:
                _adapter?.LogMessage($"[color=yellow]{action.Description}[/color]");
                await ExecuteMove(action, hexGrid, combatUi);
                actor.HasMoved = true;
                break;

            case AIAction.ActionType.Overwatch:
                _adapter?.LogMessage($"{actor.Data!.UnitName} 进入防御姿态。");
                actor.HasActed = true;
                break;

            case AIAction.ActionType.UseSkill:
                if (action.SkillId == "siege_attack_gate" || action.SkillId == "siege_build_ladder")
                {
                    await ExecuteSiegeSkill(action, hexGrid);
                }
                else
                {
                    if (action.MovePath != null && action.MovePath.Count > 0)
                    {
                        await ExecuteMove(action, hexGrid, combatUi);
                    }
                    if (GodotObject.IsInstanceValid(actor) && actor.CurrentHp > 0)
                    {
                        await ExecuteIntrinsicSkillAction(action, hexGrid);
                    }
                }
                actor.HasActed = true;
                break;

            case AIAction.ActionType.UseCareerSkill:
                await ExecuteCareerSkillAction(action, hexGrid);
                actor.HasActed = true;
                break;

            case AIAction.ActionType.Idle:
                _adapter?.LogMessage($"[color=gray]{action.Description}[/color]");
                break;
        }
    }

    private bool PrepareMoveThenAttack(AIAction action, HexGrid hexGrid)
    {
        if (!GodotObject.IsInstanceValid(action.Actor) || !GodotObject.IsInstanceValid(action.TargetUnit))
            return false;

        var actor = action.Actor!;
        var target = action.TargetUnit!;
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int apCost = weapon?.ApCost ?? 4;
        int weaponRange = weapon?.RangeCells ?? 1;

        if (actor.CurrentAp < apCost)
            return false;

        if (action.MovePath.Count == 0 && action.TargetPosition != new Vector2I(-1, -1))
            action.MovePath = hexGrid.FindPath(actor.GridPos, action.TargetPosition);

        if (action.MovePath.Count == 0)
        {
            int currentDist = actor.DistanceTo(target);
            return currentDist <= weaponRange
                && !CombatAttackRules.IsMeleeElevationBlocked(actor, target, hexGrid);
        }

        float fullCost = hexGrid.GetPathCost(actor.GridPos, action.MovePath);
        if (fullCost + apCost <= actor.CurrentAp)
            return true;

        var trimmed = GetLongestAffordableAttackPath(actor, target, action.MovePath, hexGrid, apCost, weaponRange);
        if (trimmed.Count == 0)
            return false;

        action.MovePath = trimmed;
        action.TargetPosition = trimmed[^1];
        action.AttackPosition = trimmed[^1];
        return true;
    }

    private List<Vector2I> GetLongestAffordableAttackPath(
        Unit actor,
        Unit target,
        List<Vector2I> path,
        HexGrid hexGrid,
        int attackApCost,
        int weaponRange)
    {
        var best = new List<Vector2I>();
        var current = actor.GridPos;
        float spent = 0.0f;
        for (int i = 0; i < path.Count; i++)
        {
            var next = path[i];
            var stepCost = hexGrid.GetPathCost(current, new List<Vector2I> { next });
            if (spent + stepCost + attackApCost > actor.CurrentAp) break;

            spent += stepCost;
            current = next;
            int dist = UnitFootprint.DistanceTo(next, target.GridPos, target.FootprintW, target.FootprintH);
            if (dist <= weaponRange && !CombatAttackRules.IsMeleeElevationBlocked(actor, target, hexGrid, next))
                best = path.Take(i + 1).ToList();
        }

        return best;
    }

    private async Task ExecuteReservedMoveOnly(AIAction action, HexGrid hexGrid, Node combatUi)
    {
        if (!GodotObject.IsInstanceValid(action.Actor)) return;

        var actor = action.Actor!;
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int attackApCost = weapon?.ApCost ?? 4;
        float moveBudget = Math.Max(0.0f, actor.CurrentAp - attackApCost);
        if (moveBudget < 1.0f || action.MovePath.Count == 0)
        {
            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 行动力不足，无法完成移动后攻击[/color]");
            return;
        }

        action.MovePath = TrimPathToBudget(actor.GridPos, action.MovePath, hexGrid, moveBudget);
        if (action.MovePath.Count == 0) return;
        action.Type = AIAction.ActionType.MoveOnly;
        await ExecuteMove(action, hexGrid, combatUi);
    }

    private static List<Vector2I> TrimPathToBudget(Vector2I start, List<Vector2I> path, HexGrid hexGrid, float budget)
    {
        var result = new List<Vector2I>();
        var current = start;
        float spent = 0.0f;
        foreach (var next in path)
        {
            float stepCost = hexGrid.GetPathCost(current, new List<Vector2I> { next });
            if (spent + stepCost > budget) break;
            result.Add(next);
            spent += stepCost;
            current = next;
        }
        return result;
    }

    private async Task ExecuteMove(AIAction action, HexGrid hexGrid, Node combatUi)
    {
        if (action.MovePath.Count == 0)
        {
            if (action.TargetPosition != new Vector2I(-1, -1))
            {
                action.MovePath = hexGrid.FindPath(action.Actor!.GridPos, action.TargetPosition);
            }
            if (action.MovePath.Count == 0) return;
        }

        var actor = action.Actor!;
        var finalPos = action.MovePath[^1];

        // 计算路径消耗的 AP
        float pathCost = hexGrid.GetPathCost(actor.GridPos, action.MovePath);
        if (pathCost > actor.CurrentAp)
        {
            // AP 不足以走完全程 — 截断路径到可达范围
            float apLeft = actor.CurrentAp;
            int walkable = -1;
            float spent = 0;
            var current = actor.GridPos;
            for (int i = 0; i < action.MovePath.Count; i++)
            {
                var next = action.MovePath[i];
                var cell = hexGrid.GetCell(next.X, next.Y);
                if (cell == null) continue;

                float cost = cell.Data != null ? cell.Data.moveCost : 1.0f;
                var prevCell = hexGrid.GetCell(current.X, current.Y);
                if (prevCell != null && cell.Elevation > prevCell.Elevation)
                {
                    cost += 3.0f;
                }

                if (spent + cost > apLeft) break;
                spent += cost;
                walkable = i;
                current = next;
            }
            if (walkable < 0) return;
            finalPos = action.MovePath[walkable];
            pathCost = spent;
        }

        // 消耗 AP
        actor.ConsumeAp(pathCost);

        // 执行移动（传递截断后的实际路径以触发 AoO 检测）
        var walkedPath = action.MovePath.Take(
            action.MovePath.IndexOf(finalPos) >= 0
                ? action.MovePath.IndexOf(finalPos) + 1
                : action.MovePath.Count
        ).ToList();
        _adapter?.MoveUnitTo(actor, finalPos.X, finalPos.Y, walkedPath);
        _adapter?.LogMessage($"{actor.Data!.UnitName} 移动到 ({finalPos.X}, {finalPos.Y})");

        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.3);
    }

    private async Task ExecuteAttack(AIAction action, HexGrid hexGrid, Node combatUi)
    {
        if (!GodotObject.IsInstanceValid(action.TargetUnit) || action.TargetUnit!.CurrentHp <= 0) return;

        var actor = action.Actor!;
        var target = action.TargetUnit!;

        if (!BuffTargetingRules.IsDirectlyTargetable(target) || BuffTargetingRules.ShouldAiIgnore(target))
        {
            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 失去对 {target.Data!.UnitName} 的有效目标。[/color]");
            return;
        }

        // 射程验证 — 移动后仍不在射程内则跳过攻击
        var weapon = actor.Model.GetMainHand() as WeaponData;
        int weaponRange = weapon?.RangeCells ?? 1;
        int dist = actor.DistanceTo(target);
        if (dist > weaponRange)
        {
            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 无法攻击 {target.Data!.UnitName}（距离 {dist}，射程 {weaponRange}）[/color]");
            return;
        }

        if (CombatAttackRules.IsMeleeElevationBlocked(actor, target, hexGrid))
        {
            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 无法攻击 {target.Data!.UnitName}（{CombatAttackRules.MeleeElevationBlockedReason}）[/color]");
            return;
        }

        // AP 验证 — 行动力不足则跳过攻击
        int apCost = weapon?.ApCost ?? 4;
        if (actor.CurrentAp < apCost)
        {
            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 行动力不足，无法攻击[/color]");
            return;
        }

        // 远程全掩体检查
        if (weapon != null && weapon.IsRanged)
        {
            var targetCell = hexGrid.GetCell(target.GridPos.X, target.GridPos.Y);
            if (targetCell != null && targetCell.CoverType == 2)
            {
                _adapter?.LogMessage($"[color=gray]{target.Data!.UnitName} 被 {actor.Data!.UnitName} 的全掩体阻挡，无法射击。[/color]");
                return;
            }
        }

        // v1 职业被动: 万象代价 — 无法攻击时禁止
        if (!CareerPassiveHooks.CanAttack(actor))
        {
            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 万象代价: 无法攻击[/color]");
            return;
        }

        // 消耗攻击 AP（所有硬性可攻击性检查通过后再扣除）
        actor.ConsumeAp(apCost);

        // 攻击动画编排（远程=投射物飞行，近战=突刺）
        await _attackAnimator.PlayAttack(actor, target, weapon);

        // 使用 CombatResolver 统一结算（包围加成需要传入攻击者同阵营单位）
        var allies = (_allUnitsCache ?? new List<Unit>())
            .Where(u => GodotObject.IsInstanceValid(u) && u != actor && u.CurrentHp > 0
                     && u.IsPlayerSide == actor.IsPlayerSide)
            .ToArray();
        var defenderAllies = (_allUnitsCache ?? new List<Unit>())
            .Where(u => GodotObject.IsInstanceValid(u) && u != target && u.CurrentHp > 0
                     && u.IsPlayerSide == target.IsPlayerSide)
            .ToArray();
        var result = CombatResolver.ResolveAttack(actor, target, hexGrid, action.IsCharge,
            attackerAllies: allies,
            defenderAllies: defenderAllies);

        if (result["hit"].AsBool())
        {
            int dmg = result["damage"].AsInt32();
            bool isCrit = result.ContainsKey("critical") && result["critical"].AsBool();

            // 播放命中音效
            int dmgType = 0;
            var actorWeapon = actor.Model.GetMainHand() as BladeHex.Data.WeaponData;
            if (actorWeapon != null) dmgType = (int)actorWeapon.WeaponDamageType;
            _adapter?.PlayAttackHitSfx(dmgType, isCrit);

            // 伤害数字
            _adapter?.ShowDamageNumber(target, dmg, isCrit);

            var logParts = new List<string>
            {
                $"[color=red]{actor.Data!.UnitName} 命中 {target.Data!.UnitName}，造成 {dmg} 伤害[/color]"
            };

            if (isCrit) logParts.Add("[color=yellow]★暴击!  [/color]");
            if (action.IsCharge) logParts.Add("[color=orange]冲锋加成!  [/color]");

            _adapter?.LogMessage(string.Join(" ", logParts));

            // 更新 UI
            _adapter?.UpdateUnitInfo(target);

            // 击杀处理
            if (target.CurrentHp <= 0)
            {
                _adapter?.PlaySfx("combat_death");
                _adapter?.LogMessage($"[color=yellow]{target.Data!.UnitName} 被 {actor.Data!.UnitName} 击败！[/color]");

                _adapter?.OnUnitKilled(target, actor);
            }
        }
        else
        {
            // 播放未中音效
            int missDmgType = 0;
            var actorWeapon2 = actor.Model.GetMainHand() as BladeHex.Data.WeaponData;
            if (actorWeapon2 != null) missDmgType = (int)actorWeapon2.WeaponDamageType;
            _adapter?.PlayAttackMissSfx(missDmgType);

            // 伤害数字 — Miss
            _adapter?.ShowDamageNumber(target, 0,
                missLabel: result.ContainsKey("fumble") && result["fumble"].AsBool() ? "Fumble" : "Miss");

            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 的攻击未命中 {target.Data!.UnitName}。[/color]");
        }

        // 剑舞者额外攻击 VFX
        if (result.ContainsKey("blade_dancer_extra_hit") && result["blade_dancer_extra_hit"].AsBool()
            && result.ContainsKey("blade_dancer_extra_target"))
        {
            var extraTarget = result["blade_dancer_extra_target"].AsGodotObject() as Unit;
            if (extraTarget != null && GodotObject.IsInstanceValid(extraTarget))
            {
                int extraDmg = result.ContainsKey("blade_dancer_extra_damage") ? result["blade_dancer_extra_damage"].AsInt32() : 0;
                bool extraCrit = result.ContainsKey("blade_dancer_extra_crit") && result["blade_dancer_extra_crit"].AsBool();
                bool extraHit = result["blade_dancer_extra_hit"].AsBool();

                if (extraHit)
                {
                    // 伤害数字
                    _adapter?.ShowDamageNumber(extraTarget, extraDmg, extraCrit);
                    _adapter?.LogMessage($"[color=red]{actor.Data!.UnitName} 剑舞者额外命中 {extraTarget.Data!.UnitName}，造成 {extraDmg} 伤害[/color]");
                    _adapter?.UpdateUnitInfo(extraTarget);

                    // 击杀处理
                    if (extraTarget.CurrentHp <= 0)
                    {
                        _adapter?.PlaySfx("combat_death");
                        _adapter?.LogMessage($"[color=yellow]{extraTarget.Data!.UnitName} 被剑舞者额外攻击击败！[/color]");
                        _adapter?.OnUnitKilled(extraTarget, actor);
                    }
                }
            }
        }

        _adapter?.PlayUnitAnim(actor, "default");
    }

    /// <summary>执行攻城技能（攻击城门 / 架设云梯）</summary>
    private async Task ExecuteSiegeSkill(AIAction action, HexGrid hexGrid)
    {
        var actor = action.Actor!;
        var targetPos = action.TargetPosition;
        var targetCell = hexGrid.GetCell(targetPos.X, targetPos.Y);
        if (targetCell?.Data == null) return;

        switch (action.SkillId)
        {
            case "siege_attack_gate":
            {
                var weapon = actor.Model.GetMainHand() as WeaponData;
                actor.CurrentAp -= weapon?.ApCost ?? 4;
                bool destroyed = SiegeActions.DamageDestructible(targetCell.Data);
                if (destroyed)
                {
                    targetCell.Elevation = 1;
                    _adapter?.LogMessage($"[color=red]{actor.Data!.UnitName} 破坏了城门！[/color]");
                }
                else
                {
                    _adapter?.LogMessage($"{actor.Data!.UnitName} 攻击城门（剩余 {targetCell.Data.durability}/{targetCell.Data.maxDurability}）");
                }
                break;
            }

            case "siege_build_ladder":
            {
                actor.CurrentAp -= SiegeActions.LadderApCost;
                bool completed = SiegeActions.BuildLadder(targetCell.Data);
                if (completed)
                {
                    targetCell.Elevation = 1;
                    _adapter?.LogMessage($"[color=yellow]{actor.Data!.UnitName} 完成了云梯架设！[/color]");
                }
                else
                {
                    _adapter?.LogMessage($"{actor.Data!.UnitName} 架设云梯 ({targetCell.Data.ladderProgress}/{SiegeActions.LadderRequiredSteps})");
                }
                break;
            }
        }

        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.5);
    }

    private async Task ExecuteIntrinsicSkillAction(AIAction action, HexGrid hexGrid)
    {
        var actor = action.Actor!;
        var targetCell = action.TargetPosition;
        string skillName = action.SkillId;

        if (_combatManager == null) return;

        _adapter?.LogMessage($"[color=yellow]{actor.Data!.UnitName} 释放天生技能 {skillName}！[/color]");

        var result = _combatManager.UseSkill(actor, skillName, targetCell, hexGrid);

        if (result.Success)
        {
            foreach (var sub in result.SubResults)
            {
                switch (sub)
                {
                    case StatusEffectApplication eff:
                        var target = FindRuntimeUnit(eff.Target);
                        if (GodotObject.IsInstanceValid(target) && !string.IsNullOrEmpty(eff.EffectId) && target.Data != null)
                        {
                            _adapter?.LogMessage($"对 {target.Data!.UnitName} 施加状态：{eff.EffectId}");
                        }
                        break;
                    case DamageEvent dmg:
                        var dmgTarget = FindRuntimeUnit(dmg.Target);
                        if (GodotObject.IsInstanceValid(dmgTarget))
                        {
                            _adapter?.ShowDamageNumber(dmgTarget, dmg.Damage, dmg.IsCritical);
                            if (dmgTarget.RenderBus != null) dmgTarget.RenderBus.NotifyHit(dmgTarget);
                            
                            _adapter?.LogMessage($"[color=red]{actor.Data!.UnitName} 的技能命中 {dmgTarget.Data!.UnitName}，造成 {dmg.Damage} 伤害！[/color]");
                            _adapter?.UpdateUnitInfo(dmgTarget);

                            if (dmgTarget.CurrentHp <= 0)
                            {
                                _adapter?.PlaySfx("combat_death");
                                _adapter?.LogMessage($"[color=yellow]{dmgTarget.Data!.UnitName} 被击败！[/color]");
                                _adapter?.OnUnitKilled(dmgTarget, actor);
                            }
                        }
                        break;
                    case HealEvent heal:
                        var healTarget = FindRuntimeUnit(heal.Target);
                        if (GodotObject.IsInstanceValid(healTarget))
                        {
                            _adapter?.ShowDamageNumber(healTarget, -heal.Amount, false);
                            _adapter?.LogMessage($"[color=green]{actor.Data!.UnitName} 的技能治疗 {healTarget.Data!.UnitName}，恢复 {heal.Amount} 生命！[/color]");
                            _adapter?.UpdateUnitInfo(healTarget);
                        }
                        break;
                }
            }
            _adapter?.UpdateUnitInfo(actor);
        }
        else
        {
            string reason = result.FailureReason ?? "未知原因";
            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 技能释放失败：{reason}[/color]");
        }

        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.5);
    }

    /// <summary>把技能结果中的 Core 模型映射回运行时 Unit。优先引用匹配，避免默认 CharacterId 误配。</summary>
    private Unit? FindRuntimeUnit(BattleUnitModel targetModel)
    {
        var units = _allUnitsCache ?? new List<Unit>();
        var targetData = targetModel.Data;
        var byReference = units.FirstOrDefault(u =>
            GodotObject.IsInstanceValid(u)
            && (ReferenceEquals(u.Model, targetModel) || ReferenceEquals(u.Model.Data, targetData) || ReferenceEquals(u.Data, targetData)));
        if (byReference != null) return byReference;

        int characterId = targetData?.CharacterId ?? -1;
        if (characterId >= 0)
        {
            return units.FirstOrDefault(u =>
                GodotObject.IsInstanceValid(u)
                && u.Data != null
                && u.Data.CharacterId == characterId);
        }

        return null;
    }

    private bool CanApplyResultStatus(Unit? target, StatusEffectApplication effect)
    {
        if (!GodotObject.IsInstanceValid(target) || string.IsNullOrEmpty(effect.EffectId) || target.Data == null)
            return false;
        if (!PassiveSkillResolver.IsFearEffect(effect.EffectId))
            return true;
        return PassiveSkillResolver.CanApplyFearEffect(target, _allUnitsCache);
    }

    /// <summary>v0.8 D2-A: 执行职业技能</summary>
    private async Task ExecuteCareerSkillAction(AIAction action, HexGrid hexGrid)
    {
        var actor = action.Actor!;
        if (actor.Data == null) return;

        _adapter?.LogMessage($"[color=cyan]{action.Description}[/color]");

        var result = CareerSkillExecutor.ExecuteCareerSkill(
            actor, action.SkillTargetCell, hexGrid,
            _allUnitsCache ?? new List<Unit>(),
            _allUnitsCache?.Where(u => u.IsPlayerSide).ToList() ?? new List<Unit>(),
            _allUnitsCache?.Where(u => !u.IsPlayerSide).ToList() ?? new List<Unit>()
        );

        if (result.Success)
        {
            // CareerSkillExecutor 已处理 AP 消耗和 HasActed 标记

            // 处理状态效果和伤害结果
            foreach (var sub in result.SubResults)
            {
                switch (sub)
                {
                    case StatusEffectApplication eff:
                        var target = FindRuntimeUnit(eff.Target);
                        if (CanApplyResultStatus(target, eff))
                        {
                            BladeHex.Combat.Buff.BuffSystem.Apply(target!.Data!, eff.EffectId, eff.Duration, sourceUnitId: (int)actor.GetInstanceId());
                        }
                        break;
                    case DamageEvent dmg:
                        var dmgTarget = FindRuntimeUnit(dmg.Target);
                        if (GodotObject.IsInstanceValid(dmgTarget))
                        {
                            _adapter?.ShowDamageNumber(dmgTarget, dmg.Damage, dmg.IsCritical);
                            _adapter?.UpdateUnitInfo(dmgTarget);
                        }
                        break;
                }
            }

            _adapter?.LogMessage($"[color=cyan]{actor.Data!.UnitName} 完成职业技能释放[/color]");
        }
        else
        {
            string reason = result.FailureReason ?? "未知原因";
            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 职业技能释放失败：{reason}[/color]");
        }

        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.5);
    }

    private List<Unit> SortByPriority(List<Unit> enemies)
    {
        return enemies.OrderBy(e => GetStrategyPriority(e.Data!.aiStrategy)).ToList();
    }

    public static int GetStrategyPriority(UnitData.AIStrategy strategy)
    {
        return strategy switch
        {
            UnitData.AIStrategy.Tactical or UnitData.AIStrategy.Cunning or UnitData.AIStrategy.Territorial => 0,
            UnitData.AIStrategy.Cautious => 1,
            UnitData.AIStrategy.Reckless or UnitData.AIStrategy.Berserk or UnitData.AIStrategy.Intimidate => 2,
            UnitData.AIStrategy.Instinct => 3,
            _ => 99,
        };
    }

    // ============================================================================
    // v0.8 D1/D2/D3: AI 职业大招决策
    // ============================================================================

    /// <summary>v1.0: 尝试让 AI 单位释放职业技能。返回 true 表示释放成功，本回合结束。</summary>
    private bool TryUseCareerSkill(Unit unit, List<Unit> playerUnits, List<Unit> enemyUnits)
    {
        if (!unit.CanUseCareerSkill()) return false;
        var skill = unit.GetCareerSkill();
        if (skill == null) return false;

        // v1: 跳过被动技能（无 UI 按钮）
        if (!skill.ShowInCombatUi || skill.IsPassive)
            return false;

        // v1: 五属性主动 — 检查是否满 AP
        if (skill.RequiresFullAp && unit.CurrentAp < unit.GetMaxAp() - 0.01f)
            return false;

        // v1: 万象代价 — 检查是否可攻击/可行动
        if (skill.EffectId == "paragon_all_aspects")
        {
            if (unit.Data?.Runtime.CareerParagonExhausted == true)
                return false;
        }

        // v1: 判定是否应该释放 (基于 TargetType)
        float maxHp = unit.Model.GetMaxHp();
        float hpPct = maxHp > 0 ? (float)unit.CurrentHp / maxHp : 1f;
        int currentRound = GetCurrentRound(enemyUnits);

        bool shouldUse = skill.SkillTargetType switch
        {
            CareerSkillData.TargetType.Self => hpPct < 0.40f || currentRound >= 2,
            CareerSkillData.TargetType.SingleAlly => CountAlliesInDanger(enemyUnits) >= 1,
            CareerSkillData.TargetType.AllAllies => CountAlliesInDanger(enemyUnits) >= 1,
            CareerSkillData.TargetType.SingleEnemy => HasLowHpEnemy(playerUnits, 0.3f) || currentRound >= 2,
            CareerSkillData.TargetType.Ground => currentRound >= 2,
            _ => hpPct < 0.50f || currentRound >= 2,
        };

        if (!shouldUse) return false;

        // 确定目标格
        var targetCell = ResolveCareerSkillTarget(unit, skill, playerUnits, enemyUnits);

        // 释放技能
        var allies = (_allUnitsCache ?? new List<Unit>()).Where(u => !u.IsPlayerSide).ToList();
        var enemies2 = (_allUnitsCache ?? new List<Unit>()).Where(u => u.IsPlayerSide).ToList();
        var result = CareerSkillExecutor.ExecuteCareerSkill(
            unit, targetCell,
            null, // hexGrid 暂不可用，传 null
            _allUnitsCache ?? new List<Unit>(),
            enemies2, allies);

        bool success = result.Success;
        if (success)
            _adapter?.LogMessage($"[color=cyan]{unit.Data!.UnitName} 释放职业技能![/color]");
        return success;
    }

    /// <summary>v1.0 D3: 根据 v1 CareerSkillData.TargetType 选择 AI 目标格</summary>
    private Vector2I ResolveCareerSkillTarget(Unit unit, CareerSkillData skill, List<Unit> playerUnits, List<Unit> enemyUnits)
    {
        return skill.SkillTargetType switch
        {
            CareerSkillData.TargetType.Self or CareerSkillData.TargetType.AllAllies
                or CareerSkillData.TargetType.AllAdjacent or CareerSkillData.TargetType.Ground
                => unit.GridPos,
            CareerSkillData.TargetType.SingleEnemy => FindNearestEnemy(unit, playerUnits)?.GridPos ?? unit.GridPos,
            CareerSkillData.TargetType.SingleAlly => FindLowestHpAlly(unit, enemyUnits)?.GridPos ?? unit.GridPos,
            CareerSkillData.TargetType.RangedSingle => FindLowestHpEnemyInLos(unit, playerUnits)?.GridPos ?? unit.GridPos,
            CareerSkillData.TargetType.RangedAoe => FindNearestEnemy(unit, playerUnits)?.GridPos ?? unit.GridPos,
            _ => unit.GridPos,
        };
    }

    /// <summary>D3: 距离最近的存活敌人</summary>
    private Unit? FindNearestEnemy(Unit unit, List<Unit> enemies)
    {
        Unit? best = null;
        int bestDist = int.MaxValue;
        foreach (var e in enemies)
        {
            if (!GodotObject.IsInstanceValid(e) || e.CurrentHp <= 0) continue;
            int d = unit.DistanceTo(e);
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    /// <summary>D3: HP 百分比最低的存活友军</summary>
    private Unit? FindLowestHpAlly(Unit unit, List<Unit> allies)
    {
        Unit? best = null;
        float bestPct = float.MaxValue;
        foreach (var a in allies)
        {
            if (!GodotObject.IsInstanceValid(a) || a.CurrentHp <= 0) continue;
            float pct = a.Model.GetMaxHp() > 0 ? (float)a.CurrentHp / a.Model.GetMaxHp() : 1f;
            if (pct < bestPct) { bestPct = pct; best = a; }
        }
        return best;
    }

    /// <summary>D3: LineCharge 目标—朝最近敌人方向延伸 3 格</summary>
    private Vector2I FindChargeDirection(Unit unit, List<Unit> enemies)
    {
        var nearest = FindNearestEnemy(unit, enemies);
        if (nearest == null) return unit.GridPos;
        int dx = nearest.GridPos.X - unit.GridPos.X;
        int dy = nearest.GridPos.Y - unit.GridPos.Y;
        int steps = System.Math.Max(1, System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)));
        int targetX = unit.GridPos.X + (int)System.Math.Round(dx * 3.0 / steps);
        int targetY = unit.GridPos.Y + (int)System.Math.Round(dy * 3.0 / steps);
        return new Vector2I(targetX, targetY);
    }

    /// <summary>D3: HP 最低的存活敌人（视线内）—简化为 HP 最低的敌人</summary>
    private Unit? FindLowestHpEnemyInLos(Unit unit, List<Unit> enemies)
    {
        Unit? best = null;
        float bestPct = float.MaxValue;
        foreach (var e in enemies)
        {
            if (!GodotObject.IsInstanceValid(e) || e.CurrentHp <= 0) continue;
            float pct = e.Model.GetMaxHp() > 0 ? (float)e.CurrentHp / e.Model.GetMaxHp() : 1f;
            if (pct < bestPct) { bestPct = pct; best = e; }
        }
        return best;
    }

    /// <summary>D2: 当前回合数—简化估算（取 HasActed 最多的单位 AP 消耗和）</summary>
    private int GetCurrentRound(List<Unit> units)
    {
        // 简单近似：如果有单位 AP 已低于初始値的 30%，认为至少第 2 回合
        int lowApCount = 0;
        foreach (var u in units)
            if (GodotObject.IsInstanceValid(u) && u.CurrentHp > 0 && u.CurrentAp < u.Model.GetMaxHp() * 0.3f)
                lowApCount++;
        if (lowApCount >= units.Count / 2) return 3;
        return 1;
    }

    /// <summary>D2: 敌方是否有 HP 低于阈値的存活单位</summary>
    private bool HasLowHpEnemy(List<Unit> enemies, float threshold)
    {
        foreach (var e in enemies)
        {
            if (!GodotObject.IsInstanceValid(e) || e.CurrentHp <= 0) continue;
            float pct = e.Model.GetMaxHp() > 0 ? (float)e.CurrentHp / e.Model.GetMaxHp() : 1f;
            if (pct < threshold) return true;
        }
        return false;
    }

    /// <summary>D2: 友军中 HP 低于 30% 的存活单位数</summary>
    private int CountAlliesInDanger(List<Unit> allies)
    {
        int count = 0;
        foreach (var a in allies)
        {
            if (!GodotObject.IsInstanceValid(a) || a.CurrentHp <= 0) continue;
            float pct = a.Model.GetMaxHp() > 0 ? (float)a.CurrentHp / a.Model.GetMaxHp() : 1f;
            if (pct < 0.3f) count++;
        }
        return count;
    }
}
