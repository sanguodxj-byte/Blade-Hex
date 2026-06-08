// ICombatTurnPort.cs
// 战斗场景回合端口接口 — 提供回合管理操作。
using System.Collections.Generic;
using BladeHex.Combat;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景回合端口。提供回合结束和状态查询。
/// </summary>
public interface ICombatTurnPort
{
	/// <summary>结束当前回合。</summary>
	void EndCurrentTurn();

	/// <summary>获取当前战斗状态。</summary>
	CombatManager.CombatState CurrentState { get; }

	/// <summary>检查是否为玩家回合。</summary>
	bool IsPlayerTurn { get; }

	/// <summary>检查单位是否为玩家阵营。</summary>
	bool IsPlayerUnit(Unit unit);

	/// <summary>获取所有玩家单位。</summary>
	IReadOnlyList<Unit> PlayerUnits { get; }

	/// <summary>获取所有敌方单位。</summary>
	IReadOnlyList<Unit> EnemyUnits { get; }
}
