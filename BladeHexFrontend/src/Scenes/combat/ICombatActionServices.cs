// ICombatActionServices.cs
// 战斗场景行动服务接口 — 提供核心系统依赖和高层行动代理方法。
using System.Threading.Tasks;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Combat.Skills;
using BladeHex.Data;
using BladeHex.UI.Combat;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景行动服务。暴露核心系统引用和技能/法术/攻击等行动代理方法。
/// </summary>
public interface ICombatActionServices
{
	// ===== 核心系统依赖 =====
	CombatUI CombatUI { get; }
	CombatManager CombatManager { get; }
	SpellManager SpellManager { get; }
	HexGrid HexGrid { get; }
	CombatHighlightController HighlightCtrl { get; }
	CombatSkillExecutor SkillExecutor { get; }
	CombatActionPipeline ActionPipeline { get; }
	CombatTargetingController TargetingController { get; }

	// ===== 技能瞄准辅助 =====
	SkillTargetingInfo? ResolveSkillTargetingInfo(string action);
	void HighlightSkillRangeAction(string action);
	bool IsSkillTargetCellValid(string action, HexCell cell);
	bool IsImmediateCastTargetType(string targetType);

	// ===== 行动代理 =====
	Task HandleSpell(HexCell cell);
	Task HandleAttack(HexCell cell);
	void SelectUnit(Unit unit);
	void CycleNextPlayerUnit();
	void RefreshCurrentHover();
	void OnActionHovered(string action);
}
