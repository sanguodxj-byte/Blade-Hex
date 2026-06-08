// ICombatSelectionPort.cs
// 战斗场景选择端口接口 — 提供单位选择/取消选择操作。
using BladeHex.Combat;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景选择端口。提供单位选择和取消选择的操作入口。
/// </summary>
public interface ICombatSelectionPort
{
	/// <summary>选择指定单位为当前活跃单位。</summary>
	void SelectUnit(Unit unit);

	/// <summary>取消当前选中单位。</summary>
	void DeselectCurrentUnit();

	/// <summary>切换到下一个玩家单位。</summary>
	void CycleNextPlayerUnit();

	/// <summary>获取当前选中的玩家单位（只读）。</summary>
	Unit? ActivePlayerUnit { get; }
}
