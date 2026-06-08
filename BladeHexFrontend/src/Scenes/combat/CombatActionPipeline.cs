// CombatActionPipeline.cs
// 战斗动作管线 — 单一动作入口，内部暂时委托给现有 dispatcher/executor。
using Godot;
using System;
using System.Threading.Tasks;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗动作管线。提供统一的动作执行入口，内部委托给现有 dispatcher/executor。
/// </summary>
[GlobalClass]
public partial class CombatActionPipeline : Node
{
	// ===== 小 ports 依赖 =====
	private ICombatSelectionContext? _selection;
	private CombatActionDispatcher? _dispatcher;
	private CombatSkillExecutor? _executor;

	// ===== 访问器（失败快速） =====
	private ICombatSelectionContext Selection => _selection ?? throw new InvalidOperationException("CombatActionPipeline not initialized. Call Initialize() first.");
	private CombatActionDispatcher Dispatcher => _dispatcher ?? throw new InvalidOperationException("CombatActionPipeline not initialized. Call Initialize() first.");
	private CombatSkillExecutor Executor => _executor ?? throw new InvalidOperationException("CombatActionPipeline not initialized. Call Initialize() first.");

	/// <summary>注入必要依赖。</summary>
	public void Initialize(ICombatSelectionContext selection, CombatActionDispatcher dispatcher, CombatSkillExecutor executor)
	{
		_selection = selection ?? throw new ArgumentNullException(nameof(selection));
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_executor = executor ?? throw new ArgumentNullException(nameof(executor));
	}

	/// <summary>分派高层动作（如 "move", "attack", "spell" 等按钮动作）</summary>
	public Task<CombatActionResult> DispatchActionAsync(CombatActionRequest request)
	{
		try
		{
			Dispatcher.OnActionSelected(request.Action);
			return Task.FromResult(CombatActionResult.Ok(actionType: request.Action));
		}
		catch (Exception ex)
		{
			return Task.FromResult(CombatActionResult.Fail($"Dispatch failed: {ex.Message}", actionType: request.Action));
		}
	}

	/// <summary>执行地块级动作（点击地块时的实际执行）</summary>
	public async Task<CombatActionResult> ExecuteCellActionAsync(CombatActionRequest request)
	{
		if (request.TargetCell == null)
			return CombatActionResult.Fail("No target cell specified.", actionType: request.Action);

		var mode = Selection.CurrentActionMode;
		if (mode == ActionMode.None)
		{
			mode = request.Action switch
			{
				"move" => ActionMode.Move,
				"attack" or "radial_attack" => ActionMode.Attack,
				"spell" => ActionMode.Spell,
				"item" => ActionMode.Item,
				_ => ActionMode.None,
			};
		}

		try
		{
			switch (mode)
			{
				case ActionMode.Move:
					return await Executor.HandleMove(request.TargetCell);
 
				case ActionMode.Attack:
					return await Executor.HandleAttack(request.TargetCell);
 
				case ActionMode.Spell:
					return await Executor.HandleSpell(request.TargetCell);
 
				case ActionMode.Item:
					return Executor.HandleItem(request.TargetCell);
 
				default:
					return CombatActionResult.Fail("No active action mode to execute on cell.", actionType: request.Action);
			}
		}
		catch (Exception ex)
		{
			return CombatActionResult.Fail($"Execution failed: {ex.Message}", actionType: request.Action);
		}
	}
}
