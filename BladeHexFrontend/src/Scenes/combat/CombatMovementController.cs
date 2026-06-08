// CombatMovementController.cs
// 战斗场景移动控制器 — 负责单位移动、抛物线动画、借机攻击（AoO）判定与逻辑。
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.View.Combat;
using BladeHex.UI.Combat;
using BladeHex.UI.Minimap;

namespace BladeHex.Scenes;

[GlobalClass]
public partial class CombatMovementController : Node
{
	// ===== 依赖注入 =====
	private HexGrid? _hexGrid;
	private CombatManager? _combatManager;
	private CombatUI? _combatUi;
	private Node3D? _parentScene;

	public HexGrid HexGrid => _hexGrid ?? throw new InvalidOperationException("CombatMovementController not initialized.");
	public CombatManager CombatManager => _combatManager ?? throw new InvalidOperationException("CombatMovementController not initialized.");
	public CombatUI CombatUI => _combatUi ?? throw new InvalidOperationException("CombatMovementController not initialized.");
	public CombatMinimapPanel? MinimapPanel { get; set; }
	public CombatCameraController? CameraCtrl { get; set; }
	public Node3D ParentScene => _parentScene ?? throw new InvalidOperationException("CombatMovementController not initialized.");

	/// <summary>注入必要依赖。</summary>
	public void Initialize(HexGrid hexGrid, CombatManager combatManager, CombatUI combatUi, Node3D parentScene, CombatMinimapPanel? minimapPanel = null)
	{
		_hexGrid = hexGrid ?? throw new ArgumentNullException(nameof(hexGrid));
		_combatManager = combatManager ?? throw new ArgumentNullException(nameof(combatManager));
		_combatUi = combatUi ?? throw new ArgumentNullException(nameof(combatUi));
		_parentScene = parentScene ?? throw new ArgumentNullException(nameof(parentScene));
		MinimapPanel = minimapPanel;
	}

	/// <summary>对外兼容的入口：异步移动单位到指定坐标（自动分流）</summary>
	public async void MoveUnitTo(Unit unit, int q, int r, List<Vector2I>? path = null)
	{
		try
		{
			await MoveUnitToAsync(unit, q, r, path);
		}
		catch (Exception ex)
		{
			GD.PushError($"[CombatMovementController] MoveUnitTo: {ex.Message}");
		}
	}

	/// <summary>内部异步核心方法</summary>
	public async Task MoveUnitToAsync(Unit unit, int q, int r, List<Vector2I>? path = null)
	{
		if (path != null && path.Count > 0)
		{
			await MoveUnitAlongPath(unit, path);
			return;
		}
		await MoveUnitOneStep(unit, new Vector2I(q, r));
	}

	/// <summary>按指定路径逐格移动，包含借机攻击判定</summary>
	private async Task MoveUnitAlongPath(Unit unit, List<Vector2I> path)
	{
		if (path == null || path.Count == 0) return;

		// 收集对该单位有威胁的敌方单位列表
		bool isPlayer = !unit.Data!.IsEnemy;
		var enemies = (isPlayer ? CombatManager.EnemyUnits : CombatManager.PlayerUnits)
			.Where(u => GodotObject.IsInstanceValid(u) && u.CurrentHp > 0)
			.ToArray();

		// v1 职业被动: 荒原之心 — 同高度移动免借机攻击
		bool warchiefSameHeightNoAoo = unit.HasCareerSkillEffect("warchief_same_height_charge")
			&& HexGrid != null && IsSameHeightPath(unit, path, HexGrid);

		// 一次性检测整条路径上的 AoO
		for (int i = 0; i < path.Count - 1; i++)
		{
			if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) break;

			var fromPos = (i == 0) ? unit.GridPos : path[i];
			var toPos = path[i + 1];

			// v1 职业被动: 荒原之心 — 同高度免借机
			if (warchiefSameHeightNoAoo) continue;

			// v1 职业被动: 游骑兵 — 奔袭免借机剩余格数
			int freeNoAoo = unit.Data?.Runtime?.CareerFreeMoveNoAooCellsRemaining ?? 0;
			if (freeNoAoo > 0)
			{
				unit.Data.Runtime.CareerFreeMoveNoAooCellsRemaining--;
				continue;
			}

			var aooTrigger = FacingSystem.ShouldTriggerAoo(unit, fromPos, toPos, enemies);
			if (aooTrigger != null && GodotObject.IsInstanceValid(aooTrigger) && aooTrigger.CurrentHp > 0)
			{
				CombatUI.LogMessage($"[color=orange]{aooTrigger.Data!.UnitName} 发动借机攻击！[/color]");
				var aooResult = CombatResolver.ResolveAttackOfOpportunity(aooTrigger, unit, HexGrid);

				if (aooResult["hit"].AsBool())
				{
					int aooDmg = aooResult["damage"].AsInt32();
					BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, unit, aooDmg, false);
					CombatUI.LogMessage($"[color=red]{aooTrigger.Data.UnitName} 借机命中 {unit.Data?.UnitName}，造成 {aooDmg} 伤害。[/color]");
					CombatUI.UpdateUnitInfo(unit);
					if (unit.CurrentHp <= 0)
					{
						CombatUI.LogMessage($"[color=yellow]{unit.Data?.UnitName} 在移动中被击倒！[/color]");
						CombatManager.HandleUnitKilled(unit, aooTrigger);
						return; // 单位死亡，中止移动
					}
				}
				else
				{
					if (aooResult.ContainsKey("blocked_by_elevation") && aooResult["blocked_by_elevation"].AsBool())
					{
						string reason = aooResult.ContainsKey("reason") ? aooResult["reason"].AsString() : CombatAttackRules.MeleeElevationBlockedReason;
						CombatUI.LogMessage($"[color=gray]{aooTrigger.Data.UnitName} 无法借机攻击：{reason}[/color]");
						continue;
					}

					BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, unit, 0, missLabel: "Miss");
					CombatUI.LogMessage($"[color=gray]{aooTrigger.Data.UnitName} 借机未命中。[/color]");
				}
			}
		}

		if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) return;

		// 获取终点坐标
		var finalDest = path[path.Count - 1];
		var finalCell = HexGrid.GetCell(finalDest.X, finalDest.Y);
		if (finalCell == null) return;

		// 清除起点占用
		var oldCell = HexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
		if (oldCell != null) oldCell.Occupant = null;

		// 设置终点占用
		finalCell.Occupant = unit;

		// 计算朝向（面向终点）
		int nextFacing = HexUtils.GetFacingDirection(unit.GridPos, finalDest);
		unit.Facing = nextFacing;

		Vector3 startPos = unit.Position;
		Vector3 targetPos = finalCell.Position + new Vector3(0, CombatLayerHeight.HexTopOffset + CombatLayerHeight.CharacterLayer, 0);

		// 播放移动动画
		CameraCtrl?.LockOnUnit(unit);
		unit.PlayAnim("move");

		// 计算移动距离，动态调整持续时间
		float distance = (targetPos - startPos).Length();
		float baseDuration = unit.MoveStepDuration * 2.0f;
		float duration = Mathf.Max(baseDuration, distance * 0.001f);

		// 使用 Tween 进行平滑点对点移动 + 低抛物线
		var tween = GetTree().CreateTween();
		tween.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);

		// 水平位移由 position tween 控制，Y 轴由 jumpTween 控制，避免两个 tween 争抢 Y。
		var horizontalTarget = new Vector3(targetPos.X, startPos.Y, targetPos.Z);
		tween.TweenProperty(unit, "position", horizontalTarget, duration)
			.SetTrans(Tween.TransitionType.Sine);

		// 垂直跳跃
		float jumpHeight = unit.JumpMaxHeight * 0.5f;
		float halfDuration = duration * 0.5f;
		var jumpTween = GetTree().CreateTween();

		// 上升阶段。Godot 3D 的 Y 轴向上。
		jumpTween.TweenProperty(unit, "position:y", startPos.Y + jumpHeight, halfDuration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);

		// 下降阶段
		jumpTween.TweenProperty(unit, "position:y", targetPos.Y, halfDuration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);

		// 等待动画完成
		await ToSignal(tween, Tween.SignalName.Finished);

		unit.Position = targetPos;

		// 更新逻辑坐标
		unit.GridPos = finalDest;

		// 恢复待机动画
		unit.PlayAnim("default");

		MinimapPanel?.Refresh();
	}

	/// <summary>移动单位到相邻一格（带抛物线跳跃动画）</summary>
	private async Task MoveUnitOneStep(Unit unit, Vector2I dest)
	{
		var oldCell = HexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
		if (oldCell != null) oldCell.Occupant = null;
		var newCell = HexGrid.GetCell(dest.X, dest.Y);
		if (newCell == null) return;

		newCell.Occupant = unit;

		// 计算朝向
		int nextFacing = HexUtils.GetFacingDirection(unit.GridPos, dest);
		unit.Facing = nextFacing;

		Vector3 startPos = unit.Position;
		Vector3 targetPos = newCell.Position + new Vector3(0, CombatLayerHeight.HexTopOffset + CombatLayerHeight.CharacterLayer, 0);

		// 播放移动动画
		CameraCtrl?.LockOnUnit(unit);
		unit.PlayAnim("move");

		// 使用 Tween 进行平滑位移 + 低抛物线
		float duration = unit.MoveStepDuration;
		float halfDuration = duration * 0.5f;
		float jumpHeight = unit.JumpMaxHeight * 0.5f;

		var tween = GetTree().CreateTween();
		tween.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);

		// 水平位移由 position tween 控制，Y 轴由 jumpTween 控制，避免两个 tween 争抢 Y。
		var horizontalTarget = new Vector3(targetPos.X, startPos.Y, targetPos.Z);
		tween.TweenProperty(unit, "position", horizontalTarget, duration)
			.SetTrans(Tween.TransitionType.Sine);

		// 垂直跳跃
		var jumpTween = GetTree().CreateTween();

		// 上升阶段。Godot 3D 的 Y 轴向上。
		jumpTween.TweenProperty(unit, "position:y", startPos.Y + jumpHeight, halfDuration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);

		// 下降阶段
		jumpTween.TweenProperty(unit, "position:y", targetPos.Y, halfDuration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);

		// 等待动画完成
		await ToSignal(tween, Tween.SignalName.Finished);

		unit.Position = targetPos;

		// 更新逻辑坐标
		unit.GridPos = dest;

		// 恢复待机动画
		unit.PlayAnim("default");

		MinimapPanel?.Refresh();
	}

	/// <summary>
	/// 荒原之心: 检查整条路径是否为同一高度 (用于同高度免借机判定)
	/// </summary>
	private static bool IsSameHeightPath(Unit unit, List<Vector2I> path, HexGrid grid)
	{
		var startCell = grid.GetCell(unit.GridPos.X, unit.GridPos.Y);
		if (startCell == null) return false;
		int firstElevation = startCell.Elevation;

		foreach (var pos in path)
		{
			var cell = grid.GetCell(pos.X, pos.Y);
			if (cell == null || cell.Elevation != firstElevation)
				return false;
		}
		return true;
	}
}
