// OverworldAudioController.cs
// 大地图音频环境控制器 — 管理 EnvironmentAudioComponent 的生命周期和场景切换。
//
// 职责：
//   - 创建 EnvironmentAudioComponent，挂到 Overworld scenario
//   - 每秒检查玩家所在地形 biome / 时间段，更新 envAudio
//   - 暴露 SetWeather 给 WeatherController 在天气变化时调用
//
// 服务于架构优化 spec R5 — Sprint 6 场景控制器组件化。
using Godot;
using BladeHex.Audio;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Scenes.Overworld.Components;

[GlobalClass]
public partial class OverworldAudioController : Node
{
    // ========================================
    // 引用
    // ========================================

    private EnvironmentAudioComponent? _envAudio;
    private HexOverworldGrid? _grid;
    private ChunkManager? _chunkManager;
    private EconomyManager? _economy;

    // ========================================
    // 状态
    // ========================================

    private float _biomeCheckTimer;

    // ========================================
    // 入口
    // ========================================

    /// <summary>由 OverworldScene3D 在 _Ready 调用，创建 EnvironmentAudioComponent</summary>
    public void Initialize(HexOverworldGrid grid, ChunkManager? chunkManager, EconomyManager economy)
    {
        _grid = grid;
        _chunkManager = chunkManager;
        _economy = economy;

        var audioMgr = Globals.AudioOrNull;
        if (audioMgr == null) return;

        _envAudio = new EnvironmentAudioComponent { Name = "EnvironmentAudio" };
        AddChild(_envAudio);
        _envAudio.SetScenario((int)AudioManager.Scenario.Overworld, 0.0f);
    }

    /// <summary>由 OverworldScene3D._Process 调用</summary>
    public void Tick(float dt, Vector2 playerPixelPos)
    {
        if (_envAudio == null) return;

        _biomeCheckTimer += dt;
        if (_biomeCheckTimer < 1.0f) return;
        _biomeCheckTimer = 0f;

        // 获取玩家所在地形
        var tile = GetTileAt(playerPixelPos);
        if (tile == null) return;

        var biome = tile.Terrain switch
        {
            HexOverworldTile.TerrainType.Forest or HexOverworldTile.TerrainType.DenseForest
                or HexOverworldTile.TerrainType.Jungle or HexOverworldTile.TerrainType.Taiga
                => EnvironmentAudioComponent.BiomeType.Forest,
            HexOverworldTile.TerrainType.Hills or HexOverworldTile.TerrainType.Mountain
                or HexOverworldTile.TerrainType.MountainSnow or HexOverworldTile.TerrainType.Rocky
                => EnvironmentAudioComponent.BiomeType.Mountain,
            HexOverworldTile.TerrainType.Swamp or HexOverworldTile.TerrainType.Bog
                => EnvironmentAudioComponent.BiomeType.Swamp,
            HexOverworldTile.TerrainType.Sand or HexOverworldTile.TerrainType.Wasteland
                or HexOverworldTile.TerrainType.Savanna
                => EnvironmentAudioComponent.BiomeType.Desert,
            HexOverworldTile.TerrainType.Snow or HexOverworldTile.TerrainType.Ice
                => EnvironmentAudioComponent.BiomeType.Snowland,
            _ => EnvironmentAudioComponent.BiomeType.Plains,
        };

        _envAudio.SetBiome(biome);

        // 昼夜音频
        if (_economy != null)
        {
            float hour = _economy.CurrentHour;
            var timeOfDay = hour >= 6 && hour < 19
                ? EnvironmentAudioComponent.TimeOfDay.Day
                : EnvironmentAudioComponent.TimeOfDay.Night;
            _envAudio.SetTimeOfDay(timeOfDay);
        }
    }

    /// <summary>由 WeatherController 在天气变化时调用</summary>
    public void SetWeather(EnvironmentAudioComponent.WeatherType weather)
    {
        _envAudio?.SetWeather(weather);
    }

    // ========================================
    // 内部
    // ========================================

    private HexOverworldTile? GetTileAt(Vector2 pixelPos)
    {
        if (_chunkManager != null)
        {
            var axial = HexOverworldTile.PixelToAxial(pixelPos.X, pixelPos.Y);
            return _chunkManager.GetTile(axial.X, axial.Y);
        }
        return _grid?.GetTileAtPixel(pixelPos.X, pixelPos.Y);
    }
}
