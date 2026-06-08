// ICombatEndPort.cs
// 战斗场景结束端口接口 — 提供战斗结束触发入口。

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景结束端口。用于触发战斗结束流程。
/// </summary>
public interface ICombatEndPort
{
	void TriggerCombatEnd(bool victory);
}
