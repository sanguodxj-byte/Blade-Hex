// CombatInputController.cs
// 从 CombatSceneBase 提取的输入控制器。
// 负责：键鼠输入处理、右键长按检测、OnCellClicked / OnCellRightClicked。
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.UI.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景输入控制器。接收玩家输入并转换为高层战斗命令。
/// </summary>
[GlobalClass]
public partial class CombatInputController : Node
{
    // ===== 小 ports 依赖 =====
    private ICombatSelectionContext? _selection;
    private ICombatHighlightPort? _highlight;
    private ICombatFeedbackPort? _feedback;
    private ICombatGridQuery? _gridQuery;
    private ICombatTurnPort? _turnPort;
    private ICombatSelectionPort? _selectionPort;
    private CombatActionPipeline? _pipeline;
    private CombatDeploymentController? _deployCtrl;

    // ===== 访问器（失败快速） =====
    private ICombatSelectionContext Selection => _selection ?? throw new InvalidOperationException("CombatInputController not initialized.");
    private ICombatHighlightPort Highlight => _highlight ?? throw new InvalidOperationException("CombatInputController not initialized.");
    private ICombatFeedbackPort Feedback => _feedback ?? throw new InvalidOperationException("CombatInputController not initialized.");
    private ICombatGridQuery GridQuery => _gridQuery ?? throw new InvalidOperationException("CombatInputController not initialized.");
    private ICombatTurnPort TurnPort => _turnPort ?? throw new InvalidOperationException("CombatInputController not initialized.");
    private ICombatSelectionPort SelectionPort => _selectionPort ?? throw new InvalidOperationException("CombatInputController not initialized.");

    public CombatCameraController? CameraCtrl { get; set; }

    /// <summary>注入必要依赖。</summary>
    public void Initialize(
        ICombatSelectionContext selection,
        ICombatHighlightPort highlight,
        ICombatFeedbackPort feedback,
        ICombatGridQuery gridQuery,
        ICombatTurnPort turnPort,
        ICombatSelectionPort selectionPort,
        CombatActionPipeline pipeline,
        CombatDeploymentController? deployCtrl = null,
        CombatCameraController? cameraCtrl = null)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _highlight = highlight ?? throw new ArgumentNullException(nameof(highlight));
        _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
        _gridQuery = gridQuery ?? throw new ArgumentNullException(nameof(gridQuery));
        _turnPort = turnPort ?? throw new ArgumentNullException(nameof(turnPort));
        _selectionPort = selectionPort ?? throw new ArgumentNullException(nameof(selectionPort));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _deployCtrl = deployCtrl;
        CameraCtrl = cameraCtrl;
    }

    // ===== 回调接口 =====
    public event Action<HexCell>? CellClicked;
    public event Action<HexCell>? CellRightClicked;
    public event Action? EndTurnRequested;
    public event Action? EscapePressed;
    public event Action? TabPressed;
    public event Action<int>? QuickSlotRequested;

    // ===== 长按检测 =====
    private HexCell? _longPressCell;
    private double _longPressTimer;
    private bool _longPressTriggered;
    private const double LongPressDuration = 0.8;

    // ===== 当前悬停地块 =====
    public HexCell? CurrentHoverCell { get; set; }

    // ===== 内部状态与 Tooltip 声明 =====
    private BladeHex.UI.Combat.TerrainTooltip? _terrainTooltip;

    private bool _deploymentPhaseActive => _deployCtrl != null && _deployCtrl.IsActive;

    // ===== 生命周期 =====

    public override void _UnhandledInput(InputEvent @event)
    {
        // 1. 若相机已被 UI 彻底阻断，拦截并丢弃所有 3D 交互事件
        if (CameraCtrl != null && CameraCtrl.IsInputBlocked)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        // 2. 键盘输入检测
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (Selection.Runtime.IsInteractionLocked)
            {
                if (keyEvent.Keycode != Key.Escape)
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            switch (keyEvent.Keycode)
            {
                case Key.Space:
                    if (TurnPort.IsPlayerTurn)
                    {
                        EndTurnRequested?.Invoke();
                    }
                    GetViewport().SetInputAsHandled();
                    return;
                case Key.Escape:
                    EscapePressed?.Invoke();
                    GetViewport().SetInputAsHandled();
                    return;
                case Key.Tab:
                    TabPressed?.Invoke();
                    GetViewport().SetInputAsHandled();
                    return;
                case Key.Key1: QuickSlotRequested?.Invoke(0); GetViewport().SetInputAsHandled(); return;
                case Key.Key2: QuickSlotRequested?.Invoke(1); GetViewport().SetInputAsHandled(); return;
                case Key.Key3: QuickSlotRequested?.Invoke(2); GetViewport().SetInputAsHandled(); return;
                case Key.Key4: QuickSlotRequested?.Invoke(3); GetViewport().SetInputAsHandled(); return;
                case Key.Key5: QuickSlotRequested?.Invoke(4); GetViewport().SetInputAsHandled(); return;
                case Key.Key6: QuickSlotRequested?.Invoke(5); GetViewport().SetInputAsHandled(); return;
                case Key.Key7: QuickSlotRequested?.Invoke(6); GetViewport().SetInputAsHandled(); return;
                case Key.Key8: QuickSlotRequested?.Invoke(7); GetViewport().SetInputAsHandled(); return;
                case Key.Key9: QuickSlotRequested?.Invoke(8); GetViewport().SetInputAsHandled(); return;
                case Key.Key0: QuickSlotRequested?.Invoke(9); GetViewport().SetInputAsHandled(); return;
            }
        }

        // 3. 鼠标滚轮缩放检测（转发给 CameraCtrl）
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
            {
                if (CameraCtrl != null)
                {
                    CameraCtrl.ProcessWheelZoom(mb);
                    GetViewport().SetInputAsHandled();
                }
                return;
            }
        }

        // 4. 右键长按检视触发
        if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
        {
            if (Selection.Runtime.IsInteractionLocked)
            {
                GetViewport().SetInputAsHandled();
                return;
            }
            if (rmb.Pressed)
            {
                _longPressCell = CurrentHoverCell;
                _longPressTimer = 0;
                _longPressTriggered = false;
            }
            else
            {
                if (_longPressTriggered)
                {
                    _longPressTriggered = false;
                    _longPressCell = null;
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _longPressCell = null;
                    // 非长按下的松开右键，触发右击事件
                    if (CurrentHoverCell != null)
                    {
                        OnCellRightClicked(CurrentHoverCell);
                    }
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        // 限制：在输入被阻断时不响应右键长按累加
        if (CameraCtrl != null && CameraCtrl.IsInputBlocked) return;

        // 右键长按检视计时器
        if (_longPressCell != null && !_longPressTriggered)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Right))
            {
                _longPressTimer += delta;
                if (_longPressTimer >= LongPressDuration)
                {
                    _longPressTriggered = true;
                    if (_longPressCell.Occupant != null && GodotObject.IsInstanceValid(_longPressCell.Occupant))
                    {
                        var screenPos = GetViewport().GetMousePosition();
                        Feedback.ShowUnitInspect(_longPressCell.Occupant, screenPos);
                    }
                }
            }
            else
            {
                _longPressCell = null;
                _longPressTimer = 0;
            }
        }
    }

    // ===== 信号/地块点击入口 (从 Base 迁移至此) =====

    public void OnCellClicked(HexCell cell)
    {
        if (Selection.Runtime.IsInteractionLocked || Selection.IsExecutingAction) return; // 正在执行动作，防连击

        // 部署阶段：交由部署逻辑处理
        if (_deploymentPhaseActive)
        {
            _deployCtrl?.HandleClick(cell);
            return;
        }

        if (!TurnPort.IsPlayerTurn) return;

        // 隐藏地形信息浮窗
        _terrainTooltip?.HidePanel();

        var activeUnit = Selection.ActivePlayerUnit;
        if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit)) return;

        // ===== 瞄准模式优先处理（通过 Pipeline） =====
        if (Selection.CurrentActionMode == ActionMode.Spell)
        {
            _ = _pipeline!.ExecuteCellActionAsync(new CombatActionRequest
            {
                Action = "spell",
                Actor = activeUnit,
                TargetCell = cell,
                Spell = Selection.SelectedSpell,
                SkillAction = Selection.SelectedSkillAction
            });
            return;
        }
        if (Selection.CurrentActionMode == ActionMode.Item)
        {
            _ = _pipeline!.ExecuteCellActionAsync(new CombatActionRequest
            {
                Action = "item",
                Actor = activeUnit,
                TargetCell = cell
            });
            return;
        }
        if (Selection.CurrentActionMode == ActionMode.Attack)
        {
            if (TryGetAttackableBattleAnchor(cell, activeUnit, out _, reportFailure: true))
            {
                _ = _pipeline!.ExecuteCellActionAsync(new CombatActionRequest
                {
                    Action = "attack",
                    Actor = activeUnit,
                    TargetCell = cell
                });
                return;
            }

            if (cell.Occupant != null && !TurnPort.IsPlayerUnit(cell.Occupant) && cell.Occupant.CurrentHp > 0)
            {
                var activeCell = GridQuery.GetCell(activeUnit.GridPos);
                if (CombatAttackRules.IsMeleeElevationBlocked(activeUnit, cell.Occupant, activeCell, cell))
                {
                    Feedback.LogMessage(CombatAttackRules.MeleeElevationBlockedReason);
                    return;
                }

                _ = _pipeline!.ExecuteCellActionAsync(new CombatActionRequest
                {
                    Action = "attack",
                    Actor = activeUnit,
                    TargetCell = cell
                });
                return;
            }
            Selection.Runtime.CancelAction();
            Highlight.ClearHighlights();
            Highlight.ShowSelectedUnitHighlights();
            return;
        }

        if (TryGetAttackableBattleAnchor(cell, activeUnit, out _, reportFailure: false))
        {
            Selection.Runtime.EnterAttackMode();
            Highlight.HighlightAttackRange(activeUnit);
            _ = _pipeline!.ExecuteCellActionAsync(new CombatActionRequest
            {
                Action = "attack",
                Actor = activeUnit,
                TargetCell = cell
            });
            return;
        }

        // ===== 常规交互 =====
        if (cell.Occupant != null && TurnPort.IsPlayerUnit(cell.Occupant) && cell.Occupant.CurrentHp > 0)
        {
            SelectionPort.SelectUnit(cell.Occupant);
            return;
        }

        if (cell.Occupant != null && !TurnPort.IsPlayerUnit(cell.Occupant) && cell.Occupant.CurrentHp > 0)
        {
            var weapon = activeUnit.GetMainHand() as WeaponData;
            int effectiveRange = activeUnit.GetWeaponRange();
            int dist = GridQuery.GetAxialDistance(activeUnit.GridPos, cell.GridPos);
            int apCost = weapon?.ApCost ?? 4;

            if (dist <= effectiveRange && activeUnit.CurrentAp >= apCost)
            {
                var activeCell = GridQuery.GetCell(activeUnit.GridPos);
                if (CombatAttackRules.IsMeleeElevationBlocked(activeUnit, cell.Occupant, activeCell, cell))
                {
                    Feedback.LogMessage(CombatAttackRules.MeleeElevationBlockedReason);
                    return;
                }

                Selection.Runtime.EnterAttackMode();
                Highlight.HighlightAttackRange(activeUnit);
                _ = _pipeline!.ExecuteCellActionAsync(new CombatActionRequest
                {
                    Action = "attack",
                    Actor = activeUnit,
                    TargetCell = cell
                });
            }
            else
            {
                if (dist > effectiveRange)
                    Feedback.LogMessage($"目标超出射程:{cell.Occupant.Data?.UnitName ?? "未知"} (距离 {dist}, 射程 {effectiveRange})");
                else
                    Feedback.LogMessage($"行动力不足:{cell.Occupant.Data?.UnitName ?? "未知"} (需要 {apCost}, 当前 {activeUnit.CurrentAp:F0})");
            }
            return;
        }

        if (cell.Occupant == null && activeUnit.CurrentAp >= 1)
        {
            bool isHighlighted = Highlight.HighlightedCellsContains(cell);

            if (isHighlighted)
            {
                _ = _pipeline!.ExecuteCellActionAsync(new CombatActionRequest
                {
                    Action = "move",
                    Actor = activeUnit,
                    TargetCell = cell
                });
                return;
            }

            if (cell.Data == null || cell.Data.isPassable)
            {
                var path = GridQuery.FindPath(activeUnit.GridPos, cell.GridPos);
                if (path != null && path.Count >= 1)
                {
                    float pathCost = GridQuery.GetPathCost(activeUnit.GridPos, path);
                    if (pathCost <= activeUnit.CurrentAp)
                    {
                        Selection.Runtime.CancelAction();
                        Highlight.ClearHighlights();
                        _ = _pipeline!.ExecuteCellActionAsync(new CombatActionRequest
                        {
                            Action = "move",
                            Actor = activeUnit,
                            TargetCell = cell
                        });
                        return;
                    }
                }
            }

            if (Selection.CurrentActionMode == ActionMode.None)
                Highlight.ShowSelectedUnitHighlights();
        }

        CellClicked?.Invoke(cell);
    }

    private bool TryGetAttackableBattleAnchor(
        HexCell cell,
        Unit activeUnit,
        out CombatManager.BattleAnchorState anchor,
        bool reportFailure)
    {
        anchor = default!;
        var combatManager = activeUnit.CombatManager;
        if (combatManager == null || !combatManager.TryGetBattleAnchorAt(cell.GridPos, out anchor))
            return false;

        var check = CombatAttackRules.CanAttackBattleAnchor(activeUnit, anchor);
        if (!check.CanAttack)
        {
            if (reportFailure && !string.IsNullOrEmpty(check.Reason))
                Feedback.LogMessage(check.Reason);
            return false;
        }

        return true;
    }

    public void OnCellRightClicked(HexCell cell)
    {
        if (Selection.Runtime.IsInteractionLocked) return;
        if (!TurnPort.IsPlayerTurn) return;
        if (_longPressTriggered) return;

        Feedback.HideUnitInspect();

        if (Selection.CurrentActionMode == ActionMode.Spell || Selection.CurrentActionMode == ActionMode.Item)
        {
            Selection.Runtime.CancelAction();
            Highlight.ClearHighlights();
            Feedback.SetApPreview(0f);
            if (Selection.ActivePlayerUnit != null) Highlight.ShowSelectedUnitHighlights();
            Feedback.LogMessage("取消操作。");
            return;
        }

        var activeUnit = Selection.ActivePlayerUnit;
        if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit)) return;

        if (cell.Occupant != null)
        {
            Selection.RightClickTarget = cell;
            var screenPos = GetViewport().GetMousePosition();
            Feedback.ShowUnitInspect(cell.Occupant, screenPos);
            return;
        }

        if (TryGetBattleAnchorAtCell(cell, activeUnit, out var battleAnchor, out var anchorAttackCheck))
        {
            var opts = new Godot.Collections.Dictionary();
            string label = anchorAttackCheck.CanAttack
                ? $"⚔ 攻击战旗 ({battleAnchor.Hp}HP / {battleAnchor.Duration}回合)"
                : $"战旗 ({battleAnchor.Hp}HP / {battleAnchor.Duration}回合)";
            opts[label] = anchorAttackCheck.CanAttack ? "radial_attack" : "none";
            opts["✕ 取消"] = "none";
            Selection.RightClickTarget = cell;
            var screenPos = GetViewport().GetMousePosition();
            Feedback.OpenRadialMenuCustom(screenPos, opts);
            if (!anchorAttackCheck.CanAttack && !string.IsNullOrEmpty(anchorAttackCheck.Reason))
                Feedback.LogMessage(anchorAttackCheck.Reason);
            return;
        }

        if (cell.Data != null)
        {
            if (cell.Data.terrainType == BattleCellData.TerrainType.Rampart && !cell.Data.HasLadder)
            {
                var (canLadder, ladderReason) = BladeHex.Combat.SiegeActions.CanBuildLadder(
                    activeUnit.GridPos, cell.Data, cell.GridPos, activeUnit.CurrentAp);
                if (canLadder)
                {
                    var opts = new Godot.Collections.Dictionary();
                    int progress = cell.Data.ladderProgress;
                    opts[$"🪜 架设云梯 ({progress}/3) [-8AP]"] = "build_ladder";
                    opts["✕ 取消"] = "none";
                    Selection.RightClickTarget = cell;
                    var screenPos = GetViewport().GetMousePosition();
                    Feedback.OpenRadialMenuCustom(screenPos, opts);
                    return;
                }
                else if (!string.IsNullOrEmpty(ladderReason))
                {
                    Feedback.LogMessage(ladderReason);
                }
            }

            if (cell.Data.isDestructible && cell.Data.durability > 0)
            {
                var weapon = activeUnit.GetMainHand() as WeaponData;
                int apCost = weapon?.ApCost ?? 4;
                var (canAttack, attackReason) = BladeHex.Combat.SiegeActions.CanAttackDestructible(
                    activeUnit.GridPos, cell.Data, cell.GridPos, activeUnit.CurrentAp, apCost);
                if (canAttack)
                {
                    var opts = new Godot.Collections.Dictionary();
                    opts[$"⚔ 攻击城门 ({cell.Data.durability}/{cell.Data.maxDurability}HP)"] = "attack_gate";
                    opts["✕ 取消"] = "none";
                    Selection.RightClickTarget = cell;
                    var screenPos = GetViewport().GetMousePosition();
                    Feedback.OpenRadialMenuCustom(screenPos, opts);
                    return;
                }
                else if (!string.IsNullOrEmpty(attackReason))
                {
                    Feedback.LogMessage(attackReason);
                }
            }

            var terrainScreenPos = GetViewport().GetMousePosition();
            _terrainTooltip ??= new BladeHex.UI.Combat.TerrainTooltip();
            if (!_terrainTooltip.IsInsideTree())
                Feedback.AddChild(_terrainTooltip);
            _terrainTooltip.ShowTerrain(cell.Data, terrainScreenPos);
        }

        Selection.Runtime.ResetActionMode();
        Highlight.ClearHighlights();
        if (activeUnit.CurrentAp >= 1)
            Highlight.ShowSelectedUnitHighlights();

        CellRightClicked?.Invoke(cell);
    }

    private bool TryGetBattleAnchorAtCell(
        HexCell cell,
        Unit activeUnit,
        out CombatManager.BattleAnchorState anchor,
        out (bool CanAttack, string Reason, int Distance, int Range, int ApCost) attackCheck)
    {
        anchor = default!;
        attackCheck = default;
        var combatManager = activeUnit.CombatManager;
        if (combatManager == null || !combatManager.TryGetBattleAnchorAt(cell.GridPos, out anchor))
            return false;

        attackCheck = CombatAttackRules.CanAttackBattleAnchor(activeUnit, anchor);
        return anchor.Destructible;
    }
}
