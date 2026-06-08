// CombatActionRequest.cs
// 战斗动作请求 — 为后续 action pipeline 做类型入口。
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗动作请求。封装一次战斗动作的所有输入参数。
/// </summary>
public class CombatActionRequest
{
	/// <summary>动作标识（如 "move", "attack", "spell", "skill_xxx"）</summary>
	public string Action { get; init; } = "";

	/// <summary>执行动作的单位</summary>
	public Unit? Actor { get; init; }

	/// <summary>目标地块</summary>
	public HexCell? TargetCell { get; init; }

	/// <summary>选中的法术（施法时）</summary>
	public SpellData? Spell { get; init; }

	/// <summary>选中的技能动作标识（技能释放时）</summary>
	public string? SkillAction { get; init; }
}
