// OverworldScene2D.UI.cs
// UI 系统 — 从 OverworldScene3D.UI.cs 迁移
// UI 已经是 CanvasLayer，可直接复用
using Godot;
using BladeHex.Map;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // UI 已经是 CanvasLayer，无需修改
    // OverworldUI 直接添加到场景即可

    /// <summary>每帧更新 UI 数据</summary>
    private void UpdateUIInfo()
    {
        if (_overworldUi == null || EconomyMgr == null) return;

        // 🌟 驱动顶部信息栏与日夜轮盘的旋转更新！
        _overworldUi.UpdateInfo(PlayerUnitData, EconomyMgr);

        // 更新地形显示（右上角）
        HexOverworldTile? tile = _mapAccess.GetActiveTileAtPixel(_playerPixelPos);

        if (tile != null)
        {
            string terrainName = HexOverworldTile.TerrainToString(tile.Terrain);
            Color terrainColor = TerrainVisualRegistry.Get(tile.Terrain).DominantColor;
            _overworldUi.UpdateTerrainDisplay(terrainName, terrainColor);
        }

        // 更新玩家移速（右下角动态显示）
        float playerSpeed = 300f;
        string? speedTooltip = null;
        if (PlayerParty?.SpeedComponent != null)
        {
            playerSpeed = PlayerParty.SpeedComponent.CalculateSpeed(PlayerParty.Position);
            var breakdown = PlayerParty.SpeedComponent.GetSpeedBreakdown(PlayerParty.Position);
            string terrainName = breakdown.ContainsKey("terrain_name") ? breakdown["terrain_name"].AsString() : "未知";
            speedTooltip = $"基础移速: {breakdown["base"]:F0}\n" +
                $"地形: {terrainName} (×{breakdown["terrain"]:F2})\n" +
                $"昼夜: ×{breakdown["day_night"]:F2}\n" +
                $"负重: ×{breakdown["encumbrance"]:F2}\n" +
                $"坐骑: ×{breakdown["mount"]:F2}\n" +
                $"技能: ×{breakdown["skill"]:F2}\n" +
                $"天气: ×{breakdown["weather"]:F2}\n" +
                $"控制区: {(breakdown["zoc_penalty"].AsSingle() < 1.0f ? "减速中" : "正常")} (×{breakdown["zoc_penalty"]:F2})";
        }
        _overworldUi.UpdatePlayerSpeed(playerSpeed, IsWaiting, speedTooltip);
    }
}
