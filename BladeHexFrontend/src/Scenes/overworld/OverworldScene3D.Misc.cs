// OverworldScene3D.Misc.cs
// 杂项系统：快捷键、小地图、音频、存档、天气
using Godot;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.View.Environment;
using BladeHex.Strategic;
using BladeHex.Data;

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
        _overworldUi?.UpdateWeatherDisplay($"⏩ {label}");
        // 1秒后恢复天气显示
        GetTree().CreateTimer(1.0f).Timeout += () =>
        {
            if (_weatherMgr != null)
            {
                string weatherName = _weatherMgr.CurrentWeather switch
                {
                    BladeHex.View.Environment.WeatherType.Rain => "🌧 雨天",
                    BladeHex.View.Environment.WeatherType.Snow => "🌨 雪天",
                    BladeHex.View.Environment.WeatherType.Sandstorm => "🌪 沙尘暴",
                    _ => "☀ 晴天",
                };
                _overworldUi?.UpdateWeatherDisplay(weatherName);
            }
        };
    }

    /// <summary>扎营休息</summary>
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
    // 右键信息查看
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
    // 小地图
    // ========================================

    private BladeHex.View.UI.Overworld.MinimapPanel? _minimap;

    private void InitMinimap()
    {
        if (_fog == null)
        {
            GD.PrintErr("[OverworldScene3D] 小地图初始化跳过: 迷雾未就绪");
            return;
        }

        float mapW = _fog.MapWidthPx;
        float mapH = _fog.MapHeightPx;
        if (mapW <= 0 || mapH <= 0)
        {
            GD.PrintErr($"[OverworldScene3D] 小地图初始化跳过: 地图尺寸无效 ({mapW}×{mapH})");
            return;
        }

        var minimapLayer = new CanvasLayer { Layer = 5, Name = "MinimapLayer" };
        AddChild(minimapLayer);

        _minimap = new BladeHex.View.UI.Overworld.MinimapPanel();
        _minimap.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _minimap.OffsetLeft = -348;
        _minimap.OffsetTop = 60;
        _minimap.OffsetRight = -12;
        _minimap.OffsetBottom = 310;
        minimapLayer.AddChild(_minimap);

        // 真实初始化
        _minimap.Initialize(_fog, _chunkManager, WorldPois, mapW, mapH);

        // 连接小地图点击信号
        _minimap.MinimapClicked += OnMinimapClicked;
        _minimap.MinimapPoiClicked += OnMinimapPoiClicked;

        GD.Print($"[OverworldScene3D] 小地图初始化: {mapW}×{mapH}px");
    }

    private void OnMinimapClicked(Vector2 worldPos)
    {
        // 点击小地图 → 移动相机到该位置
        var world3D = CoordConverter.PixelToWorld3D(worldPos);
        _camera.FocusOn(world3D);
    }

    private void OnMinimapPoiClicked(Vector2 worldPos)
    {
        // 点击小地图 POI → 寻路到该位置
        StartPathfinding(worldPos);
    }

    private void UpdateMinimap()
    {
        if (_minimap == null) return;
        var viewportSize = GetViewport().GetVisibleRect().Size;
        // 用 3D 相机的正交 Size 模拟 2D zoom
        float orthoSize = _camera?.Size ?? 8.0f;
        float baseSize = 8.0f;
        float zoomFactor = baseSize / orthoSize;
        var fakeZoom = new Vector2(zoomFactor, zoomFactor);
        _minimap.UpdatePlayerAndCamera(_playerPixelPos, _playerPixelPos, fakeZoom, viewportSize);
    }

    // ========================================
    // 音频
    // ========================================

    private BladeHex.Audio.AudioManager? _audioManager;
    private BladeHex.Audio.EnvironmentAudioComponent? _envAudio;

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

    private float _biomeCheckTimer = 0f;

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

    // ========================================
    // 调试控制台
    // ========================================

    private void SetupDebugConsole()
    {
        var dc = BladeHex.Data.Globals.DebugConsole;
        if (dc == null) return;

        // 地图/迷雾
        // reveal_all 与探索揭示使用同一渲染管线：
        //   1) FogOfWar.RevealAll 标记所有 fog cell 为 Revealed
        //   2) RenderAllRevealedTiles 把所有已揭示 tile 推到 3D 渲染器
        //      （等价于探索时的 RevealNewTilesToRenderer，但全图扫描而非局部）
        //   3) FullUpdateFogMask 全量刷新 shader mask
        dc.RegisterCommand("reveal_all", (_) =>
        {
            if (_fog == null) return "迷雾未初始化";
            _fog.RevealAll();
            RenderAllRevealedTiles();
            _fogOverlay?.FullUpdateFogMask();
            return $"全图揭示完成: 已渲染 {_renderedTileCoords.Count} tiles";
        }, "揭示整张地图", this);

        dc.RegisterCommand("toggle_fog", (_) =>
        {
            if (_fog == null) return "迷雾未初始化";
            _fog.DisableFog = !_fog.DisableFog;
            // 关闭迷雾时全图变为可见 → 把所有 chunk 内 tile 推到渲染器
            if (_fog.DisableFog)
            {
                _fog.UpdateVision(_playerPixelPos); // 触发 DisableFog 分支让所有 cell 变 InVision
                RenderAllRevealedTiles();
            }
            _fogOverlay?.FullUpdateFogMask();
            return $"迷雾: {(_fog.DisableFog ? "禁用" : "启用")}";
        }, "切换迷雾开关", this);

        // 导航/传送
        dc.RegisterCommand("tp", (args) =>
        {
            if (args.Length >= 2 && float.TryParse(args[0], out float x) && float.TryParse(args[1], out float y))
            {
                _playerPixelPos = new Vector2(x, y);
                PlayerParty.Position = _playerPixelPos;
                if (_playerMesh != null)
                    _playerMesh.Position = CoordConverter.PixelToWorld3D(_playerPixelPos) + new Vector3(0, 0.4f, 0);
                ForceCameraToPlayer();
                return $"传送到 ({x}, {y})";
            }
            return "用法: tp <x> <y>";
        }, "tp <x> <y> — 传送到像素坐标", this);

        dc.RegisterCommand("goto", (args) =>
        {
            if (args.Length == 0) return "用法: goto <POI名>";
            string query = string.Join(" ", args).ToLower();
            foreach (var poi in WorldPois)
            {
                if (poi.PoiName.ToLower().Contains(query))
                {
                    _playerPixelPos = poi.Position + new Vector2(200, 0);
                    PlayerParty.Position = _playerPixelPos;
                    if (_playerMesh != null)
                        _playerMesh.Position = CoordConverter.PixelToWorld3D(_playerPixelPos) + new Vector3(0, 0.4f, 0);
                    ForceCameraToPlayer();
                    return $"传送到 {poi.PoiName}";
                }
            }
            return $"未找到 POI: {query}";
        }, "goto <POI名> — 传送到指定 POI", this);

        dc.RegisterCommand("spawn_log", (_) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"POI 总数: {WorldPois.Count}");
            foreach (var poi in WorldPois)
                sb.AppendLine($"  {poi.PoiName} ({poi.PoiTypeEnum}) @ ({poi.Position.X:F0},{poi.Position.Y:F0})");
            return sb.ToString();
        }, "列出所有 POI", this);

        // 时间
        dc.RegisterCommand("time", (args) =>
        {
            if (args.Length >= 1 && float.TryParse(args[0], out float hour) && EconomyMgr != null)
            {
                float diff = hour - EconomyMgr.CurrentHour;
                if (diff < 0) diff += 24;
                EconomyMgr.AdvanceTime(diff);
                return $"时间设为 {hour:F1}h";
            }
            return $"当前时间: {EconomyMgr?.CurrentHour:F1}h";
        }, "time <小时> — 设置时刻", this);

        dc.RegisterCommand("day", (args) =>
        {
            if (args.Length >= 1 && int.TryParse(args[0], out int days) && EconomyMgr != null)
            {
                EconomyMgr.AdvanceTime(days * 24.0f);
                return $"快进 {days} 天";
            }
            return $"当前天数: {EconomyMgr?.DaysPassed}";
        }, "day <天数> — 快进若干天", this);

        dc.RegisterCommand("speed", (args) =>
        {
            if (args.Length >= 1 && float.TryParse(args[0], out float spd))
                GameTimeScale = spd;
            return $"时间流速: {GameTimeScale}";
        }, "speed <倍率>", this);

        // 经济
        dc.RegisterCommand("gold", (args) =>
        {
            if (args.Length >= 1 && int.TryParse(args[0], out int amount) && EconomyMgr != null)
                EconomyMgr.Gold = amount;
            return $"金币: {EconomyMgr?.Gold}";
        }, "gold [数量]", this);

        dc.RegisterCommand("food", (args) =>
        {
            if (args.Length >= 1 && float.TryParse(args[0], out float amount) && EconomyMgr != null)
                EconomyMgr.Food = amount;
            return $"食物: {EconomyMgr?.Food:F1}";
        }, "food [数量]", this);

        // 玩家
        dc.RegisterCommand("heal", (_) =>
        {
            if (PlayerParty?.Roster != null)
            {
                foreach (var unit in PlayerParty.Roster.Members)
                    PartyRoster.SetCurrentHp(unit, unit.BaseMaxHp);
                return "全队回满 HP";
            }
            return "无队伍";
        }, "全队回满 HP", this);

        dc.RegisterCommand("levelup", (args) =>
        {
            int targetLevel = 5;
            if (args.Length >= 1 && int.TryParse(args[0], out int lv)) targetLevel = lv;
            if (PlayerParty?.Roster != null)
            {
                foreach (var unit in PlayerParty.Roster.Members)
                    unit.Level = targetLevel;
                return $"全队升级到 Lv{targetLevel}";
            }
            return "无队伍";
        }, "levelup [等级]", this);

        // 实体
        dc.RegisterCommand("kill_all", (_) =>
        {
            if (EntityMgr == null) return "无实体管理器";
            int count = 0;
            foreach (var entity in EntityMgr.Entities.ToArray())
            {
                if (entity.IsHostileToPlayer && entity.IsAlive)
                {
                    entity.IsAlive = false;
                    EntityMgr.RemoveEntity(entity);
                    count++;
                }
            }
            return $"清除 {count} 个敌对实体";
        }, "清除所有敌对实体", this);

        // 天气
        dc.RegisterCommand("weather", (args) =>
        {
            if (args.Length == 0)
            {
                var w = GetCurrentWeather();
                var i = GetCurrentWeatherIntensity();
                return $"当前天气: {w} (强度={i:F2}), 移速×{WeatherSpeedFactor:F2}, 视野×{WeatherVisionFactor:F2}, 遭遇×{WeatherEncounterFactor:F2}";
            }
            var type = args[0].ToLower() switch
            {
                "clear" or "晴" => WeatherType.Clear,
                "rain" or "雨" => WeatherType.Rain,
                "snow" or "雪" => WeatherType.Snow,
                "sand" or "沙" => WeatherType.Sandstorm,
                _ => WeatherType.Clear,
            };
            var intensity = WeatherIntensity.Moderate;
            if (args.Length >= 2)
            {
                intensity = args[1].ToLower() switch
                {
                    "light" or "轻" => WeatherIntensity.Light,
                    "heavy" or "重" => WeatherIntensity.Heavy,
                    _ => WeatherIntensity.Moderate,
                };
            }
            DebugSetWeather(type, intensity);
            return $"天气设为: {type} ({intensity})";
        }, "weather [clear/rain/snow/sand] [light/moderate/heavy]", this);

        // 云层
        dc.RegisterCommand("cloud", (args) =>
        {
            if (_cloudLayer == null) return "云层未初始化";
            if (args.Length == 0)
                return $"云层: visible={_cloudLayer.Visible}, coverage={_cloudLayer.CloudCoverage:F2}, opacity={_cloudLayer.CloudOpacity:F2}";

            string cmd = args[0].ToLower();
            if (cmd == "off") { _cloudLayer.SetEnabled(false); return "云层已关闭"; }
            if (cmd == "on") { _cloudLayer.SetEnabled(true); return "云层已开启"; }
            if (cmd == "coverage" && args.Length >= 2 && float.TryParse(args[1], out float cov))
            { _cloudLayer.SetCoverage(cov); return $"云覆盖率={cov:F2}"; }
            if (cmd == "opacity" && args.Length >= 2 && float.TryParse(args[1], out float opa))
            { _cloudLayer.SetOpacity(opa); return $"云不透明度={opa:F2}"; }
            if (cmd == "test")
            { _cloudLayer.SetCoverage(0.8f); _cloudLayer.SetOpacity(0.7f); return "云层测试模式: coverage=0.8 opacity=0.7"; }

            return "用法: cloud [off|on|test|coverage <值>|opacity <值>]";
        }, "cloud [off|on|test|coverage|opacity] — 云层控制", this);

        // 地形分析
        dc.RegisterCommand("terrain_test", (args) =>
        {
            int seed = 12345;
            if (args.Length >= 1 && int.TryParse(args[0], out int s)) seed = s;
            return BladeHex.Tests.TerrainGenerationTest.RunAnalysis(seed, 21, 12);
        }, "terrain_test [seed] — 运行地形生成分析", this);

        GD.Print("[OverworldScene3D] 调试控制台: 16 个命令已注册");
    }

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

    // ========================================
    // 存档
    // ========================================

    /// <summary>保存世界数据</summary>
    public void SaveWorldData()
    {
        if (_chunkManager == null || string.IsNullOrEmpty(_chunkSaveId)) return;

        int saved = _chunkManager.SaveAllToDisk(_chunkSaveId);
        ChunkPersistence.SavePois(_chunkSaveId, WorldPois);

        GD.Print($"[OverworldScene3D] 已保存: {saved} chunks, {WorldPois.Count} POIs");
    }
}
