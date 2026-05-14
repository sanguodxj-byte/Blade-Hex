// OverworldScene.Debug.cs
// [T-601] OverworldScene — 调试控制台分区 + 命令 + 瓦片拼接工具 partial class
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Debug;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.View.Environment;

namespace BladeHex.Scenes.Overworld;

// ========================================
//  Partial: 调试控制台 & 瓦片拼接工具
// ========================================
public partial class OverworldScene
{
    // ========================================
    // 地形编号 → 名称映射表 (与 HexOverworldTile.TerrainType 对齐)
    // ========================================
    private static readonly string[] TerrainNames = new[]
    {
        "DEEP_WATER", "SHALLOW_WATER", "SAND", "PLAINS",
        "GRASSLAND", "FOREST", "DENSE_FOREST", "JUNGLE",
        "TAIGA", "BOG", "SWAMP", "SAVANNA",
        "WASTELAND", "ROCKY", "HILLS", "MOUNTAIN",
        "MOUNTAIN_SNOW", "SNOW", "ICE", "ROAD", "RIVER"
    };

    // ========================================
    // 调试控制台 — 设置 / 拆卸
    // ========================================

    /// <summary>
    /// 向全局 DebugConsole 注册数据提供分区 & 命令
    /// </summary>
    public void SetupDebugConsole()
    {
        var dc = GetNodeOrNull<BladeHex.Debug.DebugConsole>("/root/DebugConsole");
        if (dc == null) return;

        // 注册数据分区
        dc.RegisterSection("地形", Callable.From((Func<Godot.Collections.Dictionary>)(DebugSectionTerrain)));
        dc.RegisterSection("迷雾", Callable.From((Func<Godot.Collections.Dictionary>)(DebugSectionFog)));
        dc.RegisterSection("玩家", Callable.From((Func<Godot.Collections.Dictionary>)(DebugSectionPlayer)));
        dc.RegisterSection("世界", Callable.From((Func<Godot.Collections.Dictionary>)(DebugSectionWorld)));

        // 注册命令 — 地图/迷雾
        dc.RegisterCommand("reveal_all", (_) => CmdRevealAll(), "揭示整张地图（所有区域变为已探索）", this);
        dc.RegisterCommand("toggle_fog", (_) => CmdToggleFog(), "切换迷雾开关（完全禁用/启用）", this);

        // 导航/传送
        dc.RegisterCommand("tp", (args) => CmdTeleport(args), "tp <x> <y> — 传送到像素坐标", this);
        dc.RegisterCommand("goto", (args) => CmdGoto(args), "goto <POI名> — 传送到指定 POI（模糊匹配）", this);
        dc.RegisterCommand("spawn_log", (_) => CmdPrintSpawns(), "列出所有 POI", this);

        // 天气
        dc.RegisterCommand("weather", (args) => CmdWeather(args), "weather <rain|snow|sand|clear> [light|moderate|heavy] — 切换天气", this);
        dc.RegisterCommand("weather_info", (_) => CmdWeatherInfo(), "显示当前天气详情", this);
        dc.RegisterCommand("weather_auto", (args) => CmdWeatherAuto(args), "weather_auto on|off — 切换自动天气循环", this);

        // 时间
        dc.RegisterCommand("time", (args) => CmdTime(args), "time <小时> — 设置时刻 (0-24)", this);
        dc.RegisterCommand("day", (args) => CmdDay(args), "day <天数> — 快进若干天", this);
        dc.RegisterCommand("speed", (args) => CmdSpeed(args), "speed <倍率> — 设置时间流速", this);

        // 经济
        dc.RegisterCommand("gold", (args) => CmdGold(args), "gold [数量] — 查看/设置金币", this);
        dc.RegisterCommand("food", (args) => CmdFood(args), "food [数量] — 查看/设置食物", this);

        // 玩家
        dc.RegisterCommand("heal", (_) => CmdHeal(), "全队回满 HP", this);
        dc.RegisterCommand("levelup", (args) => CmdLevelUp(args), "levelup [等级] — 全队升级到指定等级", this);

        // 实体
        dc.RegisterCommand("kill_all", (_) => CmdKillAll(), "清除所有敌对实体", this);
        dc.RegisterCommand("spawn", (args) => CmdSpawn(args), "spawn [类型] — 生成实体 (bandit/dragon/caravan/...)", this);
    }

    /// <summary>
    /// 场景退出时注销 DebugConsole 分区 & 命令
    /// GD: _exit_tree() (line 1339)
    /// </summary>
    public override void _ExitTree()
    {
        var dc = GetNodeOrNull<BladeHex.Debug.DebugConsole>("/root/DebugConsole");
        if (dc != null)
        {
            dc.UnregisterSectionsOf(this);
            dc.UnregisterCommandsOf(this);
        }
    }

    // ========================================
    // 分区数据提供器 (返回 Dictionary — GD DebugConsole 格式)
    // ========================================

    /// <summary>地形分区: 各类型瓦片数量统计 GD: _debug_section_terrain() (line 1350)</summary>
    private Godot.Collections.Dictionary DebugSectionTerrain()
    {
        var lines = new Godot.Collections.Array();
        if (HexGrid != null && HexGrid.Tiles.Count > 0)
        {
            var tc = new Dictionary<int, int>();
            foreach (var kvp in HexGrid.Tiles)
            {
                var t = kvp.Value;
                int tt = (int)t.Terrain;
                tc[tt] = tc.GetValueOrDefault(tt, 0) + 1;
            }

            lines.Add($"瓦片总数: {HexGrid.Tiles.Count}");
            var keys = tc.Keys.ToList();
            keys.Sort();
            foreach (int tt in keys)
            {
                string name = tt >= 0 && tt < TerrainNames.Length ? TerrainNames[tt] : $"UNKNOWN_{tt}";
                var c = DebugTerrainColor(tt);
                string hexColor = $"#{(int)(c.R * 255):x2}{(int)(c.G * 255):x2}{(int)(c.B * 255):x2}";
                lines.Add($"  [color={hexColor}]■[/color] {name}: {tc[tt]}");
            }
        }
        else
        {
            lines.Add("[color=red]HexGrid 为空[/color]");
        }
        return new Godot.Collections.Dictionary
        {
            { "title", "地形" },
            { "lines", lines }
        };
    }

    /// <summary>迷雾分区: 网格信息 + 探索进度 GD: _debug_section_fog() (line 1370)</summary>
    private Godot.Collections.Dictionary DebugSectionFog()
    {
        var lines = new Godot.Collections.Array();
        if (Fog != null)
        {
            lines.Add($"网格: {Fog.GridW} x {Fog.GridH} (cell={Fog.CellSize})");
            lines.Add($"地图像素: {Fog.MapWidthPx} x {Fog.MapHeightPx}");
            lines.Add($"探索进度: {Fog.GetExplorationProgress() * 100.0:F1}%");
            lines.Add($"视野: {Fog.VisionRange:F0} px x {Fog.ScoutMultiplier:F2}(scout)");
            lines.Add($"UNEXPLORED: {Fog.CountUnexplored}");
            lines.Add($"REVEALED:   {Fog.CountRevealed}");
            lines.Add($"IN_VISION:  {Fog.CountInVision}");
        }
        else
        {
            lines.Add("[color=red]Fog 为空[/color]");
        }
        return new Godot.Collections.Dictionary
        {
            { "title", "迷雾" },
            { "lines", lines }
        };
    }

    /// <summary>玩家分区: 位置 + 移动状态 GD: _debug_section_player() (line 1385)</summary>
    private Godot.Collections.Dictionary DebugSectionPlayer()
    {
        var lines = new Godot.Collections.Array();
        if (PlayerParty != null)
        {
            lines.Add($"位置: {PlayerParty.Position}");
            lines.Add($"移动中: {IsPlayerMoving()}");
        }
        else
        {
            lines.Add("[color=red]PlayerParty 为空[/color]");
        }
        return new Godot.Collections.Dictionary
        {
            { "title", "玩家" },
            { "lines", lines }
        };
    }

    /// <summary>世界分区: 天数 / POI / 实体 GD: _debug_section_world() (line 1394)</summary>
    private Godot.Collections.Dictionary DebugSectionWorld()
    {
        var lines = new Godot.Collections.Array();
        int dayCount = EconomyMgr?.DaysPassed ?? 0;
        lines.Add($"天数: {dayCount}");
        lines.Add($"POI: {WorldPois.Count}, 实体: {WorldEntities.Count}");
        lines.Add($"时间缩放: {GameTimeScale:F2}, 暂停: {IsTimePaused}");
        return new Godot.Collections.Dictionary
        {
            { "title", "世界" },
            { "lines", lines }
        };
    }

    // ========================================
    // 调试命令
    // ========================================

    /// <summary>揭示全图 GD: _cmd_reveal_all() (line 1403)</summary>
    private string CmdRevealAll()
    {
        if (Fog == null)
            return "Fog 未就绪";
        Fog.RevealAll();
        FogRenderer?.UpdateFog();
        return "已揭示全图";
    }

    /// <summary>切换迷雾开关（完全禁用/启用迷雾）</summary>
    private string CmdToggleFog()
    {
        if (Fog == null)
            return "Fog 未就绪";
        Fog.DisableFog = !Fog.DisableFog;
        FogRenderer?.UpdateFog();
        return Fog.DisableFog ? "迷雾已关闭 (全图可见)" : "迷雾已开启";
    }

    /// <summary>传送玩家至指定像素坐标 GD: _cmd_teleport() (line 1410)</summary>
    private string CmdTeleport(string[] args)
    {
        if (PlayerParty == null)
            return "PlayerParty 未就绪";
        if (args.Length < 2)
            return "用法: tp <x> <y>";
        if (!float.TryParse(args[0], out float x) || !float.TryParse(args[1], out float y))
            return "参数无效，需要数字";
        PlacePlayerAt(x, y);
        MainCamera.Position = PlayerParty.Position;
        return $"已传送至 ({x:F0}, {y:F0})";
    }

    /// <summary>打印所有 POI 信息 GD: _cmd_print_spawns() (line 1421)</summary>
    private string CmdPrintSpawns()
    {
        var lines = new List<string> { "POI 列表:" };
        foreach (var p in WorldPois)
        {
            lines.Add($"  [{p.PoiName}] {p.PoiTypeEnum} @ {p.Position}");
        }
        return string.Join("\n", lines);
    }

    // ========================================
    // 天气调试命令
    // ========================================

    /// <summary>
    /// 手动切换天气
    /// 用法: weather rain|snow|sand|clear [light|moderate|heavy]
    /// </summary>
    private string CmdWeather(string[] args)
    {
        if (WeatherMgr == null)
            return "WeatherManager 未就绪";

        if (args.Length == 0)
            return "用法: weather <rain|snow|sand|clear> [light|moderate|heavy]";

        string typeStr = args[0].ToLower();
        var weatherType = typeStr switch
        {
            "rain" or "雨" => BladeHex.View.Environment.WeatherType.Rain,
            "snow" or "雪" => BladeHex.View.Environment.WeatherType.Snow,
            "sand" or "sandstorm" or "沙尘" or "沙尘暴" => BladeHex.View.Environment.WeatherType.Sandstorm,
            "clear" or "晴" or "none" => BladeHex.View.Environment.WeatherType.Clear,
            _ => (BladeHex.View.Environment.WeatherType?)null,
        } ?? BladeHex.View.Environment.WeatherType.Clear;

        if (typeStr != "rain" && typeStr != "snow" && typeStr != "sand" && typeStr != "sandstorm"
            && typeStr != "clear" && typeStr != "none" && typeStr != "雨" && typeStr != "雪"
            && typeStr != "沙尘" && typeStr != "沙尘暴" && typeStr != "晴")
        {
            return $"未知天气类型: {typeStr}\n可选: rain, snow, sand, clear";
        }

        // 解析强度
        var intensity = BladeHex.View.Environment.WeatherIntensity.Moderate;
        if (args.Length >= 2)
        {
            intensity = args[1].ToLower() switch
            {
                "light" or "轻" => BladeHex.View.Environment.WeatherIntensity.Light,
                "moderate" or "中" => BladeHex.View.Environment.WeatherIntensity.Moderate,
                "heavy" or "强" or "大" => BladeHex.View.Environment.WeatherIntensity.Heavy,
                _ => BladeHex.View.Environment.WeatherIntensity.Moderate,
            };
        }

        WeatherMgr.SetWeatherImmediate(weatherType, intensity);

        // 直接驱动粒子系统（绕过信号，确保生效）
        if (_weatherParticles2D != null)
        {
            if (weatherType == BladeHex.View.Environment.WeatherType.Clear)
                _weatherParticles2D.StopAll();
            else
                _weatherParticles2D.SetWeather(weatherType, WeatherMgr.GetEffectiveIntensity());
        }

        string intensityName = intensity switch
        {
            BladeHex.View.Environment.WeatherIntensity.Light => "轻",
            BladeHex.View.Environment.WeatherIntensity.Moderate => "中",
            BladeHex.View.Environment.WeatherIntensity.Heavy => "强",
            _ => "中",
        };

        string weatherName = weatherType switch
        {
            BladeHex.View.Environment.WeatherType.Rain => "雨天",
            BladeHex.View.Environment.WeatherType.Snow => "雪天",
            BladeHex.View.Environment.WeatherType.Sandstorm => "沙尘暴",
            BladeHex.View.Environment.WeatherType.Clear => "晴天",
            _ => "未知",
        };

        return $"天气切换 → {weatherName} (强度: {intensityName})";
    }

    /// <summary>显示当前天气状态</summary>
    private string CmdWeatherInfo()
    {
        if (WeatherMgr == null)
            return "WeatherManager 未就绪";

        string weatherName = WeatherMgr.CurrentWeather switch
        {
            BladeHex.View.Environment.WeatherType.Rain => "雨天",
            BladeHex.View.Environment.WeatherType.Snow => "雪天",
            BladeHex.View.Environment.WeatherType.Sandstorm => "沙尘暴",
            BladeHex.View.Environment.WeatherType.Clear => "晴天",
            _ => "未知",
        };

        string intensityName = WeatherMgr.CurrentIntensity switch
        {
            BladeHex.View.Environment.WeatherIntensity.Light => "轻",
            BladeHex.View.Environment.WeatherIntensity.Moderate => "中",
            BladeHex.View.Environment.WeatherIntensity.Heavy => "强",
            _ => "中",
        };

        var lines = new List<string>
        {
            $"当前天气: {weatherName}",
            $"强度: {intensityName}",
            $"过渡中: {WeatherMgr.IsTransitioning}",
            $"过渡进度: {WeatherMgr.TransitionProgress:P0}",
            $"有效强度: {WeatherMgr.GetEffectiveIntensity():F2}",
            $"自动循环: {WeatherMgr.AutoCycleEnabled}",
            $"地面特效: {EnvironmentFx?.GetGroundIntensity():F2}",
        };

        return string.Join("\n", lines);
    }

    /// <summary>切换自动天气循环</summary>
    private string CmdWeatherAuto(string[] args)
    {
        if (WeatherMgr == null)
            return "WeatherManager 未就绪";

        if (args.Length == 0)
            return $"weather_auto = {(WeatherMgr.AutoCycleEnabled ? "on" : "off")}";

        string v = args[0].ToLower();
        WeatherMgr.AutoCycleEnabled = v == "on" || v == "1" || v == "true";
        return $"自动天气循环: {(WeatherMgr.AutoCycleEnabled ? "开启" : "关闭")}";
    }

    // ========================================
    // 时间调试命令
    // ========================================

    /// <summary>设置当前时刻 (0-24)</summary>
    private string CmdTime(string[] args)
    {
        if (EconomyMgr == null) return "EconomyManager 未就绪";
        if (args.Length == 0) return $"当前时刻: {EconomyMgr.CurrentHour:F1}h";
        if (!float.TryParse(args[0], out float hour)) return "参数无效，需要数字 (0-24)";
        hour = Mathf.Clamp(hour, 0.0f, 24.0f);
        EconomyMgr.CurrentHour = hour;
        return $"时刻设为 {hour:F1}h";
    }

    /// <summary>快进若干天</summary>
    private string CmdDay(string[] args)
    {
        if (EconomyMgr == null) return "EconomyManager 未就绪";
        if (args.Length == 0) return $"当前: 第{EconomyMgr.DaysPassed}天, {EconomyMgr.Month}月, {EconomyMgr.Year}年";
        if (!int.TryParse(args[0], out int days) || days <= 0) return "参数无效，需要正整数";
        for (int i = 0; i < days; i++)
        {
            EconomyMgr.AdvanceDay();
            EntityMgr?.OnDayPassed();
        }
        return $"快进 {days} 天 → 第{EconomyMgr.DaysPassed}天, {EconomyMgr.Month}月, {EconomyMgr.Year}年";
    }

    /// <summary>设置时间流速</summary>
    private string CmdSpeed(string[] args)
    {
        if (args.Length == 0) return $"当前时间流速: {GameTimeScale:F2}x";
        if (!float.TryParse(args[0], out float scale)) return "参数无效，需要数字";
        scale = Mathf.Clamp(scale, 0.0f, 100.0f);
        GameTimeScale = scale;
        return $"时间流速设为 {scale:F2}x";
    }

    // ========================================
    // 经济调试命令
    // ========================================

    /// <summary>设置金币</summary>
    private string CmdGold(string[] args)
    {
        if (EconomyMgr == null) return "EconomyManager 未就绪";
        if (args.Length == 0) return $"当前金币: {EconomyMgr.Gold}";
        if (!int.TryParse(args[0], out int amount)) return "参数无效，需要整数";
        int diff = amount - EconomyMgr.Gold;
        if (diff > 0) EconomyMgr.AddGold(diff);
        else EconomyMgr.Gold = amount; // 直接设置（允许减少）
        return $"金币设为 {EconomyMgr.Gold}";
    }

    /// <summary>设置食物</summary>
    private string CmdFood(string[] args)
    {
        if (EconomyMgr == null) return "EconomyManager 未就绪";
        if (args.Length == 0) return $"当前食物: {EconomyMgr.Food:F1}/{EconomyMgr.MaxFood:F1}";
        if (!float.TryParse(args[0], out float amount)) return "参数无效，需要数字";
        EconomyMgr.Food = Mathf.Clamp(amount, 0.0f, EconomyMgr.MaxFood);
        return $"食物设为 {EconomyMgr.Food:F1}";
    }

    // ========================================
    // 玩家调试命令
    // ========================================

    /// <summary>全队回满 HP</summary>
    private string CmdHeal()
    {
        if (PlayerParty?.Roster == null) return "PlayerParty 未就绪";
        var roster = PlayerParty.Roster;
        int count = 0;
        foreach (var member in roster.Members)
        {
            int max = member.BaseMaxHp;
            PartyRoster.SetCurrentHp(member, max);
            count++;
        }
        return $"已治愈 {count} 名队员至满血";
    }

    /// <summary>全队提升到指定等级</summary>
    private string CmdLevelUp(string[] args)
    {
        if (PlayerParty?.Roster == null) return "PlayerParty 未就绪";

        int targetLevel = 0;
        if (args.Length > 0 && int.TryParse(args[0], out int parsed))
            targetLevel = parsed;

        var roster = PlayerParty.Roster;
        int count = 0;
        foreach (var member in roster.Members)
        {
            if (targetLevel > 0)
            {
                // 升到指定等级
                while (member.Level < targetLevel)
                {
                    CampSystem.ApplyLevelUp(member);
                    count++;
                }
            }
            else
            {
                // 无参数：升一级
                CampSystem.ApplyLevelUp(member);
                count++;
            }
        }

        if (targetLevel > 0)
            return $"全队升至 Lv{targetLevel} (共 {count} 次升级)";
        return $"全队各升 1 级 ({roster.Members.Count} 人)";
    }

    // ========================================
    // 实体调试命令
    // ========================================

    /// <summary>清除所有敌对实体</summary>
    private string CmdKillAll()
    {
        if (EntityMgr == null) return "EntityMgr 未就绪";

        int killed = 0;
        var toRemove = new List<OverworldEntity>();
        foreach (var entity in EntityMgr.Entities)
        {
            if (entity.IsHostileToPlayer && entity.IsAlive)
            {
                toRemove.Add(entity);
                killed++;
            }
        }
        foreach (var entity in toRemove)
        {
            entity.IsAlive = false;
            EntityMgr.RemoveEntity(entity);
        }
        // 同步到 WorldEntities
        WorldEntities.RemoveAll(e => !e.IsAlive);
        return $"已清除 {killed} 个敌对实体";
    }

    /// <summary>
    /// 在玩家视野内生成实体
    /// 用法: spawn [类型]
    /// 类型: bandit, raider, goblin, kobold, caravan, adventurer, dragon, golem, lord, pirate
    /// 无参数默认生成 bandit
    /// </summary>
    private string CmdSpawn(string[] args)
    {
        if (EntityMgr == null || PlayerParty == null) return "EntityMgr/PlayerParty 未就绪";

        string typeStr = args.Length > 0 ? args[0].ToLower() : "bandit";

        // 解析实体类型
        var (entityType, faction, name, isHostile, monsterType) = typeStr switch
        {
            "bandit" or "山贼" => (OverworldEntity.EntityType.BanditParty, "bandit", "山贼队伍", true, ""),
            "raider" or "掠夺" => (OverworldEntity.EntityType.RaidingParty, "raider", "掠夺队", true, ""),
            "goblin" or "哥布林" => (OverworldEntity.EntityType.RaidingParty, "goblin", "哥布林小队", true, ""),
            "kobold" or "狗头人" => (OverworldEntity.EntityType.RaidingParty, "kobold", "狗头人巡逻队", true, ""),
            "robber" or "劫匪" => (OverworldEntity.EntityType.RobberParty, "robber", "劫匪队伍", true, ""),
            "pirate" or "海寇" => (OverworldEntity.EntityType.PirateCrew, "pirate", "海寇队伍", true, ""),
            "caravan" or "商队" => (OverworldEntity.EntityType.Caravan, "neutral", "商队", false, ""),
            "adventurer" or "冒险者" => (OverworldEntity.EntityType.Adventurer, "neutral", "冒险者队伍", false, ""),
            "dragon" or "龙" => (OverworldEntity.EntityType.EpicMonster, "monster", "巨龙", true, "dragon"),
            "golem" or "魔像" => (OverworldEntity.EntityType.EpicMonster, "monster", "远古魔像", true, "ancient_golem"),
            "lord" or "领主" => (OverworldEntity.EntityType.LordArmy, "player_faction", "领主军队", false, ""),
            _ => (OverworldEntity.EntityType.BanditParty, "bandit", $"未知({typeStr})", true, ""),
        };

        // 在玩家视野内随机位置生成（距离 300-800 像素）
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        float angle = rng.RandfRange(0.0f, Mathf.Tau);
        float dist = rng.RandfRange(300.0f, 800.0f);
        var spawnPos = PlayerParty.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

        // 创建实体
        var entity = new OverworldEntity();
        entity.EntityName = name;
        entity.EntityTypeEnum = entityType;
        entity.Position = spawnPos;
        entity.HomePosition = spawnPos;
        entity.TerritoryCenter = spawnPos;
        entity.Faction = faction;
        entity.IsHostileToPlayer = isHostile;
        entity.PartyLevel = PlayerParty.Roster?.Leader?.Level ?? 1;
        entity.PartySize = rng.RandiRange(3, 8);
        entity.CombatPower = entity.PartyLevel * entity.PartySize * 2.5f;
        entity.MoveSpeed = 150.0f + rng.RandfRange(0.0f, 100.0f);
        entity.PatrolRadius = 400.0f;
        entity.VisionRange = 400.0f;
        entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
        entity.IsAlive = true;

        if (!string.IsNullOrEmpty(monsterType))
            entity.MonsterType = monsterType;

        // 添加到实体管理器
        EntityMgr.Entities.Add(entity);
        WorldEntities.Add(entity);
        EntityMgr.EmitSignal(OverworldEntityManager.SignalName.EntitySpawned, entity);

        return $"已生成 [{name}] Lv{entity.PartyLevel} x{entity.PartySize} @ ({spawnPos.X:F0}, {spawnPos.Y:F0})";
    }

    // ========================================
    // 导航调试命令
    // ========================================

    /// <summary>传送到指定 POI</summary>
    private string CmdGoto(string[] args)
    {
        if (PlayerParty == null) return "PlayerParty 未就绪";
        if (args.Length == 0)
        {
            // 列出可用 POI
            var names = new List<string> { "可用 POI:" };
            foreach (var poi in WorldPois)
                names.Add($"  {poi.PoiName} ({poi.PoiTypeEnum})");
            return string.Join("\n", names);
        }

        string target = string.Join(" ", args);
        OverworldPOI? found = null;

        // 模糊匹配
        foreach (var poi in WorldPois)
        {
            if (poi.PoiName.Contains(target, StringComparison.OrdinalIgnoreCase))
            {
                found = poi;
                break;
            }
        }

        if (found == null)
            return $"未找到包含 \"{target}\" 的 POI";

        PlacePlayerAt(found.Position.X + 100.0f, found.Position.Y + 100.0f);
        MainCamera.Position = PlayerParty.Position;
        return $"已传送至 [{found.PoiName}] @ ({found.Position.X:F0}, {found.Position.Y:F0})";
    }

    // ========================================
    // 地形颜色映射 GD: _debug_terrain_color() (line 1427)
    // ========================================

    private static Color DebugTerrainColor(int terrainType)
    {
        return terrainType switch
        {
            0  => new Color(0.05f, 0.1f, 0.4f),   // DeepWater
            1  => new Color(0.1f, 0.3f, 0.7f),    // ShallowWater
            2  => new Color(0.9f, 0.85f, 0.5f),   // Sand
            3  => new Color(0.4f, 0.75f, 0.3f),   // Plains
            4  => new Color(0.3f, 0.7f, 0.25f),   // Grassland
            5  => new Color(0.15f, 0.5f, 0.15f),  // Forest
            6  => new Color(0.08f, 0.35f, 0.08f), // DenseForest
            10 => new Color(0.3f, 0.4f, 0.2f),    // Swamp
            11 => new Color(0.75f, 0.7f, 0.35f),  // Savanna
            14 => new Color(0.6f, 0.55f, 0.35f),  // Hills
            15 or 16 => new Color(0.5f, 0.45f, 0.4f), // Mountain / MountainSnow
            17 => new Color(0.9f, 0.93f, 0.98f),  // Snow
            19 => new Color(0.7f, 0.6f, 0.4f),    // Road
            20 => new Color(0.15f, 0.35f, 0.75f), // River
            _  => new Color(1.0f, 0.0f, 1.0f),    // Magenta fallback
        };
    }

    // ========================================
    // 瓦片拼接工具 — 字段
    // ========================================

    protected bool _tileAlignerActive = false;
    protected Node2D? _tileAlignerNode;
    protected List<Sprite2D> _tileSprites = new();
    protected List<Label> _tileLabels = new();
    protected int _selectedNeighbor = 1;
    protected bool _dragging = false;
    protected Vector2 _dragOffset = Vector2.Zero;
    protected Texture2D? _alignerTex;

    // ========================================
    // 瓦片拼接工具 — 初始化
    // ========================================

    /// <summary>创建 CanvasLayer + Node2D 容器 GD: _create_tile_aligner() (line 1466)</summary>
    public void SetupTileAligner()
    {
        var layer = new CanvasLayer();
        layer.Name = "TileAlignerLayer";
        layer.Layer = 9998;
        AddChild(layer);

        _tileAlignerNode = new Node2D();
        _tileAlignerNode.Name = "TileAligner";
        _tileAlignerNode.Visible = false;
        layer.AddChild(_tileAlignerNode);
    }

    /// <summary>切换对齐器可见性，需要时构建 GD: _toggle_tile_aligner() (line 1477)</summary>
    protected void ToggleTileAligner()
    {
        _tileAlignerActive = !_tileAlignerActive;
        if (_tileAlignerActive)
        {
            if (_tileAlignerNode == null)
                SetupTileAligner();
            BuildTileAligner();
            if (_tileAlignerNode != null)
                _tileAlignerNode.Visible = true;
            MainCamera.Zoom = new Vector2(0.8f, 0.8f);
        }
        else
        {
            if (_tileAlignerNode != null)
                _tileAlignerNode.Visible = false;
            MainCamera.Zoom = new Vector2(0.5f, 0.5f);
        }
    }

    // ========================================
    // 瓦片拼接工具 — 构建
    // ========================================

    /// <summary>创建 7 个精灵 (中心 + 6 个邻居) 及标签 GD: _build_tile_aligner() (line 1489)</summary>
    protected void BuildTileAligner()
    {
        if (_tileAlignerNode == null) return;

        // 清理旧内容
        foreach (Node child in _tileAlignerNode.GetChildren())
            child.QueueFree();
        _tileSprites.Clear();
        _tileLabels.Clear();

        // 直接加载纹理
        var tex = GD.Load<Texture2D>("res://src/assets/tiles/hex_terrain/grassland_0.png");
        if (tex == null)
        {
            GD.PushError("[TileAligner] 纹理加载失败");
            return;
        }

        _alignerTex = tex;
        float tw = tex.GetWidth();
        float th = tex.GetHeight();

        // 6 个邻居方向 (axial 偏移) — 平顶六边形
        var dirs = new Vector2I[]
        {
            new(1, 0), new(0, 1), new(-1, 1),
            new(-1, 0), new(0, -1), new(1, -1)
        };
        var dirNames = new[] { "E(+q)", "SE(+r)", "SW(-q+r)", "W(-q)", "NW(-r)", "NE(+q-r)" };

        // 间距 = 和渲染器一致的公式
        float halfW = tw / 2.0f;
        float sqrt3Half = 0.866025f;

        // 中心位置 = 屏幕中心
        var vp = GetViewport().GetVisibleRect().Size;
        var centerPos = new Vector2(vp.X / 2.0f, vp.Y / 2.0f);

        // ---- 中心瓦片 ----
        var s0 = new Sprite2D();
        s0.Texture = tex;
        s0.Centered = true;
        s0.Position = centerPos;
        s0.Modulate = new Color(1, 1, 0.5f);
        s0.ZIndex = 10;
        _tileAlignerNode.AddChild(s0);
        _tileSprites.Add(s0);

        var l0 = new Label();
        l0.Text = $"★ 中心(q=0,r=0) ★\n纹理: {tw:F0}x{th:F0}\n半宽: {halfW:F0}";
        l0.Position = centerPos + new Vector2(-80, -th / 2.0f - 45);
        l0.AddThemeFontSizeOverride("font_size", 15);
        l0.AddThemeColorOverride("font_color", new Color(1, 1, 0));
        _tileAlignerNode.AddChild(l0);
        _tileLabels.Add(l0);

        // ---- 6 个邻居，按 y 坐标排序渲染 (下方覆盖上方) ----
        var neighbors = new List<(int idx, Vector2 pos)>();
        for (int i = 0; i < 6; i++)
        {
            var d = dirs[i];
            float nx = centerPos.X + halfW * 1.5f * d.X;
            float ny = centerPos.Y + halfW * (sqrt3Half * d.X + sqrt3Half * 2.0f * d.Y);
            neighbors.Add((i, new Vector2(nx, ny)));
        }

        // 按 y 排序
        neighbors.Sort((a, b) => a.pos.Y.CompareTo(b.pos.Y));

        for (int ni = 0; ni < neighbors.Count; ni++)
        {
            var (origIdx, pos) = neighbors[ni];

            var s = new Sprite2D();
            s.Texture = tex;
            s.Centered = true;
            s.Position = pos;
            s.ZIndex = 20 + ni; // y 大的 z 高，覆盖上方
            s.Modulate = origIdx == _selectedNeighbor - 1 ? new Color(0.5f, 1, 0.5f) : new Color(1, 1, 1);
            _tileAlignerNode.AddChild(s);
            _tileSprites.Add(s);

            var d = dirs[origIdx];
            var l = new Label();
            l.Text = $"{origIdx + 1}: {dirNames[origIdx]} (q{d.X:+0;-#},r{d.Y:+0;-#})";
            l.Position = pos + new Vector2(-50, -th / 2.0f - 25);
            l.AddThemeFontSizeOverride("font_size", 13);
            l.AddThemeColorOverride("font_color", origIdx == _selectedNeighbor - 1 ? new Color(0.5f, 1, 0.5f) : new Color(1, 1, 1));
            _tileAlignerNode.AddChild(l);
            _tileLabels.Add(l);
        }

        // ---- 操作提示 ----
        var hint = new Label();
        hint.Text = "F6=退出 | 右键=选瓦片 | 拖拽=移动 | 滚轮=微调(Ctrl=纵向) | F7=输出数据";
        hint.Position = new Vector2(vp.X / 2.0f - 280, vp.Y - 50);
        hint.AddThemeFontSizeOverride("font_size", 16);
        hint.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0));
        _tileAlignerNode.AddChild(hint);
    }

    // ========================================
    // 瓦片拼接工具 — 输入处理
    // ========================================

    /// <summary>处理对齐器输入 (F6/F7/F9/鼠标) GD: _tile_aligner_input() (line 1585)</summary>
    public void HandleTileAlignerInput(InputEvent @event)
    {
        // F6 = 切换拼接模式 (在任何时候都要能响应)
        if (@event is InputEventKey f6Key && f6Key.Pressed && f6Key.Keycode == Key.F6)
        {
            ToggleTileAligner();
            return;
        }

        // F9 = 切换迷雾开关 (调试快捷键)
        if (@event is InputEventKey f9Key && f9Key.Pressed && f9Key.Keycode == Key.F9)
        {
            var result = CmdToggleFog();
            GD.Print($"[Debug] {result}");
            return;
        }

        if (!_tileAlignerActive)
            return;

        // F7 = 输出数据
        if (@event is InputEventKey f7Key && f7Key.Pressed && f7Key.Keycode == Key.F7)
        {
            OutputAlignerData();
            return;
        }

        // 右键 = 切换选中邻居
        if (@event is InputEventMouseButton rightBtn && rightBtn.Pressed && rightBtn.ButtonIndex == MouseButton.Right)
        {
            _selectedNeighbor = (_selectedNeighbor % 6) + 1;
            UpdateAlignerHighlight();
            return;
        }

        // 左键拖拽 (用屏幕坐标，因为瓦片在 CanvasLayer 上)
        if (@event is InputEventMouseButton leftBtn && leftBtn.ButtonIndex == MouseButton.Left)
        {
            if (leftBtn.Pressed)
            {
                var mouse = GetViewport().GetMousePosition();
                var s = _tileSprites[_selectedNeighbor];
                if (mouse.DistanceTo(s.Position) < 200.0f)
                {
                    _dragging = true;
                    _dragOffset = s.Position - mouse;
                }
            }
            else
            {
                _dragging = false;
            }
        }

        if (@event is InputEventMouseMotion motion && _dragging)
        {
            var mouse = GetViewport().GetMousePosition();
            var s = _tileSprites[_selectedNeighbor];
            s.Position = mouse + _dragOffset;
            // 更新标签位置
            if (_alignerTex != null)
                _tileLabels[_selectedNeighbor].Position = s.Position + new Vector2(-30, -_alignerTex.GetHeight() / 2.0f - 20);
        }

        // 滚轮微调 (InputEventMouseButton 同时覆盖左键和滚轮)
        if (@event is InputEventMouseButton wheelBtn)
        {
            float step = Input.IsKeyPressed(Key.Shift) ? 10.0f : 1.0f;
            var s = _tileSprites[_selectedNeighbor];

            if (wheelBtn.ButtonIndex == MouseButton.WheelUp)
            {
                if (Input.IsKeyPressed(Key.Ctrl))
                    s.Position -= new Vector2(0, step);
                else
                    s.Position += new Vector2(step, 0);
                if (_alignerTex != null)
                    _tileLabels[_selectedNeighbor].Position = s.Position + new Vector2(-30, -_alignerTex.GetHeight() / 2.0f - 20);
            }
            else if (wheelBtn.ButtonIndex == MouseButton.WheelDown)
            {
                if (Input.IsKeyPressed(Key.Ctrl))
                    s.Position += new Vector2(0, step);
                else
                    s.Position -= new Vector2(step, 0);
                if (_alignerTex != null)
                    _tileLabels[_selectedNeighbor].Position = s.Position + new Vector2(-30, -_alignerTex.GetHeight() / 2.0f - 20);
            }
        }
    }

    // ========================================
    // 瓦片拼接工具 — 辅助方法
    // ========================================

    /// <summary>更新高亮显示 GD: _update_aligner_highlight() (line 1643)</summary>
    protected void UpdateAlignerHighlight()
    {
        for (int i = 0; i < _tileSprites.Count; i++)
        {
            if (i == 0)
            {
                _tileSprites[i].Modulate = new Color(1, 1, 0.5f);
            }
            else if (i == _selectedNeighbor)
            {
                _tileSprites[i].Modulate = new Color(0.5f, 1, 0.5f);
                _tileLabels[i].AddThemeColorOverride("font_color", new Color(0.5f, 1, 0.5f));
            }
            else
            {
                _tileSprites[i].Modulate = new Color(1, 1, 1);
                _tileLabels[i].AddThemeColorOverride("font_color", new Color(1, 1, 1));
            }
        }
    }

    /// <summary>输出瓦片对齐数据到调试控制台 GD: _output_aligner_data() (line 1654)</summary>
    protected void OutputAlignerData()
    {
        if (_tileSprites.Count < 7 || _alignerTex == null)
            return;

        var center = _tileSprites[0].Position;
        var lines = new List<string>();

        lines.Add("[color=yellow][b]=== 瓦片拼接数据 (F7输出) ===[/b][/color]");
        lines.Add($"纹理尺寸: {_alignerTex.GetWidth()} x {_alignerTex.GetHeight()}");
        lines.Add("");

        // 方向名
        var dirNames = new[] { "右", "右下", "左下", "左", "左上", "右上" };
        var dirAxial = new Vector2I[]
        {
            new(1, 0), new(0, 1), new(-1, 1),
            new(-1, 0), new(0, -1), new(1, -1)
        };

        lines.Add("[color=cyan]相对偏移 (相对于中心):[/color]");
        for (int i = 0; i < 6; i++)
        {
            var rel = _tileSprites[i + 1].Position - center;
            var ax = dirAxial[i];
            lines.Add($"  {dirNames[i]} (q{ax.X:+0;-#},r{ax.Y:+0;-#}): dx={rel.X:F2}, dy={rel.Y:F2}");
        }

        lines.Add("");
        lines.Add("[color=cyan]代码 (可直接复制):[/color]");
        lines.Add("[code]");
        lines.Add("# 纹理实际尺寸");
        lines.Add($"const TEX_W := {_alignerTex.GetWidth()}");
        lines.Add($"const TEX_H := {_alignerTex.GetHeight()}");
        lines.Add("const HALF_W := TEX_W / 2.0");
        lines.Add("");
        lines.Add("# 平顶六边形间距 (axial_to_pixel)");
        lines.Add("static func tile_pixel_pos(q: int, r: int) -> Vector2:");

        // 分析数据推导公式
        var rel10 = _tileSprites[1].Position - center; // 方向 (1,0)
        var rel01 = _tileSprites[2].Position - center; // 方向 (0,1)

        float halfW = _alignerTex.GetWidth() / 2.0f;
        lines.Add($"  # (1,0)偏移: ({rel10.X:F2}, {rel10.Y:F2})");
        lines.Add($"  # (0,1)偏移: ({rel01.X:F2}, {rel01.Y:F2})");
        lines.Add($"  var x := HALF_W * {rel10.X / halfW:F6} * float(q)");
        lines.Add($"  var y := HALF_W * ({rel10.Y / halfW:F6} * float(q) + {rel01.Y / halfW:F6} * float(r))");
        lines.Add("  return Vector2(x, y)");
        lines.Add("[/code]");

        // 输出到调试控制台
        var dc = GetNodeOrNull("/root/DebugConsole");
        if (dc != null && dc.HasMethod("log_info"))
        {
            foreach (var l in lines)
                dc.Call("log_info", l);
        }
        else
        {
            GD.Print(string.Join("\n", lines));
        }
    }
}
