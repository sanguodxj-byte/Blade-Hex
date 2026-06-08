// ICombatSkillPort.cs
// 战斗场景技能端口接口 — 提供技能瞄准和信息查询。
using BladeHex.Combat;
using BladeHex.Combat.Skills;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景技能端口。提供技能瞄准信息和目标验证。
/// </summary>
public interface ICombatSkillPort
{
	/// <summary>解析技能瞄准信息。</summary>
	SkillTargetingInfo? ResolveSkillTargetingInfo(string action);

	/// <summary>检查是否为即时释放目标类型（如 Self、AllAllies）。</summary>
	bool IsImmediateCastTargetType(string targetType);

	/// <summary>处理动作悬停事件。</summary>
	void OnActionHovered(string action);

	/// <summary>刷新当前悬停预览。</summary>
	void RefreshCurrentHover();
}
