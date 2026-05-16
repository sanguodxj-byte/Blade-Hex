// OriginQuestion.cs
// 起源选择问答数据模型 — 服务于架构优化 spec R4。
//
// 数据来源：BladeHexCore/src/Data/origin/origin_questions.json
// 加载器：OriginQuestionLoader
//
// 这些类型故意保持成 record / 普通 POCO（不继承 Godot.Resource），
// 便于 JSON 反序列化和 Core 层的纯逻辑访问。
using System.Collections.Generic;

namespace BladeHex.Data.Origin;

/// <summary>
/// 起源选择数据集 — 顶层数据结构。
/// </summary>
public sealed class OriginQuestionData
{
    public int Version { get; set; } = 1;

    /// <summary>每个种族的问题列表（按种族 id 索引）。</summary>
    public Dictionary<string, List<OriginQuestion>> Races { get; set; } = new();

    /// <summary>所有种族共享的"忠实伙伴"问题。</summary>
    public OriginQuestion CompanionQuestion { get; set; } = new();
}

/// <summary>一个起源问题。</summary>
public sealed class OriginQuestion
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public List<OriginChoice> Choices { get; set; } = new();
}

/// <summary>一个起源选项。</summary>
public sealed class OriginChoice
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";

    /// <summary>简短摘要（用于 UI 卡片、日志、物品/插图查表 key）。</summary>
    public string Summary { get; set; } = "";

    /// <summary>属性修正（key: str/dex/con/intel/wis/cha）。</summary>
    public Dictionary<string, int> AttrMods { get; set; } = new();

    /// <summary>奖励物品名（空=无）。</summary>
    public string ItemReward { get; set; } = "";

    /// <summary>关联插图 ID（res://assets/generated_origin_illust/{id}.png）。</summary>
    public string IllustId { get; set; } = "";

    /// <summary>是否为伙伴选择（用于 companion 字段）。仅 companionQuestion 中的选项使用。</summary>
    public bool IsCompanion { get; set; } = false;
}
