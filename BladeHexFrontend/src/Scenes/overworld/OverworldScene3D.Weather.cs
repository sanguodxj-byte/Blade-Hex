// OverworldScene3D.Weather.cs
// 天气系统 — WeatherManager + 粒子特效 + 视觉色调 + 游戏性影响
using Godot;
using BladeHex.Map;
using BladeHex.View.Environment;
using BladeHex.View.Map;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
	// ========================================
	// 天气系统字段
	// ========================================

	private WeatherManager? _weatherMgr;
	private WeatherParticles2D? _weatherParticles2D;
	private WeatherParticles3D? _weatherParticles3D;

	/// <summary>沙尘暴屏幕色调覆盖</summary>
	private ColorRect? _sandstormTintRect;
	private CanvasLayer? _sandstormTintLayer;
	private float _sandstormTintAlpha;

	// ========================================
	// 天气游戏性修正值（每帧缓存，供其他系统读取）
	// ========================================

	/// <summary>天气移速修正因子 (0.5~1.0)</summary>
	public float WeatherSpeedFactor { get; private set; } = 1.0f;

	/// <summary>天气视野修正因子 (0.6~1.0)</summary>
	public float WeatherVisionFactor { get; private set; } = 1.0f;

	/// <summary>天气遭遇率修正因子 (0.7~1.5)</summary>
	public float WeatherEncounterFactor { get; private set; } = 1.0f;

	// ========================================
	// 初始化
	// ========================================

	/// <summary>场景销毁时解绑 Autoload 的信号订阅。</summary>
	public override void _ExitTree()
	{
		if (_weatherMgr != null)
		{
			_weatherMgr.WeatherChanged -= OnWeatherChanged;
			_weatherMgr = null;
		}
	}

	/// <summary>初始化天气视觉子系统（粒子 + 沙尘暴色调）。
	/// WeatherManager 现在是 Autoload，订阅其 WeatherChanged 信号；
	/// 不再 new + AddChild。
	/// 使用 WeatherOrNull 容错：autoload 缺失时跳过整段，不阻塞 _Ready 后续步骤（UI / Toast / 调试控制台）。</summary>
	private void InitWeatherSystem()
	{
		// 天气状态机（Autoload）— 容错获取，缺失时跳过整个天气子系统
		_weatherMgr = Globals.WeatherOrNull;
		if (_weatherMgr == null)
		{
			GD.PrintErr("[OverworldScene3D] WeatherManager Autoload 不存在，跳过天气系统初始化");
			return;
		}
		_weatherMgr.WeatherChanged += OnWeatherChanged;

		// 2D 天气粒子（CanvasLayer 方案，始终在 3D 场景之上）
		_weatherParticles2D = new WeatherParticles2D();
		_weatherParticles2D.Name = "WeatherParticles2D";
		AddChild(_weatherParticles2D);

		// 3D 天气粒子（GPUParticles3D，在世界空间中有深度感）
		_weatherParticles3D = new WeatherParticles3D();
		_weatherParticles3D.Name = "WeatherParticles3D";
		AddChild(_weatherParticles3D);

		// 沙尘暴屏幕色调层
		_sandstormTintLayer = new CanvasLayer { Name = "SandstormTintLayer", Layer = 6 };
		AddChild(_sandstormTintLayer);
		_sandstormTintRect = new ColorRect();
		_sandstormTintRect.Name = "SandstormTint";
		_sandstormTintRect.Color = new Color(0.78f, 0.62f, 0.38f, 0.0f);
		_sandstormTintRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		_sandstormTintRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_sandstormTintLayer.AddChild(_sandstormTintRect);

		// 同步当前天气到 UI 和粒子（场景重建时 Autoload 仍保留前次状态）
		if (_weatherMgr.CurrentWeather != WeatherType.Clear)
			OnWeatherChanged(0, (int)_weatherMgr.CurrentWeather);

		GD.Print($"[OverworldScene3D] 天气系统就绪（Autoload 共享）: 当前 {_weatherMgr.CurrentWeather}");
	}

	// ========================================
	// 每帧更新
	// ========================================

	/// <summary>天气系统每帧更新（在 _Process 中调用）</summary>
	private void UpdateWeather(float dt)
	{
		if (_weatherMgr == null) return;

		// 天气自动循环（移动或等待时 tick）
		bool timeFlowing = (_playerMoving || IsWaiting) && !IsTimePaused;
		if (timeFlowing && EconomyMgr != null)
		{
			float deltaHours = dt * GameTimeScale;
			if (IsWaiting && !_playerMoving) deltaHours *= 8.0f;
			int season = (int)EconomyMgr.GetSeason();
			UpdateWeatherTerrainContext();
			_weatherMgr.TickWeatherCycle(season, deltaHours);
		}

		// 更新视觉效果
		UpdateWeatherVisuals(dt);

		// 更新游戏性修正值
		UpdateWeatherGameplayFactors();
	}

	// ========================================
	// 视觉效果
	// ========================================

	/// <summary>天气变化回调 — 更新 UI + 粒子</summary>
	private void OnWeatherChanged(int oldWeather, int newWeather)
	{
		var weatherType = (WeatherType)newWeather;
		string weatherName = weatherType switch
		{
			WeatherType.Rain => "🌧 雨天",
			WeatherType.Snow => "🌨 雪天",
			WeatherType.Sandstorm => "🌪 沙尘暴",
			_ => "☀ 晴天",
		};

		// 更新 UI 天气显示
		_overworldUi?.UpdateWeatherDisplay(weatherName);

		// 更新粒子系统
		if (_weatherParticles2D != null)
		{
			if (weatherType == WeatherType.Clear)
				_weatherParticles2D.StopAll();
			else
				_weatherParticles2D.SetWeather(weatherType, _weatherMgr!.GetEffectiveIntensity());
		}
		if (_weatherParticles3D != null)
		{
			if (weatherType == WeatherType.Clear)
				_weatherParticles3D.StopAll();
			else
				_weatherParticles3D.SetWeather(weatherType, _weatherMgr!.GetEffectiveIntensity());
		}

		// 更新音频
		if (_envAudio != null)
		{
			var audioWeather = weatherType switch
			{
				WeatherType.Rain => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Rain,
				WeatherType.Snow => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Snow,
				WeatherType.Sandstorm => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Sandstorm,
				_ => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Clear,
			};
			_envAudio.SetWeather(audioWeather);
		}

		// 更新云层（天气联动）
		UpdateCloudLayerForWeather(weatherType);

		GD.Print($"[OverworldScene3D/Weather] 天气变化: {(WeatherType)oldWeather} → {weatherType}");
	}

	/// <summary>更新天气视觉效果（光照色调 + 沙尘暴雾罩 + 粒子强度）</summary>
	private void UpdateWeatherVisuals(float dt)
	{
		if (_weatherMgr == null || _sunLight == null || _worldEnv == null) return;

		var activeWeather = _weatherMgr.GetActiveWeatherType();
		float intensity = _weatherMgr.GetEffectiveIntensity();

		// 光照色调修正（纯函数计算）
		var visual = BladeHex.Scenes.Overworld.Components.WeatherController.CalculateVisualParams(activeWeather, intensity);

		// 应用天气色调到光照（叠加在昼夜循环之上）
		_sunLight.LightEnergy = _baseSunEnergy * visual.EnergyMod;
		_worldEnv.AmbientLightEnergy = _baseAmbientEnergy * visual.AmbientMod;
		_sunLight.LightColor = _baseSunColor * visual.Tint;
		_worldEnv.AmbientLightColor = _baseAmbientColor * visual.Tint * 0.8f;

		// --- 粒子强度跟随过渡进度 ---
		bool particlesEnabled = Globals.StateOrNull?.Get("weather_particles_enabled").AsBool() ?? true;
		if (_weatherParticles2D != null)
		{
			if (!particlesEnabled || activeWeather == WeatherType.Clear || intensity < 0.01f)
				_weatherParticles2D.StopAll();
			else
				_weatherParticles2D.SetWeather(activeWeather, intensity);
		}
		if (_weatherParticles3D != null)
		{
			if (!particlesEnabled || activeWeather == WeatherType.Clear || intensity < 0.01f)
				_weatherParticles3D.StopAll();
			else
			{
				_weatherParticles3D.SetWeather(activeWeather, intensity);
				// 跟随相机/玩家位置
				if (_camera != null)
					_weatherParticles3D.UpdatePosition(_camera.GlobalPosition);
			}
		}

		// --- 沙尘暴屏幕雾罩 ---
		float targetTintAlpha = activeWeather == WeatherType.Sandstorm
			? intensity * 0.2f  // 最大 20% 不透明度
			: 0.0f;
		_sandstormTintAlpha = Mathf.MoveToward(_sandstormTintAlpha, targetTintAlpha, dt * 0.15f);

		if (_sandstormTintRect != null)
		{
			_sandstormTintRect.Color = new Color(0.78f, 0.62f, 0.38f, _sandstormTintAlpha);
			_sandstormTintRect.Visible = _sandstormTintAlpha > 0.005f;
		}
	}

	// ========================================
	// 游戏性影响
	// ========================================

	/// <summary>更新天气对游戏性的影响因子</summary>
	private void UpdateWeatherGameplayFactors()
	{
		if (_weatherMgr == null)
		{
			WeatherSpeedFactor = 1.0f;
			WeatherVisionFactor = 1.0f;
			WeatherEncounterFactor = 1.0f;
			return;
		}

		var factors = BladeHex.Scenes.Overworld.Components.WeatherController.CalculateGameplayFactors(
			_weatherMgr.GetActiveWeatherType(),
			_weatherMgr.GetEffectiveIntensity());
		WeatherSpeedFactor = factors.Speed;
		WeatherVisionFactor = factors.Vision;
		WeatherEncounterFactor = factors.Encounter;
	}

	// ========================================
	// 地形上下文
	// ========================================

	/// <summary>更新天气管理器的地形上下文（雪地/沙漠判定）</summary>
	private void UpdateWeatherTerrainContext()
	{
		if (_weatherMgr == null) return;

		HexOverworldTile? tile = null;
		if (_chunkManager != null)
		{
			var axial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
			tile = _chunkManager.GetTile(axial.X, axial.Y);
		}
		else
		{
			tile = _grid.GetTileAtPixel(_playerPixelPos.X, _playerPixelPos.Y);
		}

		if (tile == null) return;

		var t = tile.Terrain;
		_weatherMgr.IsInSnowTerrain = t == HexOverworldTile.TerrainType.Snow
			|| t == HexOverworldTile.TerrainType.Ice
			|| t == HexOverworldTile.TerrainType.MountainSnow
			|| t == HexOverworldTile.TerrainType.Taiga;

		_weatherMgr.IsInDesertTerrain = t == HexOverworldTile.TerrainType.Sand
			|| t == HexOverworldTile.TerrainType.Wasteland
			|| t == HexOverworldTile.TerrainType.Savanna;
	}

	// ========================================
	// 战斗天气传递
	// ========================================
	// [Autoload 化后无需快照] WriteWeatherToGlobalState 已移除：
	// 战斗场景直接读 Globals.Weather，不再走 GlobalState.Weather context 中转。

	// ========================================
	// 调试 API
	// ========================================

	/// <summary>强制设置天气（调试用）</summary>
	public void DebugSetWeather(WeatherType weather, WeatherIntensity intensity = WeatherIntensity.Moderate)
	{
		_weatherMgr?.SetWeatherImmediate(weather, intensity);
	}

	/// <summary>获取当前天气类型</summary>
	public WeatherType GetCurrentWeather()
	{
		return _weatherMgr?.CurrentWeather ?? WeatherType.Clear;
	}

	/// <summary>获取当前天气强度</summary>
	public float GetCurrentWeatherIntensity()
	{
		return _weatherMgr?.GetEffectiveIntensity() ?? 0.0f;
	}

	/// <summary>天气联动更新云层</summary>
	private void UpdateCloudLayerForWeather(WeatherType weather)
	{
		if (_cloudLayer == null) return;

		var p = BladeHex.Scenes.Overworld.Components.WeatherController.CalculateCloudParams(weather);
		_cloudLayer.SetCoverage(p.Coverage);
		_cloudLayer.SetOpacity(p.Opacity);
		_cloudLayer.SetCloudColor(p.Color);
		_windSystem?.SetWind(p.WindStrength, p.WindAngle);
		_windSystem?.SetGust(p.GustStrength, p.GustPeriod);
	}
}
