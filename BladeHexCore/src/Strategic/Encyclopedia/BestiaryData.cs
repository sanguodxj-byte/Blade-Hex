// BestiaryData.cs
// 图鉴数据模型 — 野怪与传奇生物的静态描述数据
using System.Collections.Generic;

namespace BladeHex.Strategic.Encyclopedia;

/// <summary>普通生物条目</summary>
public class CreatureEntry
{
    /// <summary>生物类型 ID（对应 SettlementRace 或 EntityType 衍生）</summary>
    public string Id { get; set; } = "";
    /// <summary>显示名称</summary>
    public string Name { get; set; } = "";
    /// <summary>分类标签（野兽/亡灵/人形/魔物）</summary>
    public string Category { get; set; } = "";
    /// <summary>描述文字</summary>
    public string Description { get; set; } = "";
    /// <summary>威胁等级（1-5）</summary>
    public int ThreatLevel { get; set; } = 1;
    /// <summary>常见出没地形</summary>
    public string Habitat { get; set; } = "";
    /// <summary>战斗特征提示</summary>
    public string CombatHint { get; set; } = "";
}

/// <summary>传奇生物条目</summary>
public class LegendaryEntry
{
    /// <summary>传奇生物 ID</summary>
    public string Id { get; set; } = "";
    /// <summary>显示名称</summary>
    public string Name { get; set; } = "";
    /// <summary>分类标签</summary>
    public string Category { get; set; } = "";
    /// <summary>击败前的故事性描述（传闻/传说）</summary>
    public string LoreDescription { get; set; } = "";
    /// <summary>击败后解锁的完整描述</summary>
    public string FullDescription { get; set; } = "";
    /// <summary>威胁等级（4-5）</summary>
    public int ThreatLevel { get; set; } = 5;
    /// <summary>出没区域</summary>
    public string Habitat { get; set; } = "";
    /// <summary>击败后的战斗分析</summary>
    public string CombatAnalysis { get; set; } = "";
    /// <summary>掉落/奖励提示（击败后显示）</summary>
    public string RewardHint { get; set; } = "";
}

/// <summary>图鉴数据根</summary>
public class BestiaryDataRoot
{
    public List<CreatureEntry> Creatures { get; set; } = new();
    public List<LegendaryEntry> Legendaries { get; set; } = new();
}
