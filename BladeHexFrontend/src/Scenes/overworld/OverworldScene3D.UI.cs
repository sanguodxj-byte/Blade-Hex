// OverworldScene3D.UI.cs
// 把场景状态推送到 OverworldUI：信息栏 + 速度状态
using Godot;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    private void UpdateUIInfo()
    {
        if (_overworldUi == null || PlayerUnitData == null || EconomyMgr == null) return;

        _overworldUi.UpdateInfo(PlayerUnitData, EconomyMgr);

        // 更新地形显示
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

        if (tile != null)
        {
            string terrainName = HexOverworldTile.TerrainToString(tile.Terrain);
            Color terrainColor = TerrainVisualRegistry.Get(tile.Terrain).DominantColor;
            _overworldUi.UpdateTerrainDisplay(terrainName, terrainColor);
        }

        // 更新速度状态显示
        UpdateSpeedStatus();
    }

    /// <summary>更新速度状态 UI（显示当前移速和影响因素）</summary>
    private void UpdateSpeedStatus()
    {
        if (_overworldUi == null) return;

        float baseSpeed = PlayerMoveSpeed;
        float finalSpeed = baseSpeed;
        string status;

        if (PlayerParty?.SpeedComponent != null)
        {
            finalSpeed = PlayerParty.SpeedComponent.CalculateSpeed(_playerPixelPos);
            float ratio = finalSpeed / baseSpeed;

            if (ratio >= 1.2f)
                status = $"急行 ({ratio:P0})";
            else if (ratio >= 0.8f)
                status = "正常";
            else if (ratio >= 0.5f)
                status = $"缓行 ({ratio:P0})";
            else
                status = $"困难 ({ratio:P0})";
        }
        else
        {
            float ratio = WeatherSpeedFactor;
            if (ratio >= 0.9f)
                status = "正常";
            else
                status = $"减速 ({ratio:P0})";
        }

        // 追击警告
        if (_chasingEntity != null)
            status = $"⚠️ 被追击！";

        // 时间倍速
        if (GameTimeScale != 0.5f)
            status += $" [{GameTimeScale}x]";

        // 暂停
        if (IsTimePaused)
            status = "⏸ 暂停";

        _overworldUi.UpdateTopInfo(
            EconomyMgr.Year, EconomyMgr.Month, EconomyMgr.DaysPassed,
            EconomyMgr.GetSeasonName(), $"{EconomyMgr.CurrentHour:F0}:00",
            EconomyMgr.Gold, (int)EconomyMgr.Food, (int)EconomyMgr.MaxFood,
            status, "正常", 0);
    }
}
