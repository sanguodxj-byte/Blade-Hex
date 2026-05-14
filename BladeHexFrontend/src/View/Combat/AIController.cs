using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BladeHex.Data;
using BladeHex.Events;
using BladeHex.Map;

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

    // 战斗场景引用（目前假设为 Node，具体可能需要接口或基类）
    // 战斗场景适配器（类型化接口，替代 HasMethod/Call 反射）
    private ICombatSceneAdapter? _adapter;

    // 所有单位缓存（用于士气系统跨阵营结算）
    private List<Unit> _allUnitsCache = new();

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
        _strategies[UnitData.AIStrategy.Reckless] = new AIStrategyReckless(_difficultyConfig);
        _strategies[UnitData.AIStrategy.Cautious] = new AIStrategyCautious(_difficultyConfig);
        _strategies[UnitData.AIStrategy.Tactical] = new AIStrategyTactical(_difficultyConfig);
        _strategies[UnitData.AIStrategy.Instinct] = new AIStrategyInstinct(_difficultyConfig);
    }

    public void SetCombatScene(ICombatSceneAdapter adapter)
    {
        _adapter = adapter;
    }

    /// <summary>主入口：执行所有敌方单位的回合行动</summary>
    public async Task ExecuteEnemyTurn(List<Unit> enemyUnits, List<Unit> playerUnits, HexGrid hexGrid, Node combatUi)
    {
        // 构建全单位缓存（士气系统需要跨阵营结算）
        _allUnitsCache = new List<Unit>(enemyUnits.Count + playerUnits.Count);
        _allUnitsCache.AddRange(enemyUnits);
        _allUnitsCache.AddRange(playerUnits);

        // 按策略优先级排序执行（战术 > 谨慎 > 鲁莽 > 本能）
        var sortedEnemies = SortByPriority(enemyUnits);

        foreach (var enemy in sortedEnemies)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;

            // 为当前单位决策
            var action = DecideActionForUnit(enemy, playerUnits, enemyUnits, hexGrid);

            // 执行行动
            await ExecuteAction(action, hexGrid, combatUi);

            // 行动间短暂延迟，增强可读性
            await ToSignal(GetTree().CreateTimer(0.4f), SceneTreeTimer.SignalName.Timeout);
        }

        EmitSignal(SignalName.AllActionsCompleted);
    }

    public AIAction DecideActionForUnit(Unit actor, List<Unit> playerUnits, List<Unit> enemyUnits, HexGrid hexGrid)
    {
        var strategyKey = actor.Data!.aiStrategy;
        if (!_strategies.TryGetValue(strategyKey, out var strategy))
        {
            strategy = _strategies[UnitData.AIStrategy.Instinct];
        }

        // 难度失误注入
        if (GD.Randf() < _difficultyConfig.MistakeChance)
        {
            strategy = _strategies[UnitData.AIStrategy.Instinct];
        }

        return strategy.DecideAction(actor, playerUnits, enemyUnits, hexGrid);
    }

    private async Task ExecuteAction(AIAction action, HexGrid hexGrid, Node combatUi)
    {
        if (action == null || !GodotObject.IsInstanceValid(action.Actor)) return;

        var actor = action.Actor!;

        switch (action.Type)
        {
            case AIAction.ActionType.MoveThenAttack:
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

            case AIAction.ActionType.Idle:
                _adapter?.LogMessage($"[color=gray]{action.Description}[/color]");
                break;
        }
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

        var finalPos = action.MovePath[^1];

        // 通过类型化适配器执行移动
        _adapter?.MoveUnitTo(action.Actor!, finalPos.X, finalPos.Y);

        _adapter?.LogMessage($"{action.Actor!.Data!.UnitName} 移动到 ({finalPos.X}, {finalPos.Y})");
        
        // 移动动画时间
        await ToSignal(GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task ExecuteAttack(AIAction action, HexGrid hexGrid, Node combatUi)
    {
        if (!GodotObject.IsInstanceValid(action.TargetUnit) || action.TargetUnit!.CurrentHp <= 0) return;

        var actor = action.Actor!;
        var target = action.TargetUnit!;

        // 远程全掩体检查
        var weapon = actor.Model.GetMainHand() as WeaponData;
        if (weapon != null && weapon.IsRanged)
        {
            var targetCell = hexGrid.GetCell(target.GridPos.X, target.GridPos.Y);
            if (targetCell != null && targetCell.CoverType == 2)
            {
                _adapter?.LogMessage($"[color=gray]{target.Data!.UnitName} 被 {actor.Data!.UnitName} 的全掩体阻挡，无法射击。[/color]");
                return;
            }
        }

        // 攻击前动画
        _adapter?.PlayUnitAnim(actor, "attack");
        await ToSignal(GetTree().CreateTimer(0.6f), SceneTreeTimer.SignalName.Timeout);

        // 使用 CombatResolver 统一结算
        var result = CombatResolver.ResolveAttack(actor, target, hexGrid, action.IsCharge);

        if (result["hit"].AsBool())
        {
            int dmg = result["damage"].AsInt32();

            // 播放命中音效
            int dmgType = 0;
            var actorWeapon = actor.Model.GetMainHand() as BladeHex.Data.WeaponData;
            if (actorWeapon != null) dmgType = (int)actorWeapon.WeaponDamageType;
            _adapter?.PlayAttackHitSfx(dmgType, result["critical"].AsBool());

            var logParts = new List<string>
            {
                $"[color=red]{actor.Data!.UnitName} 命中 {target.Data!.UnitName}，造成 {dmg} 伤害[/color]"
            };

            if (result.ContainsKey("critical") && result["critical"].AsBool()) logParts.Add("[color=yellow]★暴击！[/color]");
            if (action.IsCharge) logParts.Add("[color=orange]冲锋加成！[/color]");

            _adapter?.LogMessage(string.Join(" ", logParts));

            // 更新 UI
            _adapter?.UpdateUnitInfo(target);

            // 击杀处理
            if (target.CurrentHp <= 0)
            {
                _adapter?.PlaySfx("combat_death");
                _adapter?.LogMessage($"[color=yellow]{target.Data!.UnitName} 被 {actor.Data!.UnitName} 击败！[/color]");

                // 士气变动
                MoraleSystem.OnUnitKilled(target, actor, _allUnitsCache);

                // 清除格子占用
                var targetCell = hexGrid.GetCell(target.GridPos.X, target.GridPos.Y);
                if (targetCell != null) targetCell.Occupant = null;
            }
        }
        else
        {
            // 播放未中音效
            int missDmgType = 0;
            var actorWeapon2 = actor.Model.GetMainHand() as BladeHex.Data.WeaponData;
            if (actorWeapon2 != null) missDmgType = (int)actorWeapon2.WeaponDamageType;
            _adapter?.PlayAttackMissSfx(missDmgType);

            _adapter?.LogMessage($"[color=gray]{actor.Data!.UnitName} 的攻击未命中 {target.Data!.UnitName}。[/color]");
        }

        _adapter?.PlayUnitAnim(actor, "default");
    }

    private List<Unit> SortByPriority(List<Unit> enemies)
    {
        var priorityOrder = new Dictionary<UnitData.AIStrategy, int>
        {
            { UnitData.AIStrategy.Tactical, 0 },
            { UnitData.AIStrategy.Cautious, 1 },
            { UnitData.AIStrategy.Reckless, 2 },
            { UnitData.AIStrategy.Instinct, 3 }
        };

        return enemies.OrderBy(e => priorityOrder.GetValueOrDefault(e.Data!.aiStrategy, 99)).ToList();
    }
}
