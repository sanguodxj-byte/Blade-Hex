// OverworldScene2D.Audio.cs
// 音频系统 — 从 OverworldScene3D.Audio.cs 迁移
// BGM 交叉淡入淡出、环境音效、场景播放列表
using Godot;
using BladeHex.Map;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // 音频字段
    // ========================================

    private BladeHex.Audio.AudioManager? _audioManager;
    private BladeHex.Audio.EnvironmentAudioComponent? _envAudio;

    private float _biomeCheckTimer = 0f;

    // ========================================
    // 初始化
    // ========================================

    private void InitAudio()
    {
        _audioManager = BladeHex.Data.Globals.AudioOrNull;
        if (_audioManager != null)
        {
            _envAudio = new BladeHex.Audio.EnvironmentAudioComponent { Name = "EnvironmentAudio" };
            AddChild(_envAudio);
            _envAudio.SetScenario((int)BladeHex.Audio.AudioManager.Scenario.Overworld, 0.0f);
        }
    }

    // ========================================
    // 每秒更新
    // ========================================

    private void UpdateAudio(float dt)
    {
        if (_envAudio == null) return;

        _biomeCheckTimer += dt;
        if (_biomeCheckTimer < 1.0f) return;
        _biomeCheckTimer = 0f;

        // 获取玩家所在地形
        HexOverworldTile? tile = _mapAccess.GetActiveTileAtPixel(_playerPixelPos);

        if (tile == null) return;

        var biome = tile.Terrain switch
        {
            HexOverworldTile.TerrainType.Forest or HexOverworldTile.TerrainType.DenseForest
                or HexOverworldTile.TerrainType.Jungle or HexOverworldTile.TerrainType.Taiga
                => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Forest,
            HexOverworldTile.TerrainType.Hills or HexOverworldTile.TerrainType.Mountain
                or HexOverworldTile.TerrainType.MountainSnow or HexOverworldTile.TerrainType.Rocky
                => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Mountain,
            HexOverworldTile.TerrainType.Swamp or HexOverworldTile.TerrainType.Bog
                => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Swamp,
            HexOverworldTile.TerrainType.Sand or HexOverworldTile.TerrainType.Wasteland
                or HexOverworldTile.TerrainType.Savanna
                => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Desert,
            HexOverworldTile.TerrainType.Snow or HexOverworldTile.TerrainType.Ice
                => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Snowland,
            _ => BladeHex.Audio.EnvironmentAudioComponent.BiomeType.Plains,
        };

        _envAudio.SetBiome(biome);

        // 昼夜音频
        if (EconomyMgr != null)
        {
            float hour = EconomyMgr.CurrentHour;
            var timeOfDay = hour >= 6 && hour < 19
                ? BladeHex.Audio.EnvironmentAudioComponent.TimeOfDay.Day
                : BladeHex.Audio.EnvironmentAudioComponent.TimeOfDay.Night;
            _envAudio.SetTimeOfDay(timeOfDay);
        }
    }
}
