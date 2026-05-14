// TriggerSystem.cs
// 条件触发框架 — 统一的触发类型定义、条件模型、触发历史
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

// ========================================
// 触发类型枚举
// ========================================

/// <summary>触发类型</summary>
public enum TriggerType
{
    Spatial,       // 空间触发 — 玩家进入新 Chunk
    Interaction,   // 交互触发 — 玩家主动接触
    Time,          // 时间触发 — 天数推进
    Chain,         // 连锁触发 — 前置事件完成
    Environment,   // 环境触发 — 地形 + 季节
}

// ========================================
// TriggerCondition — 触发条件定义
// ========================================

/// <summary>
/// 触发条件 — 描述一个可触发事件的前置条件和参数
/// </summary>
public class TriggerCondition
{
    /// <summary>条件唯一 ID（如 "spatial_wolf_forest"、"chain_kill_goblin_chief"）</summary>
    public string Id = "";

    /// <summary>触发类型</summary>
    public TriggerType Type = TriggerType.Spatial;

    /// <summary>优先级（数值越大越先判定）</summary>
    public float Priority = 0.0f;

    /// <summary>基础触发概率 (0~1)，交互触发固定为 1.0</summary>
    public float Chance = 1.0f;

    /// <summary>限定区域名称（空数组 = 任意区域）</summary>
    public string[] RequiredRegions = [];

    /// <summary>限定地形类型（空数组 = 任意地形）</summary>
    public Map.HexOverworldTile.TerrainType[] RequiredTerrains = [];

    /// <summary>最低玩家等级（0 = 无限制）</summary>
    public int MinPlayerLevel = 0;

    /// <summary>最早触发天数（0 = 无限制）</summary>
    public int MinDay = 0;

    /// <summary>最晚触发天数（0 = 无限制）</summary>
    public int MaxDay = 0;

    /// <summary>触发后冷却天数（0 = 不冷却，一次性）</summary>
    public int CooldownDays = 0;

    /// <summary>前置触发 ID（全部满足才可触发）</summary>
    public string[] PrerequisiteIds = [];

    /// <summary>互斥触发 ID（任一已触发则跳过）</summary>
    public string[] MutuallyExclusive = [];

    /// <summary>触发成功后解锁的后续触发 ID</summary>
    public string[] UnlockedIds = [];

    /// <summary>遭遇类型（用于空间/交互触发）</summary>
    public EncounterType EncounterType = EncounterType.WildMonsters;

    /// <summary>叙事文本（可选）</summary>
    public string Narrative = "";
}

// ========================================
// TriggerResult — 触发判定结果
// ========================================

/// <summary>
/// 触发判定结果
/// </summary>
public class TriggerResult
{
    /// <summary>对应的触发条件 ID</summary>
    public string TriggerId = "";

    /// <summary>是否触发成功</summary>
    public bool Triggered = false;

    /// <summary>生成的遭遇数据（如果有）</summary>
    public EncounterData? Encounter = null;

    /// <summary>解锁的后续触发 ID</summary>
    public string[] UnlockedIds = [];

    /// <summary>叙事文本</summary>
    public string Narrative = "";
}

// ========================================
// TriggerContext — 触发上下文
// ========================================

/// <summary>
/// 触发上下文 — 每次判定时传入的所有可能需要的信息
/// </summary>
public class TriggerContext
{
    /// <summary>世界种子</summary>
    public int WorldSeed = 0;

    /// <summary>当前天数 — 从 ITimeProvider 读取，未注册时返回 1</summary>
    public int CurrentDay => TimeProvider.CurrentDay;

    /// <summary>玩家等级</summary>
    public int PlayerLevel = 1;

    /// <summary>玩家全局坐标</summary>
    public Vector2I PlayerWorldPos = Vector2I.Zero;

    /// <summary>玩家所在 chunk 坐标</summary>
    public Vector2I PlayerChunk = Vector2I.Zero;

    /// <summary>当前 chunk（可为 null）</summary>
    public Map.ChunkData? CurrentChunk = null;

    /// <summary>当前区域名称</summary>
    public string CurrentRegion = "";

    /// <summary>当前地形类型</summary>
    public Map.HexOverworldTile.TerrainType CurrentTerrain = Map.HexOverworldTile.TerrainType.Plains;

    /// <summary>触发历史</summary>
    public TriggerHistory History = new();

    /// <summary>区域危险等级</summary>
    public float DangerLevel = 0.0f;
}

// ========================================
// TriggerHistory — 触发历史记录
// ========================================

/// <summary>
/// 触发历史 — 记录已触发的条件，用于冷却和连锁判定
/// </summary>
[GlobalClass]
public partial class TriggerHistory : RefCounted
{
    /// <summary>已触发记录: id → 触发天数</summary>
    public Dictionary<string, int> Triggered { get; private set; } = new();

    /// <summary>判断是否已触发过</summary>
    public bool IsTriggered(string id) => Triggered.ContainsKey(id);

    /// <summary>判断是否在冷却中</summary>
    public bool IsOnCooldown(string id, int currentDay, int cooldownDays)
    {
        if (!Triggered.TryGetValue(id, out int triggerDay)) return false;
        return (currentDay - triggerDay) < cooldownDays;
    }

    /// <summary>获取触发天数（null = 未触发）</summary>
    public int? GetTriggerDay(string id)
    {
        return Triggered.TryGetValue(id, out int day) ? day : null;
    }

    /// <summary>记录一次触发</summary>
    public void Record(string id, int day)
    {
        Triggered[id] = day;
    }

    /// <summary>移除一条记录（用于重置）</summary>
    public void Remove(string id)
    {
        Triggered.Remove(id);
    }

    /// <summary>清除所有记录</summary>
    public void Clear()
    {
        Triggered.Clear();
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary();
        foreach (var kv in Triggered)
            data[kv.Key] = kv.Value;
        return data;
    }

    public static TriggerHistory Deserialize(Godot.Collections.Dictionary data)
    {
        var history = new TriggerHistory();
        foreach (var key in data.Keys)
        {
            string id = (string)key;
            int day = (int)data[key];
            history.Triggered[id] = day;
        }
        return history;
    }
}
