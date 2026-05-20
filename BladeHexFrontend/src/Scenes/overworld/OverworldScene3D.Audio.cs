// OverworldScene3D.Audio.cs
// 大地图音频：环境音（地形/昼夜/天气）— 维持原 Misc 实现，不走 OverworldAudioController 包装
using Godot;
using BladeHex.Map;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
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
