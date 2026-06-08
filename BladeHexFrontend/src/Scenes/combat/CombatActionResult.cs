// CombatActionResult.cs
// 战斗动作结果 — 为后续 action pipeline 做类型入口。
using BladeHex.Combat.Commands;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗动作执行结果。包含成功/失败状态、消耗的 AP、命令结果载荷。
/// </summary>
public class CombatActionResult
{
	/// <summary>动作是否成功执行</summary>
	public bool Success { get; init; }

	/// <summary>失败原因（Success=false 时有值）</summary>
	public string? FailureReason { get; init; }

	/// <summary>动作类型标识（如 "move", "attack", "spell"）</summary>
	public string? ActionType { get; init; }

	/// <summary>命令结果载荷（类型化的 CommandPayload）</summary>
	public CommandPayload? CommandResult { get; init; }

	/// <summary>消耗的行动力点数</summary>
	public float ConsumedAp { get; init; }

	/// <summary>是否应该结束回合</summary>
	public bool ShouldEndTurn { get; init; }

	/// <summary>创建成功结果</summary>
	public static CombatActionResult Ok(
		string? actionType = null,
		CommandPayload? commandResult = null,
		float consumedAp = 0f,
		bool shouldEndTurn = false)
		=> new()
		{
			Success = true,
			ActionType = actionType,
			CommandResult = commandResult,
			ConsumedAp = consumedAp,
			ShouldEndTurn = shouldEndTurn
		};

	/// <summary>创建失败结果</summary>
	public static CombatActionResult Fail(string failureReason, string? actionType = null)
		=> new()
		{
			Success = false,
			FailureReason = failureReason,
			ActionType = actionType
		};
}
