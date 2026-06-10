// CombatSunLight.cs
// 战斗场景太阳光组件 — 根据当前时刻模拟太阳位置、角度、色温。
//
// 太阳轨迹:
//   - 6:00 日出:东方地平线(仰角 5°),暖橙色,低能量
//   - 12:00 正午:正南偏高(仰角 60°),白色,最大能量
//   - 18:00 日落:西方地平线(仰角 5°),暖红色,低能量
//   - 夜间:月光(仰角 30°,偏蓝,极低能量)
//
// 用法:挂到战斗场景,Initialize 时传入 DirectionalLight3D + 当前小时。
// 每帧调 Tick() 或一次性调 SetHour() 即可。
using Godot;

namespace BladeHex.View.Combat;

[GlobalClass]
public partial class CombatSunLight : Node
{
    private DirectionalLight3D? _light;
    private DirectionalLight3D? _fillLight;
    private float _currentHour = 12f;

    // 太阳轨迹参数
    private const float SunriseHour = 6f;
    private const float SunsetHour = 18f;
    private const float NoonHour = 12f;

    // 能量范围
    private const float MaxEnergy = 1.1f;    // 正午(上调:拉开亮部与阴影对比,强化侧影纵深)
    private const float MinEnergy = 0.15f;   // 夜间(月光)
    private const float SunriseEnergy = 0.5f;

    // 仰角范围(度)
    private const float MaxElevation = 45f;  // 正午:玩家角度 45° 斜射(出体积 + 影子落在可见远侧)
    private const float HorizonElevation = 5f; // 日出/日落
    private const float NightElevation = 30f;  // 月光

    // 方位角日弧(锚定正午=玩家 45°，全天偏玩家一侧、不穿过 180°=相机视轴，避免正午顶光变平)。
    // 坐标含义:0°=玩家/相机侧(+Z)，90°=画面左，180°=场景远端(-Z)，270°=画面右。
    // 太阳一天内由黎明的玩家右后方移到黄昏的画面左侧，影子方向随之转动;正午恰好落在玩家左后 45°。
    private const float DawnAzimuth = -35f;  // 黎明:玩家右后方
    private const float NoonAzimuth = 45f;   // 正午:玩家左后 45°(= (Dawn+Dusk)/2，锚点)
    private const float DuskAzimuth = 125f;  // 黄昏:画面左
    private const float NightAzimuth = 90f;  // 月光:画面左侧斜照

    // 色温渐变
    private Gradient? _colorGradient;

    public void Initialize(DirectionalLight3D light, float hour, DirectionalLight3D? fillLight = null)
    {
        _light = light;
        _fillLight = fillLight;
        _currentHour = hour;
        BuildColorGradient();
        ApplyLighting();
    }

    /// <summary>
    /// 按战场世界尺寸配置方向光阴影覆盖距离。
    /// Godot 默认 DirectionalShadowMaxDistance 仅 100 世界单位，而本项目世界单位为像素级
    /// (HexUtils.Size≈96)，战场跨度可达数千单位 — 不配置会导致远端阴影整体消失，纵深感丢失。
    /// </summary>
    public void ConfigureShadowDistance(float battlefieldDiagonal)
    {
        if (_light == null) return;
        // 留 1.3 倍余量，覆盖相机平移外延 + 单位身高投影
        _light.DirectionalShadowMaxDistance = Mathf.Max(2000f, battlefieldDiagonal * 1.3f);
        // 单段正交阴影：战场不大且相机俯视固定，单段 shadow map 精度足够覆盖全场。
        // 不用 Parallel4Splits —— 4 段分屏的段交界处会在地表受光上形成"斜向明暗分层带"
        // (跟随相机、放大更明显、多条),被误认为多层阴影滤镜。单段无段边界，地表受光均匀。
        _light.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
    }

    /// <summary>设置时刻并立即更新光照(战斗场景通常不需要每帧更新)</summary>
    public void SetHour(float hour)
    {
        _currentHour = Mathf.Clamp(hour, 0f, 24f);
        ApplyLighting();
    }

    /// <summary>每帧更新(如果战斗中有时间流逝)</summary>
    public void Tick(float hour)
    {
        if (Mathf.Abs(hour - _currentHour) < 0.01f) return;
        _currentHour = hour;
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        if (_light == null || _colorGradient == null) return;

        bool isDaytime = _currentHour >= SunriseHour && _currentHour <= SunsetHour;

        float elevation, azimuth, energy;
        Color color;

        if (isDaytime)
        {
            float dayProgress = (_currentHour - SunriseHour) / (SunsetHour - SunriseHour); // 0~1

            // 仰角:日出 5° → 正午 MaxElevation → 日落 5°(抛物线)，随世界时间变化以保留日夜感
            elevation = HorizonElevation + (MaxElevation - HorizonElevation) * Mathf.Sin(dayProgress * Mathf.Pi);

            // 方位:沿锚定日弧线性移动(黎明→正午→黄昏)，正午恰为玩家 45°；全程偏玩家侧不穿 180°
            azimuth = Mathf.Lerp(DawnAzimuth, DuskAzimuth, dayProgress);

            // 能量:日出低 → 正午高 → 日落低(sin 曲线)
            energy = Mathf.Lerp(SunriseEnergy, MaxEnergy, Mathf.Sin(dayProgress * Mathf.Pi));

            // 色温:从渐变采样
            color = _colorGradient.Sample(dayProgress);
        }
        else
        {
            // 夜间:月光，画面左侧斜照保持长侧影；仅压低能量、转冷蓝
            elevation = NightElevation;
            azimuth = NightAzimuth;
            energy = MinEnergy;
            color = new Color(0.4f, 0.45f, 0.6f); // 冷蓝月光
        }

        // 应用到 DirectionalLight3D
        // Godot 的 DirectionalLight3D 方向由 RotationDegrees 决定:
        // X = 仰角(负值=向下照), Y = 方位角
        _light.RotationDegrees = new Vector3(-elevation, azimuth, 0);
        _light.LightEnergy = energy;
        _light.LightColor = color;

        // 阴影设置:低角度时阴影更长更柔和
        _light.ShadowEnabled = true;
        _light.ShadowBias = elevation < 20f ? 0.05f : 0.02f;

        // 软阴影(底座+半身美术风格,硬边投影会显廉价):
        // LightAngularDistance 给太阳一个角直径 → PCSS 距离软阴影(离投影体越远半影越宽,最自然);
        // ShadowBlur 再叠一层均匀模糊兜底。需 forward_plus(本项目已切)。
        _light.LightAngularDistance = 2.5f;
        _light.ShadowBlur = 1.5f;

        ApplyFillLighting(isDaytime, elevation, azimuth, energy);
    }

    private void ApplyFillLighting(bool isDaytime, float elevation, float azimuth, float keyEnergy)
    {
        if (_fillLight == null) return;

        float fillElevation = Mathf.Clamp(elevation + 10f, 25f, 55f);
        float fillAzimuth = azimuth + 165f;

        _fillLight.RotationDegrees = new Vector3(-fillElevation, fillAzimuth, 0);
        _fillLight.ShadowEnabled = false;
        _fillLight.LightColor = isDaytime
            ? new Color(0.58f, 0.66f, 0.82f)
            : new Color(0.34f, 0.42f, 0.65f);
        _fillLight.LightEnergy = isDaytime
            ? Mathf.Clamp(keyEnergy * 0.12f, 0.08f, 0.16f)
            : 0.08f;
    }

    private void BuildColorGradient()
    {
        // 白天色温渐变:日出暖橙 → 上午暖白 → 正午纯白 → 下午暖白 → 日落暖红
        _colorGradient = new Gradient
        {
            Offsets = new float[] { 0.0f, 0.15f, 0.35f, 0.65f, 0.85f, 1.0f },
            Colors = new Color[]
            {
                new(1.0f, 0.6f, 0.3f),   // 日出:暖橙
                new(1.0f, 0.85f, 0.7f),  // 早晨:暖白
                new(1.0f, 0.98f, 0.95f), // 正午:近纯白
                new(1.0f, 0.98f, 0.95f), // 下午:近纯白
                new(1.0f, 0.75f, 0.5f),  // 傍晚:暖黄
                new(0.95f, 0.5f, 0.3f),  // 日落:暖红
            },
        };
    }
}
