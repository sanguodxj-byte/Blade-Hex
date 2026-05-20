// OverworldLightSystem.cs
// 大地图光圈系统 — 为玩家/实体/POI 添加地面光圈（OmniLight3D）
// 夜间亮起，白天淡出。城市光圈大而亮，队伍光圈小而暖。
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 大地图光圈系统。
/// 管理所有地面点光源（OmniLight3D），根据昼夜强度自动调节。
/// 
/// 设计：
/// - 玩家：暖黄色小光圈（营火感）
/// - 城镇/城堡：大范围暖白光（城市灯火）
/// - 村庄/旅店：中等暖光
/// - 敌对实体：暗红微光（篝火/火把）
/// - 友好实体：暖黄微光
/// - 白天光圈强度降为 0（不可见），夜间渐亮
/// </summary>
public partial class OverworldLightSystem : Node3D
{
    // ========================================
    // 配置
    // ========================================

    private const float PixelToWorld = 1.0f / 156.0f;

    /// <summary>光源离地面的高度</summary>
    private const float LightHeight = 0.3f;

    /// <summary>统一光源颜色（暖黄火光）</summary>
    private static readonly Color LightColor = new(1.0f, 0.88f, 0.6f);

    /// <summary>白天时光圈强度（几乎不可见）</summary>
    private const float DayIntensity = 0.03f;

    /// <summary>夜间时光圈强度倍率</summary>
    private const float NightIntensityMult = 1.0f;

    /// <summary>当前全局昼夜强度因子（0=白天, 1=深夜）</summary>
    private float _nightFactor = 0.0f;

    /// <summary>玩家视野范围（像素），超出此范围的光源不可见</summary>
    private float _playerVisionPx = 4000.0f;

    /// <summary>玩家当前像素位置（每帧更新）</summary>
    private Vector2 _playerPos;

    // ========================================
    // 光源数据
    // ========================================

    private struct LightConfig
    {
        public float Range;
        public float BaseEnergy;
    }

    // POI 类型 → 光源配置（只有范围和强度不同，颜色统一）
    // 已废弃：现在使用 POIScaleTable.Get(scale) 派生 LightRange / LightEnergy
    // 保留映射供 PlayerLight / EntityLight 使用
    private static LightConfig PoiLightFromScale(BladeHex.Strategic.POIScale scale)
    {
        var profile = BladeHex.Strategic.POIScaleTable.Get(scale);
        return new LightConfig { Range = profile.LightRange, BaseEnergy = profile.LightEnergy };
    }

    private static readonly LightConfig PlayerLight = new() { Range = 2.0f, BaseEnergy = 0.8f };
    private static readonly LightConfig HostileEntityLight = new() { Range = 1.2f, BaseEnergy = 0.4f };
    private static readonly LightConfig FriendlyEntityLight = new() { Range = 1.2f, BaseEnergy = 0.4f };

    // ========================================
    // 管理的光源
    // ========================================

    private OmniLight3D? _playerLight;
    private readonly Dictionary<OverworldPOI, OmniLight3D> _poiLights = new();
    private readonly Dictionary<OverworldEntity, OmniLight3D> _entityLights = new();

    // ========================================
    // 初始化
    // ========================================

    public void Initialize()
    {
        Name = "OverworldLightSystem";
    }

    // ========================================
    // 玩家光源
    // ========================================

    /// <summary>创建玩家光源（初始化时调用一次）</summary>
    public void CreatePlayerLight(Vector2 playerPixelPos)
    {
        _playerLight = CreateLight(PlayerLight);
        UpdatePlayerLightPosition(playerPixelPos);
        AddChild(_playerLight);
    }

    /// <summary>每帧更新玩家光源位置</summary>
    public void UpdatePlayerLightPosition(Vector2 playerPixelPos)
    {
        _playerPos = playerPixelPos;
        if (_playerLight == null) return;
        var worldPos = new Vector3(playerPixelPos.X * PixelToWorld, LightHeight, playerPixelPos.Y * PixelToWorld);
        _playerLight.Position = worldPos;
    }

    // ========================================
    // POI 光源
    // ========================================

    /// <summary>为所有 POI 创建光源</summary>
    public void CreatePOILights(List<OverworldPOI> pois)
    {
        foreach (var poi in pois)
        {
            // Settlement 和 Lair 不发光（敌对/荒废）
            if (poi.PoiTypeEnum == OverworldPOI.POIType.Settlement) continue;
            if (poi.PoiTypeEnum == OverworldPOI.POIType.Lair) continue;

            var config = PoiLightFromScale(poi.Scale);

            var light = CreateLight(config);
            var worldPos = new Vector3(poi.Position.X * PixelToWorld, LightHeight, poi.Position.Y * PixelToWorld);
            light.Position = worldPos;
            AddChild(light);
            _poiLights[poi] = light;
        }
    }

    // ========================================
    // 实体光源
    // ========================================

    /// <summary>为可见实体添加光源</summary>
    public void AddEntityLight(OverworldEntity entity)
    {
        if (_entityLights.ContainsKey(entity)) return;

        var config = entity.IsHostileToPlayer ? HostileEntityLight : FriendlyEntityLight;
        var light = CreateLight(config);
        var worldPos = new Vector3(entity.Position.X * PixelToWorld, LightHeight, entity.Position.Y * PixelToWorld);
        light.Position = worldPos;
        AddChild(light);
        _entityLights[entity] = light;
    }

    /// <summary>更新实体光源位置</summary>
    public void UpdateEntityLightPosition(OverworldEntity entity)
    {
        if (!_entityLights.TryGetValue(entity, out var light)) return;
        light.Position = new Vector3(entity.Position.X * PixelToWorld, LightHeight, entity.Position.Y * PixelToWorld);
    }

    /// <summary>移除实体光源</summary>
    public void RemoveEntityLight(OverworldEntity entity)
    {
        if (!_entityLights.TryGetValue(entity, out var light)) return;
        light.QueueFree();
        _entityLights.Remove(entity);
    }

    // ========================================
    // 昼夜更新
    // ========================================

    /// <summary>
    /// 每帧调用 — 根据当前时间和玩家视野更新所有光源。
    /// </summary>
    /// <param name="currentHour">当前小时（0~24）</param>
    /// <param name="visionRangePx">玩家视野范围（像素），可选覆盖</param>
    public void UpdateDayNight(float currentHour, float visionRangePx = 0)
    {
        if (visionRangePx > 0) _playerVisionPx = visionRangePx;

        // 计算夜间因子：6:00~18:00 为白天（0），20:00~5:00 为深夜（1），过渡 1 小时
        float targetNight;
        if (currentHour >= 7.0f && currentHour <= 18.0f)
            targetNight = 0.0f;
        else if (currentHour >= 20.0f || currentHour <= 5.0f)
            targetNight = 1.0f;
        else if (currentHour > 18.0f)
            targetNight = (currentHour - 18.0f) / 2.0f;
        else
            targetNight = 1.0f - (currentHour - 5.0f) / 2.0f;

        _nightFactor = targetNight;
        float nightMult = Mathf.Lerp(DayIntensity, NightIntensityMult, _nightFactor);

        // 玩家光源始终可见
        if (_playerLight != null)
            _playerLight.LightEnergy = PlayerLight.BaseEnergy * nightMult;

        // POI 光源：根据距离玩家的距离决定可见度
        foreach (var kvp in _poiLights)
        {
            if (!GodotObject.IsInstanceValid(kvp.Value)) continue;
            var config = PoiLightFromScale(kvp.Key.Scale);
            float dist = _playerPos.DistanceTo(kvp.Key.Position);
            float visibility = GetVisibilityByDistance(dist);
            kvp.Value.LightEnergy = config.BaseEnergy * nightMult * visibility;
            kvp.Value.Visible = visibility > 0.01f;
        }

        // 实体光源：根据距离玩家的距离决定可见度
        foreach (var kvp in _entityLights)
        {
            if (!GodotObject.IsInstanceValid(kvp.Value)) continue;
            var config = kvp.Key.IsHostileToPlayer ? HostileEntityLight : FriendlyEntityLight;
            float dist = _playerPos.DistanceTo(kvp.Key.Position);
            float visibility = GetVisibilityByDistance(dist);
            kvp.Value.LightEnergy = config.BaseEnergy * nightMult * visibility;
            kvp.Value.Visible = visibility > 0.01f;
        }
    }

    /// <summary>根据距离计算可见度（0~1），使用平方衰减模拟自然光照</summary>
    private float GetVisibilityByDistance(float distPx)
    {
        if (distPx <= _playerVisionPx * 0.6f) return 1.0f; // 视野 60% 内：满亮度
        if (distPx >= _playerVisionPx * 1.3f) return 0.0f; // 视野 130% 外：不可见
        // 60%~130% 之间：平方衰减（接近自然光照的反平方律感知）
        float t = (distPx - _playerVisionPx * 0.6f) / (_playerVisionPx * 0.7f);
        float fade = 1.0f - t;
        return fade * fade; // 平方曲线：近处衰减慢，远处衰减快
    }

    // ========================================
    // 清理
    // ========================================

    public void ClearAll()
    {
        if (_playerLight != null) { _playerLight.QueueFree(); _playerLight = null; }
        foreach (var kvp in _poiLights) kvp.Value.QueueFree();
        _poiLights.Clear();
        foreach (var kvp in _entityLights) kvp.Value.QueueFree();
        _entityLights.Clear();
    }

    // ========================================
    // 内部
    // ========================================

    private static OmniLight3D CreateLight(LightConfig config)
    {
        var light = new OmniLight3D();
        light.LightColor = LightColor;
        light.LightEnergy = config.BaseEnergy;
        light.OmniRange = config.Range;
        light.OmniAttenuation = 1.5f; // 柔和衰减
        light.ShadowEnabled = false;
        light.LightCullMask = 1;
        return light;
    }
}
