// ICombatFeedbackPort.cs
// 战斗场景反馈端口接口 — 提供战斗日志、单位 UI 刷新、伤害数字显示。
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景反馈端口。提供战斗日志和视觉反馈的写入接口。
/// </summary>
public interface ICombatFeedbackPort
{
	/// <summary>写入战斗日志消息。</summary>
	void LogMessage(string message);

	/// <summary>刷新单位信息面板。</summary>
	void UpdateUnitInfo(Unit unit);

	/// <summary>在单位位置显示伤害数字。</summary>
	void ShowDamageNumber(Unit target, int amount, bool isCritical, string? missLabel = null);

	/// <summary>刷新 AP 消耗预览。</summary>
	void SetApPreview(float apCost);

	/// <summary>显示回合提示文本。</summary>
	void SetTurnText(string text, Color color);

	/// <summary>打开法术选择面板。</summary>
	void OpenSpellPanel(Unit unit, SpellManager spellManager);

	/// <summary>关闭法术选择面板。</summary>
	void CloseSpellPanel();

	/// <summary>打开自定义径向菜单。</summary>
	void OpenRadialMenuCustom(Vector2 screenPos, Godot.Collections.Dictionary options);

	/// <summary>隐藏单位检视面板。</summary>
	void HideUnitInspect();

	/// <summary>显示单位检视面板。</summary>
	void ShowUnitInspect(Unit unit, Vector2 screenPos);

	/// <summary>添加子节点（用于 UI 组件）。</summary>
	void AddChild(Node node);

	/// <summary>设置行动栏可见性。</summary>
	void SetActionBarVisible(bool visible);

	/// <summary>隐藏命中预览。</summary>
	void HideHitPreview();

	/// <summary>隐藏技能预览。</summary>
	void HideSkillPreview();

	/// <summary>显示命中预览。</summary>
	void ShowHitPreview(Vector2 screenPos, Unit attacker, Unit defender, HexGrid grid, int cover, int elevDiff, bool flanking, bool isCritical);

	/// <summary>显示射程外预览。</summary>
	void ShowOutOfRangePreview(Vector2 screenPos, Unit target, int distance, int range);

	/// <summary>显示行动力不足预览。</summary>
	void ShowApDeficientPreview(Vector2 screenPos, Unit target, float requiredAp, float currentAp);

	/// <summary>显示技能预览。</summary>
	void ShowSkillPreview(Vector2 screenPos, Unit caster, SkillTargetingInfo info, System.Collections.Generic.List<Unit> affectedUnits);
}
