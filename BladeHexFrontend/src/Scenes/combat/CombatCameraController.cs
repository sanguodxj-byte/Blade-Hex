// CombatCameraController.cs
// 战斗场景相机控制器 — 从 CombatSceneBase 提取的独立 Node 组件。
// 负责：相机初始/配置、缩限缩放、WASD 移动、滚轮缩放、镜头聚焦、小地图视野框。
using Godot;
using System;
using System.Threading.Tasks;
using BladeHex.View.Camera;
using BladeHex.UI.Minimap;
using BladeHex.UI.Combat;
using BladeHex.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景相机控制器。对外提供相机操作接口，自身持有 Camera3D 子节点。
/// 通过 [Export] 接收依赖（CombatUI、CombatMinimapPanel），零耦合回 CombatSceneBase。
/// </summary>
[GlobalClass]
public partial class CombatCameraController : Node3D
{
	// ===== 依赖注入 =====
	/// <summary>战场边界（含边距），用于缩限和居中</summary>
	public Aabb? BattlefieldBounds { get; set; }

	/// <summary>相机可平移边界（扩展后的边界）</summary>
	public Aabb? CameraPanBounds { get; set; }

	/// <summary>UI 层（用于读取 UI insets）</summary>
	public CombatUI? CombatUI { get; set; }

	/// <summary>小地图面板（视野更新时同步）</summary>
	public CombatMinimapPanel? MinimapPanel { get; set; }

	// ===== 内部状态 =====
	private Camera3D? _camera;
	private float _maxOrthoSize = 2000f;

	private const float MinOrthoSize = 200f;
	private const float CameraPanPaddingHexes = 6.0f;
	private const float TiltDegrees = -45f;

	/// <summary>当前活跃的过渡 Tween（用于冲突检测与取消）</summary>
	private Tween? _transitionTween;

	// ===== 阻断控制 =====
	private int _inputBlockCount = 0;

	public void PushInputBlock()
	{
		_inputBlockCount++;
	}

	public void PopInputBlock()
	{
		_inputBlockCount = Math.Max(0, _inputBlockCount - 1);
	}

	public bool IsInputBlocked => _inputBlockCount > 0;

	// ===== 锁定相机状态 =====
	/// <summary>
	/// 相机锁定模式枚举
	/// None=自由平移， Unit=跟随单位， WorldPos=跟随世界坐标
	/// </summary>
	private enum LockMode { None, Unit, WorldPos }
	private LockMode _lockMode = LockMode.None;
	private Unit? _lockUnit;         // 锁定目标单位（移动跟随）
	private Vector3 _lockWorldPos;   // 锁定世界坐标（投射物跟随）
	private float _lockOrthoSize;    // 锁定期间的目标缩放（默认 0 = 不切换缩放）

	/// <summary>是否处于锁定跟随状态</summary>
	public bool IsLocked => _lockMode != LockMode.None;

	/// <summary>锁定相机到指定单位（单位移动时相机硬跟随）</summary>
	/// <param name="unit">目标单位</param>
	/// <param name="orthoSize">目标缩放值，0表示保持当前缩放不变</param>
	public void LockOnUnit(Unit unit, float orthoSize = 0f)
	{
		if (unit == null || !IsInstanceValid(unit)) return;

		// 去重：已锁定同一单位且不需要改缩放时跳过，避免重复触发跟随逻辑
		if (_lockMode == LockMode.Unit
			&& _lockUnit != null && IsInstanceValid(_lockUnit)
			&& ReferenceEquals(_lockUnit, unit)
			&& (orthoSize <= 0f || Mathf.IsEqualApprox(_lockOrthoSize, orthoSize)))
		{
			return;
		}

		_lockUnit = unit;
		_lockMode = LockMode.Unit;
		_lockOrthoSize = orthoSize;
	}

	/// <summary>锁定相机到指定世界坐标（投射物跟随）</summary>
	public void LockOnWorldPos(Vector3 worldPos, float orthoSize = 0f)
	{
		_lockWorldPos = worldPos;
		_lockMode = LockMode.WorldPos;
		_lockOrthoSize = orthoSize;
	}

	/// <summary>更新投射物跟随坐标（在投射物飞行每帧调用）</summary>
	public void UpdateLockWorldPos(Vector3 worldPos)
	{
		if (_lockMode == LockMode.WorldPos)
			_lockWorldPos = worldPos;
	}

	/// <summary>解除相机锁定，恢复自由平移</summary>
	public void Unlock()
	{
		_lockMode = LockMode.None;
		_lockUnit = null;
	}

	// ===== 属性 =====

	/// <summary>当前正交相机</summary>
	public Camera3D? Camera => _camera;

	/// <summary>当前正交缩放值</summary>
	public float OrthoSize => _camera?.Size ?? 0f;

	/// <summary>是否已缩放到最大（可见整个战场）</summary>
	public bool IsAtMaxZoom => _camera != null && _camera.Size >= _maxOrthoSize;

	// ===== 生命周期 =====

	public override void _Ready()
	{
		InitializeCamera();
	}

	public override void _Process(double delta)
	{
		// 相机锁定跟随状态下，必须每帧更新相机位置
		if (_lockMode != LockMode.None)
		{
			// 检查用户是否手动平移（WASD），如果是则解锁
			if (_lockMode == LockMode.Unit && IsUserPanning())
			{
				Unlock();
			}
			else
			{
				ApplyLockTracking();
				return; // 锁定期间禁用手动输入
			}
		}

		if (IsInputBlocked) return;
		ProcessWASD(delta);
	}

	/// <summary>检查用户是否正在手动平移（WASD）</summary>
	private bool IsUserPanning()
	{
		return Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.A) ||
			   Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.D) ||
			   Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.Left) ||
			   Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.Right);
	}

	/// <summary>将相机对齐到锁定目标（每帧调用，速度自适应平滑跟随）</summary>
	private void ApplyLockTracking()
	{
		if (_camera == null) return;

		// Tween 过渡动画正在进行时，由 Tween 驱动位置，跳过 Lerp 跟随
		if (_transitionTween != null && _transitionTween.IsValid() && _transitionTween.IsRunning())
			return;

		Vector3 targetPos;
		switch (_lockMode)
		{
			case LockMode.Unit:
				if (_lockUnit == null || !IsInstanceValid(_lockUnit)) { Unlock(); return; }
				targetPos = _lockUnit.Position;
				break;
			case LockMode.WorldPos:
				targetPos = _lockWorldPos;
				break;
			default:
				return;
		}

		// 设置指定缩放（如果有设置且大于0）
		if (_lockOrthoSize > 0)
		{
			// 速度自适应缩放：差值越大插值越快，差值趋近0时平滑减速
			float sizeDelta = Mathf.Abs(_camera.Size - _lockOrthoSize);
			float sizeSpeed = Mathf.Clamp(sizeDelta / (_lockOrthoSize * 0.5f), 0.03f, 0.15f);
			_camera.Size = Mathf.Lerp(_camera.Size, _lockOrthoSize, sizeSpeed);
		}
		// 当_lockOrthoSize为0时，保持当前缩放不变

		// 速度自适应跟随：距离远时快速趋近，距离近时平滑减速，消除硬切感
		var camH = _camera.Position.Y;
		var desiredPos = new Vector3(targetPos.X, camH, targetPos.Z + camH * 0.7f);
		float distance = _camera.Position.DistanceTo(desiredPos);

		// 将距离归一化到当前视野比例（距离 / 视野 = 相对偏移量）
		float viewScale = _camera.Size * 0.5f;
		float normalizedDist = viewScale > 0 ? distance / viewScale : distance;

		// 小距离 (<0.2 视野) 用慢速平滑跟随，大距离 (>1 视野) 用快速趋近
		float followSpeed = Mathf.Clamp(normalizedDist * 0.12f, 0.04f, 0.18f);
		_camera.Position = _camera.Position.Lerp(desiredPos, followSpeed);
		ClampPosition();
	}

	/// <summary>创建并配置上帝视角正交相机</summary>
	private void InitializeCamera()
	{
		_camera = new Camera3D
		{
			Projection = Camera3D.ProjectionType.Orthogonal,
			Size = 700.0f,
			RotationDegrees = new Vector3(TiltDegrees, 0, 0),
			Position = new Vector3(600, 800, 1000),
			Current = true,
		};
		AddChild(_camera);
	}

	/// <summary>回收资源释放</summary>
	public override void _ExitTree()
	{
		if (_camera != null && IsInstanceValid(_camera))
			_camera.QueueFree();
		_camera = null;
	}

	// ===== 配置 API =====

	/// <summary>
	/// 根据给定战场边界（像素范围）计算相机边界和缩限上限，
	/// 并将相机居中到战场中心。
	/// </summary>
	public void ConfigureFromWorldBounds(float xMin, float xMax, float zMin, float zMax)
	{
		var battlefieldBounds = new Aabb(
			new Vector3(xMin, 0, zMin),
			new Vector3(xMax - xMin, 1, zMax - zMin));

		BattlefieldBounds = battlefieldBounds;

		float panPad = CameraPanPaddingHexes * BladeHex.Map.HexUtils.Size;
		CameraPanBounds = new Aabb(
			new Vector3(xMin - panPad, 0, zMin - panPad),
			new Vector3((xMax - xMin) + panPad * 2f, 1, (zMax - zMin) + panPad * 2f));

		RecalcMaxOrthoSize();

		// 进入战斗时默认显示全图：将相机缩放到最大（_maxOrthoSize），让玩家看见整个战场和部署区域
		if (_camera != null)
			_camera.Size = _maxOrthoSize;

		CenterCameraOnBattlefield();
		ClampPosition();

		// UI 布局完成后再次居中校正（仍保持全图缩放）
		CallDeferred(nameof(DeferredUiAwareCentering));
	}

	/// <summary>UI 布局完成后再次校正中心位置和缩限上限</summary>
	private void DeferredUiAwareCentering()
	{
		RecalcMaxOrthoSize();
		if (_camera != null)
		{
			// 进入战斗阶段：保持全图视野（设为 _maxOrthoSize）
			_camera.Size = _maxOrthoSize;
		}
		CenterCameraOnBattlefield();
		ClampPosition();
	}

	/// <summary>居中相机到战场中心</summary>
	public void CenterCameraOnBattlefield()
	{
		if (_camera == null || !BattlefieldBounds.HasValue) return;
		var bounds = BattlefieldBounds.Value;
		var center = bounds.Position + bounds.Size * 0.5f;
		_camera.Position = new Vector3(center.X, _camera.Position.Y, center.Z + _camera.Position.Y);
	}

	// ===== 缩放 =====

	/// <summary>放大（滚轮上）</summary>
	public void ZoomIn(float factor = 0.9f)
	{
		if (_camera == null) return;
		_camera.Size = Mathf.Clamp(_camera.Size * factor, MinOrthoSize, _maxOrthoSize);
	}

	/// <summary>缩小（滚轮下）</summary>
	public void ZoomOut(float factor = 1.1f)
	{
		if (_camera == null) return;
		_camera.Size = Mathf.Clamp(_camera.Size * factor, MinOrthoSize, _maxOrthoSize);

		// 缩小到最大时，自动居中相机到战场中心
		if (_camera.Size >= _maxOrthoSize)
		{
			CenterCameraOnBattlefield();
		}
	}

	// ===== 位置限制 =====

	/// <summary>限制相机位置在可平移边界内</summary>
	public void ClampPosition()
	{
		if (_camera == null || !CameraPanBounds.HasValue) return;
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();
		_camera.Position = CameraBoundsClamp.Clamp3DOrtho(
			_camera.Position, _camera.Size, TiltDegrees,
			CameraPanBounds.Value, aspect, topRatio, bottomRatio);
		UpdateMinimapViewport();
	}

	/// <summary>重新计算最大正交尺寸（视口变化时调用）</summary>
	public void RecalcMaxOrthoSize()
	{
		if (!BattlefieldBounds.HasValue) return;
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();
		_maxOrthoSize = CameraBoundsClamp.MaxOrthoSizeToFit(
			BattlefieldBounds.Value, TiltDegrees, aspect, topRatio, bottomRatio);
	}

	/// <summary>估算 UI 占视口高度的比例</summary>
	private (float top, float bottom) GetUiInsetRatios()
	{
		var viewport = GetViewport().GetVisibleRect().Size;
		float vh = Mathf.Max(1f, viewport.Y);
		float topPx = 0f, bottomPx = 0f;
		if (CombatUI != null && CombatUI.IsInsideTree())
		{
			if (CombatUI.TurnOrderBarControl is { } turnBar && turnBar.Size.Y > 0)
				bottomPx += turnBar.Size.Y;
			if (CombatUI.BottomPanel is { } bot && bot.Size.Y > 0)
				bottomPx += bot.Size.Y + 16f;
		}
		if (bottomPx <= 0f) bottomPx = 250f;
		return (Mathf.Clamp(topPx / vh, 0f, 0.45f), Mathf.Clamp(bottomPx / vh, 0f, 0.45f));
	}

	// ===== 镜头聚焦 =====

	/// <summary>平滑聚焦相机到指定世界位置</summary>
	public async Task FocusOn(Vector3 targetWorldPos, float duration = 0.4f)
	{
		if (_camera == null) return;
		var currentPos = _camera.Position;
		var targetCamPos = new Vector3(targetWorldPos.X, currentPos.Y, targetWorldPos.Z + currentPos.Y * 0.7f);

		// 限制目标位置
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();
		targetCamPos = CameraBoundsClamp.Clamp3DOrtho(
			targetCamPos, _camera.Size, TiltDegrees,
			CameraPanBounds ?? new Aabb(), aspect, topRatio, bottomRatio);

		if ((targetCamPos - currentPos).Length() < 100f) return;

		// 取消正在进行的过渡动画
		CancelTransitionTween();

		_transitionTween = CreateTween();
		_transitionTween.TweenProperty(_camera, "position", targetCamPos, duration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		await ToSignal(_transitionTween, Tween.SignalName.Finished);
	}

	/// <summary>聚焦到指定单位</summary>
	public void FocusOnUnit(Unit unit)
	{
		if (unit == null || !IsInstanceValid(unit)) return;
		_ = FocusOn(unit.Position);
	}

	// ===== 双目标框定（攻击时同时显示攻击者和目标） =====

	/// <summary>
	/// 计算能同时容纳两个世界坐标的最小正交缩放值。
	/// 返回值已包含安全边距，确保两个目标不会贴边。
	/// </summary>
	public float CalcOrthoSizeToFrame(Vector3 posA, Vector3 posB, float paddingFactor = 1.4f)
	{
		if (_camera == null) return 0f;
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();

		// 可用视口比例（去掉 UI 占用）
		float usableVertical = 1f - topRatio - bottomRatio;
		if (usableVertical < 0.3f) usableVertical = 0.3f;

		// 正交相机下，Size = 可见世界高度的一半
		// 需要的世界高度 = 两点在相机平面上的投影距离
		float camH = _camera.Position.Y;
		var camA = new Vector3(posA.X, camH, posA.Z + camH * 0.7f);
		var camB = new Vector3(posB.X, camH, posB.Z + camH * 0.7f);

		float dx = Mathf.Abs(camA.X - camB.X);
		float dz = Mathf.Abs(camA.Z - camB.Z);

		// 根据宽高比计算需要的 ortho size
		float neededByWidth = dx / aspect;
		float neededByHeight = dz / usableVertical;
		float needed = Mathf.Max(neededByWidth, neededByHeight) * paddingFactor;

		// 限制在合理范围内
		return Mathf.Clamp(needed, MinOrthoSize, _maxOrthoSize);
	}

	/// <summary>
	/// 平滑框定两个世界坐标（攻击者+目标），相机移动到中点并调整缩放。
	/// 如果两者已在当前视野内则不做任何操作。
	/// </summary>
	public async Task FrameTwoTargets(Vector3 posA, Vector3 posB, float duration = 0.35f)
	{
		if (_camera == null) return;

		// 先检查两个目标是否都已在当前视野内
		if (IsWorldPosVisible(posA) && IsWorldPosVisible(posB)) return;

		// 取消正在进行的过渡动画，防止 Tween 冲突
		CancelTransitionTween();

		// 计算中点
		var midWorld = (posA + posB) * 0.5f;
		float camH = _camera.Position.Y;
		var targetCamPos = new Vector3(midWorld.X, camH, midWorld.Z + camH * 0.7f);

		// 计算需要的缩放
		float neededOrtho = CalcOrthoSizeToFrame(posA, posB);
		float targetOrtho = Mathf.Max(neededOrtho, _camera.Size); // 只放大不缩小，避免频繁抖动

		// 限制目标位置
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();
		targetCamPos = CameraBoundsClamp.Clamp3DOrtho(
			targetCamPos, targetOrtho, TiltDegrees,
			CameraPanBounds ?? new Aabb(), aspect, topRatio, bottomRatio);

		// 根据实际位移量自适应调整持续时间：小位移用更短的时间，避免过度动画
		float actualDist = _camera.Position.DistanceTo(targetCamPos);
		float viewScale = _camera.Size * 0.5f;
		float distRatio = viewScale > 0 ? actualDist / viewScale : 1f;
		float adjustedDuration = Mathf.Clamp(duration * Mathf.Min(distRatio, 1f), 0.08f, duration);

		_transitionTween = CreateTween();
		_transitionTween.SetParallel(true);
		// Sine InOut：起止更柔和，适合中短距离平移
		_transitionTween.TweenProperty(_camera, "position", targetCamPos, adjustedDuration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_transitionTween.TweenProperty(_camera, "size", targetOrtho, adjustedDuration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		await ToSignal(_transitionTween, Tween.SignalName.Finished);
	}

	/// <summary>
	/// 平滑过渡相机到目标位置和缩放。
	/// 自动取消之前的过渡动画，防止 Tween 冲突。
	/// </summary>
	public async Task SmoothTransitionTo(Vector3 targetPos, float targetOrthoSize, float duration = 0.35f)
	{
		if (_camera == null) return;

		CancelTransitionTween();

		// 自适应持续时间
		float actualDist = _camera.Position.DistanceTo(targetPos);
		float viewScale = _camera.Size * 0.5f;
		float distRatio = viewScale > 0 ? actualDist / viewScale : 1f;
		float adjustedDuration = Mathf.Clamp(duration * Mathf.Min(distRatio, 1f), 0.08f, duration);

		_transitionTween = CreateTween();
		_transitionTween.SetParallel(true);
		_transitionTween.TweenProperty(_camera, "position", targetPos, adjustedDuration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_transitionTween.TweenProperty(_camera, "size", targetOrthoSize, adjustedDuration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		await ToSignal(_transitionTween, Tween.SignalName.Finished);
	}

	/// <summary>取消正在进行的过渡 Tween</summary>
	private void CancelTransitionTween()
	{
		if (_transitionTween != null && _transitionTween.IsValid())
			_transitionTween.Kill();
		_transitionTween = null;
	}

	/// <summary>检查一个世界坐标是否在当前相机视野内（含 UI 安全边距）</summary>
	public bool IsWorldPosVisible(Vector3 worldPos, float marginRatio = 0.15f)
	{
		if (_camera == null) return true;
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();

		float camH = _camera.Position.Y;
		// 将世界坐标转换为相机平面坐标
		float targetCamZ = worldPos.Z + camH * 0.7f;
		float dx = Mathf.Abs(worldPos.X - _camera.Position.X);
		float dz = Mathf.Abs(targetCamZ - _camera.Position.Z);

		float halfWidth = _camera.Size * aspect * 0.5f;
		float halfHeight = _camera.Size * 0.5f;

		// 考虑 UI 遮挡和安全边距
		float usableVertical = 1f - topRatio - bottomRatio;
		float safeHalfHeight = halfHeight * usableVertical * (1f - marginRatio);
		float safeHalfWidth = halfWidth * (1f - marginRatio);

		return dx < safeHalfWidth && dz < safeHalfHeight;
	}

	// ===== WASD 移动 =====

	/// <summary>在 _Process 中调用，处理 WASD 移动</summary>
	public void ProcessWASD(double delta)
	{
		if (_camera == null) return;
		// 当缩放到最大时，锁定 WASD 移动
		if (_camera.Size >= _maxOrthoSize) return;

		float spd = 800 * (float)delta * (_camera.Size / 1000);
		var v = Vector3.Zero;
		if (Input.IsKeyPressed(Key.W)) v.Z -= 1;
		if (Input.IsKeyPressed(Key.S)) v.Z += 1;
		if (Input.IsKeyPressed(Key.A)) v.X -= 1;
		if (Input.IsKeyPressed(Key.D)) v.X += 1;
		if (v.Length() > 0)
		{
			_camera.Position += v.Normalized() * spd;
			ClampPosition();
		}
	}

	/// <summary>处理滚轮缩放（在 _UnhandledInput 中调用）</summary>
	public void ProcessWheelZoom(InputEventMouseButton mb)
	{
		if (mb.ButtonIndex == MouseButton.WheelUp)
		{
			ZoomIn();
		}
		else if (mb.ButtonIndex == MouseButton.WheelDown)
		{
			ZoomOut();
		}
		ClampPosition();
	}

	// ===== 小地图同步 =====

	private void UpdateMinimapViewport()
	{
		if (MinimapPanel == null || _camera == null) return;
		float xSpacing = BladeHex.Map.HexUtils.HorizontalSpacing;
		float zSpacing = BladeHex.Map.HexUtils.VerticalSpacing;
		if (xSpacing <= 0 || zSpacing <= 0) return;

		float camX = _camera.Position.X / xSpacing;
		float camZ = (_camera.Position.Z - _camera.Position.Y) / zSpacing;
		float halfViewW = _camera.Size * 0.5f / xSpacing;
		float halfViewH = _camera.Size * 0.35f / zSpacing;
		MinimapPanel.UpdateViewport(new Vector2(camX, camZ), new Vector2(halfViewW, halfViewH));
	}

	// ===== 兼容层方法（供 CombatSceneBase 调用） =====

	/// <summary>兼容层：聚焦相机到指定世界位置（异步）</summary>
	public async Task FocusCameraOn(Vector3 targetWorldPos, float duration = 0.4f)
	{
		await FocusOn(targetWorldPos, duration);
	}

	/// <summary>兼容层：聚焦相机到指定单位</summary>
	public void CenterCameraOnUnit(Unit unit)
	{
		if (unit == null || _camera == null) return;
		var targetPos = unit.Position;
		_camera.Position = new Vector3(targetPos.X, _camera.Position.Y, targetPos.Z + _camera.Position.Y);
		ClampPosition();
	}

	/// <summary>兼容层：移动相机（相对移动）</summary>
	public void MoveCamera(Vector3 delta)
	{
		if (_camera == null) return;
		_camera.Position += delta;
		ClampPosition();
	}

	/// <summary>兼容层：限制相机位置（别名）</summary>
	public void ClampCameraPosition()
	{
		ClampPosition();
	}
}
