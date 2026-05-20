// OverworldScene3D.Debug.cs
// 调试控制台命令注册（开发期 cheat）：地图揭示、传送、时间、经济、玩家、实体、天气、云层
using Godot;
using BladeHex.Strategic;
using BladeHex.View.Environment;
using BladeHex.View.Map;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
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
            _minimap?.RebakeTerrain();
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
            _minimap?.RebakeTerrain();
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
                    _playerMesh.Position = CoordConverter.PixelToWorld3D(_playerPixelPos) + new Vector3(0, GetGroundElevationAt(_playerPixelPos) + 0.4f, 0);
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
                        _playerMesh.Position = CoordConverter.PixelToWorld3D(_playerPixelPos) + new Vector3(0, GetGroundElevationAt(_playerPixelPos) + 0.4f, 0);
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
}
