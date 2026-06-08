// ICombatUnitQuery.cs
// 战斗场景单位查询接口 — 只读访问单位状态，不暴露完整 CombatManager。
using System.Collections.Generic;
using BladeHex.Combat;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景单位查询。提供单位查找和状态查询的只读接口。
/// </summary>
public interface ICombatUnitQuery
{
	/// <summary>根据实例 ID 查找单位。</summary>
	Unit? FindUnitById(long instanceId);

	/// <summary>获取所有存活的玩家单位。</summary>
	IReadOnlyList<Unit> GetAlivePlayerUnits();

	/// <summary>获取所有存活的敌方单位。</summary>
	IReadOnlyList<Unit> GetAliveEnemyUnits();

	/// <summary>检查单位是否为玩家阵营。</summary>
	bool IsPlayerUnit(Unit unit);

	/// <summary>检查单位是否存活（HP > 0 且实例有效）。</summary>
	bool IsUnitAlive(Unit unit);
}
