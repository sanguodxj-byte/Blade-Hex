// OverworldScene2D.Debug.cs
// 调试控制台命令注册 — 从 OverworldScene3D.Debug.cs 迁移
// 移除 3D 特定命令（云层、_playerMesh）
// 新增：性能监控命令 + chunk 流式加载控制
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.View.Environment;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // 性能监控状态
    // ========================================

    private bool _perfOverlayVisible = false;
    private float _perfUpdateTimer = 0.0f;
    private const float PERF_UPDATE_INTERVAL = 0.5f; // 每 0.5 秒更新一次

    // chunk 流式加载配置
    private int _chunkLoadRadius = 3; // 加载半径（chunk 数）
    private int _chunkUnloadRadius = 5; // 卸载半径（chunk 数）
    private bool _chunkStreamingEnabled = true;

    private void SetupDebugConsole()
    {
        var dc = BladeHex.Data.Globals.DebugConsole;
        if (dc == null) return;

        // 地图/迷雾
        dc.RegisterCommand("reveal_all", (_) =>
        {
            if (_fog == null) return "迷雾未初始化";
            _fog.RevealAll();
            LoadRevealedStreamingDecorationsInActiveRange();
            _fogOverlay?.FullUpdateFogMask();
            _minimap?.RebakeTerrain();
            return $"全图迷雾已揭示；当前已加载装饰层 {_streamedDecorationTileCoords.Count} tiles";
        }, "揭示整张地图", this);

        dc.RegisterCommand("toggle_fog", (_) =>
        {
            if (_fog == null) return "迷雾未初始化";
            _fog.DisableFog = !_fog.DisableFog;
            if (_fog.DisableFog)
            {
                _fog.UpdateVision(_playerPixelPos);
                LoadRevealedStreamingDecorationsInActiveRange();
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
                {
                    if (unit.Level < targetLevel)
                    {
                        int steps = targetLevel - unit.Level;
                        for (int i = 0; i < steps; i++)
                        {
                            BladeHex.Strategic.CampSystem.ApplyLevelUp(unit);
                        }
                    }
                    else
                    {
                        unit.Level = targetLevel;
                    }
                }
                return $"全队升级到 Lv{targetLevel}，并已获取相应的技能点与未分配属性点！";
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

        dc.RegisterCommand("spawn", CmdSpawn, "spawn <type> [name] — 在玩家附近生成指定类型实体\n  类型: adventurer, bandit, robber, pirate, raiding, caravan, lord, dragon, golem", this);

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

        // 地形分析
        dc.RegisterCommand("terrain_test", (args) =>
        {
            int seed = 12345;
            if (args.Length >= 1 && int.TryParse(args[0], out int s)) seed = s;
            return BladeHex.Tests.TerrainGenerationTest.RunAnalysis(seed, 21, 12);
        }, "terrain_test [seed] — 运行地形生成分析", this);

        // ========================================
        // 性能监控命令
        // ========================================

        dc.RegisterCommand("perf_stats", (_) =>
        {
            return GetPerformanceStats();
        }, "显示性能统计信息", this);

        dc.RegisterCommand("perf_overlay", (_) =>
        {
            _perfOverlayVisible = !_perfOverlayVisible;
            return $"性能覆盖层: {(_perfOverlayVisible ? "显示" : "隐藏")}";
        }, "切换性能覆盖层显示", this);

        dc.RegisterCommand("chunk_stats", (_) =>
        {
            return GetChunkStats();
        }, "显示 chunk 统计信息", this);

        dc.RegisterCommand("chunk_streaming", (args) =>
        {
            if (args.Length >= 1)
            {
                if (args[0].ToLower() == "on")
                {
                    _chunkStreamingEnabled = true;
                    ApplyChunkStreamingSettings();
                    return "Chunk 流式加载: 启用";
                }
                else if (args[0].ToLower() == "off")
                {
                    _chunkStreamingEnabled = false;
                    return "Chunk 流式加载: 禁用";
                }
                else if (args[0].ToLower() == "radius" && args.Length >= 2 && int.TryParse(args[1], out int radius))
                {
                    _chunkLoadRadius = Mathf.Clamp(radius, 1, 10);
                    _chunkUnloadRadius = _chunkLoadRadius + 2;
                    ApplyChunkStreamingSettings();
                    return $"Chunk 加载半径: {_chunkLoadRadius}, 卸载半径: {_chunkUnloadRadius}";
                }
            }
            return $"Chunk 流式加载: {(_chunkStreamingEnabled ? "启用" : "禁用")}, 加载半径: {_chunkLoadRadius}, 卸载半径: {_chunkUnloadRadius}";
        }, "chunk_streaming [on|off|radius <N>] — 控制 chunk 流式加载", this);

        dc.RegisterCommand("memory", (_) =>
        {
            return GetMemoryStats();
        }, "显示内存使用统计", this);

        GD.Print("[OverworldScene2D] 调试控制台: 命令已注册（含性能监控）");
    }

    // ========================================
    // 性能统计收集
    // ========================================

    private string GetPerformanceStats()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== 性能统计 ===");

        // FPS
        sb.AppendLine($"FPS: {Engine.GetFramesPerSecond()}");

        // 内存
        var memInfo = OS.GetMemoryInfo();
        long physical = (long)memInfo["physical"];
        long available = (long)memInfo["available"];
        sb.AppendLine($"物理内存: {physical / 1024 / 1024} MB");
        sb.AppendLine($"可用内存: {available / 1024 / 1024} MB");

        // 渲染统计
        sb.AppendLine($"--- 渲染 ---");
        sb.AppendLine($"Ground cache tiles: {_renderer?.LoadedTileCount ?? 0}");
        sb.AppendLine($"已加载装饰层 tile 坐标: {_streamedDecorationTileCoords.Count}");
        sb.AppendLine($"Prop 数量: {_propRenderer?.PropCount ?? 0}");
        sb.AppendLine($"Decal 数量: {_decalRenderer?.TotalCount ?? 0}");

        // Chunk 统计
        if (_chunkManager != null)
        {
            sb.AppendLine($"--- Chunk ---");
            sb.AppendLine($"活跃 chunk: {_chunkManager.ActiveChunks.Count}");
        }

        // 实体统计
        if (EntityMgr != null)
        {
            sb.AppendLine($"--- 实体 ---");
            sb.AppendLine($"实体总数: {EntityMgr.Entities.Count}");
        }

        return sb.ToString();
    }

    private string GetChunkStats()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Chunk 统计 ===");

        if (_chunkManager == null)
        {
            sb.AppendLine("ChunkManager 未初始化");
            return sb.ToString();
        }

        sb.AppendLine($"活跃 chunk: {_chunkManager.ActiveChunks.Count}");
        sb.AppendLine($"流式加载: {(_chunkStreamingEnabled ? "启用" : "禁用")}");
        sb.AppendLine($"加载半径: {_chunkLoadRadius}");
        sb.AppendLine($"卸载半径: {_chunkUnloadRadius}");

        // 列出活跃 chunk 坐标
        sb.AppendLine("--- 活跃 chunk 列表 ---");
        foreach (var kvp in _chunkManager.ActiveChunks)
        {
            sb.AppendLine($"  ({kvp.Key.X}, {kvp.Key.Y}): {kvp.Value.Tiles.Count} tiles");
        }

        return sb.ToString();
    }

    private string GetMemoryStats()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== 内存统计 ===");

        // 系统内存
        var memInfo = OS.GetMemoryInfo();
        long physical = (long)memInfo["physical"];
        long available = (long)memInfo["available"];
        sb.AppendLine($"物理内存: {physical / 1024 / 1024} MB");
        sb.AppendLine($"可用内存: {available / 1024 / 1024} MB");

        // 渲染器内存估算
        int chunkCount = _chunkManager?.ActiveChunks.Count ?? 0;
        int propCount = _propRenderer?.PropCount ?? 0;
        int decalCount = _decalRenderer?.TotalCount ?? 0;
        long groundDataBytes = _renderer?.TerrainDataTextureBytes ?? 0;
        long ashDataBytes = _ashController?.AshDataTextureBytes ?? 0;

        sb.AppendLine($"--- 渲染器 ---");
        sb.AppendLine($"Active chunks: {chunkCount}");
        sb.AppendLine($"Ground cache tiles: {_renderer?.LoadedTileCount ?? 0}");
        sb.AppendLine($"Decoration tiles: {_streamedDecorationTileCoords.Count}");
        sb.AppendLine($"Props: {propCount}");
        sb.AppendLine($"Decals: {decalCount}");
        sb.AppendLine($"Ground data texture: {groundDataBytes / 1024 / 1024} MB");
        sb.AppendLine($"Ash data texture: {ashDataBytes / 1024 / 1024} MB");

        return sb.ToString();
    }

    // ========================================
    // 实体生成命令
    // ========================================

    private string? CmdSpawn(string[] args)
    {
        if (args.Length == 0)
            return "用法: spawn <type> [name]\n  类型: adventurer, bandit, robber, pirate, raiding, caravan, lord, dragon, golem";

        if (EntityMgr == null)
            return "大地图场景未就绪";

        string type = args[0].ToLower();
        string customName = args.Length > 1 ? string.Join(" ", args[1..]) : "";

        // 随机偏移：在玩家附近 100~250 px 范围内生成
        float angle = (float)(GD.Randi() % 6283) / 1000.0f; // 0 ~ 2π
        float dist = 100.0f + (float)(GD.Randi() % 150);
        Vector2 spawnPos = _playerPixelPos + new Vector2(
            Mathf.Cos(angle) * dist,
            Mathf.Sin(angle) * dist
        );

        var entity = new OverworldEntity
        {
            Position = spawnPos,
            HomePosition = spawnPos,
            Faction = "hostile",
            IsHostileToPlayer = true,
            MoveSpeed = 130.0f,
            VisionRange = 350.0f,
            PatrolRadius = 300.0f,
            AIStrategy = AIStrategyEnum.Instinct,
            PartySize = 4,
            PartyLevel = 1,
        };

        switch (type)
        {
            case "adventurer":
                entity.EntityTypeEnum = OverworldEntity.EntityType.Adventurer;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "调试冒险者" : customName;
                entity.Faction = "adventurers";
                entity.IsHostileToPlayer = false;
                entity.AdventurerType = "veteran";
                entity.PartySize = 2 + (int)(GD.Randi() % 5);
                entity.PartyLevel = 1 + (int)(GD.Randi() % 3);
                entity.MoveSpeed = 150.0f;
                entity.GoldCarried = 30 + (int)(GD.Randi() % 100);
                entity.CombatPower = entity.PartySize * entity.PartyLevel * 2.0f;
                entity.AIStrategy = AIStrategyEnum.Tactical;
                break;

            case "bandit":
                entity.EntityTypeEnum = OverworldEntity.EntityType.BanditParty;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "山贼队伍" : customName;
                entity.PartySize = 4 + (int)(GD.Randi() % 6);
                entity.PartyLevel = 1 + (int)(GD.Randi() % 2);
                entity.CombatPower = entity.PartySize * entity.PartyLevel * 1.5f;
                entity.AIStrategy = AIStrategyEnum.Reckless;
                break;

            case "robber":
                entity.EntityTypeEnum = OverworldEntity.EntityType.RobberParty;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "劫匪队伍" : customName;
                entity.PartySize = 3 + (int)(GD.Randi() % 4);
                entity.PartyLevel = 1 + (int)(GD.Randi() % 3);
                entity.CombatPower = entity.PartySize * entity.PartyLevel * 1.5f;
                entity.AIStrategy = AIStrategyEnum.Cunning;
                break;

            case "pirate":
                entity.EntityTypeEnum = OverworldEntity.EntityType.PirateCrew;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "海寇队伍" : customName;
                entity.PartySize = 5 + (int)(GD.Randi() % 5);
                entity.PartyLevel = 2 + (int)(GD.Randi() % 3);
                entity.CombatPower = entity.PartySize * entity.PartyLevel * 1.5f;
                entity.AIStrategy = AIStrategyEnum.Berserk;
                break;

            case "raiding":
                entity.EntityTypeEnum = OverworldEntity.EntityType.RaidingParty;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "掠夺者队伍" : customName;
                entity.PartySize = 4 + (int)(GD.Randi() % 8);
                entity.PartyLevel = 1 + (int)(GD.Randi() % 3);
                entity.CombatPower = entity.PartySize * entity.PartyLevel * 1.5f;
                entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget;
                break;

            case "caravan":
                entity.EntityTypeEnum = OverworldEntity.EntityType.Caravan;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "商队" : customName;
                entity.Faction = "merchants";
                entity.IsHostileToPlayer = false;
                entity.PartySize = 3;
                entity.PartyLevel = 1;
                entity.MoveSpeed = 120.0f;
                entity.CombatPower = 5.0f;
                entity.TradeGoods = 50 + (int)(GD.Randi() % 150);
                entity.AIStrategy = AIStrategyEnum.Cautious;
                break;

            case "dragon":
                entity.EntityTypeEnum = OverworldEntity.EntityType.EpicMonster;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "远古赤龙" : customName;
                entity.MonsterType = "dragon";
                entity.PartyLevel = 5 + (int)(GD.Randi() % 4);
                entity.CombatPower = 30.0f + entity.PartyLevel * 5.0f;
                entity.MoveSpeed = 130.0f;
                entity.VisionRange = 500.0f;
                entity.PatrolRadius = 400.0f;
                entity.TerritoryCenter = spawnPos;
                entity.TerritoryRadius = 400.0f;
                entity.IsAggressive = false;
                entity.AIStrategy = AIStrategyEnum.Territorial;
                break;

            case "golem":
                entity.EntityTypeEnum = OverworldEntity.EntityType.EpicMonster;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "远古魔像" : customName;
                entity.MonsterType = "ancient_golem";
                entity.PartyLevel = 3 + (int)(GD.Randi() % 3);
                entity.CombatPower = 20.0f + entity.PartyLevel * 4.0f;
                entity.MoveSpeed = 100.0f;
                entity.VisionRange = 400.0f;
                entity.PatrolRadius = 300.0f;
                entity.TerritoryCenter = spawnPos;
                entity.TerritoryRadius = 350.0f;
                entity.IsAggressive = false;
                entity.AIStrategy = AIStrategyEnum.Territorial;
                break;

            case "lord":
                entity.EntityTypeEnum = OverworldEntity.EntityType.LordArmy;
                entity.EntityName = string.IsNullOrEmpty(customName) ? "调试领主" : customName;
                entity.Faction = "kingdom";
                entity.GarrisonSize = 15 + (int)(GD.Randi() % 20);
                entity.PartySize = entity.GarrisonSize;
                entity.PartyLevel = 3 + (int)(GD.Randi() % 3);
                entity.CombatPower = entity.PartySize * entity.PartyLevel * 2.0f;
                entity.MoveSpeed = 140.0f;
                entity.AIStrategy = AIStrategyEnum.Tactical;
                break;

            default:
                return $"未知实体类型: {type}\n可用类型: adventurer, bandit, robber, pirate, raiding, caravan, lord, dragon, golem";
        }

        EntityMgr.Entities.Add(entity);
        EntityMgr.Spatial.Add(entity);
        EntityMgr.InvalidateVisibleCache();
        EntityMgr.EmitSignal(OverworldEntityManager.SignalName.EntitySpawned, entity);

        GD.Print($"[Debug] 生成实体: {entity.EntityName} ({entity.GetTypeName()}) @ ({spawnPos.X:F0}, {spawnPos.Y:F0})");
        return $"已生成 [{entity.GetTypeName()}] {entity.EntityName}，位置 ({spawnPos.X:F0}, {spawnPos.Y:F0})";
    }

    // ========================================
    // 性能覆盖层更新
    // ========================================

    private void UpdatePerformanceOverlay(float dt)
    {
        if (!_perfOverlayVisible) return;

        _perfUpdateTimer += dt;
        if (_perfUpdateTimer < PERF_UPDATE_INTERVAL) return;
        _perfUpdateTimer = 0.0f;

        // 更新覆盖层显示（通过 _Draw 或 Label）
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_perfOverlayVisible) return;

        // 绘制性能覆盖层背景
        var bgRect = new Rect2(10, 10, 300, 200);
        DrawRect(bgRect, new Color(0, 0, 0, 0.7f));

        // 绘制性能信息
        var font = ThemeDB.FallbackFont;
        int fontSize = 14;
        float x = 20;
        float y = 30;
        float lineHeight = 20;

        DrawString(font, new Vector2(x, y), $"FPS: {Engine.GetFramesPerSecond():F0}", HorizontalAlignment.Left, -1, fontSize, Colors.White);
        y += lineHeight;

        DrawString(font, new Vector2(x, y), $"Chunks: {_chunkManager?.ActiveChunks.Count ?? 0}", HorizontalAlignment.Left, -1, fontSize, Colors.White);
        y += lineHeight;

        DrawString(font, new Vector2(x, y), $"Ground: {_renderer?.LoadedTileCount ?? 0}", HorizontalAlignment.Left, -1, fontSize, Colors.White);
        y += lineHeight;

        DrawString(font, new Vector2(x, y), $"Decor: {_streamedDecorationTileCoords.Count}", HorizontalAlignment.Left, -1, fontSize, Colors.White);
        y += lineHeight;

        DrawString(font, new Vector2(x, y), $"Props: {_propRenderer?.PropCount ?? 0}", HorizontalAlignment.Left, -1, fontSize, Colors.White);
        y += lineHeight;

        DrawString(font, new Vector2(x, y), $"Entities: {EntityMgr?.Entities.Count ?? 0}", HorizontalAlignment.Left, -1, fontSize, Colors.White);
        y += lineHeight;

        var memInfo = OS.GetMemoryInfo();
        long available = (long)memInfo["available"];
        DrawString(font, new Vector2(x, y), $"Memory: {available / 1024 / 1024} MB free", HorizontalAlignment.Left, -1, fontSize, Colors.White);
    }
}
