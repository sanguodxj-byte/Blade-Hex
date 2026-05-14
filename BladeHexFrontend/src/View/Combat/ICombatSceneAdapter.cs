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
    /// <summary>移动单位到目标格(含动画)</summary>
    void MoveUnitTo(Unit unit, int q, int r);

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
}
