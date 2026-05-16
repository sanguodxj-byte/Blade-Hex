// WorldEventEngine.cs
// 世界宏观事件引擎 — 每日 tick 驱动，评估和推进所有世界级事件
// 涵盖：国家战争、外交、经济、威胁升级、聚落兴衰、季节、新闻
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic.WorldEvents;

/// <summary>
/// 世界事件引擎 — 在每日 tick 中评估条件并推进事件。
/// 由 OverworldScene3D 在 DayPassed 回调中调用 Tick()。
/// </summary>
public class WorldEventEngine
{
    // ========================================
    // 全局状态
    // ========================================

    /// <summary>全局威胁等级 [0, 1]</summary>
    public float ThreatLevel { get; set; } = 0.1f;

    /// <summary>自上次清除巢穴以来的天数</summary>
    public int DaysSinceLastLairCleared { get; set; } = 0;

    /// <summary>当前活跃战争列表</summary>
    public List<WarState> ActiveWars { get; } = new();

    /// <summary>当前活跃联盟列表</summary>
    public List<AllianceState> ActiveAlliances { get; } = new();

    /// <summary>当前活跃饥荒</summary>
    public List<FamineState> ActiveFamines { get; } = new();

    /// <summary>新闻队列（最近 50 条）</summary>
    public List<NewsEntry> NewsQueue { get; } = new();

    /// <summary>外交关系矩阵 [nationA_id, nationB_id] → value (-100~+100)</summary>
    public Dictionary<string, int> DiplomaticRelations { get; } = new();

    /// <summary>玩家介入次数（按危机类型）</summary>
    public Dictionary<string, int> PlayerInterventions { get; } = new();

    /// <summary>当前游戏日</summary>
    public int CurrentDay { get; set; } = 1;

    private Random _rng = new();

    // ========================================
    // 主入口 — 每日 Tick
    // ========================================

    /// <summary>
    /// 每日调用一次 — 评估所有事件条件并推进。
    /// </summary>
    public void Tick(WorldTickContext ctx)
    {
        CurrentDay = ctx.CurrentDay;

        try
        {
            // 1. 威胁升级
            TickThreatLevel(ctx);

            // 2. 外交自然漂移（每 30 天）
            if (CurrentDay % 30 == 0)
                TickDiplomacy(ctx);

            // 3. 战争评估与推进
            TickWars(ctx);

            // 4. 联盟评估
            TickAlliances(ctx);

            // 5. 饥荒评估
            TickFamines(ctx);

            // 6. 聚落兴衰
            TickSettlements(ctx);

            // 7. 季节效应
            TickSeasonEffects(ctx);

            // 8. 经济波动
            TickEconomy(ctx);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[WorldEventEngine] Tick 异常: {ex.Message}");
        }
    }

    // ========================================
    // 1. 威胁升级
    // ========================================

    private void TickThreatLevel(WorldTickContext ctx)
    {
        DaysSinceLastLairCleared++;

        // 每 30 天没有清除巢穴，威胁+0.05
        if (DaysSinceLastLairCleared >= 30)
        {
            ThreatLevel = Math.Min(1.0f, ThreatLevel + 0.05f);
            DaysSinceLastLairCleared = 0;
            AddNews("world_threat", $"世界各地的怪物活动愈发猖獗...", Vector2.Zero);
        }

        // 威胁超 0.5：触发大规模入侵
        if (ThreatLevel > 0.5f && CurrentDay % 15 == 0 && _rng.Next(100) < 20)
        {
            AddNews("major_invasion", "一支庞大的怪物军团正在集结，准备入侵文明之地！", Vector2.Zero);
            // 实际生成由 EntityManager 根据 ThreatLevel 调整 spawn 率
        }

        // 威胁超 0.8：世界危机
        if (ThreatLevel > 0.8f && CurrentDay % 30 == 0 && _rng.Next(100) < 10)
        {
            AddNews("world_crisis", "传说中的远古巨兽已经苏醒！整个大陆都在颤抖！", Vector2.Zero);
        }
    }

    /// <summary>玩家清除巢穴时调用</summary>
    public void OnLairCleared()
    {
        ThreatLevel = Math.Max(0.0f, ThreatLevel - 0.03f);
        DaysSinceLastLairCleared = 0;
    }

    /// <summary>国家军队击败怪物时调用</summary>
    public void OnMonsterDefeatedByNation()
    {
        ThreatLevel = Math.Max(0.0f, ThreatLevel - 0.01f);
    }

    // ========================================
    // 2. 外交自然漂移
    // ========================================

    private void TickDiplomacy(WorldTickContext ctx)
    {
        if (ctx.Nations == null) return;

        for (int i = 0; i < ctx.Nations.Count; i++)
        {
            for (int j = i + 1; j < ctx.Nations.Count; j++)
            {
                var a = ctx.Nations[i];
                var b = ctx.Nations[j];
                string key = GetDiplomacyKey(a.Id, b.Id);
                int current = GetRelation(a.Id, b.Id);

                // 同种族亲和：简化为随机 +0~1
                int drift = _rng.Next(2);
                // 共享边界无冲突：+1（简化：都有领土就算）
                drift += 1;
                // 随机扰动 -1~+1
                drift += _rng.Next(-1, 2);

                DiplomaticRelations[key] = Math.Clamp(current + drift, -100, 100);
            }
        }
    }

    // ========================================
    // 3. 战争
    // ========================================

    private void TickWars(WorldTickContext ctx)
    {
        if (ctx.Nations == null) return;

        // 检查是否有新战争
        for (int i = 0; i < ctx.Nations.Count; i++)
        {
            for (int j = i + 1; j < ctx.Nations.Count; j++)
            {
                var a = ctx.Nations[i];
                var b = ctx.Nations[j];

                // 已经在打了？
                if (AreAtWar(a.Id, b.Id)) continue;
                // 联盟中不会开战
                if (AreAllied(a.Id, b.Id)) continue;

                int relation = GetRelation(a.Id, b.Id);
                if (relation < -60 && _rng.Next(100) < 5)
                {
                    DeclareWar(a, b);
                }
            }
        }

        // 推进现有战争
        foreach (var war in ActiveWars.ToList())
        {
            war.DaysSinceStart++;

            // 和平谈判（双方都损失惨重时）
            if (war.DaysSinceStart > 60 && _rng.Next(100) < 10)
            {
                EndWar(war);
            }
        }
    }

    private void DeclareWar(NationConfig a, NationConfig b)
    {
        ActiveWars.Add(new WarState { NationA = a.Id, NationB = b.Id, DaysSinceStart = 0 });
        SetRelation(a.Id, b.Id, -80);
        AddNews("war_declared", $"{a.DisplayName}与{b.DisplayName}之间爆发了战争！", Vector2.Zero);
    }

    private void EndWar(WarState war)
    {
        ActiveWars.Remove(war);
        SetRelation(war.NationA, war.NationB, -30);
        AddNews("peace", $"{war.NationA}与{war.NationB}签订了停战协议。", Vector2.Zero);
    }

    // ========================================
    // 4. 联盟
    // ========================================

    private void TickAlliances(WorldTickContext ctx)
    {
        if (ctx.Nations == null) return;

        for (int i = 0; i < ctx.Nations.Count; i++)
        {
            for (int j = i + 1; j < ctx.Nations.Count; j++)
            {
                var a = ctx.Nations[i];
                var b = ctx.Nations[j];

                if (AreAllied(a.Id, b.Id)) continue;
                if (AreAtWar(a.Id, b.Id)) continue;

                int relation = GetRelation(a.Id, b.Id);
                if (relation > 50 && _rng.Next(100) < 3)
                {
                    ActiveAlliances.Add(new AllianceState { NationA = a.Id, NationB = b.Id });
                    AddNews("alliance", $"{a.DisplayName}与{b.DisplayName}结成了同盟！", Vector2.Zero);
                }
            }
        }
    }

    // ========================================
    // 5. 饥荒
    // ========================================

    private void TickFamines(WorldTickContext ctx)
    {
        if (ctx.Nations == null || ctx.Pois == null) return;

        foreach (var nation in ctx.Nations)
        {
            // 已有饥荒？推进
            var existing = ActiveFamines.FirstOrDefault(f => f.NationId == nation.Id);
            if (existing != null)
            {
                existing.DurationDays++;
                // 繁荣度每日-2
                foreach (var poi in ctx.Pois.Where(p => p.OwningFaction == nation.Id))
                    poi.Prosperity = Math.Max(0, poi.Prosperity - 2);

                // 持续 30 天后结束（简化）
                if (existing.DurationDays > 30)
                {
                    ActiveFamines.Remove(existing);
                    AddNews("famine_end", $"{nation.DisplayName}的饥荒终于结束了。", Vector2.Zero);
                }
                continue;
            }

            // 检查是否触发饥荒：50%+ 农场被毁/围攻
            var farms = ctx.Pois.Where(p => p.OwningFaction == nation.Id && p.PoiTypeEnum == OverworldPOI.POIType.Farm).ToList();
            if (farms.Count == 0) continue;

            int destroyedFarms = farms.Count(f => f.Prosperity <= 0 || f.IsUnderSiege);
            if (destroyedFarms > farms.Count / 2)
            {
                ActiveFamines.Add(new FamineState { NationId = nation.Id, DurationDays = 0 });
                AddNews("famine_start", $"{nation.DisplayName}陷入了饥荒！农田荒芜，百姓流离失所。", Vector2.Zero);
            }
        }
    }

    // ========================================
    // 6. 聚落兴衰
    // ========================================

    private void TickSettlements(WorldTickContext ctx)
    {
        if (ctx.Pois == null) return;

        foreach (var poi in ctx.Pois)
        {
            // 自然恢复繁荣度（非战争/饥荒时）
            if (!poi.IsUnderSiege && poi.Prosperity < 50 && poi.Prosperity > 0)
            {
                // 冬季恢复减半
                int recovery = ctx.Season == 3 ? 0 : 1; // 3=Winter
                poi.Prosperity = Math.Min(100, poi.Prosperity + recovery);
            }
        }
    }

    // ========================================
    // 7. 季节效应
    // ========================================

    private void TickSeasonEffects(WorldTickContext ctx)
    {
        // 季节变化时的一次性效果（通过检查是否是季节第一天）
        if (CurrentDay % 90 != 1) return; // 粗略：每 90 天一季

        if (ctx.Season == 0) // Spring
        {
            // 春天：农场+5
            foreach (var poi in ctx.Pois?.Where(p => p.PoiTypeEnum == OverworldPOI.POIType.Farm) ?? [])
                poi.Prosperity = Math.Min(100, poi.Prosperity + 5);
            AddNews("season_spring", "春天来了，大地回暖，农田开始播种。", Vector2.Zero);
        }
        else if (ctx.Season == 2) // Fall
        {
            // 秋收：农场和村庄+10
            foreach (var poi in ctx.Pois?.Where(p =>
                p.PoiTypeEnum == OverworldPOI.POIType.Farm ||
                p.PoiTypeEnum == OverworldPOI.POIType.Village) ?? [])
                poi.Prosperity = Math.Min(100, poi.Prosperity + 10);
            AddNews("season_harvest", "秋收季节到来，谷仓堆满了粮食。", Vector2.Zero);
        }
        else if (ctx.Season == 3) // Winter
        {
            AddNews("season_winter", "严冬降临。道路泥泞，物资匮乏，旅行变得危险。", Vector2.Zero);
        }
    }

    // ========================================
    // 8. 经济波动（简化版）
    // ========================================

    private void TickEconomy(WorldTickContext ctx)
    {
        // 战争时涨价（由外部系统读取 ActiveWars 判断）
        // 这里只做新闻提示
    }

    // ========================================
    // 新闻系统
    // ========================================

    private void AddNews(string type, string description, Vector2 location)
    {
        NewsQueue.Add(new NewsEntry
        {
            Type = type,
            Description = description,
            Location = location,
            Day = CurrentDay,
        });

        // 保持最多 50 条
        while (NewsQueue.Count > 50)
            NewsQueue.RemoveAt(0);
    }

    /// <summary>获取玩家可见的新闻（按距离延迟过滤）</summary>
    public List<NewsEntry> GetVisibleNews(Vector2 playerPos, int maxCount = 5)
    {
        var visible = new List<NewsEntry>();
        foreach (var news in NewsQueue.AsEnumerable().Reverse())
        {
            // 延迟：1 天 / 2000px 距离（全局事件无延迟）
            if (news.Location != Vector2.Zero)
            {
                float dist = playerPos.DistanceTo(news.Location);
                int delay = (int)(dist / 2000.0f);
                if (CurrentDay - news.Day < delay) continue;
            }

            visible.Add(news);
            if (visible.Count >= maxCount) break;
        }
        return visible;
    }

    // ========================================
    // 外交工具方法
    // ========================================

    public int GetRelation(string nationA, string nationB)
    {
        string key = GetDiplomacyKey(nationA, nationB);
        return DiplomaticRelations.TryGetValue(key, out int val) ? val : 0;
    }

    public void SetRelation(string nationA, string nationB, int value)
    {
        DiplomaticRelations[GetDiplomacyKey(nationA, nationB)] = Math.Clamp(value, -100, 100);
    }

    public void AdjustRelation(string nationA, string nationB, int delta)
    {
        SetRelation(nationA, nationB, GetRelation(nationA, nationB) + delta);
    }

    public bool AreAtWar(string a, string b) => ActiveWars.Any(w =>
        (w.NationA == a && w.NationB == b) || (w.NationA == b && w.NationB == a));

    public bool AreAllied(string a, string b) => ActiveAlliances.Any(al =>
        (al.NationA == a && al.NationB == b) || (al.NationA == b && al.NationB == a));

    private static string GetDiplomacyKey(string a, string b) =>
        string.Compare(a, b, StringComparison.Ordinal) < 0 ? $"{a}|{b}" : $"{b}|{a}";

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary
        {
            ["threat_level"] = ThreatLevel,
            ["days_since_lair_cleared"] = DaysSinceLastLairCleared,
            ["current_day"] = CurrentDay,
        };

        var warsArr = new Godot.Collections.Array();
        foreach (var w in ActiveWars)
            warsArr.Add(new Godot.Collections.Dictionary { ["a"] = w.NationA, ["b"] = w.NationB, ["days"] = w.DaysSinceStart });
        data["wars"] = warsArr;

        var alliancesArr = new Godot.Collections.Array();
        foreach (var al in ActiveAlliances)
            alliancesArr.Add(new Godot.Collections.Dictionary { ["a"] = al.NationA, ["b"] = al.NationB });
        data["alliances"] = alliancesArr;

        var relationsDict = new Godot.Collections.Dictionary();
        foreach (var kvp in DiplomaticRelations)
            relationsDict[kvp.Key] = kvp.Value;
        data["relations"] = relationsDict;

        return data;
    }

    public void Deserialize(Godot.Collections.Dictionary data)
    {
        if (data == null) return;
        ThreatLevel = data.ContainsKey("threat_level") ? (float)data["threat_level"] : 0.1f;
        DaysSinceLastLairCleared = data.ContainsKey("days_since_lair_cleared") ? (int)data["days_since_lair_cleared"] : 0;
        CurrentDay = data.ContainsKey("current_day") ? (int)data["current_day"] : 1;

        ActiveWars.Clear();
        if (data.ContainsKey("wars"))
        {
            var warsArr = (Godot.Collections.Array)data["wars"];
            foreach (var item in warsArr)
            {
                var d = (Godot.Collections.Dictionary)item;
                ActiveWars.Add(new WarState { NationA = (string)d["a"], NationB = (string)d["b"], DaysSinceStart = (int)d["days"] });
            }
        }

        ActiveAlliances.Clear();
        if (data.ContainsKey("alliances"))
        {
            var arr = (Godot.Collections.Array)data["alliances"];
            foreach (var item in arr)
            {
                var d = (Godot.Collections.Dictionary)item;
                ActiveAlliances.Add(new AllianceState { NationA = (string)d["a"], NationB = (string)d["b"] });
            }
        }

        DiplomaticRelations.Clear();
        if (data.ContainsKey("relations"))
        {
            var dict = (Godot.Collections.Dictionary)data["relations"];
            foreach (var key in dict.Keys)
                DiplomaticRelations[(string)key] = (int)dict[key];
        }
    }
}

// ========================================
// 数据结构
// ========================================

/// <summary>每日 Tick 的上下文数据</summary>
public struct WorldTickContext
{
    public int CurrentDay;
    /// <summary>季节 (0=Spring, 1=Summer, 2=Fall, 3=Winter)</summary>
    public int Season;
    public List<NationConfig>? Nations;
    public List<OverworldPOI>? Pois;
    public Vector2 PlayerPosition;
}

/// <summary>战争状态</summary>
public class WarState
{
    public string NationA = "";
    public string NationB = "";
    public int DaysSinceStart;
}

/// <summary>联盟状态</summary>
public class AllianceState
{
    public string NationA = "";
    public string NationB = "";
}

/// <summary>饥荒状态</summary>
public class FamineState
{
    public string NationId = "";
    public int DurationDays;
}

/// <summary>新闻条目</summary>
public class NewsEntry
{
    public string Type = "";
    public string Description = "";
    public Vector2 Location;
    public int Day;
}
