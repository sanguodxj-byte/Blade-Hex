// ICombatCameraPort.cs
// 战斗场景相机端口接口 — 提供相机聚焦和屏幕坐标辅助。
using Godot;
using BladeHex.Combat;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景相机端口。提供相机控制的最小接口。
/// </summary>
public interface ICombatCameraPort
{
	/// <summary>相机聚焦到指定世界坐标。</summary>
	void FocusCameraOn(Vector3 worldPosition, float duration = 0.4f);

	/// <summary>相机锁定到指定单位。</summary>
	void LockOnUnit(Unit unit);

	/// <summary>相机居中到指定单位。</summary>
	void CenterCameraOnUnit(Unit unit);
}
