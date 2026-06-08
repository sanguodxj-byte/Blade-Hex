// ICombatSceneAdapter.cs — 战斗场景适配器接口（Phase 3.4）
// AI 通过此接口与战斗场景交互，避免 HasMethod/Call 反射
using Godot;
using System.Threading.Tasks;

namespace BladeHex.Combat;

/// <summary>
/// 战斗场景适配器 — AI 与战斗场景的类型化交互契约
/// CombatScene 实现此接口，AIController 通过它执行移动/动画/日志
/// </summary>
public interface ICombatSceneAdapter
{
    /// <summary>移动单位到目标格(含动画)，传入完整路径时逐格检测借机攻击</summary>
    void MoveUnitTo(Unit unit, int q, int r, System.Collections.Generic.List<Godot.Vector2I>? path = null);

    /// <summary>播放单位动画</summary>
    void PlayUnitAnim(Unit unit, string animName);

    /// <summary>输出战斗日志</summary>
    void LogMessage(string message);

    /// <summary>更新单位 UI 信息</summary>
    void UpdateUnitInfo(Unit unit);

    /// <summary>播放攻击命中音效</summary>
    void PlayAttackHitSfx(int damageType, bool isCritical);

    /// <summary>播放攻击未命中音效</summary>
    void PlayAttackMissSfx(int damageType);

    /// <summary>播放指定音效</summary>
    void PlaySfx(string sfxName);

    /// <summary>在目标单位头顶弹出伤害/治疗/未命中数字</summary>
    /// <param name="target">目标单位</param>
    /// <param name="amount">数值;>0 = 伤害,&lt;0 = 治疗,=0 时配合 missLabel</param>
    /// <param name="isCritical">是否暴击</param>
    /// <param name="missLabel">非空时显示该字符串(如 "Miss"),amount 被忽略</param>
    void ShowDamageNumber(Unit target, int amount, bool isCritical = false, string? missLabel = null);

    /// <summary>单位被击杀后的统一场景收尾（格子、UI、先攻队列、战力条）。</summary>
    void OnUnitKilled(Unit dead, Unit killer);
}
