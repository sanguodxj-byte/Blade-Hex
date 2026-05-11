using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Combat.AI;
using BladeHex.Combat.UI;

namespace BladeHex.Scenes.Combat;

/// <summary>
/// 战术战斗场景控制器 —— 负责环境初始化、单位生成及流程协调
/// 迁移自 GDScript CombatScene.gd
/// </summary>
public partial class CombatSceneController : Node3D
{
    [Signal] public delegate void CombatFinishedEventHandler(bool victory);

    // 核心组件引用
    private HexGrid _hexGrid = null!;
    private CombatManager _combatManager = null!;
    private CombatUI _combatUI = null!;
    private AIController _aiController = null!;
    private SpellManager _spellManager = null!;
    private Camera3D _camera = null!;

    // 运行状态
    private string _currentActionMode = "none"; // "none", "move", "attack", "spell", "item"
    private Unit? _activePlayerUnit;
    private List<HexCell> _highlightedCells = new();
    private float _animSpeed = 1.0f;
    private int _turnNumber = 1;
    private SpellData? _selectedSpell;

    public override void _Ready()
    {
        InitEnvironment();
        InitSystems();
        GenerateBattlefield();
        SpawnUnits();

        _combatManager.StartCombat();
    }

    private void InitEnvironment()
    {
        // 设置摄像机
        _camera = new Camera3D();
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = 700.0f;
        _camera.RotationDegrees = new Vector3(-45, 0, 0);
        _camera.Position = new Vector3(600, 800, 1000);
        AddChild(_camera);

        // 灯光
        var light = new DirectionalLight3D();
        light.RotationDegrees = new Vector3(-60, 30, 0);
        light.ShadowEnabled = true;
        AddChild(light);
    }

    private void InitSystems()
    {
        // 网格
        _hexGrid = new HexGrid();
        _hexGrid.Name = "HexGrid";
        _hexGrid.CellClicked += OnCellClicked;
        AddChild(_hexGrid);

        // 战斗管理
        _combatManager = new CombatManager();
        _combatManager.Name = "CombatManager";
        _combatManager.TurnStarted += OnTurnStarted;
        _combatManager.CombatEnded += OnCombatEnded;
        AddChild(_combatManager);

        // UI
        _combatUI = new CombatUI();
        AddChild(_combatUI);
        _combatUI.ActionSelected += OnActionSelected;
        _combatUI.SpellSelected += (spell) => {
            _selectedSpell = spell;
            HighlightSpellRange(_activePlayerUnit!, spell);
        };

        // AI
        var aiDifficulty = new AIDifficultyConfig();
        // TODO: 从 GlobalState 读取难度
        _aiController = new AIController();
        _aiController.Initialize(aiDifficulty);
        _aiController.SetCombatScene(this);
        AddChild(_aiController);

        // SpellManager
        _spellManager = new SpellManager();
        AddChild(_spellManager);
    }

    private void GenerateBattlefield()
    {
        // TODO: 使用 BattleMapGenerator 生成地图
        // 暂时直接初始化 HexGrid 逻辑由其自身 load_from_map_data 处理
    }

    private void SpawnUnits()
    {
        // TODO: 真正的单位生成逻辑应基于 BattleContext
        // 暂时使用占位逻辑（参照 GDScript）
    }

    // --- 核心流程 ---

    private void OnTurnStarted(CombatManager.CombatState state)
    {
        _currentActionMode = "none";
        ClearHighlights();

        _combatUI.UpdateTurnOrder(_combatManager.AllUnits, _activePlayerUnit, _turnNumber);
        _combatUI.RefreshAllyList();

        if (state == CombatManager.CombatState.PlayerTurn)
        {
            _combatUI.SetTurnText("=== 玩家回合 ===", Colors.SkyBlue);
            _combatUI.SetActionBarVisible(true);
            if (_activePlayerUnit != null) _combatUI.UpdateUnitInfo(_activePlayerUnit);
            _combatUI.LogMessage("轮到玩家行动。");
        }
        else if (state == CombatManager.CombatState.EnemyTurn)
        {
            _combatUI.SetTurnText("=== 敌方回合 ===", Colors.Tomato);
            _combatUI.SetActionBarVisible(false);
            _combatUI.LogMessage("敌方行动中...");
            _ = ExecuteAiTurn();
        }
        _turnNumber++;
    }

    // --- 交互执行 ---

    private async void OnCellClicked(HexCell cell)
    {
        if (_combatManager.CurrentState != CombatManager.CombatState.PlayerTurn) return;
        if (_activePlayerUnit == null || _activePlayerUnit.CurrentHp <= 0) return;

        if (_currentActionMode == "move")
        {
            if (cell.Occupant != null) return;
            
            var path = _hexGrid.FindPath(_activePlayerUnit.GridPos, cell.GridPos);
            if (path.Count == 0) return;

            float cost = _hexGrid.GetPathCost(path);
            if (cost <= _activePlayerUnit.GetAp())
            {
                // 不再重置 _currentActionMode = "none"，允许连续操作
                await _activePlayerUnit.MoveAlongPath(path, _hexGrid);
                _activePlayerUnit.ConsumeAp(cost);
                
                _combatUI.UpdateUnitInfo(_activePlayerUnit);
                HighlightMoveRange(_activePlayerUnit); // 刷新可移动范围
            }
            else
            {
                _combatUI.LogMessage("AP 不足，无法到达该位置。");
            }
        }
        else if (_currentActionMode == "attack")
        {
            var target = cell.Occupant;
            if (target == null || _combatManager.PlayerUnits.Contains(target)) return;

            var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
            int apCost = weapon?.ApCost ?? 4;

            if (_activePlayerUnit.GetAp() < apCost)
            {
                _combatUI.LogMessage($"AP 不足，攻击需要 {apCost} AP。");
                return;
            }

            // 弩装填检查
            if (weapon != null && weapon.IsCrossbow && !_activePlayerUnit.Data!.IsRangedWeaponLoaded)
            {
                _combatUI.LogMessage("武器尚未装填！请先执行装填动作。");
                return;
            }

            int baseAtkRange = weapon?.RangeCells ?? 1;
            var hg = LineOfSight.GetHighGroundBonus(_activePlayerUnit.GridPos, cell.GridPos, _hexGrid);
            int dist = HexUtils.Distance(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y, cell.GridPos.X, cell.GridPos.Y);

            if (dist <= baseAtkRange + hg.RangeBonus)
            {
                await ExecutePlayerAttack(_activePlayerUnit, target);
                _activePlayerUnit.ConsumeAp(apCost);
                
                // 弩射击后失去装填
                if (weapon != null && weapon.IsCrossbow)
                    _activePlayerUnit.Data!.IsRangedWeaponLoaded = false;

                _combatUI.UpdateUnitInfo(_activePlayerUnit);
                HighlightAttackRange(_activePlayerUnit);
            }
        }
        else if (_currentActionMode == "reload")
        {
            var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
            if (weapon != null && weapon.IsCrossbow && !_activePlayerUnit.Data!.IsRangedWeaponLoaded)
            {
                int reloadCost = weapon.ReloadCost;
                if (_activePlayerUnit.GetAp() >= reloadCost)
                {
                    _activePlayerUnit.ConsumeAp(reloadCost);
                    _activePlayerUnit.Data.IsRangedWeaponLoaded = true;
                    _combatUI.LogMessage("十字弩装填完毕。");
                    _combatUI.UpdateUnitInfo(_activePlayerUnit);
                }
                else
                {
                    _combatUI.LogMessage("AP 不足，无法装填。");
                }
            }
        }
    }

    /// <summary>由 AI 调用，执行单位移动</summary>
    public async void _move_unit_to(Unit unit, int q, int r)
    {
        var targetPos = new Vector2I(q, r);
        var path = _hexGrid.FindPath(unit.GridPos, targetPos);
        if (path.Count > 0)
        {
            // 假设 AI 移动不消耗 AP（或者在 AIController 中处理）
            await unit.MoveAlongPath(path, _hexGrid);
        }
    }

    private async Task ExecutePlayerAttack(Unit actor, Unit target)
    {
        _combatUI.LogMessage($"{actor.Data!.UnitName} 攻击 {target.Data!.UnitName}...");
        
        // 简单动画延迟
        if (actor.HasMethod("play_anim")) actor.Call("play_anim", "attack");
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);

        var result = CombatResolver.ResolveAttack(actor, target, _hexGrid);
        
        if (result["hit"].AsBool())
        {
            int dmg = result["damage"].AsInt32();
            _combatUI.LogMessage($"[color=red]命中！造成 {dmg} 伤害。[/color]");
            
            if (target.CurrentHp <= 0)
            {
                _combatUI.LogMessage($"[color=yellow]{target.Data.UnitName} 被击败了。[/color]");
            }
        }
        else
        {
            _combatUI.LogMessage("未命中。");
        }

        if (actor.HasMethod("play_anim")) actor.Call("play_anim", "default");
    }

    private async Task ExecuteAiTurn()
    {
        await ToSignal(GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);

        if (_activePlayerUnit == null || _activePlayerUnit.CurrentHp <= 0)
        {
            _combatManager.EndCurrentTurn();
            return;
        }

        var aliveEnemies = _combatManager.EnemyUnits.Where(e => GodotObject.IsInstanceValid(e) && e.CurrentHp > 0).ToList();
        var alivePlayers = _combatManager.PlayerUnits.Where(p => GodotObject.IsInstanceValid(p) && p.CurrentHp > 0).ToList();

        // C# 信号处理方式
        _aiController.AllActionsCompleted += () => _combatManager.EndCurrentTurn();
        
        await _aiController.ExecuteEnemyTurn(aliveEnemies, alivePlayers, _hexGrid, _combatUI);
    }

    private void OnCombatEnded(bool victory)
    {
        ClearHighlights();
        _combatUI.SetActionBarVisible(false);

        if (victory)
        {
            _combatUI.SetTurnText("战斗胜利！", Colors.Green);
            // TODO: 结算 XP/金币/掉落
        }
        else
        {
            _combatUI.SetTurnText("战斗失败，全军覆没！", Colors.Red);
        }

        EmitSignal(SignalName.CombatFinished, victory);
    }

    // --- 交互与高亮 ---

    private void OnActionSelected(string action)
    {
        _currentActionMode = action;
        ClearHighlights();
        _combatUI.HighlightActionMode(action);

        if (_activePlayerUnit == null) return;

        switch (action)
        {
            case "move":
                _combatUI.LogMessage("选择移动：请点击蓝色高亮空地。");
                HighlightMoveRange(_activePlayerUnit);
                break;
            case "attack":
                var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
                _combatUI.LogMessage($"选择攻击：当前武器【{weapon?.ItemName ?? "徒手"}】。请点击红色高亮敌人。");
                HighlightAttackRange(_activePlayerUnit);
                break;
            case "reload":
                var w = _activePlayerUnit.GetMainHand() as WeaponData;
                if (w != null && w.IsCrossbow)
                {
                    if (_activePlayerUnit.Data!.IsRangedWeaponLoaded)
                        _combatUI.LogMessage("武器已装填。");
                    else
                        _combatUI.LogMessage($"装填需要 {w.ReloadCost} AP。再次点击该动作或确认后完成。");
                }
                break;
            case "end_turn":
                _combatUI.ShowConfirmDialog("确定要结束回合吗？", () => {
                    _combatManager.EndCurrentTurn();
                });
                break;
        }
    }

    private void ClearHighlights()
    {
        foreach (var cell in _highlightedCells)
        {
            cell.SetHighlight(false);
        }
        _highlightedCells.Clear();
    }

    private void HighlightMoveRange(Unit unit)
    {
        ClearHighlights();
        float movePoints = unit.GetAp(); // 使用 AP 作为移动力
        var coords = _hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, movePoints);
        foreach (var coord in coords)
        {
            var cell = _hexGrid.GetCell(coord.X, coord.Y);
            if (cell != null && cell.Occupant == null)
            {
                cell.SetHighlight(true, new Color(0.2f, 0.6f, 1.0f, 0.4f));
                _highlightedCells.Add(cell);
            }
        }
    }

    private void HighlightAttackRange(Unit unit)
    {
        ClearHighlights();
        var weapon = unit.GetMainHand() as WeaponData;
        int baseAtkRange = weapon?.RangeCells ?? 1;
        
        // 探测范围稍微扩大，以涵盖潜在的高地加成（最大+2）
        var coords = _hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, baseAtkRange + 2);
        foreach (var coord in coords)
        {
            var cell = _hexGrid.GetCell(coord.X, coord.Y);
            if (cell == null) continue;

            // 动态检查高地对射程的影响
            var hg = LineOfSight.GetHighGroundBonus(unit.GridPos, coord, _hexGrid);
            int currentRange = HexUtils.Distance(unit.GridPos.X, unit.GridPos.Y, coord.X, coord.Y);
            
            if (currentRange <= baseAtkRange + hg.RangeBonus)
            {
                cell.SetHighlight(true, new Color(1.0f, 0.2f, 0.2f, 0.4f));
                _highlightedCells.Add(cell);
            }
        }
    }

    private void HighlightSpellRange(Unit unit, SpellData spell)
    {
        ClearHighlights();
        var coords = _hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, spell.RangeCells);
        foreach (var coord in coords)
        {
            var cell = _hexGrid.GetCell(coord.X, coord.Y);
            if (cell != null)
            {
                cell.SetHighlight(true, new Color(1.0f, 0.5f, 0.0f, 0.4f));
                _highlightedCells.Add(cell);
            }
        }
    }
}
