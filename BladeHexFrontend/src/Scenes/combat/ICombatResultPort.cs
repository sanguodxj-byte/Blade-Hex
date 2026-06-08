// ICombatResultPort.cs
// 战斗场景结果端口接口 — 提供战斗结束和结果展示入口。
namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景结果端口。用于触发战斗结束流程和展示结果。
/// </summary>
public interface ICombatResultPort
{
	/// <summary>触发战斗结束。</summary>
	void TriggerCombatEnd(bool victory);

	/// <summary>显示战斗结果面板。</summary>
	void ShowCombatResult(bool victory);
}
