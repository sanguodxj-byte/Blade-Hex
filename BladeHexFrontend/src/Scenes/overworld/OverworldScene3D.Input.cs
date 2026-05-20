// OverworldScene3D.Input.cs
// 玩家输入与即时动作：快捷键派发、时间倍速、扎营、右键查看 Toast
using Godot;
using BladeHex.Map;
using BladeHex.View.Environment;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // 快捷键
    // ========================================

    private void HandleHotkeys(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        switch (key.Keycode)
        {
            case Key.Escape: // 关闭当前面板
                HandleEscapeKey();
                GetViewport().SetInputAsHandled();
                break;
            case Key.H: // 镜头回到玩家
                ForceCameraToPlayer();
                GetViewport().SetInputAsHandled();
                break;
            case Key.I: // 军队面板
            case Key.C: // 角色面板
            case Key.K: // 技能盘
            case Key.J: // 任务
            case Key.T: // 营地
            case Key.F: // 领地
                if (_camera != null) _camera.ExternalControl = true;
                _overworldUi?.HandleHotkey(key.Keycode switch
                {
                    Key.I => "army",
                    Key.C => "character",
                    Key.K => "skill_tree",
                    Key.J => "quests",
                    Key.T => "camp",
                    Key.F => "territory",
                    _ => ""
                });
                GetViewport().SetInputAsHandled();
                break;
            case Key.R: // 扎营休息
                if (!IsTimePaused && !_playerMoving)
                    DoCampRest();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Key1: // 时间 1x
            case Key.Key2: // 时间 2x
            case Key.Key3: // 时间 4x
            case Key.Key4: // 时间 8x
                CycleTimeSpeed(key.Keycode);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    // ========================================
    // 时间加速
    // ========================================

    private static readonly float[] TimeSpeedLevels = { 0.5f, 1.0f, 2.0f, 4.0f };
    private int _timeSpeedIndex = 0;

    private void CycleTimeSpeed(Key keycode)
    {
        _timeSpeedIndex = keycode switch
        {
            Key.Key1 => 0, // 0.5x（默认）
            Key.Key2 => 1, // 1x
            Key.Key3 => 2, // 2x
            Key.Key4 => 3, // 4x
            _ => _timeSpeedIndex,
        };

        GameTimeScale = TimeSpeedLevels[_timeSpeedIndex];
        string label = $"{GameTimeScale}x";
        GD.Print($"[Time] 时间流速: {label}");

        // 通知 UI 显示当前速度
        _overworldUi?.UpdateWeatherDisplay($"[{label}]");
        // 1秒后恢复天气显示
        GetTree().CreateTimer(1.0f).Timeout += () =>
        {
            if (_weatherMgr != null)
            {
                string weatherName = _weatherMgr.CurrentWeather switch
                {
                    BladeHex.View.Environment.WeatherType.Rain => "雨天",
                    BladeHex.View.Environment.WeatherType.Snow => "雪天",
                    BladeHex.View.Environment.WeatherType.Sandstorm => "沙尘暴",
                    _ => "晴天",
                };
                _overworldUi?.UpdateWeatherDisplay(weatherName);
            }
        };
    }

    // ========================================
    // 扎营休息
    // ========================================

    private void DoCampRest()
    {
        if (PlayerParty?.Roster == null || EconomyMgr == null) return;

        float food = EconomyMgr.Food;
        var result = CampSystem.Rest(PlayerParty.Roster, ref food, PlayerParty.Roster.Count);
        EconomyMgr.Food = food;

        if (result.Success)
        {
            EconomyMgr.AdvanceTime(result.HoursElapsed);
            GD.Print($"[Camp] {result.Message}");
        }
    }

    // ========================================
    // 右键 Toast：实体 / POI / 地形
    // ========================================

    private BladeHex.View.UI.Overworld.ToastNotification? _toast;

    private void InitToast()
    {
        _toast = new BladeHex.View.UI.Overworld.ToastNotification();
        _toast.Name = "ToastNotification";
        AddChild(_toast);
    }

    /// <summary>右键点击地图查看实体/POI/地形信息</summary>
    private void ShowInfoTooltip(Vector2 pixelPos)
    {
        // 1. 检查是否点击了实体
        if (EntityMgr != null)
        {
            foreach (var entity in EntityMgr.Entities)
            {
                if (!entity.IsAlive) continue;
                if (pixelPos.DistanceTo(entity.Position) < 200.0f)
                {
                    string info = $"{entity.EntityName}\n" +
                        $"等级: {entity.PartyLevel} | 兵力: {entity.PartySize}\n" +
                        $"态度: {(entity.IsHostileToPlayer ? "敌对" : "友好")}\n" +
                        $"状态: {entity.CurrentAIState}";
                    _toast?.Show(info);
                    return;
                }
            }
        }

        // 2. 检查是否点击了 POI
        foreach (var poi in WorldPois)
        {
            if (pixelPos.DistanceTo(poi.Position) < 300.0f)
            {
                string info = $"{poi.PoiName} ({poi.PoiTypeEnum})\n" +
                    $"繁荣: {poi.Prosperity}";
                _toast?.Show(info);
                return;
            }
        }

        // 3. 显示地形信息
        HexOverworldTile? tile = null;
        if (_chunkManager != null)
        {
            var coord = HexOverworldTile.PixelToAxial(pixelPos.X, pixelPos.Y);
            tile = _chunkManager.GetTile(coord.X, coord.Y);
        }
        else
        {
            tile = _grid.GetTileAtPixel(pixelPos.X, pixelPos.Y);
        }

        if (tile != null)
        {
            string terrainName = HexOverworldTile.TerrainToString(tile.Terrain);
            string roadInfo = tile.IsRoad ? " [道路]" : "";
            _toast?.Show($"{terrainName}{roadInfo}\n移动代价: {tile.MoveCost:F1}");
        }
    }

    // ========================================
    // ESC 关闭面板
    // ========================================

    private void HandleEscapeKey()
    {
        // 优先关闭二级面板
        if (_secondaryPanelRouter?.TryCloseActivePanel() == true) return;

        // 关闭城镇面板
        if (_townPanel?.IsPanelVisible() == true) { OnLeaveTown(); return; }

        // 关闭交互面板
        if (_interactionPanel?.IsPanelVisible() == true) { OnInteractionClosed(); return; }
    }
}
